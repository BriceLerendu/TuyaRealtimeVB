Imports System.Drawing
Imports System.Windows.Forms
Imports System.Linq

''' <summary>
''' Formulaire pour afficher le Message Center de Tuya
''' Affiche les messages, notifications et alarmes comme dans l'application SmartLife
''' </summary>
Public Class MessageCenterForm
    Inherits Form

#Region "Constantes"
    Private Const FORM_TITLE As String = "Centre de Messages Tuya"
#End Region

#Region "Champs privés"
    Private _messageCenter As TuyaMessageCenter
    Private _cfg As TuyaConfig
    Private _tokenProvider As TuyaTokenProvider
    Private _listView As ListView
    Private _statusLabel As Label
    Private _unreadCountLabel As Label
    Private _btnRefresh As Button
    Private _btnAll As Button
    Private _btnHome As Button
    Private _btnBulletin As Button
    Private _btnAlarm As Button
    Private _currentFilter As TuyaMessageCenter.MessageType = TuyaMessageCenter.MessageType.All
    Private _allMessages As New List(Of TuyaMessageCenter.TuyaMessage)
    Private _logTextBox As TextBox
    Private _logSplitter As Splitter
#End Region

#Region "Initialisation"
    Public Sub New(cfg As TuyaConfig, tokenProvider As TuyaTokenProvider)
        _cfg = cfg
        _tokenProvider = tokenProvider
        _messageCenter = New TuyaMessageCenter(cfg, tokenProvider, AddressOf Log)

        InitializeComponent()
    End Sub

    Private Sub InitializeComponent()
        ' Configuration de la fenêtre
        Text = FORM_TITLE
        Size = New Size(900, 700)
        StartPosition = FormStartPosition.CenterScreen
        BackColor = ThemeConstants.LightBg
        MinimumSize = New Size(700, 500)

        ' Panel supérieur (titre + compteur + boutons)
        Dim topPanel = CreateTopPanel()
        Controls.Add(topPanel)

        ' Panel inférieur (barre de statut)
        Dim bottomPanel = CreateBottomPanel()
        Controls.Add(bottomPanel)

        ' Panel de logs avec splitter
        _logTextBox = CreateLogPanel()
        Controls.Add(_logTextBox)

        _logSplitter = New Splitter With {
            .Dock = DockStyle.Bottom,
            .Height = 3,
            .BackColor = ThemeConstants.DarkBg
        }
        Controls.Add(_logSplitter)

        ' ListView pour afficher les messages
        _listView = CreateMessageListView()
        Controls.Add(_listView)

        ' Charger les messages au démarrage
        LoadMessagesAsync()
    End Sub

    Private Function CreateTopPanel() As Panel
        Dim panel As New Panel With {
            .Dock = DockStyle.Top,
            .Height = 140,
            .BackColor = ThemeConstants.DarkBg
        }

        ' Titre
        Dim titleLabel As New Label With {
            .Text = FORM_TITLE,
            .Font = ThemeConstants.GetTitleFont(),
            .ForeColor = ThemeConstants.TextWhite,
            .AutoSize = True,
            .Location = New Point(20, 20)
        }
        panel.Controls.Add(titleLabel)

        ' Compteur de messages non lus
        _unreadCountLabel = New Label With {
            .Text = "Messages non lus: 0",
            .Font = ThemeConstants.GetNormalFont(),
            .ForeColor = ThemeConstants.WarningOrange,
            .AutoSize = True,
            .Location = New Point(20, 55)
        }
        panel.Controls.Add(_unreadCountLabel)

        ' Bouton de rafraîchissement
        _btnRefresh = CreateFilterButton("🔄 Actualiser", 700, Sub() LoadMessagesAsync())
        panel.Controls.Add(_btnRefresh)

        ' Boutons de filtre
        Dim filterY = 90
        _btnAll = CreateFilterButton("📬 Tous", 20, Sub() FilterMessages(TuyaMessageCenter.MessageType.All))
        _btnAll.Location = New Point(20, filterY)
        _btnAll.BackColor = ThemeConstants.ActiveBlue

        _btnHome = CreateFilterButton("🏠 Famille", 160, Sub() FilterMessages(TuyaMessageCenter.MessageType.Home))
        _btnHome.Location = New Point(160, filterY)

        _btnBulletin = CreateFilterButton("📢 Bulletins", 300, Sub() FilterMessages(TuyaMessageCenter.MessageType.Bulletin))
        _btnBulletin.Location = New Point(300, filterY)

        _btnAlarm = CreateFilterButton("🚨 Alarmes", 440, Sub() FilterMessages(TuyaMessageCenter.MessageType.Alarm))
        _btnAlarm.Location = New Point(440, filterY)

        panel.Controls.AddRange({_btnAll, _btnHome, _btnBulletin, _btnAlarm})

        Return panel
    End Function

    Private Function CreateFilterButton(text As String, x As Integer, clickHandler As Action) As Button
        Dim btn As New Button With {
            .Text = text,
            .Size = New Size(130, 35),
            .Location = New Point(x, 90),
            .Font = ThemeConstants.GetNormalFont(),
            .ForeColor = Color.White,
            .BackColor = ThemeConstants.InactiveGray,
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand
        }
        btn.FlatAppearance.BorderSize = 0
        AddHandler btn.Click, Sub(s, e) clickHandler()
        Return btn
    End Function

    Private Function CreateMessageListView() As ListView
        Dim lv As New ListView With {
            .Dock = DockStyle.Fill,
            .View = View.Details,
            .FullRowSelect = True,
            .GridLines = True,
            .Font = ThemeConstants.GetNormalFont(),
            .BackColor = Color.White,
            .BorderStyle = BorderStyle.None
        }

        ' Configuration des colonnes
        lv.Columns.Add("Type", 100)
        lv.Columns.Add("Titre", 200)
        lv.Columns.Add("Message", 380)
        lv.Columns.Add("Date/Heure", 150)
        lv.Columns.Add("Statut", 80)

        ' Événement de double-clic pour afficher les détails
        AddHandler lv.DoubleClick, AddressOf OnMessageDoubleClick

        Return lv
    End Function

    Private Function CreateBottomPanel() As Panel
        Dim panel As New Panel With {
            .Dock = DockStyle.Bottom,
            .Height = 30,
            .BackColor = ThemeConstants.DarkBg
        }

        _statusLabel = New Label With {
            .Text = "Chargement...",
            .Font = ThemeConstants.GetSmallFont(),
            .ForeColor = ThemeConstants.TextLightGray,
            .AutoSize = True,
            .Location = New Point(10, 7)
        }
        panel.Controls.Add(_statusLabel)

        Return panel
    End Function

    Private Function CreateLogPanel() As TextBox
        Dim logBox As New TextBox With {
            .Dock = DockStyle.Bottom,
            .Height = 200,
            .Multiline = True,
            .ReadOnly = True,
            .ScrollBars = ScrollBars.Both,
            .BackColor = Color.Black,
            .ForeColor = Color.LimeGreen,
            .Font = New Font("Consolas", 9),
            .WordWrap = False
        }

        Return logBox
    End Function
