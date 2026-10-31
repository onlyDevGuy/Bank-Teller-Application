Imports BankTeller.Api.Services
Imports BankTeller.Contracts

Namespace Controllers

    <Route("api")>
    Public Class AccountsController
        Inherits TellerControllerBase

        Private ReadOnly _accounts As AccountService
        Private ReadOnly _audit As AuditLog
        Private ReadOnly _db As Data.Database

        Public Sub New(accounts As AccountService, audit As AuditLog, db As Data.Database)
            _accounts = accounts
            _audit = audit
            _db = db
        End Sub

        ''' <summary>Finds accounts by holder name or the start of an account number.</summary>
        <HttpGet("accounts")>
        Public Function Search(<FromQuery> query As String) As List(Of AccountDto)
            Return _accounts.Search(query)
        End Function

        <HttpGet("accounts/{number}")>
        Public Function GetAccount(number As String) As AccountDto
            Return _accounts.GetAccount(number)
        End Function

        <HttpPost("accounts")>
        Public Function Open(request As OpenAccountRequest) As ActionResult(Of AccountDto)
            Dim account = _accounts.Open(request, CurrentTeller())
            Return CreatedAtAction(NameOf(GetAccount), New With {.number = account.AccountNumber}, account)
        End Function

        ''' <summary>Most recent transactions first.</summary>
        <HttpGet("accounts/{number}/transactions")>
        Public Function History(number As String, <FromQuery> Optional limit As Integer = 50) As List(Of TransactionDto)
            Return _accounts.History(number, limit)
        End Function

        ''' <summary>Requires an Idempotency-Key header; a retry with the same key returns the original receipt.</summary>
        <HttpPost("accounts/{number}/deposits")>
        Public Function Deposit(number As String, request As CashRequest) As ReceiptDto
            Return _accounts.Deposit(number, request, CurrentTeller(), IdempotencyKey())
        End Function

        ''' <summary>Over the teller limit, include a supervisor's credentials in Approval.</summary>
        <HttpPost("accounts/{number}/withdrawals")>
        Public Function Withdraw(number As String, request As CashRequest) As ReceiptDto
            Return _accounts.Withdraw(number, request, CurrentTeller(), IdempotencyKey())
        End Function

        <HttpPost("transfers")>
        Public Function Transfer(request As TransferRequest) As ReceiptDto
            Return _accounts.Transfer(request, CurrentTeller(), IdempotencyKey())
        End Function

        <HttpPost("accounts/{number}/freeze")>
        <Authorize(Policy:="Supervisor")>
        Public Function Freeze(number As String, request As FreezeRequest) As AccountDto
            Return _accounts.SetStatus(number, AccountStatuses.Frozen, request?.Reason, CurrentTeller())
        End Function

        <HttpPost("accounts/{number}/unfreeze")>
        <Authorize(Policy:="Supervisor")>
        Public Function Unfreeze(number As String) As AccountDto
            Return _accounts.SetStatus(number, AccountStatuses.Active, Nothing, CurrentTeller())
        End Function

        ''' <summary>The signed-in teller's totals for today.</summary>
        <HttpGet("tellers/me/summary")>
        Public Function Summary() As TellerSummaryDto
            Return _accounts.Summary(CurrentTeller())
        End Function

        <HttpGet("audit")>
        <Authorize(Policy:="Supervisor")>
        Public Function Audit(<FromQuery> Optional limit As Integer = 100) As List(Of AuditEntryDto)
            Using connection = _db.Open()
                Return _audit.Recent(connection, Math.Clamp(limit, 1, 500))
            End Using
        End Function

    End Class

End Namespace
