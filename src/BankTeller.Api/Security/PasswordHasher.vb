Imports System.Security.Cryptography

Namespace Security

    ' Salted PBKDF2-SHA256 password hashes, stored as
    ' "pbkdf2-sha256$<iterations>$<salt>$<hash>" so the work factor can be raised later
    ' without invalidating existing hashes.
    Public NotInheritable Class PasswordHasher

        Private Const Scheme As String = "pbkdf2-sha256"
        Private Const SaltSize As Integer = 16
        Private Const HashSize As Integer = 32
        ' OWASP's 2023 recommendation for PBKDF2-HMAC-SHA256.
        Public Const DefaultIterations As Integer = 600_000

        Private Sub New()
        End Sub

        Public Shared Function Hash(password As String, Optional iterations As Integer = DefaultIterations) As String
            ArgumentException.ThrowIfNullOrEmpty(password)
            Dim salt = RandomNumberGenerator.GetBytes(SaltSize)
            Dim derived = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashSize)
            Return String.Join("$", Scheme, iterations, Convert.ToBase64String(salt), Convert.ToBase64String(derived))
        End Function

        Public Shared Function Verify(password As String, stored As String) As Boolean
            If String.IsNullOrEmpty(password) OrElse String.IsNullOrEmpty(stored) Then Return False

            Dim parts = stored.Split("$"c)
            Dim iterations As Integer
            If parts.Length <> 4 OrElse parts(0) <> Scheme OrElse Not Integer.TryParse(parts(1), iterations) Then
                Return False
            End If

            Try
                Dim salt = Convert.FromBase64String(parts(2))
                Dim expected = Convert.FromBase64String(parts(3))
                Dim actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length)
                ' Constant-time comparison so timing does not reveal how many bytes matched.
                Return CryptographicOperations.FixedTimeEquals(actual, expected)
            Catch ex As FormatException
                Return False
            End Try
        End Function

    End Class

End Namespace
