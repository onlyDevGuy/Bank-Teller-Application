Imports System.Net
Imports System.Net.Http.Json
Imports BankTeller.Api.Services
Imports BankTeller.Contracts
Imports Xunit

Public Class ApiTests
    Implements IDisposable

    Private ReadOnly _api As New BankApiFactory()
    Private ReadOnly _teller As TellerContext
    Private ReadOnly _supervisor As TellerContext

    Public Sub New()
        _teller = _api.CreateTeller("teller", Roles.Teller)
        _supervisor = _api.CreateTeller("super", Roles.Supervisor)
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        _api.Dispose()
    End Sub

    Private Shared ReadOnly Property Cancel As Threading.CancellationToken
        Get
            Return TestContext.Current.CancellationToken
        End Get
    End Property

    ' -- authentication ------------------------------------------------------

    <Fact>
    Public Async Function EndpointsRequireASignedInTeller() As Task
        Dim response = Await _api.CreateClient().GetAsync("/api/tellers/me/summary", Cancel)
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode)
    End Function

    <Fact>
    Public Async Function UnknownUserAndWrongPasswordGetTheSameAnswer() As Task
        Dim client = _api.CreateClient()
        Dim unknown = Await client.PostAsJsonAsync("/api/auth/login", New LoginRequest With {.Username = "nobody", .Password = "x"}, Cancel)
        Dim wrong = Await client.PostAsJsonAsync("/api/auth/login", New LoginRequest With {.Username = "teller", .Password = "x"}, Cancel)

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode)
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode)
        Assert.Equal(Await unknown.Content.ReadAsStringAsync(Cancel), Await wrong.Content.ReadAsStringAsync(Cancel))
    End Function

    <Fact>
    Public Async Function FiveWrongPasswordsLockTheSignInUntilTheLockoutEnds() As Task
        Dim client = _api.CreateClient()
        For i = 1 To 4
            Dim attempt = Await client.PostAsJsonAsync("/api/auth/login", New LoginRequest With {.Username = "teller", .Password = "wrong"}, Cancel)
            Assert.Equal(HttpStatusCode.Unauthorized, attempt.StatusCode)
        Next
        Dim fifth = Await client.PostAsJsonAsync("/api/auth/login", New LoginRequest With {.Username = "teller", .Password = "wrong"}, Cancel)
        Assert.Equal(CType(423, HttpStatusCode), fifth.StatusCode)

        ' Even the right password is refused while locked...
        Dim locked = Await client.PostAsJsonAsync("/api/auth/login", New LoginRequest With {.Username = "teller", .Password = BankApiFactory.Password}, Cancel)
        Assert.Equal(ErrorCodes.LockedOut, Await locked.ProblemCode())

        ' ...and accepted once the 15 minutes have passed.
        _api.Clock.Advance(TimeSpan.FromMinutes(16))
        Dim later = Await client.PostAsJsonAsync("/api/auth/login", New LoginRequest With {.Username = "teller", .Password = BankApiFactory.Password}, Cancel)
        Assert.Equal(HttpStatusCode.OK, later.StatusCode)
    End Function

    ' -- postings ------------------------------------------------------------

    <Fact>
    Public Async Function DepositUpdatesBalanceAndHistory() As Task
        Dim number = _api.OpenAccount("Ayanda Nkosi", 100D, _teller)
        Dim client = Await _api.SignedInClient("teller")

        Dim response = Await client.PostMoneyAsync($"/api/accounts/{number}/deposits", New CashRequest With {.Amount = 50.25D, .Reference = "Cash"})
        response.EnsureSuccessStatusCode()
        Dim receipt = Await response.Content.ReadFromJsonAsync(Of ReceiptDto)(Cancel)

        Assert.Equal(150.25D, receipt.Transactions.Single().BalanceAfter)
        Dim history = Await client.GetFromJsonAsync(Of List(Of TransactionDto))($"/api/accounts/{number}/transactions", Cancel)
        Assert.Equal({TransactionTypes.Deposit, TransactionTypes.Opening}, history.Select(Function(t) t.Type))
    End Function

    <Fact>
    Public Async Function RetryingWithTheSameKeyDoesNotPostTwice() As Task
        Dim number = _api.OpenAccount("Ayanda Nkosi", 100D, _teller)
        Dim client = Await _api.SignedInClient("teller")
        Dim body As New CashRequest With {.Amount = 10D}

        Dim first = Await (Await client.PostMoneyAsync($"/api/accounts/{number}/deposits", body, "key-1")).Content.ReadFromJsonAsync(Of ReceiptDto)(Cancel)
        Dim second = Await (Await client.PostMoneyAsync($"/api/accounts/{number}/deposits", body, "key-1")).Content.ReadFromJsonAsync(Of ReceiptDto)(Cancel)

        Assert.Equal(first.ReceiptNumber, second.ReceiptNumber)
        Assert.True(second.Replayed)
        Assert.Equal(110D, _api.Accounts().GetAccount(number).Balance)
    End Function

    <Fact>
    Public Async Function MoneyRequestsWithoutAKeyAreRejected() As Task
        Dim number = _api.OpenAccount("Ayanda Nkosi", 100D, _teller)
        Dim client = Await _api.SignedInClient("teller")
        Dim response = Await client.PostAsJsonAsync($"/api/accounts/{number}/deposits", New CashRequest With {.Amount = 10D}, Cancel)
        Assert.Equal(ErrorCodes.IdempotencyKeyRequired, Await response.ProblemCode())
    End Function

    <Theory>
    <InlineData("0")>
    <InlineData("-5")>
    <InlineData("1.005")>
    <InlineData("1000000.01")>
    Public Async Function InvalidAmountsAreRejected(amount As String) As Task
        Dim number = _api.OpenAccount("Ayanda Nkosi", 100D, _teller)
        Dim client = Await _api.SignedInClient("teller")
        Dim response = Await client.PostMoneyAsync($"/api/accounts/{number}/deposits",
                                                   New CashRequest With {.Amount = Decimal.Parse(amount, Globalization.CultureInfo.InvariantCulture)})
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode)
        Assert.Equal(ErrorCodes.InvalidAmount, Await response.ProblemCode())
    End Function

    <Fact>
    Public Async Function WithdrawalCannotOverdraw() As Task
        Dim number = _api.OpenAccount("Ayanda Nkosi", 100D, _teller)
        Dim client = Await _api.SignedInClient("teller")
        Dim response = Await client.PostMoneyAsync($"/api/accounts/{number}/withdrawals", New CashRequest With {.Amount = 100.01D})
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode)
        Assert.Equal(ErrorCodes.InsufficientFunds, Await response.ProblemCode())
        Assert.Equal(100D, _api.Accounts().GetAccount(number).Balance)
    End Function

    <Fact>
    Public Async Function OverLimitWithdrawalNeedsASupervisor() As Task
        Dim number = _api.OpenAccount("Pieter van Wyk", 50_000D, _teller)
        Dim client = Await _api.SignedInClient("teller")
        Dim request As New CashRequest With {.Amount = 25_000D}

        Dim withoutApproval = Await client.PostMoneyAsync($"/api/accounts/{number}/withdrawals", request)
        Assert.Equal(ErrorCodes.ApprovalRequired, Await withoutApproval.ProblemCode())

        request.Approval = New SupervisorApproval With {.Username = "teller", .Password = BankApiFactory.Password}
        Dim tellerApproving = Await client.PostMoneyAsync($"/api/accounts/{number}/withdrawals", request)
        Assert.Equal(ErrorCodes.ApprovalRejected, Await tellerApproving.ProblemCode())

        request.Approval = New SupervisorApproval With {.Username = "super", .Password = BankApiFactory.Password}
        Dim approved = Await client.PostMoneyAsync($"/api/accounts/{number}/withdrawals", request)
        approved.EnsureSuccessStatusCode()
        Dim receipt = Await approved.Content.ReadFromJsonAsync(Of ReceiptDto)(Cancel)
        Assert.Equal("super", receipt.Transactions.Single().ApprovedBy)
        Assert.Equal(25_000D, _api.Accounts().GetAccount(number).Balance)
    End Function

    <Fact>
    Public Async Function TransferMovesMoneyBetweenAccountsUnderOneReceipt() As Task
        Dim source = _api.OpenAccount("Fatima Patel", 1_000D, _teller)
        Dim target = _api.OpenAccount("Sipho Khumalo", 0D, _teller)
        Dim client = Await _api.SignedInClient("teller")

        Dim response = Await client.PostMoneyAsync("/api/transfers",
            New TransferRequest With {.FromAccount = source, .ToAccount = target, .Amount = 400D, .Reference = "Rent"})
        response.EnsureSuccessStatusCode()
        Dim receipt = Await response.Content.ReadFromJsonAsync(Of ReceiptDto)(Cancel)

        Assert.Equal({TransactionTypes.TransferOut, TransactionTypes.TransferIn}, receipt.Transactions.Select(Function(t) t.Type))
        Assert.Equal(600D, _api.Accounts().GetAccount(source).Balance)
        Assert.Equal(400D, _api.Accounts().GetAccount(target).Balance)
    End Function

    <Fact>
    Public Async Function TransferToTheSameAccountIsRejected() As Task
        Dim number = _api.OpenAccount("Fatima Patel", 1_000D, _teller)
        Dim client = Await _api.SignedInClient("teller")
        Dim response = Await client.PostMoneyAsync("/api/transfers", New TransferRequest With {.FromAccount = number, .ToAccount = number, .Amount = 1D})
        Assert.Equal(ErrorCodes.SameAccount, Await response.ProblemCode())
    End Function

    <Fact>
    Public Async Function ConcurrentWithdrawalsNeverOverdraw() As Task
        ' 20 tellers' worth of R100 withdrawals race for R1 000: exactly ten may succeed.
        Dim number = _api.OpenAccount("Thabo Sithole", 1_000D, _teller)
        Dim client = Await _api.SignedInClient("teller")

        Dim attempts = Enumerable.Range(0, 20).Select(
            Function(i) client.PostMoneyAsync($"/api/accounts/{number}/withdrawals", New CashRequest With {.Amount = 100D})).ToArray()
        Dim responses = Await Task.WhenAll(attempts)

        Assert.Equal(10, responses.Count(Function(r) r.IsSuccessStatusCode))
        Assert.Equal(10, responses.Count(Function(r) r.StatusCode = HttpStatusCode.Conflict))
        Assert.Equal(0D, _api.Accounts().GetAccount(number).Balance)
    End Function

    ' -- accounts and roles --------------------------------------------------

    <Fact>
    Public Async Function OnlySupervisorsCanFreezeAndFrozenAccountsRejectPostings() As Task
        Dim number = _api.OpenAccount("Nomvula Zulu", 500D, _teller)
        Dim teller = Await _api.SignedInClient("teller")
        Dim supervisor = Await _api.SignedInClient("super")
        Dim freeze As New FreezeRequest With {.Reason = "Reported lost ID"}

        Dim byTeller = Await teller.PostAsJsonAsync($"/api/accounts/{number}/freeze", freeze, Cancel)
        Assert.Equal(HttpStatusCode.Forbidden, byTeller.StatusCode)

        Dim bySupervisor = Await supervisor.PostAsJsonAsync($"/api/accounts/{number}/freeze", freeze, Cancel)
        bySupervisor.EnsureSuccessStatusCode()

        Dim deposit = Await teller.PostMoneyAsync($"/api/accounts/{number}/deposits", New CashRequest With {.Amount = 10D})
        Assert.Equal(ErrorCodes.AccountFrozen, Await deposit.ProblemCode())
    End Function

    <Fact>
    Public Async Function SearchTreatsInputAsDataNotSql() As Task
        _api.OpenAccount("Chloe Adams", 10D, _teller)
        Dim client = Await _api.SignedInClient("teller")

        Dim hostile = Await client.GetFromJsonAsync(Of List(Of AccountDto))("/api/accounts?query=" & Uri.EscapeDataString("'; DROP TABLE accounts; --"), Cancel)
        Assert.Empty(hostile)
        Dim wildcard = Await client.GetFromJsonAsync(Of List(Of AccountDto))("/api/accounts?query=%25%25", Cancel)
        Assert.Empty(wildcard)
        Dim found = Await client.GetFromJsonAsync(Of List(Of AccountDto))("/api/accounts?query=chloe", Cancel)
        Assert.Equal("Chloe Adams", found.Single().HolderName)
    End Function

    <Fact>
    Public Async Function InvalidAccountNumbersAreCaughtByTheCheckDigit() As Task
        Dim client = Await _api.SignedInClient("teller")
        Dim response = Await client.GetAsync("/api/accounts/1000000017", Cancel)
        Assert.Equal(ErrorCodes.InvalidAccountNumber, Await response.ProblemCode())
    End Function

    <Fact>
    Public Async Function AuditLogRecordsActivityAndIsForSupervisorsOnly() As Task
        Dim number = _api.OpenAccount("Johan Botha", 100D, _teller)
        Dim teller = Await _api.SignedInClient("teller")
        Await teller.PostMoneyAsync($"/api/accounts/{number}/deposits", New CashRequest With {.Amount = 5D})

        Assert.Equal(HttpStatusCode.Forbidden, (Await teller.GetAsync("/api/audit", Cancel)).StatusCode)

        Dim supervisor = Await _api.SignedInClient("super")
        Dim entries = Await supervisor.GetFromJsonAsync(Of List(Of AuditEntryDto))("/api/audit", Cancel)
        Dim actions = entries.Select(Function(e) e.Action).ToList()
        Assert.Contains("Sign in", actions)
        Assert.Contains("Open account", actions)
        Assert.Contains("Deposit", actions)
    End Function

    <Fact>
    Public Async Function MalformedJsonGetsTheStandardProblemShape() As Task
        Dim client = Await _api.SignedInClient("teller")
        Dim content As New Http.StringContent("{""amount"":", Text.Encoding.UTF8, "application/json")
        Dim response = Await client.PostAsync("/api/transfers", content, Cancel)
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode)
        Assert.Equal(ErrorCodes.InvalidRequest, Await response.ProblemCode())
    End Function

End Class
