Namespace Services

    ' Machine-readable error codes. The desktop client switches on these, so the
    ' text of an error message can change without breaking it.
    Public NotInheritable Class ErrorCodes
        Public Const InvalidRequest As String = "invalid_request"
        Public Const InvalidAmount As String = "invalid_amount"
        Public Const InvalidAccountNumber As String = "invalid_account_number"
        Public Const NotFound As String = "not_found"
        Public Const InsufficientFunds As String = "insufficient_funds"
        Public Const AccountFrozen As String = "account_frozen"
        Public Const SameAccount As String = "same_account"
        Public Const ApprovalRequired As String = "approval_required"
        Public Const ApprovalRejected As String = "approval_rejected"
        Public Const BadCredentials As String = "bad_credentials"
        Public Const LockedOut As String = "locked_out"
        Public Const IdempotencyKeyRequired As String = "idempotency_key_required"

        Private Sub New()
        End Sub
    End Class

    ' A rule the request broke. Turned into an RFC 7807 problem response by the API;
    ' anything else that escapes is logged and reported as a generic 500.
    Public Class BankException
        Inherits Exception

        Public ReadOnly Property Status As Integer
        Public ReadOnly Property Code As String

        Public Sub New(status As Integer, code As String, message As String)
            MyBase.New(message)
            Me.Status = status
            Me.Code = code
        End Sub

        Public Shared Function BadRequest(code As String, message As String) As BankException
            Return New BankException(400, code, message)
        End Function

        Public Shared Function NotFound(message As String) As BankException
            Return New BankException(404, ErrorCodes.NotFound, message)
        End Function

        Public Shared Function Conflict(code As String, message As String) As BankException
            Return New BankException(409, code, message)
        End Function
    End Class

    Public Class BankOptions
        ' Withdrawals and transfers above this need a supervisor's approval.
        Public Property TellerLimit As Decimal = 20_000D
        ' Hard ceiling on any single transaction.
        Public Property MaxTransaction As Decimal = 1_000_000D
        Public Property LockoutThreshold As Integer = 5
        Public Property LockoutMinutes As Integer = 15
        Public Property PasswordIterations As Integer = Security.PasswordHasher.DefaultIterations
    End Class

    ' Who is making the request, taken from the validated token.
    Public Class TellerContext
        Public Property Id As Long
        Public Property Username As String
        Public Property Role As String
        Public Property RemoteIp As String

        Public ReadOnly Property IsSupervisor As Boolean
            Get
                Return Role = Contracts.Roles.Supervisor
            End Get
        End Property
    End Class

End Namespace
