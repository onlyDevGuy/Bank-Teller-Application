<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Form1
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.Label3 = New System.Windows.Forms.Label()
        Me.Label4 = New System.Windows.Forms.Label()
        Me.txtAccNr = New System.Windows.Forms.TextBox()
        Me.btnTotal = New System.Windows.Forms.Button()
        Me.lblAccHold = New System.Windows.Forms.Label()
        Me.btnSave = New System.Windows.Forms.Button()
        Me.btnClose = New System.Windows.Forms.Button()
        Me.lblBalance = New System.Windows.Forms.Label()
        Me.Panel1 = New System.Windows.Forms.Panel()
        Me.rdnWith = New System.Windows.Forms.RadioButton()
        Me.rdnDep = New System.Windows.Forms.RadioButton()
        Me.txtAmt = New System.Windows.Forms.TextBox()
        Me.Label6 = New System.Windows.Forms.Label()
        Me.btnFind = New System.Windows.Forms.Button()
        Me.Panel1.SuspendLayout()
        Me.SuspendLayout()
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Location = New System.Drawing.Point(32, 38)
        Me.Label1.Margin = New System.Windows.Forms.Padding(5, 0, 5, 0)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(116, 16)
        Me.Label1.TabIndex = 0
        Me.Label1.Text = "Account Number:"
        '
        'Label3
        '
        Me.Label3.AutoSize = True
        Me.Label3.Location = New System.Drawing.Point(622, 98)
        Me.Label3.Margin = New System.Windows.Forms.Padding(5, 0, 5, 0)
        Me.Label3.Name = "Label3"
        Me.Label3.Size = New System.Drawing.Size(63, 16)
        Me.Label3.TabIndex = 2
        Me.Label3.Text = "Balance:"
        '
        'Label4
        '
        Me.Label4.AutoSize = True
        Me.Label4.Location = New System.Drawing.Point(32, 98)
        Me.Label4.Margin = New System.Windows.Forms.Padding(5, 0, 5, 0)
        Me.Label4.Name = "Label4"
        Me.Label4.Size = New System.Drawing.Size(149, 16)
        Me.Label4.TabIndex = 3
        Me.Label4.Text = "Account Holder Name:"
        '
        'txtAccNr
        '
        Me.txtAccNr.Location = New System.Drawing.Point(159, 38)
        Me.txtAccNr.Margin = New System.Windows.Forms.Padding(5, 3, 5, 3)
        Me.txtAccNr.Name = "txtAccNr"
        Me.txtAccNr.Size = New System.Drawing.Size(151, 22)
        Me.txtAccNr.TabIndex = 4
        '
        'btnTotal
        '
        Me.btnTotal.AutoSize = True
        Me.btnTotal.BackColor = System.Drawing.Color.FromArgb(CType(CType(255, Byte), Integer), CType(CType(192, Byte), Integer), CType(CType(128, Byte), Integer))
        Me.btnTotal.Location = New System.Drawing.Point(101, 288)
        Me.btnTotal.Margin = New System.Windows.Forms.Padding(5, 3, 5, 3)
        Me.btnTotal.Name = "btnTotal"
        Me.btnTotal.Size = New System.Drawing.Size(101, 64)
        Me.btnTotal.TabIndex = 5
        Me.btnTotal.Text = "Totals"
        Me.btnTotal.UseVisualStyleBackColor = False
        '
        'lblAccHold
        '
        Me.lblAccHold.BackColor = System.Drawing.SystemColors.ButtonShadow
        Me.lblAccHold.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D
        Me.lblAccHold.Location = New System.Drawing.Point(37, 119)
        Me.lblAccHold.Margin = New System.Windows.Forms.Padding(5, 0, 5, 0)
        Me.lblAccHold.Name = "lblAccHold"
        Me.lblAccHold.Size = New System.Drawing.Size(145, 27)
        Me.lblAccHold.TabIndex = 6
        '
        'btnSave
        '
        Me.btnSave.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.btnSave.BackColor = System.Drawing.Color.FromArgb(CType(CType(128, Byte), Integer), CType(CType(255, Byte), Integer), CType(CType(128, Byte), Integer))
        Me.btnSave.Location = New System.Drawing.Point(327, 288)
        Me.btnSave.Name = "btnSave"
        Me.btnSave.Size = New System.Drawing.Size(101, 64)
        Me.btnSave.TabIndex = 7
        Me.btnSave.Text = "Save Balance"
        Me.btnSave.UseVisualStyleBackColor = False
        '
        'btnClose
        '
        Me.btnClose.AutoSize = True
        Me.btnClose.BackColor = System.Drawing.Color.FromArgb(CType(CType(255, Byte), Integer), CType(CType(128, Byte), Integer), CType(CType(128, Byte), Integer))
        Me.btnClose.Location = New System.Drawing.Point(553, 288)
        Me.btnClose.Name = "btnClose"
        Me.btnClose.Size = New System.Drawing.Size(101, 64)
        Me.btnClose.TabIndex = 8
        Me.btnClose.Text = "Close"
        Me.btnClose.UseVisualStyleBackColor = False
        '
        'lblBalance
        '
        Me.lblBalance.BackColor = System.Drawing.SystemColors.ButtonShadow
        Me.lblBalance.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D
        Me.lblBalance.Location = New System.Drawing.Point(622, 119)
        Me.lblBalance.Margin = New System.Windows.Forms.Padding(5, 0, 5, 0)
        Me.lblBalance.Name = "lblBalance"
        Me.lblBalance.Size = New System.Drawing.Size(145, 27)
        Me.lblBalance.TabIndex = 9
        Me.lblBalance.Text = "0.00"
        '
        'Panel1
        '
        Me.Panel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink
        Me.Panel1.BackColor = System.Drawing.SystemColors.AppWorkspace
        Me.Panel1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D
        Me.Panel1.Controls.Add(Me.rdnWith)
        Me.Panel1.Controls.Add(Me.rdnDep)
        Me.Panel1.Controls.Add(Me.txtAmt)
        Me.Panel1.Controls.Add(Me.Label6)
        Me.Panel1.Location = New System.Drawing.Point(182, 164)
        Me.Panel1.Name = "Panel1"
        Me.Panel1.Size = New System.Drawing.Size(391, 118)
        Me.Panel1.TabIndex = 10
        '
        'rdnWith
        '
        Me.rdnWith.AutoSize = True
        Me.rdnWith.Font = New System.Drawing.Font("Arial", 11.25!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.rdnWith.Location = New System.Drawing.Point(21, 76)
        Me.rdnWith.Name = "rdnWith"
        Me.rdnWith.Size = New System.Drawing.Size(91, 22)
        Me.rdnWith.TabIndex = 3
        Me.rdnWith.TabStop = True
        Me.rdnWith.Text = "Withdraw"
        Me.rdnWith.UseVisualStyleBackColor = True
        '
        'rdnDep
        '
        Me.rdnDep.AutoSize = True
        Me.rdnDep.Font = New System.Drawing.Font("Arial", 11.25!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.rdnDep.Location = New System.Drawing.Point(21, 18)
        Me.rdnDep.Name = "rdnDep"
        Me.rdnDep.Size = New System.Drawing.Size(81, 22)
        Me.rdnDep.TabIndex = 2
        Me.rdnDep.TabStop = True
        Me.rdnDep.Text = "Deposit"
        Me.rdnDep.UseVisualStyleBackColor = True
        '
        'txtAmt
        '
        Me.txtAmt.Font = New System.Drawing.Font("Arial", 11.25!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.txtAmt.Location = New System.Drawing.Point(211, 50)
        Me.txtAmt.Name = "txtAmt"
        Me.txtAmt.Size = New System.Drawing.Size(118, 25)
        Me.txtAmt.TabIndex = 1
        '
        'Label6
        '
        Me.Label6.AutoSize = True
        Me.Label6.Font = New System.Drawing.Font("Arial", 11.25!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Label6.Location = New System.Drawing.Point(146, 52)
        Me.Label6.Name = "Label6"
        Me.Label6.Size = New System.Drawing.Size(65, 18)
        Me.Label6.TabIndex = 0
        Me.Label6.Text = "Amount:"
        '
        'btnFind
        '
        Me.btnFind.BackColor = System.Drawing.Color.FromArgb(CType(CType(255, Byte), Integer), CType(CType(192, Byte), Integer), CType(CType(255, Byte), Integer))
        Me.btnFind.Location = New System.Drawing.Point(331, 38)
        Me.btnFind.Name = "btnFind"
        Me.btnFind.Size = New System.Drawing.Size(75, 23)
        Me.btnFind.TabIndex = 11
        Me.btnFind.Text = "Find"
        Me.btnFind.UseVisualStyleBackColor = False
        '
        'Form1
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(8.0!, 16.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.SystemColors.ActiveBorder
        Me.ClientSize = New System.Drawing.Size(840, 412)
        Me.Controls.Add(Me.btnFind)
        Me.Controls.Add(Me.Panel1)
        Me.Controls.Add(Me.lblBalance)
        Me.Controls.Add(Me.btnClose)
        Me.Controls.Add(Me.btnSave)
        Me.Controls.Add(Me.lblAccHold)
        Me.Controls.Add(Me.btnTotal)
        Me.Controls.Add(Me.txtAccNr)
        Me.Controls.Add(Me.Label4)
        Me.Controls.Add(Me.Label3)
        Me.Controls.Add(Me.Label1)
        Me.Font = New System.Drawing.Font("Arial", 9.75!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.ForeColor = System.Drawing.SystemColors.ActiveCaptionText
        Me.HelpButton = True
        Me.Margin = New System.Windows.Forms.Padding(5, 3, 5, 3)
        Me.Name = "Form1"
        Me.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Show
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "Bank Teller App"
        Me.Panel1.ResumeLayout(False)
        Me.Panel1.PerformLayout()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub
    Protected WithEvents rdnWith As System.Windows.Forms.RadioButton
    Protected WithEvents rdnDep As System.Windows.Forms.RadioButton
    Protected WithEvents txtAmt As System.Windows.Forms.TextBox
    Protected WithEvents Label6 As System.Windows.Forms.Label
    Protected WithEvents Label1 As System.Windows.Forms.Label
    Protected WithEvents Label3 As System.Windows.Forms.Label
    Protected WithEvents Label4 As System.Windows.Forms.Label
    Protected WithEvents txtAccNr As System.Windows.Forms.TextBox
    Protected WithEvents btnTotal As System.Windows.Forms.Button
    Protected WithEvents lblAccHold As System.Windows.Forms.Label
    Protected WithEvents btnSave As System.Windows.Forms.Button
    Protected WithEvents btnClose As System.Windows.Forms.Button
    Protected WithEvents lblBalance As System.Windows.Forms.Label
    Protected WithEvents Panel1 As System.Windows.Forms.Panel
    Friend WithEvents btnFind As System.Windows.Forms.Button

End Class
