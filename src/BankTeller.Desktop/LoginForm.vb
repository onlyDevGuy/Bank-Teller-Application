' Sign-in: an ink-coloured brand panel on the left, the form on the right.
' Errors and notices appear inline above the button, never in a message box.
Public Class LoginForm
    Inherits Form

    Private ReadOnly _api As ApiClient
    Private ReadOnly _username As New FieldBox("e.g. thandi")
    Private ReadOnly _password As New FieldBox()
    Private ReadOnly _capsLock As New Label()
    Private ReadOnly _message As New Label()
    Private ReadOnly _signIn As New TellerButton With {.Text = "Sign in", .Kind = TellerButton.Kinds.Primary}
    Private ReadOnly _server As New Label()

    Public Sub New(api As ApiClient, notice As String)
        _api = api
        Text = "Bank Teller · Sign in"
        StartPosition = FormStartPosition.CenterScreen
        FormBorderStyle = FormBorderStyle.FixedSingle
        MaximizeBox = False
        AutoScaleMode = AutoScaleMode.Dpi
        AutoScaleDimensions = New SizeF(96.0F, 96.0F)
        ClientSize = New Size(860, 520)
        BackColor = Theme.Surface
        Font = Theme.UiFont(9.75F)
        AcceptButton = _signIn

        Controls.Add(BuildForm(notice))
        Controls.Add(BuildBrandPanel())
    End Sub

    Private Function BuildBrandPanel() As Control
        Dim panel As New Panel With {.Dock = DockStyle.Left, .Width = 340, .BackColor = Theme.Ink, .Padding = New Padding(36, 40, 36, 32)}
        AddHandler panel.Paint,
            Sub(sender, e)
                ' A vault-teal rule down the edge, like the band on a strap of notes.
                Using brush As New SolidBrush(Theme.Vault)
                    e.Graphics.FillRectangle(brush, panel.Width - 6, 0, 6, panel.Height)
                End Using
            End Sub

        Dim wordmark As New Label With {
            .Text = "Bank" & Environment.NewLine & "Teller",
            .Font = Theme.DisplayFont(34.0F),
            .ForeColor = Color.White,
            .AutoSize = True,
            .Location = New Point(36, 48)
        }
        Dim tagline As New Label With {
            .Text = "Counter system for deposits, withdrawals and transfers.",
            .Font = Theme.UiFont(10.5F),
            .ForeColor = ColorTranslator.FromHtml("#B9C3CC"),
            .Location = New Point(38, 190),
            .Size = New Size(250, 48)
        }
        _server.Font = Theme.MonoFont(8.5F)
        _server.ForeColor = ColorTranslator.FromHtml("#B9C3CC")
        _server.AutoSize = False
        _server.Size = New Size(270, 40)
        _server.Location = New Point(38, 440)
        _server.Text = $"Checking {_api.BaseUrl} …"

        panel.Controls.AddRange({wordmark, tagline, _server})
        Return panel
    End Function

    Private Function BuildForm(notice As String) As Control
        Dim layout As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(56, 56, 56, 40),
            .ColumnCount = 1,
            .BackColor = Theme.Surface
        }
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))

        Dim heading As New Label With {.Text = "Sign in to your counter", .Font = Theme.DisplayFont(18.0F), .ForeColor = Theme.Ink,
                                       .AutoSize = True, .Margin = New Padding(0, 0, 0, 6)}
        Dim subheading As New Label With {.Text = "Use the username and password your branch gave you.", .ForeColor = Theme.Muted,
                                          .AutoSize = True, .Margin = New Padding(0, 0, 0, 24)}

        _password.Box.UseSystemPasswordChar = True
        _username.Dock = DockStyle.Top
        _password.Dock = DockStyle.Top
        _username.Margin = New Padding(0, 0, 0, 16)
        _password.Margin = New Padding(0, 0, 0, 4)

        _capsLock.Text = "Caps Lock is on"
        _capsLock.ForeColor = Theme.Warn
        _capsLock.AutoSize = True
        _capsLock.Visible = False
        _capsLock.Margin = New Padding(0, 0, 0, 8)

        _message.AutoSize = False
        _message.Dock = DockStyle.Top
        _message.Height = 52
        _message.Padding = New Padding(10, 8, 10, 8)
        _message.Margin = New Padding(0, 8, 0, 12)
        ShowMessage(notice, isError:=False)

        _signIn.Dock = DockStyle.Top
        _signIn.Height = 42

        For Each control In New Control() {heading, subheading, Theme.Caption("Username"), _username,
                                           Theme.Caption("Password"), _password, _capsLock, _message, _signIn}
            layout.Controls.Add(control)
            layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Next
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

        AddHandler _signIn.Click, AddressOf OnSignIn
        AddHandler _password.Box.KeyUp, Sub() _capsLock.Visible = IsKeyLocked(Keys.CapsLock)
        AddHandler _password.Box.GotFocus, Sub() _capsLock.Visible = IsKeyLocked(Keys.CapsLock)
        Return layout
    End Function

    Protected Overrides Async Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        _username.Box.Focus()
        Dim reachable = Await _api.IsReachableAsync()
        _server.Text = If(reachable, $"● Connected{Environment.NewLine}  {_api.BaseUrl}", $"○ Server not reachable{Environment.NewLine}  {_api.BaseUrl}")
        _server.ForeColor = If(reachable, ColorTranslator.FromHtml("#7FD1B9"), ColorTranslator.FromHtml("#F2A99E"))
    End Sub

    Private Async Sub OnSignIn(sender As Object, e As EventArgs)
        If _username.Box.Text.Trim().Length = 0 OrElse _password.Box.Text.Length = 0 Then
            ShowMessage("Enter your username and password.", isError:=True)
            Return
        End If

        SetBusy(True)
        Try
            Await _api.LoginAsync(_username.Box.Text.Trim(), _password.Box.Text)
            DialogResult = DialogResult.OK
            Close()
        Catch ex As ApiException
            ' Re-enable the fields first: a disabled text box cannot take focus.
            SetBusy(False)
            ShowMessage(ex.Message, isError:=True)
            _password.Box.Clear()
            _password.Box.Focus()
        End Try
    End Sub

    Private Sub SetBusy(busy As Boolean)
        _signIn.Enabled = Not busy
        _signIn.Text = If(busy, "Signing in…", "Sign in")
        _username.Enabled = Not busy
        _password.Enabled = Not busy
        UseWaitCursor = busy
    End Sub

    Private Sub ShowMessage(text As String, isError As Boolean)
        _message.Visible = Not String.IsNullOrEmpty(text)
        _message.Text = text
        _message.ForeColor = If(isError, Theme.Debit, Theme.Warn)
        _message.BackColor = If(isError, Theme.DebitWash, Theme.WarnWash)
    End Sub

End Class
