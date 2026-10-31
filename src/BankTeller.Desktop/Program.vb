Imports System.IO
Imports System.Text.Json

Public Module Program

    <STAThread>
    Public Sub Main()
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2)
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        AddHandler Application.ThreadException, AddressOf OnUnhandled

        Dim settings = DesktopSettings.Load()
        Using api As New ApiClient(settings.ApiBaseUrl)
            ' Sign-in and the counter alternate until the teller closes the sign-in window.
            Dim notice As String = Nothing
            Do
                Using login As New LoginForm(api, notice)
                    If login.ShowDialog() <> DialogResult.OK Then Exit Do
                End Using
                Using counter As New MainForm(api, settings)
                    Application.Run(counter)
                    notice = counter.SignOutReason
                End Using
                api.SignOut()
            Loop
        End Using
    End Sub

    Private Sub OnUnhandled(sender As Object, e As Threading.ThreadExceptionEventArgs)
        ' Last resort: never show a stack trace at the counter.
        Dim dialog As New TaskDialogPage With {
            .Caption = "Bank Teller",
            .Heading = "Something went wrong",
            .Text = "The last action did not finish. If it was a transaction, check the account history before trying again." &
                    Environment.NewLine & Environment.NewLine & e.Exception.Message,
            .Icon = TaskDialogIcon.Error
        }
        TaskDialog.ShowDialog(dialog)
    End Sub

End Module

Public Class DesktopSettings
    Public Property ApiBaseUrl As String = "http://localhost:5080"
    Public Property IdleMinutes As Integer = 5

    Public Shared Function Load() As DesktopSettings
        Dim path = IO.Path.Combine(AppContext.BaseDirectory, "appsettings.json")
        If Not File.Exists(path) Then Return New DesktopSettings()
        Return If(JsonSerializer.Deserialize(Of DesktopSettings)(File.ReadAllText(path)), New DesktopSettings())
    End Function
End Class

' Signs the teller out after a period without keyboard or mouse input, so an
' unattended counter cannot be used by someone else.
Public Class IdleMonitor
    Implements IMessageFilter, IDisposable

    Private Const WM_KEYDOWN As Integer = &H100
    Private Const WM_MOUSEMOVE As Integer = &H200
    Private Const WM_LBUTTONDOWN As Integer = &H201
    Private Const WM_MOUSEWHEEL As Integer = &H20A

    Private ReadOnly _limit As TimeSpan
    Private ReadOnly _timer As New Timer With {.Interval = 1000}
    Private _lastInput As DateTime = DateTime.UtcNow

    Public Event Tick(remaining As TimeSpan)
    Public Event TimedOut As EventHandler

    Public Sub New(limit As TimeSpan)
        _limit = limit
        Application.AddMessageFilter(Me)
        AddHandler _timer.Tick, AddressOf OnTimer
        _timer.Start()
    End Sub

    Public Function PreFilterMessage(ByRef m As Message) As Boolean Implements IMessageFilter.PreFilterMessage
        Select Case m.Msg
            Case WM_KEYDOWN, WM_MOUSEMOVE, WM_LBUTTONDOWN, WM_MOUSEWHEEL
                _lastInput = DateTime.UtcNow
        End Select
        Return False
    End Function

    Private Sub OnTimer(sender As Object, e As EventArgs)
        Dim remaining = _limit - (DateTime.UtcNow - _lastInput)
        If remaining <= TimeSpan.Zero Then
            _timer.Stop()
            RaiseEvent TimedOut(Me, EventArgs.Empty)
        Else
            RaiseEvent Tick(remaining)
        End If
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        _timer.Stop()
        _timer.Dispose()
        Application.RemoveMessageFilter(Me)
    End Sub

End Class
