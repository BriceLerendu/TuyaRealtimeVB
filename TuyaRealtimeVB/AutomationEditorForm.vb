Imports System.Drawing
Imports System.Windows.Forms
Imports Newtonsoft.Json.Linq

''' <summary>
''' Formulaire d'édition/création d'automatisations Tuya
''' Permet de définir les déclencheurs, actions, et préconditions
''' </summary>
Public Class AutomationEditorForm
    Inherits Form

#Region "Constantes de couleurs"
    Private Shared ReadOnly DarkBg As Color = Color.FromArgb(45, 45, 48)
    Private Shared ReadOnly LightBg As Color = Color.FromArgb(242, 242, 247)
    Private Shared ReadOnly SecondaryBg As Color = Color.FromArgb(55, 55, 58)
    Private Shared ReadOnly ActiveBlue As Color = Color.FromArgb(0, 122, 255)
    Private Shared ReadOnly CardBg As Color = Color.White
    Private Shared ReadOnly TextColor As Color = Color.White
    Private Shared ReadOnly BorderColor As Color = Color.FromArgb(70, 70, 73)
#End Region

#Region "Champs privés"
    Private _apiClient As TuyaApiClient
    Private _homeId As String
    Private _automationId As String ' Nothing si création, sinon ID pour modification
    Private _existingData As JToken ' Données existantes si modification

    ' Contrôles principaux
    Private _txtName As TextBox
    Private _cboMatchType As ComboBox
    Private _tabControl As TabControl
    Private _btnSave As Button
    Private _btnCancel As Button

    ' Onglet Déclencheurs
    Private _conditionsPanel As FlowLayoutPanel
    Private _conditionsList As New List(Of ConditionControl)

    ' Onglet Actions
    Private _actionsPanel As FlowLayoutPanel
    Private _actionsList As New List(Of ActionControl)

    ' Onglet Plages horaires
    Private _preconditionsPanel As Panel
    Private _chkTimeRestriction As CheckBox
    Private _timeStartPicker As DateTimePicker
    Private _timeEndPicker As DateTimePicker
    Private _chkMonday As CheckBox
    Private _chkTuesday As CheckBox
    Private _chkWednesday As CheckBox
    Private _chkThursday As CheckBox
    Private _chkFriday As CheckBox
    Private _chkSaturday As CheckBox
    Private _chkSunday As CheckBox

    ' Panneau d'explication du mode AND/OR
    Private _lblMatchTypeExplanation As Label

    ' Données
    Private _devices As JArray = New JArray()
    Public Property AutomationResult As JObject = Nothing
#End Region

#Region "Initialisation"
    ''' <summary>
    ''' Constructeur pour créer une nouvelle automatisation
    ''' </summary>
    Public Sub New(apiClient As TuyaApiClient, homeId As String)
        _apiClient = apiClient
        _homeId = homeId
        _automationId = Nothing
        _existingData = Nothing
        InitializeComponent()
        Text = "🆕 Créer une automatisation"
    End Sub

    ''' <summary>
    ''' Constructeur pour modifier une automatisation existante
    ''' </summary>
    Public Sub New(apiClient As TuyaApiClient, homeId As String, automationId As String, existingData As JToken)
        _apiClient = apiClient
        _homeId = homeId
        _automationId = automationId
        _existingData = existingData
        InitializeComponent()
        Text = "✏️ Modifier l'automatisation"
    End Sub

    Private Sub InitializeComponent()
        ' Configuration du formulaire
        Size = New Size(1200, 800)
        StartPosition = FormStartPosition.CenterParent
        BackColor = DarkBg
        ForeColor = TextColor
        FormBorderStyle = FormBorderStyle.Sizable
        MinimumSize = New Size(1000, 700)

        ' Panel principal avec scroll
        Dim mainPanel As New Panel With {
            .Dock = DockStyle.Fill,
            .AutoScroll = True,
            .BackColor = DarkBg,
            .Padding = New Padding(20)
        }

        ' === SECTION GÉNÉRALE ===
        Dim lblTitle = New Label With {
            .Text = "Nom de l'automatisation",
            .Location = New Point(20, 20),
            .Size = New Size(300, 25),
            .ForeColor = TextColor,
            .Font = New Font("Segoe UI", 11, FontStyle.Bold)
        }

        _txtName = New TextBox With {
            .Location = New Point(20, 50),
            .Size = New Size(500, 30),
            .Font = New Font("Segoe UI", 11),
            .BackColor = SecondaryBg,
            .ForeColor = TextColor,
            .BorderStyle = BorderStyle.FixedSingle
        }

        ' === TABCONTROL POUR LES SECTIONS ===
        _tabControl = New TabControl With {
            .Location = New Point(20, 100),
            .Size = New Size(1140, 550),
            .Font = New Font("Segoe UI", 10)
        }

        ' Onglet Déclencheurs - NOUVELLE STRUCTURE AVEC LAYOUT AUTOMATIQUE
        Dim tabConditions = New TabPage("⚡ Déclencheurs (IF)") With {
            .BackColor = DarkBg
        }

        ' Panel conteneur principal (non scrollable)
        Dim mainConditionsContainer = New Panel With {
            .Dock = DockStyle.Fill,
            .BackColor = DarkBg,
            .Padding = New Padding(10)
        }

        ' Panneau de configuration du mode AND/OR (fixe en haut)
        Dim pnlMatchTypeConfig = New Panel With {
            .Dock = DockStyle.Top,
            .Height = 75,
            .BackColor = Color.FromArgb(60, 60, 63),
            .BorderStyle = BorderStyle.FixedSingle,
            .Padding = New Padding(10)
        }

        Dim lblMatchType = New Label With {
            .Text = "Mode de déclenchement :",
            .Location = New Point(10, 10),
            .Size = New Size(180, 25),
            .ForeColor = TextColor,
            .Font = New Font("Segoe UI", 10, FontStyle.Bold)
        }

        _cboMatchType = New ComboBox With {
            .Location = New Point(200, 8),
            .Size = New Size(290, 30),
            .Font = New Font("Segoe UI", 10),
            .BackColor = SecondaryBg,
            .ForeColor = TextColor,
            .DropDownStyle = ComboBoxStyle.DropDownList
        }
        _cboMatchType.Items.AddRange({"Toutes les conditions (AND)", "Au moins une condition (OR)"})
        _cboMatchType.SelectedIndex = 0
        AddHandler _cboMatchType.SelectedIndexChanged, AddressOf MatchType_Changed

        ' Explication du mode AND/OR
        _lblMatchTypeExplanation = New Label With {
            .Location = New Point(10, 42),
            .Size = New Size(1070, 25),
            .ForeColor = Color.FromArgb(52, 199, 89),
            .Font = New Font("Segoe UI", 9, FontStyle.Italic),
            .AutoSize = False,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Text = "✓ Mode AND : Tous les déclencheurs doivent être vrais pour exécuter les actions."
        }

        pnlMatchTypeConfig.Controls.AddRange({lblMatchType, _cboMatchType, _lblMatchTypeExplanation})

        ' Bouton Ajouter (fixe sous le panneau config)
        Dim pnlAddButton = New Panel With {
            .Dock = DockStyle.Top,
            .Height = 50,
            .BackColor = DarkBg,
            .Padding = New Padding(5)
        }

        Dim btnAddCondition = New Button With {
            .Text = "➕ Ajouter un déclencheur",
            .Location = New Point(5, 8),
            .Size = New Size(200, 35),
            .BackColor = ActiveBlue,
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 10, FontStyle.Bold)
        }
        AddHandler btnAddCondition.Click, AddressOf BtnAddCondition_Click
        pnlAddButton.Controls.Add(btnAddCondition)

        ' Panel scrollable pour les déclencheurs - UTILISE FLOWLAYOUTPANEL
        _conditionsPanel = New FlowLayoutPanel With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.TopDown,
            .WrapContents = False,
            .AutoScroll = True,
            .BackColor = DarkBg,
            .Padding = New Padding(5)
        }

        ' Ordre d'ajout important pour Dock
        mainConditionsContainer.Controls.Add(_conditionsPanel)  ' Fill - en dernier
        mainConditionsContainer.Controls.Add(pnlAddButton)      ' Top
        mainConditionsContainer.Controls.Add(pnlMatchTypeConfig) ' Top - en premier

        tabConditions.Controls.Add(mainConditionsContainer)

        ' Onglet Actions - MÊME STRUCTURE QUE DÉCLENCHEURS
        Dim tabActions = New TabPage("🎯 Actions (THEN)") With {
            .BackColor = DarkBg
        }

        ' Panel conteneur principal
        Dim mainActionsContainer = New Panel With {
            .Dock = DockStyle.Fill,
            .BackColor = DarkBg,
            .Padding = New Padding(10)
        }

        ' Bouton Ajouter (en haut)
        Dim pnlAddAction = New Panel With {
            .Dock = DockStyle.Top,
            .Height = 50,
            .BackColor = DarkBg,
            .Padding = New Padding(5)
        }

        Dim btnAddAction = New Button With {
            .Text = "➕ Ajouter une action",
            .Location = New Point(5, 8),
            .Size = New Size(200, 35),
            .BackColor = ActiveBlue,
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 10, FontStyle.Bold)
        }
        AddHandler btnAddAction.Click, AddressOf BtnAddAction_Click
        pnlAddAction.Controls.Add(btnAddAction)

        ' Panel scrollable pour les actions - UTILISE FLOWLAYOUTPANEL
        _actionsPanel = New FlowLayoutPanel With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.TopDown,
            .WrapContents = False,
            .AutoScroll = True,
            .BackColor = DarkBg,
            .Padding = New Padding(5)
        }

        mainActionsContainer.Controls.Add(_actionsPanel)
        mainActionsContainer.Controls.Add(pnlAddAction)

        tabActions.Controls.Add(mainActionsContainer)

        ' Onglet Plages horaires
        Dim tabPreconditions = New TabPage("🕒 Plages horaires") With {
            .BackColor = DarkBg
        }
        _preconditionsPanel = CreateTimeRestrictionPanel()
        tabPreconditions.Controls.Add(_preconditionsPanel)

        _tabControl.TabPages.Add(tabConditions)
        _tabControl.TabPages.Add(tabActions)
        _tabControl.TabPages.Add(tabPreconditions)

        ' === BOUTONS ===
        _btnSave = New Button With {
            .Text = "💾 Enregistrer",
            .Location = New Point(850, 670),
            .Size = New Size(140, 40),
            .BackColor = Color.FromArgb(52, 199, 89),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 11, FontStyle.Bold)
        }
        AddHandler _btnSave.Click, AddressOf BtnSave_Click

        _btnCancel = New Button With {
            .Text = "❌ Annuler",
            .Location = New Point(1010, 670),
            .Size = New Size(140, 40),
            .BackColor = Color.FromArgb(142, 142, 147),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 11, FontStyle.Bold)
        }
        AddHandler _btnCancel.Click, AddressOf BtnCancel_Click

        ' Ajout des contrôles
        mainPanel.Controls.AddRange({lblTitle, _txtName, _tabControl, _btnSave, _btnCancel})
        Controls.Add(mainPanel)
    End Sub

    Private Sub MatchType_Changed(sender As Object, e As EventArgs)
        Console.WriteLine($"[DEBUG] MatchType_Changed - SelectedIndex: {_cboMatchType.SelectedIndex}")

        If _cboMatchType.SelectedIndex = 0 Then
            ' Mode AND
            _lblMatchTypeExplanation.Text = "✓ Mode AND : Tous les déclencheurs doivent être vrais pour exécuter les actions."
            _lblMatchTypeExplanation.ForeColor = Color.FromArgb(52, 199, 89)
            Console.WriteLine($"[DEBUG] Affichage: Mode AND (vert)")
        Else
            ' Mode OR
            _lblMatchTypeExplanation.Text = "✓ Mode OR : Au moins un déclencheur doit être vrai pour exécuter les actions."
            _lblMatchTypeExplanation.ForeColor = Color.FromArgb(255, 149, 0)
            Console.WriteLine($"[DEBUG] Affichage: Mode OR (orange)")
        End If

        ' Mettre à jour les connecteurs visuels entre déclencheurs
        UpdateConditionConnectors()
    End Sub

    Private Function CreateTimeRestrictionPanel() As Panel
        Dim panel = New Panel With {
            .Dock = DockStyle.Fill,
            .AutoScroll = True,
            .BackColor = DarkBg,
            .Padding = New Padding(20)
        }

        _chkTimeRestriction = New CheckBox With {
            .Text = "Activer la restriction horaire",
            .Location = New Point(20, 20),
            .Size = New Size(300, 30),
            .ForeColor = TextColor,
            .Font = New Font("Segoe UI", 11, FontStyle.Bold)
        }
        AddHandler _chkTimeRestriction.CheckedChanged, AddressOf ChkTimeRestriction_CheckedChanged

        Dim lblStart = New Label With {
            .Text = "Heure de début:",
            .Location = New Point(20, 60),
            .Size = New Size(120, 25),
            .ForeColor = TextColor
        }

        _timeStartPicker = New DateTimePicker With {
            .Location = New Point(150, 60),
            .Size = New Size(100, 30),
            .Format = DateTimePickerFormat.Time,
            .ShowUpDown = True,
            .Enabled = False
        }

        Dim lblEnd = New Label With {
            .Text = "Heure de fin:",
            .Location = New Point(280, 60),
            .Size = New Size(120, 25),
            .ForeColor = TextColor
        }

        _timeEndPicker = New DateTimePicker With {
            .Location = New Point(410, 60),
            .Size = New Size(100, 30),
            .Format = DateTimePickerFormat.Time,
            .ShowUpDown = True,
            .Enabled = False
        }

        Dim lblDays = New Label With {
            .Text = "Jours de la semaine:",
            .Location = New Point(20, 110),
            .Size = New Size(200, 25),
            .ForeColor = TextColor,
            .Font = New Font("Segoe UI", 10, FontStyle.Bold)
        }

        _chkMonday = CreateDayCheckBox("Lun", 20, 140)
        _chkTuesday = CreateDayCheckBox("Mar", 110, 140)
        _chkWednesday = CreateDayCheckBox("Mer", 200, 140)
        _chkThursday = CreateDayCheckBox("Jeu", 290, 140)
        _chkFriday = CreateDayCheckBox("Ven", 380, 140)
        _chkSaturday = CreateDayCheckBox("Sam", 470, 140)
        _chkSunday = CreateDayCheckBox("Dim", 560, 140)

        panel.Controls.AddRange({_chkTimeRestriction, lblStart, _timeStartPicker, lblEnd, _timeEndPicker,
                                 lblDays, _chkMonday, _chkTuesday, _chkWednesday, _chkThursday,
                                 _chkFriday, _chkSaturday, _chkSunday})
        Return panel
    End Function

    Private Function CreateDayCheckBox(text As String, x As Integer, y As Integer) As CheckBox
        ' CheckBox STANDARD simple - texte blanc visible sur fond sombre
        Dim chk = New CheckBox With {
            .Text = text,
            .Location = New Point(x, y),
            .Size = New Size(90, 25),
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 11, FontStyle.Bold),
            .Enabled = False,
            .AutoSize = False,
            .BackColor = Color.Transparent
        }
        Return chk
    End Function
