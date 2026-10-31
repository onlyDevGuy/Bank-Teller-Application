Imports BankTeller.Api.Services

Namespace Controllers

    <ApiController>
    Public MustInherit Class TellerControllerBase
        Inherits ControllerBase

        ' The signed-in teller, read from the validated token's claims.
        Protected Function CurrentTeller() As TellerContext
            Return New TellerContext With {
                .Id = Long.Parse(User.FindFirst("sub").Value),
                .Username = User.Identity.Name,
                .Role = User.FindFirst("role").Value,
                .RemoteIp = RemoteIp()
            }
        End Function

        Protected Function RemoteIp() As String
            Return HttpContext.Connection.RemoteIpAddress?.ToString()
        End Function

        Protected Function IdempotencyKey() As String
            Return Request.Headers("Idempotency-Key").ToString()
        End Function

    End Class

End Namespace
