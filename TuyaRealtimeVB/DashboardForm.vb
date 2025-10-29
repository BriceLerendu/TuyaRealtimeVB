Imports System.Drawing
Imports System.Windows.Forms
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports System.IO
Imports System.Text
Imports System.Diagnostics
Imports System.Linq
Imports System.Collections.Concurrent

Public Class DashboardForm
    Inherits Form

#Region "Constantes"
    Private Const MAX_DEBUG_LINES As Integer = 10000
    Private Const LINES_TO_REMOVE As Integer = 1000
    Private Const RESIZE_TIMER_INTERVAL As Integer = 150
    Private Const FLASH_TIMER_INTERVAL As Integer = 300
    Private Const FLASH_COUNT As Integer = 6

    ' ✅ PHASE 6 - Virtualisation optimisée: Rendu progressif adaptatif
    ' Testé et optimisé pour 380+ appareils
    Private Const PROGRESSIVE_RENDER_THRESHOLD As Integer = 50  ' Abaissé de 100 à 50
    Private Const PROGRESSIVE_RENDER_BATCH_SMALL As Integer = 10   ' < 100 appareils
    Private Const PROGRESSIVE_RENDER_BATCH_MEDIUM As Integer = 20  ' 100-300 appareils
    Private Const PROGRESSIVE_RENDER_BATCH_LARGE As Integer = 30   ' > 300 appareils (380 dans votre cas)
    Private Const PROGRESSIVE_RENDER_DELAY_SMALL As Integer = 30   ' < 100 appareils
    Private Const PROGRESSIVE_RENDER_DELAY_MEDIUM As Integer = 50  ' 100-300 appareils
    Private Const PROGRESSIVE_RENDER_DELAY_LARGE As Integer = 80   ' > 300 appareils

    ' Couleurs thématiques
    Private Shared ReadOnly DarkBg As Color = Color.FromArgb(45, 45, 48)
    Private Shared ReadOnly LightBg As Color = Color.FromArgb(242, 242, 247)
    Private Shared ReadOnly SecondaryBg As Color = Color.FromArgb(55, 55, 58)
    Private Shared ReadOnly ActiveBlue As Color = Color.FromArgb(0, 122, 255)
    Private Shared ReadOnly InactiveGray As Color = Color.FromArgb(142, 142, 147)
    Private Shared ReadOnly CriticalRed As Color = Color.FromArgb(255, 59, 48)
    Private Shared ReadOnly SuccessGreen As Color = Color.FromArgb(52, 199, 89)
    Private Shared ReadOnly RoomHeaderBg As Color = Color.FromArgb(60, 60, 65)
    Private Shared ReadOnly DebugConsoleBg As Color = Color.FromArgb(20, 20, 20)

    Private Const ROOM_HEADER_HEIGHT As Integer = 40
    Private Const HEADER_PANEL_HEIGHT As Integer = 80
    Private Const BOTTOM_PANEL_HEIGHT As Integer = 30
#End Region

#Region "Champs privés - Services"
    Private _httpServer As TuyaHttpServer
    Private _realtimeClient As ITuyaRealtimeClient
    Private _apiClient As TuyaApiClient
    Private _config As TuyaConfig
    Private ReadOnly _notificationManager As NotificationManager
#End Region

#Region "Champs privés - Contrôles UI"
    Private _devicesPanel As FlowLayoutPanel
    Private _tableView As RoomTableView
    Private _statusLabel As Label
    Private _eventCountLabel As Label
    Private _debugTextBox As TextBox
    Private _pauseButton As Button
    Private _roomFilterComboBox As ComboBox
    Private _notificationBadge As Button
    Private _splitContainer As SplitContainer
    Private _btnGridView As Button
    Private _btnTableView As Button
    Private _fileContextMenu As ContextMenuStrip
    Private _startMenuItem As ToolStripMenuItem
    Private _stopMenuItem As ToolStripMenuItem
    Private _toggleDebugMenuItem As ToolStripMenuItem
    Private _notificationsPopup As NotificationsPopup
#End Region

#Region "Champs privés - État"
    Private ReadOnly _deviceCards As New ConcurrentDictionary(Of String, DeviceCard)
    Private ReadOnly _deviceInfoCache As New ConcurrentDictionary(Of String, DeviceInfo)
    Private ReadOnly _roomHeaders As New ConcurrentDictionary(Of String, Panel)
    Private _resizeTimer As Timer

    Private _eventCount As Integer = 0
    Private _isPaused As Boolean = False
    Private _allowAutoScroll As Boolean = False
    Private _isScrolling As Boolean = False
    Private _isRunning As Boolean = False
    Private _selectedRoomFilter As String = Nothing
    Private _currentView As ViewMode = ViewMode.Grid

    ' REFACTO: Nouveaux champs pour gérer séparation chargement/temps réel
    Private _dataLoaded As Boolean = False
    Private _realTimeActive As Boolean = False
    Private _realTimePausedCount As Integer = 0
    Private _wasRealTimeActiveBeforePause As Boolean = False

    ' ✅ PHASE 5 - Virtualisation: Rendu progressif
    Private _progressiveRenderCancellation As Threading.CancellationTokenSource
    Private _isProgressiveRendering As Boolean = False

    Private Enum ViewMode
        Grid
        Table
    End Enum
#End Region

