Imports System.Drawing
Imports System.Windows.Forms
Imports Newtonsoft.Json.Linq

''' <summary>
''' Formulaire de gestion des automatisations Tuya (Scenes et Automations)
''' Phase 1: Lecture seule, activation/désactivation, suppression
''' </summary>
Public Class AutomationForm
    Inherits Form

#Region "Constantes de couleurs et styles"
    Private Shared ReadOnly DarkBg As Color = Color.FromArgb(45, 45, 48)
    Private Shared ReadOnly LightBg As Color = Color.FromArgb(242, 242, 247)
    Private Shared ReadOnly SecondaryBg As Color = Color.FromArgb(55, 55, 58)
    Private Shared ReadOnly ActiveBlue As Color = Color.FromArgb(0, 122, 255)
    Private Shared ReadOnly InactiveGray As Color = Color.FromArgb(142, 142, 147)
    Private Shared ReadOnly CriticalRed As Color = Color.FromArgb(255, 59, 48)
    Private Shared ReadOnly SuccessGreen As Color = Color.FromArgb(52, 199, 89)
    Private Shared ReadOnly WarningOrange As Color = Color.FromArgb(255, 149, 0)
    Private Shared ReadOnly CardBg As Color = Color.White
#End Region

#Region "Champs privés"
    Private _apiClient As TuyaApiClient
    Private _treeView As TreeView
    Private _detailsPanel As Panel
    Private _detailsTextBox As RichTextBox
    Private _btnRefresh As Button
    Private _btnCreate As Button
    Private _btnEnable As Button
    Private _btnDisable As Button
    Private _btnEdit As Button
    Private _btnDelete As Button
    Private _statusLabel As Label
    Private _homeComboBox As ComboBox

    ' Données
    Private _automationsData As New Dictionary(Of String, JToken)
    Private _selectedAutomationId As String = Nothing
    Private _selectedHomeId As String = Nothing
#End Region

