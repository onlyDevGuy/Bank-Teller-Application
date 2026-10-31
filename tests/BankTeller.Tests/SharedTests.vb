Imports BankTeller.Api.Security
Imports Xunit

Public Class AccountNumberTests

    <Fact>
    Public Sub CreatedNumbersAreValid()
        For sequence = 100_000_001L To 100_000_050L
            Assert.True(AccountNumber.IsValid(AccountNumber.Create(sequence)))
        Next
    End Sub

    <Fact>
    Public Sub OneWrongDigitIsRejected()
        Dim number = AccountNumber.Create(100_000_001L)
        Dim wrong = number.Substring(0, 4) & If(number(4) = "9"c, "0", ChrW(AscW(number(4)) + 1)) & number.Substring(5)
        Assert.False(AccountNumber.IsValid(wrong))
    End Sub

    <Theory>
    <InlineData("")>
    <InlineData("12345")>
    <InlineData("10000000AB")>
    <InlineData(Nothing)>
    Public Sub MalformedNumbersAreRejected(value As String)
        Assert.False(AccountNumber.IsValid(value))
    End Sub

    <Fact>
    Public Sub SpacesAndDashesAreIgnoredAndFormatGroupsDigits()
        Dim number = AccountNumber.Create(100_000_001L)
        Dim formatted = AccountNumber.Format(number)
        Assert.Equal("100 000 0016", formatted)
        Assert.True(AccountNumber.IsValid(formatted))
        Assert.True(AccountNumber.IsValid("100-000-0016"))
    End Sub

End Class

Public Class MoneyTests

    <Theory>
    <InlineData("100", 10000L)>
    <InlineData("0.01", 1L)>
    <InlineData("1234.5", 123450L)>
    Public Sub ConvertsWholeCents(amount As String, expected As Long)
        Dim cents As Long
        Assert.True(Money.TryToCents(Decimal.Parse(amount, Globalization.CultureInfo.InvariantCulture), cents))
        Assert.Equal(expected, cents)
    End Sub

    <Fact>
    Public Sub RejectsFractionsOfACent()
        Dim cents As Long
        Assert.False(Money.TryToCents(1.005D, cents))
    End Sub

    <Theory>
    <InlineData("R1500", 1500)>
    <InlineData("1 500.50", 1500.5)>
    <InlineData("1,500.50", 1500.5)>
    Public Sub ParsesWhatTellersType(text As String, expected As Double)
        Dim amount As Decimal
        Assert.True(Money.TryParse(text, amount))
        Assert.Equal(CDec(expected), amount)
    End Sub

    <Fact>
    Public Sub FormatsWithSpaceGroups()
        Assert.Equal("R 1 234 567.80", Money.Format(1_234_567.8D))
    End Sub

End Class

Public Class PasswordHasherTests

    <Fact>
    Public Sub VerifiesTheRightPasswordOnly()
        Dim stored = PasswordHasher.Hash("correct horse", iterations:=1_000)
        Assert.True(PasswordHasher.Verify("correct horse", stored))
        Assert.False(PasswordHasher.Verify("Correct horse", stored))
        Assert.False(PasswordHasher.Verify("", stored))
    End Sub

    <Fact>
    Public Sub SaltsEveryHash()
        Assert.NotEqual(PasswordHasher.Hash("same", iterations:=1_000), PasswordHasher.Hash("same", iterations:=1_000))
    End Sub

    <Theory>
    <InlineData("")>
    <InlineData("plain-text-password")>
    <InlineData("pbkdf2-sha256$abc$AAAA$AAAA")>
    <InlineData("pbkdf2-sha256$1000$not base64$AAAA")>
    Public Sub RejectsMalformedHashes(stored As String)
        Assert.False(PasswordHasher.Verify("anything", stored))
    End Sub

End Class