#Region "Initialisation"
    Public Sub New()
        InitializeComponent()
        _notificationManager = NotificationManager.Instance
        AddHandler _notificationManager.NotificationAdded, AddressOf OnNotificationAdded
        UpdateNotificationBadge()
    End Sub

    Private Sub InitializeComponent()
        ConfigureForm()

        Dim headerPanel = CreateHeaderPanel()
        Dim bottomPanel = CreateBottomPanel()

        ' CreateSplitContainer assigne _splitContainer en interne avant de créer les contrôles
        Dim split = CreateSplitContainer()

        Me.Controls.Add(split)
        Me.Controls.Add(bottomPanel)
        Me.Controls.Add(headerPanel)

        LogDebug("=== DÉMARRAGE DU SYSTÈME ===")
    End Sub

    Private Sub ConfigureForm()
        Text = "TuyaRealtimeVB - Tableau de bord"
        Size = New Size(1800, 900)
        StartPosition = FormStartPosition.CenterScreen
        BackColor = LightBg
    End Sub

    Private Function CreateHeaderPanel() As Panel
        Dim panel = New Panel With {
            .Dock = DockStyle.Top,
            .Height = HEADER_PANEL_HEIGHT,
            .BackColor = DarkBg,
            .Padding = New Padding(20)
        }

        ' Titre
        panel.Controls.Add(CreateTitleLabel())

        ' Menu fichier
        panel.Controls.Add(CreateFileMenu())

        ' Compteur d'événements
        _eventCountLabel = CreateLabel("Événements: 0", 115, 48, Color.LightGray, 10)
        panel.Controls.Add(_eventCountLabel)

        ' Filtre de pièce
        panel.Controls.AddRange(CreateRoomFilter())

        ' Badge de notifications
        _notificationBadge = CreateNotificationBadge()
        panel.Controls.Add(_notificationBadge)

        ' Boutons de vue
        _btnGridView = CreateViewButton("📊 Tuiles", True, AddressOf SwitchToGridView)
        _btnTableView = CreateViewButton("📋 Tableau", False, AddressOf SwitchToTableView)
        panel.Controls.AddRange({_btnGridView, _btnTableView})

        ' Gérer le positionnement dynamique
        AddHandler panel.Resize, Sub(s, e) PositionRightControls(panel)
        PositionRightControls(panel)

        Return panel
    End Function

    Private Function CreateTitleLabel() As Label
        Return New Label With {
            .Text = "Tableau de bord Tuya - Objets connectés",
            .Font = New Font("Segoe UI", 18, FontStyle.Bold),
            .ForeColor = Color.White,
            .AutoSize = True,
            .Location = New Point(20, 10)
        }
    End Function

    Private Function CreateFileMenu() As Button
        Dim btn = New Button With {
            .Text = "Fichier ▼",
            .Font = New Font("Segoe UI", 9),
            .ForeColor = Color.White,
            .BackColor = SecondaryBg,
            .FlatStyle = FlatStyle.Flat,
            .Size = New Size(80, 26),
            .Location = New Point(20, 47),
            .Cursor = Cursors.Hand
        }
        btn.FlatAppearance.BorderSize = 0

        _fileContextMenu = CreateFileContextMenu()
        AddHandler btn.Click, Sub(s, e) _fileContextMenu.Show(btn, New Point(0, btn.Height))

        Return btn
    End Function

    Private Function CreateFileContextMenu() As ContextMenuStrip
        Dim menu = New ContextMenuStrip With {
            .BackColor = DarkBg,
            .ForeColor = Color.White
        }

        ' Démarrer
        _startMenuItem = New ToolStripMenuItem("▶ Démarrer") With {
            .ForeColor = SuccessGreen,
            .Font = New Font("Segoe UI", 10, FontStyle.Bold)
        }
        AddHandler _startMenuItem.Click, AddressOf StartMenuItem_Click
        menu.Items.Add(_startMenuItem)

        ' Arrêter
        _stopMenuItem = New ToolStripMenuItem("⏹ Arrêter") With {
            .ForeColor = CriticalRed,
            .Font = New Font("Segoe UI", 10, FontStyle.Bold),
            .Enabled = False
        }
        AddHandler _stopMenuItem.Click, AddressOf StopMenuItem_Click
        menu.Items.Add(_stopMenuItem)

        menu.Items.Add(New ToolStripSeparator())

        ' Toggle debug
        _toggleDebugMenuItem = New ToolStripMenuItem("👁 Masquer la console debug") With {
            .ForeColor = Color.White,
            .Checked = True
        }
        AddHandler _toggleDebugMenuItem.Click, AddressOf ToggleDebugMenuItem_Click
        menu.Items.Add(_toggleDebugMenuItem)

        ' Paramètres
        Dim settingsItem = New ToolStripMenuItem("⚙ Paramètres...") With {
            .ShortcutKeyDisplayString = "Ctrl+P",
            .ForeColor = Color.White
        }
        AddHandler settingsItem.Click, AddressOf SettingsMenuItem_Click
        menu.Items.Add(settingsItem)

        ' Notifications
        Dim notifItem = New ToolStripMenuItem("🔔 Gestion des alertes...") With {
            .ForeColor = Color.White
        }
        AddHandler notifItem.Click, AddressOf NotificationsMenuItem_Click
        menu.Items.Add(notifItem)

        menu.Items.Add(New ToolStripSeparator())

        ' Administration des Homes/Rooms/Appareils
        Dim homeAdminMenuItem = New ToolStripMenuItem("🏠 Administration Homes/Pièces/Appareils...") With {
            .ForeColor = Color.White
        }
        AddHandler homeAdminMenuItem.Click, AddressOf HomeAdminMenuItem_Click
        menu.Items.Add(homeAdminMenuItem)

        ' Configuration des catégories
        Dim categoryConfigMenuItem = New ToolStripMenuItem("🏷️ Configuration des catégories...") With {
            .ForeColor = Color.White
        }
        AddHandler categoryConfigMenuItem.Click, AddressOf CategoryConfigMenuItem_Click
        menu.Items.Add(categoryConfigMenuItem)

        ' Automatisations
        Dim automationMenuItem = New ToolStripMenuItem("⚡ Automatisations...") With {
            .ForeColor = Color.White
        }
        AddHandler automationMenuItem.Click, AddressOf AutomationMenuItem_Click
        menu.Items.Add(automationMenuItem)

        menu.Items.Add(New ToolStripSeparator())

        ' Quitter
        Dim exitItem = New ToolStripMenuItem("Quitter") With {
            .ShortcutKeyDisplayString = "Alt+F4",
            .ForeColor = Color.White
        }
        AddHandler exitItem.Click, Sub(s, e) Me.Close()
        menu.Items.Add(exitItem)

        Return menu
    End Function

    Private Function CreateRoomFilter() As Control()
        Dim filterLabel = CreateLabel("Filtrer :", 300, 48, Color.LightGray, 10)

        _roomFilterComboBox = New ComboBox With {
            .Font = New Font("Segoe UI", 10),
            .DropDownStyle = ComboBoxStyle.DropDownList,
            .Location = New Point(360, 45),
            .Width = 250
        }
        _roomFilterComboBox.Items.Add("Toutes les pièces")
        _roomFilterComboBox.SelectedIndex = 0
        AddHandler _roomFilterComboBox.SelectedIndexChanged, AddressOf RoomFilter_Changed

        Return {filterLabel, _roomFilterComboBox}
    End Function

    Private Function CreateNotificationBadge() As Button
        Dim btn = New Button With {
            .Text = "🔔 (0)",
            .Font = New Font("Segoe UI", 10, FontStyle.Bold),
            .ForeColor = Color.White,
            .BackColor = SecondaryBg,
            .FlatStyle = FlatStyle.Flat,
            .Size = New Size(85, 26),
            .Cursor = Cursors.Hand
        }
        btn.FlatAppearance.BorderSize = 0
        AddHandler btn.Click, AddressOf NotificationBadge_Click
        Return btn
    End Function

    Private Function CreateViewButton(text As String, isActive As Boolean, handler As EventHandler) As Button
        Dim btn = New Button With {
            .Text = text,
            .Font = New Font("Segoe UI", 9, FontStyle.Bold),
            .ForeColor = Color.White,
            .BackColor = If(isActive, ActiveBlue, InactiveGray),
            .FlatStyle = FlatStyle.Flat,
            .Size = New Size(If(text.Contains("Tuiles"), 95, 105), 26),
            .Cursor = Cursors.Hand
        }
        btn.FlatAppearance.BorderSize = 0
        AddHandler btn.Click, handler
        Return btn
    End Function

    Private Sub PositionRightControls(panel As Panel)
        Dim rightMargin = 20
        Dim spacing = 15

        _notificationBadge.Location = New Point(
            panel.Width - _notificationBadge.Width - rightMargin, 47)

        _btnTableView.Location = New Point(
            _notificationBadge.Left - _btnTableView.Width - spacing, 47)

        _btnGridView.Location = New Point(
            _btnTableView.Left - _btnGridView.Width - spacing, 47)
    End Sub

    Private Function CreateSplitContainer() As SplitContainer
        Dim split = New SplitContainer With {
            .Dock = DockStyle.Fill,
            .Orientation = Orientation.Vertical,
            .SplitterDistance = 1200
        }

        ' Panel 1: Vues principales
        _devicesPanel = CreateDevicesPanel()
        _tableView = CreateTableView()
        split.Panel1.Controls.AddRange({_devicesPanel, _tableView})

        ' Panel 2: Console debug
        split.Panel2.BackColor = Color.FromArgb(30, 30, 30)

        ' Assigner _splitContainer AVANT de créer les contrôles de debug
        _splitContainer = split

        ' Maintenant on peut créer les contrôles qui dépendent de _splitContainer
        split.Panel2.Controls.AddRange(CreateDebugConsole())

        Return split
    End Function

    Private Function CreateDevicesPanel() As FlowLayoutPanel
        Dim panel = New FlowLayoutPanel With {
            .Dock = DockStyle.Fill,
            .AutoScroll = True,
            .Padding = New Padding(20),
            .BackColor = LightBg,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = True
        }

        _resizeTimer = New Timer With {.Interval = RESIZE_TIMER_INTERVAL}
        AddHandler _resizeTimer.Tick, AddressOf ResizeTimer_Tick
        AddHandler panel.Resize, Sub(s, e)
                                     _resizeTimer.Stop()
                                     _resizeTimer.Start()
                                 End Sub

        AddHandler panel.Scroll, Sub(s, e) _isScrolling = True
        AddHandler panel.MouseUp, Sub(s, e) _isScrolling = False

        Return panel
    End Function

    Private Function CreateTableView() As RoomTableView
        Return New RoomTableView With {
            .Dock = DockStyle.Fill,
            .Visible = False
        }
    End Function

    Private Function CreateDebugConsole() As Control()
        Dim title = New Label With {
            .Text = "Console de Debug",
            .Font = New Font("Consolas", 10, FontStyle.Bold),
            .ForeColor = Color.LightGreen,
            .AutoSize = True,
            .Location = New Point(10, 15),
            .BackColor = Color.Transparent
        }

        _debugTextBox = New TextBox With {
            .Location = New Point(0, 50),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right,
            .Multiline = True,
            .ReadOnly = True,
            .ScrollBars = ScrollBars.Vertical,
            .Font = New Font("Consolas", 9),
            .BackColor = DebugConsoleBg,
            .ForeColor = Color.LightGray,
            .BorderStyle = BorderStyle.FixedSingle
        }

        _pauseButton = New Button With {
            .Text = "⏸ Pause",
            .Font = New Font("Segoe UI", 9, FontStyle.Bold),
            .Size = New Size(100, 30),
            .BackColor = Color.FromArgb(50, 120, 200),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand
        }
        _pauseButton.FlatAppearance.BorderSize = 0
        AddHandler _pauseButton.Click, AddressOf PauseButton_Click

        ' Position dynamique du bouton pause
        AddHandler _splitContainer.Panel2.Resize, Sub(s, e)
                                                      _debugTextBox.Size = New Size(
                                                          _splitContainer.Panel2.Width,
                                                          _splitContainer.Panel2.Height - 50)
                                                      _pauseButton.Location = New Point(
                                                          _splitContainer.Panel2.Width - _pauseButton.Width - 10, 10)
                                                  End Sub

        title.BringToFront()
        _pauseButton.BringToFront()

        Return {_debugTextBox, title, _pauseButton}
    End Function

    Private Function CreateBottomPanel() As Panel
        Dim panel = New Panel With {
            .Dock = DockStyle.Bottom,
            .Height = BOTTOM_PANEL_HEIGHT,
            .BackColor = DarkBg
        }

        _statusLabel = CreateLabel("Système démarré", 10, 7, Color.LightGray, 9)
        panel.Controls.Add(_statusLabel)

        Return panel
    End Function

    Private Function CreateLabel(text As String, x As Integer, y As Integer,
                                 color As Color, fontSize As Integer) As Label
        Return New Label With {
            .Text = text,
            .Font = New Font("Segoe UI", fontSize),
            .ForeColor = color,
            .AutoSize = True,
            .Location = New Point(x, y)
        }
    End Function

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        RedirectConsoleOutput()
        LogDebug("Redirection Console/Debug activée")
        LogDebug("Application prête. Cliquez sur Fichier > Démarrer pour lancer les services.")
        UpdateStatus("Application prête - Cliquez sur Fichier > Démarrer")
    End Sub
#End Region

