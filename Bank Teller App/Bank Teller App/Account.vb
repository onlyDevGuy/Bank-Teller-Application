Option Strict On

Imports System.Globalization
Imports System.IO

Public Class Account
    Public Property AccountId As String
    Public Property AccountName As String
    Public Property Balance As Decimal
    Public Property FilePath As String
    Public Property ErrorMsg As String
    Public Property Records As List(Of Account)

    Private totalDep As Decimal
    Private totalWith As Decimal

    ' Returns the total amount deposited during the current app session.
    Public ReadOnly Property TotalDeposits As Decimal
        Get
            Return totalDep
        End Get
    End Property

    ' Returns the total amount withdrawn during the current app session.
    Public ReadOnly Property TotalWithdrawals As Decimal
        Get
            Return totalWith
        End Get
    End Property

    ' Creates an empty account object ready to load records from accounts.dat.
    Public Sub New()
        AccountId = String.Empty
        AccountName = String.Empty
        Balance = 0D
        FilePath = String.Empty
        ErrorMsg = String.Empty
        Records = New List(Of Account)()
    End Sub

    ' Loads one account from the data file by matching its account number.
    Public Function GetData(accID As String) As Boolean
        ErrorMsg = String.Empty

        If Not ValidateFilePath() Then
            Return False
        End If

        Try
            For Each account As Account In ReadAccounts()
                If String.Equals(account.AccountId, accID.Trim(), StringComparison.OrdinalIgnoreCase) Then
                    AccountId = account.AccountId
                    AccountName = account.AccountName
                    Balance = account.Balance
                    totalDep = 0D
                    totalWith = 0D
                    Return True
                End If
            Next

            ErrorMsg = "Account " & accID & " was not found."
        Catch ex As Exception
            ErrorMsg = ex.Message
        End Try

        Return False
    End Function

    ' Loads all valid account records from the data file into the Records list.
    Public Function GetData() As Boolean
        ErrorMsg = String.Empty
        Records.Clear()

        If Not ValidateFilePath() Then
            Return False
        End If

        Try
            Records.AddRange(ReadAccounts())

            If Records.Count > 0 Then
                Return True
            End If

            ErrorMsg = "No account records were found."
        Catch ex As Exception
            ErrorMsg = ex.Message
        End Try

        Return False
    End Function

    ' Saves the current account balance by rewriting the matching row in accounts.dat.
    Public Function Save() As Boolean
        ErrorMsg = String.Empty

        If AccountId = String.Empty Then
            ErrorMsg = "No account has been loaded."
            Return False
        End If

        If Not ValidateFilePath() Then
            Return False
        End If

        Try
            Dim accounts As List(Of Account) = ReadAccounts()
            Dim foundAccount As Boolean = False

            For Each account As Account In accounts
                If String.Equals(account.AccountId, AccountId, StringComparison.OrdinalIgnoreCase) Then
                    account.AccountName = AccountName
                    account.Balance = Balance
                    foundAccount = True
                    Exit For
                End If
            Next

            If Not foundAccount Then
                ErrorMsg = "Account " & AccountId & " could not be found while saving."
                Return False
            End If

            WriteAccounts(accounts)
            Return True
        Catch ex As Exception
            ErrorMsg = ex.Message
        End Try

        Return False
    End Function

    ' Adds money to the current balance and records the session deposit total.
    Public Sub Deposit(amount As Decimal)
        If amount <= 0D Then
            Throw New ArgumentException("Deposit must be a positive value.")
        End If

        Balance += amount
        totalDep += amount
    End Sub

    ' Removes money from the current balance if enough funds are available.
    Public Function Withdraw(amount As Decimal) As Boolean
        If amount <= 0D Then
            Throw New ArgumentException("Withdrawal must be a positive value.")
        End If

        If amount > Balance Then
            Throw New ArgumentException("Insufficient funds.")
        End If

        Balance -= amount
        totalWith += amount
        Return True
    End Function

    ' Confirms the data file exists before any read or write operation.
    Private Function ValidateFilePath() As Boolean
        If String.IsNullOrWhiteSpace(FilePath) Then
            ErrorMsg = "The accounts data file path has not been set."
            Return False
        End If

        If Not File.Exists(FilePath) Then
            ErrorMsg = "The accounts data file could not be found: " & FilePath
            Return False
        End If

        Return True
    End Function

    ' Reads accounts.dat and turns each valid comma-separated line into an Account object.
    Private Function ReadAccounts() As List(Of Account)
        Dim accounts As New List(Of Account)()
        Dim lineNumber As Integer = 0

        For Each line As String In File.ReadAllLines(FilePath)
            lineNumber += 1

            If String.IsNullOrWhiteSpace(line) Then
                Continue For
            End If

            Dim account As Account = ParseLine(line, lineNumber)
            accounts.Add(account)
        Next

        Return accounts
    End Function

    ' Parses one account row and validates the required account number, holder name, and balance fields.
    Private Function ParseLine(line As String, lineNumber As Integer) As Account
        Dim fields() As String = line.Split(","c)

        If fields.Length <> 3 Then
            Throw New InvalidDataException("Line " & lineNumber & " must contain account number, holder name, and balance.")
        End If

        Dim parsedBalance As Decimal

        If Not Decimal.TryParse(fields(2).Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, parsedBalance) AndAlso
           Not Decimal.TryParse(fields(2).Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, parsedBalance) Then
            Throw New InvalidDataException("Line " & lineNumber & " has an invalid balance.")
        End If

        Return New Account() With {
            .AccountId = fields(0).Trim(),
            .AccountName = fields(1).Trim(),
            .Balance = parsedBalance,
            .FilePath = FilePath
        }
    End Function

    ' Writes all accounts back to accounts.dat using a stable two-decimal balance format.
    Private Sub WriteAccounts(accounts As List(Of Account))
        Dim lines As New List(Of String)()

        For Each account As Account In accounts
            lines.Add(account.AccountId & ", " &
                      account.AccountName & ", " &
                      account.Balance.ToString("0.00", CultureInfo.InvariantCulture))
        Next

        File.WriteAllLines(FilePath, lines.ToArray())
    End Sub
End Class
