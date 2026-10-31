Imports BankTeller.Api.Data
Imports BankTeller.Contracts
Imports Microsoft.Data.Sqlite

Namespace Services

    ' Append-only record of sign-ins, money movements and account changes.
    Public Class AuditLog

        Private ReadOnly _clock As TimeProvider

        Public Sub New(clock As TimeProvider)
            _clock = clock
        End Sub

        ' Pass the open transaction so the audit row commits or rolls back with the change it describes.
        Public Sub Write(connection As SqliteConnection, transaction As SqliteTransaction,
                         tellerId As Long?, username As String, action As String, detail As String,
                         success As Boolean, remoteIp As String)
            Using command = connection.CreateCommand()
                command.Transaction = transaction
                command.CommandText = "
INSERT INTO audit_log (at, teller_id, username, action, detail, success, remote_ip)
VALUES ($at, $teller, $username, $action, $detail, $success, $ip)"
                command.Bind("$at", ToTimestamp(_clock.GetUtcNow()))
                command.Bind("$teller", If(tellerId.HasValue, CObj(tellerId.Value), Nothing))
                command.Bind("$username", If(username, String.Empty))
                command.Bind("$action", action)
                command.Bind("$detail", detail)
                command.Bind("$success", If(success, 1, 0))
                command.Bind("$ip", remoteIp)
                command.ExecuteNonQuery()
            End Using
        End Sub

        Public Function Recent(connection As SqliteConnection, limit As Integer) As List(Of AuditEntryDto)
            Dim entries As New List(Of AuditEntryDto)
            Using command = connection.CreateCommand()
                command.CommandText = "
SELECT id, at, username, action, detail, success FROM audit_log ORDER BY id DESC LIMIT $limit"
                command.Bind("$limit", limit)
                Using reader = command.ExecuteReader()
                    While reader.Read()
                        entries.Add(New AuditEntryDto With {
                            .Id = reader.GetInt64(0),
                            .At = reader.GetTimestamp(1),
                            .Username = reader.GetString(2),
                            .Action = reader.GetString(3),
                            .Detail = reader.GetString(4),
                            .Success = reader.GetInt64(5) = 1
                        })
                    End While
                End Using
            End Using
            Return entries
        End Function

    End Class

End Namespace
