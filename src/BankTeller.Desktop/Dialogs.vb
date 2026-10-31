Imports BankTeller.Contracts

' Shared frame for the small modal dialogs: heading, explanation, fields,
' inline message, and a button row with the primary action on the right.
Public MustInherit Class TellerDialog
    Inherits Form

    Protected ReadOnly Body As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .Padding = New Padding(28, 24, 28, 16), .AutoSize = True}
    Protected ReadOnly Message As New Label With {.AutoSize = False, .Dock = DockStyle.Top, .Height = 0, .ForeColor = Theme.Debit, .Padding = New Padding(0, 4, 0, 4)}
    Protected ReadOnly Confirm As New TellerButton With {.Kind = TellerButton.Kinds.Primary, .Width = 150}
    Protected ReadOnly CancelButton_ As New TellerButton With {.Text = "Cancel", .Width = 100, .DialogResult = DialogResult.Cancel}

    Protected Sub New(title As String, heading As String, explanation As String, confirmText As String)
        Text = title
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = False
        AutoScaleMode = AutoScaleMode.Dpi
        AutoScaleDimensions = New SizeF(96.0F, 96.0F)
        BackColor = Theme.Surface
        Font = Theme.UiFont(9.75F)
        ClientSize = New Size(460, 300)
        Confirm.Text = confirmText
        AcceptButton = Confirm
        CancelButton = CancelButton_

        Body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        AddRow(New Label With {.Text = heading, .Font = Theme.DisplayFont(14.0F), .ForeColor = Theme.Ink, .AutoSize = True, .Margin = New Padding(0, 0, 0, 6)})
        If Not String.IsNullOrEmpty(explanation) Then
            AddRow(New Label With {.Text = explanation, .ForeColor = Theme.Muted, .AutoSize = True, .MaximumSize = New Size(400, 0), .Margin = New Padding(0, 0, 0, 16)})
        End If

        Dim buttons As New FlowLayoutPanel With {.Dock = DockStyle.Bottom, .FlowDirection = FlowDirection.RightToLeft, .Height = 62,
                                                 .Padding = New Padding(24, 10, 24, 10), .BackColor = Theme.Desk}
        buttons.Controls.Add(Confirm)
        buttons.Controls.Add(CancelButton_)
        Controls.Add(Body)
        Controls.Add(buttons)
        AddHandler Confirm.Click, Sub() OnConfirm()
    End Sub

    Protected Sub AddRow(control As Control)
        Body.Controls.Add(control)
        Body.RowStyles.Add(New RowStyle(SizeType.AutoSize))
    End Sub

    Protected Sub AddField(caption As String, field As FieldBox)
        field.Dock = DockStyle.Top
        field.Margin = New Padding(0, 0, 0, 12)
        AddRow(Theme.Caption(caption))
        AddRow(field)
    End Sub

    Protected Sub FinishLayout()
        AddRow(Message)
        ClientSize = New Size(ClientSize.Width, Body.PreferredSize.Height + 70)
    End Sub

    Protected Sub ShowError(text As String)
        Message.Text = text
        Message.Height = If(String.IsNullOrEmpty(text), 0, 44)
        ClientSize = New Size(ClientSize.Width, Body.PreferredSize.Height + 70)
    End Sub

    Protected MustOverride Sub OnConfirm()

End Class

' Collects a supervisor's credentials for a transaction over the teller limit.
' The API checks them; this dialog never decides anything itself.
Public Class SupervisorApprovalDialog
    Inherits TellerDialog

    Private ReadOnly _username As New FieldBox()
    Private ReadOnly _password As New FieldBox()

    Public Sub New(summary As String, limit As Decimal, Optional previousError As String = Nothing)
        MyBase.New("Supervisor approval", "Supervisor approval needed",
                   $"{summary} is over the teller limit of {Money.Format(limit)}. A supervisor must enter their own sign-in to approve it.",
                   "Approve")
        _password.Box.UseSystemPasswordChar = True
        AddField("Supervisor username", _username)
        AddField("Supervisor password", _password)
        FinishLayout()
        If previousError IsNot Nothing Then ShowError(previousError)
    End Sub

    Public ReadOnly Property Approval As SupervisorApproval
        Get
            Return New SupervisorApproval With {.Username = _username.Box.Text.Trim(), .Password = _password.Box.Text}
        End Get
    End Property

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        _username.Box.Focus()
    End Sub

    Protected Overrides Sub OnConfirm()
        If _username.Box.Text.Trim().Length = 0 OrElse _password.Box.Text.Length = 0 Then
            ShowError("Enter the supervisor's username and password.")
            Return
        End If
        DialogResult = DialogResult.OK
    End Sub

End Class

