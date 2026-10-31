Imports BankTeller.Api.Services
Imports BankTeller.Contracts
Imports Microsoft.AspNetCore.RateLimiting

Namespace Controllers

    <Route("api/auth")>
    Public Class AuthController
        Inherits TellerControllerBase

        Private ReadOnly _auth As AuthService

        Public Sub New(auth As AuthService)
            _auth = auth
        End Sub

        ''' <summary>Signs a teller in and returns a bearer token.</summary>
        <HttpPost("login")>
        <AllowAnonymous>
        <EnableRateLimiting("login")>
        Public Function Login(request As LoginRequest) As ActionResult(Of LoginResponse)
            Return _auth.Login(request, RemoteIp())
        End Function

        ''' <summary>The teller the current token belongs to.</summary>
        <HttpGet("me")>
        Public Function CurrentUser() As TellerDto
            Return New TellerDto With {
                .Username = User.Identity.Name,
                .DisplayName = User.FindFirst("name")?.Value,
                .Role = User.FindFirst("role")?.Value
            }
        End Function

    End Class

End Namespace
