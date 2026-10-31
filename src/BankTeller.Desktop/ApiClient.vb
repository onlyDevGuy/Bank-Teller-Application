Imports System.Net
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Net.Http.Json
Imports System.Text.Json
Imports BankTeller.Contracts

' A failed API call, carrying the API's machine-readable code and a message
' that is safe to show the teller as-is.
Public Class ApiException
    Inherits Exception

    Public ReadOnly Property Status As Integer
    Public ReadOnly Property Code As String

    Public Sub New(status As Integer, code As String, message As String)
        MyBase.New(message)
        Me.Status = status
        Me.Code = code
    End Sub
End Class

' Typed wrapper over the Bank Teller API. Every method throws ApiException with
' a teller-friendly message; a 401 also raises SessionExpired.
Public Class ApiClient
    Implements IDisposable

    Private ReadOnly _http As HttpClient
    Private Shared ReadOnly Json As New JsonSerializerOptions(JsonSerializerDefaults.Web)

    Public Event SessionExpired As EventHandler

    Public Property Teller As TellerDto
    Public Property TokenExpiresAt As DateTimeOffset

    Public Sub New(baseUrl As String)
        _http = New HttpClient With {.BaseAddress = New Uri(baseUrl), .Timeout = TimeSpan.FromSeconds(20)}
    End Sub

    Public ReadOnly Property BaseUrl As String
        Get
            Return _http.BaseAddress.ToString().TrimEnd("/"c)
        End Get
    End Property

    Public ReadOnly Property IsSupervisor As Boolean
        Get
            Return Teller IsNot Nothing AndAlso Teller.Role = Roles.Supervisor
        End Get
    End Property

    ' -- session -------------------------------------------------------------

    Public Async Function LoginAsync(username As String, password As String) As Task
        Dim response = Await SendAsync(HttpMethod.Post, "api/auth/login", New LoginRequest With {.Username = username, .Password = password},
                                       expectAuthFailure:=True)
        Dim login = Await response.Content.ReadFromJsonAsync(Of LoginResponse)(Json)
        _http.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", login.Token)
        Teller = login.Teller
        TokenExpiresAt = login.ExpiresAt
    End Function

    Public Sub SignOut()
        _http.DefaultRequestHeaders.Authorization = Nothing
        Teller = Nothing
    End Sub

    Public Async Function IsReachableAsync() As Task(Of Boolean)
        Try
            Using response = Await _http.GetAsync("health")
                Return response.IsSuccessStatusCode
            End Using
        Catch ex As Exception When TypeOf ex Is HttpRequestException OrElse TypeOf ex Is TaskCanceledException
            Return False
        End Try
    End Function

    ' -- accounts ------------------------------------------------------------

    Public Function SearchAsync(query As String) As Task(Of List(Of AccountDto))
        Return GetAsync(Of List(Of AccountDto))($"api/accounts?query={Uri.EscapeDataString(query)}")
    End Function

    Public Function GetAccountAsync(number As String) As Task(Of AccountDto)
        Return GetAsync(Of AccountDto)($"api/accounts/{AccountNumber.Normalise(number)}")
    End Function

    Public Function HistoryAsync(number As String) As Task(Of List(Of TransactionDto))
        Return GetAsync(Of List(Of TransactionDto))($"api/accounts/{AccountNumber.Normalise(number)}/transactions?limit=100")
    End Function

    Public Function SummaryAsync() As Task(Of TellerSummaryDto)
        Return GetAsync(Of TellerSummaryDto)("api/tellers/me/summary")
    End Function

    Public Function AuditAsync() As Task(Of List(Of AuditEntryDto))
        Return GetAsync(Of List(Of AuditEntryDto))("api/audit?limit=300")
    End Function

    Public Function OpenAccountAsync(request As OpenAccountRequest) As Task(Of AccountDto)
        Return PostAsync(Of AccountDto)("api/accounts", request)
    End Function

    Public Function FreezeAsync(number As String, reason As String) As Task(Of AccountDto)
        Return PostAsync(Of AccountDto)($"api/accounts/{number}/freeze", New FreezeRequest With {.Reason = reason})
    End Function

    Public Function UnfreezeAsync(number As String) As Task(Of AccountDto)
        Return PostAsync(Of AccountDto)($"api/accounts/{number}/unfreeze", New Object())
    End Function

    ' -- postings ------------------------------------------------------------
    ' The caller supplies the idempotency key and reuses it when retrying the same
    ' transaction (for example after adding a supervisor's approval), so a slow
    ' network or a double click can never post twice.

    Public Function DepositAsync(number As String, request As CashRequest, key As String) As Task(Of ReceiptDto)
        Return PostAsync(Of ReceiptDto)($"api/accounts/{number}/deposits", request, key)
    End Function

    Public Function WithdrawAsync(number As String, request As CashRequest, key As String) As Task(Of ReceiptDto)
        Return PostAsync(Of ReceiptDto)($"api/accounts/{number}/withdrawals", request, key)
    End Function

    Public Function TransferAsync(request As TransferRequest, key As String) As Task(Of ReceiptDto)
        Return PostAsync(Of ReceiptDto)("api/transfers", request, key)
    End Function

    ' -- plumbing ------------------------------------------------------------

    Private Async Function GetAsync(Of T)(url As String) As Task(Of T)
        Dim response = Await SendAsync(HttpMethod.Get, url, Nothing)
        Return Await response.Content.ReadFromJsonAsync(Of T)(Json)
    End Function

    Private Async Function PostAsync(Of T)(url As String, body As Object, Optional idempotencyKey As String = Nothing) As Task(Of T)
        Dim response = Await SendAsync(HttpMethod.Post, url, body, idempotencyKey:=idempotencyKey)
        Return Await response.Content.ReadFromJsonAsync(Of T)(Json)
    End Function

    Private Async Function SendAsync(method As HttpMethod, url As String, body As Object,
                                     Optional idempotencyKey As String = Nothing,
                                     Optional expectAuthFailure As Boolean = False) As Task(Of HttpResponseMessage)
        Using request As New HttpRequestMessage(method, url)
            If body IsNot Nothing Then request.Content = JsonContent.Create(body, options:=Json)
            If idempotencyKey IsNot Nothing Then request.Headers.Add("Idempotency-Key", idempotencyKey)

            Dim response As HttpResponseMessage
            Try
                response = Await _http.SendAsync(request)
            Catch ex As TaskCanceledException
                Throw New ApiException(0, "timeout", "The bank server took too long to answer. Check the connection and try again; a retry will not post twice.")
            Catch ex As HttpRequestException
                Throw New ApiException(0, "offline", $"Can't reach the bank server at {BaseUrl}. Check that it is running.")
            End Try

            If response.IsSuccessStatusCode Then Return response

            Dim problem = Await ReadProblemAsync(response)
            If response.StatusCode = HttpStatusCode.Unauthorized AndAlso Not expectAuthFailure Then
                RaiseEvent SessionExpired(Me, EventArgs.Empty)
                Throw New ApiException(401, "session_expired", "Your session has ended. Sign in again to continue.")
            End If
            If response.StatusCode = HttpStatusCode.TooManyRequests Then
                Throw New ApiException(429, "rate_limited", "Too many sign-in attempts from this counter. Wait a minute and try again.")
            End If
            Throw New ApiException(CInt(response.StatusCode), problem.Item1, problem.Item2)
        End Using
    End Function

    Private Shared Async Function ReadProblemAsync(response As HttpResponseMessage) As Task(Of (String, String))
        Try
            Dim doc = Await response.Content.ReadFromJsonAsync(Of JsonElement)(Json)
            Dim code As JsonElement, title As JsonElement
            Return (If(doc.TryGetProperty("code", code), code.GetString(), "error"),
                    If(doc.TryGetProperty("title", title), title.GetString(), $"The server answered {CInt(response.StatusCode)}."))
        Catch ex As Exception When TypeOf ex Is JsonException OrElse TypeOf ex Is NotSupportedException
            Return ("error", $"The server answered {CInt(response.StatusCode)} {response.ReasonPhrase}.")
        End Try
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        _http.Dispose()
    End Sub

End Class