#Region "Gestion des services"
    Private Async Sub InitializeServices()
        Try
            LogDebug("Initialisation des services...")

            ' Charger les catégories
            Dim deviceCategories = TuyaDeviceCategories.GetInstance()
            LogDebug($"✅ {deviceCategories.GetAllCategories().Count} catégories chargées")

            ' Charger la configuration
            _config = TuyaConfig.Load()
            LogDebug($"Configuration chargée: {_config.AccessId}")

            ' Initialiser l'API client
            Dim tokenProvider As New TuyaTokenProvider(_config)
            _apiClient = New TuyaApiClient(_config, tokenProvider, AddressOf LogDebug)
            LogDebug("Client API créé")

            ' Afficher les règles de notification
            LogNotificationRules()

            ' Charger les données
            UpdateStatus("Chargement du cache des pièces...")
            Await _apiClient.InitializeRoomsCacheAsync()
            LogDebug("Cache des pièces initialisé")

            UpdateStatus("Chargement de tous les appareils...")
            Await LoadAllDevicesInfoAsync()

            UpdateStatus("Récupération des états initiaux...")
            Await LoadInitialDeviceStatesAsync()

            ' Démarrer les services
            UpdateStatus("Démarrage du serveur...")
            StartHttpServer()
            Await StartRealtimeClientAsync()

            UpdateStatus($"Prêt - {_deviceInfoCache.Count} appareils en cache")
            LogDebug("=== SYSTÈME OPÉRATIONNEL - EN ÉCOUTE ===")
            _allowAutoScroll = True

        Catch ex As Exception
            HandleError("Erreur InitializeServices", ex)
        End Try
    End Sub

    Private Sub LogNotificationRules()
        Try
            LogDebug("=== RÈGLES DE NOTIFICATION CHARGÉES ===")
            LogDebug($"Fichier: {_notificationManager.GetConfigFilePath()}")
            LogDebug($"Total: {_notificationManager.GetRules().Count} règles")
            LogDebug("")

            Dim ruleIndex = 0
            For Each rule In _notificationManager.GetRules()
                ruleIndex += 1
                Dim opSymbol = _notificationManager.GetOperatorSymbol(rule.ComparisonOperator)
                Dim statusIcon = If(rule.IsEnabled, "✅", "❌")
                LogDebug($"  {statusIcon} {rule.Name}")
                LogDebug($"      {rule.PropertyCode} {opSymbol} {rule.TriggerValue}")
            Next

            LogDebug("=========================================")
        Catch ex As Exception
            LogDebug($"❌ ERREUR affichage règles: {ex.Message}")
        End Try
    End Sub

    Private Sub HandleMissingPythonScript()
        LogDebug("⚠ ATTENTION: tuya_bridge.py introuvable")
        LogDebug($"   Chemin recherché: {_config.PythonScriptPath}")
        If Not String.IsNullOrEmpty(_config.PythonFallbackPath) Then
            LogDebug($"   Chemin alternatif: {_config.PythonFallbackPath}")
        End If
        UpdateStatus("Serveur démarré - Script Python introuvable")
        MessageBox.Show("Le script Python tuya_bridge.py est introuvable." & Environment.NewLine & Environment.NewLine &
                      "Veuillez configurer le chemin dans Fichier > Paramètres.",
                      "Script Python manquant", MessageBoxButtons.OK, MessageBoxIcon.Warning)
    End Sub

    Private Sub StartHttpServer()
        LogDebug("Démarrage du serveur HTTP...")
        _httpServer = New TuyaHttpServer()
        AddHandler _httpServer.EventReceived, AddressOf OnEventReceived
        _httpServer.Start()
        LogDebug("Serveur HTTP démarré")
    End Sub

    Private Async Function StartRealtimeClientAsync() As Task
        Try
            LogDebug($"=== DÉMARRAGE CLIENT TEMPS RÉEL ===")
            LogDebug($"Mode : {TuyaRealtimeFactory.GetModeName(_config.RealtimeMode)}")

            ' Créer le client via la factory
            _realtimeClient = TuyaRealtimeFactory.CreateClient(_config, AddressOf LogDebug)

            ' Connecter l'événement DeviceStatusChanged (pour SDK Tuya .NET)
            AddHandler _realtimeClient.DeviceStatusChanged, AddressOf OnDeviceStatusChanged

            ' Démarrer le client
            Dim success = Await _realtimeClient.StartAsync()

            If success Then
                LogDebug($"✅ Client temps réel démarré ({_realtimeClient.Mode})")
                _realTimeActive = True
            Else
                LogDebug($"❌ Échec démarrage client temps réel")
                _realTimeActive = False
            End If

        Catch ex As Exception
            LogDebug($"❌ Erreur StartRealtimeClientAsync: {ex.Message}")
            _realTimeActive = False
        End Try
    End Function

    Private Sub StopServices()
        Try
            LogDebug("Arrêt du serveur HTTP...")
            If _httpServer IsNot Nothing Then
                RemoveHandler _httpServer.EventReceived, AddressOf OnEventReceived
                _httpServer.Stop()
                _httpServer = Nothing
            End If

            LogDebug("Arrêt du client temps réel...")
            If _realtimeClient IsNot Nothing Then
                RemoveHandler _realtimeClient.DeviceStatusChanged, AddressOf OnDeviceStatusChanged
                _realtimeClient.Stop()
                _realtimeClient = Nothing
            End If

            _realTimeActive = False
        Catch ex As Exception
            LogDebug($"Erreur lors de l'arrêt des services : {ex.Message}")
            Throw
        End Try
    End Sub

    ''' <summary>
    ''' REFACTO: Charge uniquement les données via API (pas de temps réel)
    ''' Appelé au premier lancement de l'application
    ''' </summary>
    Private Async Function LoadInitialDataAsync() As Task
        Try
            LogDebug("=== CHARGEMENT INITIAL DES DONNÉES ===")

            ' Charger les catégories
            Dim deviceCategories = TuyaDeviceCategories.GetInstance()
            LogDebug($"✅ {deviceCategories.GetAllCategories().Count} catégories chargées")

            ' Charger la configuration
            _config = TuyaConfig.Load()
            LogDebug($"Configuration chargée: {_config.AccessId}")

            ' Initialiser l'API client
            Dim tokenProvider As New TuyaTokenProvider(_config)
            _apiClient = New TuyaApiClient(_config, tokenProvider, AddressOf LogDebug)
            LogDebug("Client API créé")

            ' Afficher les règles de notification
            LogNotificationRules()

            ' Charger les données
            UpdateStatus("Chargement du cache des pièces...")
            Await _apiClient.InitializeRoomsCacheAsync()
            LogDebug("Cache des pièces initialisé")

            UpdateStatus("Chargement de tous les appareils...")
            Await LoadAllDevicesInfoAsync()

            UpdateStatus("Récupération des états initiaux...")
            Await LoadInitialDeviceStatesAsync()

            _dataLoaded = True
            UpdateStatus($"Données chargées - {_deviceInfoCache.Count} appareils")
            LogDebug($"=== DONNÉES CHARGÉES - {_deviceInfoCache.Count} appareils ===")
            LogDebug("Utilisez Fichier > Démarrer pour activer l'écoute temps réel")

        Catch ex As Exception
            HandleError("Erreur chargement initial des données", ex)
            Throw
        End Try
    End Function

    ''' <summary>
    ''' REFACTO: Démarre uniquement les services temps réel (serveur HTTP + Client)
    ''' </summary>
    Private Async Function StartRealTimeServicesAsync() As Task
        Try
            LogDebug("=== DÉMARRAGE SERVICES TEMPS RÉEL ===")

            ' Démarrer les services
            UpdateStatus("Démarrage du serveur...")
            StartHttpServer()
            Await StartRealtimeClientAsync()

            _allowAutoScroll = True
            UpdateStatus("Temps réel actif - En écoute des événements")
            LogDebug("=== TEMPS RÉEL ACTIF ===")

        Catch ex As Exception
            HandleError("Erreur démarrage temps réel", ex)
            Throw
        End Try
    End Function

    ''' <summary>
    ''' REFACTO: Arrête uniquement les services temps réel
    ''' </summary>
    Private Sub StopRealTimeServices()
        Try
            LogDebug("=== ARRÊT SERVICES TEMPS RÉEL ===")
            StopServices()
            _realTimeActive = False
            _allowAutoScroll = False
            UpdateStatus("Temps réel arrêté")
            LogDebug("=== TEMPS RÉEL ARRÊTÉ ===")
        Catch ex As Exception
            LogDebug($"Erreur arrêt temps réel: {ex.Message}")
            Throw
        End Try
    End Sub

    ''' <summary>
    ''' REFACTO: Met en pause le temps réel temporairement
    ''' Utilise un compteur pour gérer les appels imbriqués
    ''' </summary>
    Private Sub PauseRealTime()
        _realTimePausedCount += 1

        ' Première pause : sauvegarder l'état et arrêter si actif
        If _realTimePausedCount = 1 Then
            _wasRealTimeActiveBeforePause = _realTimeActive
            If _realTimeActive Then
                LogDebug("⏸️ PAUSE AUTOMATIQUE du temps réel")
                StopRealTimeServices()
            End If
        End If
    End Sub

    ''' <summary>
    ''' REFACTO: Reprend le temps réel après une pause
    ''' Utilise un compteur pour gérer les appels imbriqués
    ''' </summary>
    Private Sub ResumeRealTime()
        If _realTimePausedCount > 0 Then
            _realTimePausedCount -= 1

            ' Dernière reprise : redémarrer si c'était actif avant
            If _realTimePausedCount = 0 AndAlso _wasRealTimeActiveBeforePause Then
                LogDebug("▶️ REPRISE AUTOMATIQUE du temps réel")
                ' Fire-and-forget avec gestion d'erreur
                Dim resumeTask = Task.Run(
                    Async Function()
                        Try
                            Await StartRealTimeServicesAsync()
                        Catch ex As Exception
                            LogDebug($"❌ Erreur reprise temps réel: {ex.Message}")
                        End Try
                    End Function)
                _wasRealTimeActiveBeforePause = False
            End If
        End If
    End Sub
#End Region

