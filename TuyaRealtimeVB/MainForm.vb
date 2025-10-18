Imports System.Drawing
Imports System.Windows.Forms
Imports Newtonsoft.Json.Linq

Public Class MainForm
    Inherits Form

    Private WithEvents _httpServer As TuyaHttpServer
    Private WithEvents _pythonBridge As PythonBridge
    Private _listView As ListView
    Private _statusLabel As System.Windows.Forms.Label
    Private _serverStatusLabel As System.Windows.Forms.Label
    Private _pythonStatusLabel As System.Windows.Forms.Label
    Private _eventCountLabel As System.Windows.Forms.Label
    Private _eventCount As Integer = 0

    ' ✅ NOUVEAU : Vue tableau (ajout minimal)
    Private _tableView As RoomTableView
    Private _currentView As String = "events" ' "events" ou "table"
    Private _btnEvents As Button
    Private _btnTable As Button

    Public Sub New()
        InitializeComponent()
        InitializeServices()
    End Sub

    Private Sub InitializeComponent()
        ' Configuration de la fenêtre
        Me.Text = "TuyaRealtimeVB - Moniteur d'événements"
        Me.Size = New Size(1000, 600)
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.BackColor = Color.FromArgb(240, 240, 240)

        ' Panel du haut (status)
        Dim topPanel As New Panel()
        topPanel.Dock = DockStyle.Top
        topPanel.Height = 100
        topPanel.BackColor = Color.FromArgb(45, 45, 48)
        topPanel.Padding = New Padding(20)

        ' Titre
        Dim titleLabel As New System.Windows.Forms.Label()
        titleLabel.Text = "Moniteur d'événements Tuya en temps réel"
        titleLabel.Font = New System.Drawing.Font("Segoe UI", 16, FontStyle.Bold)
        titleLabel.ForeColor = Color.White
        titleLabel.AutoSize = True
        titleLabel.Location = New Point(20, 15)
        topPanel.Controls.Add(titleLabel)

        ' Statuts
        _serverStatusLabel = New System.Windows.Forms.Label()
        _serverStatusLabel.Text = "● Serveur HTTP: Démarrage..."
        _serverStatusLabel.Font = New System.Drawing.Font("Segoe UI", 9)
        _serverStatusLabel.ForeColor = Color.Orange
        _serverStatusLabel.AutoSize = True
        _serverStatusLabel.Location = New Point(20, 50)
        topPanel.Controls.Add(_serverStatusLabel)

        _pythonStatusLabel = New System.Windows.Forms.Label()
        _pythonStatusLabel.Text = "● Client Pulsar: Démarrage..."
        _pythonStatusLabel.Font = New System.Drawing.Font("Segoe UI", 9)
        _pythonStatusLabel.ForeColor = Color.Orange
        _pythonStatusLabel.AutoSize = True
        _pythonStatusLabel.Location = New Point(250, 50)
        topPanel.Controls.Add(_pythonStatusLabel)

        _eventCountLabel = New System.Windows.Forms.Label()
        _eventCountLabel.Text = "Événements reçus: 0"
        _eventCountLabel.Font = New System.Drawing.Font("Segoe UI", 9)
        _eventCountLabel.ForeColor = Color.LightGray
        _eventCountLabel.AutoSize = True
        _eventCountLabel.Location = New Point(500, 50)
        topPanel.Controls.Add(_eventCountLabel)

        ' ✅ NOUVEAU : Boutons pour basculer entre les vues (POSITIONS FIXES À GAUCHE POUR TEST)
        _btnEvents = New Button()
        _btnEvents.Text = "EVENEMENTS"
        _btnEvents.Size = New Size(150, 40)
        _btnEvents.Location = New Point(500, 30)
        _btnEvents.BackColor = Color.Red
        _btnEvents.ForeColor = Color.White
        _btnEvents.Font = New Font("Arial", 12, FontStyle.Bold)
        AddHandler _btnEvents.Click, Sub(s, e) SwitchToEventsView()
        topPanel.Controls.Add(_btnEvents)

        _btnTable = New Button()
        _btnTable.Text = "TABLEAU"
        _btnTable.Size = New Size(150, 40)
        _btnTable.Location = New Point(670, 30)
        _btnTable.BackColor = Color.Blue
        _btnTable.ForeColor = Color.White
        _btnTable.Font = New Font("Arial", 12, FontStyle.Bold)
        AddHandler _btnTable.Click, Sub(s, e) SwitchToTableView()
        topPanel.Controls.Add(_btnTable)

        Me.Controls.Add(topPanel)

        ' ListView pour les événements (vue actuelle)
        _listView = New ListView()
        _listView.Dock = DockStyle.Fill
        _listView.View = View.Details
        _listView.FullRowSelect = True
        _listView.GridLines = True
        _listView.Font = New System.Drawing.Font("Consolas", 9)
        _listView.BackColor = Color.White

        ' Colonnes
        _listView.Columns.Add("Heure", 100)
        _listView.Columns.Add("Appareil", 200)
        _listView.Columns.Add("Type", 120)
        _listView.Columns.Add("Propriété", 150)
        _listView.Columns.Add("Valeur", 200)
        _listView.Columns.Add("JSON", 200)

        Me.Controls.Add(_listView)

        ' ✅ NOUVEAU : Vue tableau (cachée par défaut)
        _tableView = New RoomTableView With {
            .Dock = DockStyle.Fill,
            .Visible = False
        }
        Me.Controls.Add(_tableView)

        ' Panel du bas (status bar)
        Dim bottomPanel As New Panel()
        bottomPanel.Dock = DockStyle.Bottom
        bottomPanel.Height = 30
        bottomPanel.BackColor = Color.FromArgb(45, 45, 48)

        _statusLabel = New System.Windows.Forms.Label()
        _statusLabel.Text = "Prêt"
        _statusLabel.Font = New System.Drawing.Font("Segoe UI", 9)
        _statusLabel.ForeColor = Color.LightGray
        _statusLabel.AutoSize = True
        _statusLabel.Location = New Point(10, 7)
        bottomPanel.Controls.Add(_statusLabel)

        Me.Controls.Add(bottomPanel)
    End Sub

    Private Sub InitializeServices()
        Try
            ' Démarrer le serveur HTTP
            _httpServer = New TuyaHttpServer()
            AddHandler _httpServer.EventReceived, AddressOf OnEventReceived
            _httpServer.Start()

            UpdateStatus("Serveur HTTP démarré", _serverStatusLabel, Color.LimeGreen)

            ' Démarrer Python
            Dim pythonScriptPath = "C:\Users\leren\Downloads\tuya_bridge.py"
            _pythonBridge = New PythonBridge(pythonScriptPath)
            _pythonBridge.Start()

            UpdateStatus("Client Pulsar connecté", _pythonStatusLabel, Color.LimeGreen)
            UpdateStatus("Système opérationnel - En attente d'événements")

        Catch ex As Exception
            UpdateStatus($"Erreur: {ex.Message}", _statusLabel, Color.Red)
            MessageBox.Show($"Erreur d'initialisation: {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub OnEventReceived(eventData As String)
        If Me.InvokeRequired Then
            Me.Invoke(Sub() OnEventReceived(eventData))
            Return
        End If

        Try
            Dim json = JObject.Parse(eventData)
            Dim devId = json.SelectToken("devId")?.ToString()
            Dim status = json.SelectToken("status")
            Dim bizCode = json.SelectToken("bizCode")?.ToString()
            Dim timestamp = DateTime.Now.ToString("HH:mm:ss")

            _eventCount += 1
            _eventCountLabel.Text = $"Événements reçus: {_eventCount}"

            If status IsNot Nothing Then
                For Each item In status
                    Dim code = item.SelectToken("code")?.ToString()
                    Dim value = item.SelectToken("value")?.ToString()

                    Dim listItem As New ListViewItem(timestamp)
                    listItem.SubItems.Add(devId)
                    listItem.SubItems.Add(If(bizCode, "report"))
                    listItem.SubItems.Add(code)
                    listItem.SubItems.Add(value)
                    listItem.SubItems.Add(json.ToString(Newtonsoft.Json.Formatting.None))

                    ' Couleur selon le type
                    If bizCode = "online" Then
                        listItem.BackColor = Color.LightGreen
                    ElseIf bizCode = "offline" Then
                        listItem.BackColor = Color.LightCoral
                    End If

                    _listView.Items.Insert(0, listItem) ' Ajouter en haut

                    ' Limiter à 100 événements
                    If _listView.Items.Count > 100 Then
                        _listView.Items.RemoveAt(_listView.Items.Count - 1)
                    End If
                Next
            Else
                ' Événement sans status (online/offline)
                Dim listItem As New ListViewItem(timestamp)
                listItem.SubItems.Add(devId)
                listItem.SubItems.Add(bizCode)
                listItem.SubItems.Add("-")
                listItem.SubItems.Add("-")
                listItem.SubItems.Add(json.ToString(Newtonsoft.Json.Formatting.None))

                If bizCode = "online" Then
                    listItem.BackColor = Color.LightGreen
                ElseIf bizCode = "offline" Then
                    listItem.BackColor = Color.LightCoral
                End If

                _listView.Items.Insert(0, listItem)
            End If

            UpdateStatus($"Dernier événement: {devId} - {timestamp}")

        Catch ex As Exception
            UpdateStatus($"Erreur traitement: {ex.Message}", _statusLabel, Color.Red)
        End Try
    End Sub

    ' ✅ NOUVEAU : Basculer vers la vue événements
    Private Sub SwitchToEventsView()
        _currentView = "events"
        _listView.Visible = True
        _listView.BringToFront()
        _tableView.Visible = False
        _btnEvents.BackColor = Color.FromArgb(0, 122, 255)
        _btnTable.BackColor = Color.FromArgb(142, 142, 147)
        UpdateStatus("Vue Événements activée")
    End Sub

    ' ✅ NOUVEAU : Basculer vers la vue tableau
    Private Sub SwitchToTableView()
        _currentView = "table"
        _tableView.Visible = True
        _tableView.BringToFront()
        _listView.Visible = False
        _btnTable.BackColor = Color.FromArgb(0, 122, 255)
        _btnEvents.BackColor = Color.FromArgb(142, 142, 147)
        UpdateStatus("Vue Tableau activée")
    End Sub

    Private Sub UpdateStatus(message As String, Optional label As System.Windows.Forms.Label = Nothing, Optional color As Color = Nothing)
        If Me.InvokeRequired Then
            Me.Invoke(Sub() UpdateStatus(message, label, color))
            Return
        End If

        Dim targetLabel = If(label, _statusLabel)
        targetLabel.Text = message

        If color <> Nothing Then
            targetLabel.ForeColor = color
        End If
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        MyBase.OnFormClosing(e)

        ' Arrêter les services
        _pythonBridge?.Stop()
        _httpServer?.Stop()
    End Sub
End Class