Public Class OpenAccountDialog
    Inherits TellerDialog

    Private ReadOnly _api As ApiClient
    Private ReadOnly _holder As New FieldBox("Full name as on ID")
    Private ReadOnly _deposit As New FieldBox("0.00", Theme.MonoFont(12.0F))

    <ComponentModel.DesignerSerializationVisibility(ComponentModel.DesignerSerializationVisibility.Hidden)>
    Public Property Opened As AccountDto

    Public Sub New(api As ApiClient)
        MyBase.New("Open account", "Open a new account",
                   "The account number is assigned by the bank and printed on the first receipt.", "Open account")
        _api = api
        AddField("Account holder", _holder)
        AddField("Opening deposit (optional)", _deposit)
        FinishLayout()
    End Sub

    Protected Overrides Async Sub OnConfirm()
        Dim amount As Decimal = 0D
        If _holder.Box.Text.Trim().Length < 2 Then
            ShowError("Enter the account holder's full name.")
            Return
        End If
        If _deposit.Box.Text.Trim().Length > 0 AndAlso (Not Money.TryParse(_deposit.Box.Text, amount) OrElse amount < 0D) Then
            ShowError("The opening deposit must be an amount like 500 or 1 250.50.")
            Return
        End If

        Confirm.Enabled = False
        Try
            Opened = Await _api.OpenAccountAsync(New OpenAccountRequest With {.HolderName = _holder.Box.Text.Trim(), .OpeningDeposit = amount})
            DialogResult = DialogResult.OK
        Catch ex As ApiException
            ShowError(ex.Message)
            Confirm.Enabled = True
        End Try
    End Sub

End Class

Public Class ReasonDialog
    Inherits TellerDialog

    Private ReadOnly _reason As New FieldBox()

    Public Sub New(title As String, heading As String, explanation As String, confirmText As String)
        MyBase.New(title, heading, explanation, confirmText)
        AddField("Reason", _reason)
        FinishLayout()
    End Sub

    Public ReadOnly Property Reason As String
        Get
            Return _reason.Box.Text.Trim()
        End Get
    End Property

    Protected Overrides Sub OnConfirm()
        If Reason.Length = 0 Then
            ShowError("Give a reason; it is saved in the audit log.")
            Return
        End If
        DialogResult = DialogResult.OK
    End Sub

End Class

' Read-only audit log for supervisors.
Public Class AuditForm
    Inherits Form

    Public Sub New(entries As List(Of AuditEntryDto))
        Text = "Audit log"
        StartPosition = FormStartPosition.CenterParent
        AutoScaleMode = AutoScaleMode.Dpi
        AutoScaleDimensions = New SizeF(96.0F, 96.0F)
        ClientSize = New Size(980, 600)
        BackColor = Theme.Desk
        Font = Theme.UiFont(9.5F)
        Padding = New Padding(16)

        Dim grid = Grids.Create()
        grid.Columns.Add(Grids.Column("When", 130))
        grid.Columns.Add(Grids.Column("User", 100))
        grid.Columns.Add(Grids.Column("Action", 150))
        grid.Columns.Add(Grids.Column("Detail", 0, fill:=True))
        grid.Columns.Add(Grids.Column("Result", 90))
        For Each entry In entries
            Dim row = grid.Rows.Add(entry.At.ToLocalTime().ToString("dd MMM HH:mm:ss"), entry.Username, entry.Action, entry.Detail,
                                    If(entry.Success, "OK", "Failed"))
            If Not entry.Success Then grid.Rows(row).Cells(4).Style.ForeColor = Theme.Debit
        Next
        Controls.Add(grid)
    End Sub

End Class

' Consistent styling for the data grids.
Public Module Grids

    Public Function Create() As DataGridView
        Dim grid As New DataGridView With {
            .Dock = DockStyle.Fill,
            .ReadOnly = True,
            .AllowUserToAddRows = False,
            .AllowUserToDeleteRows = False,
            .AllowUserToResizeRows = False,
            .RowHeadersVisible = False,
            .SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            .MultiSelect = False,
            .BorderStyle = BorderStyle.None,
            .BackgroundColor = Theme.Surface,
            .GridColor = Theme.Rule,
            .CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            .ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            .ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            .ColumnHeadersHeight = 34,
            .EnableHeadersVisualStyles = False
        }
        grid.RowTemplate.Height = 32
        grid.ColumnHeadersDefaultCellStyle.BackColor = Theme.Surface
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Muted
        grid.ColumnHeadersDefaultCellStyle.Font = Theme.UiFont(8.25F, FontStyle.Bold)
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.Surface
        grid.ColumnHeadersDefaultCellStyle.Padding = New Padding(6, 0, 6, 0)
        grid.DefaultCellStyle.BackColor = Theme.Surface
        grid.DefaultCellStyle.ForeColor = Theme.Ink
        grid.DefaultCellStyle.SelectionBackColor = Theme.VaultWash
        grid.DefaultCellStyle.SelectionForeColor = Theme.Ink
        grid.DefaultCellStyle.Padding = New Padding(6, 0, 6, 0)
        grid.DefaultCellStyle.Font = Theme.UiFont(9.25F)
        Return grid
    End Function

    Public Function Column(header As String, width As Integer, Optional fill As Boolean = False,
                           Optional rightAlign As Boolean = False, Optional mono As Boolean = False) As DataGridViewTextBoxColumn
        Dim col As New DataGridViewTextBoxColumn With {
            .HeaderText = header.ToUpperInvariant(),
            .SortMode = DataGridViewColumnSortMode.NotSortable,
            .AutoSizeMode = If(fill, DataGridViewAutoSizeColumnMode.Fill, DataGridViewAutoSizeColumnMode.None)
        }
        If Not fill Then col.Width = width
        If rightAlign Then
            col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight
            col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight
        End If
        If mono Then col.DefaultCellStyle.Font = Theme.MonoFont(9.0F)
        Return col
    End Function

End Module