#Region "Gestion des appareils"
    Private Async Function LoadAllDevicesInfoAsync() As Task
        Try
            LogDebug("Récupération de la liste des appareils...")
            Dim devices = Await _apiClient.GetAllDevicesAsync()
            LogDebug($"API a retourné {devices.Count} appareils")

            ' ConcurrentDictionary est thread-safe, pas besoin de lock
            For Each device In devices
                _deviceInfoCache(device.Id) = device
            Next

            LogDebug($"✓ {_deviceInfoCache.Count} appareils chargés en cache")

            ' ✅ FIX: Attendre que toutes les cartes soient créées avant de continuer
            ' Cela garantit que LoadInitialDeviceStatesAsync() trouvera les cartes dans _deviceCards
            Await DisplayDevicesByRoomAsync()
            LogDebug($"✓ {_deviceCards.Count} cartes d'appareils créées et prêtes")

            If InvokeRequired Then
                Invoke(Sub() PopulateRoomFilter())
            Else
                PopulateRoomFilter()
            End If
        Catch ex As Exception
            LogDebug($"ERREUR LoadAllDevicesInfoAsync: {ex.Message}")
        End Try
    End Function

    Private Async Function LoadInitialDeviceStatesAsync() As Task
        Try
            Dim total = _deviceCards.Count
            LogDebug($"=== CHARGEMENT BATCH DES ÉTATS ({total} appareils) ===")

            ' ✅ VALIDATION: Vérifier qu'on a bien des cartes à ce stade
            If total = 0 Then
                LogDebug("⚠️ ATTENTION: Aucune carte trouvée dans _deviceCards!")
                LogDebug($"   _deviceInfoCache contient {_deviceInfoCache.Count} appareils")
                Return
            End If

            ' Récupérer tous les device IDs
            Dim allDeviceIds = _deviceCards.Keys.ToList()

            ' ✅ PHASE 6 - Augmentation batch size de 20 à 50 pour réduire le nombre d'appels API
            Dim batchSize = 50
            Dim batchCount = CInt(Math.Ceiling(allDeviceIds.Count / CDbl(batchSize)))
            Dim processedCount = 0

            For batchIndex = 0 To batchCount - 1
                Dim batchDeviceIds = allDeviceIds.Skip(batchIndex * batchSize).Take(batchSize).ToList()
                Dim batchNumber = batchIndex + 1

                Try
                    LogDebug($"  📦 Batch {batchNumber}/{batchCount}: {batchDeviceIds.Count} appareils...")

                    ' Appel batch API
                    Dim batchResults = Await _apiClient.GetDeviceStatusBatchAsync(batchDeviceIds)

                    ' Traiter les résultats du batch
                    For Each deviceId In batchDeviceIds
                        If batchResults.ContainsKey(deviceId) Then
                            Try
                                If _deviceCards.ContainsKey(deviceId) Then
                                    Dim card = _deviceCards(deviceId)
                                    ProcessDeviceStatus(card, batchResults(deviceId))
                                    card.UpdateTimestamp()
                                    processedCount += 1
                                End If
                            Catch ex As Exception
                                LogDebug($"      Erreur traitement {deviceId}: {ex.Message}")
                            End Try
                        Else
                            LogDebug($"      ⚠️ Status non reçu pour {deviceId}")
                        End If
                    Next

                    UpdateStatus($"Chargement états: {processedCount}/{total}")

                Catch ex As Exception
                    LogDebug($"    ❌ Erreur batch {batchNumber}: {ex.Message}")
                End Try
            Next

            LogDebug($"✅ États initiaux chargés: {processedCount}/{total} appareils ({batchCount} batchs API)")

            ' ✅ FIX: Forcer l'affichage immédiat de toutes les propriétés en attente
            ' Le système de debouncing peut empêcher l'affichage des propriétés au démarrage
            LogDebug("Forçage de l'affichage des propriétés en attente...")
            Dim flushedCount = 0
            For Each kvp In _deviceCards.ToList()
                kvp.Value.FlushPendingUpdates()
                flushedCount += 1
            Next
            LogDebug($"✓ {flushedCount} cartes mises à jour avec leurs propriétés")

        Catch ex As Exception
            LogDebug($"ERREUR LoadInitialDeviceStatesAsync: {ex.Message}")
        End Try
    End Function

    Private Sub ProcessDeviceStatus(card As DeviceCard, status As JToken)
        If Not (TypeOf status Is JArray) Then Return

        For Each item As JToken In CType(status, JArray)
            Dim code = GetJsonString(item, "code")
            Dim value = GetJsonString(item, "value")

            If String.IsNullOrEmpty(code) OrElse value Is Nothing Then Continue For

            If IsPhaseCode(code) Then
                ProcessPhaseData(card, code, value)
            Else
                card.UpdateProperty(code, value)
            End If
        Next
    End Sub

    Private Function IsPhaseCode(code As String) As Boolean
        Return code = "phase_a" OrElse code = "phase_b" OrElse code = "phase_c"
    End Function

    Private Sub ProcessPhaseData(card As DeviceCard, phase As String, value As String)
        ' ✅ SIMPLIFIÉ: Laisser UpdateProperty() gérer automatiquement l'expansion JSON
        ' Si c'est du JSON, ExpandJsonProperty() créera phase_a.voltage, phase_a.electricCurrent, etc.
        If value.StartsWith("{") Then
            card.UpdateProperty(phase, value)
        ElseIf value.Contains("=") OrElse value.Length Mod 4 = 0 Then
            DecodePhaseData(card, phase, value)
        Else
            card.UpdateProperty(phase, value)
        End If
    End Sub

    Private Async Sub LoadDeviceAsync(devId As String)
        Try
            Dim deviceInfo = Await _apiClient.GetDeviceInfoAsync(devId)
            If deviceInfo IsNot Nothing Then
                ' ConcurrentDictionary est thread-safe
                _deviceInfoCache(devId) = deviceInfo
                If InvokeRequired Then
                    BeginInvoke(New Action(Of String, DeviceInfo)(AddressOf CreateDeviceCardDynamic), devId, deviceInfo)
                Else
                    CreateDeviceCardDynamic(devId, deviceInfo)
                End If
                LogDebug($"✓ Appareil chargé: {deviceInfo.Name}")
            Else
                LogDebug($"✗ Échec chargement {devId}")
            End If
        Catch ex As Exception
            LogDebug($"✗ Erreur chargement {devId}: {ex.Message}")
        End Try
    End Sub
#End Region