#End Region

#Region "Chargement initial"
    Protected Overrides Async Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        Await LoadDevicesAsync()
        If _existingData IsNot Nothing Then
            LoadExistingAutomation()
        End If
    End Sub

    Private Async Function LoadDevicesAsync() As Task
        Try
            _devices = Await _apiClient.GetDevicesByHomeAsync(_homeId)
        Catch ex As Exception
            MessageBox.Show($"Erreur lors du chargement des appareils: {ex.Message}", "Erreur",
                          MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Function

    Private Sub LoadExistingAutomation()
        ' Charger le nom
        _txtName.Text = GetJsonString(_existingData, "name")

        ' Charger match_type
        Dim matchType = GetJsonInt(_existingData, "match_type")

        ' Debug: Afficher la valeur reçue
        Console.WriteLine($"[DEBUG] LoadExistingAutomation - match_type reçu: {matchType}")

        ' Selon la doc Tuya:
        ' match_type = 1 : "any condition is met" = OR (au moins une)
        ' match_type = 2 : "all conditions are met" = AND (toutes)
        ' ComboBox: Index 0 = "Toutes les conditions (AND)", Index 1 = "Au moins une condition (OR)"
        If matchType = 1 Then
            _cboMatchType.SelectedIndex = 1  ' OR (any condition)
            Console.WriteLine($"[DEBUG] match_type=1 → Mode OR sélectionné (au moins une condition)")
        ElseIf matchType = 2 Then
            _cboMatchType.SelectedIndex = 0  ' AND (all conditions)
            Console.WriteLine($"[DEBUG] match_type=2 → Mode AND sélectionné (toutes les conditions)")
        Else
            ' Par défaut AND si la valeur est étrange
            _cboMatchType.SelectedIndex = 0
            Console.WriteLine($"[DEBUG] Mode par défaut: AND (valeur inattendue: {matchType})")
        End If

        ' Charger les conditions
        Dim conditions = _existingData("conditions")
        If conditions IsNot Nothing AndAlso TypeOf conditions Is JArray Then
            For Each condition As JToken In CType(conditions, JArray)
                AddConditionControl(condition)
            Next
        End If

        ' Charger les actions
        Dim actions = _existingData("actions")
        If actions IsNot Nothing AndAlso TypeOf actions Is JArray Then
            For Each action As JToken In CType(actions, JArray)
                AddActionControl(action)
            Next
        End If

        ' Charger les préconditions (plages horaires)
        Dim preconditions = _existingData("preconditions")
        If preconditions IsNot Nothing AndAlso TypeOf preconditions Is JArray Then
            Dim preArray = CType(preconditions, JArray)
            If preArray.Count > 0 Then
                Dim timeCheck = preArray(0)
                If GetJsonString(timeCheck, "cond_type") = "timeCheck" Then
                    _chkTimeRestriction.Checked = True
                    Dim display = timeCheck("display")
                    If display IsNot Nothing Then
                        ' Charger les heures
                        Dim startTime = GetJsonString(display, "start")
                        Dim endTime = GetJsonString(display, "end")
                        If Not String.IsNullOrEmpty(startTime) Then
                            _timeStartPicker.Value = DateTime.Parse(startTime)
                        End If
                        If Not String.IsNullOrEmpty(endTime) Then
                            _timeEndPicker.Value = DateTime.Parse(endTime)
                        End If

                        ' Charger les jours (loops = "1111111" pour Lun-Dim)
                        Dim loops = GetJsonString(display, "loops")
                        If Not String.IsNullOrEmpty(loops) AndAlso loops.Length = 7 Then
                            _chkMonday.Checked = loops(0) = "1"c
                            _chkTuesday.Checked = loops(1) = "1"c
                            _chkWednesday.Checked = loops(2) = "1"c
                            _chkThursday.Checked = loops(3) = "1"c
                            _chkFriday.Checked = loops(4) = "1"c
                            _chkSaturday.Checked = loops(5) = "1"c
                            _chkSunday.Checked = loops(6) = "1"c
                        End If
                    End If
                End If
            End If
        End If
    End Sub
#End Region

#Region "Gestion des déclencheurs"
    Private Sub BtnAddCondition_Click(sender As Object, e As EventArgs)
        AddConditionControl(Nothing)
    End Sub

    Private Sub AddConditionControl(existingCondition As JToken)
        ' Ajouter un connecteur ET/OU avant le nouveau déclencheur (sauf pour le premier)
        If _conditionsList.Count > 0 Then
            Dim connectorText = If(_cboMatchType.SelectedIndex = 0, "ET", "OU")
            Dim connectorColor = If(_cboMatchType.SelectedIndex = 0,
                                    Color.FromArgb(52, 199, 89),
                                    Color.FromArgb(255, 149, 0))

            Dim connector = New Label With {
                .Text = $"═══════ {connectorText} ═══════",
                .AutoSize = False,
                .Width = 1080,
                .Height = 30,
                .TextAlign = ContentAlignment.MiddleCenter,
                .ForeColor = connectorColor,
                .BackColor = DarkBg,
                .Font = New Font("Segoe UI", 11, FontStyle.Bold),
                .Tag = "connector",
                .Margin = New Padding(5, 5, 5, 5)
            }
            _conditionsPanel.Controls.Add(connector)
        End If

        ' Créer le contrôle de condition
        Dim conditionControl = New ConditionControl(_devices, existingCondition) With {
            .Width = 1080,
            .Margin = New Padding(5)
        }
        conditionControl.SetNumber(_conditionsList.Count + 1)
        AddHandler conditionControl.RemoveRequested, AddressOf RemoveCondition
        _conditionsList.Add(conditionControl)
        _conditionsPanel.Controls.Add(conditionControl)
    End Sub

    Private Sub RemoveCondition(conditionControl As ConditionControl)
        ' Trouver l'index du contrôle à supprimer
        Dim index = _conditionsList.IndexOf(conditionControl)

        ' Supprimer le contrôle et le connecteur qui le précède (s'il y en a un)
        _conditionsList.Remove(conditionControl)

        Dim controlIndex = _conditionsPanel.Controls.IndexOf(conditionControl)
        _conditionsPanel.Controls.Remove(conditionControl)
        conditionControl.Dispose()

        ' Supprimer le connecteur qui précède (si ce n'est pas le premier élément)
        If controlIndex > 0 AndAlso controlIndex - 1 < _conditionsPanel.Controls.Count Then
            Dim prevControl = _conditionsPanel.Controls(controlIndex - 1)
            If TypeOf prevControl Is Label AndAlso prevControl.Tag?.ToString() = "connector" Then
                _conditionsPanel.Controls.Remove(prevControl)
                prevControl.Dispose()
            End If
        End If

        ' Renuméroter tous les déclencheurs
        For i = 0 To _conditionsList.Count - 1
            _conditionsList(i).SetNumber(i + 1)
        Next
    End Sub

    Private Sub UpdateConditionConnectors()
        ' Mettre à jour le texte et la couleur de tous les connecteurs existants
        Dim connectorText = If(_cboMatchType.SelectedIndex = 0, "ET", "OU")
        Dim connectorColor = If(_cboMatchType.SelectedIndex = 0,
                                Color.FromArgb(52, 199, 89),
                                Color.FromArgb(255, 149, 0))

        For Each ctrl As Control In _conditionsPanel.Controls
            If TypeOf ctrl Is Label AndAlso ctrl.Tag?.ToString() = "connector" Then
                Dim lbl = CType(ctrl, Label)
                lbl.Text = $"═══════ {connectorText} ═══════"
                lbl.ForeColor = connectorColor
            End If
        Next
    End Sub
