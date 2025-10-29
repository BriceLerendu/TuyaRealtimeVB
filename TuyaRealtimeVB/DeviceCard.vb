Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

Public Class DeviceCard
    Inherits Panel

#Region "Constantes"
    Private Const CARD_WIDTH As Integer = 320
    Private Const CARD_HEIGHT As Integer = 260
    Private Const CORNER_RADIUS As Integer = 20
    Private Const FOOTER_HEIGHT As Integer = 35
    Private Const PROPERTY_Y_START As Integer = 85
    Private Const PROPERTY_Y_MAX As Integer = 210
    Private Const PROPERTY_HEIGHT As Integer = 26
    Private Const MAX_PROPERTIES As Integer = 5
    Private Const FLASH_INTERVAL As Integer = 200
    Private Const SHADOW_LAYERS As Integer = 4

    ' Couleurs
    Private Shared ReadOnly DefaultBackColor As Color = Color.FromArgb(250, 250, 252)
    Private Shared ReadOnly DefaultBorderColor As Color = Color.FromArgb(230, 230, 235)
    Private Shared ReadOnly DefaultFooterBackColor As Color = Color.FromArgb(248, 248, 250)
    Private Shared ReadOnly OnlineBackColor As Color = Color.White
    Private Shared ReadOnly OnlineBorderColor As Color = Color.FromArgb(52, 199, 89)
    Private Shared ReadOnly OnlineFooterBackColor As Color = Color.FromArgb(240, 255, 245)
    Private Shared ReadOnly OfflineBackColor As Color = Color.White
    Private Shared ReadOnly OfflineBorderColor As Color = Color.FromArgb(255, 59, 48)
    Private Shared ReadOnly OfflineFooterBackColor As Color = Color.FromArgb(255, 245, 245)
    Private Shared ReadOnly FlashBackColor As Color = Color.FromArgb(230, 245, 255)
    Private Shared ReadOnly FlashBorderColor As Color = Color.FromArgb(0, 122, 255)
#End Region

#Region "Champs privés - Données"
    Private _deviceId As String
    Private _deviceName As String = ""
    Private _roomName As String = ""
    Private _productId As String = ""
    Private _category As String = ""
    Private ReadOnly _lockObject As New Object()
    Private _lastUpdateTime As DateTime = DateTime.MinValue
    Private _apiClient As TuyaApiClient
    Private _logCallback As Action(Of String)
    Private ReadOnly _rawValues As New Dictionary(Of String, String)  ' ✅ NOUVEAU
#End Region

#Region "Champs privés - Contrôles UI"
    Private _titleLabel As Label
    Private _idLabel As Label
    Private _roomLabel As Label
    Private _statusLabel As Label
    Private _timestampLabel As Label
    Private _statusFooter As Panel
    Private _iconLabel As Label
    Private _historyButton As Button
    Private ReadOnly _properties As New Dictionary(Of String, Label)
    Private ReadOnly _propertyCodes As New Dictionary(Of Label, String)
#End Region

#Region "Champs privés - Apparence"
    Private _backgroundColor As Color = DefaultBackColor
    Private _borderColor As Color = DefaultBorderColor
    Private _footerBackColor As Color = DefaultFooterBackColor
    Private _flashTimer As Timer
    Private _flashCount As Integer = 0
    Private _originalBorderColor As Color
    Private _originalBackgroundColor As Color
    Private _isFlashing As Boolean = False

    ' Debouncing des mises à jour UI
    Private _updateTimer As Timer
    Private ReadOnly _pendingUpdates As New Dictionary(Of String, String)

    ' ✅ PHASE 1 - Optimisation: Cache des GraphicsPath pour éviter recréations
    Private _cachedMainPath As GraphicsPath
    Private _cachedFooterPath As GraphicsPath
    Private _isPathCacheValid As Boolean = False
#End Region

#Region "Champs privés - Managers"
    Private Shared ReadOnly _deviceCategories As TuyaDeviceCategories = TuyaDeviceCategories.GetInstance()
    Private Shared ReadOnly _categoryManager As TuyaCategoryManager = TuyaCategoryManager.Instance
    Private Shared ReadOnly _displayPreferencesManager As DisplayPreferencesManager = DisplayPreferencesManager.Instance
#End Region