#Region "Gestion des événements"
    Private Sub OnEventReceived(eventData As String)
        If InvokeRequired Then
            BeginInvoke(New Action(Of String)(AddressOf OnEventReceived), eventData)
            Return
        End If

        ProcessEvent(eventData)
    End Sub

    ''' <summary>
    ''' Gestionnaire pour l'événement DeviceStatusChanged du SDK Tuya .NET
    ''' </summary>
    Private Sub OnDeviceStatusChanged(deviceId As String, statusData As JObject)
        If InvokeRequired Then
            BeginInvoke(New Action(Of String, JObject)(AddressOf OnDeviceStatusChanged), deviceId, statusData)
            Return
        End If

        ' Convertir l'objet JObject en JSON string et appeler ProcessEvent
        ' statusData contient déjà devId, status, dataId, bizCode, etc.
        Dim eventJson = statusData.ToString(Formatting.None)
        ProcessEvent(eventJson)
    End Sub

    Private Sub ProcessEvent(eventData As String)
        Try
            LogDebug("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")
            LogDebug("TRAME BRUTE REÇUE:")
            LogDebug(eventData)

            Dim json = JObject.Parse(eventData)
            Dim devId = GetJsonString(json, "devId")

            If String.IsNullOrEmpty(devId) Then
                LogDebug("⚠ devId vide, événement ignoré")
                Return
            End If

            _eventCount += 1
            _eventCountLabel.Text = $"Événements: {_eventCount}"

            LogDeviceInfo(json, devId)

            ' Créer ou récupérer la carte
            If Not EnsureDeviceCard(devId) Then Return

            ' Traiter les données
            ProcessDeviceEvent(json, devId)

            UpdateStatus($"Événement #{_eventCount}: {devId}")
        Catch ex As Exception
            HandleError("Erreur OnEventReceived", ex)
        End Try
    End Sub

    Private Sub LogDeviceInfo(json As JObject, devId As String)
        LogDebug($"DevID: {devId}")
        Dim bizCode = GetJsonString(json, "bizCode")
        If Not String.IsNullOrEmpty(bizCode) Then
            LogDebug($"BizCode: {bizCode}")
        End If
    End Sub

    Private Function EnsureDeviceCard(devId As String) As Boolean
        ' ConcurrentDictionary est thread-safe
        If _deviceCards.ContainsKey(devId) Then Return True

        If _deviceInfoCache.ContainsKey(devId) Then
            Dim deviceInfo = _deviceInfoCache(devId)
            Dim deviceRoomName = If(String.IsNullOrEmpty(deviceInfo.RoomName), "📦 Sans pièce", deviceInfo.RoomName)

            If Not String.IsNullOrEmpty(_selectedRoomFilter) AndAlso deviceRoomName <> _selectedRoomFilter Then
                LogDebug($"⚠ Appareil {deviceInfo.Name} filtré - pièce: {deviceRoomName}")
                Return False
            End If

            LogDebug($"Création tuile depuis cache: {deviceInfo.Name}")
            CreateDeviceCard(devId, deviceInfo)
            Return True
        Else
            LogDebug($"⚠ Appareil non en cache, chargement API...")
            LoadDeviceAsync(devId)
            Return False
        End If
    End Function

    Private Sub ProcessDeviceEvent(json As JObject, devId As String)
        If Not _deviceCards.ContainsKey(devId) Then Return

        Dim card = _deviceCards(devId)
        Dim status = json.SelectToken("status")
        Dim bizCode = GetJsonString(json, "bizCode")

        If status IsNot Nothing Then
            ProcessEventStatus(card, status, devId)
        End If

        If Not String.IsNullOrEmpty(bizCode) Then
            card.UpdateStatus(bizCode)
        End If

        card.UpdateTimestamp()
    End Sub

    Private Sub ProcessEventStatus(card As DeviceCard, status As JToken, devId As String)
        LogDebug("Propriétés:")
        Dim statusDict As New Dictionary(Of String, Object)

        For Each item As JToken In status
            Dim code = GetJsonString(item, "code")
            Dim value = GetJsonString(item, "value")

            If String.IsNullOrEmpty(code) OrElse value Is Nothing Then Continue For

            LogDebug($"  {code} = {value}")
            statusDict(code) = value

            If IsPhaseCode(code) Then
                ProcessPhaseData(card, code, value)
            Else
                card.UpdateProperty(code, value)
            End If
        Next

        ' Vérifier les règles de notification
        If _deviceInfoCache.ContainsKey(devId) AndAlso statusDict.Count > 0 Then
            CheckNotificationRules(devId, statusDict)
        End If
    End Sub

    Private Sub CheckNotificationRules(devId As String, statusDict As Dictionary(Of String, Object))
        Dim deviceToCheck = New With {
            .Name = _deviceInfoCache(devId).Name,
            .Category = _deviceInfoCache(devId).Category,
            .Status = statusDict
        }
        _notificationManager.CheckDevice(deviceToCheck)
    End Sub
#End Region

#Region "Décodage des données de phase"
    Private Sub DecodePhaseJSON(card As DeviceCard, phase As String, jsonData As String)
        Try
            LogDebug($"  Décodage {phase} (JSON):")
            Dim phaseObj = JObject.Parse(jsonData)

            Dim voltage = GetPhaseValue(phaseObj, {"voltage", "volt"})
            Dim current = GetPhaseValue(phaseObj, {"electricCurrent", "current"})
            Dim power = GetPhaseValue(phaseObj, {"power"})

            UpdatePhaseProperties(card, phase, voltage, current, power)

            LogDebug($"    → V={voltage:F1}V, I={current:F3}A, P={power:F0}W")
        Catch ex As Exception
            LogDebug($"  ERREUR décodage JSON: {ex.Message}")
        End Try
    End Sub

    Private Function GetPhaseValue(obj As JObject, keys As String()) As Double
        For Each key In keys
            If obj(key) IsNot Nothing Then
                Return obj(key).Value(Of Double)()
            End If
        Next
        Return 0
    End Function

    Private Sub UpdatePhaseProperties(card As DeviceCard, phase As String,
                                     voltage As Double, current As Double, power As Double)
        ' ✅ MODIFIÉ: Utiliser notation pointée pour cohérence avec ExpandJsonProperty
        If voltage > 0 Then card.UpdateProperty($"{phase}.voltage", voltage.ToString("F1"))
        If current > 0 Then card.UpdateProperty($"{phase}.electricCurrent", current.ToString("F3"))
        If power >= 0 Then
            If power < 10 Then power *= 1000
            card.UpdateProperty($"{phase}.power", power.ToString("F0"))
        End If
    End Sub

    Private Sub DecodePhaseData(card As DeviceCard, phase As String, base64Data As String)
        Try
            Dim bytes = Convert.FromBase64String(base64Data)
            Dim hexStr = String.Join(" ", bytes.Select(Function(b) b.ToString("X2")))
            LogDebug($"  Décodage {phase} (base64): {hexStr}")

            If bytes.Length >= 8 Then
                DecodePhase8Bytes(card, phase, bytes)
            ElseIf bytes.Length >= 6 Then
                DecodePhase6Bytes(card, phase, bytes)
            Else
                LogDebug($"    ⚠ Longueur de données insuffisante: {bytes.Length} bytes")
            End If
        Catch ex As Exception
            LogDebug($"  ERREUR décodage base64: {ex.Message}")
        End Try
    End Sub

    Private Sub DecodePhase8Bytes(card As DeviceCard, phase As String, bytes As Byte())
        Dim voltage = (CInt(bytes(0)) << 8) Or bytes(1)
        Dim current = (CInt(bytes(2)) << 8) Or bytes(3)
        Dim power = bytes(4)

        ' ✅ MODIFIÉ: Utiliser notation pointée pour cohérence avec ExpandJsonProperty
        card.UpdateProperty($"{phase}.voltage", (voltage / 10.0).ToString("F1"))
        card.UpdateProperty($"{phase}.electricCurrent", (current / 1000.0).ToString("F3"))
        card.UpdateProperty($"{phase}.power", power.ToString())

        LogDebug($"    → V={voltage / 10.0:F1}V, I={current / 1000.0:F3}A, P={power}W")
    End Sub

    Private Sub DecodePhase6Bytes(card As DeviceCard, phase As String, bytes As Byte())
        Dim voltage = (CInt(bytes(0)) << 8) Or bytes(1)
        Dim current = (CInt(bytes(2)) << 8) Or bytes(3)
        Dim power = (CInt(bytes(4)) << 8) Or bytes(5)

        ' ✅ MODIFIÉ: Utiliser notation pointée pour cohérence avec ExpandJsonProperty
        If voltage >= 2000 AndAlso voltage <= 2500 Then
            card.UpdateProperty($"{phase}.voltage", (voltage / 10.0).ToString("F1"))
            card.UpdateProperty($"{phase}.electricCurrent", (current / 1000.0).ToString("F3"))
            card.UpdateProperty($"{phase}.power", power.ToString())
            LogDebug($"    → V={voltage / 10.0:F1}V, I={current / 1000.0:F3}A, P={power}W")
        Else
            LogDebug($"    ⚠ Valeurs hors plage: V={voltage}")
        End If
    End Sub
#End Region

#Region "Gestion de l'affichage"
    ''' <summary>
    ''' ✅ PHASE 5 - Optimisé avec rendu progressif pour 500+ appareils
    ''' </summary>
    Private Async Function DisplayDevicesByRoomAsync() As Task
        Try
            ' Annuler tout rendu progressif en cours
            _progressiveRenderCancellation?.Cancel()
            _progressiveRenderCancellation = New Threading.CancellationTokenSource()

            LogDebug("Organisation des appareils par pièce...")
            _devicesPanel.SuspendLayout()
            _devicesPanel.Controls.Clear()
            _roomHeaders.Clear()

            Dim filteredDevices = ApplyRoomFilter().ToList()
            Dim devicesByRoom = GroupDevicesByRoom(filteredDevices).ToList()
            Dim totalDevices = filteredDevices.Count

            ' ✅ PHASE 6 - Rendu progressif adaptatif activé dès 50 appareils
            If totalDevices >= PROGRESSIVE_RENDER_THRESHOLD Then
                LogDebug($"🔄 Rendu progressif adaptatif activé pour {totalDevices} appareils...")
                Await DisplayDevicesByRoomProgressiveAsync(devicesByRoom, _progressiveRenderCancellation.Token)
            Else
                ' Rendu classique pour petit nombre d'appareils
                For Each roomGroup In devicesByRoom
                    CreateRoomHeader(roomGroup.Key, roomGroup.Count())
                    AddDevicesForRoom(roomGroup)
                Next
                _devicesPanel.ResumeLayout()
                LogDebug($"✓ Affichage organisé : {devicesByRoom.Count} pièces, {totalDevices} appareils")
            End If
        Catch ex As Exception
            LogDebug($"ERREUR DisplayDevicesByRoom: {ex.Message}")
            _devicesPanel.ResumeLayout()
        End Try
    End Function

    ''' <summary>
    ''' ✅ PHASE 6 - Rendu progressif adaptatif par lots pour éviter de bloquer l'UI
    ''' Ajuste automatiquement la taille des lots et les délais selon le nombre d'appareils
    ''' </summary>
    Private Async Function DisplayDevicesByRoomProgressiveAsync(
        devicesByRoom As List(Of IGrouping(Of String, DeviceInfo)),
        cancellationToken As Threading.CancellationToken) As Task

        _isProgressiveRendering = True

        Try
            Dim totalDevices = devicesByRoom.Sum(Function(g) g.Count())
            Dim processedDevices = 0

            ' ✅ PHASE 6 - Paramètres adaptatifs selon le nombre d'appareils
            Dim batchSize As Integer
            Dim delayMs As Integer
            If totalDevices < 100 Then
                batchSize = PROGRESSIVE_RENDER_BATCH_SMALL
                delayMs = PROGRESSIVE_RENDER_DELAY_SMALL
                LogDebug($"  Mode RAPIDE : batch={batchSize}, délai={delayMs}ms")
            ElseIf totalDevices < 300 Then
                batchSize = PROGRESSIVE_RENDER_BATCH_MEDIUM
                delayMs = PROGRESSIVE_RENDER_DELAY_MEDIUM
                LogDebug($"  Mode MOYEN : batch={batchSize}, délai={delayMs}ms")
            Else
                batchSize = PROGRESSIVE_RENDER_BATCH_LARGE
                delayMs = PROGRESSIVE_RENDER_DELAY_LARGE
                LogDebug($"  Mode LARGE : batch={batchSize}, délai={delayMs}ms")
            End If

            For Each roomGroup In devicesByRoom
                If cancellationToken.IsCancellationRequested Then Exit For

                ' Créer l'en-tête de la pièce
                If InvokeRequired Then
                    Invoke(Sub() CreateRoomHeader(roomGroup.Key, roomGroup.Count()))
                Else
                    CreateRoomHeader(roomGroup.Key, roomGroup.Count())
                End If

                ' Ajouter les appareils par lots (taille adaptative)
                Dim devices = roomGroup.OrderBy(Function(d) d.Name).ToList()
                For batchStart = 0 To devices.Count - 1 Step batchSize
                    If cancellationToken.IsCancellationRequested Then Exit For

                    Dim batchEnd = Math.Min(batchStart + batchSize - 1, devices.Count - 1)

                    If InvokeRequired Then
                        Invoke(Sub()
                                   _devicesPanel.SuspendLayout()
                                   For i = batchStart To batchEnd
                                       Dim device = devices(i)
                                       If _deviceCards.ContainsKey(device.Id) Then
                                           _devicesPanel.Controls.Add(_deviceCards(device.Id))
                                       Else
                                           CreateDeviceCard(device.Id, device)
                                       End If
                                   Next
                                   _devicesPanel.ResumeLayout()
                               End Sub)
                    Else
                        _devicesPanel.SuspendLayout()
                        For i = batchStart To batchEnd
                            Dim device = devices(i)
                            If _deviceCards.ContainsKey(device.Id) Then
                                _devicesPanel.Controls.Add(_deviceCards(device.Id))
                            Else
                                CreateDeviceCard(device.Id, device)
                            End If
                        Next
                        _devicesPanel.ResumeLayout()
                    End If

                    processedDevices += (batchEnd - batchStart + 1)
                    UpdateStatus($"Chargement des appareils... {processedDevices}/{totalDevices}")

                    ' Délai adaptatif pour ne pas bloquer l'UI
                    Await Task.Delay(delayMs, cancellationToken)
                Next
            Next

            If Not cancellationToken.IsCancellationRequested Then
                UpdateStatus($"✓ {totalDevices} appareils chargés")
                LogDebug($"✓ Rendu progressif terminé : {devicesByRoom.Count} pièces, {totalDevices} appareils")
            End If

        Catch ex As Threading.Tasks.TaskCanceledException
            LogDebug("Rendu progressif annulé")
        Catch ex As Exception
            LogDebug($"ERREUR DisplayDevicesByRoomProgressiveAsync: {ex.Message}")
        Finally
            _isProgressiveRendering = False
            If InvokeRequired Then
                Invoke(Sub() _devicesPanel.ResumeLayout())
            Else
                _devicesPanel.ResumeLayout()
            End If
        End Try
    End Function

    Private Function ApplyRoomFilter() As IEnumerable(Of DeviceInfo)
        Dim devices = _deviceInfoCache.Values.AsEnumerable()

        If Not String.IsNullOrEmpty(_selectedRoomFilter) Then
            devices = devices.Where(Function(d) GetRoomName(d).Equals(_selectedRoomFilter))
            LogDebug($"Filtrage sur la pièce : {_selectedRoomFilter}")
        End If

        Return devices
    End Function

    Private Function GroupDevicesByRoom(devices As IEnumerable(Of DeviceInfo)) As IOrderedEnumerable(Of IGrouping(Of String, DeviceInfo))
        Return devices.GroupBy(Function(d) GetRoomName(d)).OrderBy(Function(g) g.Key)
    End Function

    Private Function GetRoomName(device As DeviceInfo) As String
        Return If(String.IsNullOrEmpty(device.RoomName), "📦 Sans pièce", device.RoomName)
    End Function

    Private Sub AddDevicesForRoom(roomGroup As IGrouping(Of String, DeviceInfo))
        For Each device In roomGroup.OrderBy(Function(d) d.Name)
            If _deviceCards.ContainsKey(device.Id) Then
                _devicesPanel.Controls.Add(_deviceCards(device.Id))
            Else
                CreateDeviceCard(device.Id, device)
            End If
        Next
    End Sub

    Private Sub CreateRoomHeader(roomName As String, deviceCount As Integer)
        Try
            Dim header = New Panel With {
                .Width = _devicesPanel.ClientSize.Width - _devicesPanel.Padding.Left - _devicesPanel.Padding.Right - 10,
                .Height = ROOM_HEADER_HEIGHT,
                .BackColor = RoomHeaderBg,
                .Margin = New Padding(0, 15, 0, 5)
            }

            Dim label = New Label With {
                .Text = $"{roomName} ({deviceCount})",
                .Font = New Font("Segoe UI", 12, FontStyle.Bold),
                .ForeColor = Color.White,
                .Dock = DockStyle.Fill,
                .TextAlign = ContentAlignment.MiddleLeft,
                .Padding = New Padding(10, 0, 0, 0)
            }

            header.Controls.Add(label)
            _devicesPanel.Controls.Add(header)
            _devicesPanel.SetFlowBreak(header, True)
            _roomHeaders(roomName) = header
        Catch ex As Exception
            LogDebug($"ERREUR CreateRoomHeader: {ex.Message}")
        End Try
    End Sub

    Private Sub CreateDeviceCard(devId As String, deviceInfo As DeviceInfo)
        Try
            Dim card = New DeviceCard(devId, _apiClient, AddressOf LogDebug)
            card.UpdateDeviceInfo(deviceInfo)
            _deviceCards(devId) = card
            _devicesPanel.Controls.Add(card)
        Catch ex As Exception
            LogDebug($"ERREUR CreateDeviceCard: {ex.Message}")
        End Try
    End Sub

    Private Sub CreateDeviceCardDynamic(devId As String, deviceInfo As DeviceInfo)
        Try
            Dim deviceRoomName = GetRoomName(deviceInfo)
            If Not String.IsNullOrEmpty(_selectedRoomFilter) AndAlso deviceRoomName <> _selectedRoomFilter Then
                LogDebug($"Appareil {deviceInfo.Name} filtré (pièce: {deviceRoomName})")
                Return
            End If

            Dim card = New DeviceCard(devId, _apiClient, AddressOf LogDebug)
            card.UpdateDeviceInfo(deviceInfo)
            _deviceCards(devId) = card

            InsertCardIntoRoom(card, deviceRoomName)
        Catch ex As Exception
            LogDebug($"ERREUR CreateDeviceCardDynamic: {ex.Message}")
        End Try
    End Sub

    Private Sub InsertCardIntoRoom(card As DeviceCard, roomName As String)
        If _roomHeaders.ContainsKey(roomName) Then
            Dim headerIndex = _devicesPanel.Controls.IndexOf(_roomHeaders(roomName))
            If headerIndex >= 0 Then
                Dim insertIndex = FindInsertIndex(headerIndex)
                _devicesPanel.Controls.Add(card)
                _devicesPanel.Controls.SetChildIndex(card, insertIndex)
                Return
            End If
        End If

        CreateRoomHeader(roomName, 1)
        _devicesPanel.Controls.Add(card)
    End Sub

    Private Function FindInsertIndex(startIndex As Integer) As Integer
        Dim insertIndex = startIndex + 1

        While insertIndex < _devicesPanel.Controls.Count
            Dim ctrl = _devicesPanel.Controls(insertIndex)
            Dim panel As Panel = TryCast(ctrl, Panel)
            If panel IsNot Nothing AndAlso Not TypeOf ctrl Is DeviceCard Then
                If _roomHeaders.Values.Contains(panel) Then Exit While
            End If
            insertIndex += 1
        End While

        Return insertIndex
    End Function

    Private Sub PopulateRoomFilter()
        Try
            _roomFilterComboBox.Items.Clear()
            _roomFilterComboBox.Items.Add("Toutes les pièces")

            Dim rooms = _deviceInfoCache.Values _
                .Select(Function(d) GetRoomName(d)) _
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

    Private Sub ResizeTimer_Tick(sender As Object, e As EventArgs)
        _resizeTimer.Stop()
        _devicesPanel.SuspendLayout()

        Try
            For Each ctrl As Control In _devicesPanel.Controls
                Dim panel As Panel = TryCast(ctrl, Panel)
                If panel IsNot Nothing AndAlso panel.Height = ROOM_HEADER_HEIGHT Then
                    panel.Width = _devicesPanel.ClientSize.Width - _devicesPanel.Padding.Left - _devicesPanel.Padding.Right - 10
                End If
            Next
        Finally
            _devicesPanel.ResumeLayout()
        End Try
    End Sub
