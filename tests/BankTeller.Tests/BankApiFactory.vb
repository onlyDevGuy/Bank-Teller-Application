Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Net.Http.Json
Imports BankTeller.Api
Imports BankTeller.Api.Services
Imports BankTeller.Contracts
Imports Microsoft.AspNetCore.Hosting
Imports Microsoft.AspNetCore.Mvc.Testing
Imports Microsoft.AspNetCore.TestHost
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Time.Testing

' Hosts the real API in memory against a throwaway SQLite file, with a fake
' clock and a cheap password work factor so tests run fast.
Public Class BankApiFactory
    Inherits WebApplicationFactory(Of Program)

    Public Const Password As String = "Test-password-1"

    Public ReadOnly Property Clock As New FakeTimeProvider(New DateTimeOffset(2026, 10, 31, 9, 0, 0, TimeSpan.Zero))

    Private ReadOnly _dbPath As String = IO.Path.Combine(IO.Path.GetTempPath(), $"bankteller-test-{Guid.NewGuid():N}.db")

    Protected Overrides Sub ConfigureWebHost(builder As IWebHostBuilder)
        builder.UseEnvironment("Testing")
        builder.UseSetting("ConnectionStrings:Bank", $"Data Source={_dbPath};Pooling=False")
        builder.UseSetting("Jwt:Key", "test-signing-key-that-is-long-enough-1234567890")
        builder.UseSetting("Seed:Enabled", "false")
        builder.UseSetting("Bank:PasswordIterations", "1000")
        builder.ConfigureTestServices(
            Sub(services)
                services.AddSingleton(Of TimeProvider)(Clock)
            End Sub)
    End Sub

    Public Function Accounts() As AccountService
        Return Services.GetRequiredService(Of AccountService)()
    End Function

    Public Function CreateTeller(username As String, role As String) As TellerContext
        Dim id = Services.GetRequiredService(Of AuthService)().CreateTeller(username, $"{username} Test", role, Password)
        Return New TellerContext With {.Id = id, .Username = username, .Role = role, .RemoteIp = "test"}
    End Function

    Public Function OpenAccount(holder As String, balance As Decimal, teller As TellerContext) As String
        Return Accounts().Open(New OpenAccountRequest With {.HolderName = holder, .OpeningDeposit = balance}, teller).AccountNumber
    End Function

    ' An HTTP client signed in as the given user.
    Public Async Function SignedInClient(username As String) As Task(Of HttpClient)
        Dim client = CreateClient()
        Dim response = Await client.PostAsJsonAsync("/api/auth/login", New LoginRequest With {.Username = username, .Password = Password})
        response.EnsureSuccessStatusCode()
        Dim login = Await response.Content.ReadFromJsonAsync(Of LoginResponse)()
        client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", login.Token)
        Return client
    End Function

    Protected Overrides Sub Dispose(disposing As Boolean)
        MyBase.Dispose(disposing)
        For Each suffix In {"", "-wal", "-shm"}
            Try
                IO.File.Delete(_dbPath & suffix)
            Catch ex As IO.IOException
            End Try
        Next
    End Sub

End Class

Public Module HttpTestExtensions

    ' Posts JSON with a fresh Idempotency-Key unless one is given.
    <Runtime.CompilerServices.Extension>
    Public Async Function PostMoneyAsync(client As HttpClient, url As String, body As Object, Optional key As String = Nothing) As Task(Of HttpResponseMessage)
        Using request As New HttpRequestMessage(HttpMethod.Post, url)
            request.Content = JsonContent.Create(body)
            request.Headers.Add("Idempotency-Key", If(key, Guid.NewGuid().ToString()))
            Return Await client.SendAsync(request)
        End Using
    End Function

    ' Reads the "code" field of a problem response.
    <Runtime.CompilerServices.Extension>
    Public Async Function ProblemCode(response As HttpResponseMessage) As Task(Of String)
        Dim json = Await response.Content.ReadFromJsonAsync(Of Text.Json.JsonElement)()
        Dim code As Text.Json.JsonElement
        Return If(json.TryGetProperty("code", code), code.GetString(), Nothing)
    End Function

End Module
