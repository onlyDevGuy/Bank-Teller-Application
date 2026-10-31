Imports System.Security.Cryptography
Imports BankTeller.Api.Data
Imports BankTeller.Contracts
Imports Microsoft.Data.Sqlite

Namespace Services

    ' Accounts and the money movements on them. Every posting runs inside one
    ' SQLite write transaction (BEGIN IMMEDIATE), so balance checks and updates
    ' cannot interleave with another teller's posting on the same account.
    Public Class AccountService

        Private Const MaxReferenceLength As Integer = 60
        Private Const MaxKeyLength As Integer = 64

        Private ReadOnly _db As Database
        Private ReadOnly _audit As AuditLog
        Private ReadOnly _auth As AuthService
        Private ReadOnly _options As BankOptions
        Private ReadOnly _clock As TimeProvider

        Public Sub New(db As Database, audit As AuditLog, auth As AuthService, options As BankOptions, clock As TimeProvider)
            _db = db
            _audit = audit
            _auth = auth
            _options = options
            _clock = clock
        End Sub

        ' -- queries -----------------------------------------------------------

        Public Function Search(query As String) As List(Of AccountDto)
            query = If(query, String.Empty).Trim()
            If query.Length < 2 Then
                Throw BankException.BadRequest(ErrorCodes.InvalidRequest, "Type at least 2 characters of a name or account number.")
            End If

            Dim digits = AccountNumber.Normalise(query)
            Dim byNumber = digits.Length > 0 AndAlso digits.All(AddressOf Char.IsAsciiDigit)

            Using connection = _db.Open(), command = connection.CreateCommand()
                If byNumber Then
                    command.CommandText = AccountColumns & " WHERE account_number LIKE $pattern ORDER BY account_number LIMIT 25"
                    command.Bind("$pattern", digits & "%")
                Else
                    command.CommandText = AccountColumns & " WHERE holder_name LIKE $pattern ESCAPE '\' ORDER BY holder_name LIMIT 25"
                    command.Bind("$pattern", "%" & EscapeLike(query) & "%")
                End If
                Return ReadAccounts(command)
            End Using
        End Function

        Public Function GetAccount(number As String) As AccountDto
            number = ValidateAccountNumber(number)
            Using connection = _db.Open()
                Return ToDto(LoadAccount(connection, Nothing, number))
            End Using
        End Function

        Public Function History(number As String, limit As Integer) As List(Of TransactionDto)
            number = ValidateAccountNumber(number)
            limit = Math.Clamp(limit, 1, 200)
            Using connection = _db.Open()
                Dim account = LoadAccount(connection, Nothing, number)
                Using command = connection.CreateCommand()
                    command.CommandText = TransactionColumns & " WHERE t.account_id = $id ORDER BY t.id DESC LIMIT $limit"
                    command.Bind("$id", account.Id)
                    command.Bind("$limit", limit)
                    Return ReadTransactions(command)
                End Using
            End Using
        End Function

        Public Function Summary(teller As TellerContext) As TellerSummaryDto
            ' "Today" is the branch's local day, matching what the teller sees on the wall clock.
            Dim localNow = _clock.GetLocalNow()
            Dim startOfDay = New DateTimeOffset(localNow.Date, localNow.Offset)

            Dim result As New TellerSummaryDto With {.Date = DateOnly.FromDateTime(localNow.Date), .TellerLimit = _options.TellerLimit}
            Using connection = _db.Open(), command = connection.CreateCommand()
                command.CommandText = "
SELECT
    COALESCE(SUM(CASE WHEN type IN ('Deposit', 'Opening') THEN amount_cents END), 0),
    COALESCE(SUM(CASE WHEN type = 'Withdrawal' THEN amount_cents END), 0),
    COALESCE(SUM(CASE WHEN type = 'TransferOut' THEN amount_cents END), 0),
    COUNT(DISTINCT receipt_number)
