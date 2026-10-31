Imports BankTeller.Api.Services
Imports BankTeller.Contracts

Namespace Data

    ' Fills an empty database with fictional tellers, customers and a little
    ' history so the app can be tried straight away. Enabled by Seed:Enabled,
    ' which only appsettings.Development.json turns on.
    Public Class DemoSeeder

        Private ReadOnly _db As Database
        Private ReadOnly _auth As AuthService
        Private ReadOnly _accounts As AccountService
        Private ReadOnly _logger As ILogger(Of DemoSeeder)

        Public Sub New(db As Database, auth As AuthService, accounts As AccountService, logger As ILogger(Of DemoSeeder))
            _db = db
            _auth = auth
            _accounts = accounts
            _logger = logger
        End Sub

        Public Sub SeedIfEmpty(demoPassword As String)
            Using connection = _db.Open(), command = connection.CreateCommand()
                command.CommandText = "SELECT COUNT(*) FROM tellers"
                If CLng(command.ExecuteScalar()) > 0 Then Return
            End Using

            If String.IsNullOrWhiteSpace(demoPassword) Then
                _logger.LogWarning("Seed:Enabled is on but Seed:DemoPassword is empty; skipping demo data.")
                Return
            End If

            Dim tellerId = _auth.CreateTeller("thandi", "Thandi Mahlangu", Roles.Teller, demoPassword)
            Dim supervisorId = _auth.CreateTeller("karabo", "Karabo Moloi", Roles.Supervisor, demoPassword)
            Dim teller As New TellerContext With {.Id = tellerId, .Username = "thandi", .Role = Roles.Teller, .RemoteIp = "seed"}
            Dim supervisor As New TellerContext With {.Id = supervisorId, .Username = "karabo", .Role = Roles.Supervisor, .RemoteIp = "seed"}

            ' Fictional customers.
            Dim customers = {
                ("Ayanda Nkosi", 18_450.75D), ("Pieter van Wyk", 152_300D), ("Lerato Mokoena", 9_320.95D),
                ("Fatima Patel", 64_015.4D), ("Sipho Khumalo", 2_678.65D), ("Chloe Adams", 31_250D),
                ("Kagiso Molefe", 540.1D), ("Naledi Dlamini", 87_900D), ("Johan Botha", 12_005.5D),
                ("Nomvula Zulu", 4_380D), ("Thabo Sithole", 226_740.2D), ("Riaan Steyn", 0D)
            }
            Dim numbers As New List(Of String)
            For Each customer In customers
                numbers.Add(_accounts.Open(New OpenAccountRequest With {.HolderName = customer.Item1, .OpeningDeposit = customer.Item2}, teller).AccountNumber)
            Next

            Dim key = 0
            Dim nextKey = Function() $"seed-{Threading.Interlocked.Increment(key)}"
            _accounts.Deposit(numbers(0), New CashRequest With {.Amount = 2_500D, .Reference = "Salary top-up"}, teller, nextKey())
            _accounts.Withdraw(numbers(0), New CashRequest With {.Amount = 800D, .Reference = "Cash"}, teller, nextKey())
            _accounts.Transfer(New TransferRequest With {.FromAccount = numbers(1), .ToAccount = numbers(0), .Amount = 1_200D, .Reference = "Rent share"}, teller, nextKey())
            _accounts.Withdraw(numbers(1), New CashRequest With {.Amount = 25_000D, .Reference = "Vehicle deposit"}, supervisor, nextKey())
            _accounts.Deposit(numbers(3), New CashRequest With {.Amount = 7_450D, .Reference = "Stokvel payout"}, teller, nextKey())
            _accounts.SetStatus(numbers(9), AccountStatuses.Frozen, "Reported lost ID document", supervisor)

            _logger.LogInformation("Seeded demo data: tellers 'thandi' (Teller) and 'karabo' (Supervisor), {Count} accounts.", numbers.Count)
        End Sub

    End Class

End Namespace