#End Region

#Region "Gestion des vues"
    Private Sub SwitchToGridView(sender As Object, e As EventArgs)
        _currentView = ViewMode.Grid
        _devicesPanel.Visible = True
        _devicesPanel.BringToFront()
        _tableView.Visible = False

        UpdateViewButtons()

        LogDebug("Vue Tuiles activée")
        UpdateStatus("Vue Tuiles")
    End Sub

    Private Async Sub SwitchToTableView(sender As Object, e As EventArgs)
        Try
            _currentView = ViewMode.Table
            UpdateStatus("Préparation du tableau...")
            LogDebug("Début construction tableau...")

            SetViewButtonsEnabled(False)

            ' Construire le tableau
            Await Task.Run(Sub() BuildTableData())

            ' Afficher le tableau
            _tableView.BuildTable()
            _tableView.Visible = True
            _tableView.BringToFront()
            _devicesPanel.Visible = False

            UpdateViewButtons()
            SetViewButtonsEnabled(True)

            Dim roomCount = _deviceInfoCache.Values.Select(Function(d) GetRoomName(d)).Distinct().Count()
            LogDebug($"Vue Tableau activée - {roomCount} pièces affichées")
            UpdateStatus($"Vue Tableau - {_deviceInfoCache.Count} appareils")
        Catch ex As Exception
            HandleError("Erreur SwitchToTableView", ex)
            SetViewButtonsEnabled(True)
            SwitchToGridView(Nothing, Nothing)
        End Try
    End Sub

    Private Sub BuildTableData()
        For Each roomGroup In _deviceInfoCache.Values.GroupBy(Function(d) GetRoomName(d))
            Dim roomName = roomGroup.Key
            For Each device In roomGroup
                If _deviceCards.ContainsKey(device.Id) Then
                    _tableView.AddDevice(roomName, _deviceCards(device.Id))
                End If
            Next
        Next
    End Sub

    Private Sub UpdateViewButtons()
        _btnGridView.BackColor = If(_currentView = ViewMode.Grid, ActiveBlue, InactiveGray)
        _btnTableView.BackColor = If(_currentView = ViewMode.Table, ActiveBlue, InactiveGray)
    End Sub

    Private Sub SetViewButtonsEnabled(enabled As Boolean)
        _btnTableView.Enabled = enabled
        _btnGridView.Enabled = enabled
    End Sub
