Imports System.Drawing.Drawing2D
Imports System.Drawing.Text

' Colours, fonts and small drawing helpers shared by every screen.
' Cool paper-grey desk, blue-black ink, one vault-teal accent; credits in
' green and debits in brick red so a glance at the history tells the story.
Public Module Theme

    Public ReadOnly Desk As Color = ColorTranslator.FromHtml("#EEF0EC")
    Public ReadOnly Surface As Color = Color.White
    Public ReadOnly Ink As Color = ColorTranslator.FromHtml("#18212B")
    Public ReadOnly Muted As Color = ColorTranslator.FromHtml("#5E6A74")
    Public ReadOnly Rule As Color = ColorTranslator.FromHtml("#D8DDD9")
    Public ReadOnly Vault As Color = ColorTranslator.FromHtml("#0E5E57")
    Public ReadOnly VaultHover As Color = ColorTranslator.FromHtml("#0A4842")
    Public ReadOnly VaultWash As Color = ColorTranslator.FromHtml("#E3EFED")
    Public ReadOnly Credit As Color = ColorTranslator.FromHtml("#1D7A46")
    Public ReadOnly Debit As Color = ColorTranslator.FromHtml("#B23A2B")
    Public ReadOnly DebitWash As Color = ColorTranslator.FromHtml("#F8E7E4")
    Public ReadOnly Warn As Color = ColorTranslator.FromHtml("#8A5A00")
    Public ReadOnly WarnWash As Color = ColorTranslator.FromHtml("#FBF0D9")
    Public ReadOnly Paper As Color = ColorTranslator.FromHtml("#FFFDF6")

    Private ReadOnly Installed As HashSet(Of String) = LoadInstalledFonts()

    Public ReadOnly UiFamily As String = FirstInstalled("Segoe UI Variable Text", "Segoe UI")
    Public ReadOnly DisplayFamily As String = FirstInstalled("Segoe UI Variable Display", "Segoe UI Semibold", "Segoe UI")
    Public ReadOnly MonoFamily As String = FirstInstalled("Cascadia Mono", "Consolas", "Courier New")

    Public Function UiFont(size As Single, Optional style As FontStyle = FontStyle.Regular) As Font
        Return New Font(UiFamily, size, style, GraphicsUnit.Point)
    End Function

    Public Function DisplayFont(size As Single, Optional style As FontStyle = FontStyle.Bold) As Font
        Return New Font(DisplayFamily, size, style, GraphicsUnit.Point)
    End Function

    Public Function MonoFont(size As Single, Optional style As FontStyle = FontStyle.Regular) As Font
        Return New Font(MonoFamily, size, style, GraphicsUnit.Point)
    End Function

    ' Small uppercase label used above fields and panels.
    Public Function Caption(text As String) As Label
        Return New Label With {
            .Text = text.ToUpperInvariant(),
            .AutoSize = True,
            .ForeColor = Muted,
            .Font = UiFont(8.25F, FontStyle.Bold),
            .Margin = New Padding(0, 0, 0, 4)
        }
    End Function

    Public Function RoundedRect(bounds As RectangleF, radius As Single) As GraphicsPath
        Dim path As New GraphicsPath()
        Dim d = radius * 2
        If d <= 0 Then
            path.AddRectangle(bounds)
            Return path
        End If
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90)
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90)
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90)
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90)
        path.CloseFigure()
        Return path
    End Function

    Private Function FirstInstalled(ParamArray families As String()) As String
        For Each family In families
            If Installed.Contains(family) Then Return family
        Next
        Return SystemFonts.MessageBoxFont.FontFamily.Name
    End Function

    Private Function LoadInstalledFonts() As HashSet(Of String)
        Using collection As New InstalledFontCollection()
            Return New HashSet(Of String)(collection.Families.Select(Function(f) f.Name), StringComparer.OrdinalIgnoreCase)
        End Using
    End Function

End Module

