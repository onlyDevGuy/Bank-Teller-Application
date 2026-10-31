Imports System.Globalization

' Money is stored as whole cents (Long) so balances never pick up rounding
' errors, and shown to people as rand with two decimals.

Public Module Money

    Private ReadOnly ZaCulture As CultureInfo = CreateZaCulture()

    ' Converts a rand amount to cents. Returns False for amounts with more than two decimals.
    Public Function TryToCents(amount As Decimal, ByRef cents As Long) As Boolean
        Dim scaled = amount * 100D
        If scaled <> Decimal.Truncate(scaled) Then Return False
        If scaled > Long.MaxValue OrElse scaled < Long.MinValue Then Return False
        cents = CLng(scaled)
        Return True
    End Function

    Public Function FromCents(cents As Long) As Decimal
        Return cents / 100D
    End Function

    ' Formats as "R 1 234 567.89": space-grouped thousands with a dot decimal.
    Public Function Format(amount As Decimal) As String
        Return "R " & amount.ToString("#,##0.00", ZaCulture)
    End Function

    ' Accepts "1500", "1 500.50", "1,500.50" and "R1500".
    Public Function TryParse(text As String, ByRef amount As Decimal) As Boolean
        If text Is Nothing Then Return False
        Dim cleaned = text.Trim().TrimStart("R"c, "r"c).Replace(" ", "").Replace(ChrW(&HA0), "")
        Return Decimal.TryParse(cleaned, NumberStyles.AllowDecimalPoint Or NumberStyles.AllowThousands,
                                CultureInfo.InvariantCulture, amount)
    End Function

    Private Function CreateZaCulture() As CultureInfo
        Dim culture = CType(CultureInfo.InvariantCulture.Clone(), CultureInfo)
        culture.NumberFormat.NumberGroupSeparator = " "
        culture.NumberFormat.NumberDecimalSeparator = "."
        Return culture
    End Function

End Module