FROM transactions
WHERE teller_id = $teller AND created_at >= $start"
                command.Bind("$teller", teller.Id)
                command.Bind("$start", ToTimestamp(startOfDay))
                Using reader = command.ExecuteReader()
                    reader.Read()
                    result.Deposits = Money.FromCents(reader.GetInt64(0))
                    result.Withdrawals = Money.FromCents(reader.GetInt64(1))
                    result.Transfers = Money.FromCents(reader.GetInt64(2))
                    result.TransactionCount = reader.GetInt32(3)
                End Using
            End Using
            Return result
        End Function

        ' -- account changes ---------------------------------------------------

        Public Function Open(request As OpenAccountRequest, teller As TellerContext) As AccountDto
            Dim holder = CleanText(request?.HolderName, 80)
            If holder.Length < 2 Then
                Throw BankException.BadRequest(ErrorCodes.InvalidRequest, "Enter the account holder's full name.")
            End If

            Dim openingCents As Long = 0
            If request.OpeningDeposit <> 0D Then openingCents = ValidateAmount(request.OpeningDeposit)

            Dim now = _clock.GetUtcNow()
            Using connection = _db.Open(), transaction = connection.BeginTransaction()
                Dim number As String
                Using command = connection.CreateCommand()
                    command.Transaction = transaction
                    command.CommandText = "SELECT COALESCE(MAX(id), 0) + 1 FROM accounts"
                    number = AccountNumber.Create(100_000_000L + CLng(command.ExecuteScalar()))
                End Using

                Dim accountId As Long
                Using command = connection.CreateCommand()
                    command.Transaction = transaction
                    command.CommandText = "