#Region "Propriétés publiques"
    Public ReadOnly Property DeviceName As String
        Get
            Return _deviceName
        End Get
    End Property

    Public ReadOnly Property Category As String
        Get
            Return _category
        End Get
    End Property

    ''' <summary>
    ''' Retourne la liste de tous les codes de propriétés connus (incluant les sous-propriétés)
    ''' </summary>
    Public Function GetAllKnownPropertyCodes() As List(Of String)
        SyncLock _lockObject
            Return _rawValues.Keys.ToList()
        End SyncLock
    End Function
#End Region

#Region "Initialisation"
    Public Sub New(deviceId As String, apiClient As TuyaApiClient, Optional logCallback As Action(Of String) = Nothing)
        _deviceId = deviceId
        _apiClient = apiClient
        _logCallback = logCallback
        ConfigurePanel()
        InitializeControls()
        InitializeFlashTimer()
    End Sub

    Private Sub ConfigurePanel()
        Me.Size = New Size(CARD_WIDTH, CARD_HEIGHT)
        Me.BackColor = Color.FromArgb(240, 240, 240)
        Me.BorderStyle = BorderStyle.None
        Me.Margin = New Padding(12)
        Me.Cursor = Cursors.Hand
        Me.DoubleBuffered = True

        AddHandler Me.Paint, AddressOf OnPaintCard
        AddHandler Me.Click, AddressOf OnCardClick
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            ' Nettoyer les event handlers
            RemoveHandler Me.Paint, AddressOf OnPaintCard
            RemoveHandler Me.Click, AddressOf OnCardClick

            ' Nettoyer le flash timer
            If _flashTimer IsNot Nothing Then
                RemoveHandler _flashTimer.Tick, AddressOf FlashTimer_Tick
                _flashTimer.Stop()
                _flashTimer.Dispose()
                _flashTimer = Nothing
            End If

            ' Nettoyer le update timer (debounce)
            If _updateTimer IsNot Nothing Then
                RemoveHandler _updateTimer.Tick, AddressOf UpdateTimer_Tick
                _updateTimer.Stop()
                _updateTimer.Dispose()
                _updateTimer = Nothing
            End If

            ' ✅ PHASE 1 - Nettoyer les GraphicsPath en cache
            If _cachedMainPath IsNot Nothing Then
                _cachedMainPath.Dispose()
                _cachedMainPath = Nothing
            End If
            If _cachedFooterPath IsNot Nothing Then
                _cachedFooterPath.Dispose()
                _cachedFooterPath = Nothing
            End If

            ' Nettoyer les contrôles
            For Each ctrl As Control In Me.Controls
                ctrl.Dispose()
            Next

            ' Nettoyer les dictionnaires
            _properties.Clear()
            _propertyCodes.Clear()
            _rawValues.Clear()
            _pendingUpdates.Clear()
        End If

        MyBase.Dispose(disposing)
    End Sub

    Private Sub InitializeControls()
        _iconLabel = CreateIconLabel()
        _titleLabel = CreateTitleLabel()
        _idLabel = CreateIdLabel()
        _roomLabel = CreateRoomLabel()
        _historyButton = CreateHistoryButton()
        _statusFooter = CreateFooter()

        Me.Controls.AddRange({_iconLabel, _titleLabel, _idLabel, _roomLabel, _historyButton, _statusFooter})
    End Sub

    Private Function CreateIconLabel() As Label
        Dim label = New Label With {
            .Location = New Point(18, 18),
            .Size = New Size(38, 38),
            .Font = New Font("Segoe UI Emoji", 22, FontStyle.Regular),
            .BackColor = Color.Transparent,
            .ForeColor = Color.FromArgb(0, 122, 255),
            .Text = "📱",
            .TextAlign = ContentAlignment.MiddleCenter
        }
        AddHandler label.Click, AddressOf OnCardClick
        Return label
    End Function

    Private Function CreateTitleLabel() As Label
        Dim label = New Label With {
            .Location = New Point(65, 18),
            .Size = New Size(200, 24),
            .Font = New Font("Segoe UI", 12, FontStyle.Bold),
            .BackColor = Color.Transparent,
            .ForeColor = Color.FromArgb(28, 28, 30),
            .Text = "Chargement...",
            .AutoEllipsis = True
        }
        AddHandler label.Click, AddressOf OnCardClick
        Return label
    End Function

    Private Function CreateIdLabel() As Label
        Dim label = New Label With {
            .Location = New Point(65, 42),
            .Size = New Size(240, 16),
            .Font = New Font("Segoe UI", 7),
            .BackColor = Color.Transparent,
            .ForeColor = Color.FromArgb(142, 142, 147),
            .Text = _deviceId
        }
        AddHandler label.Click, AddressOf OnCardClick
        Return label
    End Function

    Private Function CreateRoomLabel() As Label
        Dim label = New Label With {
            .Location = New Point(65, 58),
            .Size = New Size(240, 18),
            .Font = New Font("Segoe UI", 9, FontStyle.Regular),
            .BackColor = Color.Transparent,
            .ForeColor = Color.FromArgb(99, 99, 102),
            .Text = ""
        }
        AddHandler label.Click, AddressOf OnCardClick
        Return label
    End Function

    Private Function CreateHistoryButton() As Button
        Dim button = New Button With {
            .Text = "📊",
            .Location = New Point(CARD_WIDTH - 36, 18),
            .Size = New Size(26, 26),
            .Font = New Font("Segoe UI Emoji", 9),
            .BackColor = Color.FromArgb(0, 122, 255),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand,
            .TabStop = False
        }
        button.FlatAppearance.BorderSize = 0
        button.FlatAppearance.BorderColor = Color.FromArgb(0, 122, 255)
        AddHandler button.Click, AddressOf OnHistoryButtonClick
        Return button
    End Function

    Private Function CreateFooter() As Panel
        Dim footer = New Panel With {
            .Dock = DockStyle.Bottom,
            .Height = FOOTER_HEIGHT,
            .BackColor = Color.Transparent
        }

        _statusLabel = New Label With {
            .Text = "● En attente...",
            .Font = New Font("Segoe UI", 9, FontStyle.Bold),
            .ForeColor = Color.FromArgb(142, 142, 147),
            .AutoSize = True,
            .Location = New Point(15, 8),
            .BackColor = Color.Transparent
        }
        footer.Controls.Add(_statusLabel)

        _timestampLabel = New Label With {
            .Text = "🕐 --:--:--",
            .Font = New Font("Segoe UI", 8),
            .ForeColor = Color.FromArgb(142, 142, 147),
            .AutoSize = True,
            .BackColor = Color.Transparent
        }
        footer.Controls.Add(_timestampLabel)

        AddHandler footer.Resize, Sub(sender, e)
                                      _timestampLabel.Location = New Point(
                                          footer.Width - _timestampLabel.Width - 15, 10)
                                  End Sub

        Return footer
    End Function

    Private Sub InitializeFlashTimer()
        _flashTimer = New Timer With {.Interval = FLASH_INTERVAL}
        AddHandler _flashTimer.Tick, AddressOf FlashTimer_Tick

        ' Initialiser le timer de debounce (100ms)
        _updateTimer = New Timer With {.Interval = 100}
        AddHandler _updateTimer.Tick, AddressOf UpdateTimer_Tick
    End Sub

    Private Sub UpdateTimer_Tick(sender As Object, e As EventArgs)
        _updateTimer.Stop()

        SyncLock _lockObject
            Try
                ' Appliquer toutes les mises à jour en attente
                For Each kvp In _pendingUpdates
                    UpdatePropertyInternal(kvp.Key, kvp.Value)
                Next
                _pendingUpdates.Clear()
            Catch ex As Exception
                Debug.WriteLine($"Erreur UpdateTimer_Tick: {ex.Message}")
            End Try
        End SyncLock

        ' Un seul invalidate pour toutes les mises à jour
        Me.Invalidate()
    End Sub
#End Region

#Region "Dessin de la carte"
    Private Sub OnPaintCard(sender As Object, e As PaintEventArgs)
        Dim g = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.PixelOffsetMode = PixelOffsetMode.HighQuality

        Dim rect = New Rectangle(2, 2, Me.Width - 5, Me.Height - 5)

        ' ✅ PHASE 1 - Utiliser le cache des GraphicsPath
        Dim path = GetOrCreateCachedPath(rect, CORNER_RADIUS)
        DrawShadow(g, rect)
        DrawBackground(g, path, rect)
        DrawFooter(g, rect)
        DrawBorder(g, path)
        DrawHighlight(g, rect)
    End Sub

    ''' <summary>
    ''' ✅ PHASE 1 - Obtient ou crée le GraphicsPath en cache pour éviter recréations
    ''' </summary>
    Private Function GetOrCreateCachedPath(rect As Rectangle, radius As Integer) As GraphicsPath
        ' Invalider le cache si la taille a changé
        If _cachedMainPath IsNot Nothing Then
            Dim bounds = _cachedMainPath.GetBounds()
            If bounds.Width <> rect.Width OrElse bounds.Height <> rect.Height Then
                _isPathCacheValid = False
                _cachedMainPath.Dispose()
                _cachedMainPath = Nothing
            End If
        End If

        ' Créer le path si nécessaire
        If Not _isPathCacheValid OrElse _cachedMainPath Is Nothing Then
            _cachedMainPath = CreateRoundedPath(rect, radius)
            _isPathCacheValid = True
        End If

        Return _cachedMainPath
    End Function

    Private Function CreateRoundedPath(rect As Rectangle, radius As Integer) As GraphicsPath
        Dim path = New GraphicsPath()
        path.AddArc(rect.X, rect.Y, radius, radius, 180, 90)
        path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90)
        path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90)
        path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90)
        path.CloseFigure()
        Return path
    End Function

    Private Sub DrawShadow(g As Graphics, rect As Rectangle)
        For i As Integer = 0 To SHADOW_LAYERS
            Using shadowBrush As New SolidBrush(Color.FromArgb(8 - i, 0, 0, 0))
                Using shadowPath = CreateRoundedPath(
                    New Rectangle(rect.X + i, rect.Y + i + 2, rect.Width, rect.Height),
                    CORNER_RADIUS)
                    g.FillPath(shadowBrush, shadowPath)
                End Using
            End Using
        Next
    End Sub

    Private Sub DrawBackground(g As Graphics, path As GraphicsPath, rect As Rectangle)
        Using backBrush As New SolidBrush(_backgroundColor)
            g.FillPath(backBrush, path)
        End Using
    End Sub

    Private Sub DrawFooter(g As Graphics, rect As Rectangle)
        Dim footerY = rect.Bottom - FOOTER_HEIGHT + 2

        Using footerPath As New GraphicsPath()
            footerPath.AddLine(rect.X + 1, footerY, rect.Right - 1, footerY)
            footerPath.AddLine(rect.Right - 1, footerY, rect.Right - 1, rect.Bottom - CORNER_RADIUS)
            footerPath.AddArc(rect.Right - CORNER_RADIUS, rect.Bottom - CORNER_RADIUS,
                            CORNER_RADIUS, CORNER_RADIUS, 0, 90)
            footerPath.AddArc(rect.X, rect.Bottom - CORNER_RADIUS, CORNER_RADIUS, CORNER_RADIUS, 90, 90)
            footerPath.AddLine(rect.X + 1, rect.Bottom - CORNER_RADIUS, rect.X + 1, footerY)
            footerPath.CloseFigure()

            Using footerBrush As New SolidBrush(_footerBackColor)
                g.FillPath(footerBrush, footerPath)
            End Using

            Using separatorPen As New Pen(Color.FromArgb(230, 230, 235), 1)
                g.DrawLine(separatorPen, rect.X + 10, footerY, rect.Right - 10, footerY)
            End Using
        End Using
    End Sub

    Private Sub DrawBorder(g As Graphics, path As GraphicsPath)
        Using borderPen As New Pen(_borderColor, 1)
            g.DrawPath(borderPen, path)
        End Using
    End Sub

    Private Sub DrawHighlight(g As Graphics, rect As Rectangle)
        Using highlightBrush As New LinearGradientBrush(
            New Rectangle(rect.X, rect.Y, rect.Width, 60),
            Color.FromArgb(25, 255, 255, 255),
            Color.FromArgb(0, 255, 255, 255),
            LinearGradientMode.Vertical)

            Using highlightPath As New GraphicsPath()
                highlightPath.AddArc(rect.X, rect.Y, CORNER_RADIUS, CORNER_RADIUS, 180, 90)
                highlightPath.AddArc(rect.Right - CORNER_RADIUS, rect.Y, CORNER_RADIUS, CORNER_RADIUS, 270, 90)
                highlightPath.AddLine(rect.Right, CORNER_RADIUS, rect.Right, 60)
                highlightPath.AddLine(rect.Right, 60, rect.X, 60)
                highlightPath.CloseFigure()

                g.FillPath(highlightBrush, highlightPath)
            End Using
        End Using
    End Sub
