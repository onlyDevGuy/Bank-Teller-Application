Imports BankTeller.Api.Data
Imports BankTeller.Api.Security
Imports BankTeller.Contracts
Imports Microsoft.Data.Sqlite

Namespace Services

    ' Signs tellers in, enforces lockout after repeated failures, and checks
    ' supervisor approvals for over-limit transactions.
    Public Class AuthService

        ' Verified against when the username does not exist, so a miss costs the
        ' same work as a wrong password and response times do not reveal valid usernames.
        Private ReadOnly _dummyHash As String

        Private ReadOnly _db As Database
        Private ReadOnly _tokens As TokenService
        Private ReadOnly _audit As AuditLog
        Private ReadOnly _options As BankOptions
        Private ReadOnly _clock As TimeProvider

        Public Sub New(db As Database, tokens As TokenService, audit As AuditLog, options As BankOptions, clock As TimeProvider)
            _db = db
            _tokens = tokens
            _audit = audit
            _options = options
            _clock = clock
            _dummyHash = PasswordHasher.Hash(Guid.NewGuid().ToString(), options.PasswordIterations)
        End Sub

        Public Function Login(request As LoginRequest, remoteIp As String) As LoginResponse
            Dim teller = Authenticate(request?.Username, request?.Password, remoteIp, "Sign in")

            Dim expiresAt As DateTimeOffset
            Dim token = _tokens.Issue(teller.Id, teller.Username, teller.DisplayName, teller.Role, expiresAt)
            Return New LoginResponse With {
                .Token = token,
                .ExpiresAt = expiresAt,
                .Teller = New TellerDto With {.Username = teller.Username, .DisplayName = teller.DisplayName, .Role = teller.Role}
            }
        End Function

        ' Returns the approving supervisor's id. Failed approvals count towards that supervisor's lockout.
        Public Function VerifySupervisor(approval As SupervisorApproval, remoteIp As String) As TellerRecord
            Dim supervisor As TellerRecord
            Try
                supervisor = Authenticate(approval?.Username, approval?.Password, remoteIp, "Supervisor approval")
            Catch ex As BankException When ex.Code = ErrorCodes.BadCredentials
                Throw New BankException(403, ErrorCodes.ApprovalRejected, "The supervisor username or password is incorrect.")
            End Try

            If supervisor.Role <> Roles.Supervisor Then
                Throw New BankException(403, ErrorCodes.ApprovalRejected, $"{supervisor.DisplayName} is not a supervisor and cannot approve this.")
            End If
            Return supervisor
        End Function

        Public Function FindById(id As Long) As TellerRecord
            Using connection = _db.Open()
                Return Find(connection, Nothing, "id = $value", id)
            End Using
        End Function

        Public Function CreateTeller(username As String, displayName As String, role As String, password As String) As Long
            Using connection = _db.Open(), command = connection.CreateCommand()
                command.CommandText = "