' A flat, rounded button in one of three weights. Draws its own focus ring so
' keyboard users can always see where they are.
Public Class TellerButton
    Inherits Button

    Public Enum Kinds
        Primary
        Secondary
        Danger
    End Enum

    Private _kind As Kinds = Kinds.Secondary
    Private _hover As Boolean

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.ResizeRedraw, True)
        FlatStyle = FlatStyle.Flat
        Font = Theme.UiFont(9.75F, FontStyle.Bold)
        Cursor = Cursors.Hand
        Height = 38
        Padding = New Padding(14, 0, 14, 0)
    End Sub

    <ComponentModel.DesignerSerializationVisibility(ComponentModel.DesignerSerializationVisibility.Hidden)>
    Public Property Kind As Kinds
        Get
            Return _kind
        End Get
        Set(value As Kinds)
            _kind = value
            Invalidate()
        End Set
    End Property

    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        _hover = True
        Invalidate()
        MyBase.OnMouseEnter(e)
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        _hover = False
        Invalidate()
        MyBase.OnMouseLeave(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.Clear(If(Parent IsNot Nothing, Parent.BackColor, Theme.Surface))

        Dim fill, text, border As Color
        Select Case _kind
            Case Kinds.Primary
                fill = If(_hover, Theme.VaultHover, Theme.Vault) : text = Color.White : border = fill
            Case Kinds.Danger
                fill = If(_hover, Theme.DebitWash, Theme.Surface) : text = Theme.Debit : border = Theme.Debit
            Case Else
                fill = If(_hover, Theme.VaultWash, Theme.Surface) : text = Theme.Ink : border = Theme.Rule
        End Select
        If Not Enabled Then
            fill = Theme.Desk : text = Theme.Muted : border = Theme.Rule
        End If

        Dim rect = New RectangleF(0.5F, 0.5F, Width - 1.5F, Height - 1.5F)
        Using path = Theme.RoundedRect(rect, 6), brush As New SolidBrush(fill), pen As New Pen(border)
            g.FillPath(brush, path)
            g.DrawPath(pen, path)
        End Using

        If Focused AndAlso ShowFocusCues Then
            Using path = Theme.RoundedRect(New RectangleF(2.5F, 2.5F, Width - 5.5F, Height - 5.5F), 4),
                  pen As New Pen(If(_kind = Kinds.Primary, Color.White, Theme.Vault), 1.5F) With {.DashStyle = DashStyle.Dot}
                g.DrawPath(pen, path)
            End Using
        End If

        TextRenderer.DrawText(g, Me.Text, Font, ClientRectangle, text,
                              TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis)
    End Sub

End Class

' A text box with a padded, ruled look and an optional placeholder.
Public Class FieldBox
    Inherits Panel

    Public ReadOnly Property Box As New TextBox()

    Public Sub New(Optional placeholder As String = "", Optional font As Font = Nothing)
        BackColor = Theme.Surface
        Padding = New Padding(10, 8, 10, 6)
        Height = 40
        Box.BorderStyle = BorderStyle.None
        Box.Font = If(font, Theme.UiFont(11.0F))
        Box.ForeColor = Theme.Ink
        Box.BackColor = Theme.Surface
        Box.PlaceholderText = placeholder
        Box.Dock = DockStyle.Fill
        Controls.Add(Box)
        AddHandler Box.GotFocus, Sub() Invalidate()
        AddHandler Box.LostFocus, Sub() Invalidate()
        Height = Box.PreferredHeight + Padding.Vertical + 2
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        Dim focused = Box.Focused
        Using pen As New Pen(If(focused, Theme.Vault, Theme.Rule), If(focused, 2.0F, 1.0F))
            e.Graphics.DrawRectangle(pen, New Rectangle(0, 0, Width - 1, Height - 1))
        End Using
    End Sub

    Protected Overrides Sub OnClick(e As EventArgs)
        Box.Focus()
        MyBase.OnClick(e)
    End Sub

End Class