#End Region

#Region "Chargement des messages"
    Private Async Sub LoadMessagesAsync()
        Try
            _statusLabel.Text = "Chargement des messages..."
            _btnRefresh.Enabled = False
            _listView.Items.Clear()

            Log("=== Début du chargement des messages ===")

            ' Récupérer tous les messages
            _allMessages = Await _messageCenter.GetAllMessagesAsync()

            Log($"Messages récupérés : {_allMessages.Count}")

            ' Afficher les messages filtrés
            FilterMessages(_currentFilter)

            ' Mettre à jour le compteur de messages non lus
            Dim unreadCount = _allMessages.Where(Function(m) Not m.IsRead).Count()
            _unreadCountLabel.Text = $"Messages non lus: {unreadCount}"

            If _allMessages.Count = 0 Then
                _statusLabel.Text = "ℹ️ Aucun message disponible"
                MessageBox.Show(
                    "Aucun message n'a été trouvé." & Environment.NewLine & Environment.NewLine &
                    "📋 VÉRIFICATIONS À FAIRE :" & Environment.NewLine & Environment.NewLine &
                    "1️⃣ Activez l'API 'Message Service' dans votre projet Tuya" & Environment.NewLine &
                    "   → Allez sur https://iot.tuya.com" & Environment.NewLine &
                    "   → Cloud → Project → Votre Projet → API" & Environment.NewLine &
                    "   → Recherchez 'Message' et activez le service" & Environment.NewLine & Environment.NewLine &
                    "2️⃣ Vérifiez qu'il y a des messages dans SmartLife" & Environment.NewLine &
                    "   → Ouvrez l'app SmartLife" & Environment.NewLine &
                    "   → Onglet 'Moi' → Centre de messages" & Environment.NewLine & Environment.NewLine &
                    "3️⃣ Consultez les LOGS dans la zone noire en bas" & Environment.NewLine &
                    "   → Regardez le code d'erreur retourné par l'API" & Environment.NewLine &
                    "   → Le diagnostic vous indiquera le problème exact" & Environment.NewLine & Environment.NewLine &
                    "💡 Note: L'API 'Mobile Push Notification Service' permet" & Environment.NewLine &
                    "    d'ENVOYER des notifications, pas de les RECEVOIR." & Environment.NewLine &
                    "    Il faut activer 'Message Service' pour recevoir.",
                    "Aucun message trouvé - Aide au diagnostic",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information)
            Else
                _statusLabel.Text = $"✅ {_allMessages.Count} message(s) chargé(s)"
            End If
        Catch ex As Exception
            _statusLabel.Text = $"❌ Erreur: {ex.Message}"
            Log($"ERREUR LoadMessagesAsync: {ex.Message}")
            Log($"StackTrace: {ex.StackTrace}")
            MessageBox.Show($"Erreur lors du chargement des messages: {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            _btnRefresh.Enabled = True
        End Try
    End Sub

    Private Sub FilterMessages(filterType As TuyaMessageCenter.MessageType)
        Try
            _currentFilter = filterType

            ' Mettre à jour l'état des boutons
            ResetFilterButtonColors()
            Select Case filterType
                Case TuyaMessageCenter.MessageType.All
                    _btnAll.BackColor = ThemeConstants.ActiveBlue
                Case TuyaMessageCenter.MessageType.Home
                    _btnHome.BackColor = ThemeConstants.ActiveBlue
                Case TuyaMessageCenter.MessageType.Bulletin
                    _btnBulletin.BackColor = ThemeConstants.ActiveBlue
                Case TuyaMessageCenter.MessageType.Alarm
                    _btnAlarm.BackColor = ThemeConstants.ActiveBlue
            End Select

            ' Filtrer et afficher les messages
            _listView.Items.Clear()

            Dim filteredMessages = If(filterType = TuyaMessageCenter.MessageType.All,
                                     _allMessages,
                                     _allMessages.Where(Function(m) m.MessageType = filterType).ToList())

            For Each msg In filteredMessages.OrderByDescending(Function(m) m.Timestamp)
                Dim item = CreateListViewItem(msg)
                _listView.Items.Add(item)
            Next

            _statusLabel.Text = $"📊 {filteredMessages.Count} message(s) affiché(s)"
        Catch ex As Exception
            _statusLabel.Text = $"❌ Erreur de filtrage: {ex.Message}"
        End Try
    End Sub

    Private Sub ResetFilterButtonColors()
        _btnAll.BackColor = ThemeConstants.InactiveGray
        _btnHome.BackColor = ThemeConstants.InactiveGray
        _btnBulletin.BackColor = ThemeConstants.InactiveGray
        _btnAlarm.BackColor = ThemeConstants.InactiveGray
    End Sub

    Private Function CreateListViewItem(msg As TuyaMessageCenter.TuyaMessage) As ListViewItem
        ' Type de message avec icône
        Dim typeText = GetMessageTypeIcon(msg.MessageType)

        ' Titre
        Dim title = If(String.IsNullOrEmpty(msg.Title), "[Sans titre]", msg.Title)

        ' Contenu (limité à 100 caractères)
        Dim content = If(String.IsNullOrEmpty(msg.Content), "[Pas de contenu]", msg.Content)
        If content.Length > 100 Then
            content = content.Substring(0, 97) & "..."
        End If

        ' Date/Heure
        Dim dateTime = msg.Timestamp.ToString("dd/MM/yyyy HH:mm")

        ' Statut lu/non-lu
        Dim status = If(msg.IsRead, "Lu", "NON LU")

        ' Créer l'item
        Dim item As New ListViewItem(typeText)
        item.SubItems.Add(title)
        item.SubItems.Add(content)
        item.SubItems.Add(dateTime)
        item.SubItems.Add(status)
        item.Tag = msg

        ' Couleur selon le statut
        If Not msg.IsRead Then
            item.Font = New Font(item.Font, FontStyle.Bold)
            item.BackColor = Color.FromArgb(240, 248, 255) ' Bleu très clair
        End If

        ' Couleur selon le type
        Select Case msg.MessageType
            Case TuyaMessageCenter.MessageType.Alarm
                item.ForeColor = ThemeConstants.CriticalRed
            Case TuyaMessageCenter.MessageType.Home
                item.ForeColor = ThemeConstants.ActiveBlue
        End Select

        Return item
    End Function

    Private Function GetMessageTypeIcon(msgType As TuyaMessageCenter.MessageType) As String
        Select Case msgType
            Case TuyaMessageCenter.MessageType.Home
                Return "🏠 Famille"
            Case TuyaMessageCenter.MessageType.Bulletin
                Return "📢 Bulletin"
            Case TuyaMessageCenter.MessageType.Alarm
                Return "🚨 Alarme"
            Case Else
                Return "📬 Message"
        End Select
    End Function
#End Region

#Region "Événements"
    Private Sub OnMessageDoubleClick(sender As Object, e As EventArgs)
        If _listView.SelectedItems.Count = 0 Then Return

        Try
            Dim selectedItem = _listView.SelectedItems(0)
            Dim msg = TryCast(selectedItem.Tag, TuyaMessageCenter.TuyaMessage)

            If msg IsNot Nothing Then
                ShowMessageDetails(msg)
            End If
        Catch ex As Exception
            MessageBox.Show($"Erreur: {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ShowMessageDetails(msg As TuyaMessageCenter.TuyaMessage)
        Dim details As New System.Text.StringBuilder()

        details.AppendLine($"Type: {GetMessageTypeIcon(msg.MessageType)}")
        details.AppendLine($"ID: {msg.Id}")
        details.AppendLine($"")
        details.AppendLine($"Titre: {msg.Title}")
        details.AppendLine($"")
        details.AppendLine($"Message:")
        details.AppendLine(msg.Content)
        details.AppendLine($"")
        details.AppendLine($"Date/Heure: {msg.Timestamp:dd/MM/yyyy HH:mm:ss}")
        details.AppendLine($"Statut: {If(msg.IsRead, "Lu", "Non lu")}")

        If Not String.IsNullOrEmpty(msg.DeviceId) Then
            details.AppendLine($"")
            details.AppendLine($"Appareil: {msg.DeviceName} ({msg.DeviceId})")
        End If

        If msg.RawData IsNot Nothing Then
            details.AppendLine($"")
            details.AppendLine("--- Données brutes JSON ---")
            details.AppendLine(msg.RawData.ToString(Newtonsoft.Json.Formatting.Indented))
        End If

        ' Afficher dans une boîte de dialogue
        Dim detailsForm As New Form With {
            .Text = "Détails du message",
            .Size = New Size(600, 500),
            .StartPosition = FormStartPosition.CenterParent,
            .BackColor = Color.White
        }

        Dim textBox As New TextBox With {
            .Multiline = True,
            .ReadOnly = True,
            .Dock = DockStyle.Fill,
            .ScrollBars = ScrollBars.Both,
            .Font = ThemeConstants.GetNormalFont(),
            .Text = details.ToString()
        }

        detailsForm.Controls.Add(textBox)
        detailsForm.ShowDialog(Me)
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        MyBase.OnFormClosing(e)
        If _messageCenter IsNot Nothing Then
            _messageCenter.Dispose()
        End If
    End Sub
#End Region

#Region "Logging"
    Private Sub Log(message As String)
        Try
            ' Thread-safe: Invoke si nécessaire
            If _logTextBox.InvokeRequired Then
                _logTextBox.Invoke(Sub() Log(message))
                Return
            End If

            Dim timestamp = DateTime.Now.ToString("HH:mm:ss.fff")
            Dim logLine = $"[{timestamp}] {message}{Environment.NewLine}"

            _logTextBox.AppendText(logLine)

            ' Scroller automatiquement vers le bas
            _logTextBox.SelectionStart = _logTextBox.TextLength
            _logTextBox.ScrollToCaret()
        Catch ex As Exception
            ' Fallback vers Console si l'interface n'est pas prête
            Console.WriteLine($"[MessageCenter] {message}")
        End Try
    End Sub
#End Region

End Class
