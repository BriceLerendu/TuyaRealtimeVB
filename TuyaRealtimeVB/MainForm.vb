Imports System.Drawing
Imports System.Windows.Forms
Imports Newtonsoft.Json.Linq

Public Class MainForm
    Inherits Form

#Region "Constantes"
    Private Const MAX_EVENTS As Integer = 100
    Private Const TITLE_TEXT As String = "Moniteur d'événements Tuya en temps réel"
    Private Const PYTHON_SCRIPT_PATH As String = "C:\Users\leren\Downloads\tuya_bridge.py"

    ' Couleurs thématiques
    Private Shared ReadOnly DarkBg As Color = Color.FromArgb(45, 45, 48)
    Private Shared ReadOnly LightBg As Color = Color.FromArgb(240, 240, 240)
    Private Shared ReadOnly ActiveBtn As Color = Color.FromArgb(0, 122, 255)
    Private Shared ReadOnly InactiveBtn As Color = Color.FromArgb(142, 142, 147)
#End Region

#Region "Champs privés"
    Private WithEvents _httpServer As TuyaHttpServer
    Private WithEvents _pythonBridge As PythonBridge
    Private _listView As ListView
    Private _statusLabel As Label
    Private _serverStatusLabel As Label
    Private _pythonStatusLabel As Label
    Private _eventCountLabel As Label
    Private _tableView As RoomTableView
    Private _btnEvents As Button
    Private _btnTable As Button

    Private _eventCount As Integer = 0
    Private _currentView As ViewMode = ViewMode.Events

    Private Enum ViewMode
        Events
        Table
    End Enum
#End Region

