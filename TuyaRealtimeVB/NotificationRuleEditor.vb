Imports System.Drawing
Imports System.Windows.Forms

Public Class NotificationRuleEditor
    Inherits Form

    Private _rule As NotificationRule
    Private _nameTextBox As TextBox
    Private _propertyTextBox As TextBox
    Private _operatorComboBox As ComboBox
    Private _valueTextBox As TextBox
    Private _messageTextBox As TextBox
    Private _typeComboBox As ComboBox
    Private _soundCheckBox As CheckBox
    Private _cooldownNumeric As NumericUpDown

    Public Sub New(rule As NotificationRule)
        _rule = rule
        InitializeComponent()
        LoadRuleData()
    End Sub

    Private Sub InitializeComponent()
        Me.Text = "Édition de règle"
        Me.Size = New Size(600, 550)
        Me.StartPosition = FormStartPosition.CenterParent
        Me.BackColor = Color.FromArgb(242, 242, 247)
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False

        Dim yPos As Integer = 20

        ' Nom de la règle
        AddLabel("Nom de la règle:", yPos)
        _nameTextBox = New TextBox With {
            .Location = New Point(20, yPos + 25),
            .Size = New Size(540, 30),
            .Font = New Font("Segoe UI", 11)
        }
        Me.Controls.Add(_nameTextBox)
        yPos += 70

        ' Property Code
        AddLabel("Property Code:", yPos)
        _propertyTextBox = New TextBox With {
            .Location = New Point(20, yPos + 25),
            .Size = New Size(540, 30),
            .Font = New Font("Segoe UI", 11)
        }
        Me.Controls.Add(_propertyTextBox)
        yPos += 70

        ' Opérateur de comparaison
        AddLabel("Opérateur de comparaison:", yPos)
        _operatorComboBox = New ComboBox With {
            .Location = New Point(20, yPos + 25),
            .Size = New Size(260, 30),
            .Font = New Font("Segoe UI", 11),
            .DropDownStyle = ComboBoxStyle.DropDownList
        }
        _operatorComboBox.Items.Add(New ComboBoxItem("Supérieur à (>)", ComparisonOperator.GreaterThan))
        _operatorComboBox.Items.Add(New ComboBoxItem("Supérieur ou égal (>=)", ComparisonOperator.GreaterOrEqual))
        _operatorComboBox.Items.Add(New ComboBoxItem("Inférieur à (<)", ComparisonOperator.LessThan))
        _operatorComboBox.Items.Add(New ComboBoxItem("Inférieur ou égal (<=)", ComparisonOperator.LessOrEqual))
        _operatorComboBox.Items.Add(New ComboBoxItem("Égal (=)", ComparisonOperator.Equal))
        Me.Controls.Add(_operatorComboBox)

        ' Valeur de déclenchement
        AddLabel("Valeur de déclenchement:", yPos, 300)
        _valueTextBox = New TextBox With {
            .Location = New Point(300, yPos + 25),
            .Size = New Size(260, 30),
            .Font = New Font("Segoe UI", 11)
        }
        Me.Controls.Add(_valueTextBox)
        yPos += 70

        ' Message
        AddLabel("Message d'alerte:", yPos)
        _messageTextBox = New TextBox With {
            .Location = New Point(20, yPos + 25),
            .Size = New Size(540, 30),
            .Font = New Font("Segoe UI", 11)
        }
        Me.Controls.Add(_messageTextBox)
        yPos += 70

        ' Type de notification
        AddLabel("Niveau de gravité:", yPos)
        _typeComboBox = New ComboBox With {
            .Location = New Point(20, yPos + 25),
            .Size = New Size(260, 30),
            .Font = New Font("Segoe UI", 11),
            .DropDownStyle = ComboBoxStyle.DropDownList
        }
        _typeComboBox.Items.Add("ℹ️ Information")
        _typeComboBox.Items.Add("⚠️ Avertissement")
        _typeComboBox.Items.Add("🚨 Critique")
        Me.Controls.Add(_typeComboBox)

        ' Cooldown
        AddLabel("Cooldown (minutes):", yPos, 300)
        _cooldownNumeric = New NumericUpDown With {
            .Location = New Point(300, yPos + 25),
            .Size = New Size(260, 30),
            .Font = New Font("Segoe UI", 11),
            .Minimum = 1,
            .Maximum = 10080,
            .Value = 5
        }
        Me.Controls.Add(_cooldownNumeric)
        yPos += 70

        ' Son activé
        _soundCheckBox = New CheckBox With {
            .Text = "🔊 Jouer un son lors du déclenchement",
            .Location = New Point(20, yPos),
            .Size = New Size(540, 30),
            .Font = New Font("Segoe UI", 11),
            .Checked = False
        }
        Me.Controls.Add(_soundCheckBox)
        yPos += 50

        ' Boutons
        Dim saveButton As New Button With {
            .Text = "💾 Enregistrer",
            .Location = New Point(450, yPos),
            .Size = New Size(110, 40),
            .BackColor = Color.FromArgb(52, 199, 89),
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 10, FontStyle.Bold),
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand,
            .DialogResult = DialogResult.OK
        }
        saveButton.FlatAppearance.BorderSize = 0
        AddHandler saveButton.Click, AddressOf SaveButton_Click
        Me.Controls.Add(saveButton)

        Dim cancelButton As New Button With {
            .Text = "❌ Annuler",
            .Location = New Point(330, yPos),
            .Size = New Size(110, 40),
            .BackColor = Color.FromArgb(142, 142, 147),
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 10),
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand,
            .DialogResult = DialogResult.Cancel
        }
        cancelButton.FlatAppearance.BorderSize = 0
        Me.Controls.Add(cancelButton)

        Me.AcceptButton = saveButton
        Me.CancelButton = cancelButton
    End Sub

    Private Sub AddLabel(text As String, yPos As Integer, Optional xPos As Integer = 20)
        Dim label As New Label With {
            .Text = text,
            .Location = New Point(xPos, yPos),
            .AutoSize = True,
            .Font = New Font("Segoe UI", 10, FontStyle.Bold),
            .ForeColor = Color.FromArgb(60, 60, 67)
        }
        Me.Controls.Add(label)
    End Sub

    Private Sub LoadRuleData()
        _nameTextBox.Text = _rule.Name
        _propertyTextBox.Text = _rule.PropertyCode
        _valueTextBox.Text = _rule.TriggerValue
        _messageTextBox.Text = _rule.Message
        _soundCheckBox.Checked = _rule.PlaySound
        _cooldownNumeric.Value = _rule.CooldownMinutes

        ' Sélectionner l'opérateur
        For Each item As ComboBoxItem In _operatorComboBox.Items
            If item.Value = _rule.ComparisonOperator Then
                _operatorComboBox.SelectedItem = item
                Exit For
            End If
        Next

        ' Sélectionner le type
        Select Case _rule.NotificationType
            Case NotificationType.Info
                _typeComboBox.SelectedIndex = 0
            Case NotificationType.Warning
                _typeComboBox.SelectedIndex = 1
            Case NotificationType.Critical
                _typeComboBox.SelectedIndex = 2
        End Select
    End Sub

    Private Sub SaveButton_Click(sender As Object, e As EventArgs)
        ' Valider les champs
        If String.IsNullOrWhiteSpace(_nameTextBox.Text) Then
            MessageBox.Show("Le nom de la règle est obligatoire.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            _nameTextBox.Focus()
            Me.DialogResult = DialogResult.None
            Return
        End If

        If String.IsNullOrWhiteSpace(_propertyTextBox.Text) Then
            MessageBox.Show("Le Property Code est obligatoire.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            _propertyTextBox.Focus()
            Me.DialogResult = DialogResult.None
            Return
        End If

        If String.IsNullOrWhiteSpace(_valueTextBox.Text) Then
            MessageBox.Show("La valeur de déclenchement est obligatoire.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            _valueTextBox.Focus()
            Me.DialogResult = DialogResult.None
            Return
        End If

        If _operatorComboBox.SelectedItem Is Nothing Then
            MessageBox.Show("Veuillez sélectionner un opérateur de comparaison.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            _operatorComboBox.Focus()
            Me.DialogResult = DialogResult.None
            Return
        End If

        ' Sauvegarder les modifications
        _rule.Name = _nameTextBox.Text
        _rule.PropertyCode = _propertyTextBox.Text
        _rule.TriggerValue = _valueTextBox.Text
        _rule.Message = _messageTextBox.Text
        _rule.PlaySound = _soundCheckBox.Checked
        _rule.CooldownMinutes = CInt(_cooldownNumeric.Value)

        ' Opérateur
        Dim selectedOp = CType(_operatorComboBox.SelectedItem, ComboBoxItem)
        _rule.ComparisonOperator = selectedOp.Value

        ' Type
        Select Case _typeComboBox.SelectedIndex
            Case 0
                _rule.NotificationType = NotificationType.Info
            Case 1
                _rule.NotificationType = NotificationType.Warning
            Case 2
                _rule.NotificationType = NotificationType.Critical
        End Select

        Console.WriteLine($"💾 Règle modifiée: {_rule.Name} - {_rule.PropertyCode} {selectedOp.Text} {_rule.TriggerValue}")
    End Sub

    ' Classe helper pour le ComboBox
    Private Class ComboBoxItem
        Public Property Text As String
        Public Property Value As ComparisonOperator

        Public Sub New(text As String, value As ComparisonOperator)
            Me.Text = text
            Me.Value = value
        End Sub

        Public Overrides Function ToString() As String
            Return Text
        End Function
    End Class
End Class