#End Region

#Region "Gestion des actions"
    Private Sub BtnAddAction_Click(sender As Object, e As EventArgs)
        AddActionControl(Nothing)
    End Sub

    Private Sub AddActionControl(existingAction As JToken)
        ' Créer le contrôle d'action
        Dim actionControl = New ActionControl(_devices, existingAction) With {
            .Width = 1080,
            .Margin = New Padding(5)
        }
        actionControl.SetNumber(_actionsList.Count + 1)
        AddHandler actionControl.RemoveRequested, AddressOf RemoveAction
        _actionsList.Add(actionControl)
        _actionsPanel.Controls.Add(actionControl)
    End Sub

    Private Sub RemoveAction(actionControl As ActionControl)
        _actionsList.Remove(actionControl)
        _actionsPanel.Controls.Remove(actionControl)
        actionControl.Dispose()

        ' Renuméroter toutes les actions
        For i = 0 To _actionsList.Count - 1
            _actionsList(i).SetNumber(i + 1)
        Next
    End Sub
#End Region

#Region "Gestion des plages horaires"
    Private Sub ChkTimeRestriction_CheckedChanged(sender As Object, e As EventArgs)
        Dim enabled = _chkTimeRestriction.Checked
        _timeStartPicker.Enabled = enabled
        _timeEndPicker.Enabled = enabled

        ' Activer/désactiver les jours (simple et fonctionnel)
        _chkMonday.Enabled = enabled
        _chkTuesday.Enabled = enabled
        _chkWednesday.Enabled = enabled
        _chkThursday.Enabled = enabled
        _chkFriday.Enabled = enabled
        _chkSaturday.Enabled = enabled
        _chkSunday.Enabled = enabled
    End Sub
#End Region