#Region "Initialisation"
    Public Sub New()
        InitializeComponent()
        InitializeServices()
    End Sub

    Private Sub InitializeComponent()
        ' Configuration de la fenêtre
        Text = TITLE_TEXT
        Size = New Size(1000, 600)
        StartPosition = FormStartPosition.CenterScreen
        BackColor = LightBg

        ' Panel supérieur
        Dim topPanel = CreateTopPanel()
        _serverStatusLabel = CreateStatusLabel("● Serveur HTTP: Démarrage...", 20, 50, Color.Orange)
        _pythonStatusLabel = CreateStatusLabel("● Client Pulsar: Démarrage...", 250, 50, Color.Orange)
        _eventCountLabel = CreateStatusLabel("Événements reçus: 0", 500, 50, Color.LightGray)

        topPanel.Controls.AddRange({
            CreateTitleLabel(),
            _serverStatusLabel,
            _pythonStatusLabel,
            _eventCountLabel
        })

        ' Boutons de navigation
        _btnEvents = CreateNavButton("ÉVÉNEMENTS", 500, Sub() SwitchView(ViewMode.Events))
        _btnTable = CreateNavButton("TABLEAU", 670, Sub() SwitchView(ViewMode.Table))
        topPanel.Controls.AddRange({_btnEvents, _btnTable})

        Controls.Add(topPanel)

        ' Vue liste d'événements
        _listView = CreateEventListView()
        Controls.Add(_listView)

        ' Vue tableau
        _tableView = New RoomTableView With {
            .Dock = DockStyle.Fill,
            .Visible = False
        }
        Controls.Add(_tableView)

        ' Panel inférieur
        _statusLabel = New Label With {
            .Text = "Prêt",
            .Font = New Font("Segoe UI", 9),
            .ForeColor = Color.LightGray,
            .AutoSize = True,
            .Location = New Point(10, 7)
        }

        Dim bottomPanel = New Panel With {
            .Dock = DockStyle.Bottom,
            .Height = 30,
            .BackColor = DarkBg
        }
        bottomPanel.Controls.Add(_statusLabel)
        Controls.Add(bottomPanel)

        ' Définir la vue initiale
        UpdateViewButtons()
    End Sub

    Private Function CreateTopPanel() As Panel
        Return New Panel With {
            .Dock = DockStyle.Top,
            .Height = 100,
            .BackColor = DarkBg,
            .Padding = New Padding(20)
        }
    End Function

    Private Function CreateTitleLabel() As Label
        Return New Label With {
            .Text = TITLE_TEXT,
            .Font = New Font("Segoe UI", 16, FontStyle.Bold),
            .ForeColor = Color.White,
            .AutoSize = True,
            .Location = New Point(20, 15)
        }
    End Function

    Private Function CreateStatusLabel(text As String, x As Integer, y As Integer, color As Color) As Label
        Return New Label With {
            .Text = text,
            .Font = New Font("Segoe UI", 9),
            .ForeColor = color,
            .AutoSize = True,
            .Location = New Point(x, y)
        }
    End Function

    Private Function CreateNavButton(text As String, x As Integer, clickHandler As Action) As Button
        Dim btn = New Button With {
            .Text = text,
            .Size = New Size(150, 40),
            .Location = New Point(x, 30),
            .BackColor = InactiveBtn,
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 11, FontStyle.Bold),
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand
        }
        btn.FlatAppearance.BorderSize = 0
        AddHandler btn.Click, Sub(s, e) clickHandler()
        Return btn
    End Function

    Private Function CreateEventListView() As ListView
        Dim lv = New ListView With {
            .Dock = DockStyle.Fill,
            .View = View.Details,
            .FullRowSelect = True,
            .GridLines = True,
            .Font = New Font("Consolas", 9),
            .BackColor = Color.White
        }

        ' Colonnes avec largeurs optimisées
        lv.Columns.AddRange({
            New ColumnHeader With {.Text = "Heure", .Width = 100},
            New ColumnHeader With {.Text = "Appareil", .Width = 200},
            New ColumnHeader With {.Text = "Type", .Width = 120},
            New ColumnHeader With {.Text = "Propriété", .Width = 150},
            New ColumnHeader With {.Text = "Valeur", .Width = 200},
            New ColumnHeader With {.Text = "JSON", .Width = 200}
        })

        Return lv
    End Function

    Private Sub InitializeServices()
        Try
            ' Démarrer le serveur HTTP
            _httpServer = New TuyaHttpServer()
            AddHandler _httpServer.EventReceived, AddressOf OnEventReceived
            _httpServer.Start()
            SetStatus(_serverStatusLabel, "● Serveur HTTP: Actif", Color.LimeGreen)

            ' Démarrer le client Pulsar
            _pythonBridge = New PythonBridge(PYTHON_SCRIPT_PATH)
            _pythonBridge.Start()
            SetStatus(_pythonStatusLabel, "● Client Pulsar: Connecté", Color.LimeGreen)

            SetStatus(_statusLabel, "Système opérationnel - En attente d'événements")

        Catch ex As Exception
            HandleError("Erreur d'initialisation", ex)
        End Try
    End Sub
#End Region

