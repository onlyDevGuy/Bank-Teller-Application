' Request and response shapes shared by the API and the desktop client.
' Amounts travel as Decimal rand values; the API stores them as whole cents.

Namespace Contracts

    Public NotInheritable Class Roles
        Public Const Teller As String = "Teller"
        Public Const Supervisor As String = "Supervisor"

        Private Sub New()
        End Sub
    End Class

    Public NotInheritable Class TransactionTypes
        Public Const Opening As String = "Opening"
        Public Const Deposit As String = "Deposit"
        Public Const Withdrawal As String = "Withdrawal"
        Public Const TransferIn As String = "TransferIn"
        Public Const TransferOut As String = "TransferOut"

        Private Sub New()
        End Sub
    End Class

    Public NotInheritable Class AccountStatuses
        Public Const Active As String = "Active"
        Public Const Frozen As String = "Frozen"

        Private Sub New()
        End Sub
    End Class

    ' -- auth ------------------------------------------------------------------

    Public Class LoginRequest
        Public Property Username As String
        Public Property Password As String
    End Class

    Public Class TellerDto
        Public Property Username As String
        Public Property DisplayName As String
        Public Property Role As String
    End Class

    Public Class LoginResponse
        Public Property Token As String
        Public Property ExpiresAt As DateTimeOffset
        Public Property Teller As TellerDto
    End Class

    ' A supervisor's credentials, sent with a transaction that is over the teller limit.
    Public Class SupervisorApproval
        Public Property Username As String
        Public Property Password As String
    End Class

    ' -- accounts --------------------------------------------------------------

    Public Class AccountDto
        Public Property AccountNumber As String
        Public Property HolderName As String
        Public Property Balance As Decimal
        Public Property Status As String
        Public Property OpenedAt As DateTimeOffset
    End Class

    Public Class OpenAccountRequest
        Public Property HolderName As String
        Public Property OpeningDeposit As Decimal
    End Class

    Public Class FreezeRequest
        Public Property Reason As String
    End Class

    ' -- transactions ----------------------------------------------------------

    Public Class CashRequest
        Public Property Amount As Decimal
        Public Property Reference As String
        Public Property Approval As SupervisorApproval
    End Class

    Public Class TransferRequest
        Public Property FromAccount As String
        Public Property ToAccount As String
        Public Property Amount As Decimal
        Public Property Reference As String
        Public Property Approval As SupervisorApproval
    End Class

    Public Class TransactionDto
        Public Property Id As Long
        Public Property AccountNumber As String
        Public Property Type As String
        Public Property Amount As Decimal
        Public Property BalanceAfter As Decimal
        Public Property Reference As String
        Public Property Teller As String
        Public Property ApprovedBy As String
        Public Property CounterpartyAccount As String
        Public Property ReceiptNumber As String
        Public Property CreatedAt As DateTimeOffset
    End Class

    ' What the teller prints or shows after a successful transaction.
    Public Class ReceiptDto
        Public Property ReceiptNumber As String
        Public Property Transactions As List(Of TransactionDto)
        Public Property Replayed As Boolean
    End Class

    Public Class TellerSummaryDto
        Public Property [Date] As DateOnly
        Public Property Deposits As Decimal
        Public Property Withdrawals As Decimal
        Public Property Transfers As Decimal
        Public Property TransactionCount As Integer
        Public Property TellerLimit As Decimal
    End Class

    Public Class AuditEntryDto
        Public Property Id As Long
        Public Property At As DateTimeOffset
        Public Property Username As String
        Public Property Action As String
        Public Property Detail As String
        Public Property Success As Boolean
    End Class

End Namespace
