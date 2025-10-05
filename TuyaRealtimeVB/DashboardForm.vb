Imports System.Drawing
Imports System.Windows.Forms
Imports Newtonsoft.Json.Linq

Public Class DashboardForm
    Inherits Form

    Private WithEvents _httpServer As TuyaHttpServer
    Private WithEvents _pythonBridge As PythonBridge
    Private _devicesPanel As FlowLayoutPanel
    Private _deviceCards As New Dictionary(Of String, DeviceCard)
    Private _statusLabel As System.Windows.Forms.Label
    Private _eventCountLabel As System.Windows.Forms.Label
    Private _eventCount As Integer = 0
    Private _apiClient As TuyaApiClient

    Public Sub New()
        InitializeComponent()
        InitializeServices()
    End Sub

    Private Sub InitializeComponent()
        Me.Text = "TuyaRealtimeVB - Tableau de bord"
        Me.Size = New Size(1400, 800)
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.BackColor = Color.FromArgb(240, 240, 240)

        ' Panel du haut (header)
        Dim headerPanel As New Panel()
        headerPanel.Dock = DockStyle.Top
        headerPanel.Height = 80
        headerPanel.BackColor = Color.FromArgb(45, 45, 48)
        headerPanel.Padding = New Padding(20)

        Dim titleLabel As New System.Windows.Forms.Label()
        titleLabel.Text = "Tableau de bord Tuya - Objets connectés"
        titleLabel.Font = New System.Drawing.Font("Segoe UI", 18, FontStyle.Bold)
        titleLabel.ForeColor = Color.White
        titleLabel.AutoSize = True
        titleLabel.Location = New Point(20, 15)
        headerPanel.Controls.Add(titleLabel)

        _eventCountLabel = New System.Windows.Forms.Label()
        _eventCountLabel.Text = "Événements: 0"
        _eventCountLabel.Font = New System.Drawing.Font("Segoe UI", 10)
        _eventCountLabel.ForeColor = Color.LightGray
        _eventCountLabel.AutoSize = True
        _eventCountLabel.Location = New Point(20, 48)
        headerPanel.Controls.Add(_eventCountLabel)

        ' Panel scrollable pour les appareils
        _devicesPanel = New FlowLayoutPanel()
        _devicesPanel.Dock = DockStyle.Fill
        _devicesPanel.AutoScroll = True
        _devicesPanel.Padding = New Padding(20)
        _devicesPanel.BackColor = Color.FromArgb(240, 240, 240)

        ' Status bar
        Dim bottomPanel As New Panel()
        bottomPanel.Dock = DockStyle.Bottom
        bottomPanel.Height = 30
        bottomPanel.BackColor = Color.FromArgb(45, 45, 48)

        _statusLabel = New System.Windows.Forms.Label()
        _statusLabel.Text = "Système démarré"
        _statusLabel.Font = New System.Drawing.Font("Segoe UI", 9)
        _statusLabel.ForeColor = Color.LightGray
        _statusLabel.AutoSize = True
        _statusLabel.Location = New Point(10, 7)
        bottomPanel.Controls.Add(_statusLabel)

        ' IMPORTANT: Ordre d'ajout inversé pour que Dock fonctionne correctement
        Me.Controls.Add(_devicesPanel)
        Me.Controls.Add(bottomPanel)
        Me.Controls.Add(headerPanel)
    End Sub

    Private Sub InitializeServices()
        Try
            ' Initialiser la configuration et le token provider
            Dim cfg = TuyaConfig.Load()
            Dim tokenProvider As New TuyaTokenProvider(cfg)

            ' Créer le client API
            _apiClient = New TuyaApiClient(cfg, tokenProvider)

            _httpServer = New TuyaHttpServer()
            AddHandler _httpServer.EventReceived, AddressOf OnEventReceived
            _httpServer.Start()

            Dim pythonScriptPath = "C:\Users\leren\Downloads\tuya_bridge.py"
            _pythonBridge = New PythonBridge(pythonScriptPath)
            _pythonBridge.Start()

            UpdateStatus("Système opérationnel - En attente d'événements")

        Catch ex As Exception
            UpdateStatus($"Erreur: {ex.Message}")
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

            If String.IsNullOrEmpty(devId) Then Return

            _eventCount += 1
            _eventCountLabel.Text = $"Événements: {_eventCount}"

            ' Créer ou mettre à jour la carte de l'appareil
            If Not _deviceCards.ContainsKey(devId) Then
                Dim card As New DeviceCard(devId)
                _deviceCards(devId) = card
                _devicesPanel.Controls.Add(card)

                ' Charger les infos de l'appareil via l'API en arrière-plan
                Task.Run(Async Function()
                             Dim deviceInfo = Await _apiClient.GetDeviceInfoAsync(devId)
                             If deviceInfo IsNot Nothing Then
                                 Me.Invoke(Sub() card.UpdateDeviceInfo(deviceInfo))
                             End If
                         End Function)
            End If

            Dim deviceCard = _deviceCards(devId)

            ' Mettre à jour les données
            If status IsNot Nothing Then
                For Each item In status
                    Dim code = item.SelectToken("code")?.ToString()
                    Dim value = item.SelectToken("value")?.ToString()
                    deviceCard.UpdateProperty(code, value)
                Next
            End If

            If Not String.IsNullOrEmpty(bizCode) Then
                deviceCard.UpdateStatus(bizCode)
            End If

            deviceCard.UpdateTimestamp()

            UpdateStatus($"Dernier événement: {devId} - {DateTime.Now:HH:mm:ss}")

        Catch ex As Exception
            UpdateStatus($"Erreur: {ex.Message}")
        End Try
    End Sub

    Private Sub UpdateStatus(message As String)
        If Me.InvokeRequired Then
            Me.Invoke(Sub() UpdateStatus(message))
            Return
        End If

        _statusLabel.Text = message
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        MyBase.OnFormClosing(e)
        _pythonBridge?.Stop()
        _httpServer?.Stop()
    End Sub