#End Region

#Region "Mise à jour des informations"
    Public Sub UpdateDeviceInfo(deviceInfo As DeviceInfo)
        If Me.InvokeRequired Then
            Me.BeginInvoke(Sub() UpdateDeviceInfo(deviceInfo))
            Return
        End If

        If deviceInfo Is Nothing Then Return

        UpdateDeviceName(deviceInfo)
        UpdateRoomName(deviceInfo)
        UpdateCategory(deviceInfo)
        UpdateStatus(If(deviceInfo.IsOnline, "online", "offline"))
    End Sub

    Private Sub UpdateDeviceName(deviceInfo As DeviceInfo)
        If Not String.IsNullOrEmpty(deviceInfo.Name) Then
            _deviceName = deviceInfo.Name
            _titleLabel.Text = _deviceName
        ElseIf Not String.IsNullOrEmpty(deviceInfo.ProductName) Then
            _deviceName = deviceInfo.ProductName
            _titleLabel.Text = _deviceName
        End If
    End Sub

    Private Sub UpdateRoomName(deviceInfo As DeviceInfo)
        If Not String.IsNullOrEmpty(deviceInfo.RoomName) Then
            _roomName = deviceInfo.RoomName
            _roomLabel.Text = String.Format("📍 {0}", _roomName)
        End If
    End Sub

    Private Sub UpdateCategory(deviceInfo As DeviceInfo)
        If Not String.IsNullOrEmpty(deviceInfo.Category) Then
            _category = deviceInfo.Category
            UpdateDeviceIconFromCategory(_category)
            Debug.WriteLine(String.Format("📦 Appareil : {0} | Catégorie : {1}", _deviceName, _category))
        Else
            DetectCategoryFromName()
        End If
    End Sub

    Private Sub UpdateDeviceIconFromCategory(category As String)
        Dim deviceInfo = _deviceCategories.GetDeviceInfo(category)
        _iconLabel.Text = deviceInfo.icon
        Debug.WriteLine(String.Format("🏷️ Type détecté : {0} ({1})", deviceInfo.name, deviceInfo.icon))
    End Sub

    Private Sub DetectCategoryFromName()
        Dim nameLower = _deviceName.ToLower()

        If nameLower.Contains("température") OrElse nameLower.Contains("temp") Then
            _category = "wsdcg"
        ElseIf nameLower.Contains("fumée") OrElse nameLower.Contains("smoke") Then
            _category = "ywbj"
        ElseIf nameLower.Contains("mouvement") OrElse nameLower.Contains("pir") Then
            _category = "pir"
        ElseIf nameLower.Contains("porte") OrElse nameLower.Contains("door") Then
            _category = "mcs"
        ElseIf nameLower.Contains("compteur") OrElse nameLower.Contains("edf") Then
            _category = "cz"
        ElseIf nameLower.Contains("chauffage") OrElse nameLower.Contains("switch") Then
            _category = "kg"
        ElseIf nameLower.Contains("libre") OrElse nameLower.Contains("button") Then
            _category = "kg"
        End If

        UpdateDeviceIconFromCategory(_category)
    End Sub