#Region "Initialisation"
    Public Sub New(apiClient As TuyaApiClient)
        _apiClient = apiClient
        InitializeComponent()
    End Sub

    Private Sub InitializeComponent()
        ' Configuration du formulaire
        Text = "⚡ Gestion des Automatisations Tuya"
        Size = New Size(1400, 800)
        StartPosition = FormStartPosition.CenterScreen
        BackColor = LightBg
        MinimumSize = New Size(1000, 600)

        ' Créer le layout principal
        CreateMainLayout()
    End Sub

    Private Sub CreateMainLayout()
        ' TableLayoutPanel principal - 3 lignes (Toolbar, Contenu, Statut)
        Dim mainLayout = New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .BackColor = LightBg,
            .CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
            .Padding = New Padding(0)
        }

        ' Configuration des lignes
        mainLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 60))    ' Toolbar
        mainLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))    ' Contenu
        mainLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 26))    ' Statut

        mainLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))

        ' Créer les 3 sections
        Dim toolbarPanel = CreateToolbarPanel()
        Dim contentPanel = CreateContentPanel()
        Dim statusPanel = CreateStatusPanel()

        ' Ajouter au layout principal
        mainLayout.Controls.Add(toolbarPanel, 0, 0)
        mainLayout.Controls.Add(contentPanel, 0, 1)
        mainLayout.Controls.Add(statusPanel, 0, 2)

        ' Ajouter le layout au formulaire
        Me.Controls.Add(mainLayout)
    End Sub

    Private Function CreateToolbarPanel() As Panel
        Dim panel = New Panel With {
            .Dock = DockStyle.Fill,
            .BackColor = DarkBg,
            .Margin = New Padding(0)
        }

        ' Titre
        Dim titleLabel = New Label With {
            .Text = "⚡ Gestion des Automatisations",
            .Font = New Font("Segoe UI", 12, FontStyle.Bold),
            .ForeColor = Color.White,
            .AutoSize = True,
            .Location = New Point(15, 8)
        }
        panel.Controls.Add(titleLabel)

        ' Label Logement
        Dim homeLabel = New Label With {
            .Text = "Logement:",
            .Font = New Font("Segoe UI", 9),
            .ForeColor = Color.White,
            .AutoSize = True,
            .Location = New Point(15, 35)
        }
        panel.Controls.Add(homeLabel)

        ' ComboBox Logement
        _homeComboBox = New ComboBox With {
            .Font = New Font("Segoe UI", 9),
            .DropDownStyle = ComboBoxStyle.DropDownList,
            .Size = New Size(250, 25),
            .Location = New Point(85, 32)
        }
        AddHandler _homeComboBox.SelectedIndexChanged, AddressOf HomeComboBox_SelectedIndexChanged
        panel.Controls.Add(_homeComboBox)

        ' Bouton Créer
        _btnCreate = New Button With {
            .Text = "➕ Créer",
            .Font = New Font("Segoe UI", 9, FontStyle.Bold),
            .ForeColor = Color.White,
            .BackColor = ActiveBlue,
            .FlatStyle = FlatStyle.Flat,
            .Size = New Size(110, 32),
            .Cursor = Cursors.Hand
        }
        _btnCreate.FlatAppearance.BorderSize = 0
        AddHandler _btnCreate.Click, AddressOf BtnCreate_Click
        panel.Controls.Add(_btnCreate)

        ' Bouton rafraîchir
        _btnRefresh = New Button With {
            .Text = "🔄 Rafraîchir",
            .Font = New Font("Segoe UI", 9, FontStyle.Bold),
            .ForeColor = Color.White,
            .BackColor = SuccessGreen,
            .FlatStyle = FlatStyle.Flat,
            .Size = New Size(130, 32),
            .Cursor = Cursors.Hand
        }
        _btnRefresh.FlatAppearance.BorderSize = 0
        AddHandler _btnRefresh.Click, AddressOf BtnRefresh_Click
        panel.Controls.Add(_btnRefresh)

        ' Positionner les boutons à droite
        AddHandler panel.Resize, Sub(s, e)
                                     _btnRefresh.Location = New Point(panel.Width - _btnRefresh.Width - 15, 14)
                                     _btnCreate.Location = New Point(_btnRefresh.Left - _btnCreate.Width - 10, 14)
                                 End Sub

        Return panel
    End Function

    Private Function CreateContentPanel() As Panel
        Dim panel = New Panel With {
            .Dock = DockStyle.Fill,
            .BackColor = LightBg,
            .Margin = New Padding(0)
        }

        ' SplitContainer pour séparer TreeView (gauche) et Détails (droite)
        Dim contentSplitter = New SplitContainer With {
            .Dock = DockStyle.Fill,
            .Orientation = Orientation.Vertical,
            .BackColor = InactiveGray,
            .SplitterWidth = 3,
            .IsSplitterFixed = False,
            .SplitterDistance = 450
        }

        ' Panel gauche: TreeView + Boutons d'action
        Dim leftPanel = CreateLeftPanel()
        contentSplitter.Panel1.Controls.Add(leftPanel)

        ' Panel droit: Détails de l'automatisation
        _detailsPanel = CreateDetailsPanel()
        contentSplitter.Panel2.Controls.Add(_detailsPanel)

        panel.Controls.Add(contentSplitter)
        Return panel
    End Function

    Private Function CreateLeftPanel() As Panel
        Dim panel = New Panel With {
            .Dock = DockStyle.Fill,
            .BackColor = Color.White,
            .Padding = New Padding(10),
            .Margin = New Padding(0)
        }

        ' TreeView
        _treeView = New TreeView With {
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 10),
            .BorderStyle = BorderStyle.None,
            .BackColor = Color.White,
            .ShowLines = True,
            .ShowPlusMinus = True,
            .ShowRootLines = True,
            .HideSelection = False,
            .ImageList = CreateImageList(),
            .Indent = 25,
            .ItemHeight = 24
        }
        AddHandler _treeView.AfterSelect, AddressOf TreeView_AfterSelect

        ' Panel des boutons d'action
        Dim buttonsPanel = New FlowLayoutPanel With {
            .Dock = DockStyle.Bottom,
            .Height = 50,
            .FlowDirection = FlowDirection.LeftToRight,
            .BackColor = LightBg,
            .Padding = New Padding(5)
        }

        ' Bouton Modifier
        _btnEdit = CreateActionButton("✏️ Modifier", ActiveBlue)
        AddHandler _btnEdit.Click, AddressOf BtnEdit_Click

        ' Bouton Activer
        _btnEnable = CreateActionButton("✅ Activer", SuccessGreen)
        AddHandler _btnEnable.Click, AddressOf BtnEnable_Click

        ' Bouton Désactiver
        _btnDisable = CreateActionButton("⏸️ Désactiver", WarningOrange)
        AddHandler _btnDisable.Click, AddressOf BtnDisable_Click

        ' Bouton Supprimer
        _btnDelete = CreateActionButton("🗑️ Supprimer", CriticalRed)
        AddHandler _btnDelete.Click, AddressOf BtnDelete_Click

        buttonsPanel.Controls.AddRange({_btnEdit, _btnEnable, _btnDisable, _btnDelete})
        panel.Controls.AddRange({_treeView, buttonsPanel})

        Return panel
    End Function

    Private Function CreateActionButton(text As String, color As Color) As Button
        Dim btn = New Button With {
            .Text = text,
            .Font = New Font("Segoe UI", 9, FontStyle.Bold),
            .ForeColor = Color.White,
            .BackColor = color,
            .FlatStyle = FlatStyle.Flat,
            .Size = New Size(140, 35),
            .Cursor = Cursors.Hand,
            .Enabled = False,
            .Margin = New Padding(3)
        }
        btn.FlatAppearance.BorderSize = 0
        Return btn
    End Function

    Private Function CreateDetailsPanel() As Panel
        Dim panel = New Panel With {
            .Dock = DockStyle.Fill,
            .BackColor = CardBg,
            .Padding = New Padding(20),
            .Margin = New Padding(0)
        }

        ' Titre
        Dim titleLabel = New Label With {
            .Text = "Détails de l'automatisation",
            .Font = New Font("Segoe UI", 14, FontStyle.Bold),
            .ForeColor = DarkBg,
            .AutoSize = True,
            .Location = New Point(20, 20)
        }
        panel.Controls.Add(titleLabel)

        ' RichTextBox pour afficher les détails
        _detailsTextBox = New RichTextBox With {
            .Location = New Point(20, 55),
            .Size = New Size(panel.Width - 40, panel.Height - 75),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right,
            .Font = New Font("Segoe UI", 10),
            .BackColor = CardBg,
            .BorderStyle = BorderStyle.None,
            .ReadOnly = True,
            .Text = "Sélectionnez une automatisation pour voir ses détails..."
        }
        panel.Controls.Add(_detailsTextBox)

        Return panel
    End Function

    Private Function CreateStatusPanel() As Panel
        Dim panel = New Panel With {
            .Dock = DockStyle.Fill,
            .BackColor = DarkBg,
            .Margin = New Padding(0)
        }

        _statusLabel = New Label With {
            .Text = "Prêt",
            .Font = New Font("Segoe UI", 9),
            .ForeColor = Color.LightGray,
            .AutoSize = True,
            .Location = New Point(10, 5)
        }
        panel.Controls.Add(_statusLabel)

        Return panel
    End Function

    Private Function CreateImageList() As ImageList
        Dim imageList = New ImageList With {
            .ImageSize = New Size(16, 16),
            .ColorDepth = ColorDepth.Depth32Bit
        }

        ' Créer des images simples (icônes colorées)
        imageList.Images.Add("scene", CreateColorIcon(ActiveBlue))      ' Scène
        imageList.Images.Add("automation", CreateColorIcon(SuccessGreen)) ' Automation
        imageList.Images.Add("enabled", CreateColorIcon(SuccessGreen))   ' Activée
        imageList.Images.Add("disabled", CreateColorIcon(InactiveGray))  ' Désactivée

        Return imageList
    End Function

    Private Function CreateColorIcon(color As Color) As Bitmap
        Dim bmp = New Bitmap(16, 16)
        Using g As Graphics = Graphics.FromImage(bmp)
            g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
            Using brush As New SolidBrush(color)
                g.FillEllipse(brush, 2, 2, 12, 12)
            End Using
        End Using
        Return bmp
    End Function