End Class

' Classe représentant une carte d'appareil
Public Class DeviceCard
    Inherits Panel

    Private _deviceId As String
    Private _nameLabel As System.Windows.Forms.Label
    Private _idLabel As System.Windows.Forms.Label
    Private _statusLabel As System.Windows.Forms.Label
    Private _timestampLabel As System.Windows.Forms.Label
    Private _propertiesPanel As TableLayoutPanel
    Private _properties As New Dictionary(Of String, System.Windows.Forms.Label)

    Public Sub New(deviceId As String)
        _deviceId = deviceId

        Me.Size = New Size(320, 200)
        Me.BackColor = Color.White
        Me.BorderStyle = BorderStyle.FixedSingle
        Me.Padding = New Padding(15)
        Me.Margin = New Padding(10)

        ' En-tête
        _nameLabel = New System.Windows.Forms.Label()
        _nameLabel.Text = "Chargement..."
        _nameLabel.Font = New System.Drawing.Font("Segoe UI", 11, FontStyle.Bold)
        _nameLabel.ForeColor = Color.FromArgb(45, 45, 48)
        _nameLabel.AutoSize = True
        _nameLabel.Location = New Point(15, 15)
        Me.Controls.Add(_nameLabel)

        ' ID (petit texte sous le nom)
        _idLabel = New System.Windows.Forms.Label()
        _idLabel.Text = deviceId
        _idLabel.Font = New System.Drawing.Font("Segoe UI", 8)
        _idLabel.ForeColor = Color.Gray
        _idLabel.AutoSize = True
        _idLabel.Location = New Point(15, 38)
        Me.Controls.Add(_idLabel)

        ' Statut
        _statusLabel = New System.Windows.Forms.Label()
        _statusLabel.Text = "●"
        _statusLabel.Font = New System.Drawing.Font("Segoe UI", 16)
        _statusLabel.ForeColor = Color.Gray
        _statusLabel.AutoSize = True
        _statusLabel.Location = New Point(280, 12)
        Me.Controls.Add(_statusLabel)

        ' Timestamp
        _timestampLabel = New System.Windows.Forms.Label()
        _timestampLabel.Text = DateTime.Now.ToString("HH:mm:ss")
        _timestampLabel.Font = New System.Drawing.Font("Segoe UI", 8)
        _timestampLabel.ForeColor = Color.Gray
        _timestampLabel.AutoSize = True
        _timestampLabel.Location = New Point(15, 58)
        Me.Controls.Add(_timestampLabel)

        ' Panel des propriétés
        _propertiesPanel = New TableLayoutPanel()
        _propertiesPanel.Location = New Point(15, 80)
        _propertiesPanel.Size = New Size(280, 105)
        _propertiesPanel.ColumnCount = 2
        _propertiesPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        _propertiesPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        _propertiesPanel.AutoScroll = True
        Me.Controls.Add(_propertiesPanel)
    End Sub

    Public Sub UpdateDeviceInfo(deviceInfo As DeviceInfo)
        If deviceInfo IsNot Nothing Then
            ' Mettre à jour le nom
            If Not String.IsNullOrEmpty(deviceInfo.Name) Then
                _nameLabel.Text = deviceInfo.Name
            ElseIf Not String.IsNullOrEmpty(deviceInfo.ProductName) Then
                _nameLabel.Text = deviceInfo.ProductName
            End If

            ' Mettre à jour le statut online/offline
            If deviceInfo.IsOnline Then
                UpdateStatus("online")
            Else
                UpdateStatus("offline")
            End If
        End If
    End Sub

    Public Sub UpdateProperty(code As String, value As String)
        If Not _properties.ContainsKey(code) Then
            Dim row = _propertiesPanel.RowCount
            _propertiesPanel.RowCount += 1
            _propertiesPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 25))

            Dim codeLabel As New System.Windows.Forms.Label()
            codeLabel.Text = code
            codeLabel.Font = New System.Drawing.Font("Segoe UI", 9)
            codeLabel.ForeColor = Color.FromArgb(100, 100, 100)
            codeLabel.AutoSize = True
            _propertiesPanel.Controls.Add(codeLabel, 0, row)

            Dim valueLabel As New System.Windows.Forms.Label()
            valueLabel.Font = New System.Drawing.Font("Segoe UI", 9, FontStyle.Bold)
            valueLabel.ForeColor = Color.FromArgb(45, 45, 48)
            valueLabel.AutoSize = True
            _propertiesPanel.Controls.Add(valueLabel, 1, row)

            _properties(code) = valueLabel
        End If

        ' Formatage de la valeur
        Dim displayValue = FormatValue(code, value)
        _properties(code).Text = displayValue

        ' Couleur selon le type
        If code.Contains("switch") OrElse code = "doorcontact_state" Then
            _properties(code).ForeColor = If(value = "true" Or value = "True", Color.Green, Color.Red)
        ElseIf code.Contains("temperature") Then
            _properties(code).ForeColor = Color.Orange
        ElseIf code.Contains("humidity") Then
            _properties(code).ForeColor = Color.Blue
        ElseIf code.Contains("power") Then
            _properties(code).ForeColor = Color.Purple
        End If
    End Sub

    Private Function FormatValue(code As String, value As String) As String
        If code.Contains("temperature") Then
            Return $"{value} °C"
        ElseIf code.Contains("humidity") Then
            Return $"{value} %"
        ElseIf code.Contains("power") Then
            Return $"{value} W"
        ElseIf code.Contains("switch") OrElse code = "doorcontact_state" Then
            If value = "true" Or value = "True" Then
                Return "ON"
            Else
                Return "OFF"
            End If
        Else
            Return value
        End If
    End Function

    Public Sub UpdateStatus(bizCode As String)
        If bizCode = "online" Then
            _statusLabel.ForeColor = Color.LimeGreen
            Me.BackColor = Color.FromArgb(240, 255, 240)
        ElseIf bizCode = "offline" Then
            _statusLabel.ForeColor = Color.Red
            Me.BackColor = Color.FromArgb(255, 240, 240)
        Else
            _statusLabel.ForeColor = Color.Orange
            Me.BackColor = Color.White
        End If
    End Sub

    Public Sub UpdateTimestamp()
        _timestampLabel.Text = DateTime.Now.ToString("HH:mm:ss")
    End Sub
End Class