#Region "Gestion des événements"
    Private Sub OnEventReceived(eventData As String)
        RunOnUIThread(Sub() ProcessEvent(eventData))
    End Sub

    Private Sub ProcessEvent(eventData As String)
        Try
            Dim json = JObject.Parse(eventData)

            ' Incrémenter le compteur
            _eventCount += 1
            _eventCountLabel.Text = $"Événements reçus: {_eventCount}"

            ' Extraire les données
            Dim devId = GetJsonValue(json, "devId")
            Dim bizCode = GetJsonValue(json, "bizCode")
            Dim status = json.SelectToken("status")
            Dim timestamp = DateTime.Now.ToString("HH:mm:ss")

            ' Traiter selon le type d'événement
            If status IsNot Nothing Then
                ProcessStatusEvent(timestamp, devId, bizCode, status, json)
            Else
                ProcessSimpleEvent(timestamp, devId, bizCode, json)
            End If

            SetStatus(_statusLabel, $"Dernier événement: {devId} - {timestamp}")

        Catch ex As Exception
            HandleError("Erreur de traitement", ex)
        End Try
    End Sub

    Private Sub ProcessStatusEvent(timestamp As String, devId As String, bizCode As String,
                                   status As JToken, json As JObject)
        For Each item In status
            Dim code = GetJsonValue(item, "code")
            Dim value = GetJsonValue(item, "value")

            Dim listItem = CreateListItem(timestamp, devId, If(bizCode, "report"),
                                         code, value, json, bizCode)
            AddListItem(listItem)
        Next
    End Sub

    Private Sub ProcessSimpleEvent(timestamp As String, devId As String,
                                   bizCode As String, json As JObject)
        Dim listItem = CreateListItem(timestamp, devId, bizCode, "-", "-", json, bizCode)
        AddListItem(listItem)
    End Sub

    Private Function CreateListItem(timestamp As String, devId As String, type As String,
                                   code As String, value As String, json As JObject,
                                   bizCode As String) As ListViewItem
        Dim item = New ListViewItem(timestamp)
        item.SubItems.AddRange({devId, type, code, value,
                               json.ToString(Newtonsoft.Json.Formatting.None)})

        ' Appliquer la couleur selon le type
        item.BackColor = GetEventColor(bizCode)

        Return item
    End Function

    Private Sub AddListItem(item As ListViewItem)
        _listView.Items.Insert(0, item)

        ' Limiter le nombre d'événements affichés
        While _listView.Items.Count > MAX_EVENTS
            _listView.Items.RemoveAt(_listView.Items.Count - 1)
        End While
    End Sub

    Private Function GetEventColor(bizCode As String) As Color
        Select Case bizCode?.ToLower()
            Case "online"
                Return Color.LightGreen
            Case "offline"
                Return Color.LightCoral
            Case Else
                Return Color.White
        End Select
    End Function
#End Region

#Region "Navigation entre vues"
    Private Sub SwitchView(newView As ViewMode)
        _currentView = newView

        Select Case newView
            Case ViewMode.Events
                ShowEventsView()
            Case ViewMode.Table
                ShowTableView()
        End Select

        UpdateViewButtons()
    End Sub

    Private Sub ShowEventsView()
        _listView.Visible = True
        _listView.BringToFront()
        _tableView.Visible = False
        SetStatus(_statusLabel, "Vue Événements activée")
    End Sub

    Private Sub ShowTableView()
        _tableView.Visible = True
        _tableView.BringToFront()
        _listView.Visible = False
        SetStatus(_statusLabel, "Vue Tableau activée")
    End Sub

    Private Sub UpdateViewButtons()
        _btnEvents.BackColor = If(_currentView = ViewMode.Events, ActiveBtn, InactiveBtn)
        _btnTable.BackColor = If(_currentView = ViewMode.Table, ActiveBtn, InactiveBtn)
    End Sub
#End Region

#Region "Méthodes utilitaires"
    Private Sub RunOnUIThread(action As Action)
        If InvokeRequired Then
            Invoke(action)
        Else
            action()
        End If
    End Sub

    Private Sub SetStatus(label As Label, message As String, Optional color As Color = Nothing)
        RunOnUIThread(Sub()
                          label.Text = message
                          If color <> Nothing Then label.ForeColor = color
                      End Sub)
    End Sub

    Private Function GetJsonValue(token As JToken, path As String) As String
        Return token?.SelectToken(path)?.ToString()
    End Function

    Private Sub HandleError(context As String, ex As Exception)
        Dim message = $"{context}: {ex.Message}"
        SetStatus(_statusLabel, message, Color.Red)
        MessageBox.Show(message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
    End Sub
#End Region

#Region "Nettoyage"
    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        MyBase.OnFormClosing(e)

        Try
            _pythonBridge?.Stop()
            _httpServer?.Stop()
        Catch ex As Exception
            ' Log silencieux lors de la fermeture
            Debug.WriteLine($"Erreur lors de la fermeture: {ex.Message}")
        End Try
    End Sub
#End Region
End Class