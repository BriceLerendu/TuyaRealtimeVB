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

        ' IMPORTANT: Ajouter dans cet ordre précis
        Me.Controls.Add(_devicesPanel)
        Me.Controls.Add(bottomPanel)
        Me.Controls.Add(headerPanel)
    End Sub

    Private Sub InitializeServices()
        Try
            Dim cfg = TuyaConfig.Load()
            Dim tokenProvider As New TuyaTokenProvider(cfg)

            _apiClient = New TuyaApiClient(cfg, tokenProvider)

            Task.Run(Async Function()
                         Await _apiClient.InitializeRoomsCacheAsync()
                         Me.Invoke(Sub() UpdateStatus("Cache des pièces chargé"))
                     End Function)

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

            If Not _deviceCards.ContainsKey(devId) Then
                Dim card As New DeviceCard(devId)
                _deviceCards(devId) = card
                _devicesPanel.Controls.Add(card)

                Task.Run(Async Function()
                             Dim deviceInfo = Await _apiClient.GetDeviceInfoAsync(devId)
                             If deviceInfo IsNot Nothing Then
                                 Me.Invoke(Sub() card.UpdateDeviceInfo(deviceInfo))
                             End If
                         End Function)
            End If

            Dim deviceCard = _deviceCards(devId)

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

Public Class DeviceCard
    Inherits Panel

    Private _deviceId As String
    Private _nameLabel As System.Windows.Forms.Label
    Private _roomLabel As System.Windows.Forms.Label
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

        _nameLabel = New System.Windows.Forms.Label()
        _nameLabel.Text = "Chargement..."
        _nameLabel.Font = New System.Drawing.Font("Segoe UI", 11, FontStyle.Bold)
        _nameLabel.ForeColor = Color.FromArgb(45, 45, 48)
        _nameLabel.AutoSize = True
        _nameLabel.Location = New Point(15, 15)
        Me.Controls.Add(_nameLabel)

        _roomLabel = New System.Windows.Forms.Label()
        _roomLabel.Text = ""
        _roomLabel.Font = New System.Drawing.Font("Segoe UI", 9, FontStyle.Italic)
        _roomLabel.ForeColor = Color.FromArgb(100, 100, 100)
        _roomLabel.AutoSize = True
        _roomLabel.Location = New Point(15, 38)
        _roomLabel.Visible = False
        Me.Controls.Add(_roomLabel)

        _idLabel = New System.Windows.Forms.Label()
        _idLabel.Text = deviceId
        _idLabel.Font = New System.Drawing.Font("Segoe UI", 7)
        _idLabel.ForeColor = Color.LightGray
        _idLabel.AutoSize = True
        _idLabel.Location = New Point(15, 58)
        Me.Controls.Add(_idLabel)

        _statusLabel = New System.Windows.Forms.Label()
        _statusLabel.Text = "●"
        _statusLabel.Font = New System.Drawing.Font("Segoe UI", 16)
        _statusLabel.ForeColor = Color.Gray
        _statusLabel.AutoSize = True
        _statusLabel.Location = New Point(280, 12)
        Me.Controls.Add(_statusLabel)

        _timestampLabel = New System.Windows.Forms.Label()
        _timestampLabel.Text = DateTime.Now.ToString("HH:mm:ss")
        _timestampLabel.Font = New System.Drawing.Font("Segoe UI", 8)
        _timestampLabel.ForeColor = Color.Gray
        _timestampLabel.AutoSize = True
        _timestampLabel.Location = New Point(15, 95)
        Me.Controls.Add(_timestampLabel)

        _propertiesPanel = New TableLayoutPanel()
        _propertiesPanel.Location = New Point(15, 115)
        _propertiesPanel.Size = New Size(280, 70)
        _propertiesPanel.ColumnCount = 2
        _propertiesPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        _propertiesPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        _propertiesPanel.AutoScroll = True
        Me.Controls.Add(_propertiesPanel)
    End Sub

    Public Sub UpdateDeviceInfo(deviceInfo As DeviceInfo)
        If deviceInfo IsNot Nothing Then
            If Not String.IsNullOrEmpty(deviceInfo.Name) Then
                _nameLabel.Text = deviceInfo.Name
            ElseIf Not String.IsNullOrEmpty(deviceInfo.ProductName) Then
                _nameLabel.Text = deviceInfo.ProductName
            End If

            If Not String.IsNullOrEmpty(deviceInfo.RoomName) Then
                _roomLabel.Text = $"📍 {deviceInfo.RoomName}"
                _roomLabel.Visible = True
                _idLabel.Location = New Point(15, 78)
            End If

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

        Dim displayValue = FormatValue(code, value)
        _properties(code).Text = displayValue

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
            Return If(value = "true" Or value = "True", "ON", "OFF")
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