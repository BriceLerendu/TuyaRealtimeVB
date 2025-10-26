Imports System.Drawing
Imports System.Windows.Forms
Imports Newtonsoft.Json.Linq

''' <summary>
''' Formulaire d'administration des Homes, Rooms et Appareils Tuya
''' Permet de créer, renommer, supprimer et déplacer les éléments de manière visuelle
''' </summary>
Public Class HomeAdminForm
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
#End Region

#Region "Champs privés"
    Private _apiClient As TuyaApiClient
    Private _treeView As TreeView
    Private _btnRefresh As Button
    Private _btnCreateHome As Button
    Private _btnCreateRoom As Button
    Private _btnRename As Button
    Private _btnDelete As Button
    Private _btnMoveDevice As Button
    Private _statusLabel As Label
    Private _infoPanel As Panel
    Private _infoLabel As Label
    Private _contextMenu As ContextMenuStrip

    ' Données en cache
    Private _homesData As New Dictionary(Of String, JToken)
    Private _roomsData As New Dictionary(Of String, JToken)
    Private _devicesData As New Dictionary(Of String, DeviceInfo)

    ' Données pré-chargées depuis le dashboard (pour éviter les appels API)
    Private _preloadedDevices As List(Of DeviceInfo)
#End Region

#Region "Initialisation"
    ''' <summary>
    ''' Constructeur optimisé - utilise les données déjà en cache
    ''' </summary>
    Public Sub New(apiClient As TuyaApiClient, Optional preloadedDevices As List(Of DeviceInfo) = Nothing)
        _apiClient = apiClient
        _preloadedDevices = preloadedDevices
        InitializeComponent()
    End Sub

    Private Sub InitializeComponent()
        ' Configuration du formulaire
        Text = "Administration Tuya - Gestion des Homes, Rooms et Appareils"
        Size = New Size(1200, 750)
        StartPosition = FormStartPosition.CenterScreen
        BackColor = LightBg
        MinimumSize = New Size(1000, 650)

        ' Créer le layout principal avec TableLayoutPanel (pas de Dock/SplitContainer)
        CreateMainLayout()
        CreateContextMenu()
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
        mainLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 50))    ' Toolbar
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
            .Text = "🏠 Administration Tuya",
            .Font = New Font("Segoe UI", 12, FontStyle.Bold),
            .ForeColor = Color.White,
            .AutoSize = True,
            .Location = New Point(15, 15)
        }
        panel.Controls.Add(titleLabel)

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

        ' Bouton Créer Home
        _btnCreateHome = New Button With {
            .Text = "➕ Nouveau Logement",
            .Font = New Font("Segoe UI", 9, FontStyle.Bold),
            .ForeColor = Color.White,
            .BackColor = ActiveBlue,
            .FlatStyle = FlatStyle.Flat,
            .Size = New Size(160, 32),
            .Cursor = Cursors.Hand
        }
        _btnCreateHome.FlatAppearance.BorderSize = 0
        AddHandler _btnCreateHome.Click, AddressOf BtnCreateHome_Click
        panel.Controls.Add(_btnCreateHome)

        ' Positionner les boutons à droite
        AddHandler panel.Resize, Sub(s, e)
                                     _btnRefresh.Location = New Point(panel.Width - _btnRefresh.Width - 15, 9)
                                     _btnCreateHome.Location = New Point(_btnRefresh.Left - _btnCreateHome.Width - 10, 9)
                                 End Sub

        Return panel
    End Function

    Private Function CreateContentPanel() As Panel
        Dim panel = New Panel With {
            .Dock = DockStyle.Fill,
            .BackColor = LightBg,
            .Margin = New Padding(0)
        }

        ' SplitContainer pour permettre le redimensionnement (TreeView / Actions)
        Dim contentSplitter = New SplitContainer With {
            .Dock = DockStyle.Fill,
            .Orientation = Orientation.Horizontal,
            .BackColor = InactiveGray,
            .SplitterWidth = 3,
            .IsSplitterFixed = False,
            .SplitterDistance = 450
        }

        ' Panel haut: TreeView
        Dim treePanel = CreateTreePanel()
        contentSplitter.Panel1.Controls.Add(treePanel)

        ' Panel bas: Actions (étirable)
        Dim actionsPanel = CreateActionsPanel()
        contentSplitter.Panel2.Controls.Add(actionsPanel)

        panel.Controls.Add(contentSplitter)
        Return panel
    End Function

    Private Function CreateTreePanel() As Panel
        Dim panel = New Panel With {
            .Dock = DockStyle.Fill,
            .BackColor = Color.White,
            .Padding = New Padding(10),
            .Margin = New Padding(0)
        }

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
            .ItemHeight = 24,
            .AllowDrop = True
        }

        AddHandler _treeView.AfterSelect, AddressOf TreeView_AfterSelect
        AddHandler _treeView.NodeMouseClick, AddressOf TreeView_NodeMouseClick
        AddHandler _treeView.ItemDrag, AddressOf TreeView_ItemDrag
        AddHandler _treeView.DragEnter, AddressOf TreeView_DragEnter
        AddHandler _treeView.DragOver, AddressOf TreeView_DragOver
        AddHandler _treeView.DragDrop, AddressOf TreeView_DragDrop

        panel.Controls.Add(_treeView)
        Return panel
    End Function

    Private Function CreateActionsPanel() As Panel
        Dim panel = New Panel With {
            .Dock = DockStyle.Fill,
            .BackColor = LightBg,
            .Padding = New Padding(10, 8, 10, 8),
            .Margin = New Padding(0)
        }

        ' TableLayoutPanel pour info + boutons - 1 colonne, 2 lignes
        Dim actionsLayout = New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .BackColor = LightBg,
            .CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
            .Padding = New Padding(0),
            .Margin = New Padding(0)
        }

        ' Configuration des lignes - Info prend tout l'espace, boutons fixes
        actionsLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))  ' Info (étirable)
        actionsLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 45))  ' Boutons (fixes)

        actionsLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))

        ' Panel d'information (prend tout l'espace vertical disponible)
        _infoPanel = New Panel With {
            .Dock = DockStyle.Fill,
            .BackColor = SecondaryBg,
            .Padding = New Padding(15),
            .Margin = New Padding(0, 0, 0, 10),
            .AutoScroll = True
        }

        _infoLabel = New Label With {
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 9),
            .ForeColor = Color.White,
            .Text = "Sélectionnez un élément dans l'arborescence pour voir les détails." & Environment.NewLine & Environment.NewLine & "💡 Astuce : Glissez la barre horizontale pour ajuster la taille de cette zone.",
            .AutoSize = False,
            .TextAlign = ContentAlignment.TopLeft
        }
        _infoPanel.Controls.Add(_infoLabel)

        ' Panel des boutons
        Dim buttonsPanel = New Panel With {
            .Dock = DockStyle.Fill,
            .BackColor = LightBg,
            .Margin = New Padding(0)
        }

        ' TableLayoutPanel pour les boutons - 4 colonnes
        Dim buttonsLayout = New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 4,
            .RowCount = 1,
            .BackColor = LightBg,
            .CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
            .Padding = New Padding(0),
            .Margin = New Padding(0)
        }

        ' 4 colonnes égales
        For i As Integer = 0 To 3
            buttonsLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25))
        Next
        buttonsLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

        ' Créer les boutons
        _btnCreateRoom = CreateActionButton("🚪 Créer Pièce", SuccessGreen, AddressOf BtnCreateRoom_Click)
        _btnRename = CreateActionButton("✏️ Renommer", WarningOrange, AddressOf BtnRename_Click)
        _btnMoveDevice = CreateActionButton("➡️ Déplacer", ActiveBlue, AddressOf BtnMoveDevice_Click)
        _btnDelete = CreateActionButton("🗑️ Supprimer", CriticalRed, AddressOf BtnDelete_Click)

        buttonsLayout.Controls.Add(_btnCreateRoom, 0, 0)
        buttonsLayout.Controls.Add(_btnRename, 1, 0)
        buttonsLayout.Controls.Add(_btnMoveDevice, 2, 0)
        buttonsLayout.Controls.Add(_btnDelete, 3, 0)

        buttonsPanel.Controls.Add(buttonsLayout)

        actionsLayout.Controls.Add(_infoPanel, 0, 0)
        actionsLayout.Controls.Add(buttonsPanel, 0, 1)

        panel.Controls.Add(actionsLayout)

        ' État initial
        UpdateButtonStates(Nothing)

        Return panel
    End Function

    Private Function CreateActionButton(text As String, color As Color, handler As EventHandler) As Button
        Dim btn = New Button With {
            .Text = text,
            .Font = New Font("Segoe UI", 9, FontStyle.Bold),
            .ForeColor = Color.White,
            .BackColor = color,
            .FlatStyle = FlatStyle.Flat,
            .Dock = DockStyle.Fill,
            .Margin = New Padding(2),
            .Cursor = Cursors.Hand
        }
        btn.FlatAppearance.BorderSize = 0
        AddHandler btn.Click, handler
        Return btn
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
            .AutoSize = False,
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(15, 0, 0, 0)
        }
        panel.Controls.Add(_statusLabel)

        Return panel
    End Function

    Private Function CreateImageList() As ImageList
        Dim imageList As New ImageList With {
            .ImageSize = New Size(16, 16),
            .ColorDepth = ColorDepth.Depth32Bit
        }

        ' Créer des icônes simples (en attendant de vraies icônes)
        imageList.Images.Add("home", CreateColorIcon(ActiveBlue))
        imageList.Images.Add("room", CreateColorIcon(SuccessGreen))
        imageList.Images.Add("device", CreateColorIcon(WarningOrange))

        Return imageList
    End Function

    Private Function CreateColorIcon(color As Color) As Bitmap
        Dim bmp As New Bitmap(16, 16)
        Using g As Graphics = Graphics.FromImage(bmp)
            g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
            Using brush As New SolidBrush(color)
                g.FillEllipse(brush, 2, 2, 12, 12)
            End Using
        End Using
        Return bmp
    End Function

    Private Sub CreateContextMenu()
        _contextMenu = New ContextMenuStrip With {
            .BackColor = Color.White,
            .Font = New Font("Segoe UI", 9)
        }

        ' Menu items seront ajoutés dynamiquement selon le type de noeud
    End Sub

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        LoadDataAsync()
    End Sub
#End Region

#Region "Chargement des données"
    Private Async Function LoadDataAsync() As Task
        Try
            UpdateStatus("Chargement de la structure...")
            _treeView.Nodes.Clear()

            ' Charger les homes (rapide - juste la structure)
            Dim homes = Await _apiClient.GetHomesAsync()

            If homes Is Nothing OrElse homes.Count = 0 Then
                UpdateStatus("Aucun logement trouvé")
                _treeView.Nodes.Add("Aucun logement configuré. Cliquez sur 'Créer un Logement' pour commencer.")
                Return
            End If

            ' Construire l'arborescence (utilise les données pré-chargées pour les appareils)
            Dim homeCount = 0
            For Each home In homes
                homeCount += 1
                UpdateStatus("Chargement logement " & homeCount.ToString() & "/" & homes.Count.ToString() & "...")
                Await AddHomeToTree(home)
            Next

            ' Garder l'arborescence fermée au démarrage pour une meilleure lisibilité
            _treeView.CollapseAll()

            ' Message de statut détaillé
            Dim totalDevices = _devicesData.Count
            Dim statusMsg = homes.Count.ToString() & " logement(s), " & totalDevices.ToString() & " appareil(s)"
            If _preloadedDevices IsNot Nothing AndAlso _preloadedDevices.Count > 0 Then
                statusMsg &= " (mode rapide - données en cache)"
            End If
            UpdateStatus(statusMsg)

        Catch ex As Exception
            MessageBox.Show("Erreur lors du chargement des données : " & ex.Message,
                          "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
            UpdateStatus("Erreur de chargement")
        End Try
    End Function

    Private Async Function AddHomeToTree(home As JToken) As Task
        Dim homeId = GetJsonString(home, "home_id")
        Dim homeName = GetJsonString(home, "name")

        If String.IsNullOrEmpty(homeId) Then Return

        ' Stocker les données
        _homesData(homeId) = home

        ' Créer le noeud home
        Dim homeNode = New TreeNode(homeName) With {
            .Tag = New NodeData With {.Type = NodeType.Home, .Id = homeId, .Name = homeName},
            .ImageIndex = 0,
            .SelectedImageIndex = 0
        }

        _treeView.Nodes.Add(homeNode)

        ' Charger les rooms
        Dim rooms = Await _apiClient.GetRoomsAsync(homeId)

        For Each room In rooms
            Await AddRoomToNode(homeNode, homeId, room)
        Next

        ' Charger les appareils non assignés à une room (directement sous le home)
        Await AddUnassignedDevicesToHome(homeNode, homeId)
    End Function

    Private Async Function AddRoomToNode(homeNode As TreeNode, homeId As String, room As JToken) As Task
        Dim roomId = GetJsonString(room, "room_id")
        Dim roomName = GetJsonString(room, "name")

        If String.IsNullOrEmpty(roomId) Then Return

        ' Stocker les données
        _roomsData(roomId) = room

        ' Créer le noeud room
        Dim roomNode = New TreeNode(roomName) With {
            .Tag = New NodeData With {.Type = NodeType.Room, .Id = roomId, .Name = roomName, .HomeId = homeId},
            .ImageIndex = 1,
            .SelectedImageIndex = 1
        }

        homeNode.Nodes.Add(roomNode)

        ' Charger les appareils de cette room
        Await AddDevicesToRoom(roomNode, roomId)
    End Function

    Private Async Function AddDevicesToRoom(roomNode As TreeNode, roomId As String) As Task
        Try
            ' OPTIMISATION: Utiliser les données pré-chargées si disponibles
            Dim devicesToUse As IEnumerable(Of DeviceInfo)

            If _preloadedDevices IsNot Nothing AndAlso _preloadedDevices.Count > 0 Then
                ' Utiliser les données déjà en cache (rapide, pas d'appel API)
                devicesToUse = _preloadedDevices.Where(Function(d) d.RoomId = roomId)
            Else
                ' Fallback: charger depuis l'API (lent)
                Dim allDevices = Await _apiClient.GetAllDevicesAsync()
                devicesToUse = allDevices.Where(Function(d) d.RoomId = roomId)
            End If

            For Each device In devicesToUse
                AddDeviceToNode(roomNode, device)
            Next
        Catch ex As Exception
            ' Ignorer les erreurs de chargement des appareils
        End Try
    End Function

    Private Async Function AddUnassignedDevicesToHome(homeNode As TreeNode, homeId As String) As Task
        Try
            ' Récupérer tous les devices
            Dim allDevices As List(Of DeviceInfo)

            If _preloadedDevices IsNot Nothing AndAlso _preloadedDevices.Count > 0 Then
                allDevices = _preloadedDevices
            Else
                allDevices = Await _apiClient.GetAllDevicesAsync()
            End If

            ' Filtrer les devices qui appartiennent à ce home mais n'ont pas de room
            Dim unassignedDevices = allDevices.Where(Function(d) d.HomeId = homeId AndAlso String.IsNullOrEmpty(d.RoomId)).ToList()

            If unassignedDevices.Count > 0 Then
                ' Créer un noeud pour les appareils non assignés
                Dim unassignedNode = New TreeNode("📦 Appareils non assignés (" & unassignedDevices.Count.ToString() & ")") With {
                    .Tag = New NodeData With {
                        .Type = NodeType.Room,
                        .Id = "",
                        .Name = "Appareils non assignés",
                        .HomeId = homeId
                    },
                    .ImageIndex = 1,
                    .SelectedImageIndex = 1,
                    .ForeColor = Color.Gray
                }

                ' Ajouter les devices non assignés
                For Each device In unassignedDevices
                    AddDeviceToNode(unassignedNode, device)
                Next

                homeNode.Nodes.Add(unassignedNode)
            End If
        Catch ex As Exception
            ' Ignorer les erreurs
        End Try
    End Function

    Private Sub AddDeviceToNode(roomNode As TreeNode, device As DeviceInfo)
        If device Is Nothing OrElse String.IsNullOrEmpty(device.Id) Then Return

        ' Stocker les données
        _devicesData(device.Id) = device

        ' Récupérer la catégorie avec icône
        Dim deviceCategories = TuyaDeviceCategories.GetInstance()
        Dim categoryName = deviceCategories.GetDeviceName(device.Category)
        Dim categoryIcon = deviceCategories.GetDeviceIcon(device.Category)

        ' Créer le noeud device
        Dim displayName = categoryIcon & " " & device.Name
        If Not String.IsNullOrEmpty(categoryName) Then
            displayName &= " (" & categoryName & ")"
        End If

        Dim deviceNode = New TreeNode(displayName) With {
            .Tag = New NodeData With {
                .Type = NodeType.Device,
                .Id = device.Id,
                .Name = device.Name,
                .RoomId = device.RoomId,
                .HomeId = device.HomeId
            },
            .ImageIndex = 2,
            .SelectedImageIndex = 2
        }

        roomNode.Nodes.Add(deviceNode)
    End Sub
#End Region

#Region "Gestion des événements TreeView"
    Private Sub TreeView_AfterSelect(sender As Object, e As TreeViewEventArgs)
        UpdateButtonStates(e.Node)
        UpdateInfoPanel(e.Node)
    End Sub

    Private Sub TreeView_NodeMouseClick(sender As Object, e As TreeNodeMouseClickEventArgs)
        If e.Button = MouseButtons.Right Then
            _treeView.SelectedNode = e.Node
            ShowContextMenuForNode(e.Node, e.Location)
        End If
    End Sub

    Private Sub ShowContextMenuForNode(node As TreeNode, location As Point)
        If node Is Nothing OrElse node.Tag Is Nothing Then Return

        _contextMenu.Items.Clear()

        Dim nodeData = TryCast(node.Tag, NodeData)
        If nodeData Is Nothing Then Return

        Select Case nodeData.Type
            Case NodeType.Home
                _contextMenu.Items.Add(CreateMenuItem("🏠 Créer une pièce", AddressOf BtnCreateRoom_Click))
                _contextMenu.Items.Add(CreateMenuItem("✏️ Renommer le logement", AddressOf BtnRename_Click))
                _contextMenu.Items.Add(New ToolStripSeparator())
                _contextMenu.Items.Add(CreateMenuItem("🗑️ Supprimer le logement", AddressOf BtnDelete_Click, Color.Red))

            Case NodeType.Room
                _contextMenu.Items.Add(CreateMenuItem("✏️ Renommer la pièce", AddressOf BtnRename_Click))
                _contextMenu.Items.Add(New ToolStripSeparator())
                _contextMenu.Items.Add(CreateMenuItem("🗑️ Supprimer la pièce", AddressOf BtnDelete_Click, Color.Red))

            Case NodeType.Device
                _contextMenu.Items.Add(CreateMenuItem("✏️ Renommer l'appareil", AddressOf BtnRename_Click))
                _contextMenu.Items.Add(CreateMenuItem("➡️ Déplacer vers une autre pièce", AddressOf BtnMoveDevice_Click))
                _contextMenu.Items.Add(New ToolStripSeparator())
                _contextMenu.Items.Add(CreateMenuItem("🗑️ Supprimer l'appareil", AddressOf BtnDelete_Click, Color.Red))
        End Select

        _contextMenu.Show(_treeView, location)
    End Sub

    ''' <summary>
    ''' Démarre le drag d'un noeud (uniquement les devices)
    ''' </summary>
    Private Sub TreeView_ItemDrag(sender As Object, e As ItemDragEventArgs)
        If e.Item Is Nothing Then Return

        Dim node = TryCast(e.Item, TreeNode)
        If node Is Nothing OrElse node.Tag Is Nothing Then Return

        Dim nodeData = TryCast(node.Tag, NodeData)
        If nodeData Is Nothing OrElse nodeData.Type <> NodeType.Device Then Return

        ' Démarrer le drag seulement pour les devices
        _treeView.DoDragDrop(node, DragDropEffects.Move)
    End Sub

    ''' <summary>
    ''' Accepte l'entrée de données drag
    ''' </summary>
    Private Sub TreeView_DragEnter(sender As Object, e As DragEventArgs)
        e.Effect = DragDropEffects.Move
    End Sub

    ''' <summary>
    ''' Vérifie si le drop est valide pendant le survol
    ''' </summary>
    Private Sub TreeView_DragOver(sender As Object, e As DragEventArgs)
        ' Obtenir le noeud sous le curseur
        Dim targetPoint = _treeView.PointToClient(New Point(e.X, e.Y))
        Dim targetNode = _treeView.GetNodeAt(targetPoint)

        ' Par défaut, pas de drop
        e.Effect = DragDropEffects.None

        ' Vérifier si on a un noeud source valide
        If Not e.Data.GetDataPresent(GetType(TreeNode)) Then Return
        Dim sourceNode = TryCast(e.Data.GetData(GetType(TreeNode)), TreeNode)
        If sourceNode Is Nothing OrElse sourceNode.Tag Is Nothing Then Return

        Dim sourceData = TryCast(sourceNode.Tag, NodeData)
        If sourceData Is Nothing OrElse sourceData.Type <> NodeType.Device Then Return

        ' Vérifier si on peut dropper sur la cible
        If targetNode IsNot Nothing AndAlso targetNode.Tag IsNot Nothing Then
            Dim targetData = TryCast(targetNode.Tag, NodeData)
            If targetData IsNot Nothing Then
                ' Autoriser le drop sur une room (différente de la room actuelle)
                If targetData.Type = NodeType.Room AndAlso targetData.Id <> sourceData.RoomId Then
                    e.Effect = DragDropEffects.Move
                    _treeView.SelectedNode = targetNode ' Highlight la cible
                    ' Autoriser le drop sur un home (pour retirer de la room)
                ElseIf targetData.Type = NodeType.Home AndAlso Not String.IsNullOrEmpty(sourceData.RoomId) Then
                    e.Effect = DragDropEffects.Move
                    _treeView.SelectedNode = targetNode
                End If
            End If
        End If
    End Sub

    ''' <summary>
    ''' Effectue le déplacement du device
    ''' </summary>
    Private Async Sub TreeView_DragDrop(sender As Object, e As DragEventArgs)
        Try
            ' Récupérer le noeud source (device)
            If Not e.Data.GetDataPresent(GetType(TreeNode)) Then Return
            Dim sourceNode = TryCast(e.Data.GetData(GetType(TreeNode)), TreeNode)
            If sourceNode Is Nothing OrElse sourceNode.Tag Is Nothing Then Return

            Dim sourceData = TryCast(sourceNode.Tag, NodeData)
            If sourceData Is Nothing OrElse sourceData.Type <> NodeType.Device Then Return

            ' Récupérer le noeud cible
            Dim targetPoint = _treeView.PointToClient(New Point(e.X, e.Y))
            Dim targetNode = _treeView.GetNodeAt(targetPoint)
            If targetNode Is Nothing OrElse targetNode.Tag Is Nothing Then Return

            Dim targetData = TryCast(targetNode.Tag, NodeData)
            If targetData Is Nothing Then Return

            Dim success As Boolean = False
            Dim oldParent = sourceNode.Parent

            ' CAS 1: Drop sur une ROOM → Déplacer vers cette room
            If targetData.Type = NodeType.Room AndAlso targetData.Id <> sourceData.RoomId Then
                UpdateStatus("Déplacement de l'appareil...")
                Console.WriteLine($"🖱️ DRAG & DROP: {sourceData.Name} → {targetData.Name}")

                success = Await _apiClient.MoveDeviceToRoomAsync(sourceData.Id, targetData.Id)

                If success Then
                    ' Mettre à jour le cache
                    If _preloadedDevices IsNot Nothing Then
                        Dim deviceToUpdate = _preloadedDevices.FirstOrDefault(Function(d) d.Id = sourceData.Id)
                        If deviceToUpdate IsNot Nothing Then
                            deviceToUpdate.RoomId = targetData.Id
                        End If
                    End If

                    ' Déplacer visuellement
                    If oldParent IsNot Nothing Then
                        oldParent.Nodes.Remove(sourceNode)
                    End If
                    targetNode.Nodes.Add(sourceNode)
                    targetNode.Expand()
                    sourceData.RoomId = targetData.Id
                    _treeView.SelectedNode = sourceNode
                    sourceNode.EnsureVisible()

                    UpdateStatus("Appareil déplacé")
                End If

            ' CAS 2: Drop sur un HOME → Retirer de la room (mettre à la racine)
            ElseIf targetData.Type = NodeType.Home AndAlso Not String.IsNullOrEmpty(sourceData.RoomId) Then
                UpdateStatus("Retrait de l'appareil de sa pièce...")
                Console.WriteLine($"🖱️ DRAG & DROP: {sourceData.Name} → Racine de {targetData.Name}")

                success = Await _apiClient.RemoveDeviceFromRoomAsync(sourceData.Id)

                If success Then
                    ' Mettre à jour le cache
                    If _preloadedDevices IsNot Nothing Then
                        Dim deviceToUpdate = _preloadedDevices.FirstOrDefault(Function(d) d.Id = sourceData.Id)
                        If deviceToUpdate IsNot Nothing Then
                            deviceToUpdate.RoomId = Nothing
                        End If
                    End If

                    ' Déplacer vers "Appareils non assignés"
                    If oldParent IsNot Nothing Then
                        oldParent.Nodes.Remove(sourceNode)
                    End If

                    ' Trouver ou créer le noeud "Appareils non assignés"
                    Dim unassignedNode As TreeNode = Nothing
                    For Each node As TreeNode In targetNode.Nodes
                        If node.Tag IsNot Nothing Then
                            Dim roomData = TryCast(node.Tag, NodeData)
                            If roomData IsNot Nothing AndAlso roomData.Type = NodeType.Room AndAlso String.IsNullOrEmpty(roomData.Id) Then
                                unassignedNode = node
                                Exit For
                            End If
                        End If
                    Next

                    If unassignedNode Is Nothing Then
                        unassignedNode = New TreeNode("📦 Appareils non assignés (1)") With {
                            .Tag = New NodeData With {
                                .Type = NodeType.Room,
                                .Id = "",
                                .Name = "Appareils non assignés",
                                .HomeId = sourceData.HomeId
                            },
                            .ImageIndex = 1,
                            .SelectedImageIndex = 1,
                            .ForeColor = Color.Gray
                        }
                        targetNode.Nodes.Add(unassignedNode)
                    End If

                    unassignedNode.Nodes.Add(sourceNode)
                    unassignedNode.Expand()
                    unassignedNode.Text = $"📦 Appareils non assignés ({unassignedNode.Nodes.Count})"
                    sourceData.RoomId = ""
                    _treeView.SelectedNode = sourceNode
                    sourceNode.EnsureVisible()

                    UpdateStatus("Appareil retiré de sa pièce")
                End If
            End If

            If Not success Then
                UpdateStatus("Erreur lors du déplacement")
            End If

        Catch ex As Exception
            Console.WriteLine($"❌ Erreur drag & drop: {ex.Message}")
            UpdateStatus("Erreur")
        End Try
    End Sub

    Private Function CreateMenuItem(text As String, handler As EventHandler, Optional foreColor As Color = Nothing) As ToolStripMenuItem
        Dim item = New ToolStripMenuItem(text)
        If foreColor <> Nothing AndAlso foreColor <> Color.Empty Then
            item.ForeColor = foreColor
        End If
        AddHandler item.Click, handler
        Return item
    End Function

    Private Sub UpdateButtonStates(node As TreeNode)
        If node Is Nothing OrElse node.Tag Is Nothing Then
            _btnCreateRoom.Enabled = False
            _btnRename.Enabled = False
            _btnDelete.Enabled = False
            _btnMoveDevice.Enabled = False
            Return
        End If

        Dim nodeData = TryCast(node.Tag, NodeData)
        If nodeData Is Nothing Then Return

        Select Case nodeData.Type
            Case NodeType.Home
                _btnCreateRoom.Enabled = True
                _btnRename.Enabled = True
                _btnDelete.Enabled = True
                _btnMoveDevice.Enabled = False

            Case NodeType.Room
                _btnCreateRoom.Enabled = False
                _btnRename.Enabled = True
                _btnDelete.Enabled = True
                _btnMoveDevice.Enabled = False

            Case NodeType.Device
                _btnCreateRoom.Enabled = False
                _btnRename.Enabled = True
                _btnDelete.Enabled = True
                _btnMoveDevice.Enabled = True

            Case Else
                _btnCreateRoom.Enabled = False
                _btnRename.Enabled = False
                _btnDelete.Enabled = False
                _btnMoveDevice.Enabled = False
        End Select
    End Sub

    Private Sub UpdateInfoPanel(node As TreeNode)
        If node Is Nothing OrElse node.Tag Is Nothing Then
            _infoLabel.Text = "Sélectionnez un élément dans l'arborescence pour voir les détails."
            Return
        End If

        Dim nodeData = TryCast(node.Tag, NodeData)
        If nodeData Is Nothing Then Return

        Dim info As New System.Text.StringBuilder()

        Select Case nodeData.Type
            Case NodeType.Home
                info.AppendLine("═══ 🏠 LOGEMENT ═══")
                info.AppendLine("")
                info.AppendLine("Nom: " & nodeData.Name)
                info.AppendLine("ID: " & nodeData.Id)
                info.AppendLine("")
                Dim roomCount = node.Nodes.Count
                Dim deviceCountInHome = 0
                For Each roomNode As TreeNode In node.Nodes
                    deviceCountInHome += roomNode.Nodes.Count
                Next
                info.AppendLine("📊 Statistiques:")
                info.AppendLine("  • " & roomCount.ToString() & " pièce(s)")
                info.AppendLine("  • " & deviceCountInHome.ToString() & " appareil(s)")

            Case NodeType.Room
                info.AppendLine("═══ 🚪 PIÈCE ═══")
                info.AppendLine("")
                info.AppendLine("Nom: " & nodeData.Name)
                info.AppendLine("ID: " & nodeData.Id)
                info.AppendLine("")
                Dim deviceCount = node.Nodes.Count
                info.AppendLine("📊 Appareils: " & deviceCount.ToString())

            Case NodeType.Device
                If _devicesData.ContainsKey(nodeData.Id) Then
                    Dim device = _devicesData(nodeData.Id)
                    Dim deviceCategories = TuyaDeviceCategories.GetInstance()
                    Dim categoryName = deviceCategories.GetDeviceName(device.Category)
                    Dim categoryIcon = deviceCategories.GetDeviceIcon(device.Category)

                    info.AppendLine("═══ " & categoryIcon & " APPAREIL ═══")
                    info.AppendLine("")
                    info.AppendLine("Nom: " & device.Name)
                    info.AppendLine("Catégorie: " & categoryName)
                    info.AppendLine("Produit: " & device.ProductName)
                    info.AppendLine("")
                    info.AppendLine("État: " & If(device.IsOnline, "✅ En ligne", "❌ Hors ligne"))
                    info.AppendLine("")
                    info.AppendLine("ID: " & device.Id)
                End If
        End Select

        _infoLabel.Text = info.ToString()
    End Sub
#End Region

#Region "Gestionnaires d'événements - Boutons"
    Private Async Sub BtnRefresh_Click(sender As Object, e As EventArgs)
        Try
            _btnRefresh.Enabled = False
            _btnRefresh.Text = "⏳ Chargement..."
            UpdateStatus("Rafraîchissement des données depuis l'API...")

            ' Recharger VRAIMENT depuis l'API (pas depuis le cache)
            _preloadedDevices = Await _apiClient.GetAllDevicesAsync()

            ' Recharger l'affichage
            Await LoadDataAsync()

            _btnRefresh.Text = "🔄 Rafraîchir"
        Catch ex As Exception
            MessageBox.Show("Erreur lors du rafraîchissement : " & ex.Message,
                          "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            _btnRefresh.Enabled = True
        End Try
    End Sub

    Private Async Sub BtnCreateHome_Click(sender As Object, e As EventArgs)
        Try
            Dim homeName = InputBox("Entrez le nom du nouveau logement :", "Créer un logement", "Mon logement")
            If String.IsNullOrWhiteSpace(homeName) Then Return

            UpdateStatus("Création du logement...")
            Dim homeId = Await _apiClient.CreateHomeAsync(homeName)

            If Not String.IsNullOrEmpty(homeId) Then
                MessageBox.Show("Logement '" & homeName & "' créé avec succès!", "Succès",
                              MessageBoxButtons.OK, MessageBoxIcon.Information)
                Await LoadDataAsync()
            Else
                MessageBox.Show("Échec de la création du logement.", "Erreur",
                              MessageBoxButtons.OK, MessageBoxIcon.Error)
                UpdateStatus("Erreur")
            End If
        Catch ex As Exception
            MessageBox.Show("Erreur : " & ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
            UpdateStatus("Erreur")
        End Try
    End Sub

    Private Async Sub BtnCreateRoom_Click(sender As Object, e As EventArgs)
        Try
            Dim selectedNode = _treeView.SelectedNode
            Dim homeId As String = Nothing

            ' Déterminer le homeId
            If selectedNode IsNot Nothing AndAlso selectedNode.Tag IsNot Nothing Then
                Dim nodeData = TryCast(selectedNode.Tag, NodeData)
                If nodeData IsNot Nothing Then
                    If nodeData.Type = NodeType.Home Then
                        homeId = nodeData.Id
                    ElseIf nodeData.Type = NodeType.Room Then
                        homeId = nodeData.HomeId
                    End If
                End If
            End If

            ' Si pas de home sélectionné, demander à l'utilisateur de choisir
            If String.IsNullOrEmpty(homeId) Then
                homeId = SelectHomeDialog()
                If String.IsNullOrEmpty(homeId) Then Return
            End If

            Dim roomName = InputBox("Entrez le nom de la nouvelle pièce :", "Créer une pièce", "Salon")
            If String.IsNullOrWhiteSpace(roomName) Then Return

            UpdateStatus("Création de la pièce...")
            Console.WriteLine($"🔍 DEBUG: Création room '{roomName}' dans home '{homeId}'")

            Dim roomId = Await _apiClient.CreateRoomAsync(homeId, roomName)

            If Not String.IsNullOrEmpty(roomId) Then
                Console.WriteLine($"✅ DEBUG: Room créée avec ID '{roomId}'")
                MessageBox.Show("Pièce '" & roomName & "' créée avec succès!" & Environment.NewLine & "ID: " & roomId, "Succès",
                              MessageBoxButtons.OK, MessageBoxIcon.Information)
                Await LoadDataAsync()
            Else
                Console.WriteLine("❌ DEBUG: CreateRoomAsync a retourné Nothing/vide")
                MessageBox.Show("Échec de la création de la pièce." & Environment.NewLine & Environment.NewLine &
                              "Vérifiez la console pour plus de détails.", "Erreur",
                              MessageBoxButtons.OK, MessageBoxIcon.Error)
                UpdateStatus("Erreur")
            End If
        Catch ex As Exception
            Console.WriteLine($"❌ DEBUG: Exception lors de la création de la room: {ex.Message}")
            Console.WriteLine(ex.StackTrace)
            MessageBox.Show("Erreur : " & ex.Message & Environment.NewLine & Environment.NewLine &
                          "Détails dans la console.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
            UpdateStatus("Erreur")
        End Try
    End Sub

    Private Async Sub BtnRename_Click(sender As Object, e As EventArgs)
        Try
            Dim selectedNode = _treeView.SelectedNode
            If selectedNode Is Nothing OrElse selectedNode.Tag Is Nothing Then Return

            Dim nodeData = TryCast(selectedNode.Tag, NodeData)
            If nodeData Is Nothing Then Return

            Dim newName = InputBox("Entrez le nouveau nom :", "Renommer", nodeData.Name)
            If String.IsNullOrWhiteSpace(newName) OrElse newName = nodeData.Name Then Return

            UpdateStatus("Renommage...")
            Dim success = False

            Select Case nodeData.Type
                Case NodeType.Home
                    success = Await _apiClient.RenameHomeAsync(nodeData.Id, newName)

                Case NodeType.Room
                    success = Await _apiClient.RenameRoomAsync(nodeData.HomeId, nodeData.Id, newName)

                Case NodeType.Device
                    success = Await _apiClient.RenameDeviceAsync(nodeData.Id, newName)
            End Select

            If success Then
                MessageBox.Show("Renommage réussi!", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Await LoadDataAsync()
            Else
                MessageBox.Show("Échec du renommage.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
                UpdateStatus("Erreur")
            End If
        Catch ex As Exception
            MessageBox.Show("Erreur : " & ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
            UpdateStatus("Erreur")
        End Try
    End Sub

    Private Async Sub BtnDelete_Click(sender As Object, e As EventArgs)
        Try
            Dim selectedNode = _treeView.SelectedNode
            If selectedNode Is Nothing OrElse selectedNode.Tag Is Nothing Then Return

            Dim nodeData = TryCast(selectedNode.Tag, NodeData)
            If nodeData Is Nothing Then Return

            Dim itemType = ""
            Select Case nodeData.Type
                Case NodeType.Home
                    itemType = "logement"
                Case NodeType.Room
                    itemType = "pièce"
                Case NodeType.Device
                    itemType = "appareil"
            End Select

            Dim result = MessageBox.Show(
                "Êtes-vous sûr de vouloir supprimer ce " & itemType & " ?" & Environment.NewLine &
                "Nom: " & nodeData.Name & Environment.NewLine & Environment.NewLine &
                "Cette action est irréversible!",
                "Confirmation de suppression",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning)

            If result <> DialogResult.Yes Then Return

            UpdateStatus("Suppression...")
            Dim success = False

            Select Case nodeData.Type
                Case NodeType.Home
                    success = Await _apiClient.DeleteHomeAsync(nodeData.Id)

                Case NodeType.Room
                    success = Await _apiClient.DeleteRoomAsync(nodeData.HomeId, nodeData.Id)

                Case NodeType.Device
                    success = Await _apiClient.DeleteDeviceAsync(nodeData.Id)
            End Select

            If success Then
                MessageBox.Show("Suppression réussie!", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Await LoadDataAsync()
            Else
                MessageBox.Show("Échec de la suppression.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
                UpdateStatus("Erreur")
            End If
        Catch ex As Exception
            MessageBox.Show("Erreur : " & ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
            UpdateStatus("Erreur")
        End Try
    End Sub

    Private Async Sub BtnMoveDevice_Click(sender As Object, e As EventArgs)
        Try
            Dim selectedNode = _treeView.SelectedNode
            If selectedNode Is Nothing OrElse selectedNode.Tag Is Nothing Then Return

            Dim nodeData = TryCast(selectedNode.Tag, NodeData)
            If nodeData Is Nothing OrElse nodeData.Type <> NodeType.Device Then Return

            ' Vérifier que le homeId est disponible
            If String.IsNullOrEmpty(nodeData.HomeId) Then
                MessageBox.Show("Impossible de déterminer le logement de l'appareil.", "Erreur",
                              MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            ' Sélectionner la destination
            Dim targetRoomId = SelectRoomDialog(nodeData.RoomId)
            If targetRoomId Is Nothing Then Return ' L'utilisateur a annulé

            Dim success As Boolean
            Dim deviceNode = selectedNode
            Dim oldParent = deviceNode.Parent

            ' CAS 1: Retirer de la room (déplacer à la racine du home)
            If targetRoomId = "" Then
                ' Vérifier que le device est bien dans une room
                If String.IsNullOrEmpty(nodeData.RoomId) Then
                    MessageBox.Show("Cet appareil est déjà à la racine du logement (sans pièce).", "Information",
                                  MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Return
                End If

                UpdateStatus("Retrait de l'appareil de sa pièce...")
                Console.WriteLine($"🔍 DEBUG: Retrait device {nodeData.Id} de sa room {nodeData.RoomId} (vers racine home {nodeData.HomeId})")

                ' Appeler l'API pour retirer le device de sa room en passant les infos nécessaires
                success = Await _apiClient.RemoveDeviceFromRoomAsync(nodeData.Id)

                If success Then
                    Console.WriteLine("⚡ Mise à jour locale du cache (instantané)...")

                    ' Mettre à jour le cache local
                    If _preloadedDevices IsNot Nothing Then
                        Dim deviceToUpdate = _preloadedDevices.FirstOrDefault(Function(d) d.Id = nodeData.Id)
                        If deviceToUpdate IsNot Nothing Then
                            deviceToUpdate.RoomId = Nothing ' Plus de room
                            Console.WriteLine($"✅ Cache mis à jour: device {nodeData.Id} -> SANS room")
                        End If
                    End If

                    ' Déplacer visuellement vers la section "Appareils non assignés"
                    Console.WriteLine($"🔍 Recherche du noeud 'Appareils non assignés' dans home {nodeData.HomeId}")

                    ' Trouver le noeud du home
                    Dim homeNode As TreeNode = Nothing
                    For Each node As TreeNode In _treeView.Nodes
                        If node.Tag IsNot Nothing Then
                            Dim homeData = TryCast(node.Tag, NodeData)
                            If homeData IsNot Nothing AndAlso homeData.Type = NodeType.Home AndAlso homeData.Id = nodeData.HomeId Then
                                homeNode = node
                                Exit For
                            End If
                        End If
                    Next

                    If homeNode IsNot Nothing Then
                        ' Retirer le device de son parent actuel
                        If oldParent IsNot Nothing Then
                            Console.WriteLine($"🔄 Retrait du device de: {oldParent.Text}")
                            oldParent.Nodes.Remove(deviceNode)
                        End If

                        ' Chercher ou créer le noeud "Appareils non assignés"
                        Dim unassignedNode As TreeNode = Nothing
                        For Each node As TreeNode In homeNode.Nodes
                            If node.Tag IsNot Nothing Then
                                Dim roomData = TryCast(node.Tag, NodeData)
                                If roomData IsNot Nothing AndAlso roomData.Type = NodeType.Room AndAlso String.IsNullOrEmpty(roomData.Id) Then
                                    unassignedNode = node
                                    Exit For
                                End If
                            End If
                        Next

                        ' Si le noeud "Appareils non assignés" n'existe pas, le créer
                        If unassignedNode Is Nothing Then
                            Console.WriteLine("📦 Création du noeud 'Appareils non assignés'")
                            unassignedNode = New TreeNode("📦 Appareils non assignés (1)") With {
                                .Tag = New NodeData With {
                                    .Type = NodeType.Room,
                                    .Id = "",
                                    .Name = "Appareils non assignés",
                                    .HomeId = nodeData.HomeId
                                },
                                .ImageIndex = 1,
                                .SelectedImageIndex = 1,
                                .ForeColor = Color.Gray
                            }
                            homeNode.Nodes.Add(unassignedNode)
                        End If

                        ' Ajouter le device au noeud "Appareils non assignés"
                        Console.WriteLine($"🔄 Ajout du device à: {unassignedNode.Text}")
                        unassignedNode.Nodes.Add(deviceNode)
                        unassignedNode.Expand()

                        ' Mettre à jour le compteur dans le texte
                        unassignedNode.Text = $"📦 Appareils non assignés ({unassignedNode.Nodes.Count})"

                        ' Mettre à jour le NodeData du device
                        If deviceNode.Tag IsNot Nothing Then
                            Dim deviceNodeData = TryCast(deviceNode.Tag, NodeData)
                            If deviceNodeData IsNot Nothing Then
                                deviceNodeData.RoomId = ""
                                Console.WriteLine("✅ NodeData.RoomId mis à jour: (vide)")
                            End If
                        End If

                        _treeView.SelectedNode = deviceNode
                        deviceNode.EnsureVisible()
                        Console.WriteLine("✅ Arborescence mise à jour visuellement")
                    End If

                    UpdateStatus("Appareil retiré de sa pièce")
                    MessageBox.Show("Appareil retiré de sa pièce avec succès!", "Succès",
                                  MessageBoxButtons.OK, MessageBoxIcon.Information)
                End If

            ' CAS 2: Déplacer vers une room
            Else
                UpdateStatus("Déplacement de l'appareil...")
                Console.WriteLine($"🔍 DEBUG: Déplacement device {nodeData.Id} vers room {targetRoomId} dans home {nodeData.HomeId}")

                ' Appeler l'API en passant le cache pour éviter un rechargement complet
                success = Await _apiClient.MoveDeviceToRoomAsync(nodeData.Id, targetRoomId)

                If success Then
                    Console.WriteLine("⚡ Mise à jour locale du cache (instantané)...")

                    ' Mettre à jour le cache local
                    If _preloadedDevices IsNot Nothing Then
                        Dim deviceToUpdate = _preloadedDevices.FirstOrDefault(Function(d) d.Id = nodeData.Id)
                        If deviceToUpdate IsNot Nothing Then
                            deviceToUpdate.RoomId = targetRoomId
                            Console.WriteLine($"✅ Cache mis à jour: device {nodeData.Id} -> room {targetRoomId}")
                        End If
                    End If

                    ' Déplacer visuellement le noeud dans l'arborescence
                    Console.WriteLine($"🔍 Recherche du noeud de la room cible: {targetRoomId}")

                    ' Retirer le device de son parent actuel
                    If oldParent IsNot Nothing Then
                        Console.WriteLine($"🔄 Retrait du device de: {oldParent.Text}")
                        oldParent.Nodes.Remove(deviceNode)
                    End If

                    ' Trouver le noeud de la room cible
                    Dim targetRoomNode = FindRoomNode(targetRoomId)
                    If targetRoomNode IsNot Nothing Then
                        Console.WriteLine($"✅ Room cible trouvée: {targetRoomNode.Text}")

                        ' Ajouter le noeud à la nouvelle room
                        Console.WriteLine($"🔄 Ajout du device à: {targetRoomNode.Text}")
                        targetRoomNode.Nodes.Add(deviceNode)
                        targetRoomNode.Expand()

                        ' Mettre à jour le NodeData
                        If deviceNode.Tag IsNot Nothing Then
                            Dim deviceNodeData = TryCast(deviceNode.Tag, NodeData)
                            If deviceNodeData IsNot Nothing Then
                                deviceNodeData.RoomId = targetRoomId
                                Console.WriteLine($"✅ NodeData.RoomId mis à jour: {targetRoomId}")
                            End If
                        End If

                        _treeView.SelectedNode = deviceNode
                        deviceNode.EnsureVisible()
                        Console.WriteLine("✅ Arborescence mise à jour visuellement")
                    Else
                        Console.WriteLine($"❌ ERREUR: Room cible {targetRoomId} NON TROUVÉE dans l'arborescence")
                    End If

                    UpdateStatus("Appareil déplacé")
                    MessageBox.Show("Appareil déplacé avec succès!", "Succès",
                                  MessageBoxButtons.OK, MessageBoxIcon.Information)
                End If
            End If

            ' Gestion des erreurs communes
            If Not success Then
                MessageBox.Show("Échec du déplacement." & Environment.NewLine & "Vérifiez la console pour plus de détails.", "Erreur",
                              MessageBoxButtons.OK, MessageBoxIcon.Error)
                UpdateStatus("Erreur")
            End If
        Catch ex As Exception
            Console.WriteLine($"❌ DEBUG: Erreur déplacement: {ex.Message}")
            MessageBox.Show("Erreur : " & ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
            UpdateStatus("Erreur")
        End Try
    End Sub
#End Region

#Region "Dialogues de sélection"
    Private Function SelectHomeDialog() As String
        If _homesData.Count = 0 Then
            MessageBox.Show("Aucun logement disponible. Créez d'abord un logement.",
                          "Information", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return Nothing
        End If

        Dim selectForm As New Form With {
            .Text = "Sélectionner un logement",
            .Size = New Size(400, 300),
            .StartPosition = FormStartPosition.CenterParent,
            .FormBorderStyle = FormBorderStyle.FixedDialog,
            .MaximizeBox = False,
            .MinimizeBox = False
        }

        Dim listBox As New ListBox With {
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 10)
        }

        For Each kvp In _homesData
            Dim homeName = GetJsonString(kvp.Value, "name")
            listBox.Items.Add(New ListItem(homeName, kvp.Key))
        Next

        Dim btnOK = New Button With {
            .Text = "OK",
            .DialogResult = DialogResult.OK,
            .Dock = DockStyle.Bottom,
            .Height = 40
        }

        selectForm.Controls.Add(listBox)
        selectForm.Controls.Add(btnOK)
        selectForm.AcceptButton = btnOK

        If selectForm.ShowDialog() = DialogResult.OK AndAlso listBox.SelectedItem IsNot Nothing Then
            Return CType(listBox.SelectedItem, ListItem).Value
        End If

        Return Nothing
    End Function

    Private Function SelectRoomDialog(currentRoomId As String) As String
        Dim selectForm As New Form With {
            .Text = "Sélectionner une pièce de destination",
            .Size = New Size(400, 300),
            .StartPosition = FormStartPosition.CenterParent,
            .FormBorderStyle = FormBorderStyle.FixedDialog,
            .MaximizeBox = False,
            .MinimizeBox = False
        }

        Dim listBox As New ListBox With {
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 10)
        }

        ' OPTION SPÉCIALE: Ajouter "Aucune pièce" en première position (valeur vide = retirer de la room)
        listBox.Items.Add(New ListItem("📦 Aucune pièce (racine du logement)", ""))

        ' Ajouter toutes les autres rooms (sauf la room actuelle)
        For Each kvp In _roomsData
            If kvp.Key <> currentRoomId Then ' Exclure la pièce actuelle
                Dim roomName = GetJsonString(kvp.Value, "name")
                listBox.Items.Add(New ListItem(roomName, kvp.Key))
            End If
        Next

        If listBox.Items.Count = 1 AndAlso Not String.IsNullOrEmpty(currentRoomId) Then
            ' Seulement "Aucune pièce" disponible (device déjà dans une room et pas d'autre room)
            MessageBox.Show("Aucune autre pièce disponible. Vous pouvez uniquement retirer l'appareil de sa pièce actuelle.",
                          "Information", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End If

        Dim btnOK = New Button With {
            .Text = "OK",
            .DialogResult = DialogResult.OK,
            .Dock = DockStyle.Bottom,
            .Height = 40
        }

        selectForm.Controls.Add(listBox)
        selectForm.Controls.Add(btnOK)
        selectForm.AcceptButton = btnOK

        If selectForm.ShowDialog() = DialogResult.OK AndAlso listBox.SelectedItem IsNot Nothing Then
            Return CType(listBox.SelectedItem, ListItem).Value
        End If

        Return Nothing
    End Function
#End Region

#Region "Méthodes utilitaires"
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

    ''' <summary>
    ''' Trouve le noeud TreeView correspondant à une room par son ID
    ''' </summary>
    Private Function FindRoomNode(roomId As String) As TreeNode
        For Each homeNode As TreeNode In _treeView.Nodes
            For Each roomNode As TreeNode In homeNode.Nodes
                If roomNode.Tag IsNot Nothing Then
                    Dim nodeData = TryCast(roomNode.Tag, NodeData)
                    If nodeData IsNot Nothing AndAlso nodeData.Type = NodeType.Room AndAlso nodeData.Id = roomId Then
                        Return roomNode
                    End If
                End If
            Next
        Next
        Return Nothing
    End Function
#End Region

#Region "Classes internes"
    Private Enum NodeType
        Home
        Room
        Device
    End Enum

    Private Class NodeData
        Public Property Type As NodeType
        Public Property Id As String
        Public Property Name As String
        Public Property HomeId As String
        Public Property RoomId As String
    End Class

    Private Class ListItem
        Public Property Text As String
        Public Property Value As String

        Public Sub New(text As String, value As String)
            Me.Text = text
            Me.Value = value
        End Sub

        Public Overrides Function ToString() As String
            Return Text
        End Function
    End Class
#End Region
End Class