#End Region

#Region "Gestion des propriétés"
    Public Sub UpdateProperty(code As String, value As String)
        If Me.InvokeRequired Then
            Me.BeginInvoke(Sub() UpdateProperty(code, value))
            Return
        End If

        ' OPTIMISÉ : Debouncing - accumuler les mises à jour
        SyncLock _lockObject
            Try
                ' Stocker la valeur brute
                _rawValues(code) = value

                ' ✅ NOUVEAU: Détecter si la valeur est du JSON et l'exploser en sous-propriétés
                If IsJsonValue(value) Then
                    ExpandJsonProperty(code, value)
                    Return
                End If

                ' ✅ NOUVEAU: Vérifier si la propriété doit être affichée selon les préférences
                If Not _displayPreferencesManager.IsPropertyVisible(_category, code) Then
                    ' Propriété masquée par préférences - ne pas afficher
                    ' Mais si elle était déjà affichée, la retirer
                    If _properties.ContainsKey(code) Then
                        RemoveProperty(code)
                    End If
                    Return
                End If

                ' OPTIMISÉ: Debouncing - ajouter aux mises à jour en attente au lieu de mettre à jour immédiatement
                _pendingUpdates(code) = value

                ' Redémarrer le timer de debounce
                _updateTimer.Stop()
                _updateTimer.Start()
            Catch ex As Exception
                Debug.WriteLine($"Erreur UpdateProperty: {ex.Message}")
            End Try
        End SyncLock
    End Sub

    Private Sub UpdatePropertyInternal(code As String, value As String)
        ' Méthode interne appelée par le timer de debounce
        Try
            If Not _properties.ContainsKey(code) Then
                CreateNewProperty(code, value)
            Else
                UpdateExistingProperty(code, value)
            End If

            StartFlashEffect()
        Catch ex As Exception
            Debug.WriteLine($"Erreur UpdatePropertyInternal: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Vérifie si une valeur est du JSON
    ''' </summary>
    Private Function IsJsonValue(value As String) As Boolean
        If String.IsNullOrWhiteSpace(value) Then Return False
        Dim trimmed = value.Trim()
        Return (trimmed.StartsWith("{") AndAlso trimmed.EndsWith("}")) OrElse
               (trimmed.StartsWith("[") AndAlso trimmed.EndsWith("]"))
    End Function

    ''' <summary>
    ''' Explose une propriété JSON en sous-propriétés individuelles
    ''' </summary>
    Private Sub ExpandJsonProperty(parentCode As String, jsonValue As String)
        Try
            Dim jsonObj = Newtonsoft.Json.Linq.JObject.Parse(jsonValue)

            ' Parcourir chaque propriété du JSON
            For Each prop In jsonObj.Properties()
                Dim subPropertyCode = $"{parentCode}.{prop.Name}"
                Dim subPropertyValue = prop.Value.ToString()

                ' Stocker la sous-propriété
                _rawValues(subPropertyCode) = subPropertyValue

                ' Vérifier si la sous-propriété doit être affichée
                If _displayPreferencesManager.IsPropertyVisible(_category, subPropertyCode) Then
                    If Not _properties.ContainsKey(subPropertyCode) Then
                        CreateNewProperty(subPropertyCode, subPropertyValue)
                    Else
                        UpdateExistingProperty(subPropertyCode, subPropertyValue)
                    End If
                Else
                    ' Retirer si elle était affichée
                    If _properties.ContainsKey(subPropertyCode) Then
                        RemoveProperty(subPropertyCode)
                    End If
                End If
            Next

            Debug.WriteLine($"✓ Propriété JSON '{parentCode}' explosée en {jsonObj.Properties().Count()} sous-propriétés")
        Catch ex As Exception
            Debug.WriteLine($"✗ Erreur ExpandJsonProperty pour '{parentCode}': {ex.Message}")
            ' En cas d'erreur, traiter comme une propriété normale
            _rawValues(parentCode) = jsonValue
        End Try
    End Sub

    Private Sub CreateNewProperty(code As String, value As String)
        ' ✅ MODIFIÉ: Vérifier d'abord les préférences pour la limite
        Dim visibleProps = _displayPreferencesManager.GetVisibleProperties(_category)
        Dim maxProperties As Integer

        ' Si des préférences existent, respecter la limite définie
        If visibleProps IsNot Nothing AndAlso visibleProps.Count > 0 Then
            maxProperties = visibleProps.Count
        Else
            ' Comportement par défaut: max 5 propriétés
            maxProperties = MAX_PROPERTIES
        End If

        ' Limiter le nombre de propriétés affichées
        If _properties.Count >= maxProperties Then Return

        Dim yPos = PROPERTY_Y_START + (_properties.Count * PROPERTY_HEIGHT)
        If yPos > PROPERTY_Y_MAX Then Return

        Dim propPanel = CreatePropertyPanel(yPos)
        Dim icon = CreatePropertyIcon(code)
        Dim nameLabel = CreatePropertyNameLabel(code)
        Dim valueLabel = CreatePropertyValueLabel(code, value)

        propPanel.Controls.AddRange({icon, nameLabel, valueLabel})

        AddHandler propPanel.Click, AddressOf OnCardClick

        _properties(code) = valueLabel
        _propertyCodes(valueLabel) = code
        Me.Controls.Add(propPanel)
    End Sub

    Private Function CreatePropertyPanel(yPos As Integer) As Panel
        Dim panel = New Panel With {
            .Location = New Point(18, yPos),
            .Size = New Size(290, 24),
            .BackColor = Color.Transparent
        }
        Return panel
    End Function

    Private Function CreatePropertyIcon(code As String) As Label
        Dim icon = New Label With {
            .Location = New Point(0, 2),
            .Size = New Size(18, 20),
            .Font = New Font("Segoe UI Emoji", 9),
            .BackColor = Color.Transparent,
            .Text = GetPropertyIconFromConfig(code),
            .TextAlign = ContentAlignment.MiddleLeft
        }
        AddHandler icon.Click, AddressOf OnCardClick
        Return icon
    End Function

    Private Function CreatePropertyNameLabel(code As String) As Label
        Dim displayName = _categoryManager.GetDisplayName(_category, code)

        ' Extraire seulement le texte sans l'icône si présent
        If displayName.Length > 2 AndAlso Char.IsWhiteSpace(displayName.Chars(1)) Then
            displayName = displayName.Substring(2).Trim()
        End If

        Dim label = New Label With {
            .Location = New Point(22, 2),
            .Size = New Size(130, 20),
            .Font = New Font("Segoe UI", 9),
            .BackColor = Color.Transparent,
            .ForeColor = Color.FromArgb(99, 99, 102),
            .Text = displayName,
            .TextAlign = ContentAlignment.MiddleLeft
        }
        AddHandler label.Click, AddressOf OnCardClick
        Return label
    End Function

    Private Function CreatePropertyValueLabel(code As String, value As String) As Label
        Dim formattedValue = _categoryManager.FormatValueWithScale(_category, _deviceId, code, value, _apiClient)

        Dim label = New Label With {
            .Location = New Point(155, 2),
            .Size = New Size(135, 20),
            .Font = New Font("Segoe UI", 10, FontStyle.Bold),
            .BackColor = Color.Transparent,
            .Text = formattedValue,
            .TextAlign = ContentAlignment.MiddleRight
        }

        SetPropertyColor(code, label)
        AddHandler label.Click, AddressOf OnCardClick

        Return label
    End Function

    Private Sub UpdateExistingProperty(code As String, value As String)
        _properties(code).Text = _categoryManager.FormatValueWithScale(_category, _deviceId, code, value, _apiClient)

        ' Mettre à jour la couleur pour les switches
        If code.Contains("switch") OrElse code = "doorcontact_state" Then
            _properties(code).ForeColor = If(value.Equals("true", StringComparison.OrdinalIgnoreCase),
                                            Color.FromArgb(50, 180, 50),
                                            Color.FromArgb(255, 80, 80))
        End If
    End Sub

    Private Sub RemoveProperty(code As String)
        Try
            If _properties.ContainsKey(code) Then
                Dim valueLabel = _properties(code)
                Dim parentPanel = valueLabel.Parent

                ' Retirer le panel parent de la carte
                If parentPanel IsNot Nothing Then
                    Me.Controls.Remove(parentPanel)
                    parentPanel.Dispose()
                End If

                ' Nettoyer les dictionnaires
                _properties.Remove(code)
                If _propertyCodes.ContainsKey(valueLabel) Then
                    _propertyCodes.Remove(valueLabel)
                End If

                Debug.WriteLine($"✓ Propriété '{code}' retirée de l'affichage")
            End If
        Catch ex As Exception
            Debug.WriteLine($"✗ Erreur RemoveProperty pour '{code}': {ex.Message}")
        End Try
    End Sub

    Private Function GetPropertyIcon(code As String) As String
        ' Pour les sous-propriétés JSON (ex: phase_a.electricCurrent)
        If code.Contains(".") Then
            Dim subPropertyName = code.Substring(code.IndexOf(".") + 1).ToLower()
            If subPropertyName.Contains("current") OrElse subPropertyName = "electriccurrent" Then Return "🔌"
            If subPropertyName.Contains("voltage") Then Return "🔋"
            If subPropertyName.Contains("power") Then Return "⚡"
            If subPropertyName.Contains("temperature") Then Return "🌡️"
            If subPropertyName.Contains("humidity") Then Return "💧"
            If subPropertyName.Contains("energy") Then Return "📊"
        End If

        ' Propriétés normales
        If code.Contains("temperature") OrElse code = "va_temperature" Then Return "🌡️"
        If code.Contains("humidity") OrElse code = "humidity_value" Then Return "💧"
        If code.Contains("power") OrElse code.EndsWith("_P") Then Return "⚡"
        If code.Contains("current") OrElse code.EndsWith("_I") Then Return "🔌"
        If code.Contains("voltage") OrElse code.EndsWith("_V") Then Return "🔋"
        If code.Contains("battery") Then Return "🔋"
        If code.Contains("energy") OrElse code = "add_ele" Then Return "📊"
        If code = "pir" Then Return "👁️"
        If code.Contains("switch") OrElse code = "doorcontact_state" Then Return "🎚️"
        Return "📊"
    End Function

    Private Sub SetPropertyColor(code As String, label As Label)
        ' Pour les sous-propriétés JSON (ex: phase_a.electricCurrent)
        Dim checkCode = code
        If code.Contains(".") Then
            checkCode = code.Substring(code.IndexOf(".") + 1)
        End If

        If checkCode.Contains("temperature") OrElse checkCode = "va_temperature" Then
            label.ForeColor = Color.FromArgb(255, 149, 0)
        ElseIf checkCode.Contains("humidity") OrElse checkCode = "humidity_value" Then
            label.ForeColor = Color.FromArgb(0, 122, 255)
        ElseIf checkCode.Contains("power") OrElse checkCode.EndsWith("_P") Then
            label.ForeColor = Color.FromArgb(175, 82, 222)
        ElseIf checkCode.Contains("current") OrElse checkCode.EndsWith("_I") OrElse checkCode.ToLower() = "electriccurrent" Then
            label.ForeColor = Color.FromArgb(255, 149, 0)
        ElseIf checkCode.Contains("voltage") OrElse checkCode.EndsWith("_V") Then
            label.ForeColor = Color.FromArgb(0, 122, 255)
        ElseIf checkCode.Contains("battery") Then
            label.ForeColor = Color.FromArgb(52, 199, 89)
        ElseIf checkCode.Contains("energy") OrElse checkCode = "add_ele" Then
            label.ForeColor = Color.FromArgb(0, 122, 255)
        Else
            label.ForeColor = Color.FromArgb(28, 28, 30)
        End If
    End Sub
#End Region

#Region "Rafraîchissement de la configuration"
    ' ✅ MÉTHODE pour rafraîchir l'affichage avec la nouvelle config
    Public Sub RefreshDisplay()
        If Me.InvokeRequired Then
            Me.BeginInvoke(Sub() RefreshDisplay())
            Return
        End If

        SyncLock _lockObject
            Try
                ' ✅ NOUVELLE APPROCHE: Recréer toutes les propriétés depuis zéro
                ' Cela permet de gérer l'ajout/suppression de propriétés selon les préférences

                ' 1. Retirer toutes les propriétés actuellement affichées
                Dim propertiesToRemove = _properties.Keys.ToList()
                For Each code In propertiesToRemove
                    RemoveProperty(code)
                Next

                ' 2. Recréer toutes les propriétés selon les nouvelles préférences
                ' Parcourir _rawValues et appeler UpdateProperty pour chaque code
                For Each kvp In _rawValues.ToList()
                    Dim code = kvp.Key
                    Dim value = kvp.Value

                    ' UpdateProperty gère automatiquement les préférences d'affichage
                    ' Il ne créera la propriété que si elle doit être visible
                    If Not _properties.ContainsKey(code) Then
                        ' Vérifier si la propriété doit être affichée selon les préférences
                        If _displayPreferencesManager.IsPropertyVisible(_category, code) Then
                            CreateNewProperty(code, value)
                        End If
                    End If
                Next

                Debug.WriteLine(String.Format("✓ DeviceCard {0} rafraîchie: {1} propriétés affichées", _deviceName, _properties.Count))
            Catch ex As Exception
                Debug.WriteLine(String.Format("✗ Erreur RefreshDisplay: {0}", ex.Message))
            End Try
        End SyncLock
    End Sub

    Private Sub UpdatePropertyName(parentPanel As Panel, code As String)
        For Each ctrl As Control In parentPanel.Controls
            Dim nameLabel As Label = TryCast(ctrl, Label)
            If nameLabel IsNot Nothing AndAlso nameLabel.Location.X = 22 Then
                Dim displayName As String = _categoryManager.GetDisplayName(_category, code)

                If displayName.Length > 2 AndAlso Char.IsWhiteSpace(displayName.Chars(1)) Then
                    displayName = displayName.Substring(2).Trim()
                End If

                nameLabel.Text = displayName
                Exit For
            End If
        Next
    End Sub

    Private Sub UpdatePropertyIcon(parentPanel As Panel, code As String)
        For Each ctrl As Control In parentPanel.Controls
            Dim iconLabel As Label = TryCast(ctrl, Label)
            If iconLabel IsNot Nothing AndAlso iconLabel.Location.X = 0 Then
                iconLabel.Text = GetPropertyIconFromConfig(code)
                Exit For
            End If
        Next
    End Sub

    Private Function GetPropertyIconFromConfig(code As String) As String
        Try
            Dim config = _categoryManager.GetConfiguration()
            If config IsNot Nothing AndAlso Not String.IsNullOrEmpty(_category) Then
                Dim categoryConfig = config("categories")(_category)
                If categoryConfig IsNot Nothing Then
                    Dim properties = categoryConfig("properties")
                    If properties IsNot Nothing Then
                        Dim propConfig = properties(code)
                        If propConfig IsNot Nothing Then
                            Dim iconToken = propConfig("icon")
                            If iconToken IsNot Nothing Then
                                Dim iconValue As String = iconToken.ToString()
                                If Not String.IsNullOrEmpty(iconValue) Then
                                    Return iconValue
                                End If
                            End If
                        End If
                    End If
                End If
            End If
        Catch ex As Exception
            Debug.WriteLine(String.Format("Erreur GetPropertyIconFromConfig: {0}", ex.Message))
        End Try

        Return GetPropertyIcon(code)
    End Function
#End Region

#Region "Gestion du statut"
    Public Sub UpdateStatus(bizCode As String)
        If Me.InvokeRequired Then
            Me.BeginInvoke(Sub() UpdateStatus(bizCode))
            Return
        End If

        Select Case bizCode.ToLower()
            Case "online"
                SetOnlineStatus()
            Case "offline"
                SetOfflineStatus()
            Case Else
                SetUnknownStatus()
        End Select

        Me.Invalidate()
    End Sub

    Private Sub SetOnlineStatus()
        _statusLabel.Text = "🟢 En ligne"
        _statusLabel.ForeColor = Color.FromArgb(52, 199, 89)
        _footerBackColor = OnlineFooterBackColor
        _backgroundColor = OnlineBackColor
        _borderColor = OnlineBorderColor
    End Sub

    Private Sub SetOfflineStatus()
        _statusLabel.Text = "🔴 Hors ligne"
        _statusLabel.ForeColor = Color.FromArgb(255, 59, 48)
        _footerBackColor = OfflineFooterBackColor
        _backgroundColor = OfflineBackColor
        _borderColor = OfflineBorderColor
    End Sub

    Private Sub SetUnknownStatus()
        _statusLabel.Text = "❓ Inconnu"
        _statusLabel.ForeColor = Color.FromArgb(142, 142, 147)
        _footerBackColor = DefaultFooterBackColor
        _backgroundColor = DefaultBackColor
        _borderColor = DefaultBorderColor
    End Sub

    Public Sub UpdateTimestamp()
        If Me.InvokeRequired Then
            Me.BeginInvoke(Sub() UpdateTimestamp())
            Return
        End If

        _lastUpdateTime = DateTime.Now
        _timestampLabel.Text = String.Format("🕐 {0:HH:mm:ss}", _lastUpdateTime)
        _timestampLabel.ForeColor = Color.FromArgb(52, 199, 89)
        _timestampLabel.Location = New Point(_statusFooter.Width - _timestampLabel.Width - 15, 10)
    End Sub

    Public Sub RefreshTimestampColor()
        If Me.InvokeRequired Then
            Me.BeginInvoke(Sub() RefreshTimestampColor())
            Return
        End If

        If _lastUpdateTime = DateTime.MinValue Then Return

        Dim elapsed = DateTime.Now - _lastUpdateTime

        If elapsed.TotalMinutes < 1 Then
            _timestampLabel.ForeColor = Color.FromArgb(52, 199, 89)
        ElseIf elapsed.TotalMinutes < 5 Then
            _timestampLabel.ForeColor = Color.FromArgb(255, 149, 0)
        Else
            _timestampLabel.ForeColor = Color.FromArgb(255, 59, 48)
        End If
    End Sub
#End Region

#Region "Effet flash"
    Private Sub StartFlashEffect()
        If _isFlashing OrElse _flashTimer Is Nothing Then Return

        _originalBorderColor = _borderColor
        _originalBackgroundColor = _backgroundColor
        _isFlashing = True
        _flashCount = 0
        _flashTimer.Start()
    End Sub

    Private Sub FlashTimer_Tick(sender As Object, e As EventArgs)
        _flashCount += 1

        If _flashCount = 1 Then
            _borderColor = FlashBorderColor
            _backgroundColor = FlashBackColor
        Else
            _flashTimer.Stop()
            _flashCount = 0
            _isFlashing = False
            _borderColor = _originalBorderColor
            _backgroundColor = _originalBackgroundColor
        End If

        Me.Invalidate()
    End Sub
#End Region

#Region "Interaction utilisateur"
    Private Sub OnCardClick(sender As Object, e As EventArgs)
        If _apiClient Is Nothing Then
            MessageBox.Show("API Client non initialisé", "Erreur",
                          MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        ShowContextMenu()
    End Sub

    Private Sub OnHistoryButtonClick(sender As Object, e As EventArgs)
        If _apiClient Is Nothing Then
            MessageBox.Show("API Client non initialisé", "Erreur",
                          MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Try
            ' Créer le service d'historique avec le callback de log
            Dim historyService As New TuyaHistoryService(_apiClient, _logCallback)

            ' Ouvrir la fenêtre d'historique
            Dim historyForm As New HistoryForm(_deviceId, _deviceName, historyService)
            historyForm.ShowDialog()
        Catch ex As Exception
            MessageBox.Show($"Erreur lors de l'ouverture de l'historique:{vbCrLf}{ex.Message}",
                          "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ShowContextMenu()
        Dim contextMenu = CreateContextMenu()
        contextMenu.Show(Me, Me.PointToClient(Cursor.Position))
    End Sub

    Private Function CreateContextMenu() As ContextMenuStrip
        Dim menu = New ContextMenuStrip With {
            .BackColor = Color.FromArgb(45, 45, 48),
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 10)
        }

        Dim controlItem = New ToolStripMenuItem("🎛️ Contrôler l'appareil") With {
            .ForeColor = Color.White
        }
        AddHandler controlItem.Click, AddressOf ControlDevice_Click
        menu.Items.Add(controlItem)

        menu.Items.Add(New ToolStripSeparator())

        Dim renameItem = New ToolStripMenuItem("✏️ Renommer l'appareil") With {
            .ForeColor = Color.White
        }
        AddHandler renameItem.Click, AddressOf RenameDevice_Click
        menu.Items.Add(renameItem)

        Return menu
    End Function

    Private Sub ControlDevice_Click(sender As Object, e As EventArgs)
        Dim propertiesCopy As New Dictionary(Of String, String)

        SyncLock _lockObject
            For Each kvp In _propertyCodes
                Dim valueLabel = kvp.Key
                Dim code = kvp.Value
                propertiesCopy(code) = valueLabel.Text
            Next
        End SyncLock

        Using controlForm As New DeviceControlForm(_deviceId, _deviceName, _category, _apiClient, propertiesCopy)
            controlForm.ShowDialog()
        End Using
    End Sub

    Private Async Sub RenameDevice_Click(sender As Object, e As EventArgs)
        Try
            Dim newName = ShowRenameDialog()
            If String.IsNullOrEmpty(newName) Then Return
            If newName = _deviceName Then Return

            Dim originalBackColor = Me.BackColor
            Me.BackColor = Color.FromArgb(255, 250, 200)
            Me.Update()

            Dim success = Await _apiClient.RenameDeviceAsync(_deviceId, newName)

            Me.BackColor = originalBackColor

            If success Then
                _deviceName = newName
                _titleLabel.Text = newName
                MessageBox.Show(String.Format("✅ Appareil renommé en '{0}'", newName), "Succès",
                              MessageBoxButtons.OK, MessageBoxIcon.Information)
            Else
                MessageBox.Show("❌ Échec du renommage. Vérifiez les logs pour plus de détails.",
                              "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End If

        Catch ex As Exception
            MessageBox.Show(String.Format("❌ Erreur : {0}", ex.Message), "Erreur",
                          MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function ShowRenameDialog() As String
        Using inputForm As New Form()
            inputForm.Text = "Renommer l'appareil"
            inputForm.Size = New Size(400, 150)
            inputForm.StartPosition = FormStartPosition.CenterParent
            inputForm.FormBorderStyle = FormBorderStyle.FixedDialog
            inputForm.MaximizeBox = False
            inputForm.MinimizeBox = False
            inputForm.BackColor = Color.FromArgb(242, 242, 247)

            Dim label = New Label With {
                .Text = "Nouveau nom :",
                .Location = New Point(20, 20),
                .Size = New Size(100, 20),
                .Font = New Font("Segoe UI", 10)
            }
            inputForm.Controls.Add(label)

            Dim textBox = New TextBox With {
                .Text = _deviceName,
                .Location = New Point(20, 45),
                .Size = New Size(340, 25),
                .Font = New Font("Segoe UI", 10)
            }
            textBox.SelectAll()
            inputForm.Controls.Add(textBox)

            Dim okButton = New Button With {
                .Text = "OK",
                .DialogResult = DialogResult.OK,
                .Location = New Point(200, 80),
                .Size = New Size(75, 30),
                .BackColor = Color.FromArgb(0, 122, 255),
                .ForeColor = Color.White,
                .FlatStyle = FlatStyle.Flat,
                .Font = New Font("Segoe UI", 10)
            }
            inputForm.Controls.Add(okButton)
            inputForm.AcceptButton = okButton

            Dim cancelButton = New Button With {
                .Text = "Annuler",
                .DialogResult = DialogResult.Cancel,
                .Location = New Point(285, 80),
                .Size = New Size(75, 30),
                .BackColor = Color.FromArgb(142, 142, 147),
                .ForeColor = Color.White,
                .FlatStyle = FlatStyle.Flat,
                .Font = New Font("Segoe UI", 10)
            }
            inputForm.Controls.Add(cancelButton)
            inputForm.CancelButton = cancelButton

            If inputForm.ShowDialog() = DialogResult.OK Then
                Dim newName = textBox.Text.Trim()
                If String.IsNullOrEmpty(newName) Then
                    MessageBox.Show("Le nom ne peut pas être vide", "Erreur",
                                  MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Return Nothing
                End If
                Return newName
            End If
        End Using

        Return Nothing
    End Function
#End Region

End Class