INSERT INTO tellers (username, display_name, role, password_hash, created_at)
VALUES ($username, $name, $role, $hash, $now) RETURNING id"
                command.Bind("$username", username)
                command.Bind("$name", displayName)
                command.Bind("$role", role)
                command.Bind("$hash", PasswordHasher.Hash(password, _options.PasswordIterations))
                command.Bind("$now", ToTimestamp(_clock.GetUtcNow()))
                Return CLng(command.ExecuteScalar())
            End Using
        End Function

        Private Function Authenticate(username As String, password As String, remoteIp As String, action As String) As TellerRecord
            username = If(username, String.Empty).Trim()
            If username.Length = 0 OrElse String.IsNullOrEmpty(password) Then
                Throw New BankException(401, ErrorCodes.BadCredentials, "Enter a username and password.")
            End If

            Dim now = _clock.GetUtcNow()
            Using connection = _db.Open()
                Using transaction = connection.BeginTransaction()
                    Dim teller = Find(connection, transaction, "username = $value", username)

                    If teller Is Nothing Then
                        PasswordHasher.Verify(password, _dummyHash)
                        _audit.Write(connection, transaction, Nothing, username, action, "Unknown username", False, remoteIp)
                        transaction.Commit()
                        Throw BadCredentials()
                    End If

                    If teller.LockedUntil.HasValue AndAlso teller.LockedUntil.Value > now Then
                        _audit.Write(connection, transaction, teller.Id, teller.Username, action, "Rejected: account locked", False, remoteIp)
                        transaction.Commit()
                        Throw LockedOut(teller.LockedUntil.Value)
                    End If

                    If Not PasswordHasher.Verify(password, teller.PasswordHash) Then
                        Dim attempts = teller.FailedAttempts + 1
                        Dim lockUntil As DateTimeOffset? = Nothing
                        If attempts >= _options.LockoutThreshold Then
                            lockUntil = now.AddMinutes(_options.LockoutMinutes)
                            attempts = 0
                        End If
                        UpdateLockout(connection, transaction, teller.Id, attempts, lockUntil)
                        _audit.Write(connection, transaction, teller.Id, teller.Username, action,
                                     If(lockUntil.HasValue, $"Wrong password; locked for {_options.LockoutMinutes} minutes", "Wrong password"),
                                     False, remoteIp)
                        transaction.Commit()
                        If lockUntil.HasValue Then Throw LockedOut(lockUntil.Value)
                        Throw BadCredentials()
                    End If

                    UpdateLockout(connection, transaction, teller.Id, 0, Nothing)
                    _audit.Write(connection, transaction, teller.Id, teller.Username, action, "Succeeded", True, remoteIp)
                    transaction.Commit()
                    Return teller
                End Using
            End Using
        End Function

        Private Shared Function BadCredentials() As BankException
            ' Same message for an unknown user and a wrong password.
            Return New BankException(401, ErrorCodes.BadCredentials, "The username or password is incorrect.")
        End Function

        Private Shared Function LockedOut(until As DateTimeOffset) As BankException
            Return New BankException(423, ErrorCodes.LockedOut,
                $"This sign-in is locked after too many failed attempts. Try again after {until.ToLocalTime():HH:mm}, or ask a supervisor.")
        End Function

        Private Shared Sub UpdateLockout(connection As SqliteConnection, transaction As SqliteTransaction,
                                         id As Long, attempts As Integer, lockedUntil As DateTimeOffset?)
            Using command = connection.CreateCommand()
                command.Transaction = transaction
                command.CommandText = "UPDATE tellers SET failed_attempts = $attempts, locked_until = $until WHERE id = $id"
                command.Bind("$attempts", attempts)
                command.Bind("$until", If(lockedUntil.HasValue, ToTimestamp(lockedUntil.Value), Nothing))
                command.Bind("$id", id)
                command.ExecuteNonQuery()
            End Using
        End Sub

        Private Shared Function Find(connection As SqliteConnection, transaction As SqliteTransaction,
                                     where As String, value As Object) As TellerRecord
            Using command = connection.CreateCommand()
                command.Transaction = transaction
                command.CommandText = $"
SELECT id, username, display_name, role, password_hash, failed_attempts, locked_until
FROM tellers WHERE {where}"
                command.Bind("$value", value)
                Using reader = command.ExecuteReader()
                    If Not reader.Read() Then Return Nothing
                    Return New TellerRecord With {
                        .Id = reader.GetInt64(0),
                        .Username = reader.GetString(1),
                        .DisplayName = reader.GetString(2),
                        .Role = reader.GetString(3),
                        .PasswordHash = reader.GetString(4),
                        .FailedAttempts = reader.GetInt32(5),
                        .LockedUntil = If(reader.IsDBNull(6), CType(Nothing, DateTimeOffset?), reader.GetTimestamp(6))
                    }
                End Using
            End Using
        End Function

    End Class

    Public Class TellerRecord
        Public Property Id As Long
        Public Property Username As String
        Public Property DisplayName As String
        Public Property Role As String
        Public Property PasswordHash As String
        Public Property FailedAttempts As Integer
        Public Property LockedUntil As DateTimeOffset?
    End Class

End Namespace
