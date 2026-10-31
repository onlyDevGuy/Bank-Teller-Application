Imports BankTeller.Contracts

' The counter: find an account on the left, act on it in the middle, and see
' the receipt and today's totals on the right.
Public Class MainForm
    Inherits Form

    Private Enum Action
        Deposit
        Withdraw
        Transfer
    End Enum

    ' Asking "are you sure?" on every small deposit trains people to click through it,
    ' so only debits from this amount up are confirmed.
    Private Const ConfirmFrom As Decimal = 5_000D

    Private ReadOnly _api As ApiClient
    Private ReadOnly _idle As IdleMonitor
    Private _current As AccountDto
    Private _action As Action = Action.Deposit
    Private _tellerLimit As Decimal = 20_000D
    Private _pendingKey As String
    Private _transferTarget As AccountDto
    Private ReadOnly _holderNames As New Dictionary(Of String, String)
    Private ReadOnly _searchDelay As New Timer With {.Interval = 300}
    Private ReadOnly _targetDelay As New Timer With {.Interval = 300}

    ' header
    Private ReadOnly _tellerLabel As New Label()
    Private ReadOnly _idleLabel As New Label()
    Private ReadOnly _auditButton As New TellerButton With {.Text = "Audit log", .Width = 110, .Height = 34}
    Private ReadOnly _signOutButton As New TellerButton With {.Text = "Sign out", .Width = 100, .Height = 34}

    ' left
    Private ReadOnly _search As New FieldBox("Name or account number")
    Private ReadOnly _results As New ListBox()
    Private ReadOnly _resultsHint As New Label()
    Private ReadOnly _openAccountButton As New TellerButton With {.Text = "Open new account", .Dock = DockStyle.Bottom}

    ' centre
    Private ReadOnly _accountCard As New Panel()
    Private ReadOnly _emptyState As New Label()
    Private ReadOnly _holder As New Label()
    Private ReadOnly _number As New Label()
    Private ReadOnly _status As New Label()
    Private ReadOnly _balance As New Label()
    Private ReadOnly _freezeButton As New TellerButton With {.Width = 110, .Height = 32}
    Private ReadOnly _actionButtons As New Dictionary(Of Action, TellerButton)
    Private ReadOnly _amount As New FieldBox("0.00", Theme.MonoFont(15.0F, FontStyle.Bold))
    Private ReadOnly _reference As New FieldBox("Optional, e.g. Rent")
    Private ReadOnly _target As New FieldBox("10-digit account number", Theme.MonoFont(11.0F))
    Private ReadOnly _targetName As New Label()
    Private ReadOnly _targetRow As New TableLayoutPanel()
    Private ReadOnly _message As New Label()
    Private ReadOnly _submit As New TellerButton With {.Kind = TellerButton.Kinds.Primary, .Height = 44}
    Private ReadOnly _history As DataGridView = Grids.Create()

    ' right
    Private ReadOnly _receipt As New ReceiptView()
    Private ReadOnly _printButton As New TellerButton With {.Text = "Print receipt", .Dock = DockStyle.Bottom, .Enabled = False}
    Private ReadOnly _summaryLabels As New Dictionary(Of String, Label)

    ' footer
    Private ReadOnly _serverLabel As New Label()

    <ComponentModel.DesignerSerializationVisibility(ComponentModel.DesignerSerializationVisibility.Hidden)>
    Public Property SignOutReason As String

    Public Sub New(api As ApiClient, settings As DesktopSettings)
        _api = api
        Text = $"Bank Teller · {api.Teller.DisplayName}"
        StartPosition = FormStartPosition.CenterScreen
        AutoScaleMode = AutoScaleMode.Dpi
        AutoScaleDimensions = New SizeF(96.0F, 96.0F)
        ClientSize = New Size(1360, 820)
        MinimumSize = New Size(1080, 640)
        BackColor = Theme.Desk
        Font = Theme.UiFont(9.75F)
        KeyPreview = True
        ' Laptop screens (e.g. 1366x768) are smaller than the preferred size: fill them instead.
        Dim workArea = Screen.PrimaryScreen.WorkingArea
        If workArea.Width < Width OrElse workArea.Height < Height Then WindowState = FormWindowState.Maximized

        Dim body As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 3, .RowCount = 1, .Padding = New Padding(12)}
        body.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 280))
        body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        body.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 300))
        body.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        body.Controls.Add(BuildSearchColumn(), 0, 0)
        body.Controls.Add(BuildCentreColumn(), 1, 0)
        body.Controls.Add(BuildReceiptColumn(), 2, 0)

        Controls.Add(body)
        Controls.Add(BuildFooter())
        Controls.Add(BuildHeader())

        _idle = New IdleMonitor(TimeSpan.FromMinutes(Math.Max(1, settings.IdleMinutes)))
        AddHandler _idle.Tick, AddressOf OnIdleTick
        AddHandler _idle.TimedOut, Sub() SignOut($"You were signed out after {settings.IdleMinutes} minutes without activity.")
        AddHandler _api.SessionExpired, AddressOf OnSessionExpired

        SelectAction(Action.Deposit)
        ShowAccount(Nothing)
    End Sub

    ' -- layout --------------------------------------------------------------

    Private Function BuildHeader() As Control
        ' Bottom padding keeps docked children off the 4 px vault-teal rule.
        Dim header As New Panel With {.Dock = DockStyle.Top, .Height = 58, .BackColor = Theme.Ink, .Padding = New Padding(20, 0, 16, 4)}
        AddHandler header.Paint,
            Sub(sender, e)
                Using brush As New SolidBrush(Theme.Vault)
                    e.Graphics.FillRectangle(brush, 0, header.Height - 4, header.Width, 4)
                End Using
            End Sub

        Dim wordmark As New Label With {.Text = "Bank Teller", .Font = Theme.DisplayFont(15.0F), .ForeColor = Color.White,
                                        .AutoSize = True, .Location = New Point(20, 15)}
        Dim right As New FlowLayoutPanel With {.Dock = DockStyle.Right, .FlowDirection = FlowDirection.LeftToRight, .AutoSize = True,
                                               .WrapContents = False, .Padding = New Padding(0, 11, 0, 0), .BackColor = Theme.Ink}

        Dim muted = ColorTranslator.FromHtml("#B9C3CC")
        _idleLabel.AutoSize = True
        _idleLabel.ForeColor = ColorTranslator.FromHtml("#F2C46D")
        _idleLabel.Margin = New Padding(0, 9, 18, 0)
        _tellerLabel.AutoSize = True
        _tellerLabel.ForeColor = muted
        _tellerLabel.Margin = New Padding(0, 9, 18, 0)
        _tellerLabel.Text = $"{_api.Teller.DisplayName}  ·  {_api.Teller.Role}"
        _auditButton.Margin = New Padding(0, 0, 8, 0)
        _auditButton.Visible = _api.IsSupervisor
        _signOutButton.Margin = New Padding(0)

        right.Controls.AddRange({_idleLabel, _tellerLabel, _auditButton, _signOutButton})
        header.Controls.Add(right)
        header.Controls.Add(wordmark)

        AddHandler _signOutButton.Click, Sub() SignOut(Nothing)
        AddHandler _auditButton.Click, AddressOf OnAuditLog
        Return header
    End Function

    Private Function BuildFooter() As Control
        Dim footer As New Panel With {.Dock = DockStyle.Bottom, .Height = 28, .BackColor = Theme.Surface, .Padding = New Padding(20, 6, 20, 0)}
        AddHandler footer.Paint, Sub(sender, e) e.Graphics.DrawLine(New Pen(Theme.Rule), 0, 0, footer.Width, 0)
        _serverLabel.Dock = DockStyle.Left
        _serverLabel.AutoSize = True
        _serverLabel.ForeColor = Theme.Muted
        _serverLabel.Font = Theme.MonoFont(8.5F)
        _serverLabel.Text = $"● Connected to {_api.BaseUrl}"
        footer.Controls.Add(_serverLabel)

        Dim shortcuts As New Label With {.Dock = DockStyle.Right, .AutoSize = True, .ForeColor = Theme.Muted, .Font = Theme.UiFont(8.5F),
                                         .Text = "Ctrl+F find   ·   F2 deposit   ·   F3 withdraw   ·   F4 transfer   ·   Ctrl+P print"}
        footer.Controls.Add(shortcuts)
        Return footer
    End Function

    Private Shared Function Card(Optional padding As Padding = Nothing) As Panel
        Return New CardPanel With {.Dock = DockStyle.Fill, .BackColor = Theme.Surface, .Margin = New Padding(6),
                                   .Padding = If(padding = Nothing, New Padding(18), padding)}
    End Function

    ' A white panel with a hairline border, repainted whole on resize so the
    ' border follows the card as content grows (e.g. when a message appears).
    Private Class CardPanel
        Inherits Panel

        Public Sub New()
            SetStyle(ControlStyles.ResizeRedraw Or ControlStyles.OptimizedDoubleBuffer, True)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e)
            Using pen As New Pen(Theme.Rule)
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1)
            End Using
        End Sub
    End Class

    Private Function BuildSearchColumn() As Control
        Dim card = MainForm.Card()
        Dim heading As New Label With {.Text = "Find account", .Font = Theme.DisplayFont(13.0F), .ForeColor = Theme.Ink, .Dock = DockStyle.Top, .Height = 32}
        _search.Dock = DockStyle.Top
        Dim gap As New Panel With {.Dock = DockStyle.Top, .Height = 10}

        _resultsHint.Dock = DockStyle.Top
        _resultsHint.Height = 52
        _resultsHint.ForeColor = Theme.Muted
        _resultsHint.Padding = New Padding(0, 10, 0, 0)
        _resultsHint.Text = "Type at least 2 letters of a name, or the start of an account number."

        _results.Dock = DockStyle.Fill
        _results.BorderStyle = BorderStyle.None
        _results.DrawMode = DrawMode.OwnerDrawFixed
        _results.ItemHeight = 58
        _results.IntegralHeight = False
        AddHandler _results.DrawItem, AddressOf DrawResult
        AddHandler _results.SelectedIndexChanged, AddressOf OnResultSelected

        _openAccountButton.Margin = New Padding(0, 10, 0, 0)
        Dim buttonGap As New Panel With {.Dock = DockStyle.Bottom, .Height = 12}

        card.Controls.Add(_results)
        card.Controls.Add(_resultsHint)
        card.Controls.Add(gap)
        card.Controls.Add(_search)
        card.Controls.Add(heading)
        card.Controls.Add(buttonGap)
        card.Controls.Add(_openAccountButton)

        AddHandler _search.Box.TextChanged, Sub()
                                                _searchDelay.Stop()
                                                _searchDelay.Start()
                                            End Sub
        AddHandler _search.Box.KeyDown, AddressOf OnSearchKeyDown
        AddHandler _searchDelay.Tick, Async Sub()
                                          _searchDelay.Stop()
                                          Await RunSearchAsync()
                                      End Sub
        AddHandler _openAccountButton.Click, AddressOf OnOpenAccount
        Return card
    End Function

    Private Function BuildCentreColumn() As Control
        Dim column As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3, .Margin = New Padding(0)}
        column.RowStyles.Add(New RowStyle(SizeType.Absolute, 128))
        column.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        column.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        column.Controls.Add(BuildAccountCard(), 0, 0)
        column.Controls.Add(BuildActionCard(), 0, 1)
        column.Controls.Add(BuildHistoryCard(), 0, 2)
        Return column
    End Function

    Private Function BuildAccountCard() As Control
        Dim card = MainForm.Card(New Padding(22, 16, 22, 16))
        _accountCard.Dock = DockStyle.Fill
        _accountCard.BackColor = Theme.Surface

        _holder.Font = Theme.DisplayFont(20.0F)
        _holder.ForeColor = Theme.Ink
        _holder.AutoSize = True
        _holder.Location = New Point(0, 4)

        _number.Font = Theme.MonoFont(11.0F)
        _number.ForeColor = Theme.Muted
        _number.AutoSize = True
        _number.Location = New Point(2, 50)

        _status.Font = Theme.UiFont(8.25F, FontStyle.Bold)
        _status.AutoSize = True
        _status.Padding = New Padding(8, 3, 8, 3)

        Dim balanceCaption = Theme.Caption("Available balance")
        balanceCaption.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        _balance.Font = Theme.DisplayFont(26.0F)
        _balance.ForeColor = Theme.Ink
        _balance.AutoSize = True
        _balance.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        _freezeButton.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        _freezeButton.Visible = False

        _accountCard.Controls.AddRange({_holder, _number, _status, balanceCaption, _balance, _freezeButton})
        AddHandler _accountCard.Resize,
            Sub()
                Dim rightEdge = _accountCard.ClientSize.Width
                balanceCaption.Location = New Point(rightEdge - balanceCaption.Width, 6)
                _balance.Location = New Point(rightEdge - _balance.Width, 24)
                _freezeButton.Location = New Point(rightEdge - _balance.Width - _freezeButton.Width - 24, 30)
                _status.Location = New Point(_number.Right + 12, 49)
            End Sub

        _emptyState.Dock = DockStyle.Fill
        _emptyState.ForeColor = Theme.Muted
        _emptyState.Font = Theme.UiFont(11.0F)
        _emptyState.TextAlign = ContentAlignment.MiddleLeft
        _emptyState.Text = "Find an account to start." & Environment.NewLine & "Search by holder name or account number on the left."

        card.Controls.Add(_accountCard)
        card.Controls.Add(_emptyState)
        AddHandler _freezeButton.Click, AddressOf OnFreezeToggle
        Return card
    End Function

    Private Function BuildActionCard() As Control
        Dim card = MainForm.Card(New Padding(22, 16, 22, 18))
        card.AutoSize = True
        card.AutoSizeMode = AutoSizeMode.GrowAndShrink
        card.Dock = DockStyle.Top

        Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 2}
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 42))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 58))

        Dim tabs As New FlowLayoutPanel With {.AutoSize = True, .WrapContents = False, .Margin = New Padding(0, 0, 0, 14)}
        For Each pair In {(Action.Deposit, "Deposit"), (Action.Withdraw, "Withdraw"), (Action.Transfer, "Transfer")}
            Dim button As New TellerButton With {.Text = pair.Item2, .Width = 112, .Height = 36, .Margin = New Padding(0, 0, 6, 0), .Tag = pair.Item1}
            AddHandler button.Click, Sub(sender, e) SelectAction(CType(CType(sender, Control).Tag, Action))
            _actionButtons(pair.Item1) = button
            tabs.Controls.Add(button)
        Next
        layout.Controls.Add(tabs, 0, 0)
        layout.SetColumnSpan(tabs, 2)

        Dim amountBlock = Stack(Theme.Caption("Amount (R)"), _amount)
        Dim referenceBlock = Stack(Theme.Caption("Reference"), _reference)
        amountBlock.Margin = New Padding(0, 0, 12, 0)
        layout.Controls.Add(amountBlock, 0, 1)
        layout.Controls.Add(referenceBlock, 1, 1)

        _targetRow.AutoSize = True
        _targetRow.Dock = DockStyle.Top
        _targetRow.ColumnCount = 2
        _targetRow.Margin = New Padding(0, 12, 0, 0)
        _targetRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 42))
        _targetRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 58))
        Dim targetBlock = Stack(Theme.Caption("To account"), _target)
        targetBlock.Margin = New Padding(0, 0, 12, 0)
        _targetName.AutoSize = False
        _targetName.Dock = DockStyle.Fill
        _targetName.TextAlign = ContentAlignment.BottomLeft
        _targetName.Padding = New Padding(0, 0, 0, 10)
        _targetRow.Controls.Add(targetBlock, 0, 0)
        _targetRow.Controls.Add(_targetName, 1, 0)
        layout.Controls.Add(_targetRow, 0, 2)
        layout.SetColumnSpan(_targetRow, 2)

        _message.AutoSize = False
        _message.Dock = DockStyle.Top
        _message.Height = 0
        _message.Padding = New Padding(10, 8, 10, 8)
        _message.Margin = New Padding(0, 12, 0, 0)
        layout.Controls.Add(_message, 0, 3)
        layout.SetColumnSpan(_message, 2)

        _submit.Dock = DockStyle.Top
        _submit.Margin = New Padding(0, 14, 0, 0)
        layout.Controls.Add(_submit, 0, 4)
        layout.SetColumnSpan(_submit, 2)

        For i = 0 To 4
            layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Next
        card.Controls.Add(layout)

        AddHandler _amount.Box.TextChanged, Sub() OnFormEdited()
        AddHandler _reference.Box.TextChanged, Sub() OnFormEdited()
        AddHandler _target.Box.TextChanged, Sub()
                                                OnFormEdited()
                                                _targetDelay.Stop()
                                                _targetDelay.Start()
                                            End Sub
        AddHandler _targetDelay.Tick, Async Sub()
                                          _targetDelay.Stop()
                                          Await ResolveTargetAsync()
                                      End Sub
        AddHandler _amount.Box.KeyDown, AddressOf SubmitOnEnter
        AddHandler _reference.Box.KeyDown, AddressOf SubmitOnEnter
        AddHandler _target.Box.KeyDown, AddressOf SubmitOnEnter
        AddHandler _submit.Click, Async Sub() Await SubmitAsync()
        Return card
    End Function

    Private Function BuildHistoryCard() As Control
        Dim card = MainForm.Card(New Padding(18, 14, 18, 8))
        Dim heading As New Label With {.Text = "History", .Font = Theme.DisplayFont(12.0F), .ForeColor = Theme.Ink, .Dock = DockStyle.Top, .Height = 30}

        _history.ScrollBars = ScrollBars.Vertical
        _history.Columns.Add(Grids.Column("Date", 92))
        _history.Columns.Add(Grids.Column("Type", 128))
        _history.Columns.Add(Grids.Column("Amount", 104, rightAlign:=True))
        _history.Columns.Add(Grids.Column("Balance", 108, rightAlign:=True))
        Dim reference = Grids.Column("Reference", 0, fill:=True)
        reference.MinimumWidth = 70
        _history.Columns.Add(reference)
        _history.Columns.Add(Grids.Column("Teller", 70))
        _history.Columns.Add(Grids.Column("Receipt", 116, mono:=True))

        card.Controls.Add(_history)
        card.Controls.Add(heading)
        Return card
    End Function

    Private Function BuildReceiptColumn() As Control
        Dim column As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2, .Margin = New Padding(0)}
        column.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        column.RowStyles.Add(New RowStyle(SizeType.Absolute, 196))

        Dim receiptCard = MainForm.Card(New Padding(14))
        receiptCard.BackColor = Theme.Desk
        Dim heading As New Label With {.Text = "Last receipt", .Font = Theme.DisplayFont(12.0F), .ForeColor = Theme.Ink, .Dock = DockStyle.Top, .Height = 30}
        _receipt.Dock = DockStyle.Fill
        Dim gap As New Panel With {.Dock = DockStyle.Bottom, .Height = 10}
        receiptCard.Controls.Add(_receipt)
        receiptCard.Controls.Add(heading)
        receiptCard.Controls.Add(gap)
        receiptCard.Controls.Add(_printButton)
        AddHandler _printButton.Click, Sub() _receipt.Print(Me)

        Dim summaryCard = MainForm.Card(New Padding(18, 14, 18, 14))
        Dim summaryHeading As New Label With {.Text = "Today at this counter", .Font = Theme.DisplayFont(12.0F), .ForeColor = Theme.Ink, .Dock = DockStyle.Top, .Height = 30}
        Dim grid As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 2}
        grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        grid.RowStyles.Add(New RowStyle(SizeType.Percent, 50))
        grid.RowStyles.Add(New RowStyle(SizeType.Percent, 50))
        For Each stat In {"Deposits", "Withdrawals", "Transfers", "Receipts"}
            Dim value As New Label With {.Text = "–", .Font = Theme.MonoFont(11.0F, FontStyle.Bold), .ForeColor = Theme.Ink, .AutoSize = True}
            _summaryLabels(stat) = value
            grid.Controls.Add(Stack(Theme.Caption(stat), value))
        Next
        summaryCard.Controls.Add(grid)
        summaryCard.Controls.Add(summaryHeading)

        column.Controls.Add(receiptCard, 0, 0)
        column.Controls.Add(summaryCard, 0, 1)
        Return column
    End Function

    Private Shared Function Stack(ParamArray controls As Control()) As Control
        Dim panel As New FlowLayoutPanel With {.FlowDirection = FlowDirection.TopDown, .AutoSize = True, .WrapContents = False,
                                               .Dock = DockStyle.Fill, .Margin = New Padding(0)}
        For Each control In controls
            panel.Controls.Add(control)
        Next
        AddHandler panel.Resize,
            Sub()
                For Each control As Control In panel.Controls
                    If TypeOf control Is FieldBox Then control.Width = panel.ClientSize.Width - control.Margin.Horizontal
                Next
            End Sub
        Return panel
    End Function

    ' -- startup and session -------------------------------------------------

    Protected Overrides Async Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        _search.Box.Focus()
        Await RefreshSummaryAsync()
    End Sub

    Private Sub OnIdleTick(remaining As TimeSpan)
        ' Only warn in the last minute; a constant countdown is noise. The label is
        ' left untouched otherwise, so the header is not re-laid out every second.
        If remaining.TotalSeconds > 60 Then
            If _idleLabel.Visible Then _idleLabel.Visible = False
            Return
        End If
        _idleLabel.Text = $"Signing out in {CInt(Math.Ceiling(remaining.TotalSeconds))} s"
        _idleLabel.Visible = True
    End Sub

    Private Sub OnSessionExpired(sender As Object, e As EventArgs)
        BeginInvoke(Sub() SignOut("Your session ended. Sign in again to continue."))
    End Sub

    Private Sub SignOut(reason As String)
        SignOutReason = reason
        Close()
    End Sub

    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        RemoveHandler _api.SessionExpired, AddressOf OnSessionExpired
        _idle.Dispose()
        _searchDelay.Dispose()
        _targetDelay.Dispose()
        MyBase.OnFormClosed(e)
    End Sub

    Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
        Select Case keyData
            Case Keys.Control Or Keys.F
                _search.Box.Focus()
                _search.Box.SelectAll()
                Return True
            Case Keys.F2
                SelectAction(Action.Deposit) : Return True
            Case Keys.F3
                SelectAction(Action.Withdraw) : Return True
            Case Keys.F4
                SelectAction(Action.Transfer) : Return True
            Case Keys.Control Or Keys.P
                If _receipt.HasReceipt Then _receipt.Print(Me)
                Return True
        End Select
        Return MyBase.ProcessCmdKey(msg, keyData)
    End Function

    ' -- search --------------------------------------------------------------

    Private Async Function RunSearchAsync() As Task
        Dim query = _search.Box.Text.Trim()
        If query.Length < 2 Then
            _results.Items.Clear()
            _resultsHint.Text = "Type at least 2 letters of a name, or the start of an account number."
            Return
        End If
        Try
            Dim found = Await _api.SearchAsync(query)
            If _search.Box.Text.Trim() <> query Then Return ' the teller kept typing
            _results.BeginUpdate()
            _results.Items.Clear()
            For Each account In found
                _results.Items.Add(account)
            Next
            _results.EndUpdate()
            _resultsHint.Text = If(found.Count = 0, $"No accounts match '{query}'.", $"{found.Count} found")
        Catch ex As ApiException
            _resultsHint.Text = ex.Message
        End Try
    End Function

    Private Async Sub OnSearchKeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode = Keys.Enter Then
            e.SuppressKeyPress = True
            _searchDelay.Stop()
            ' A full, valid account number opens straight away.
            If AccountNumber.IsValid(_search.Box.Text) Then
                Await SelectAccountAsync(AccountNumber.Normalise(_search.Box.Text))
            Else
                Await RunSearchAsync()
                If _results.Items.Count = 1 Then _results.SelectedIndex = 0
            End If
        ElseIf e.KeyCode = Keys.Down AndAlso _results.Items.Count > 0 Then
            e.SuppressKeyPress = True
            _results.Focus()
            If _results.SelectedIndex < 0 Then _results.SelectedIndex = 0
        End If
    End Sub

    Private Async Sub OnResultSelected(sender As Object, e As EventArgs)
        Dim account = TryCast(_results.SelectedItem, AccountDto)
        If account IsNot Nothing AndAlso account.AccountNumber <> _current?.AccountNumber Then
            Await SelectAccountAsync(account.AccountNumber)
        End If
    End Sub

    Private Sub DrawResult(sender As Object, e As DrawItemEventArgs)
        If e.Index < 0 Then Return
        Dim account = CType(_results.Items(e.Index), AccountDto)
        Dim selected = (e.State And DrawItemState.Selected) = DrawItemState.Selected
        Dim g = e.Graphics
        Using background As New SolidBrush(If(selected, Theme.VaultWash, Theme.Surface))
            g.FillRectangle(background, e.Bounds)
        End Using
        If selected Then
            Using bar As New SolidBrush(Theme.Vault)
                g.FillRectangle(bar, e.Bounds.Left, e.Bounds.Top, 3, e.Bounds.Height)
            End Using
        End If
        Using rule As New Pen(Theme.Rule)
            g.DrawLine(rule, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1)
        End Using

        Dim inner = Rectangle.Inflate(e.Bounds, -12, -8)
        Using nameFont = Theme.UiFont(10.0F, FontStyle.Bold), numberFont = Theme.MonoFont(8.75F)
            TextRenderer.DrawText(g, account.HolderName, nameFont, New Rectangle(inner.Left, inner.Top, inner.Width - 96, 22), Theme.Ink,
                                  TextFormatFlags.EndEllipsis Or TextFormatFlags.Left)
            TextRenderer.DrawText(g, AccountNumber.Format(account.AccountNumber), numberFont, New Point(inner.Left, inner.Top + 22), Theme.Muted)
            If account.Status = AccountStatuses.Frozen Then
                TextRenderer.DrawText(g, "FROZEN", Theme.UiFont(7.5F, FontStyle.Bold), New Rectangle(inner.Right - 90, inner.Top, 90, 20), Theme.Debit,
                                      TextFormatFlags.Right)
            Else
                TextRenderer.DrawText(g, Money.Format(account.Balance), numberFont, New Rectangle(inner.Right - 120, inner.Top + 2, 120, 20), Theme.Ink,
                                      TextFormatFlags.Right)
            End If
        End Using
        e.DrawFocusRectangle()
    End Sub

    ' -- account -------------------------------------------------------------

    Private Async Function SelectAccountAsync(number As String) As Task
        Try
            UseWaitCursor = True
            Dim accountTask = _api.GetAccountAsync(number)
            Dim historyTask = _api.HistoryAsync(number)
            Await Task.WhenAll(accountTask, historyTask)
            ShowAccount(accountTask.Result)
            ShowHistory(historyTask.Result)
            ClearMessage()
            _amount.Box.Focus()
        Catch ex As ApiException
            ShowMessage(ex.Message, isError:=True)
        Finally
            UseWaitCursor = False
        End Try
    End Function

    Private Sub ShowAccount(account As AccountDto)
        _current = account
        _pendingKey = Nothing
        Dim has = account IsNot Nothing
        _accountCard.Visible = has
        _emptyState.Visible = Not has
        For Each button In _actionButtons.Values
            button.Enabled = has
        Next
        _amount.Enabled = has
        _reference.Enabled = has
        _target.Enabled = has
        If Not has Then
            _history.Rows.Clear()
            UpdateSubmitText()
            Return
        End If

        _holderNames(account.AccountNumber) = account.HolderName
        _holder.Text = account.HolderName
        _number.Text = AccountNumber.Format(account.AccountNumber)
        _balance.Text = Money.Format(account.Balance)
        Dim frozen = account.Status = AccountStatuses.Frozen
        _status.Text = If(frozen, "FROZEN", "ACTIVE")
        _status.ForeColor = If(frozen, Theme.Debit, Theme.Credit)
        _status.BackColor = If(frozen, Theme.DebitWash, Theme.VaultWash)
        _freezeButton.Visible = _api.IsSupervisor
        _freezeButton.Text = If(frozen, "Unfreeze", "Freeze")
        _freezeButton.Kind = If(frozen, TellerButton.Kinds.Secondary, TellerButton.Kinds.Danger)
        _accountCard.PerformLayout()
        ' Re-run the anchored layout now that label widths changed.
        _accountCard.Width += 1
        _accountCard.Width -= 1
        UpdateSubmitText()
    End Sub

    Private Sub ShowHistory(items As List(Of TransactionDto))
        _history.Rows.Clear()
        For Each t In items
            Dim isCredit = t.Type = TransactionTypes.Deposit OrElse t.Type = TransactionTypes.Opening OrElse t.Type = TransactionTypes.TransferIn
            Dim index = _history.Rows.Add(
                t.CreatedAt.ToLocalTime().ToString("dd MMM HH:mm"),
                DescribeType(t),
                If(isCredit, "+ ", "− ") & Money.Format(t.Amount),
                Money.Format(t.BalanceAfter),
                If(t.Reference, ""),
                t.Teller,
                t.ReceiptNumber)
            Dim amountColour = If(isCredit, Theme.Credit, Theme.Debit)
            _history.Rows(index).Cells(2).Style.ForeColor = amountColour
            _history.Rows(index).Cells(2).Style.SelectionForeColor = amountColour
            If Not String.IsNullOrEmpty(t.ApprovedBy) Then
                _history.Rows(index).Cells(5).ToolTipText = $"Approved by {t.ApprovedBy}"
                _history.Rows(index).Cells(5).Value = $"{t.Teller} ✓"
            End If
        Next
        _history.ClearSelection()
    End Sub

    Private Shared Function DescribeType(t As TransactionDto) As String
        Select Case t.Type
            Case TransactionTypes.Opening : Return "Opening deposit"
            Case TransactionTypes.TransferIn : Return $"From {AccountNumber.Format(t.CounterpartyAccount)}"
            Case TransactionTypes.TransferOut : Return $"To {AccountNumber.Format(t.CounterpartyAccount)}"
            Case Else : Return t.Type
        End Select
    End Function

    Private Async Sub OnOpenAccount(sender As Object, e As EventArgs)
        Using dialog As New OpenAccountDialog(_api)
            If dialog.ShowDialog(Me) = DialogResult.OK Then
                _search.Box.Text = dialog.Opened.HolderName
                Await SelectAccountAsync(dialog.Opened.AccountNumber)
                ShowMessage($"Opened account {AccountNumber.Format(dialog.Opened.AccountNumber)} for {dialog.Opened.HolderName}.", isError:=False)
                Await RefreshSummaryAsync()
            End If
        End Using
    End Sub

    Private Async Sub OnFreezeToggle(sender As Object, e As EventArgs)
        If _current Is Nothing Then Return
        Try
            If _current.Status = AccountStatuses.Frozen Then
                ShowAccount(Await _api.UnfreezeAsync(_current.AccountNumber))
                ShowMessage("Account unfrozen. It can be used again.", isError:=False)
            Else
                Using dialog As New ReasonDialog("Freeze account", $"Freeze {_current.HolderName}'s account?",
                                                 "No deposits, withdrawals or transfers will be allowed until a supervisor unfreezes it.", "Freeze account")
                    If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
                    ShowAccount(Await _api.FreezeAsync(_current.AccountNumber, dialog.Reason))
                    ShowMessage("Account frozen.", isError:=False)
                End Using
            End If
            Await RunSearchAsync()
        Catch ex As ApiException
            ShowMessage(ex.Message, isError:=True)
        End Try
    End Sub

    Private Async Sub OnAuditLog(sender As Object, e As EventArgs)
        Try
            Dim entries = Await _api.AuditAsync()
            Using form As New AuditForm(entries)
                form.ShowDialog(Me)
            End Using
        Catch ex As ApiException
            ShowMessage(ex.Message, isError:=True)
        End Try
    End Sub

    ' -- transaction form ----------------------------------------------------

    Private Sub SelectAction(action As Action)
        _action = action
        For Each pair In _actionButtons
            pair.Value.Kind = If(pair.Key = action, TellerButton.Kinds.Primary, TellerButton.Kinds.Secondary)
        Next
        _targetRow.Visible = action = Action.Transfer
        _pendingKey = Nothing
        ClearMessage()
        UpdateSubmitText()
        If _current IsNot Nothing Then _amount.Box.Focus()
    End Sub

    Private Sub OnFormEdited()
        ' A changed form is a new transaction, so it gets a new idempotency key.
        _pendingKey = Nothing
        UpdateSubmitText()
    End Sub

    ' The button says exactly what will happen: "Withdraw R 1 500.00".
    Private Sub UpdateSubmitText()
        Dim verb = _action.ToString()
        Dim amount As Decimal
        If _current Is Nothing Then
            _submit.Text = "Choose an account first"
            _submit.Enabled = False
        ElseIf Money.TryParse(_amount.Box.Text, amount) AndAlso amount > 0D Then
            _submit.Text = $"{verb} {Money.Format(amount)}"
            _submit.Enabled = True
        Else
            _submit.Text = verb
            _submit.Enabled = True
        End If
    End Sub

    Private Async Function ResolveTargetAsync() As Task
        _transferTarget = Nothing
        Dim text = _target.Box.Text
        If AccountNumber.Normalise(text).Length = 0 Then
            _targetName.Text = ""
            Return
        End If
        If AccountNumber.Normalise(text).Length < AccountNumber.Length Then
            _targetName.ForeColor = Theme.Muted
            _targetName.Text = $"{AccountNumber.Normalise(text).Length} of 10 digits"
            Return
        End If
        If Not AccountNumber.IsValid(text) Then
            _targetName.ForeColor = Theme.Debit
            _targetName.Text = "That number has a typo: the check digit doesn't match."
            Return
        End If
        Try
            Dim account = Await _api.GetAccountAsync(text)
            If AccountNumber.Normalise(_target.Box.Text) <> account.AccountNumber Then Return
            _transferTarget = account
            _holderNames(account.AccountNumber) = account.HolderName
            _targetName.ForeColor = If(account.Status = AccountStatuses.Frozen, Theme.Debit, Theme.Credit)
            _targetName.Text = If(account.Status = AccountStatuses.Frozen, $"→ {account.HolderName} (frozen)", $"→ {account.HolderName}")
        Catch ex As ApiException
            _targetName.ForeColor = Theme.Debit
            _targetName.Text = ex.Message
        End Try
    End Function

    Private Async Sub SubmitOnEnter(sender As Object, e As KeyEventArgs)
        If e.KeyCode = Keys.Enter Then
            e.SuppressKeyPress = True
            Await SubmitAsync()
        End If
    End Sub

    Private Async Function SubmitAsync() As Task
        If _current Is Nothing OrElse Not _submit.Enabled Then Return

        Dim amount As Decimal
        Dim cents As Long
        If Not Money.TryParse(_amount.Box.Text, amount) OrElse amount <= 0D Then
            ShowMessage("Enter an amount greater than zero, like 500 or 1 250.50.", isError:=True)
            _amount.Box.Focus()
            Return
        End If
        If Not Money.TryToCents(amount, cents) Then
            ShowMessage("Amounts can have at most two decimal places (cents).", isError:=True)
            _amount.Box.Focus()
            Return
        End If
        If _action = Action.Transfer Then
            If _transferTarget Is Nothing Then Await ResolveTargetAsync()
            If _transferTarget Is Nothing Then
                ShowMessage("Enter the 10-digit account number to transfer to, and check the name shown beside it.", isError:=True)
                _target.Box.Focus()
                Return
            End If
        End If

        Dim summary = DescribePending(amount)
        If _action <> Action.Deposit AndAlso amount >= ConfirmFrom AndAlso Not ConfirmWithTeller(summary) Then Return

        ' The same key is reused if this exact transaction is retried (after a
        ' timeout, or with a supervisor's approval), so it can never post twice.
        If _pendingKey Is Nothing Then _pendingKey = Guid.NewGuid().ToString()
        Dim reference = _reference.Box.Text.Trim()
        Dim approval As SupervisorApproval = Nothing

        SetBusy(True)
        Try
            Do
                Try
                    Dim receipt = Await PostAsync(amount, reference, approval)
                    OnPosted(receipt, summary)
                    Exit Do
                Catch ex As ApiException When ex.Code = "approval_required" OrElse ex.Code = "approval_rejected"
                    Using dialog As New SupervisorApprovalDialog(summary, _tellerLimit, If(ex.Code = "approval_rejected", ex.Message, Nothing))
                        If dialog.ShowDialog(Me) <> DialogResult.OK Then
                            ShowMessage("Not posted: the transaction needs a supervisor's approval.", isError:=True)
                            Exit Do
                        End If
                        approval = dialog.Approval
                    End Using
                End Try
            Loop
        Catch ex As ApiException
            ShowMessage(ex.Message, isError:=True)
        Finally
            SetBusy(False)
        End Try
    End Function

    Private Function PostAsync(amount As Decimal, reference As String, approval As SupervisorApproval) As Task(Of ReceiptDto)
        Select Case _action
            Case Action.Deposit
                Return _api.DepositAsync(_current.AccountNumber, New CashRequest With {.Amount = amount, .Reference = reference}, _pendingKey)
            Case Action.Withdraw
                Return _api.WithdrawAsync(_current.AccountNumber, New CashRequest With {.Amount = amount, .Reference = reference, .Approval = approval}, _pendingKey)
            Case Else
                Return _api.TransferAsync(New TransferRequest With {
                    .FromAccount = _current.AccountNumber, .ToAccount = _transferTarget.AccountNumber,
                    .Amount = amount, .Reference = reference, .Approval = approval}, _pendingKey)
        End Select
    End Function

    Private Function DescribePending(amount As Decimal) As String
        Select Case _action
            Case Action.Deposit : Return $"Deposit of {Money.Format(amount)} into {_current.HolderName}'s account"
            Case Action.Withdraw : Return $"Withdrawal of {Money.Format(amount)} from {_current.HolderName}'s account"
            Case Else : Return $"Transfer of {Money.Format(amount)} from {_current.HolderName} to {_transferTarget.HolderName}"
        End Select
    End Function

    Private Function ConfirmWithTeller(summary As String) As Boolean
        Dim post As New TaskDialogButton("Post it")
        Dim page As New TaskDialogPage With {
            .Caption = "Confirm transaction",
            .Heading = "Check before posting",
            .Text = summary & "." & Environment.NewLine & "Confirm the amount with the customer.",
            .Icon = TaskDialogIcon.Information,
            .Buttons = New TaskDialogButtonCollection From {post, TaskDialogButton.Cancel},
            .DefaultButton = TaskDialogButton.Cancel
        }
        Return TaskDialog.ShowDialog(Me, page) Is post
    End Function

    Private Async Sub OnPosted(receipt As ReceiptDto, summary As String)
        _pendingKey = Nothing
        _receipt.ShowReceipt(receipt, _holderNames)
        _printButton.Enabled = True
        Dim note = If(receipt.Replayed, " This was already posted, so the original receipt is shown.", "")
        ShowMessage($"Done. {summary}. Receipt {receipt.ReceiptNumber}.{note}", isError:=False)
        _amount.Box.Clear()
        _reference.Box.Clear()
        If _action = Action.Transfer Then _target.Box.Clear()
        _amount.Box.Focus()

        Try
            Dim number = _current.AccountNumber
            Dim accountTask = _api.GetAccountAsync(number)
            Dim historyTask = _api.HistoryAsync(number)
            Await Task.WhenAll(accountTask, historyTask)
            ShowAccount(accountTask.Result)
            ShowHistory(historyTask.Result)
            Await RefreshSummaryAsync()
            Await RunSearchAsync()
        Catch ex As ApiException
            ' The posting succeeded; only the refresh failed.
        End Try
    End Sub

    Private Async Function RefreshSummaryAsync() As Task
        Try
            Dim summary = Await _api.SummaryAsync()
            _tellerLimit = summary.TellerLimit
            _summaryLabels("Deposits").Text = Money.Format(summary.Deposits)
            _summaryLabels("Withdrawals").Text = Money.Format(summary.Withdrawals)
            _summaryLabels("Transfers").Text = Money.Format(summary.Transfers)
            _summaryLabels("Receipts").Text = summary.TransactionCount.ToString()
            _serverLabel.Text = $"● Connected to {_api.BaseUrl}   ·   Teller limit {Money.Format(summary.TellerLimit)}"
            _serverLabel.ForeColor = Theme.Muted
        Catch ex As ApiException
            _serverLabel.Text = $"○ {ex.Message}"
            _serverLabel.ForeColor = Theme.Debit
        End Try
    End Function

    Private Sub SetBusy(busy As Boolean)
        UseWaitCursor = busy
        _submit.Enabled = Not busy
        If busy Then _submit.Text = "Posting…" Else UpdateSubmitText()
    End Sub

    Private Sub ShowMessage(text As String, isError As Boolean)
        _message.Text = text
        _message.ForeColor = If(isError, Theme.Debit, Theme.Credit)
        _message.BackColor = If(isError, Theme.DebitWash, Theme.VaultWash)
        _message.Height = If(String.IsNullOrEmpty(text), 0, 44)
    End Sub

    Private Sub ClearMessage()
        ShowMessage(Nothing, isError:=False)
    End Sub

End Class
