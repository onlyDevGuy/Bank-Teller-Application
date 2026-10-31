' Account numbers are 10 digits: a 9-digit sequence plus a Luhn check digit.
' The client validates them before calling the API, so a mistyped digit is
' caught at the counter instead of reaching the wrong account.

Public Module AccountNumber

    Public Const Length As Integer = 10

    ' Strips spaces and dashes so "100 000 0019" and "100-000-0019" are accepted.
    Public Function Normalise(value As String) As String
        If value Is Nothing Then Return String.Empty
        Return New String(value.Where(Function(c) Not Char.IsWhiteSpace(c) AndAlso c <> "-"c).ToArray())
    End Function

    Public Function IsValid(value As String) As Boolean
        Dim digits = Normalise(value)
        If digits.Length <> Length OrElse Not digits.All(AddressOf Char.IsAsciiDigit) Then Return False
        Return CheckDigit(digits.Substring(0, Length - 1)) = digits(Length - 1)
    End Function

    Public Function Create(sequence As Long) As String
        Dim body = sequence.ToString("D9")
        If body.Length <> Length - 1 Then Throw New ArgumentOutOfRangeException(NameOf(sequence))
        Return body & CheckDigit(body)
    End Function

    ' Groups the digits for display: 1000000019 -> 100 000 0019.
    Public Function Format(value As String) As String
        Dim digits = Normalise(value)
        If digits.Length <> Length Then Return value
        Return $"{digits.Substring(0, 3)} {digits.Substring(3, 3)} {digits.Substring(6)}"
    End Function

    Private Function CheckDigit(body As String) As Char
        Dim sum = 0
        Dim doubleIt = True
        For i = body.Length - 1 To 0 Step -1
            Dim d = AscW(body(i)) - AscW("0"c)
            If doubleIt Then
                d *= 2
                If d > 9 Then d -= 9
            End If
            sum += d
            doubleIt = Not doubleIt
        Next
        Return ChrW(AscW("0"c) + (10 - sum Mod 10) Mod 10)
    End Function

End Module
