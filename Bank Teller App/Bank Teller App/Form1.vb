Option Strict On

Imports System.Globalization
Imports System.IO

Public Class Form1
    Private ReadOnly activeAccount As New Account()

    ' Prepares the form, connects the account data file, and sets helpful default controls.
    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        activeAccount.FilePath = FindAccountsFile()
        rdnDep.Checked = True
        btnSave.Enabled = False
        ClearAccountDisplay()
        txtAccNr.Focus()
    End Sub

    ' Finds accounts.dat whether the app is running from Visual Studio's bin folder or from the project folder.
    Private Function FindAccountsFile() As String
        Dim startupFile As String = Path.Combine(Application.StartupPath, "accounts.dat")

        If File.Exists(startupFile) Then
            Return startupFile
        End If

        Dim projectFile As String = Path.GetFullPath(Path.Combine(Application.StartupPath, "..\..\accounts.dat"))

        If File.Exists(projectFile) Then
            Return projectFile
        End If

        Return startupFile
    End Function

    ' Searches for the account number typed by the user and displays its holder and balance.
    Private Sub btnFind_Click(sender As Object, e As EventArgs) Handles btnFind.Click
        Dim accountNumber As String = txtAccNr.Text.Trim()

        If accountNumber = String.Empty Then
            MessageBox.Show("Enter an account number first.", "Missing Account Number", MessageBoxButtons.OK, MessageBoxIcon.Information)
            txtAccNr.Focus()
            Return
        End If

        If activeAccount.GetData(accountNumber) Then
            DisplayAccount()
            btnSave.Enabled = True
            txtAmt.Focus()
        Else
            ClearAccountDisplay()
            btnSave.Enabled = False
            MessageBox.Show(activeAccount.ErrorMsg, "Account Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            txtAccNr.SelectAll()
            txtAccNr.Focus()
        End If
    End Sub

    ' Applies the selected transaction, saves the new balance to accounts.dat, and refreshes the balance label.
    Private Sub btnSave_Click(sender As Object, e As EventArgs) Handles btnSave.Click
        If activeAccount.AccountId = String.Empty Then
            MessageBox.Show("Find an account before saving a transaction.", "No Account Selected", MessageBoxButtons.OK, MessageBoxIcon.Information)
            txtAccNr.Focus()
            Return
        End If

        Dim amount As Decimal

        If Not TryReadAmount(amount) Then
            MessageBox.Show("Enter a valid positive amount.", "Invalid Amount", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            txtAmt.SelectAll()
            txtAmt.Focus()
            Return
        End If

        Dim previousBalance As Decimal = activeAccount.Balance

        Try
            If rdnDep.Checked Then
                activeAccount.Deposit(amount)
            ElseIf rdnWith.Checked Then
                activeAccount.Withdraw(amount)
            Else
                MessageBox.Show("Choose Deposit or Withdraw.", "Missing Transaction Type", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            If activeAccount.Save() Then
                DisplayAccount()
                txtAmt.Clear()
                txtAmt.Focus()
                MessageBox.Show("Transaction saved successfully.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Else
                activeAccount.Balance = previousBalance
                DisplayAccount()
                MessageBox.Show(activeAccount.ErrorMsg, "Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End If
        Catch ex As ArgumentException
            activeAccount.Balance = previousBalance
            DisplayAccount()
            MessageBox.Show(ex.Message, "Transaction Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    ' Shows the running deposit and withdrawal totals for the current app session.
    Private Sub btnTotal_Click(sender As Object, e As EventArgs) Handles btnTotal.Click
        Dim message As String = "Total deposits: " & FormatMoney(activeAccount.TotalDeposits) & Environment.NewLine &
                                "Total withdrawals: " & FormatMoney(activeAccount.TotalWithdrawals)

        MessageBox.Show(message, "Transaction Totals", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    ' Closes the teller app.
    Private Sub btnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
        Me.Close()
    End Sub

    ' Converts the amount text into a positive Decimal, accepting the user's culture and standard dot-decimal input.
    Private Function TryReadAmount(ByRef amount As Decimal) As Boolean
        Dim value As String = txtAmt.Text.Trim()

        If Decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, amount) OrElse
           Decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, amount) Then
            Return amount > 0D
        End If

        amount = 0D
        Return False
    End Function

    ' Copies the active account values onto the form labels.
    Private Sub DisplayAccount()
        lblAccHold.Text = activeAccount.AccountName
        lblBalance.Text = FormatMoney(activeAccount.Balance)
    End Sub

    ' Resets the account labels when there is no valid selected account.
    Private Sub ClearAccountDisplay()
        lblAccHold.Text = String.Empty
        lblBalance.Text = FormatMoney(0D)
    End Sub

    ' Formats balances and totals consistently with two decimal places.
    Private Function FormatMoney(amount As Decimal) As String
        Return amount.ToString("N2", CultureInfo.CurrentCulture)
    End Function
End Class
