Imports System.Drawing
Imports System.Windows.Forms
Imports Newtonsoft.Json.Linq
Imports System.IO
Imports System.Text
Imports System.Diagnostics
Imports System.Linq

Public Class DashboardForm
    Inherits Form

    Private _httpServer As TuyaHttpServer
    Private _pythonBridge As PythonBridge
    Private _devicesPanel As FlowLayoutPanel
    Private _deviceCards As New Dictionary(Of String, DeviceCard)
    Private _roomHeaders As New Dictionary(Of String, Panel)
    Private _statusLabel As Label
    Private _eventCountLabel As Label
    Private _debugTextBox As TextBox
    Private _eventCount As Integer = 0
    Private _apiClient As TuyaApiClient
    Private _deviceInfoCache As New Dictionary(Of String, DeviceInfo)
    Private ReadOnly _lockObject As New Object()
    Private _isPaused As Boolean = False
    Private _pauseButton As Button
    Private _allowAutoScroll As Boolean = False
    Private _resizeTimer As Timer
    Private _roomFilterComboBox As ComboBox
    Private _selectedRoomFilter As String = Nothing
    Private _config As TuyaConfig
    Private _isRunning As Boolean = False
    Private _startMenuItem As ToolStripMenuItem
    Private _stopMenuItem As ToolStripMenuItem
    Private _toggleDebugMenuItem As ToolStripMenuItem
    Private _splitContainer As SplitContainer
    Private _isScrolling As Boolean = False
    Private _notificationManager As NotificationManager
    Private _notificationBadge As Button
    Private _notificationsPopup As NotificationsPopup

    ' ✅ NOUVEAU : Variables pour la vue tableau
    Private _tableView As RoomTableView
    Private _currentView As String = "grid"
    Private _btnGridView As Button
    Private _btnTableView As Button

    Public Sub New()
        InitializeComponent()

        ' ✅ CORRECT - Utiliser le Singleton
        _notificationManager = NotificationManager.Instance
        AddHandler _notificationManager.NotificationAdded, AddressOf OnNotificationAdded
        UpdateNotificationBadge()
    End Sub

    Private Sub InitializeComponent()
        Me.Text = "TuyaRealtimeVB - Tableau de bord"
        Me.Size = New Size(1800, 900)
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.BackColor = Color.FromArgb(242, 242, 247)

        Dim headerPanel As New Panel()
        headerPanel.Dock = DockStyle.Top
        headerPanel.Height = 80
        headerPanel.BackColor = Color.FromArgb(45, 45, 48)
        headerPanel.Padding = New Padding(20)

        Dim titleLabel As New Label()
        titleLabel.Text = "Tableau de bord Tuya - Objets connectés"
        titleLabel.Font = New Font("Segoe UI", 18, FontStyle.Bold)
        titleLabel.ForeColor = Color.White
        titleLabel.AutoSize = True
        titleLabel.Location = New Point(20, 15)
        headerPanel.Controls.Add(titleLabel)

        ' Menu Fichier (bouton avec menu contextuel)
        Dim fileMenuButton As New Button()
        fileMenuButton.Text = "Fichier ▼"
        fileMenuButton.Font = New Font("Segoe UI", 9)
        fileMenuButton.ForeColor = Color.White
        fileMenuButton.BackColor = Color.FromArgb(55, 55, 58)
        fileMenuButton.FlatStyle = FlatStyle.Flat
        fileMenuButton.FlatAppearance.BorderSize = 0
        fileMenuButton.Size = New Size(80, 26)
        fileMenuButton.Location = New Point(20, 47)
        fileMenuButton.Cursor = Cursors.Hand

        Dim fileContextMenu As New ContextMenuStrip()
        fileContextMenu.BackColor = Color.FromArgb(45, 45, 48)
        fileContextMenu.ForeColor = Color.White

        ' Bouton DÉMARRER
        _startMenuItem = New ToolStripMenuItem("▶ Démarrer")
        _startMenuItem.ForeColor = Color.FromArgb(52, 199, 89)
        _startMenuItem.Font = New Font("Segoe UI", 10, FontStyle.Bold)
        AddHandler _startMenuItem.Click, AddressOf StartMenuItem_Click
        fileContextMenu.Items.Add(_startMenuItem)

        ' Bouton ARRÊTER
        _stopMenuItem = New ToolStripMenuItem("⏹ Arrêter")
        _stopMenuItem.ForeColor = Color.FromArgb(255, 59, 48)
        _stopMenuItem.Font = New Font("Segoe UI", 10, FontStyle.Bold)
        _stopMenuItem.Enabled = False
        AddHandler _stopMenuItem.Click, AddressOf StopMenuItem_Click
        fileContextMenu.Items.Add(_stopMenuItem)

        fileContextMenu.Items.Add(New ToolStripSeparator())

        ' Option pour masquer/afficher la console debug
        _toggleDebugMenuItem = New ToolStripMenuItem("👁 Masquer la console debug")
        _toggleDebugMenuItem.ForeColor = Color.White
        _toggleDebugMenuItem.Checked = True
        _toggleDebugMenuItem.CheckOnClick = False
        AddHandler _toggleDebugMenuItem.Click, AddressOf ToggleDebugMenuItem_Click
        fileContextMenu.Items.Add(_toggleDebugMenuItem)

        Dim settingsMenuItem As New ToolStripMenuItem("⚙ Paramètres...")
        settingsMenuItem.ShortcutKeyDisplayString = "Ctrl+P"
        settingsMenuItem.ForeColor = Color.White
        AddHandler settingsMenuItem.Click, AddressOf SettingsMenuItem_Click
        fileContextMenu.Items.Add(settingsMenuItem)

        Dim notificationsMenuItem As New ToolStripMenuItem("🔔 Gestion des alertes...")
        notificationsMenuItem.ForeColor = Color.White
        AddHandler notificationsMenuItem.Click, AddressOf NotificationsMenuItem_Click
        fileContextMenu.Items.Add(notificationsMenuItem)

        fileContextMenu.Items.Add(New ToolStripSeparator())

        Dim exitMenuItem As New ToolStripMenuItem("Quitter")
        exitMenuItem.ShortcutKeyDisplayString = "Alt+F4"
        exitMenuItem.ForeColor = Color.White
        AddHandler exitMenuItem.Click, Sub(s, e) Me.Close()
        fileContextMenu.Items.Add(exitMenuItem)

        AddHandler fileMenuButton.Click, Sub(s, e)
                                             fileContextMenu.Show(fileMenuButton, New Point(0, fileMenuButton.Height))
                                         End Sub

        headerPanel.Controls.Add(fileMenuButton)

        _eventCountLabel = New Label()
        _eventCountLabel.Text = "Événements: 0"
        _eventCountLabel.Font = New Font("Segoe UI", 10)
        _eventCountLabel.ForeColor = Color.LightGray
        _eventCountLabel.AutoSize = True
        _eventCountLabel.Location = New Point(115, 48)
        headerPanel.Controls.Add(_eventCountLabel)

        ' Badge de notifications
        _notificationBadge = New Button()
        _notificationBadge.Text = "🔔 (0)"
        _notificationBadge.Font = New Font("Segoe UI", 10, FontStyle.Bold)
        _notificationBadge.ForeColor = Color.White
        _notificationBadge.BackColor = Color.FromArgb(55, 55, 58)
        _notificationBadge.FlatStyle = FlatStyle.Flat
        _notificationBadge.FlatAppearance.BorderSize = 0
        _notificationBadge.Size = New Size(85, 26)
        _notificationBadge.Cursor = Cursors.Hand
        AddHandler _notificationBadge.Click, AddressOf NotificationBadge_Click
        headerPanel.Controls.Add(_notificationBadge)

        ' ✅ NOUVEAU : Boutons de vue
        _btnGridView = New Button()
        _btnGridView.Text = "📊 Tuiles"
        _btnGridView.Font = New Font("Segoe UI", 9, FontStyle.Bold)
        _btnGridView.ForeColor = Color.White
        _btnGridView.BackColor = Color.FromArgb(0, 122, 255)
        _btnGridView.FlatStyle = FlatStyle.Flat
        _btnGridView.FlatAppearance.BorderSize = 0
        _btnGridView.Size = New Size(95, 26)
        _btnGridView.Cursor = Cursors.Hand
        AddHandler _btnGridView.Click, AddressOf SwitchToGridView
        headerPanel.Controls.Add(_btnGridView)

        _btnTableView = New Button()
        _btnTableView.Text = "📋 Tableau"
        _btnTableView.Font = New Font("Segoe UI", 9, FontStyle.Bold)
        _btnTableView.ForeColor = Color.White
        _btnTableView.BackColor = Color.FromArgb(142, 142, 147)
        _btnTableView.FlatStyle = FlatStyle.Flat
        _btnTableView.FlatAppearance.BorderSize = 0
        _btnTableView.Size = New Size(105, 26)
        _btnTableView.Cursor = Cursors.Hand
        AddHandler _btnTableView.Click, AddressOf SwitchToTableView
        headerPanel.Controls.Add(_btnTableView)

        ' Positionner à droite et gérer le redimensionnement
        AddHandler headerPanel.Resize, Sub(s, e)
                                           _notificationBadge.Location = New Point(headerPanel.Width - _notificationBadge.Width - 20, 47)
                                           _btnTableView.Location = New Point(headerPanel.Width - _notificationBadge.Width - _btnTableView.Width - 35, 47)
                                           _btnGridView.Location = New Point(headerPanel.Width - _notificationBadge.Width - _btnTableView.Width - _btnGridView.Width - 50, 47)
                                       End Sub
        ' Position initiale à droite
        _notificationBadge.Location = New Point(headerPanel.Width - _notificationBadge.Width - 20, 47)
        _btnTableView.Location = New Point(headerPanel.Width - _notificationBadge.Width - _btnTableView.Width - 35, 47)
        _btnGridView.Location = New Point(headerPanel.Width - _notificationBadge.Width - _btnTableView.Width - _btnGridView.Width - 50, 47)

        Dim filterLabel As New Label()
        filterLabel.Text = "Filtrer :"
        filterLabel.Font = New Font("Segoe UI", 10)
        filterLabel.ForeColor = Color.LightGray
        filterLabel.AutoSize = True
        filterLabel.Location = New Point(300, 48)
        headerPanel.Controls.Add(filterLabel)

        _roomFilterComboBox = New ComboBox()
        _roomFilterComboBox.Font = New Font("Segoe UI", 10)
        _roomFilterComboBox.DropDownStyle = ComboBoxStyle.DropDownList
        _roomFilterComboBox.Location = New Point(360, 45)
        _roomFilterComboBox.Width = 250
        _roomFilterComboBox.Items.Add("Toutes les pièces")
        _roomFilterComboBox.SelectedIndex = 0
        AddHandler _roomFilterComboBox.SelectedIndexChanged, AddressOf RoomFilter_Changed
        headerPanel.Controls.Add(_roomFilterComboBox)

        _splitContainer = New SplitContainer()
        _splitContainer.Dock = DockStyle.Fill
        _splitContainer.Orientation = Orientation.Vertical
        _splitContainer.SplitterDistance = 1200

        _devicesPanel = New FlowLayoutPanel()
        _devicesPanel.Dock = DockStyle.Fill
        _devicesPanel.AutoScroll = True
        _devicesPanel.Padding = New Padding(20)
        _devicesPanel.BackColor = Color.FromArgb(242, 242, 247)
        _devicesPanel.FlowDirection = FlowDirection.LeftToRight
        _devicesPanel.WrapContents = True
        _splitContainer.Panel1.Controls.Add(_devicesPanel)

        ' ✅ NOUVEAU : Vue tableau (cachée par défaut)
        _tableView = New RoomTableView With {
            .Dock = DockStyle.Fill,
            .Visible = False
        }
        _splitContainer.Panel1.Controls.Add(_tableView)

        _resizeTimer = New Timer()
        _resizeTimer.Interval = 150
        AddHandler _resizeTimer.Tick, AddressOf ResizeTimer_Tick

        AddHandler _devicesPanel.Resize, Sub(s, e)
                                             _resizeTimer.Stop()
                                             _resizeTimer.Start()
                                         End Sub

        _splitContainer.Panel2.BackColor = Color.FromArgb(30, 30, 30)

        _debugTextBox = New TextBox()
        _debugTextBox.Location = New Point(0, 50)
        _debugTextBox.Size = New Size(_splitContainer.Panel2.Width, _splitContainer.Panel2.Height - 50)
        _debugTextBox.Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right
        _debugTextBox.Multiline = True
        _debugTextBox.ReadOnly = True
        _debugTextBox.ScrollBars = ScrollBars.Vertical
        _debugTextBox.Font = New Font("Consolas", 9)
        _debugTextBox.BackColor = Color.FromArgb(20, 20, 20)
        _debugTextBox.ForeColor = Color.LightGray
        _debugTextBox.BorderStyle = BorderStyle.FixedSingle
        _splitContainer.Panel2.Controls.Add(_debugTextBox)

        Dim debugTitleLabel As New Label()
        debugTitleLabel.Text = "Console de Debug"
        debugTitleLabel.Font = New Font("Consolas", 10, FontStyle.Bold)
        debugTitleLabel.ForeColor = Color.LightGreen
        debugTitleLabel.AutoSize = True
        debugTitleLabel.Location = New Point(10, 15)
        debugTitleLabel.BackColor = Color.Transparent
        _splitContainer.Panel2.Controls.Add(debugTitleLabel)
        debugTitleLabel.BringToFront()

        _pauseButton = New Button()
        _pauseButton.Text = "⏸ Pause"
        _pauseButton.Font = New Font("Segoe UI", 9, FontStyle.Bold)
        _pauseButton.Size = New Size(100, 30)
        _pauseButton.BackColor = Color.FromArgb(50, 120, 200)
        _pauseButton.ForeColor = Color.White
        _pauseButton.FlatStyle = FlatStyle.Flat
        _pauseButton.FlatAppearance.BorderSize = 0
        _pauseButton.Cursor = Cursors.Hand
        AddHandler _pauseButton.Click, AddressOf PauseButton_Click
        _splitContainer.Panel2.Controls.Add(_pauseButton)
        _pauseButton.BringToFront()

        AddHandler _splitContainer.Panel2.Resize, Sub(s, e)
                                                      _pauseButton.Location = New Point(_splitContainer.Panel2.Width - _pauseButton.Width - 10, 10)
                                                  End Sub
        _pauseButton.Location = New Point(_splitContainer.Panel2.Width - _pauseButton.Width - 10, 10)

        ' Détection du scroll
        AddHandler _devicesPanel.Scroll, Sub(s, e)
                                             _isScrolling = True
                                         End Sub

        AddHandler _devicesPanel.MouseUp, Sub(s, e)
                                              _isScrolling = False
                                          End Sub

        Dim bottomPanel As New Panel()
        bottomPanel.Dock = DockStyle.Bottom
        bottomPanel.Height = 30
        bottomPanel.BackColor = Color.FromArgb(45, 45, 48)

        _statusLabel = New Label()
        _statusLabel.Text = "Système démarré"
        _statusLabel.Font = New Font("Segoe UI", 9)
        _statusLabel.ForeColor = Color.LightGray
        _statusLabel.AutoSize = True
        _statusLabel.Location = New Point(10, 7)
        bottomPanel.Controls.Add(_statusLabel)

        Me.Controls.Add(_splitContainer)
        Me.Controls.Add(bottomPanel)
        Me.Controls.Add(headerPanel)

        LogDebug("=== DÉMARRAGE DU SYSTÈME ===")
    End Sub

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        RedirectConsoleOutput()
        LogDebug("Redirection Console/Debug activée")

        _debugTextBox.SelectionStart = 0
        _debugTextBox.SelectionLength = 0
        _debugTextBox.ScrollToCaret()

        LogDebug("Application prête. Cliquez sur Fichier > Démarrer pour lancer les services.")
        UpdateStatus("Application prête - Cliquez sur Fichier > Démarrer")
    End Sub

    Private Sub RedirectConsoleOutput()
        Try
            Console.OutputEncoding = System.Text.Encoding.UTF8

            Dim writer As New TextBoxWriter(_debugTextBox, Function() _isPaused)
            Console.SetOut(writer)
            Console.SetError(writer)

            Dim outWriter As System.IO.StreamWriter = TryCast(Console.Out, System.IO.StreamWriter)
            If outWriter IsNot Nothing Then
                outWriter.AutoFlush = True
            End If

            Trace.Listeners.Clear()
            Trace.Listeners.Add(New TextBoxTraceListener(_debugTextBox, Function() _isPaused))
            Trace.AutoFlush = True
            Debug.AutoFlush = True
        Catch ex As Exception
            Debug.WriteLine($"Erreur redirection console: {ex.Message}")
        End Try
    End Sub

    Private Sub LogDebug(message As String)
        If _isPaused Then Return

        If Me.InvokeRequired Then
            Me.BeginInvoke(Sub() LogDebug(message))
            Return
        End If

        Try
            Dim timestamp As String = DateTime.Now.ToString("HH:mm:ss.fff")
            Dim logMessage As String = $"[{timestamp}] {message}{Environment.NewLine}"

            If _debugTextBox.Lines.Length > 10000 Then
                Dim lines = _debugTextBox.Lines.Skip(1000).ToArray()
                _debugTextBox.Lines = lines
            End If

            _debugTextBox.AppendText(logMessage)

            If _allowAutoScroll Then
                _debugTextBox.SelectionStart = _debugTextBox.Text.Length
                _debugTextBox.ScrollToCaret()
            End If

            _debugTextBox.Update()
        Catch ex As Exception
            Debug.WriteLine($"Erreur LogDebug: {ex.Message}")
        End Try
    End Sub

    Private Sub PauseButton_Click(sender As Object, e As EventArgs)
        _isPaused = Not _isPaused

        If _isPaused Then
            _pauseButton.Text = "▶ Reprendre"
            _pauseButton.BackColor = Color.FromArgb(200, 80, 50)
            _debugTextBox.BackColor = Color.FromArgb(40, 20, 20)
        Else
            _pauseButton.Text = "⏸ Pause"
            _pauseButton.BackColor = Color.FromArgb(50, 120, 200)
            _debugTextBox.BackColor = Color.FromArgb(20, 20, 20)
        End If
    End Sub

    Private Sub ResizeTimer_Tick(sender As Object, e As EventArgs)
        _resizeTimer.Stop()
        _devicesPanel.SuspendLayout()

        Try
            For Each ctrl As Control In _devicesPanel.Controls
                If TypeOf ctrl Is Panel AndAlso ctrl.Height = 40 Then
                    ctrl.Width = _devicesPanel.ClientSize.Width - _devicesPanel.Padding.Left - _devicesPanel.Padding.Right - 10
                End If
            Next
        Finally
            _devicesPanel.ResumeLayout()
        End Try
    End Sub

    Private Async Sub InitializeServices()
        Try
            LogDebug("Initialisation des services...")

            LogDebug("Chargement des catégories d'appareils...")
            Dim deviceCategories = TuyaDeviceCategories.GetInstance()
            LogDebug($"✅ {deviceCategories.GetAllCategories().Count} catégories chargées")

            _config = TuyaConfig.Load()
            LogDebug($"Configuration chargée: {_config.AccessId}")

            Dim tokenProvider As New TuyaTokenProvider(_config)
            _apiClient = New TuyaApiClient(_config, tokenProvider, AddressOf LogDebug)
            LogDebug("Client API créé")

            LogDebug("=== RÈGLES DE NOTIFICATION CHARGÉES ===")
            LogDebug($"Fichier: {_notificationManager.GetConfigFilePath()}")
            LogDebug($"Total: {_notificationManager.GetRules().Count} règles")
            LogDebug("")

            Try
                LogDebug("🔍 Début de la boucle...")
                Dim ruleIndex As Integer = 0

                For Each rule In _notificationManager.GetRules()
                    ruleIndex += 1
                    LogDebug($"  Traitement règle #{ruleIndex}")

                    Dim opSymbol As String = _notificationManager.GetOperatorSymbol(rule.ComparisonOperator)
                    Dim statusIcon As String = If(rule.IsEnabled, "✅", "❌")

                    LogDebug($"  {statusIcon} {rule.Name}")
                    LogDebug($"      {rule.PropertyCode} {opSymbol} {rule.TriggerValue}")
                Next

                LogDebug("✅ Fin de la boucle")
            Catch ex As Exception
                LogDebug($"❌ ERREUR dans la boucle: {ex.Message}")
                LogDebug($"   StackTrace: {ex.StackTrace}")
            End Try

            LogDebug("=========================================")

            UpdateStatus("Chargement du cache des pièces...")
            LogDebug("Chargement du cache des pièces et logements...")
            Await _apiClient.InitializeRoomsCacheAsync()
            LogDebug("Cache des pièces initialisé")

            UpdateStatus("Chargement de tous les appareils...")
            LogDebug("Chargement de tous les appareils via API...")
            Await LoadAllDevicesInfoAsync()

            UpdateStatus("Récupération des états initiaux...")
            LogDebug("Récupération des états initiaux des appareils...")
            Await LoadInitialDeviceStatesAsync()

            UpdateStatus("Démarrage du serveur...")
            LogDebug($"Cache chargé : {_deviceInfoCache.Count} appareils")
            LogDebug("Démarrage du serveur HTTP...")

            _httpServer = New TuyaHttpServer()
            AddHandler _httpServer.EventReceived, AddressOf OnEventReceived
            _httpServer.Start()
            LogDebug("Serveur HTTP démarré")

            Dim pythonScriptPath As String = _config.GetPythonScriptPath()

            If String.IsNullOrEmpty(pythonScriptPath) Then
                LogDebug("⚠ ATTENTION: tuya_bridge.py introuvable")
                LogDebug($"   Chemin recherché: {_config.PythonScriptPath}")
                If Not String.IsNullOrEmpty(_config.PythonFallbackPath) Then
                    LogDebug($"   Chemin alternatif: {_config.PythonFallbackPath}")
                End If
                UpdateStatus("Serveur démarré - Script Python introuvable")
                MessageBox.Show("Le script Python tuya_bridge.py est introuvable." & Environment.NewLine & Environment.NewLine &
                          "Veuillez configurer le chemin dans Fichier > Paramètres.",
                          "Script Python manquant", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            LogDebug($"Démarrage du pont Python : {pythonScriptPath}")
            _pythonBridge = New PythonBridge(pythonScriptPath)
            _pythonBridge.Start()
            LogDebug("Pont Python démarré")

            UpdateStatus($"Prêt - {_deviceInfoCache.Count} appareils en cache")
            LogDebug("=== SYSTÈME OPÉRATIONNEL - EN ÉCOUTE ===")

            _allowAutoScroll = True

        Catch ex As Exception
            LogDebug($"ERREUR InitializeServices: {ex.Message}")
            LogDebug($"StackTrace: {ex.StackTrace}")
            UpdateStatus($"Erreur: {ex.Message}")
            MessageBox.Show($"Erreur d'initialisation: {ex.Message}{Environment.NewLine}{Environment.NewLine}Détails: {ex.StackTrace}",
                          "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Async Function LoadAllDevicesInfoAsync() As Task
        Try
            LogDebug("Récupération de la liste des appareils...")
            Dim devices As List(Of DeviceInfo) = Await _apiClient.GetAllDevicesAsync()
            LogDebug($"API a retourné {devices.Count} appareils")

            SyncLock _lockObject
                For Each device As DeviceInfo In devices
                    _deviceInfoCache(device.Id) = device
                Next
            End SyncLock

            LogDebug($"✓ {_deviceInfoCache.Count} appareils chargés en cache")

            Me.Invoke(Sub()
                          DisplayDevicesByRoom()
                          PopulateRoomFilter()
                      End Sub)

        Catch ex As Exception
            LogDebug($"ERREUR LoadAllDevicesInfoAsync: {ex.Message}")
        End Try
    End Function

    Private Async Function LoadInitialDeviceStatesAsync() As Task
        Try
            Dim count As Integer = 0
            Dim total As Integer = _deviceCards.Count

            For Each kvp In _deviceCards.ToList()
                Dim deviceId = kvp.Key
                Dim card = kvp.Value

                Try
                    count += 1
                    LogDebug($"  [{count}/{total}] Récupération état de {_deviceInfoCache(deviceId).Name}...")

                    Dim statusJson = Await _apiClient.GetDeviceStatusAsync(deviceId)

                    If statusJson IsNot Nothing AndAlso statusJson("result") IsNot Nothing Then
                        Dim status = statusJson("result")

                        If TypeOf status Is JArray Then
                            For Each item As JToken In CType(status, JArray)
                                Dim code As String = item.SelectToken("code")?.ToString()
                                Dim value As String = item.SelectToken("value")?.ToString()

                                If Not String.IsNullOrEmpty(code) AndAlso value IsNot Nothing Then
                                    If code = "phase_a" OrElse code = "phase_b" OrElse code = "phase_c" Then
                                        Dim valueStr As String = value.ToString()
                                        If valueStr.StartsWith("{") Then
                                            DecodePhaseJSON(card, code, valueStr)
                                        ElseIf valueStr.Contains("=") OrElse valueStr.Length Mod 4 = 0 Then
                                            DecodePhaseData(card, code, valueStr)
                                        Else
                                            card.UpdateProperty(code, valueStr)
                                        End If
                                    Else
                                        card.UpdateProperty(code, value.ToString())
                                    End If
                                End If
                            Next
                        End If

                        card.UpdateTimestamp()
                    End If

                Catch ex As Exception
                    LogDebug($"    Erreur récupération état {deviceId}: {ex.Message}")
                End Try
            Next

            LogDebug($"✓ États initiaux chargés pour {count} appareils")

        Catch ex As Exception
            LogDebug($"ERREUR LoadInitialDeviceStatesAsync: {ex.Message}")
        End Try
    End Function

    Private Sub PopulateRoomFilter()
        Try
            _roomFilterComboBox.Items.Clear()
            _roomFilterComboBox.Items.Add("Toutes les pièces")

            Dim rooms = _deviceInfoCache.Values _
                .Select(Function(d) If(String.IsNullOrEmpty(d.RoomName), "📦 Sans pièce", d.RoomName)) _
                .Distinct() _
                .OrderBy(Function(r) r)

            For Each roomName In rooms
                _roomFilterComboBox.Items.Add(roomName)
            Next

            _roomFilterComboBox.SelectedIndex = 0
        Catch ex As Exception
            LogDebug($"ERREUR PopulateRoomFilter: {ex.Message}")
        End Try
    End Sub

    Private Sub RoomFilter_Changed(sender As Object, e As EventArgs)
        Try
            Dim selectedItem = _roomFilterComboBox.SelectedItem?.ToString()

            If selectedItem = "Toutes les pièces" Then
                _selectedRoomFilter = Nothing
            Else
                _selectedRoomFilter = selectedItem
            End If

            DisplayDevicesByRoom()

            LogDebug($"Filtre appliqué : {If(_selectedRoomFilter, "Toutes les pièces")}")
        Catch ex As Exception
            LogDebug($"ERREUR RoomFilter_Changed: {ex.Message}")
        End Try
    End Sub

    Private Sub DisplayDevicesByRoom()
        Try
            LogDebug("Organisation des appareils par pièce...")
            _devicesPanel.SuspendLayout()
            _devicesPanel.Controls.Clear()
            _roomHeaders.Clear()

            Dim filteredDevices = _deviceInfoCache.Values.AsEnumerable()

            If Not String.IsNullOrEmpty(_selectedRoomFilter) Then
                filteredDevices = filteredDevices.Where(Function(d) If(String.IsNullOrEmpty(d.RoomName), "📦 Sans pièce", d.RoomName) = _selectedRoomFilter)
                LogDebug($"Filtrage sur la pièce : {_selectedRoomFilter}")
            End If

            Dim devicesByRoom = filteredDevices _
                .GroupBy(Function(d) If(String.IsNullOrEmpty(d.RoomName), "📦 Sans pièce", d.RoomName)) _
                .OrderBy(Function(g) g.Key)

            For Each roomGroup In devicesByRoom
                Dim roomName As String = roomGroup.Key

                CreateRoomHeader(roomName, roomGroup.Count())

                For Each device In roomGroup.OrderBy(Function(d) d.Name)
                    If _deviceCards.ContainsKey(device.Id) Then
                        _devicesPanel.Controls.Add(_deviceCards(device.Id))
                    Else
                        CreateDeviceCard(device.Id, device)
                    End If
                Next
            Next

            _devicesPanel.ResumeLayout()
            LogDebug($"✓ Affichage organisé : {devicesByRoom.Count()} pièces, {filteredDevices.Count()} appareils")

        Catch ex As Exception
            LogDebug($"ERREUR DisplayDevicesByRoom: {ex.Message}")
        Finally
            _devicesPanel.ResumeLayout()
        End Try
    End Sub

    Private Sub CreateRoomHeader(roomName As String, deviceCount As Integer)
        Try
            Dim header As New Panel()
            header.Width = _devicesPanel.ClientSize.Width - _devicesPanel.Padding.Left - _devicesPanel.Padding.Right - 10
            header.Height = 40
            header.BackColor = Color.FromArgb(60, 60, 65)
            header.Margin = New Padding(0, 15, 0, 5)

            Dim label As New Label()
            label.Text = $"{roomName} ({deviceCount})"
            label.Font = New Font("Segoe UI", 12, FontStyle.Bold)
            label.ForeColor = Color.White
            label.Dock = DockStyle.Fill
            label.TextAlign = ContentAlignment.MiddleLeft
            label.Padding = New Padding(10, 0, 0, 0)

            header.Controls.Add(label)
            _devicesPanel.Controls.Add(header)

            _devicesPanel.SetFlowBreak(header, True)

            _roomHeaders(roomName) = header

        Catch ex As Exception
            LogDebug($"ERREUR CreateRoomHeader: {ex.Message}")
        End Try
    End Sub

    Private Sub OnEventReceived(eventData As String)
        If Me.InvokeRequired Then
            Me.BeginInvoke(Sub() OnEventReceived(eventData))
            Return
        End If

        Try
            LogDebug("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")
            LogDebug("TRAME BRUTE REÇUE:")
            LogDebug(eventData)

            Dim json As JObject = JObject.Parse(eventData)
            Dim devId As String = json.SelectToken("devId")?.ToString()
            Dim status As JToken = json.SelectToken("status")
            Dim bizCode As String = json.SelectToken("bizCode")?.ToString()

            If String.IsNullOrEmpty(devId) Then
                LogDebug("⚠ devId vide, événement ignoré")
                Return
            End If

            _eventCount += 1
            _eventCountLabel.Text = $"Événements: {_eventCount}"

            LogDebug($"DevID: {devId}")
            If Not String.IsNullOrEmpty(bizCode) Then
                LogDebug($"BizCode: {bizCode}")
            End If

            SyncLock _lockObject
                If Not _deviceCards.ContainsKey(devId) Then
                    If _deviceInfoCache.ContainsKey(devId) Then
                        Dim deviceInfo = _deviceInfoCache(devId)
                        Dim deviceRoomName = If(String.IsNullOrEmpty(deviceInfo.RoomName), "📦 Sans pièce", deviceInfo.RoomName)

                        If Not String.IsNullOrEmpty(_selectedRoomFilter) AndAlso deviceRoomName <> _selectedRoomFilter Then
                            LogDebug($"⚠ Appareil {deviceInfo.Name} filtré - pièce: {deviceRoomName}")
                            Return
                        End If

                        LogDebug($"Création tuile depuis cache: {deviceInfo.Name}")
                        CreateDeviceCard(devId, deviceInfo)
                    Else
                        LogDebug($"⚠ Appareil non en cache, chargement API...")
                        LoadDeviceAsync(devId)
                        Return
                    End If
                End If
            End SyncLock

            If _deviceCards.ContainsKey(devId) Then
                If status IsNot Nothing Then
                    LogDebug("Propriétés:")

                    Dim statusDict As New Dictionary(Of String, Object)

                    For Each item As JToken In status
                        Dim code As String = item.SelectToken("code")?.ToString()
                        Dim value As String = item.SelectToken("value")?.ToString()

                        If Not String.IsNullOrEmpty(code) AndAlso value IsNot Nothing Then
                            LogDebug($"  {code} = {value}")

                            statusDict(code) = value

                            If code = "phase_a" OrElse code = "phase_b" OrElse code = "phase_c" Then
                                Dim valueStr As String = value.ToString()
                                If valueStr.StartsWith("{") Then
                                    DecodePhaseJSON(_deviceCards(devId), code, valueStr)
                                ElseIf valueStr.Contains("=") OrElse valueStr.Length Mod 4 = 0 Then
                                    DecodePhaseData(_deviceCards(devId), code, valueStr)
                                Else
                                    _deviceCards(devId).UpdateProperty(code, valueStr)
                                End If
                            Else
                                _deviceCards(devId).UpdateProperty(code, value.ToString())
                            End If
                        End If
                    Next

                    If _deviceInfoCache.ContainsKey(devId) AndAlso statusDict.Count > 0 Then
                        Dim deviceToCheck = New With {
                        .Name = _deviceInfoCache(devId).Name,
                        .Category = _deviceInfoCache(devId).Category,
                        .Status = statusDict
                    }
                        _notificationManager.CheckDevice(deviceToCheck)
                    End If
                End If

                If Not String.IsNullOrEmpty(bizCode) Then
                    _deviceCards(devId).UpdateStatus(bizCode)
                End If
                _deviceCards(devId).UpdateTimestamp()
            End If

            UpdateStatus($"Événement #{_eventCount}: {devId}")
        Catch ex As Exception
            LogDebug($"ERREUR OnEventReceived: {ex.Message}")
            LogDebug($"StackTrace: {ex.StackTrace}")
            UpdateStatus($"Erreur: {ex.Message}")
        End Try
    End Sub

    Private Async Sub LoadDeviceAsync(devId As String)
        Try
            Dim deviceInfo As DeviceInfo = Await _apiClient.GetDeviceInfoAsync(devId)
            If deviceInfo IsNot Nothing Then
                SyncLock _lockObject
                    _deviceInfoCache(devId) = deviceInfo
                    If Me.InvokeRequired Then
                        Me.BeginInvoke(Sub() CreateDeviceCardDynamic(devId, deviceInfo))
                    Else
                        CreateDeviceCardDynamic(devId, deviceInfo)
                    End If
                End SyncLock
                LogDebug($"✓ Appareil chargé: {deviceInfo.Name}")
            Else
                LogDebug($"✗ Échec chargement {devId}")
            End If
        Catch ex As Exception
            LogDebug($"✗ Erreur chargement {devId}: {ex.Message}")
        End Try
    End Sub

    Private Sub DecodePhaseJSON(card As DeviceCard, phase As String, jsonData As String)
        Try
            LogDebug($"  Décodage {phase} (JSON):")
            Dim phaseObj As JObject = JObject.Parse(jsonData)
            Dim voltage As Double = 0
            Dim current As Double = 0
            Dim power As Double = 0

            If phaseObj("voltage") IsNot Nothing Then
                voltage = phaseObj("voltage").Value(Of Double)()
            ElseIf phaseObj("volt") IsNot Nothing Then
                voltage = phaseObj("volt").Value(Of Double)()
            End If

            If phaseObj("electricCurrent") IsNot Nothing Then
                current = phaseObj("electricCurrent").Value(Of Double)()
            ElseIf phaseObj("current") IsNot Nothing Then
                current = phaseObj("current").Value(Of Double)()
            End If

            If phaseObj("power") IsNot Nothing Then
                power = phaseObj("power").Value(Of Double)()
            End If

            If voltage > 0 Then
                card.UpdateProperty($"{phase}_V", voltage.ToString("F1"))
            End If
            If current > 0 Then
                card.UpdateProperty($"{phase}_I", current.ToString("F3"))
            End If
            If power >= 0 Then
                If power < 10 Then power = power * 1000
                card.UpdateProperty($"{phase}_P", power.ToString("F0"))
            End If

            LogDebug($"    → V={voltage:F1}V, I={current:F3}A, P={power:F0}W")
        Catch ex As Exception
            LogDebug($"  ERREUR décodage JSON: {ex.Message}")
        End Try
    End Sub

    Private Sub DecodePhaseData(card As DeviceCard, phase As String, base64Data As String)
        Try
            Dim bytes() As Byte = Convert.FromBase64String(base64Data)
            Dim hexStr As String = String.Join(" ", bytes.Select(Function(b) b.ToString("X2")))
            LogDebug($"  Décodage {phase} (base64): {hexStr}")

            If bytes.Length >= 8 Then
                Dim voltage As Integer = (CInt(bytes(0)) << 8) Or bytes(1)
                Dim current As Integer = (CInt(bytes(2)) << 8) Or bytes(3)
                Dim power As Integer = bytes(4)

                card.UpdateProperty($"{phase}_V", (voltage / 10.0).ToString("F1"))
                card.UpdateProperty($"{phase}_I", (current / 1000.0).ToString("F3"))
                card.UpdateProperty($"{phase}_P", power.ToString())

                LogDebug($"    → V={voltage / 10.0:F1}V, I={current / 1000.0:F3}A, P={power}W")
            ElseIf bytes.Length >= 6 Then
                Dim voltage As Integer = (CInt(bytes(0)) << 8) Or bytes(1)
                Dim current As Integer = (CInt(bytes(2)) << 8) Or bytes(3)
                Dim power As Integer = (CInt(bytes(4)) << 8) Or bytes(5)

                If voltage >= 2000 AndAlso voltage <= 2500 Then
                    card.UpdateProperty($"{phase}_V", (voltage / 10.0).ToString("F1"))
                    card.UpdateProperty($"{phase}_I", (current / 1000.0).ToString("F3"))
                    card.UpdateProperty($"{phase}_P", power.ToString())
                    LogDebug($"    → V={voltage / 10.0:F1}V, I={current / 1000.0:F3}A, P={power}W")
                Else
                    LogDebug($"    ⚠ Valeurs hors plage: V={voltage}")
                End If
            Else
                LogDebug($"    ⚠ Longueur de données insuffisante: {bytes.Length} bytes")
            End If
        Catch ex As Exception
            LogDebug($"  ERREUR décodage base64: {ex.Message}")
        End Try
    End Sub

    Private Sub CreateDeviceCard(devId As String, deviceInfo As DeviceInfo)
        Try
            Dim card As New DeviceCard(devId)
            card.SetApiClient(_apiClient)
            card.UpdateDeviceInfo(deviceInfo)
            _deviceCards(devId) = card
            _devicesPanel.Controls.Add(card)
        Catch ex As Exception
            LogDebug($"ERREUR CreateDeviceCard: {ex.Message}")
        End Try
    End Sub

    Private Sub CreateDeviceCardDynamic(devId As String, deviceInfo As DeviceInfo)
        Dim card As DeviceCard = Nothing

        Try
            Dim deviceRoomName = If(String.IsNullOrEmpty(deviceInfo.RoomName), "📦 Sans pièce", deviceInfo.RoomName)
            If Not String.IsNullOrEmpty(_selectedRoomFilter) AndAlso deviceRoomName <> _selectedRoomFilter Then
                LogDebug($"Appareil {deviceInfo.Name} filtré (pièce: {deviceRoomName})")
                Return
            End If

            card = New DeviceCard(devId)
            card.SetApiClient(_apiClient)
            card.UpdateDeviceInfo(deviceInfo)
            _deviceCards(devId) = card

            Dim roomName As String = If(String.IsNullOrEmpty(deviceInfo.RoomName), "📦 Sans pièce", deviceInfo.RoomName)

            If _roomHeaders.ContainsKey(roomName) Then
                Dim headerIndex As Integer = _devicesPanel.Controls.IndexOf(_roomHeaders(roomName))

                If headerIndex >= 0 Then
                    Dim insertIndex As Integer = headerIndex + 1
                    While insertIndex < _devicesPanel.Controls.Count
                        Dim ctrl As Control = _devicesPanel.Controls(insertIndex)
                        If TypeOf ctrl Is Panel AndAlso Not TypeOf ctrl Is DeviceCard Then
                            Dim isRoomHeader As Boolean = False
                            For Each kvp In _roomHeaders
                                If kvp.Value Is ctrl Then
                                    isRoomHeader = True
                                    Exit For
                                End If
                            Next

                            If isRoomHeader Then
                                Exit While
                            End If
                        End If
                        insertIndex += 1
                    End While

                    _devicesPanel.Controls.Add(card)
                    _devicesPanel.Controls.SetChildIndex(card, insertIndex)
                    Exit Sub
                End If
            Else
                CreateRoomHeader(roomName, 1)
                _devicesPanel.Controls.Add(card)
                Exit Sub
            End If

        Catch ex As Exception
            LogDebug($"ERREUR CreateDeviceCardDynamic: {ex.Message}")
            If card IsNot Nothing Then
                _devicesPanel.Controls.Add(card)
            End If
        End Try
    End Sub

    Private Sub UpdateStatus(message As String)
        If Me.InvokeRequired Then
            Me.BeginInvoke(Sub() UpdateStatus(message))
            Return
        End If
        _statusLabel.Text = message
    End Sub

    Private Sub SettingsMenuItem_Click(sender As Object, e As EventArgs)
        Try
            Dim config As TuyaConfig = TuyaConfig.Load()

            Using settingsForm As New SettingsForm(config)
                If settingsForm.ShowDialog() = DialogResult.OK Then
                    Dim result = MessageBox.Show(
                    "La configuration a été enregistrée." & Environment.NewLine & Environment.NewLine &
                    "Voulez-vous redémarrer l'application maintenant pour appliquer les changements ?",
                    "Redémarrage nécessaire",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question)

                    If result = DialogResult.Yes Then
                        Application.Restart()
                    End If
                End If
            End Using
        Catch ex As Exception
            MessageBox.Show($"Erreur lors de l'ouverture des paramètres :{Environment.NewLine}{ex.Message}",
                      "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub RefreshMenuItem_Click(sender As Object, e As EventArgs)
        Try
            LogDebug("=== ACTUALISATION MANUELLE ===")
            DisplayDevicesByRoom()
            LogDebug("Affichage actualisé")
        Catch ex As Exception
            LogDebug($"ERREUR Refresh: {ex.Message}")
        End Try
    End Sub

    Private Sub AboutMenuItem_Click(sender As Object, e As EventArgs)
        MessageBox.Show(
        "TuyaRealtimeVB" & Environment.NewLine &
        "Version 1.0" & Environment.NewLine & Environment.NewLine &
        "Application de monitoring en temps réel pour appareils Tuya" & Environment.NewLine & Environment.NewLine &
        "© 2025",
        "À propos",
        MessageBoxButtons.OK,
        MessageBoxIcon.Information)
    End Sub

    Private Sub StartMenuItem_Click(sender As Object, e As EventArgs)
        If _isRunning Then
            MessageBox.Show("L'application est déjà démarrée.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Try
            LogDebug("=== DÉMARRAGE MANUEL DES SERVICES ===")
            _startMenuItem.Enabled = False
            _stopMenuItem.Enabled = True
            _isRunning = True

            InitializeServices()

        Catch ex As Exception
            LogDebug($"ERREUR lors du démarrage : {ex.Message}")
            MessageBox.Show($"Erreur lors du démarrage : {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
            _startMenuItem.Enabled = True
            _stopMenuItem.Enabled = False
            _isRunning = False
        End Try
    End Sub

    Private Sub StopMenuItem_Click(sender As Object, e As EventArgs)
        If Not _isRunning Then
            MessageBox.Show("L'application n'est pas démarrée.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim result = MessageBox.Show(
        "Êtes-vous sûr de vouloir arrêter les services ?" & Environment.NewLine & Environment.NewLine &
        "Les événements en temps réel ne seront plus reçus.",
        "Confirmation",
        MessageBoxButtons.YesNo,
        MessageBoxIcon.Question)

        If result = DialogResult.Yes Then
            Try
                LogDebug("=== ARRÊT MANUEL DES SERVICES ===")
                StopServices()

                _startMenuItem.Enabled = True
                _stopMenuItem.Enabled = False
                _isRunning = False

                UpdateStatus("Services arrêtés - Cliquez sur Fichier > Démarrer pour redémarrer")
                LogDebug("Services arrêtés avec succès")

            Catch ex As Exception
                LogDebug($"ERREUR lors de l'arrêt : {ex.Message}")
                MessageBox.Show($"Erreur lors de l'arrêt : {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End If
    End Sub

    Private Sub StopServices()
        Try
            LogDebug("Arrêt du serveur HTTP...")
            If _httpServer IsNot Nothing Then
                RemoveHandler _httpServer.EventReceived, AddressOf OnEventReceived
                _httpServer.Stop()
                _httpServer = Nothing
            End If

            LogDebug("Arrêt du pont Python...")
            If _pythonBridge IsNot Nothing Then
                _pythonBridge.Stop()
                _pythonBridge = Nothing
            End If

        Catch ex As Exception
            LogDebug($"Erreur lors de l'arrêt des services : {ex.Message}")
            Throw
        End Try
    End Sub

    Private Sub ToggleDebugMenuItem_Click(sender As Object, e As EventArgs)
        Try
            If _splitContainer.Panel2Collapsed Then
                _splitContainer.Panel2Collapsed = False
                _toggleDebugMenuItem.Text = "👁 Masquer la console debug"
                _toggleDebugMenuItem.Checked = True
                LogDebug("Console debug affichée")
            Else
                _splitContainer.Panel2Collapsed = True
                _toggleDebugMenuItem.Text = "👁 Afficher la console debug"
                _toggleDebugMenuItem.Checked = False
            End If

            _devicesPanel.SuspendLayout()

            For Each ctrl As Control In _devicesPanel.Controls
                If TypeOf ctrl Is Panel AndAlso ctrl.Height = 40 Then
                    ctrl.Width = _devicesPanel.ClientSize.Width - _devicesPanel.Padding.Left - _devicesPanel.Padding.Right - 10
                End If
            Next

            _devicesPanel.ResumeLayout(True)
            _devicesPanel.PerformLayout()

        Catch ex As Exception
            MessageBox.Show($"Erreur : {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub NotificationBadge_Click(sender As Object, e As EventArgs)
        Try
            If _notificationsPopup Is Nothing OrElse _notificationsPopup.IsDisposed Then
                _notificationsPopup = New NotificationsPopup(_notificationManager)
            End If

            Dim badgeLocation = _notificationBadge.PointToScreen(Point.Empty)
            _notificationsPopup.ShowAtLocation(
            badgeLocation.X - 365,
            badgeLocation.Y + _notificationBadge.Height + 5
        )

            UpdateNotificationBadge()

        Catch ex As Exception
            LogDebug($"Erreur NotificationBadge_Click: {ex.Message}")
        End Try
    End Sub

    Private Sub OnNotificationAdded(sender As Object, notification As NotificationEntry)
        UpdateNotificationBadge()

        If notification.Type = NotificationType.Critical Then
            FlashNotificationBadge()
        End If
    End Sub

    Private Sub UpdateNotificationBadge()
        If Me.InvokeRequired Then
            Me.BeginInvoke(Sub() UpdateNotificationBadge())
            Return
        End If

        Dim unreadCount = _notificationManager.GetUnreadCount()

        If unreadCount = 0 Then
            _notificationBadge.Text = "🔔"
            _notificationBadge.BackColor = Color.FromArgb(55, 55, 58)
        Else
            _notificationBadge.Text = $"🔔 ({unreadCount})"
            _notificationBadge.BackColor = Color.FromArgb(255, 59, 48)
        End If
    End Sub

    Private Sub FlashNotificationBadge()
        Dim flashTimer As New Timer With {.Interval = 300}
        Dim flashCount As Integer = 0

        AddHandler flashTimer.Tick, Sub(s, e)
                                        flashCount += 1

                                        If flashCount Mod 2 = 0 Then
                                            _notificationBadge.BackColor = Color.FromArgb(255, 59, 48)
                                        Else
                                            _notificationBadge.BackColor = Color.FromArgb(255, 149, 0)
                                        End If

                                        If flashCount >= 6 Then
                                            flashTimer.Stop()
                                            UpdateNotificationBadge()
                                        End If
                                    End Sub

        flashTimer.Start()
    End Sub

    Private Sub NotificationsMenuItem_Click(sender As Object, e As EventArgs)
        Try
            If _notificationManager Is Nothing Then
                MessageBox.Show(
                "Le gestionnaire de notifications n'est pas encore démarré." & Environment.NewLine & Environment.NewLine &
                "Veuillez d'abord démarrer l'application (Fichier > Démarrer).",
                "Information",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information)
                Return
            End If

            Using settingsForm As New NotificationSettingsForm(_notificationManager)
                settingsForm.ShowDialog()
            End Using
        Catch ex As Exception
            LogDebug($"Erreur ouverture paramètres notifications: {ex.Message}")
            MessageBox.Show($"Erreur : {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' ✅ NOUVEAU : Basculer vers la vue Tuiles (grille)
    Private Sub SwitchToGridView(sender As Object, e As EventArgs)
        _currentView = "grid"
        _devicesPanel.Visible = True
        _devicesPanel.BringToFront()
        _tableView.Visible = False

        _btnGridView.BackColor = Color.FromArgb(0, 122, 255)
        _btnTableView.BackColor = Color.FromArgb(142, 142, 147)

        LogDebug("Vue Tuiles activée")
        UpdateStatus("Vue Tuiles")
    End Sub

    ' ✅ NOUVEAU : Basculer vers la vue Tableau
    Private Async Sub SwitchToTableView(sender As Object, e As EventArgs)
        Try
            _currentView = "table"

            UpdateStatus("Préparation du tableau...")
            LogDebug("Début construction tableau...")

            ' Désactiver les boutons pendant le chargement
            _btnTableView.Enabled = False
            _btnGridView.Enabled = False

            ' Construire le tableau en arrière-plan
            Await Task.Run(Sub()
                               ' Reconstruire le tableau avec les appareils actuels
                               For Each roomGroup In _deviceInfoCache.Values.GroupBy(Function(d) If(String.IsNullOrEmpty(d.RoomName), "📦 Sans pièce", d.RoomName))
                                   Dim roomName = roomGroup.Key
                                   For Each device In roomGroup
                                       If _deviceCards.ContainsKey(device.Id) Then
                                           _tableView.AddDevice(roomName, _deviceCards(device.Id))
                                       End If
                                   Next
                               Next
                           End Sub)

            ' Retour sur le thread UI pour BuildTable
            _tableView.BuildTable()

            _tableView.Visible = True
            _tableView.BringToFront()
            _devicesPanel.Visible = False

            _btnTableView.BackColor = Color.FromArgb(0, 122, 255)
            _btnGridView.BackColor = Color.FromArgb(142, 142, 147)

            ' Réactiver les boutons
            _btnTableView.Enabled = True
            _btnGridView.Enabled = True

            Dim roomCount = _deviceInfoCache.Values.Select(Function(d) If(String.IsNullOrEmpty(d.RoomName), "📦 Sans pièce", d.RoomName)).Distinct().Count()
            LogDebug($"Vue Tableau activée - {roomCount} pièces affichées")
            UpdateStatus($"Vue Tableau - {_deviceInfoCache.Count} appareils")

        Catch ex As Exception
            LogDebug($"ERREUR SwitchToTableView: {ex.Message}")
            UpdateStatus($"Erreur: {ex.Message}")

            ' Réactiver les boutons en cas d'erreur
            _btnTableView.Enabled = True
            _btnGridView.Enabled = True

            ' Revenir à la vue grille
            SwitchToGridView(Nothing, Nothing)
        End Try
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        MyBase.OnFormClosing(e)
        Try
            LogDebug("=== FERMETURE DE L'APPLICATION ===")

            If _isRunning Then
                StopServices()
            End If

            If _pauseButton IsNot Nothing Then
                RemoveHandler _pauseButton.Click, AddressOf PauseButton_Click
            End If

            If _roomFilterComboBox IsNot Nothing Then
                RemoveHandler _roomFilterComboBox.SelectedIndexChanged, AddressOf RoomFilter_Changed
            End If

            If _resizeTimer IsNot Nothing Then
                _resizeTimer.Stop()
                _resizeTimer.Dispose()
            End If

            LogDebug("Application fermée proprement")
        Catch ex As Exception
            Debug.WriteLine($"Erreur lors de la fermeture: {ex.Message}")
        End Try
    End Sub

End Class