#Region "Sauvegarde"
    Private Async Sub BtnSave_Click(sender As Object, e As EventArgs)
        Try
            ' Validation
            If String.IsNullOrWhiteSpace(_txtName.Text) Then
                MessageBox.Show("Veuillez saisir un nom pour l'automatisation.", "Validation",
                              MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            If _actionsList.Count = 0 Then
                MessageBox.Show("Veuillez ajouter au moins une action.", "Validation",
                              MessageBoxButtons.OK, MessageBoxIcon.Warning)
                _tabControl.SelectedIndex = 1 ' Onglet Actions
                Return
            End If

            ' Construction du JSON
            Dim automationData As New JObject()
            automationData("name") = _txtName.Text

            ' Match type selon doc Tuya:
            ' 1 = "any condition is met" (OR - au moins une)
            ' 2 = "all conditions are met" (AND - toutes)
            ' ComboBox: Index 0 = AND, Index 1 = OR
            Dim matchTypeValue = If(_cboMatchType.SelectedIndex = 1, 1, 2)  ' Index 1 (OR) = 1, Index 0 (AND) = 2
            automationData("match_type") = matchTypeValue
            Console.WriteLine($"[DEBUG] Sauvegarde - ComboBox Index: {_cboMatchType.SelectedIndex}, match_type envoyé: {matchTypeValue}")

            ' Conditions
            Dim conditionsArray As New JArray()
            For Each condControl In _conditionsList
                Dim condition = condControl.GetConditionData()
                If condition IsNot Nothing Then
                    conditionsArray.Add(condition)
                End If
            Next
            automationData("conditions") = conditionsArray

            ' Actions
            Dim actionsArray As New JArray()
            For Each actControl In _actionsList
                Dim action = actControl.GetActionData()
                If action IsNot Nothing Then
                    actionsArray.Add(action)
                End If
            Next
            automationData("actions") = actionsArray

            ' Préconditions (plages horaires)
            If _chkTimeRestriction.Checked Then
                Dim preconditionsArray As New JArray()
                Dim timeCheck As New JObject()
                timeCheck("cond_type") = "timeCheck"

                Dim display As New JObject()
                display("start") = _timeStartPicker.Value.ToString("HH:mm")
                display("end") = _timeEndPicker.Value.ToString("HH:mm")

                ' Construire loops (1111111 = tous les jours)
                Dim loops = ""
                loops &= If(_chkMonday.Checked, "1", "0")
                loops &= If(_chkTuesday.Checked, "1", "0")
                loops &= If(_chkWednesday.Checked, "1", "0")
                loops &= If(_chkThursday.Checked, "1", "0")
                loops &= If(_chkFriday.Checked, "1", "0")
                loops &= If(_chkSaturday.Checked, "1", "0")
                loops &= If(_chkSunday.Checked, "1", "0")
                display("loops") = loops
                display("timezone_id") = TimeZoneInfo.Local.Id

                timeCheck("display") = display
                preconditionsArray.Add(timeCheck)
                automationData("preconditions") = preconditionsArray
            End If

            ' Désactiver les boutons pendant l'enregistrement
            _btnSave.Enabled = False
            _btnCancel.Enabled = False

            ' Appel API
            Dim success = False
            If String.IsNullOrEmpty(_automationId) Then
                ' Création
                Dim newId = Await _apiClient.CreateAutomationAsync(_homeId, automationData)
                success = Not String.IsNullOrEmpty(newId)
                If success Then
                    MessageBox.Show("Automatisation créée avec succès!", "Succès",
                                  MessageBoxButtons.OK, MessageBoxIcon.Information)
                End If
            Else
                ' Modification
                success = Await _apiClient.UpdateAutomationAsync(_homeId, _automationId, automationData)
                If success Then
                    MessageBox.Show("Automatisation mise à jour avec succès!", "Succès",
                                  MessageBoxButtons.OK, MessageBoxIcon.Information)
                End If
            End If

            If success Then
                AutomationResult = automationData
                DialogResult = DialogResult.OK
                Close()
            Else
                MessageBox.Show("L'enregistrement a échoué. Vérifiez les logs.", "Erreur",
                              MessageBoxButtons.OK, MessageBoxIcon.Error)
                _btnSave.Enabled = True
                _btnCancel.Enabled = True
            End If

        Catch ex As Exception
            MessageBox.Show($"Erreur lors de l'enregistrement: {ex.Message}", "Erreur",
                          MessageBoxButtons.OK, MessageBoxIcon.Error)
            _btnSave.Enabled = True
            _btnCancel.Enabled = True
        End Try
    End Sub

    Private Sub BtnCancel_Click(sender As Object, e As EventArgs)
        DialogResult = DialogResult.Cancel
        Close()
    End Sub
#End Region

#Region "Fonctions utilitaires JSON"
    Private Function GetJsonString(token As JToken, key As String) As String
        Try
            If token Is Nothing Then Return ""
            Dim value = token(key)
            If value Is Nothing Then Return ""
            Return value.ToString()
        Catch
            Return ""
        End Try
    End Function

    Private Function GetJsonInt(token As JToken, key As String) As Integer
        Try
            If token Is Nothing Then Return 0
            Dim value = token(key)
            If value Is Nothing Then Return 0
            Return CInt(value)
        Catch
            Return 0
        End Try
    End Function
#End Region
End Class

#Region "Contrôle personnalisé : Condition"
''' <summary>
''' Contrôle pour définir une condition de déclenchement
''' </summary>
Public Class ConditionControl
    Inherits Panel

    Private _devices As JArray
    Private _cboDevice As ComboBox
    Private _cboProperty As ComboBox
    Private _cboOperator As ComboBox

    ' Contrôles de valeur (un seul visible à la fois)
    Private _cboValueEnum As ComboBox
    Private _numValueInt As NumericUpDown
    Private _txtValueJson As TextBox
    Private _valueControlPanel As Panel

    ' Informations sur l'échelle (pour température, humidité, etc.)
    Private _currentScale As Integer = 0
    Private _currentUnit As String = ""

    Private _btnRemove As Button
    Private _lblTitle As Label
    Private _lblSummary As Label

    Public Event RemoveRequested(sender As ConditionControl)

    Public Sub New(devices As JArray, existingCondition As JToken)
        _devices = devices
        InitializeControl(existingCondition)
    End Sub

    Public Sub SetNumber(number As Integer)
        _lblTitle.Text = $"⚡ Déclencheur #{number}"
    End Sub

    Private Sub InitializeControl(existingCondition As JToken)
        Size = New Size(1000, 120)
        BackColor = Color.FromArgb(55, 55, 58)
        BorderStyle = BorderStyle.FixedSingle
        Padding = New Padding(5)

        ' Titre du déclencheur
        _lblTitle = New Label With {
            .Text = "⚡ Déclencheur",
            .Location = New Point(10, 5),
            .Size = New Size(200, 20),
            .ForeColor = Color.FromArgb(0, 122, 255),
            .Font = New Font("Segoe UI", 9, FontStyle.Bold)
        }

        ' Ligne de séparation visuelle
        Dim separator = New Panel With {
            .Location = New Point(10, 27),
            .Size = New Size(980, 1),
            .BackColor = Color.FromArgb(100, 100, 103)
        }

        ' Appareil
        Dim lblDevice = New Label With {
            .Text = "Appareil:",
            .Location = New Point(10, 35),
            .Size = New Size(80, 25),
            .ForeColor = Color.White
        }

        _cboDevice = New ComboBox With {
            .Location = New Point(100, 32),
            .Size = New Size(250, 30),
            .DropDownStyle = ComboBoxStyle.DropDownList,
            .BackColor = Color.FromArgb(70, 70, 73),
            .ForeColor = Color.White
        }
        For Each device As JToken In _devices
            Dim deviceName = GetJsonString(device, "name")
            Dim deviceId = GetJsonString(device, "id")
            _cboDevice.Items.Add(New DeviceItem(deviceId, deviceName))
        Next

        ' Propriété
        Dim lblProperty = New Label With {
            .Text = "Propriété:",
            .Location = New Point(370, 35),
            .Size = New Size(80, 25),
            .ForeColor = Color.White
        }

        _cboProperty = New ComboBox With {
            .Location = New Point(460, 32),
            .Size = New Size(150, 30),
            .DropDownStyle = ComboBoxStyle.DropDownList,
            .BackColor = Color.FromArgb(70, 70, 73),
            .ForeColor = Color.White
        }
        ' Handlers pour charger dynamiquement depuis les specs
        AddHandler _cboDevice.SelectedIndexChanged, AddressOf OnDeviceChanged
        AddHandler _cboProperty.SelectedIndexChanged, AddressOf OnPropertyChanged

        ' Opérateur
        _cboOperator = New ComboBox With {
            .Location = New Point(630, 32),
            .Size = New Size(80, 30),
            .DropDownStyle = ComboBoxStyle.DropDownList,
            .BackColor = Color.FromArgb(70, 70, 73),
            .ForeColor = Color.White
        }
        _cboOperator.Items.AddRange({"==", "!=", ">", "<", ">=", "<="})
        _cboOperator.SelectedIndex = 0
        AddHandler _cboOperator.SelectedIndexChanged, AddressOf UpdateSummary

        ' Valeur - Panel conteneur pour switcher entre différents contrôles
        Dim lblValue = New Label With {
            .Text = "Valeur:",
            .Location = New Point(730, 15),
            .Size = New Size(50, 15),
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 8)
        }

        _valueControlPanel = New Panel With {
            .Location = New Point(730, 32),
            .Size = New Size(150, 30),
            .BackColor = Color.FromArgb(70, 70, 73)
        }

        ' Créer les 3 types de contrôles de valeur (tous cachés au départ)
        CreateValueControls()

        ' Bouton supprimer
        _btnRemove = New Button With {
            .Text = "🗑️",
            .Location = New Point(900, 32),
            .Size = New Size(40, 30),
            .BackColor = Color.FromArgb(255, 59, 48),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat
        }
        AddHandler _btnRemove.Click, Sub() RaiseEvent RemoveRequested(Me)

        ' Label résumé
        _lblSummary = New Label With {
            .Text = "Configuration: En attente...",
            .Location = New Point(10, 75),
            .Size = New Size(880, 35),
            .ForeColor = Color.FromArgb(200, 200, 200),
            .Font = New Font("Segoe UI", 9, FontStyle.Italic),
            .AutoSize = False
        }

        Controls.AddRange({_lblTitle, separator, lblDevice, _cboDevice, lblProperty, _cboProperty, _cboOperator, lblValue, _valueControlPanel, _btnRemove, _lblSummary})

        ' Charger les données existantes
        If existingCondition IsNot Nothing Then
            LoadExistingData(existingCondition)
        Else
            UpdateSummary(Nothing, Nothing)
        End If
    End Sub

    Private Sub CreateValueControls()
        ' ComboBox pour Enum/Boolean
        _cboValueEnum = New ComboBox With {
            .Location = New Point(0, 0),
            .Size = New Size(150, 30),
            .DropDownStyle = ComboBoxStyle.DropDownList,
            .BackColor = Color.FromArgb(70, 70, 73),
            .ForeColor = Color.White,
            .Visible = False
        }
        AddHandler _cboValueEnum.SelectedIndexChanged, AddressOf UpdateSummary

        ' NumericUpDown pour Integer
        _numValueInt = New NumericUpDown With {
            .Location = New Point(0, 0),
            .Size = New Size(150, 30),
            .BackColor = Color.FromArgb(70, 70, 73),
            .ForeColor = Color.White,
            .Visible = False
        }
        AddHandler _numValueInt.ValueChanged, AddressOf UpdateSummary

        ' TextBox pour Json et autres
        _txtValueJson = New TextBox With {
            .Location = New Point(0, 0),
            .Size = New Size(150, 30),
            .BackColor = Color.FromArgb(70, 70, 73),
            .ForeColor = Color.White,
            .Text = "true",
            .Visible = True
        }
        AddHandler _txtValueJson.TextChanged, AddressOf UpdateSummary

        _valueControlPanel.Controls.AddRange({_cboValueEnum, _numValueInt, _txtValueJson})
    End Sub

    Private Sub OnDeviceChanged(sender As Object, e As EventArgs)
        If _cboDevice.SelectedItem Is Nothing Then Return

        Dim deviceItem = CType(_cboDevice.SelectedItem, DeviceItem)

        ' Récupérer les spécifications depuis le cache (déjà dans l'objet device)
        Dim deviceSpecs As JObject = Nothing

        For Each device As JToken In _devices
            If GetJsonString(device, "id") = deviceItem.Id Then
                ' Les specs sont déjà attachées par GetDevicesByHomeAsync
                Dim cachedSpecs = device("_cached_specifications")
                If cachedSpecs IsNot Nothing AndAlso TypeOf cachedSpecs Is JObject Then
                    deviceSpecs = CType(cachedSpecs, JObject)
                End If
                Exit For
            End If
        Next

        ' Peupler la ComboBox des propriétés avec les "status" disponibles (pour les déclencheurs, on utilise status plutôt que functions)
        _cboProperty.Items.Clear()

        If deviceSpecs IsNot Nothing Then
            ' Pour les déclencheurs, on utilise à la fois "functions" et "status"
            Dim allProperties As New HashSet(Of String)

            ' Ajouter les "status"
            If deviceSpecs("status") IsNot Nothing Then
                Dim statusArray = CType(deviceSpecs("status"), JArray)
                For Each stat As JToken In statusArray
                    Dim code = GetJsonString(stat, "code")
                    If Not String.IsNullOrEmpty(code) AndAlso Not allProperties.Contains(code) Then
                        _cboProperty.Items.Add(New CommandItem(code, stat))
                        allProperties.Add(code)
                    End If
                Next
            End If

            ' Ajouter aussi les "functions" (car certains devices utilisent les deux)
            If deviceSpecs("functions") IsNot Nothing Then
                Dim functions = CType(deviceSpecs("functions"), JArray)
                For Each func As JToken In functions
                    Dim code = GetJsonString(func, "code")
                    If Not String.IsNullOrEmpty(code) AndAlso Not allProperties.Contains(code) Then
                        _cboProperty.Items.Add(New CommandItem(code, func))
                        allProperties.Add(code)
                    End If
                Next
            End If
        End If

        ' Fallback si pas de specs
        If _cboProperty.Items.Count = 0 Then
            _cboProperty.Items.AddRange({"switch_1", "switch", "temp_current", "humidity_value", "bright_value"})
        End If

        If _cboProperty.Items.Count > 0 Then
            _cboProperty.SelectedIndex = 0
        End If
    End Sub

    Private Sub OnPropertyChanged(sender As Object, e As EventArgs)
        If _cboProperty.SelectedItem Is Nothing Then
            UpdateSummary(Nothing, Nothing)
            Return
        End If

        ' Vérifier si c'est un CommandItem (avec specs) ou un string (fallback)
        If TypeOf _cboProperty.SelectedItem Is CommandItem Then
            Dim commandItem = CType(_cboProperty.SelectedItem, CommandItem)
            Dim spec = commandItem.Specification

            ' Obtenir le type et les valeurs
            Dim specType = GetJsonString(spec, "type")
            Dim valuesJson = GetJsonString(spec, "values")

            ' Configurer le contrôle approprié selon le type
            ConfigureValueControl(specType, valuesJson)
        Else
            ' Fallback : utiliser TextBox
            _cboValueEnum.Visible = False
            _numValueInt.Visible = False
            _txtValueJson.Visible = True
        End If

        UpdateSummary(Nothing, Nothing)
    End Sub

    Private Sub ConfigureValueControl(specType As String, valuesJson As String)
        ' Masquer tous les contrôles
        _cboValueEnum.Visible = False
        _numValueInt.Visible = False
        _txtValueJson.Visible = False

        Select Case specType
            Case "Boolean"
                _cboValueEnum.Items.Clear()
                _cboValueEnum.Items.AddRange({"true", "false"})
                _cboValueEnum.SelectedIndex = 0
                _cboValueEnum.Visible = True

            Case "Integer"
                Try
                    Dim values = JObject.Parse(valuesJson)

                    ' Extraire le scale (facteur de division) et unit
                    _currentScale = If(values("scale") IsNot Nothing, CInt(values("scale")), 0)
                    _currentUnit = If(values("unit") IsNot Nothing, values("unit").ToString(), "")

                    ' Calculer le diviseur basé sur le scale
                    Dim scaleFactor = CDec(Math.Pow(10, _currentScale))

                    ' Récupérer les valeurs brutes de l'API
                    Dim minRaw = CInt(values("min"))
                    Dim maxRaw = CInt(values("max"))
                    Dim stepRaw = If(values("step") IsNot Nothing, CInt(values("step")), 1)

                    ' Appliquer le scale pour l'affichage
                    _numValueInt.DecimalPlaces = _currentScale
                    _numValueInt.Minimum = CDec(minRaw / scaleFactor)
                    _numValueInt.Maximum = CDec(maxRaw / scaleFactor)
                    _numValueInt.Increment = CDec(stepRaw / scaleFactor)
                    _numValueInt.Value = CDec(minRaw / scaleFactor)
                    _numValueInt.Visible = True

                    Console.WriteLine($"[ConditionControl] Scale={_currentScale}, Unit={_currentUnit}, Affichage: {_numValueInt.Minimum}-{_numValueInt.Maximum}")
                Catch ex As Exception
                    Console.WriteLine($"[ConditionControl] Erreur parsing Integer: {ex.Message}")
                    _currentScale = 0
                    _currentUnit = ""
                    _txtValueJson.Visible = True
                End Try

            Case "Enum"
                Try
                    Dim values = JObject.Parse(valuesJson)
                    Dim range = CType(values("range"), JArray)

                    _cboValueEnum.Items.Clear()
                    For Each opt As JToken In range
                        _cboValueEnum.Items.Add(opt.ToString())
                    Next

                    If _cboValueEnum.Items.Count > 0 Then
                        _cboValueEnum.SelectedIndex = 0
                    End If

                    _cboValueEnum.Visible = True
                Catch
                    _txtValueJson.Visible = True
                End Try

            Case Else
                _txtValueJson.Visible = True
        End Select
    End Sub

    Private Sub UpdateSummary(sender As Object, e As EventArgs)
        Try
            If _cboDevice.SelectedItem Is Nothing Then
                _lblSummary.Text = "⚙️ Configuration: Sélectionnez un appareil pour commencer"
                _lblSummary.ForeColor = Color.FromArgb(255, 149, 0)
                Return
            End If

            Dim deviceName = _cboDevice.SelectedItem.ToString()
            Dim propertyName = If(_cboProperty.SelectedItem IsNot Nothing, _cboProperty.SelectedItem.ToString(), "???")
            Dim operatorText = If(_cboOperator.SelectedItem IsNot Nothing, _cboOperator.SelectedItem.ToString(), "==")

            ' Récupérer la valeur depuis le contrôle visible
            Dim valueText As String = ""
            If _cboValueEnum.Visible AndAlso _cboValueEnum.SelectedItem IsNot Nothing Then
                valueText = _cboValueEnum.SelectedItem.ToString()
            ElseIf _numValueInt.Visible Then
                ' Afficher avec l'unité si disponible
                valueText = _numValueInt.Value.ToString()
                If Not String.IsNullOrEmpty(_currentUnit) Then
                    valueText &= _currentUnit
                End If
            ElseIf _txtValueJson.Visible Then
                valueText = _txtValueJson.Text
            End If

            _lblSummary.Text = $"📋 Si [{deviceName}] → propriété '{propertyName}' {operatorText} {valueText}"
            _lblSummary.ForeColor = Color.FromArgb(52, 199, 89)
        Catch ex As Exception
            _lblSummary.Text = "⚙️ Configuration en cours..."
            _lblSummary.ForeColor = Color.FromArgb(200, 200, 200)
        End Try
    End Sub

    Private Sub LoadExistingData(condition As JToken)
        Dim entityId = GetJsonString(condition, "entity_id")
        For i = 0 To _cboDevice.Items.Count - 1
            Dim item = CType(_cboDevice.Items(i), DeviceItem)
            If item.Id = entityId Then
                _cboDevice.SelectedIndex = i
                Exit For
            End If
        Next

        Dim display = condition("display")
        If display IsNot Nothing Then
            Dim code = GetJsonString(display, "code")
            Dim operator_val = GetJsonString(display, "operator")
            Dim value = display("value")

            ' Propriété
            For i = 0 To _cboProperty.Items.Count - 1
                If _cboProperty.Items(i).ToString() = code Then
                    _cboProperty.SelectedIndex = i
                    Exit For
                End If
            Next
            If _cboProperty.SelectedIndex = -1 AndAlso Not String.IsNullOrEmpty(code) Then
                _cboProperty.Items.Add(code)
                _cboProperty.SelectedItem = code
            End If

            ' Opérateur
            For i = 0 To _cboOperator.Items.Count - 1
                If _cboOperator.Items(i).ToString() = operator_val Then
                    _cboOperator.SelectedIndex = i
                    Exit For
                End If
            Next

            ' Valeur - charger dans le contrôle approprié en appliquant le scale
            If value IsNot Nothing Then
                ' Attendre que OnPropertyChanged ait configuré le contrôle
                Application.DoEvents()

                ' Maintenant charger la valeur dans le contrôle visible
                If _numValueInt.Visible Then
                    ' Pour NumericUpDown, diviser par le scale pour l'affichage
                    Dim rawValue As Decimal = 0
                    If IsNumeric(value.ToString()) Then
                        rawValue = CDec(value)
                        Dim scaleFactor = CDec(Math.Pow(10, _currentScale))
                        Dim displayValue = rawValue / scaleFactor
                        _numValueInt.Value = Math.Max(_numValueInt.Minimum, Math.Min(_numValueInt.Maximum, CDec(displayValue)))
                        Console.WriteLine($"[ConditionControl] Chargement valeur: API={rawValue}, Scale={_currentScale}, Affichage={_numValueInt.Value}")
                    End If
                ElseIf _cboValueEnum.Visible Then
                    ' Pour ComboBox, chercher la valeur correspondante
                    Dim valueStr = value.ToString().ToLower()
                    For i = 0 To _cboValueEnum.Items.Count - 1
                        If _cboValueEnum.Items(i).ToString().ToLower() = valueStr Then
                            _cboValueEnum.SelectedIndex = i
                            Exit For
                        End If
                    Next
                Else
                    ' Pour TextBox, utiliser la valeur brute
                    _txtValueJson.Text = value.ToString()
                End If
            End If
        End If

        ' Mettre à jour le résumé après avoir chargé toutes les données
        UpdateSummary(Nothing, Nothing)
    End Sub

    Public Function GetConditionData() As JObject
        If _cboDevice.SelectedItem Is Nothing OrElse _cboProperty.SelectedItem Is Nothing Then
            Return Nothing
        End If

        Dim deviceItem = CType(_cboDevice.SelectedItem, DeviceItem)
        Dim condition As New JObject()
        condition("entity_id") = deviceItem.Id
        condition("entity_type") = 1 ' 1 = device

        Dim display As New JObject()
        display("code") = _cboProperty.SelectedItem.ToString()
        display("operator") = _cboOperator.SelectedItem.ToString()

        ' Récupérer la valeur depuis le contrôle visible
        Dim valueText As String = ""
        Dim numericValue As Decimal = 0

        If _cboValueEnum.Visible AndAlso _cboValueEnum.SelectedItem IsNot Nothing Then
            valueText = _cboValueEnum.SelectedItem.ToString()
        ElseIf _numValueInt.Visible Then
            ' Pour NumericUpDown, appliquer le scale inverse (multiplier par 10^scale)
            Dim displayValue = _numValueInt.Value
            Dim scaleFactor = CDec(Math.Pow(10, _currentScale))
            numericValue = CDec(displayValue * scaleFactor)
            valueText = numericValue.ToString()
            Console.WriteLine($"[ConditionControl] Valeur affichée: {displayValue}, Scale: {_currentScale}, Valeur envoyée: {numericValue}")
        ElseIf _txtValueJson.Visible Then
            valueText = _txtValueJson.Text.Trim()
        End If

        ' Parser la valeur (bool, number, ou string)
        If valueText.ToLower() = "true" Then
            display("value") = True
        ElseIf valueText.ToLower() = "false" Then
            display("value") = False
        ElseIf _numValueInt.Visible Then
            ' Utiliser la valeur numérique déjà mise à l'échelle
            display("value") = CInt(numericValue)
        ElseIf IsNumeric(valueText) Then
            display("value") = CDbl(valueText)
        Else
            display("value") = valueText
        End If

        condition("display") = display
        Return condition
    End Function

    Private Function GetJsonString(token As JToken, key As String) As String
        Try
            If token Is Nothing Then Return ""
            Dim value = token(key)
            If value Is Nothing Then Return ""
            Return value.ToString()
        Catch
            Return ""
        End Try
    End Function
End Class
#End Region

#Region "Contrôle personnalisé : Action"
''' <summary>
''' Contrôle pour définir une action à exécuter
''' </summary>
Public Class ActionControl
    Inherits Panel

    Private _devices As JArray
    Private _cboDevice As ComboBox
    Private _cboProperty As ComboBox

    ' Contrôles de valeur (un seul visible à la fois)
    Private _cboValueEnum As ComboBox
    Private _numValueInt As NumericUpDown
    Private _txtValueJson As TextBox
    Private _valueControlPanel As Panel

    ' Informations sur l'échelle (pour température, humidité, etc.)
    Private _currentScale As Integer = 0
    Private _currentUnit As String = ""

    Private _btnRemove As Button
    Private _lblTitle As Label
    Private _lblSummary As Label

    Public Event RemoveRequested(sender As ActionControl)

    Public Sub New(devices As JArray, existingAction As JToken)
        _devices = devices
        InitializeControl(existingAction)
    End Sub

    Public Sub SetNumber(number As Integer)
        _lblTitle.Text = $"🎯 Action #{number}"
    End Sub

    Private Sub InitializeControl(existingAction As JToken)
        Size = New Size(1000, 120)
        BackColor = Color.FromArgb(55, 55, 58)
        BorderStyle = BorderStyle.FixedSingle
        Padding = New Padding(5)

        ' Titre de l'action
        _lblTitle = New Label With {
            .Text = "🎯 Action",
            .Location = New Point(10, 5),
            .Size = New Size(200, 20),
            .ForeColor = Color.FromArgb(0, 122, 255),
            .Font = New Font("Segoe UI", 9, FontStyle.Bold)
        }

        ' Ligne de séparation visuelle
        Dim separator = New Panel With {
            .Location = New Point(10, 27),
            .Size = New Size(980, 1),
            .BackColor = Color.FromArgb(100, 100, 103)
        }

        ' Appareil
        Dim lblDevice = New Label With {
            .Text = "Appareil:",
            .Location = New Point(10, 35),
            .Size = New Size(80, 25),
            .ForeColor = Color.White
        }

        _cboDevice = New ComboBox With {
            .Location = New Point(100, 32),
            .Size = New Size(250, 30),
            .DropDownStyle = ComboBoxStyle.DropDownList,
            .BackColor = Color.FromArgb(70, 70, 73),
            .ForeColor = Color.White
        }
        For Each device As JToken In _devices
            Dim deviceName = GetJsonString(device, "name")
            Dim deviceId = GetJsonString(device, "id")
            _cboDevice.Items.Add(New DeviceItem(deviceId, deviceName))
        Next

        ' Commande
        Dim lblCommand = New Label With {
            .Text = "Commande:",
            .Location = New Point(370, 35),
            .Size = New Size(90, 25),
            .ForeColor = Color.White
        }

        _cboProperty = New ComboBox With {
            .Location = New Point(470, 32),
            .Size = New Size(150, 30),
            .DropDownStyle = ComboBoxStyle.DropDownList,
            .BackColor = Color.FromArgb(70, 70, 73),
            .ForeColor = Color.White
        }
        ' Handlers pour les changements
        AddHandler _cboDevice.SelectedIndexChanged, AddressOf OnDeviceChanged
        AddHandler _cboProperty.SelectedIndexChanged, AddressOf OnCommandChanged

        ' Valeur - Panel conteneur pour switcher entre différents contrôles
        Dim lblValue = New Label With {
            .Text = "Valeur:",
            .Location = New Point(640, 35),
            .Size = New Size(60, 25),
            .ForeColor = Color.White
        }

        _valueControlPanel = New Panel With {
            .Location = New Point(710, 32),
            .Size = New Size(170, 30),
            .BackColor = Color.FromArgb(70, 70, 73)
        }

        ' Créer les 3 types de contrôles de valeur (tous cachés au départ)
        CreateValueControls()

        ' Bouton supprimer
        _btnRemove = New Button With {
            .Text = "🗑️",
            .Location = New Point(900, 32),
            .Size = New Size(40, 30),
            .BackColor = Color.FromArgb(255, 59, 48),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat
        }
        AddHandler _btnRemove.Click, Sub() RaiseEvent RemoveRequested(Me)

        ' Label résumé
        _lblSummary = New Label With {
            .Text = "Configuration: En attente...",
            .Location = New Point(10, 75),
            .Size = New Size(880, 35),
            .ForeColor = Color.FromArgb(200, 200, 200),
            .Font = New Font("Segoe UI", 9, FontStyle.Italic),
            .AutoSize = False
        }

        Controls.AddRange({_lblTitle, separator, lblDevice, _cboDevice, lblCommand, _cboProperty, lblValue, _valueControlPanel, _btnRemove, _lblSummary})

        ' Charger les données existantes
        If existingAction IsNot Nothing Then
            LoadExistingData(existingAction)
        Else
            UpdateSummary(Nothing, Nothing)
        End If
    End Sub

    Private Sub CreateValueControls()
        ' ComboBox pour Enum/Boolean
        _cboValueEnum = New ComboBox With {
            .Location = New Point(0, 0),
            .Size = New Size(170, 30),
            .DropDownStyle = ComboBoxStyle.DropDownList,
            .BackColor = Color.FromArgb(70, 70, 73),
            .ForeColor = Color.White,
            .Visible = False
        }
        AddHandler _cboValueEnum.SelectedIndexChanged, AddressOf UpdateSummary

        ' NumericUpDown pour Integer
        _numValueInt = New NumericUpDown With {
            .Location = New Point(0, 0),
            .Size = New Size(170, 30),
            .BackColor = Color.FromArgb(70, 70, 73),
            .ForeColor = Color.White,
            .Visible = False
        }
        AddHandler _numValueInt.ValueChanged, AddressOf UpdateSummary

        ' TextBox pour Json et autres
        _txtValueJson = New TextBox With {
            .Location = New Point(0, 0),
            .Size = New Size(170, 30),
            .BackColor = Color.FromArgb(70, 70, 73),
            .ForeColor = Color.White,
            .Text = "true",
            .Visible = True
        }
        AddHandler _txtValueJson.TextChanged, AddressOf UpdateSummary

        _valueControlPanel.Controls.AddRange({_cboValueEnum, _numValueInt, _txtValueJson})
    End Sub

    Private Sub OnDeviceChanged(sender As Object, e As EventArgs)
        Console.WriteLine("[ActionControl] OnDeviceChanged appelé")

        If _cboDevice.SelectedItem Is Nothing Then
            Console.WriteLine("[ActionControl] Aucun appareil sélectionné")
            Return
        End If

        Dim deviceItem = CType(_cboDevice.SelectedItem, DeviceItem)
        Console.WriteLine($"[ActionControl] Appareil sélectionné: {deviceItem.Name} (ID: {deviceItem.Id})")

        ' Récupérer les spécifications depuis le cache (déjà dans l'objet device)
        Dim deviceSpecs As JObject = Nothing

        For Each device As JToken In _devices
            If GetJsonString(device, "id") = deviceItem.Id Then
                Console.WriteLine($"[ActionControl] Device trouvé dans _devices")

                ' Les specs sont déjà attachées par GetDevicesByHomeAsync
                Dim cachedSpecs = device("_cached_specifications")
                If cachedSpecs IsNot Nothing Then
                    Console.WriteLine($"[ActionControl] _cached_specifications existe")
                    If TypeOf cachedSpecs Is JObject Then
                        deviceSpecs = CType(cachedSpecs, JObject)
                        Console.WriteLine($"[ActionControl] Specs récupérées: {deviceSpecs.ToString().Substring(0, Math.Min(100, deviceSpecs.ToString().Length))}...")
                    Else
                        Console.WriteLine($"[ActionControl] _cached_specifications n'est pas un JObject: {cachedSpecs.GetType().Name}")
                    End If
                Else
                    Console.WriteLine($"[ActionControl] _cached_specifications est NULL")
                End If
                Exit For
            End If
        Next

        ' Peupler la ComboBox des commandes avec les "functions" disponibles
        _cboProperty.Items.Clear()

        If deviceSpecs IsNot Nothing AndAlso deviceSpecs("functions") IsNot Nothing Then
            Dim functions = CType(deviceSpecs("functions"), JArray)
            Console.WriteLine($"[ActionControl] {functions.Count} fonctions trouvées dans les specs")

            For Each func As JToken In functions
                Dim code = GetJsonString(func, "code")
                If Not String.IsNullOrEmpty(code) Then
                    ' Stocker la spec complète dans un CommandItem
                    _cboProperty.Items.Add(New CommandItem(code, func))
                End If
            Next
            Console.WriteLine($"[ActionControl] {_cboProperty.Items.Count} commandes ajoutées")
        Else
            Console.WriteLine($"[ActionControl] Pas de specs disponibles - utilisation des commandes par défaut")
            ' Fallback : commandes par défaut si pas de specs
            _cboProperty.Items.AddRange({"switch_1", "switch", "bright_value", "temp_set", "colour_data"})
        End If

        If _cboProperty.Items.Count > 0 Then
            _cboProperty.SelectedIndex = 0
        End If
    End Sub

    Private Sub OnCommandChanged(sender As Object, e As EventArgs)
        Console.WriteLine("[ActionControl] OnCommandChanged appelé")

        If _cboProperty.SelectedItem Is Nothing Then
            Console.WriteLine("[ActionControl] Aucune commande sélectionnée")
            UpdateSummary(Nothing, Nothing)
            Return
        End If

        ' Vérifier si c'est un CommandItem (avec specs) ou un string (fallback)
        If TypeOf _cboProperty.SelectedItem Is CommandItem Then
            Dim commandItem = CType(_cboProperty.SelectedItem, CommandItem)
            Dim funcSpec = commandItem.Specification

            ' Obtenir le type et les valeurs
            Dim funcType = GetJsonString(funcSpec, "type")
            Dim valuesJson = GetJsonString(funcSpec, "values")

            Console.WriteLine($"[ActionControl] Commande: {commandItem.Code}, Type: {funcType}")
            Console.WriteLine($"[ActionControl] Values JSON: {valuesJson}")

            ' Configurer le contrôle approprié selon le type
            ConfigureValueControl(funcType, valuesJson)
        Else
            Console.WriteLine($"[ActionControl] Item est un String (fallback): {_cboProperty.SelectedItem}")
            ' Fallback : utiliser TextBox
            _cboValueEnum.Visible = False
            _numValueInt.Visible = False
            _txtValueJson.Visible = True
        End If

        UpdateSummary(Nothing, Nothing)
    End Sub

    Private Sub ConfigureValueControl(funcType As String, valuesJson As String)
        Console.WriteLine($"[ActionControl] ConfigureValueControl - Type: {funcType}")

        ' Masquer tous les contrôles
        _cboValueEnum.Visible = False
        _numValueInt.Visible = False
        _txtValueJson.Visible = False

        Select Case funcType
            Case "Boolean"
                Console.WriteLine("[ActionControl] Configuration Boolean → ComboBox")
                _cboValueEnum.Items.Clear()
                _cboValueEnum.Items.AddRange({"true", "false"})
                _cboValueEnum.SelectedIndex = 0
                _cboValueEnum.Visible = True
                Console.WriteLine($"[ActionControl] ComboBox visible: {_cboValueEnum.Visible}")

            Case "Integer"
                Try
                    Dim values = JObject.Parse(valuesJson)

                    ' Extraire le scale (facteur de division) et unit
                    _currentScale = If(values("scale") IsNot Nothing, CInt(values("scale")), 0)
                    _currentUnit = If(values("unit") IsNot Nothing, values("unit").ToString(), "")

                    ' Calculer le diviseur basé sur le scale
                    Dim scaleFactor = CDec(Math.Pow(10, _currentScale))

                    ' Récupérer les valeurs brutes de l'API
                    Dim minRaw = CInt(values("min"))
                    Dim maxRaw = CInt(values("max"))
                    Dim stepRaw = If(values("step") IsNot Nothing, CInt(values("step")), 1)

                    Console.WriteLine($"[ActionControl] Configuration Integer → NumericUpDown (raw: {minRaw}-{maxRaw}, scale: {_currentScale}, unit: {_currentUnit})")

                    ' Appliquer le scale pour l'affichage
                    _numValueInt.DecimalPlaces = _currentScale
                    _numValueInt.Minimum = CDec(minRaw / scaleFactor)
                    _numValueInt.Maximum = CDec(maxRaw / scaleFactor)
                    _numValueInt.Increment = CDec(stepRaw / scaleFactor)
                    _numValueInt.Value = CDec(minRaw / scaleFactor)
                    _numValueInt.Visible = True

                    Console.WriteLine($"[ActionControl] NumericUpDown visible, affichage: {_numValueInt.Minimum}-{_numValueInt.Maximum}")
                Catch ex As Exception
                    Console.WriteLine($"[ActionControl] Erreur parsing Integer: {ex.Message} - Fallback TextBox")
                    _currentScale = 0
                    _currentUnit = ""
                    _txtValueJson.Visible = True
                End Try

            Case "Enum"
                Try
                    Dim values = JObject.Parse(valuesJson)
                    Dim range = CType(values("range"), JArray)
                    Console.WriteLine($"[ActionControl] Configuration Enum → ComboBox ({range.Count} options)")

                    _cboValueEnum.Items.Clear()
                    For Each opt As JToken In range
                        _cboValueEnum.Items.Add(opt.ToString())
                    Next

                    If _cboValueEnum.Items.Count > 0 Then
                        _cboValueEnum.SelectedIndex = 0
                    End If

                    _cboValueEnum.Visible = True
                    Console.WriteLine($"[ActionControl] ComboBox visible: {_cboValueEnum.Visible}")
                Catch ex As Exception
                    Console.WriteLine($"[ActionControl] Erreur parsing Enum: {ex.Message} - Fallback TextBox")
                    _txtValueJson.Visible = True
                End Try

            Case Else
                Console.WriteLine($"[ActionControl] Type '{funcType}' non reconnu ou Json → TextBox")
                _txtValueJson.Visible = True
        End Select

        Console.WriteLine($"[ActionControl] État final - Enum: {_cboValueEnum.Visible}, Numeric: {_numValueInt.Visible}, Text: {_txtValueJson.Visible}")
    End Sub

    Private Sub UpdateSummary(sender As Object, e As EventArgs)
        Try
            If _cboDevice.SelectedItem Is Nothing Then
                _lblSummary.Text = "⚙️ Configuration: Sélectionnez un appareil pour commencer"
                _lblSummary.ForeColor = Color.FromArgb(255, 149, 0)
                Return
            End If

            Dim deviceName = _cboDevice.SelectedItem.ToString()
            Dim commandName = If(_cboProperty.SelectedItem IsNot Nothing, _cboProperty.SelectedItem.ToString(), "???")

            ' Récupérer la valeur depuis le contrôle visible
            Dim valueText As String = ""
            If _cboValueEnum.Visible AndAlso _cboValueEnum.SelectedItem IsNot Nothing Then
                valueText = _cboValueEnum.SelectedItem.ToString()
            ElseIf _numValueInt.Visible Then
                ' Afficher avec l'unité si disponible
                valueText = _numValueInt.Value.ToString()
                If Not String.IsNullOrEmpty(_currentUnit) Then
                    valueText &= _currentUnit
                End If
            ElseIf _txtValueJson.Visible Then
                valueText = _txtValueJson.Text
            End If

            _lblSummary.Text = $"📋 Alors [{deviceName}] → définir '{commandName}' = {valueText}"
            _lblSummary.ForeColor = Color.FromArgb(52, 199, 89)
        Catch ex As Exception
            _lblSummary.Text = "⚙️ Configuration en cours..."
            _lblSummary.ForeColor = Color.FromArgb(200, 200, 200)
        End Try
    End Sub

    Private Sub LoadExistingData(action As JToken)
        Dim entityId = GetJsonString(action, "entity_id")
        For i = 0 To _cboDevice.Items.Count - 1
            Dim item = CType(_cboDevice.Items(i), DeviceItem)
            If item.Id = entityId Then
                _cboDevice.SelectedIndex = i
                Exit For
            End If
        Next

        Dim executorProperty = action("executor_property")
        If executorProperty IsNot Nothing AndAlso TypeOf executorProperty Is JObject Then
            Dim propObj = CType(executorProperty, JObject)
            If propObj.Count > 0 Then
                Dim firstProp = propObj.Properties().First()
                Dim propName = firstProp.Name
                Dim propValue = firstProp.Value

                ' Propriété
                For i = 0 To _cboProperty.Items.Count - 1
                    If _cboProperty.Items(i).ToString() = propName Then
                        _cboProperty.SelectedIndex = i
                        Exit For
                    End If
                Next
                If _cboProperty.SelectedIndex = -1 AndAlso Not String.IsNullOrEmpty(propName) Then
                    _cboProperty.Items.Add(propName)
                    _cboProperty.SelectedItem = propName
                End If

                ' Valeur - charger dans le contrôle approprié en appliquant le scale
                If propValue IsNot Nothing Then
                    ' Attendre que OnCommandChanged ait configuré le contrôle
                    Application.DoEvents()

                    ' Maintenant charger la valeur dans le contrôle visible
                    If _numValueInt.Visible Then
                        ' Pour NumericUpDown, diviser par le scale pour l'affichage
                        Dim rawValue As Decimal = 0
                        If IsNumeric(propValue.ToString()) Then
                            rawValue = CDec(propValue)
                            Dim scaleFactor = CDec(Math.Pow(10, _currentScale))
                            Dim displayValue = rawValue / scaleFactor
                            _numValueInt.Value = Math.Max(_numValueInt.Minimum, Math.Min(_numValueInt.Maximum, CDec(displayValue)))
                            Console.WriteLine($"[ActionControl] Chargement valeur: API={rawValue}, Scale={_currentScale}, Affichage={_numValueInt.Value}")
                        End If
                    ElseIf _cboValueEnum.Visible Then
                        ' Pour ComboBox, chercher la valeur correspondante
                        Dim valueStr = propValue.ToString().ToLower()
                        For i = 0 To _cboValueEnum.Items.Count - 1
                            If _cboValueEnum.Items(i).ToString().ToLower() = valueStr Then
                                _cboValueEnum.SelectedIndex = i
                                Exit For
                            End If
                        Next
                    Else
                        ' Pour TextBox, utiliser la valeur brute
                        _txtValueJson.Text = propValue.ToString()
                    End If
                End If
            End If
        End If

        ' Mettre à jour le résumé après avoir chargé toutes les données
        UpdateSummary(Nothing, Nothing)
    End Sub

    Public Function GetActionData() As JObject
        If _cboDevice.SelectedItem Is Nothing OrElse _cboProperty.SelectedItem Is Nothing Then
            Return Nothing
        End If

        Dim deviceItem = CType(_cboDevice.SelectedItem, DeviceItem)
        Dim action As New JObject()
        action("entity_id") = deviceItem.Id
        action("action_executor") = "dpIssue"

        ' Construire executor_property
        Dim executorProperty As New JObject()
        Dim propertyName = _cboProperty.SelectedItem.ToString()

        ' Récupérer la valeur depuis le contrôle visible
        Dim valueText As String = ""
        Dim numericValue As Decimal = 0

        If _cboValueEnum.Visible AndAlso _cboValueEnum.SelectedItem IsNot Nothing Then
            valueText = _cboValueEnum.SelectedItem.ToString()
        ElseIf _numValueInt.Visible Then
            ' Pour NumericUpDown, appliquer le scale inverse (multiplier par 10^scale)
            Dim displayValue = _numValueInt.Value
            Dim scaleFactor = CDec(Math.Pow(10, _currentScale))
            numericValue = CDec(displayValue * scaleFactor)
            valueText = numericValue.ToString()
            Console.WriteLine($"[ActionControl] Valeur affichée: {displayValue}{_currentUnit}, Scale: {_currentScale}, Valeur envoyée: {numericValue}")
        ElseIf _txtValueJson.Visible Then
            valueText = _txtValueJson.Text.Trim()
        End If

        ' Parser la valeur
        If valueText.ToLower() = "true" Then
            executorProperty(propertyName) = True
        ElseIf valueText.ToLower() = "false" Then
            executorProperty(propertyName) = False
        ElseIf _numValueInt.Visible Then
            ' Utiliser la valeur numérique déjà mise à l'échelle
            executorProperty(propertyName) = CInt(numericValue)
        ElseIf IsNumeric(valueText) Then
            executorProperty(propertyName) = CDbl(valueText)
        Else
            executorProperty(propertyName) = valueText
        End If

        action("executor_property") = executorProperty
        Return action
    End Function

    Private Function GetJsonString(token As JToken, key As String) As String
        Try
            If token Is Nothing Then Return ""
            Dim value = token(key)
            If value Is Nothing Then Return ""
            Return value.ToString()
        Catch
            Return ""
        End Try
    End Function
End Class
#End Region

#Region "Classe DeviceItem"
Public Class DeviceItem
    Public Property Id As String
    Public Property Name As String

    Public Sub New(id As String, name As String)
        Me.Id = id
        Me.Name = name
    End Sub

    Public Overrides Function ToString() As String
        Return Name
    End Function
End Class
#End Region

#Region "Classe CommandItem"
''' <summary>
''' Classe helper pour stocker une commande avec sa spécification complète
''' </summary>
Public Class CommandItem
    Public Property Code As String
    Public Property Specification As JToken

    Public Sub New(code As String, spec As JToken)
        Me.Code = code
        Me.Specification = spec
    End Sub

    Public Overrides Function ToString() As String
        Return Code
    End Function
End Class
#End Region
