Imports System.Drawing
Imports System.Windows.Forms

Public Class NotificationSettingsForm
    Inherits Form

    Private _notificationManager As NotificationManager
    Private _rulesListView As ListView

    Public Sub New(notificationManager As NotificationManager)
        _notificationManager = notificationManager
        InitializeComponent()
        LoadRules()
    End Sub

    Private Sub InitializeComponent()
        Me.Text = "Gestion des notifications"
        Me.Size = New Size(900, 600)
        Me.StartPosition = FormStartPosition.CenterParent
        Me.BackColor = Color.FromArgb(242, 242, 247)
        Me.FormBorderStyle = FormBorderStyle.Sizable
        Me.MinimumSize = New Size(800, 500)

        ' Header
        Dim headerPanel As New Panel With {
            .Dock = DockStyle.Top,
            .Height = 70,
            .BackColor = Color.FromArgb(45, 45, 48),
            .Padding = New Padding(20)
        }

        Dim titleLabel As New Label With {
            .Text = "Configuration des alertes",
            .Font = New Font("Segoe UI", 16, FontStyle.Bold),
            .ForeColor = Color.White,
            .AutoSize = True,
            .Location = New Point(20, 20)
        }
        headerPanel.Controls.Add(titleLabel)

        Me.Controls.Add(headerPanel)

        ' Panel de boutons en haut
        Dim buttonPanel As New Panel With {
            .Dock = DockStyle.Top,
            .Height = 60,
            .BackColor = Color.FromArgb(242, 242, 247),
            .Padding = New Padding(20, 10, 20, 10)
        }

        Dim addButton As New Button With {
            .Text = "➕ Ajouter une règle",
            .Font = New Font("Segoe UI", 10, FontStyle.Bold),
            .Size = New Size(160, 40),
            .Location = New Point(20, 10),
            .BackColor = Color.FromArgb(52, 199, 89),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand
        }
        addButton.FlatAppearance.BorderSize = 0
        AddHandler addButton.Click, AddressOf AddRule_Click
        buttonPanel.Controls.Add(addButton)

        Dim editButton As New Button With {
            .Text = "✏️ Modifier",
            .Font = New Font("Segoe UI", 10),
            .Size = New Size(120, 40),
            .Location = New Point(190, 10),
            .BackColor = Color.FromArgb(0, 122, 255),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand
        }
        editButton.FlatAppearance.BorderSize = 0
        AddHandler editButton.Click, AddressOf EditRule_Click
        buttonPanel.Controls.Add(editButton)

        Dim deleteButton As New Button With {
            .Text = "🗑️ Supprimer",
            .Font = New Font("Segoe UI", 10),
            .Size = New Size(120, 40),
            .Location = New Point(320, 10),
            .BackColor = Color.FromArgb(255, 59, 48),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand
        }
        deleteButton.FlatAppearance.BorderSize = 0
        AddHandler deleteButton.Click, AddressOf DeleteRule_Click
        buttonPanel.Controls.Add(deleteButton)

        Dim resetButton As New Button With {
            .Text = "🔄 Réinitialiser",
            .Font = New Font("Segoe UI", 10),
            .Size = New Size(140, 40),
            .Location = New Point(450, 10),
            .BackColor = Color.FromArgb(255, 149, 0),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand
        }
        resetButton.FlatAppearance.BorderSize = 0
        AddHandler resetButton.Click, AddressOf ResetRules_Click
        buttonPanel.Controls.Add(resetButton)

        Me.Controls.Add(buttonPanel)

        ' ListView des règles
        _rulesListView = New ListView With {
            .Dock = DockStyle.Fill,
            .View = View.Details,
            .FullRowSelect = True,
            .GridLines = True,
            .Font = New Font("Segoe UI", 10),
            .BackColor = Color.White,
            .MultiSelect = False
        }

        ' Colonnes
        _rulesListView.Columns.Add("Activée", 70, HorizontalAlignment.Center)
        _rulesListView.Columns.Add("Nom", 180, HorizontalAlignment.Left)
        _rulesListView.Columns.Add("Niveau", 100, HorizontalAlignment.Center)
        _rulesListView.Columns.Add("Property", 150, HorizontalAlignment.Left)
        _rulesListView.Columns.Add("Opérateur", 80, HorizontalAlignment.Center)
        _rulesListView.Columns.Add("Valeur", 80, HorizontalAlignment.Left)
        _rulesListView.Columns.Add("Son", 60, HorizontalAlignment.Center)
        _rulesListView.Columns.Add("Cooldown", 100, HorizontalAlignment.Right)
        _rulesListView.Columns.Add("Message", 200, HorizontalAlignment.Left)

        AddHandler _rulesListView.DoubleClick, AddressOf EditRule_Click
        AddHandler _rulesListView.ItemChecked, AddressOf RuleChecked_Changed

        _rulesListView.CheckBoxes = True

        Me.Controls.Add(_rulesListView)

        ' Panel de boutons en bas
        Dim bottomPanel As New Panel With {
            .Dock = DockStyle.Bottom,
            .Height = 60,
            .BackColor = Color.FromArgb(242, 242, 247),
            .Padding = New Padding(20, 10, 20, 10)
        }

        Dim closeButton As New Button With {
            .Text = "Fermer",
            .Font = New Font("Segoe UI", 10),
            .Size = New Size(100, 40),
            .BackColor = Color.FromArgb(142, 142, 147),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand,
            .DialogResult = DialogResult.OK
        }
        closeButton.FlatAppearance.BorderSize = 0
        bottomPanel.Controls.Add(closeButton)

        AddHandler bottomPanel.Resize, Sub(s, e)
                                           closeButton.Location = New Point(bottomPanel.Width - closeButton.Width - 20, 10)
                                       End Sub
        closeButton.Location = New Point(bottomPanel.Width - closeButton.Width - 20, 10)

        Me.Controls.Add(bottomPanel)
        Me.AcceptButton = closeButton
    End Sub

    Private Sub LoadRules()
        Console.WriteLine("📋 LoadRules appelé")
        _rulesListView.Items.Clear()

        Dim index As Integer = 0
        For Each rule As NotificationRule In _notificationManager.GetRules()
            Dim item As New ListViewItem()
            item.Checked = rule.IsEnabled
            item.SubItems.Add(rule.Name)

            ' Icône + niveau
            Dim levelText As String = ""
            Select Case rule.NotificationType
                Case NotificationType.Critical
                    levelText = "🚨 Critique"
                Case NotificationType.Warning
                    levelText = "⚠️ Alerte"
                Case NotificationType.Info
                    levelText = "ℹ️ Info"
            End Select
            item.SubItems.Add(levelText)

            item.SubItems.Add(rule.PropertyCode)

            ' Afficher l'opérateur
            Dim operatorText As String = ""
            Select Case rule.ComparisonOperator
                Case ComparisonOperator.GreaterThan
                    operatorText = ">"
                Case ComparisonOperator.GreaterOrEqual
                    operatorText = ">="
                Case ComparisonOperator.LessThan
                    operatorText = "<"
                Case ComparisonOperator.LessOrEqual
                    operatorText = "<="
                Case ComparisonOperator.Equal
                    operatorText = "="
            End Select
            item.SubItems.Add(operatorText)

            item.SubItems.Add(rule.TriggerValue)
            item.SubItems.Add(If(rule.PlaySound, "🔊", "🔇"))
            item.SubItems.Add($"{rule.CooldownMinutes} min")
            item.SubItems.Add(rule.Message)

            ' ✅ Stocker l'INDEX au lieu de l'objet
            item.Tag = index
            _rulesListView.Items.Add(item)

            index += 1
        Next

        Console.WriteLine($"📋 {index} règles affichées")
    End Sub

    Private Sub RuleChecked_Changed(sender As Object, e As ItemCheckedEventArgs)
        If e.Item.Tag IsNot Nothing Then
            Dim index As Integer = CInt(e.Item.Tag)
            Console.WriteLine($"🔘 Checkbox changée pour la règle index {index}")

            ' ✅ Utiliser ToggleRule qui sauvegarde automatiquement
            _notificationManager.ToggleRule(index)
        End If
    End Sub

    Private Sub AddRule_Click(sender As Object, e As EventArgs)
        Console.WriteLine("➕ Bouton Ajouter cliqué")

        Dim newRule As New NotificationRule With {
            .Name = "Nouvelle règle",
            .PropertyCode = "",
            .TriggerValue = "",
            .ComparisonOperator = ComparisonOperator.GreaterOrEqual,
            .NotificationType = NotificationType.Info,
            .Message = "Nouvelle alerte",
            .PlaySound = False,
            .CooldownMinutes = 5,
            .IsEnabled = True
        }

        Using editor As New NotificationRuleEditor(newRule)
            If editor.ShowDialog() = DialogResult.OK Then
                Console.WriteLine($"✅ Ajout de la règle: {newRule.Name}")
                ' ✅ Utiliser AddRule qui sauvegarde automatiquement
                _notificationManager.AddRule(newRule)
                LoadRules()
            End If
        End Using
    End Sub

    Private Sub EditRule_Click(sender As Object, e As EventArgs)
        If _rulesListView.SelectedItems.Count = 0 Then
            MessageBox.Show("Veuillez sélectionner une règle à modifier.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim index As Integer = CInt(_rulesListView.SelectedItems(0).Tag)
        Dim selectedRule = _notificationManager.GetRules()(index)

        Console.WriteLine($"✏️ Edition de la règle index {index}: {selectedRule.Name}")

        Using editor As New NotificationRuleEditor(selectedRule)
            If editor.ShowDialog() = DialogResult.OK Then
                Console.WriteLine($"✅ Mise à jour de la règle: {selectedRule.Name}")
                ' ✅ Utiliser UpdateRule qui sauvegarde automatiquement
                _notificationManager.UpdateRule(index, selectedRule)
                LoadRules()
            End If
        End Using
    End Sub

    Private Sub DeleteRule_Click(sender As Object, e As EventArgs)
        If _rulesListView.SelectedItems.Count = 0 Then
            MessageBox.Show("Veuillez sélectionner une règle à supprimer.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim result = MessageBox.Show(
            "Voulez-vous vraiment supprimer cette règle ?",
            "Confirmation",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question)

        If result = DialogResult.Yes Then
            Dim index As Integer = CInt(_rulesListView.SelectedItems(0).Tag)
            Dim selectedRule = _notificationManager.GetRules()(index)

            Console.WriteLine($"🗑️ Suppression de la règle index {index}: {selectedRule.Name}")

            ' ✅ Utiliser DeleteRule qui sauvegarde automatiquement
            _notificationManager.DeleteRule(index)
            LoadRules()
        End If
    End Sub

    Private Sub ResetRules_Click(sender As Object, e As EventArgs)
        Dim result = MessageBox.Show(
            "Voulez-vous vraiment réinitialiser toutes les règles aux valeurs par défaut ?" & Environment.NewLine & Environment.NewLine &
            "Toutes vos modifications seront perdues !",
            "Confirmation",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning)

        If result = DialogResult.Yes Then
            Console.WriteLine("🔄 Réinitialisation des règles")
            _notificationManager.ResetToDefaults()
            LoadRules()
            MessageBox.Show("Les règles ont été réinitialisées aux valeurs par défaut.", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End If
    End Sub
End Class