#End Region

#Region "Gestionnaires d'événements UI"
    Private Async Sub RoomFilter_Changed(sender As Object, e As EventArgs)
        Try
            Dim selectedItem = _roomFilterComboBox.SelectedItem?.ToString()
            _selectedRoomFilter = If(selectedItem = "Toutes les pièces", Nothing, selectedItem)
            Await DisplayDevicesByRoomAsync()
            LogDebug($"Filtre appliqué : {If(_selectedRoomFilter, "Toutes les pièces")}")
        Catch ex As Exception
            LogDebug($"ERREUR RoomFilter_Changed: {ex.Message}")
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
            _debugTextBox.BackColor = DebugConsoleBg
        End If
    End Sub

    Private Async Sub StartMenuItem_Click(sender As Object, e As EventArgs)
        Try
            ' REFACTO: Premier lancement => charger les données d'abord
            If Not _dataLoaded Then
                LogDebug("=== PREMIER DÉMARRAGE - CHARGEMENT DES DONNÉES ===")
                _startMenuItem.Enabled = False
                UpdateStatus("Chargement initial des données...")

                Await LoadInitialDataAsync()

                _startMenuItem.Text = "▶ Démarrer le temps réel"
                _startMenuItem.Enabled = True
                Return
            End If

            ' REFACTO: Données déjà chargées => démarrer le temps réel
            If _realTimeActive Then
                MessageBox.Show("Le processus temps réel est déjà actif.", "Information",
                              MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            LogDebug("=== DÉMARRAGE TEMPS RÉEL ===")
            _startMenuItem.Enabled = False
            _stopMenuItem.Enabled = True
            _isRunning = True

            Await StartRealTimeServicesAsync()

            _startMenuItem.Enabled = True

        Catch ex As Exception
            HandleError("Erreur lors du démarrage", ex)
            _startMenuItem.Enabled = True
            _stopMenuItem.Enabled = False
            _isRunning = False
            _realTimeActive = False
        End Try
    End Sub

    Private Sub StopMenuItem_Click(sender As Object, e As EventArgs)
        If Not _realTimeActive Then
            MessageBox.Show("Le processus temps réel n'est pas actif.", "Information",
                          MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim result = MessageBox.Show(
            "Êtes-vous sûr de vouloir arrêter le temps réel ?" & Environment.NewLine & Environment.NewLine &
            "Les événements en temps réel ne seront plus reçus.",
            "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question)

        If result = DialogResult.Yes Then
            Try
                LogDebug("=== ARRÊT MANUEL TEMPS RÉEL ===")
                StopRealTimeServices()
                _startMenuItem.Enabled = True
                _stopMenuItem.Enabled = False
                _isRunning = False
                UpdateStatus("Temps réel arrêté - Cliquez sur Fichier > Démarrer pour redémarrer")
                LogDebug("Temps réel arrêté avec succès")
            Catch ex As Exception
                HandleError("Erreur lors de l'arrêt", ex)
            End Try
        End If
    End Sub

    Private Sub ToggleDebugMenuItem_Click(sender As Object, e As EventArgs)
        Try
            _splitContainer.Panel2Collapsed = Not _splitContainer.Panel2Collapsed

            If _splitContainer.Panel2Collapsed Then
                _toggleDebugMenuItem.Text = "👁 Afficher la console debug"
                _toggleDebugMenuItem.Checked = False
            Else
                _toggleDebugMenuItem.Text = "👁 Masquer la console debug"
                _toggleDebugMenuItem.Checked = True
                LogDebug("Console debug affichée")
            End If

            ' Redimensionner les en-têtes de pièces
            _devicesPanel.SuspendLayout()
            For Each ctrl As Control In _devicesPanel.Controls
                Dim panel As Panel = TryCast(ctrl, Panel)
                If panel IsNot Nothing AndAlso panel.Height = ROOM_HEADER_HEIGHT Then
                    panel.Width = _devicesPanel.ClientSize.Width - _devicesPanel.Padding.Left - _devicesPanel.Padding.Right - 10
                End If
            Next
            _devicesPanel.ResumeLayout(True)
            _devicesPanel.PerformLayout()
        Catch ex As Exception
            MessageBox.Show($"Erreur : {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub SettingsMenuItem_Click(sender As Object, e As EventArgs)
        Try
            Dim config = TuyaConfig.Load()
            Using settingsForm As New SettingsForm(config)
                If settingsForm.ShowDialog() = DialogResult.OK Then
                    Dim result = MessageBox.Show(
                        "La configuration a été enregistrée." & Environment.NewLine & Environment.NewLine &
                        "Voulez-vous redémarrer l'application maintenant pour appliquer les changements ?",
                        "Redémarrage nécessaire", MessageBoxButtons.YesNo, MessageBoxIcon.Question)

                    If result = DialogResult.Yes Then Application.Restart()
                End If
            End Using
        Catch ex As Exception
            MessageBox.Show($"Erreur lors de l'ouverture des paramètres :{Environment.NewLine}{ex.Message}",
                          "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub NotificationsMenuItem_Click(sender As Object, e As EventArgs)
        Try
            If _notificationManager Is Nothing Then
                MessageBox.Show(
                    "Le gestionnaire de notifications n'est pas encore démarré." & Environment.NewLine & Environment.NewLine &
                    "Veuillez d'abord démarrer l'application (Fichier > Démarrer).",
                    "Information", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Using settingsForm As New NotificationSettingsForm(_notificationManager)
                settingsForm.ShowDialog()
            End Using
        Catch ex As Exception
            HandleError("Erreur ouverture paramètres notifications", ex)
        End Try
    End Sub

    Private Sub NotificationBadge_Click(sender As Object, e As EventArgs)
        Try
            If _notificationsPopup Is Nothing OrElse _notificationsPopup.IsDisposed Then
                _notificationsPopup = New NotificationsPopup(_notificationManager)
                AddHandler _notificationsPopup.NotificationsChanged, AddressOf OnNotificationsChanged
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

    Private Sub OnNotificationsChanged(sender As Object, e As EventArgs)
        UpdateNotificationBadge()
    End Sub

    Private Sub OnNotificationAdded(sender As Object, notification As NotificationEntry)
        UpdateNotificationBadge()
        If notification.Type = NotificationType.Critical Then
            FlashNotificationBadge()
        End If
    End Sub

    Private Sub UpdateNotificationBadge()
        If InvokeRequired Then
            BeginInvoke(New Action(AddressOf UpdateNotificationBadge))
            Return
        End If

        Dim unreadCount = _notificationManager.GetUnreadCount()

        If unreadCount = 0 Then
            _notificationBadge.Text = "🔔"
            _notificationBadge.BackColor = SecondaryBg
        Else
            _notificationBadge.Text = $"🔔 ({unreadCount})"
            _notificationBadge.BackColor = CriticalRed
        End If
    End Sub

    Private Sub FlashNotificationBadge()
        Dim flashTimer As New Timer With {.Interval = FLASH_TIMER_INTERVAL}
        Dim flashCount = 0

        AddHandler flashTimer.Tick, Sub(s, e)
                                        flashCount += 1

                                        If flashCount Mod 2 = 0 Then
                                            _notificationBadge.BackColor = CriticalRed
                                        Else
                                            _notificationBadge.BackColor = Color.FromArgb(255, 149, 0)
                                        End If

                                        If flashCount >= FLASH_COUNT Then
                                            flashTimer.Stop()
                                            UpdateNotificationBadge()
                                        End If
                                    End Sub
        flashTimer.Start()
    End Sub

    Private Async Sub HomeAdminMenuItem_Click(sender As Object, e As EventArgs)
        Try
            If _apiClient Is Nothing Then
                MessageBox.Show(
                    "Le client API n'est pas encore démarré." & Environment.NewLine & Environment.NewLine &
                    "Veuillez d'abord charger les données (Fichier > Démarrer).",
                    "Information", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            ' REFACTO: Pause automatique du temps réel (transparent pour l'utilisateur)
            PauseRealTime()

            Try
                ' OPTIMISATION: Préparer les données en cache pour éviter les appels API
                LogDebug("=== OUVERTURE ADMINISTRATION (MODE OPTIMISÉ) ===")
                Dim cachedDevices As New List(Of DeviceInfo)

                ' Copier les données déjà en cache (ConcurrentDictionary = thread-safe, pas besoin de lock)
                cachedDevices.AddRange(_deviceInfoCache.Values)

                If cachedDevices.Count = 0 Then
                    LogDebug("⚠️ Aucune donnée en cache - l'administration chargera depuis l'API (peut être lent)")
                    UpdateStatus("Ouverture administration - chargement initial...")
                Else
                    LogDebug($"✓ {cachedDevices.Count} appareils passés au formulaire d'administration (mode rapide)")
                    UpdateStatus("Ouverture administration - mode rapide...")
                End If

                ' Ouvrir le formulaire avec les données pré-chargées (pas d'appels API supplémentaires)
                Using adminForm As New HomeAdminForm(_apiClient, cachedDevices)
                    Dim result = adminForm.ShowDialog()

                ' Rafraîchir l'affichage après fermeture si des changements ont été effectués
                If result = DialogResult.OK OrElse result = DialogResult.Cancel Then
                    LogDebug("=== RAFRAÎCHISSEMENT AFFICHAGE APRÈS ADMINISTRATION ===")

                    ' Les modifications dans HomeAdminForm ont déjà mis à jour _preloadedDevices (cache local)
                    ' Pas besoin de recharger depuis l'API, juste rafraîchir l'affichage !
                    Await DisplayDevicesByRoomAsync()

                    LogDebug("=== RAFRAÎCHISSEMENT TERMINÉ ===")
                    UpdateStatus("Affichage rafraîchi après administration")
                End If
            End Using

            Catch ex As Exception
                LogDebug(String.Format("Erreur ouverture administration: {0}", ex.Message))
                MessageBox.Show($"Erreur lors de l'ouverture de l'administration :{Environment.NewLine}{ex.Message}",
                              "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                ' REFACTO: Reprise automatique du temps réel
                ResumeRealTime()
            End Try

        Catch ex As Exception
            LogDebug(String.Format("Erreur critique administration: {0}", ex.Message))
            MessageBox.Show($"Erreur critique :{Environment.NewLine}{ex.Message}",
                          "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub CategoryConfigMenuItem_Click(sender As Object, e As EventArgs)
        Try
            ' Vérifier que le client API est initialisé
            If _apiClient Is Nothing Then
                MessageBox.Show(
                    "Le client API n'est pas encore démarré." & Environment.NewLine & Environment.NewLine &
                    "Veuillez d'abord charger les données (Fichier > Démarrer).",
                    "Information", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            ' REFACTO: Pause automatique du temps réel
            PauseRealTime()

            Try
                ' Ouvrir le formulaire de préférences d'affichage
                Using prefsForm As New DisplayPreferencesForm(_apiClient, Me)
                    If prefsForm.ShowDialog() = DialogResult.OK Then
                        ' L'utilisateur a enregistré les préférences, rafraîchir toutes les tuiles
                        RefreshAllDeviceCards()
                        LogDebug("✓ Préférences d'affichage appliquées, toutes les tuiles ont été rafraîchies")
                    End If
                End Using
            Finally
                ResumeRealTime()
            End Try

        Catch ex As Exception
            LogDebug($"✗ Erreur ouverture préférences d'affichage: {ex.Message}")
            MessageBox.Show("Erreur lors de l'ouverture des préférences d'affichage." & Environment.NewLine & ex.Message,
                          "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
            ResumeRealTime()
        End Try
    End Sub

    Private Sub AutomationMenuItem_Click(sender As Object, e As EventArgs)
        Try
            If _apiClient Is Nothing Then
                MessageBox.Show(
                    "Le client API n'est pas encore démarré." & Environment.NewLine & Environment.NewLine &
                    "Veuillez d'abord charger les données (Fichier > Démarrer).",
                    "Information", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            ' REFACTO: Pause automatique du temps réel
            PauseRealTime()

            Try
                LogDebug("=== OUVERTURE GESTION AUTOMATISATIONS ===")
                UpdateStatus("Ouverture gestion des automatisations...")

                ' Ouvrir le formulaire de gestion des automatisations
                Using automationForm As New AutomationForm(_apiClient)
                    automationForm.ShowDialog()
                End Using

                LogDebug("=== FORMULAIRE AUTOMATISATIONS FERMÉ ===")
                UpdateStatus("Gestion des automatisations fermée")

            Catch ex As Exception
                LogDebug(String.Format("Erreur ouverture automatisations: {0}", ex.Message))
                MessageBox.Show($"Erreur lors de l'ouverture de la gestion des automatisations :{Environment.NewLine}{ex.Message}",
                              "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                ' REFACTO: Reprise automatique du temps réel
                ResumeRealTime()
            End Try

        Catch ex As Exception
            LogDebug(String.Format("Erreur critique automatisations: {0}", ex.Message))
            MessageBox.Show($"Erreur critique :{Environment.NewLine}{ex.Message}",
                          "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub RefreshAllDeviceCards()
        Try
            Dim cardCount As Integer = 0

            ' ConcurrentDictionary est thread-safe, pas besoin de lock
            For Each kvp In _deviceCards.ToList()
                Dim card As DeviceCard = kvp.Value
                card.RefreshDisplay()
                cardCount += 1
            Next

            LogDebug(String.Format("✓ {0} cartes d'appareils rafraîchies", cardCount))

            ' Rafraîchir également la vue tableau si elle est active
            If _currentView = ViewMode.Table AndAlso _tableView.Visible Then
                LogDebug("Rafraîchissement de la vue tableau...")
                SwitchToTableView(Nothing, Nothing)
            End If

        Catch ex As Exception
            LogDebug(String.Format("Erreur RefreshAllDeviceCards: {0}", ex.Message))
        End Try
    End Sub

    ''' <summary>
    ''' Rafraîchit uniquement les tuiles d'une catégorie spécifique (optimisé)
    ''' </summary>
    Public Sub RefreshDeviceCardsByCategory(category As String)
        Try
            Dim cardCount As Integer = 0

            ' ConcurrentDictionary = thread-safe, pas besoin de lock
            For Each kvp In _deviceCards.ToList()
                Dim card As DeviceCard = kvp.Value

                ' Rafraîchir uniquement si la carte appartient à la catégorie
                If card.Category = category Then
                    card.RefreshDisplay()
                    cardCount += 1
                End If
            Next

            LogDebug($"✓ {cardCount} carte(s) de la catégorie '{category}' rafraîchie(s)")

            ' Rafraîchir également la vue tableau si elle est active
            If _currentView = ViewMode.Table AndAlso _tableView.Visible Then
                LogDebug("Rafraîchissement de la vue tableau...")
                SwitchToTableView(Nothing, Nothing)
            End If

        Catch ex As Exception
            LogDebug($"✗ Erreur RefreshDeviceCardsByCategory pour '{category}': {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Retourne toutes les propriétés connues pour une catégorie donnée
    ''' Inclut les sous-propriétés JSON découvertes dynamiquement
    ''' </summary>
    Public Function GetKnownPropertiesForCategory(category As String) As HashSet(Of String)
        Dim knownProperties As New HashSet(Of String)

        Try
            ' ConcurrentDictionary = thread-safe, pas besoin de lock
            ' Parcourir tous les DeviceCard de cette catégorie
            For Each kvp In _deviceCards.ToList()
                Dim card As DeviceCard = kvp.Value

                ' Vérifier si la carte appartient à la catégorie recherchée
                If card.Category = category Then
                    ' Récupérer toutes les propriétés connues de cette carte
                    Dim propertyCodes = card.GetAllKnownPropertyCodes()
                    For Each code In propertyCodes
                        knownProperties.Add(code)
                    Next
                End If
            Next

            LogDebug($"✓ {knownProperties.Count} propriétés connues pour la catégorie '{category}'")
        Catch ex As Exception
            LogDebug($"✗ Erreur GetKnownPropertiesForCategory: {ex.Message}")
        End Try

        Return knownProperties
    End Function

#End Region

#Region "Gestion de la console debug"
    Private Sub RedirectConsoleOutput()
        Try
            Console.OutputEncoding = Encoding.UTF8

            Dim writer As New TextBoxWriter(_debugTextBox, Function() _isPaused)
            Console.SetOut(writer)
            Console.SetError(writer)

            Dim outWriter = TryCast(Console.Out, StreamWriter)
            If outWriter IsNot Nothing Then outWriter.AutoFlush = True

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

        If InvokeRequired Then
            BeginInvoke(New Action(Of String)(AddressOf LogDebug), message)
            Return
        End If

        Try
            Dim timestamp = DateTime.Now.ToString("HH:mm:ss.fff")
            Dim logMessage = $"[{timestamp}] {message}{Environment.NewLine}"

            ' Limiter le nombre de lignes - OPTIMISÉ
            If _debugTextBox.Lines.Length > MAX_DEBUG_LINES Then
                Dim lines = _debugTextBox.Lines
                Dim newLines = lines.Skip(LINES_TO_REMOVE).ToArray()
                _debugTextBox.Lines = newLines
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
#End Region

#Region "Méthodes utilitaires"
    Private Sub RunOnUIThread(action As Action)
        If InvokeRequired Then
            BeginInvoke(action)
        Else
            action()
        End If
    End Sub

    Private Sub UpdateStatus(message As String)
        If InvokeRequired Then
            BeginInvoke(New Action(Of String)(AddressOf UpdateStatus), message)
            Return
        End If
        _statusLabel.Text = message
    End Sub

    Private Function GetJsonString(token As JToken, path As String) As String
        Return token?.SelectToken(path)?.ToString()
    End Function

    Private Sub HandleError(context As String, ex As Exception)
        Dim message = $"{context}: {ex.Message}"
        LogDebug($"ERREUR {message}")
        LogDebug($"StackTrace: {ex.StackTrace}")
        UpdateStatus($"Erreur: {ex.Message}")
        MessageBox.Show($"{message}{Environment.NewLine}{Environment.NewLine}Détails: {ex.StackTrace}",
                      "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
    End Sub
#End Region

#Region "Nettoyage"
    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        MyBase.OnFormClosing(e)
        Try
            LogDebug("=== FERMETURE DE L'APPLICATION ===")

            If _isRunning Then StopServices()

            ' Nettoyer les event handlers
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
#End Region
End Class