Imports System.Drawing.Drawing2D
Imports System.Drawing.Printing
Imports BankTeller.Contracts

' The last transaction as a thermal-printer slip with a torn bottom edge.
' The same layout is drawn on screen and sent to the printer.
Public Class ReceiptView
    Inherits Control

    Private Enum LineKind
        Title
        Subtitle
        Rule
        Pair
        Heading
        Total
        Footer
        Blank
    End Enum

    Private Structure ReceiptLine
        Public Kind As LineKind
        Public Left As String
        Public Right As String
    End Structure

    Private _lines As New List(Of ReceiptLine)

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.ResizeRedraw, True)
        BackColor = Theme.Desk
    End Sub

    Public ReadOnly Property HasReceipt As Boolean
        Get
            Return _lines.Count > 0
        End Get
    End Property

    ' holderNames maps account numbers to names, which the receipt prints beside each account.
    Public Sub ShowReceipt(receipt As ReceiptDto, holderNames As IDictionary(Of String, String))
        _lines = BuildLines(receipt, holderNames)
        Invalidate()
    End Sub

    Public Sub Clear()
        _lines.Clear()
        Invalidate()
    End Sub

    Public Sub Print(owner As IWin32Window)
        If Not HasReceipt Then Return
        Using document As New PrintDocument With {.DocumentName = "Bank Teller receipt"}
            AddHandler document.PrintPage,
                Sub(sender, e)
                    ' 80 mm receipt width, top-left of the page.
                    Dim width = 80 / 25.4F * 100
                    Render(e.Graphics, e.MarginBounds.Left, e.MarginBounds.Top, width, torn:=False)
                End Sub
            Using preview As New PrintPreviewDialog With {.Document = document, .Width = 700, .Height = 800, .UseAntiAlias = True}
                preview.ShowDialog(owner)
            End Using
        End Using
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.TextRenderingHint = Drawing.Text.TextRenderingHint.ClearTypeGridFit

        If Not HasReceipt Then
            TextRenderer.DrawText(g, "No transaction yet." & Environment.NewLine & "The receipt for each one appears here.",
                                  Theme.UiFont(9.5F), New Rectangle(12, 12, Width - 24, 80), Theme.Muted,
                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.WordBreak)
            Return
        End If

        Render(g, 10, 6, Width - 20, torn:=True)
    End Sub

    ' Draws the slip at (x, y) and returns its height.
    Private Function Render(g As Graphics, x As Single, y As Single, width As Single, torn As Boolean) As Single
        Dim scale = g.DpiY / 96.0F
        Using body = Theme.MonoFont(8.75F), bold = Theme.MonoFont(8.75F, FontStyle.Bold),
              title = Theme.MonoFont(11.0F, FontStyle.Bold), total = Theme.MonoFont(10.5F, FontStyle.Bold)

            Dim lineHeight = body.GetHeight(g) * 1.15F
            Dim padding = 14 * scale
            Dim height = padding * 2 + _lines.Sum(Function(l) HeightOf(l.Kind, lineHeight, title.GetHeight(g), total.GetHeight(g)))
            Dim paper = New RectangleF(x, y, width, height)

            ' Soft shadow, then the paper with a zig-zag torn edge along the bottom.
            Using shadow As New SolidBrush(Color.FromArgb(28, 0, 0, 0)), paperBrush As New SolidBrush(Theme.Paper),
                  path = PaperPath(paper, torn, 7 * scale), shadowPath = PaperPath(New RectangleF(x + 2, y + 3, width, height), torn, 7 * scale)
                g.FillPath(shadow, shadowPath)
                g.FillPath(paperBrush, path)
            End Using

            Dim cursor = y + padding
            Dim left = x + padding
            Dim right = x + width - padding
            Dim center As New StringFormat With {.Alignment = StringAlignment.Center}
            Dim far As New StringFormat With {.Alignment = StringAlignment.Far}

            Using ink As New SolidBrush(Theme.Ink), muted As New SolidBrush(Theme.Muted), rulePen As New Pen(Theme.Muted, 1) With {.DashPattern = {2, 3}}
                For Each line In _lines
                    Select Case line.Kind
                        Case LineKind.Title
                            g.DrawString(line.Left, title, ink, New RectangleF(left, cursor, right - left, title.GetHeight(g)), center)
                            cursor += title.GetHeight(g) * 1.1F
                        Case LineKind.Subtitle, LineKind.Footer
                            g.DrawString(line.Left, body, muted, New RectangleF(left, cursor, right - left, lineHeight), center)
                            cursor += lineHeight
                        Case LineKind.Rule
                            Dim mid = cursor + lineHeight / 2
                            g.DrawLine(rulePen, left, mid, right, mid)
                            cursor += lineHeight
                        Case LineKind.Heading
                            g.DrawString(line.Left, bold, ink, left, cursor)
                            cursor += lineHeight
                        Case LineKind.Pair
                            g.DrawString(line.Left, body, muted, left, cursor)
                            g.DrawString(line.Right, body, ink, New RectangleF(left + 70 * scale, cursor, right - left - 70 * scale, lineHeight), far)
                            cursor += lineHeight
                        Case LineKind.Total
                            g.DrawString(line.Left, total, ink, left, cursor)
                            g.DrawString(line.Right, total, ink, New RectangleF(left, cursor, right - left, total.GetHeight(g)), far)
                            cursor += total.GetHeight(g) * 1.2F
                        Case LineKind.Blank
                            cursor += lineHeight / 2
                    End Select
                Next
            End Using
            Return height
        End Using
    End Function

    Private Shared Function HeightOf(kind As LineKind, lineHeight As Single, titleHeight As Single, totalHeight As Single) As Single
        Select Case kind
            Case LineKind.Title : Return titleHeight * 1.1F
            Case LineKind.Total : Return totalHeight * 1.2F
            Case LineKind.Blank : Return lineHeight / 2
            Case Else : Return lineHeight
        End Select
    End Function

    Private Shared Function PaperPath(bounds As RectangleF, torn As Boolean, tooth As Single) As GraphicsPath
        Dim path As New GraphicsPath()
        If Not torn Then
            path.AddRectangle(bounds)
            Return path
        End If
        Dim points As New List(Of PointF) From {New PointF(bounds.Left, bounds.Top), New PointF(bounds.Right, bounds.Top)}
        Dim xPos = bounds.Right
        Dim down = True
        points.Add(New PointF(bounds.Right, bounds.Bottom - tooth))
        While xPos > bounds.Left
            xPos = Math.Max(bounds.Left, xPos - tooth)
            points.Add(New PointF(xPos, If(down, bounds.Bottom, bounds.Bottom - tooth)))
            down = Not down
        End While
        path.AddPolygon(points.ToArray())
        Return path
    End Function

    Private Shared Sub AddLine(lines As List(Of ReceiptLine), kind As LineKind, Optional left As String = "", Optional right As String = "")
        lines.Add(New ReceiptLine With {.Kind = kind, .Left = left, .Right = right})
    End Sub

    Private Shared Function BuildLines(receipt As ReceiptDto, holderNames As IDictionary(Of String, String)) As List(Of ReceiptLine)
        Dim lines As New List(Of ReceiptLine)
        Dim first = receipt.Transactions.First()
        Dim holderOf = Function(number As String) As String
                         Dim name As String = Nothing
                         Return If(number IsNot Nothing AndAlso holderNames.TryGetValue(number, name), name, "")
                     End Function

        AddLine(lines, LineKind.Title, "BANK TELLER")
        AddLine(lines, LineKind.Subtitle, If(receipt.Replayed, "Counter receipt (copy)", "Counter receipt"))
        AddLine(lines, LineKind.Rule)
        AddLine(lines, LineKind.Pair, "Receipt", receipt.ReceiptNumber)
        AddLine(lines, LineKind.Pair, "Date", first.CreatedAt.ToLocalTime().ToString("dd MMM yyyy HH:mm"))
        AddLine(lines, LineKind.Pair, "Teller", first.Teller)
        If Not String.IsNullOrEmpty(first.ApprovedBy) Then AddLine(lines, LineKind.Pair, "Approved", first.ApprovedBy)
        AddLine(lines, LineKind.Rule)

        Select Case first.Type
            Case TransactionTypes.TransferOut, TransactionTypes.TransferIn
                Dim outgoing = receipt.Transactions.First(Function(t) t.Type = TransactionTypes.TransferOut)
                AddLine(lines, LineKind.Heading, "TRANSFER")
                AddLine(lines, LineKind.Pair, "From", AccountNumber.Format(outgoing.AccountNumber))
                AddLine(lines, LineKind.Pair, "", holderOf(outgoing.AccountNumber))
                AddLine(lines, LineKind.Pair, "To", AccountNumber.Format(outgoing.CounterpartyAccount))
                AddLine(lines, LineKind.Pair, "", holderOf(outgoing.CounterpartyAccount))
                AddLine(lines, LineKind.Blank)
                AddLine(lines, LineKind.Total, "Amount", Money.Format(outgoing.Amount))
                AddLine(lines, LineKind.Pair, "From bal.", Money.Format(outgoing.BalanceAfter))
            Case Else
                AddLine(lines, LineKind.Heading, If(first.Type = TransactionTypes.Withdrawal, "CASH WITHDRAWAL", first.Type.ToUpperInvariant()))
                AddLine(lines, LineKind.Pair, "Account", AccountNumber.Format(first.AccountNumber))
                AddLine(lines, LineKind.Pair, "Holder", holderOf(first.AccountNumber))
                AddLine(lines, LineKind.Blank)
                AddLine(lines, LineKind.Total, "Amount", Money.Format(first.Amount))
                AddLine(lines, LineKind.Pair, "Balance", Money.Format(first.BalanceAfter))
        End Select
        If Not String.IsNullOrEmpty(first.Reference) Then AddLine(lines, LineKind.Pair, "Ref", first.Reference)
        AddLine(lines, LineKind.Rule)
        AddLine(lines, LineKind.Footer, "Keep this slip")
        Return lines
    End Function

End Class