INSERT INTO accounts (account_number, holder_name, balance_cents, opened_at)
VALUES ($number, $holder, $balance, $now) RETURNING id"
                    command.Bind("$number", number)
                    command.Bind("$holder", holder)
                    command.Bind("$balance", openingCents)
                    command.Bind("$now", ToTimestamp(now))
                    accountId = CLng(command.ExecuteScalar())
                End Using

                If openingCents > 0 Then
                    InsertTransaction(connection, transaction, accountId, TransactionTypes.Opening, openingCents, openingCents,
                                      "Opening deposit", teller.Id, Nothing, NewReceiptNumber(now), Nothing, now)
                End If

                _audit.Write(connection, transaction, teller.Id, teller.Username, "Open account",
                             $"{number} for {holder}, opening deposit {Money.Format(Money.FromCents(openingCents))}", True, teller.RemoteIp)
                transaction.Commit()

                Return ToDto(LoadAccount(connection, Nothing, number))
            End Using
        End Function

        Public Function SetStatus(number As String, status As String, reason As String, supervisor As TellerContext) As AccountDto
            number = ValidateAccountNumber(number)
            reason = CleanText(reason, 120)
            If status = AccountStatuses.Frozen AndAlso reason.Length = 0 Then
                Throw BankException.BadRequest(ErrorCodes.InvalidRequest, "Give a reason for freezing the account.")
            End If

            Using connection = _db.Open(), transaction = connection.BeginTransaction()
                Dim account = LoadAccount(connection, transaction, number)
                Using command = connection.CreateCommand()
                    command.Transaction = transaction
                    command.CommandText = "UPDATE accounts SET status = $status WHERE id = $id"
                    command.Bind("$status", status)
                    command.Bind("$id", account.Id)
                    command.ExecuteNonQuery()
                End Using
                Dim action = If(status = AccountStatuses.Frozen, "Freeze account", "Unfreeze account")
                _audit.Write(connection, transaction, supervisor.Id, supervisor.Username, action,
                             If(reason.Length > 0, $"{number}: {reason}", number), True, supervisor.RemoteIp)
                transaction.Commit()
                Return ToDto(LoadAccount(connection, Nothing, number))
            End Using
        End Function

        ' -- postings ----------------------------------------------------------

        Public Function Deposit(number As String, request As CashRequest, teller As TellerContext, idempotencyKey As String) As ReceiptDto
            number = ValidateAccountNumber(number)
            Dim cents = ValidateAmount(request?.Amount)
            Dim reference = CleanText(request.Reference, MaxReferenceLength)

            Return Post(teller, idempotencyKey, cents, needsApproval:=False, approval:=Nothing, "deposit",
                Sub(connection, transaction, receipt, approverId, now)
                    Dim account = LoadActiveAccount(connection, transaction, number)
                    Dim balance = account.BalanceCents + cents
                    UpdateBalance(connection, transaction, account.Id, balance)
                    InsertTransaction(connection, transaction, account.Id, TransactionTypes.Deposit, cents, balance,
                                      reference, teller.Id, approverId, receipt, Nothing, now)
                    _audit.Write(connection, transaction, teller.Id, teller.Username, "Deposit",
                                 $"{Money.Format(Money.FromCents(cents))} to {number} (receipt {receipt})", True, teller.RemoteIp)
                End Sub)
        End Function

        Public Function Withdraw(number As String, request As CashRequest, teller As TellerContext, idempotencyKey As String) As ReceiptDto
            number = ValidateAccountNumber(number)
            Dim cents = ValidateAmount(request?.Amount)
            Dim reference = CleanText(request.Reference, MaxReferenceLength)

            Return Post(teller, idempotencyKey, cents, needsApproval:=True, approval:=request.Approval, "withdrawal",
                Sub(connection, transaction, receipt, approverId, now)
                    Dim account = LoadActiveAccount(connection, transaction, number)
                    EnsureFunds(account, cents)
                    Dim balance = account.BalanceCents - cents
                    UpdateBalance(connection, transaction, account.Id, balance)
                    InsertTransaction(connection, transaction, account.Id, TransactionTypes.Withdrawal, cents, balance,
                                      reference, teller.Id, approverId, receipt, Nothing, now)
                    _audit.Write(connection, transaction, teller.Id, teller.Username, "Withdrawal",
                                 $"{Money.Format(Money.FromCents(cents))} from {number} (receipt {receipt}){ApprovalNote(approverId)}",
                                 True, teller.RemoteIp)
                End Sub,
                Sub(connection) EnsureFunds(LoadActiveAccount(connection, Nothing, number), cents))
        End Function

        Public Function Transfer(request As TransferRequest, teller As TellerContext, idempotencyKey As String) As ReceiptDto
            Dim fromNumber = ValidateAccountNumber(request?.FromAccount)
            Dim toNumber = ValidateAccountNumber(request.ToAccount)
            If fromNumber = toNumber Then
                Throw BankException.BadRequest(ErrorCodes.SameAccount, "Choose two different accounts for a transfer.")
            End If
            Dim cents = ValidateAmount(request.Amount)
            Dim reference = CleanText(request.Reference, MaxReferenceLength)

            Return Post(teller, idempotencyKey, cents, needsApproval:=True, approval:=request.Approval, "transfer",
                Sub(connection, transaction, receipt, approverId, now)
                    Dim source = LoadActiveAccount(connection, transaction, fromNumber)
                    Dim target = LoadActiveAccount(connection, transaction, toNumber)
                    EnsureFunds(source, cents)

                    Dim sourceBalance = source.BalanceCents - cents
                    Dim targetBalance = target.BalanceCents + cents
                    UpdateBalance(connection, transaction, source.Id, sourceBalance)
                    UpdateBalance(connection, transaction, target.Id, targetBalance)
                    InsertTransaction(connection, transaction, source.Id, TransactionTypes.TransferOut, cents, sourceBalance,
                                      reference, teller.Id, approverId, receipt, target.Id, now)
                    InsertTransaction(connection, transaction, target.Id, TransactionTypes.TransferIn, cents, targetBalance,
                                      reference, teller.Id, approverId, receipt, source.Id, now)
                    _audit.Write(connection, transaction, teller.Id, teller.Username, "Transfer",
                                 $"{Money.Format(Money.FromCents(cents))} from {fromNumber} to {toNumber} (receipt {receipt}){ApprovalNote(approverId)}",
                                 True, teller.RemoteIp)
                End Sub,
                Sub(connection)
                    LoadActiveAccount(connection, Nothing, toNumber)
                    EnsureFunds(LoadActiveAccount(connection, Nothing, fromNumber), cents)
                End Sub)
        End Function

        Private Delegate Sub Posting(connection As SqliteConnection, transaction As SqliteTransaction,
                                     receipt As String, approverId As Long?, now As DateTimeOffset)

        ' Shared flow for every money movement: idempotency, supervisor approval, one write transaction.
        Private Function Post(teller As TellerContext, idempotencyKey As String, cents As Long, needsApproval As Boolean,
                              approval As SupervisorApproval, noun As String, work As Posting,
                              Optional precheck As Action(Of SqliteConnection) = Nothing) As ReceiptDto
            idempotencyKey = If(idempotencyKey, String.Empty).Trim()
            If idempotencyKey.Length = 0 OrElse idempotencyKey.Length > MaxKeyLength Then
                Throw BankException.BadRequest(ErrorCodes.IdempotencyKeyRequired,
                    "Send a unique Idempotency-Key header (up to 64 characters) with every transaction.")
            End If

            ' A retry of something already posted returns the original receipt, without re-checking approval.
            Dim replay = TryReplay(teller.Id, idempotencyKey)
            If replay IsNot Nothing Then Return replay

            ' Fail fast on rules that make approval pointless (unknown, frozen or underfunded
            ' account), so a supervisor is never called over for a transaction that cannot go
            ' through. The same checks run again inside the write transaction.
            If precheck IsNot Nothing Then
                Using connection = _db.Open()
                    precheck(connection)
                End Using
            End If

            ' Approval is checked before the write transaction starts: verifying a
            ' supervisor writes to the tellers table, and SQLite allows one writer at a time.
            Dim approverId As Long? = Nothing
            Dim limitCents As Long
            Money.TryToCents(_options.TellerLimit, limitCents)
            If needsApproval AndAlso Not teller.IsSupervisor AndAlso cents > limitCents Then
                If approval Is Nothing Then
                    Throw New BankException(403, ErrorCodes.ApprovalRequired,
                        $"A {noun} over {Money.Format(_options.TellerLimit)} needs a supervisor's approval.")
                End If
                approverId = _auth.VerifySupervisor(approval, teller.RemoteIp).Id
            End If

            Dim now = _clock.GetUtcNow()
            Using connection = _db.Open(), transaction = connection.BeginTransaction()
                Dim existing = FindReceiptForKey(connection, transaction, teller.Id, idempotencyKey)
                If existing IsNot Nothing Then
                    Return LoadReceipt(connection, transaction, existing, replayed:=True)
                End If

                Dim receipt = NewReceiptNumber(now)
                work(connection, transaction, receipt, approverId, now)

                Using command = connection.CreateCommand()
                    command.Transaction = transaction
                    command.CommandText = "