#End Region

#Region "Chargement des données"
    Protected Overrides Async Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        Await LoadHomesAsync()
    End Sub

    Private Async Function LoadHomesAsync() As Task
        Try
            UpdateStatus("Chargement des logements...")
            _homeComboBox.Items.Clear()

            Dim homes = Await _apiClient.GetHomesAsync()

            If homes.Count = 0 Then
                UpdateStatus("Aucun logement trouvé")
                MessageBox.Show("Aucun logement trouvé. Veuillez créer un logement d'abord.", "Information",
                              MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            For Each home As JToken In homes
                Dim homeId = GetJsonString(home, "home_id")
                Dim homeName = GetJsonString(home, "name")

                If Not String.IsNullOrEmpty(homeId) Then
                    _homeComboBox.Items.Add(New HomeItem With {
                        .Id = homeId,
                        .Name = homeName
                    })
                End If
            Next

            If _homeComboBox.Items.Count > 0 Then
                _homeComboBox.SelectedIndex = 0
            End If

            UpdateStatus($"{homes.Count} logement(s) chargé(s)")
        Catch ex As Exception
            UpdateStatus($"Erreur: {ex.Message}")
            MessageBox.Show($"Erreur lors du chargement des logements: {ex.Message}", "Erreur",
                          MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Function

    Private Async Function LoadAutomationsAsync() As Task
        If _selectedHomeId Is Nothing Then Return

        Try
            UpdateStatus("Chargement des automatisations...")
            _treeView.Nodes.Clear()
            _automationsData.Clear()

            Dim automations = Await _apiClient.GetAutomationsAsync(_selectedHomeId)

            If automations.Count = 0 Then
                UpdateStatus("Aucune automatisation trouvée")
                _detailsTextBox.Text = "Aucune automatisation trouvée pour ce logement." & vbCrLf & vbCrLf &
                    "IMPORTANT: Si vous voyez une erreur 'API not subscribed' dans la console de debug, " &
                    "vous devez activer l'API 'Scene Automation' dans votre projet Tuya IoT Cloud Platform." & vbCrLf & vbCrLf &
                    "Étapes:" & vbCrLf &
                    "1. Allez sur https://iot.tuya.com" & vbCrLf &
                    "2. Sélectionnez votre projet Cloud" & vbCrLf &
                    "3. Onglet 'API' → Cherchez 'Smart Scene Management'" & vbCrLf &
                    "4. Cliquez sur 'Subscribe' (gratuit)" & vbCrLf &
                    "5. Attendez 1-5 minutes et réessayez"
                Return
            End If

            ' Créer les nœuds racine
            Dim scenesNode = New TreeNode("🎬 Scènes") With {.ImageKey = "scene", .SelectedImageKey = "scene"}
            Dim automationsNode = New TreeNode("⚡ Automatisations") With {.ImageKey = "automation", .SelectedImageKey = "automation"}

            Dim sceneCount = 0
            Dim automationCount = 0

            For Each automation As JToken In automations
                ' L'API retourne différents champs selon l'endpoint:
                ' - /v1.0/homes/{home_id}/automations → "automation_id"
                ' - /v1.1/homes/{home_id}/scenes → "scene_id"
                Dim autoId = GetJsonString(automation, "automation_id")
                If String.IsNullOrEmpty(autoId) Then
                    autoId = GetJsonString(automation, "scene_id")
                End If

                Dim autoName = GetJsonString(automation, "name")
                Dim enabled = GetJsonBool(automation, "enabled")

                ' Le champ "status" indique le type:
                ' "1" = automation (déclenchée automatiquement)
                ' "2" = scene (tap-to-run, déclenchement manuel)
                Dim status = GetJsonString(automation, "status")

                If String.IsNullOrEmpty(autoId) Then
                    ' Pas d'ID valide - ignorer cette automatisation
                    Continue For
                End If

                ' Stocker les données
                _automationsData(autoId) = automation

                ' Créer le nœud
                Dim icon = If(enabled, "enabled", "disabled")
                Dim statusText = If(enabled, "✅", "⏸️")
                Dim node = New TreeNode($"{statusText} {autoName}") With {
                    .Tag = autoId,
                    .ImageKey = icon,
                    .SelectedImageKey = icon
                }

                ' Ajouter au bon groupe selon le status
                ' status "2" = scène manuelle (tap-to-run)
                ' status "1" = automation (déclenchée automatiquement)
                If status = "2" Then
                    scenesNode.Nodes.Add(node)
                    sceneCount += 1
                Else
                    automationsNode.Nodes.Add(node)
                    automationCount += 1
                End If
            Next

            ' Ajouter les nœuds au TreeView
            If sceneCount > 0 Then
                scenesNode.Text = $"🎬 Scènes ({sceneCount})"
                _treeView.Nodes.Add(scenesNode)
                scenesNode.Expand()
            End If

            If automationCount > 0 Then
                automationsNode.Text = $"⚡ Automatisations ({automationCount})"
                _treeView.Nodes.Add(automationsNode)
                automationsNode.Expand()
            End If

            UpdateStatus($"{automations.Count} automatisation(s) chargée(s)")
        Catch ex As Exception
            UpdateStatus($"Erreur: {ex.Message}")
            MessageBox.Show($"Erreur lors du chargement des automatisations: {ex.Message}", "Erreur",
                          MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Function
#End Region

#Region "Événements des contrôles"
    Private Async Sub HomeComboBox_SelectedIndexChanged(sender As Object, e As EventArgs)
        If _homeComboBox.SelectedItem Is Nothing Then Return

        Dim selectedHome = CType(_homeComboBox.SelectedItem, HomeItem)
        _selectedHomeId = selectedHome.Id

        Await LoadAutomationsAsync()
    End Sub

    Private Async Sub BtnRefresh_Click(sender As Object, e As EventArgs)
        Await LoadAutomationsAsync()
    End Sub

    Private Async Sub TreeView_AfterSelect(sender As Object, e As TreeViewEventArgs)
        Dim node = e.Node
        If node?.Tag Is Nothing Then
            ' Nœud racine sélectionné
            _selectedAutomationId = Nothing
            _btnEdit.Enabled = False
            _btnEnable.Enabled = False
            _btnDisable.Enabled = False
            _btnDelete.Enabled = False
            _detailsTextBox.Text = "Sélectionnez une automatisation pour voir ses détails..."
            Return
        End If

        ' Automatisation sélectionnée
        _selectedAutomationId = node.Tag.ToString()
        Await LoadAutomationDetailsAsync(_selectedAutomationId)

        ' Activer les boutons
        If _automationsData.ContainsKey(_selectedAutomationId) Then
            Dim enabled = GetJsonBool(_automationsData(_selectedAutomationId), "enabled")
            _btnEdit.Enabled = True
            _btnEnable.Enabled = Not enabled
            _btnDisable.Enabled = enabled
            _btnDelete.Enabled = True
        End If
    End Sub

    Private Async Function LoadAutomationDetailsAsync(automationId As String) As Task
        Try
            UpdateStatus("Chargement des détails...")

            ' L'API dépréciée retourne déjà toutes les infos dans la liste
            ' Pas besoin d'appeler GetAutomationDetailsAsync
            If _automationsData.ContainsKey(automationId) Then
                DisplayAutomationInfo(_automationsData(automationId))
            Else
                _detailsTextBox.Text = "Détails non disponibles"
            End If

            UpdateStatus("Prêt")
        Catch ex As Exception
            _detailsTextBox.Text = $"Erreur lors du chargement des détails: {ex.Message}"
            UpdateStatus($"Erreur: {ex.Message}")
        End Try
    End Function

    Private Sub DisplayAutomationInfo(automation As JToken)
        Dim sb = New System.Text.StringBuilder()

        ' Informations générales
        sb.AppendLine("━━━ INFORMATIONS GÉNÉRALES ━━━")

        Dim name = GetJsonString(automation, "name")
        Dim status = GetJsonString(automation, "status")
        Dim enabled = GetJsonBool(automation, "enabled")
        Dim sceneId = GetJsonString(automation, "scene_id")

        sb.AppendLine($"Nom: {name}")
        sb.AppendLine($"ID: {sceneId}")
        sb.AppendLine($"Type: {If(status = "2", "🎬 Scène (tap-to-run)", "⚡ Automatisation (déclenchée)")}")
        sb.AppendLine($"État: {If(enabled, "✅ Activée", "⏸️ Désactivée")}")
        sb.AppendLine()

        ' Actions
        Dim actions = automation("actions")
        If actions IsNot Nothing AndAlso TypeOf actions Is JArray Then
            sb.AppendLine("━━━ ACTIONS (THEN) ━━━")
            Dim actArray = CType(actions, JArray)
            If actArray.Count > 0 Then
                For Each action As JToken In actArray
                    sb.AppendLine($"• {FormatAction(action)}")
                Next
            Else
                sb.AppendLine("Aucune action définie")
            End If
            sb.AppendLine()
        End If

        _detailsTextBox.Text = sb.ToString()
    End Sub

    Private Sub DisplayAutomationDetails(details As JObject)
        Dim sb = New System.Text.StringBuilder()

        ' Informations générales
        sb.AppendLine("━━━ INFORMATIONS GÉNÉRALES ━━━")
        sb.AppendLine($"Nom: {GetJsonString(details, "name")}")
        sb.AppendLine($"Type: {GetJsonString(details, "type")}")
        sb.AppendLine($"État: {If(GetJsonBool(details, "enabled"), "✅ Activée", "⏸️ Désactivée")}")
        sb.AppendLine()

        ' Déclencheurs (preconditions)
        Dim preconditions = details("preconditions")
        If preconditions IsNot Nothing AndAlso TypeOf preconditions Is JArray Then
            sb.AppendLine("━━━ DÉCLENCHEURS (IF) ━━━")
            Dim preArray = CType(preconditions, JArray)
            If preArray.Count > 0 Then
                For Each condition As JToken In preArray
                    sb.AppendLine($"• {FormatCondition(condition)}")
                Next
            Else
                sb.AppendLine("Aucun déclencheur")
            End If
            sb.AppendLine()
        End If

        ' Conditions
        Dim conditions = details("conditions")
        If conditions IsNot Nothing AndAlso TypeOf conditions Is JArray Then
            sb.AppendLine("━━━ CONDITIONS (AND) ━━━")
            Dim condArray = CType(conditions, JArray)
            If condArray.Count > 0 Then
                For Each condition As JToken In condArray
                    sb.AppendLine($"• {FormatCondition(condition)}")
                Next
            Else
                sb.AppendLine("Aucune condition")
            End If
            sb.AppendLine()
        End If

        ' Actions
        Dim actions = details("actions")
        If actions IsNot Nothing AndAlso TypeOf actions Is JArray Then
            sb.AppendLine("━━━ ACTIONS (THEN) ━━━")
            Dim actArray = CType(actions, JArray)
            If actArray.Count > 0 Then
                For Each action As JToken In actArray
                    sb.AppendLine($"• {FormatAction(action)}")
                Next
            Else
                sb.AppendLine("Aucune action")
            End If
            sb.AppendLine()
        End If

        _detailsTextBox.Text = sb.ToString()
    End Sub

    Private Function FormatCondition(condition As JToken) As String
        ' Format basique - peut être amélioré
        Dim entityType = GetJsonString(condition, "entity_type")
        Dim display = GetJsonString(condition, "display")

        If Not String.IsNullOrEmpty(display) Then
            Return display
        ElseIf Not String.IsNullOrEmpty(entityType) Then
            Return $"{entityType}: {condition.ToString()}"
        Else
            Return condition.ToString()
        End If
    End Function

    Private Function FormatAction(action As JToken) As String
        ' Format basique - peut être amélioré
        Dim entityName = GetJsonString(action, "entity_name")
        Dim actionType = GetJsonString(action, "action_executor")

        If Not String.IsNullOrEmpty(entityName) Then
            Return $"{entityName}: {actionType}"
        Else
            Return action.ToString()
        End If
    End Function

    Private Async Sub BtnEnable_Click(sender As Object, e As EventArgs)
        If String.IsNullOrEmpty(_selectedAutomationId) Then Return

        Try
            _btnEnable.Enabled = False
            UpdateStatus("Activation en cours...")

            Dim success = Await _apiClient.EnableAutomationAsync(_selectedHomeId, _selectedAutomationId)

            If success Then
                MessageBox.Show("Automatisation activée avec succès!", "Succès",
                              MessageBoxButtons.OK, MessageBoxIcon.Information)
                Await LoadAutomationsAsync()
            Else
                MessageBox.Show("L'activation a échoué. Vérifiez les logs.", "Erreur",
                              MessageBoxButtons.OK, MessageBoxIcon.Error)
            End If

            UpdateStatus("Prêt")
        Catch ex As Exception
            MessageBox.Show($"Erreur: {ex.Message}", "Erreur",
                          MessageBoxButtons.OK, MessageBoxIcon.Error)
            UpdateStatus($"Erreur: {ex.Message}")
        Finally
            _btnEnable.Enabled = True
        End Try
    End Sub

    Private Async Sub BtnDisable_Click(sender As Object, e As EventArgs)
        If String.IsNullOrEmpty(_selectedAutomationId) Then Return

        Try
            _btnDisable.Enabled = False
            UpdateStatus("Désactivation en cours...")

            Dim success = Await _apiClient.DisableAutomationAsync(_selectedHomeId, _selectedAutomationId)

            If success Then
                MessageBox.Show("Automatisation désactivée avec succès!", "Succès",
                              MessageBoxButtons.OK, MessageBoxIcon.Information)
                Await LoadAutomationsAsync()
            Else
                MessageBox.Show("La désactivation a échoué. Vérifiez les logs.", "Erreur",
                              MessageBoxButtons.OK, MessageBoxIcon.Error)
            End If

            UpdateStatus("Prêt")
        Catch ex As Exception
            MessageBox.Show($"Erreur: {ex.Message}", "Erreur",
                          MessageBoxButtons.OK, MessageBoxIcon.Error)
            UpdateStatus($"Erreur: {ex.Message}")
        Finally
            _btnDisable.Enabled = True
        End Try
    End Sub

    Private Async Sub BtnDelete_Click(sender As Object, e As EventArgs)
        If String.IsNullOrEmpty(_selectedAutomationId) Then Return

        Dim autoName = ""
        If _automationsData.ContainsKey(_selectedAutomationId) Then
            autoName = GetJsonString(_automationsData(_selectedAutomationId), "name")
        End If

        Dim result = MessageBox.Show(
            $"Voulez-vous vraiment supprimer l'automatisation '{autoName}'?{vbCrLf}{vbCrLf}Cette action est irréversible.",
            "Confirmation de suppression",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning)

        If result <> DialogResult.Yes Then Return

        Try
            _btnDelete.Enabled = False
            UpdateStatus("Suppression en cours...")

            Dim success = Await _apiClient.DeleteAutomationAsync(_selectedHomeId, _selectedAutomationId)

            If success Then
                MessageBox.Show("Automatisation supprimée avec succès!", "Succès",
                              MessageBoxButtons.OK, MessageBoxIcon.Information)
                _selectedAutomationId = Nothing
                Await LoadAutomationsAsync()
            Else
                MessageBox.Show("La suppression a échoué. Vérifiez les logs.", "Erreur",
                              MessageBoxButtons.OK, MessageBoxIcon.Error)
            End If

            UpdateStatus("Prêt")
        Catch ex As Exception
            MessageBox.Show($"Erreur: {ex.Message}", "Erreur",
                          MessageBoxButtons.OK, MessageBoxIcon.Error)
            UpdateStatus($"Erreur: {ex.Message}")
        Finally
            _btnDelete.Enabled = True
        End Try
    End Sub

    Private Async Sub BtnCreate_Click(sender As Object, e As EventArgs)
        If String.IsNullOrEmpty(_selectedHomeId) Then
            MessageBox.Show("Veuillez sélectionner un logement.", "Information",
                          MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Try
            Using editorForm As New AutomationEditorForm(_apiClient, _selectedHomeId)
                If editorForm.ShowDialog() = DialogResult.OK Then
                    ' Rafraîchir la liste des automatisations
                    Await LoadAutomationsAsync()
                End If
            End Using
        Catch ex As Exception
            MessageBox.Show($"Erreur lors de l'ouverture de l'éditeur: {ex.Message}", "Erreur",
                          MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Async Sub BtnEdit_Click(sender As Object, e As EventArgs)
        If String.IsNullOrEmpty(_selectedAutomationId) Then Return
        If String.IsNullOrEmpty(_selectedHomeId) Then Return

        Try
            If Not _automationsData.ContainsKey(_selectedAutomationId) Then
                MessageBox.Show("Données de l'automatisation non disponibles.", "Erreur",
                              MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            Dim existingData = _automationsData(_selectedAutomationId)
            Using editorForm As New AutomationEditorForm(_apiClient, _selectedHomeId, _selectedAutomationId, existingData)
                If editorForm.ShowDialog() = DialogResult.OK Then
                    ' Rafraîchir la liste des automatisations
                    Await LoadAutomationsAsync()
                End If
            End Using
        Catch ex As Exception
            MessageBox.Show($"Erreur lors de l'ouverture de l'éditeur: {ex.Message}", "Erreur",
                          MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub
#End Region

#Region "Méthodes utilitaires"
    Private Sub UpdateStatus(message As String)
        _statusLabel.Text = message
    End Sub

    Private Function GetJsonString(token As JToken, key As String) As String
        Return token?.SelectToken(key)?.ToString()
    End Function

    Private Function GetJsonBool(token As JToken, key As String) As Boolean
        Dim value = token?.SelectToken(key)
        If value Is Nothing Then Return False

        If TypeOf value Is JValue Then
            Try
                Return CBool(CType(value, JValue).Value)
            Catch
                Return False
            End Try
        End If

        Return False
    End Function
#End Region
End Class

''' <summary>
''' Classe pour représenter un Home dans la ComboBox
''' </summary>
Public Class HomeItem
    Public Property Id As String
    Public Property Name As String

    Public Overrides Function ToString() As String
        Return Name
    End Function
End Class
