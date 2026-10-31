Imports System.IdentityModel.Tokens.Jwt
Imports System.Security.Claims
Imports System.Text
' Claim names are the short JWT forms ("sub", "unique_name", "role"); the API
' turns off ASP.NET's mapping to long URI claim types so they read back unchanged.
Imports Microsoft.IdentityModel.Tokens

Namespace Security

    Public Class JwtOptions
        Public Property Issuer As String = "BankTeller.Api"
        Public Property Audience As String = "BankTeller.Desktop"
        ' At least 32 bytes. Supplied through configuration (e.g. the Jwt__Key
        ' environment variable); never committed.
        Public Property Key As String
        Public Property LifetimeMinutes As Integer = 30

        Public Function SigningKey() As SymmetricSecurityKey
            Return New SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key))
        End Function
    End Class

    Public Class TokenService

        Private ReadOnly _options As JwtOptions
        Private ReadOnly _clock As TimeProvider

        Public Sub New(options As JwtOptions, clock As TimeProvider)
            _options = options
            _clock = clock
        End Sub

        Public Function Issue(tellerId As Long, username As String, displayName As String, role As String,
                              ByRef expiresAt As DateTimeOffset) As String
            Dim now = _clock.GetUtcNow()
            expiresAt = now.AddMinutes(_options.LifetimeMinutes)

            Dim claims = New List(Of Claim) From {
                New Claim(JwtRegisteredClaimNames.Sub, tellerId.ToString()),
                New Claim(JwtRegisteredClaimNames.UniqueName, username),
                New Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                New Claim("name", displayName),
                New Claim("role", role)
            }

            Dim token As New JwtSecurityToken(
                issuer:=_options.Issuer,
                audience:=_options.Audience,
                claims:=claims,
                notBefore:=now.UtcDateTime,
                expires:=expiresAt.UtcDateTime,
                signingCredentials:=New SigningCredentials(_options.SigningKey(), SecurityAlgorithms.HmacSha256))

            Return New JwtSecurityTokenHandler().WriteToken(token)
        End Function

    End Class

End Namespace