INSERT INTO idempotency_keys (teller_id, key, receipt_number, created_at) VALUES ($teller, $key, $receipt, $now)"
                    command.Bind("$teller", teller.Id)
                    command.Bind("$key", idempotencyKey)
                    command.Bind("$receipt", receipt)
                    command.Bind("$now", ToTimestamp(now))
                    command.ExecuteNonQuery()
                End Using

                transaction.Commit()
                Return LoadReceipt(connection, Nothing, receipt, replayed:=False)
            End Using
        End Function

        Private Function TryReplay(tellerId As Long, key As String) As ReceiptDto
            Using connection = _db.Open()
                Dim receipt = FindReceiptForKey(connection, Nothing, tellerId, key)
                Return If(receipt Is Nothing, Nothing, LoadReceipt(connection, Nothing, receipt, replayed:=True))
            End Using
        End Function

        ' -- validation --------------------------------------------------------

        Private Function ValidateAmount(amount As Decimal?) As Long
            If Not amount.HasValue OrElse amount.Value <= 0D Then
                Throw BankException.BadRequest(ErrorCodes.InvalidAmount, "Enter an amount greater than zero.")
            End If
            Dim cents As Long
            If Not Money.TryToCents(amount.Value, cents) Then
                Throw BankException.BadRequest(ErrorCodes.InvalidAmount, "Amounts can have at most two decimal places (cents).")
            End If
            If amount.Value > _options.MaxTransaction Then
                Throw BankException.BadRequest(ErrorCodes.InvalidAmount,
                    $"A single transaction cannot be more than {Money.Format(_options.MaxTransaction)}.")
            End If
            Return cents
        End Function

        Private Shared Function ValidateAccountNumber(number As String) As String
            If Not AccountNumber.IsValid(number) Then
                Throw BankException.BadRequest(ErrorCodes.InvalidAccountNumber,
                    $"'{CleanText(number, 20)}' is not a valid account number. Check the 10 digits.")
            End If
            Return AccountNumber.Normalise(number)
        End Function

        Private Shared Sub EnsureFunds(account As AccountRow, cents As Long)
            If cents > account.BalanceCents Then
                Throw BankException.Conflict(ErrorCodes.InsufficientFunds,
                    $"Insufficient funds. The available balance is {Money.Format(Money.FromCents(account.BalanceCents))}.")
            End If
        End Sub

        ' Trims, removes control characters and caps the length of free text.
        Private Shared Function CleanText(value As String, maxLength As Integer) As String
            If value Is Nothing Then Return String.Empty
            Dim cleaned = New String(value.Where(Function(c) Not Char.IsControl(c)).ToArray()).Trim()
            Return If(cleaned.Length > maxLength, cleaned.Substring(0, maxLength), cleaned)
        End Function

        Private Shared Function EscapeLike(value As String) As String
            Return value.Replace("\", "\\").Replace("%", "\%").Replace("_", "\_")
        End Function

        Private Function ApprovalNote(approverId As Long?) As String
            If Not approverId.HasValue Then Return String.Empty
            Return $", approved by {_auth.FindById(approverId.Value)?.Username}"
        End Function

        Private Shared Function NewReceiptNumber(now As DateTimeOffset) As String
            Return $"{now.UtcDateTime:yyMMdd}-{RandomNumberGenerator.GetHexString(6)}"
        End Function

        ' -- data access -------------------------------------------------------

        Private Const AccountColumns As String = "SELECT id, account_number, holder_name, balance_cents, status, opened_at FROM accounts"

        Private Const TransactionColumns As String = "
SELECT t.id, a.account_number, t.type, t.amount_cents, t.balance_after_cents, t.reference,
       teller.username, approver.username, counterparty.account_number, t.created_at, t.receipt_number
FROM transactions t
JOIN accounts a ON a.id = t.account_id
JOIN tellers teller ON teller.id = t.teller_id
LEFT JOIN tellers approver ON approver.id = t.approved_by_id
LEFT JOIN accounts counterparty ON counterparty.id = t.counterparty_id"

        Private Class AccountRow
            Public Property Id As Long
            Public Property Number As String
            Public Property Holder As String
            Public Property BalanceCents As Long
            Public Property Status As String
            Public Property OpenedAt As DateTimeOffset
        End Class

        Private Shared Function LoadAccount(connection As SqliteConnection, transaction As SqliteTransaction, number As String) As AccountRow
            Using command = connection.CreateCommand()
                command.Transaction = transaction
                command.CommandText = AccountColumns & " WHERE account_number = $number"
                command.Bind("$number", number)
                Using reader = command.ExecuteReader()
                    If Not reader.Read() Then
                        Throw BankException.NotFound($"No account {AccountNumber.Format(number)} exists.")
                    End If
                    Return ReadAccount(reader)
                End Using
            End Using
        End Function

        Private Shared Function LoadActiveAccount(connection As SqliteConnection, transaction As SqliteTransaction, number As String) As AccountRow
            Dim account = LoadAccount(connection, transaction, number)
            If account.Status <> AccountStatuses.Active Then
                Throw BankException.Conflict(ErrorCodes.AccountFrozen,
                    $"Account {AccountNumber.Format(number)} is frozen. A supervisor must unfreeze it before it can be used.")
            End If
            Return account
        End Function

        Private Shared Function ReadAccount(reader As SqliteDataReader) As AccountRow
            Return New AccountRow With {
                .Id = reader.GetInt64(0),
                .Number = reader.GetString(1),
                .Holder = reader.GetString(2),
                .BalanceCents = reader.GetInt64(3),
                .Status = reader.GetString(4),
                .OpenedAt = reader.GetTimestamp(5)
            }
        End Function

        Private Shared Function ReadAccounts(command As SqliteCommand) As List(Of AccountDto)
            Dim accounts As New List(Of AccountDto)
            Using reader = command.ExecuteReader()
                While reader.Read()
                    accounts.Add(ToDto(ReadAccount(reader)))
                End While
            End Using
            Return accounts
        End Function

        Private Shared Function ToDto(row As AccountRow) As AccountDto
            Return New AccountDto With {
                .AccountNumber = row.Number,
                .HolderName = row.Holder,
                .Balance = Money.FromCents(row.BalanceCents),
                .Status = row.Status,
                .OpenedAt = row.OpenedAt
            }
        End Function

        Private Shared Sub UpdateBalance(connection As SqliteConnection, transaction As SqliteTransaction, id As Long, balanceCents As Long)
            Using command = connection.CreateCommand()
                command.Transaction = transaction
                command.CommandText = "UPDATE accounts SET balance_cents = $balance WHERE id = $id"
                command.Bind("$balance", balanceCents)
                command.Bind("$id", id)
                command.ExecuteNonQuery()
            End Using
        End Sub

        Private Shared Sub InsertTransaction(connection As SqliteConnection, transaction As SqliteTransaction,
                                             accountId As Long, type As String, amountCents As Long, balanceAfterCents As Long,
                                             reference As String, tellerId As Long, approverId As Long?, receipt As String,
                                             counterpartyId As Long?, now As DateTimeOffset)
            Using command = connection.CreateCommand()
                command.Transaction = transaction
                command.CommandText = "
INSERT INTO transactions (account_id, type, amount_cents, balance_after_cents, reference, teller_id,
                          approved_by_id, receipt_number, counterparty_id, created_at)
VALUES ($account, $type, $amount, $balance, $reference, $teller, $approver, $receipt, $counterparty, $now)"
                command.Bind("$account", accountId)
                command.Bind("$type", type)
                command.Bind("$amount", amountCents)
                command.Bind("$balance", balanceAfterCents)
                command.Bind("$reference", If(String.IsNullOrEmpty(reference), Nothing, reference))
                command.Bind("$teller", tellerId)
                command.Bind("$approver", If(approverId.HasValue, CObj(approverId.Value), Nothing))
                command.Bind("$receipt", receipt)
                command.Bind("$counterparty", If(counterpartyId.HasValue, CObj(counterpartyId.Value), Nothing))
                command.Bind("$now", ToTimestamp(now))
                command.ExecuteNonQuery()
            End Using
        End Sub

        Private Shared Function FindReceiptForKey(connection As SqliteConnection, transaction As SqliteTransaction,
                                                  tellerId As Long, key As String) As String
            Using command = connection.CreateCommand()
                command.Transaction = transaction
                command.CommandText = "SELECT receipt_number FROM idempotency_keys WHERE teller_id = $teller AND key = $key"
                command.Bind("$teller", tellerId)
                command.Bind("$key", key)
                Return TryCast(command.ExecuteScalar(), String)
            End Using
        End Function

        Private Shared Function LoadReceipt(connection As SqliteConnection, transaction As SqliteTransaction,
                                            receipt As String, replayed As Boolean) As ReceiptDto
            Using command = connection.CreateCommand()
                command.Transaction = transaction
                command.CommandText = TransactionColumns & " WHERE t.receipt_number = $receipt ORDER BY t.id"
                command.Bind("$receipt", receipt)
                Return New ReceiptDto With {.ReceiptNumber = receipt, .Transactions = ReadTransactions(command), .Replayed = replayed}
            End Using
        End Function

        Private Shared Function ReadTransactions(command As SqliteCommand) As List(Of TransactionDto)
            Dim items As New List(Of TransactionDto)
            Using reader = command.ExecuteReader()
                While reader.Read()
                    items.Add(New TransactionDto With {
                        .Id = reader.GetInt64(0),
                        .AccountNumber = reader.GetString(1),
                        .Type = reader.GetString(2),
                        .Amount = Money.FromCents(reader.GetInt64(3)),
                        .BalanceAfter = Money.FromCents(reader.GetInt64(4)),
                        .Reference = reader.GetStringOrNothing(5),
                        .Teller = reader.GetString(6),
                        .ApprovedBy = reader.GetStringOrNothing(7),
                        .CounterpartyAccount = reader.GetStringOrNothing(8),
                        .CreatedAt = reader.GetTimestamp(9),
                        .ReceiptNumber = reader.GetString(10)
                    })
                End While
            End Using
            Return items
        End Function

    End Class

End Namespace
