Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

Public Class DeviceCard
    Inherits Panel

    Private _deviceId As String
    Private _deviceName As String = ""
    Private _roomName As String = ""
    Private _productId As String = ""
    Private _category As String = ""

    Private _titleLabel As Label
    Private _idLabel As Label
    Private _roomLabel As Label
    Private _statusLabel As Label
    Private _timestampLabel As Label
    Private _lastUpdateLabel As Label
    Private _statusFooter As Panel
    Private _properties As New Dictionary(Of String, Label)
    Private _propertyCodes As New Dictionary(Of Label, String)
    Private _iconLabel As Label
    Private ReadOnly _lockObject As New Object()
    Private _lastUpdateTime As DateTime = DateTime.MinValue
    Private _apiClient As TuyaApiClient

    ' Couleurs style iOS
    Private _backgroundColor As Color = Color.FromArgb(250, 250, 252)
    Private _borderColor As Color = Color.FromArgb(230, 230, 235)
    Private _footerBackColor As Color = Color.FromArgb(248, 248, 250)
    Private _shadowColor As Color = Color.FromArgb(30, 0, 0, 0)
    Private _innerShadowColor As Color = Color.FromArgb(15, 0, 0, 0)
    Private _flashTimer As Timer
    Private _flashCount As Integer = 0
    Private _originalBorderColor As Color
    Private _originalBackgroundColor As Color
    Private _isFlashing As Boolean = False

    Private Shared _deviceCategories As TuyaDeviceCategories = TuyaDeviceCategories.GetInstance()

    Public ReadOnly Property DeviceName As String
        Get
            Return _deviceName
        End Get
    End Property

    Public Sub New(deviceId As String)
        _deviceId = deviceId
        Me.Size = New Size(320, 220)
        Me.BackColor = Color.FromArgb(240, 240, 240)
        Me.BorderStyle = BorderStyle.None
        Me.Margin = New Padding(12)
        Me.Cursor = Cursors.Hand

        Me.DoubleBuffered = True

        AddHandler Me.Paint, AddressOf OnPaintCard
        AddHandler Me.Click, AddressOf OnCardClick

        InitializeControls()

        _flashTimer = New Timer()
        _flashTimer.Interval = 200
        AddHandler _flashTimer.Tick, AddressOf FlashTimer_Tick
    End Sub

    Private Sub OnPaintCard(sender As Object, e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality

        Dim radius As Integer = 20
        Dim rect As New Rectangle(2, 2, Me.Width - 5, Me.Height - 5)

        Using path As New GraphicsPath()
            ' Créer le path arrondi global de la carte
            path.AddArc(rect.X, rect.Y, radius, radius, 180, 90)
            path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90)
            path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90)
            path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90)
            path.CloseFigure()

            ' Ombre externe subtile (style iOS)
            For i As Integer = 0 To 4
                Using shadowBrush As New SolidBrush(Color.FromArgb(8 - i, 0, 0, 0))
                    Using shadowPath As New GraphicsPath()
                        Dim shadowRect As New Rectangle(rect.X + i, rect.Y + i + 2, rect.Width, rect.Height)
                        shadowPath.AddArc(shadowRect.X, shadowRect.Y, radius, radius, 180, 90)
                        shadowPath.AddArc(shadowRect.Right - radius, shadowRect.Y, radius, radius, 270, 90)
                        shadowPath.AddArc(shadowRect.Right - radius, shadowRect.Bottom - radius, radius, radius, 0, 90)
                        shadowPath.AddArc(shadowRect.X, shadowRect.Bottom - radius, radius, radius, 90, 90)
                        shadowPath.CloseFigure()
                        g.FillPath(shadowBrush, shadowPath)
                    End Using
                End Using
            Next

            ' Remplir le fond principal
            Using backBrush As New SolidBrush(_backgroundColor)
                g.FillPath(backBrush, path)
            End Using

            ' ✨ NOUVEAU : Dessiner le footer intégré dans la forme arrondie
            Dim footerHeight As Integer = 35
            Dim footerY As Integer = rect.Bottom - footerHeight + 2

            Using footerPath As New GraphicsPath()
                ' Créer un path pour le footer qui suit les coins arrondis du bas
                footerPath.AddLine(rect.X + 1, footerY, rect.Right - 1, footerY)
                footerPath.AddLine(rect.Right - 1, footerY, rect.Right - 1, rect.Bottom - radius)
                footerPath.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90)
                footerPath.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90)
                footerPath.AddLine(rect.X + 1, rect.Bottom - radius, rect.X + 1, footerY)
                footerPath.CloseFigure()

                ' Remplir le footer avec sa couleur
                Using footerBrush As New SolidBrush(_footerBackColor)
                    g.FillPath(footerBrush, footerPath)
                End Using

                ' Ligne de séparation au-dessus du footer
                Using separatorPen As New Pen(Color.FromArgb(230, 230, 235), 1)
                    g.DrawLine(separatorPen, rect.X + 10, footerY, rect.Right - 10, footerY)
                End Using
            End Using

            ' Dessiner le contour de la carte
            Using borderPen As New Pen(_borderColor, 1)
                g.DrawPath(borderPen, path)
            End Using

            ' Léger highlight en haut
            Using highlightBrush As New LinearGradientBrush(
                New Rectangle(rect.X, rect.Y, rect.Width, 60),
                Color.FromArgb(25, 255, 255, 255),
                Color.FromArgb(0, 255, 255, 255),
                LinearGradientMode.Vertical)

                Using highlightPath As New GraphicsPath()
                    highlightPath.AddArc(rect.X, rect.Y, radius, radius, 180, 90)
                    highlightPath.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90)
                    highlightPath.AddLine(rect.Right, radius, rect.Right, 60)
                    highlightPath.AddLine(rect.Right, 60, rect.X, 60)
                    highlightPath.CloseFigure()

                    g.FillPath(highlightBrush, highlightPath)
                End Using
            End Using
        End Using
    End Sub

    Private Sub InitializeControls()
        ' Icône du type d'appareil
        _iconLabel = New Label With {
            .Location = New Point(18, 18),
            .Size = New Size(38, 38),
            .Font = New Font("Segoe UI Emoji", 22, FontStyle.Regular),
            .BackColor = Color.Transparent,
            .ForeColor = Color.FromArgb(0, 122, 255),
            .Text = "📱",
            .TextAlign = ContentAlignment.MiddleCenter
        }
        AddHandler _iconLabel.Click, AddressOf OnCardClick
        Me.Controls.Add(_iconLabel)

        ' Nom de l'appareil
        _titleLabel = New Label With {
            .Location = New Point(65, 18),
            .Size = New Size(240, 24),
            .Font = New Font("Segoe UI", 12, FontStyle.Bold),
            .BackColor = Color.Transparent,
            .ForeColor = Color.FromArgb(28, 28, 30),
            .Text = "Chargement..."
        }
        AddHandler _titleLabel.Click, AddressOf OnCardClick
        Me.Controls.Add(_titleLabel)

        ' ID de l'appareil
        _idLabel = New Label With {
            .Location = New Point(65, 42),
            .Size = New Size(240, 16),
            .Font = New Font("Segoe UI", 7),
            .BackColor = Color.Transparent,
            .ForeColor = Color.FromArgb(142, 142, 147),
            .Text = _deviceId
        }
        AddHandler _idLabel.Click, AddressOf OnCardClick
        Me.Controls.Add(_idLabel)

        ' Nom de la pièce
        _roomLabel = New Label With {
            .Location = New Point(65, 58),
            .Size = New Size(240, 18),
            .Font = New Font("Segoe UI", 9, FontStyle.Regular),
            .BackColor = Color.Transparent,
            .ForeColor = Color.FromArgb(99, 99, 102),
            .Text = ""
        }
        AddHandler _roomLabel.Click, AddressOf OnCardClick
        Me.Controls.Add(_roomLabel)

        ' ✅ Footer intégré (transparent, dessiné dans OnPaintCard)
        _statusFooter = New Panel With {
            .Dock = DockStyle.Bottom,
            .Height = 35,
            .BackColor = Color.Transparent
        }

        ' Label de statut (à gauche)
        _statusLabel = New Label With {
            .Text = "● En attente...",
            .Font = New Font("Segoe UI", 9, FontStyle.Bold),
            .ForeColor = Color.FromArgb(142, 142, 147),
            .AutoSize = True,
            .Location = New Point(15, 8),
            .BackColor = Color.Transparent
        }
        _statusFooter.Controls.Add(_statusLabel)

        ' Timestamp (à droite)
        _timestampLabel = New Label With {
            .Text = "🕐 --:--:--",
            .Font = New Font("Segoe UI", 8),
            .ForeColor = Color.FromArgb(142, 142, 147),
            .AutoSize = True,
            .BackColor = Color.Transparent
        }
        _statusFooter.Controls.Add(_timestampLabel)

        ' Positionner le timestamp à droite
        Dim footerResizeHandler As EventHandler = Sub(sender, e)
                                                      _timestampLabel.Location = New Point(_statusFooter.Width - _timestampLabel.Width - 15, 10)
                                                  End Sub
        AddHandler _statusFooter.Resize, footerResizeHandler

        Me.Controls.Add(_statusFooter)
    End Sub

    Public Sub UpdateDeviceInfo(deviceInfo As DeviceInfo)
        If Me.InvokeRequired Then
            Me.BeginInvoke(Sub() UpdateDeviceInfo(deviceInfo))
            Return
        End If

        If deviceInfo IsNot Nothing Then
            If Not String.IsNullOrEmpty(deviceInfo.Name) Then
                _deviceName = deviceInfo.Name
                _titleLabel.Text = _deviceName
            ElseIf Not String.IsNullOrEmpty(deviceInfo.ProductName) Then
                _deviceName = deviceInfo.ProductName
                _titleLabel.Text = _deviceName
            End If

            If Not String.IsNullOrEmpty(deviceInfo.RoomName) Then
                _roomName = deviceInfo.RoomName
                _roomLabel.Text = $"📍 {_roomName}"
            End If

            If Not String.IsNullOrEmpty(deviceInfo.Category) Then
                _category = deviceInfo.Category
                UpdateDeviceIconFromCategory(_category)
                Console.WriteLine($"📦 Appareil : {_deviceName} | Catégorie : {_category}")
            Else
                DetectCategoryFromName()
            End If

            UpdateStatus(If(deviceInfo.IsOnline, "online", "offline"))
        End If
    End Sub

    Private Sub UpdateDeviceIconFromCategory(category As String)
        Dim deviceInfo = _deviceCategories.GetDeviceInfo(category)
        _iconLabel.Text = deviceInfo.icon
        Debug.WriteLine($"🏷️ Type détecté : {deviceInfo.name} ({deviceInfo.icon})")
    End Sub

    Private Sub DetectCategoryFromName()
        If _deviceName.ToLower().Contains("température") OrElse _deviceName.ToLower().Contains("temp") Then
            _category = "wsdcg"
        ElseIf _deviceName.ToLower().Contains("fumée") OrElse _deviceName.ToLower().Contains("smoke") Then
            _category = "ywbj"
        ElseIf _deviceName.ToLower().Contains("mouvement") OrElse _deviceName.ToLower().Contains("pir") Then
            _category = "pir"
        ElseIf _deviceName.ToLower().Contains("porte") OrElse _deviceName.ToLower().Contains("door") Then
            _category = "mcs"
        ElseIf _deviceName.ToLower().Contains("compteur") OrElse _deviceName.ToLower().Contains("edf") Then
            _category = "zndb"
        ElseIf _deviceName.ToLower().Contains("chauffage") OrElse _deviceName.ToLower().Contains("switch") Then
            _category = "kg"
        ElseIf _deviceName.ToLower().Contains("libre") OrElse _deviceName.ToLower().Contains("button") Then
            _category = "kg"
        End If

        UpdateDeviceIconFromCategory(_category)
    End Sub

    Public Sub UpdateProperty(code As String, value As String)
        If Me.InvokeRequired Then
            Me.BeginInvoke(Sub() UpdateProperty(code, value))
            Return
        End If

        SyncLock _lockObject
            Try
                If Not _properties.ContainsKey(code) Then
                    ' ✅ Zone disponible : de Y=85 à Y=145 (laisser 35px pour le footer + marge)
                    Dim yPos As Integer = 85 + (_properties.Count * 26)
                    If yPos > 145 Then Return

                    Dim propPanel As New Panel With {
                        .Location = New Point(18, yPos),
                        .Size = New Size(290, 24),
                        .BackColor = Color.Transparent
                    }

                    Dim iconLabel As New Label With {
                        .Location = New Point(0, 2),
                        .Size = New Size(18, 20),
                        .Font = New Font("Segoe UI Emoji", 9),
                        .BackColor = Color.Transparent,
                        .Text = GetPropertyIcon(code),
                        .TextAlign = ContentAlignment.MiddleLeft
                    }
                    AddHandler iconLabel.Click, AddressOf OnCardClick
                    propPanel.Controls.Add(iconLabel)

                    Dim nameLabel As New Label With {
                        .Location = New Point(22, 2),
                        .Size = New Size(130, 20),
                        .Font = New Font("Segoe UI", 9),
                        .BackColor = Color.Transparent,
                        .ForeColor = Color.FromArgb(99, 99, 102),
                        .Text = GetFriendlyName(code),
                        .TextAlign = ContentAlignment.MiddleLeft
                    }
                    AddHandler nameLabel.Click, AddressOf OnCardClick
                    propPanel.Controls.Add(nameLabel)

                    Dim valueLabel As New Label With {
                        .Location = New Point(155, 2),
                        .Size = New Size(135, 20),
                        .Font = New Font("Segoe UI", 10, FontStyle.Bold),
                        .BackColor = Color.Transparent,
                        .Text = FormatValue(code, value),
                        .TextAlign = ContentAlignment.MiddleRight
                    }
                    SetPropertyColor(code, valueLabel)
                    AddHandler valueLabel.Click, AddressOf OnCardClick
                    propPanel.Controls.Add(valueLabel)

                    AddHandler propPanel.Click, AddressOf OnCardClick
                    _properties(code) = valueLabel
                    _propertyCodes(valueLabel) = code
                    Me.Controls.Add(propPanel)
                Else
                    _properties(code).Text = FormatValue(code, value)

                    If code.Contains("switch") OrElse code = "doorcontact_state" Then
                        _properties(code).ForeColor = If(value.Equals("true", StringComparison.OrdinalIgnoreCase),
                                                        Color.FromArgb(50, 180, 50),
                                                        Color.FromArgb(255, 80, 80))
                    End If
                End If
            Catch ex As Exception
                Debug.WriteLine($"Erreur UpdateProperty: {ex.Message}")
            End Try
        End SyncLock

        StartFlashEffect()
    End Sub

    Private Function GetPropertyIcon(code As String) As String
        If code.Contains("temperature") OrElse code = "va_temperature" Then Return "🌡️"
        If code.Contains("humidity") OrElse code = "humidity_value" Then Return "💧"
        If code.Contains("power") OrElse code.EndsWith("_P") Then Return "⚡"
        If code.Contains("current") OrElse code.EndsWith("_I") Then Return "🔌"
        If code.Contains("voltage") OrElse code.EndsWith("_V") Then Return "🔋"
        If code.Contains("battery") Then Return "🔋"
        If code = "pir" Then Return "👁️"
        If code.Contains("switch") OrElse code = "doorcontact_state" Then Return "🎚️"
        Return "📊"
    End Function

    Private Function GetFriendlyName(code As String) As String
        If code = "va_temperature" Then Return "Température"
        If code = "humidity_value" Then Return "Humidité"
        If code = "cur_power" Then Return "Puissance actuelle"
        If code = "pir" Then Return "Mouvement"
        If code = "doorcontact_state" Then Return "Contact"
        If code = "switch_1" Then Return "Interrupteur"
        If code.EndsWith("_P") Then Return "Puissance"
        If code.EndsWith("_I") Then Return "Courant"
        If code.EndsWith("_V") Then Return "Tension"
        If code.Contains("battery") Then Return "Batterie"
        If code.Contains("phase_a") Then Return "Phase A"
        If code.Contains("phase_b") Then Return "Phase B"
        If code.Contains("phase_c") Then Return "Phase C"
        Return code
    End Function

    Private Sub SetPropertyColor(code As String, label As Label)
        If code.Contains("temperature") OrElse code = "va_temperature" Then
            label.ForeColor = Color.FromArgb(255, 149, 0)
        ElseIf code.Contains("humidity") OrElse code = "humidity_value" Then
            label.ForeColor = Color.FromArgb(0, 122, 255)
        ElseIf code = "cur_power" Then
            label.ForeColor = Color.FromArgb(175, 82, 222)
        ElseIf code.Contains("power") OrElse code.EndsWith("_P") Then
            label.ForeColor = Color.FromArgb(175, 82, 222)
        ElseIf code.Contains("current") OrElse code.EndsWith("_I") Then
            label.ForeColor = Color.FromArgb(255, 149, 0)
        ElseIf code.Contains("voltage") OrElse code.EndsWith("_V") Then
            label.ForeColor = Color.FromArgb(0, 122, 255)
        ElseIf code.Contains("battery") Then
            label.ForeColor = Color.FromArgb(52, 199, 89)
        Else
            label.ForeColor = Color.FromArgb(28, 28, 30)
        End If
    End Sub

    Private Function FormatValue(code As String, value As String) As String
        If String.IsNullOrEmpty(value) Then Return "-"

        Try
            If code = "pir" Then
                Return If(value = "pir" OrElse value = "none", "Aucun", "Détecté")
            ElseIf code.Contains("temperature") OrElse code = "va_temperature" Then
                Dim t As Double
                If Double.TryParse(value, t) Then
                    Return $"{(t / 10.0):F1} °C"
                Else
                    Return $"{value} °C"
                End If
            ElseIf code.Contains("humidity") Then
                Return $"{value} %"
            ElseIf code = "cur_power" Then
                Dim p As Double
                If Double.TryParse(value, p) Then
                    Return $"{(p / 10.0):F0} W"
                Else
                    Return $"{value} W"
                End If
            ElseIf code.Contains("power") OrElse code.EndsWith("_P") Then
                Return $"{value} W"
            ElseIf code.Contains("current") OrElse code.EndsWith("_I") Then
                Return $"{value} A"
            ElseIf code.Contains("voltage") OrElse code.EndsWith("_V") Then
                Return $"{value} V"
            ElseIf code.Contains("switch") OrElse code = "doorcontact_state" Then
                Return If(value.Equals("true", StringComparison.OrdinalIgnoreCase), "✓ ON", "✗ OFF")
            ElseIf code.Contains("battery") Then
                Return $"{value} %"
            Else
                Return value
            End If
        Catch ex As Exception
            Return value
        End Try
    End Function

    Public Sub UpdateStatus(bizCode As String)
        If Me.InvokeRequired Then
            Me.BeginInvoke(Sub() UpdateStatus(bizCode))
            Return
        End If

        If bizCode = "online" Then
            _statusLabel.Text = "🟢 En ligne"
            _statusLabel.ForeColor = Color.FromArgb(52, 199, 89)
            _footerBackColor = Color.FromArgb(240, 255, 245)
            _backgroundColor = Color.White
            _borderColor = Color.FromArgb(52, 199, 89)
        ElseIf bizCode = "offline" Then
            _statusLabel.Text = "🔴 Hors ligne"
            _statusLabel.ForeColor = Color.FromArgb(255, 59, 48)
            _footerBackColor = Color.FromArgb(255, 245, 245)
            _backgroundColor = Color.White
            _borderColor = Color.FromArgb(255, 59, 48)
        Else
            _statusLabel.Text = "❓ Inconnu"
            _statusLabel.ForeColor = Color.FromArgb(142, 142, 147)
            _footerBackColor = Color.FromArgb(248, 248, 250)
            _backgroundColor = Color.White
            _borderColor = Color.FromArgb(230, 230, 235)
        End If

        Me.Invalidate()
    End Sub

    Public Sub UpdateTimestamp()
        If Me.InvokeRequired Then
            Me.BeginInvoke(Sub() UpdateTimestamp())
            Return
        End If

        _lastUpdateTime = DateTime.Now
        _timestampLabel.Text = $"🕐 {_lastUpdateTime:HH:mm:ss}"
        _timestampLabel.ForeColor = Color.FromArgb(52, 199, 89)
        _timestampLabel.Location = New Point(_statusFooter.Width - _timestampLabel.Width - 15, 10)
    End Sub

    Public Sub RefreshTimestampColor()
        If Me.InvokeRequired Then
            Me.BeginInvoke(Sub() RefreshTimestampColor())
            Return
        End If

        If _lastUpdateTime = DateTime.MinValue Then Return

        Dim elapsed As TimeSpan = DateTime.Now - _lastUpdateTime
        If elapsed.TotalMinutes < 1 Then
            _timestampLabel.ForeColor = Color.FromArgb(52, 199, 89)
        ElseIf elapsed.TotalMinutes < 5 Then
            _timestampLabel.ForeColor = Color.FromArgb(255, 149, 0)
        Else
            _timestampLabel.ForeColor = Color.FromArgb(255, 59, 48)
        End If
    End Sub

    Public Sub SetApiClient(apiClient As TuyaApiClient)
        _apiClient = apiClient
    End Sub

    Private Sub OnCardClick(sender As Object, e As EventArgs)
        If _apiClient Is Nothing Then
            MessageBox.Show("API Client non initialisé", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim contextMenu As New ContextMenuStrip()
        contextMenu.BackColor = Color.FromArgb(45, 45, 48)
        contextMenu.ForeColor = Color.White
        contextMenu.Font = New Font("Segoe UI", 10)

        Dim controlItem As New ToolStripMenuItem("🎛️ Contrôler l'appareil")
        controlItem.ForeColor = Color.White
        Dim controlClickHandler As EventHandler = Sub(s, ev)
                                                      Dim propertiesCopy As New Dictionary(Of String, String)
                                                      SyncLock _lockObject
                                                          For Each kvp In _propertyCodes
                                                              Dim valueLabel As Label = kvp.Key
                                                              Dim code As String = kvp.Value
                                                              propertiesCopy(code) = valueLabel.Text
                                                          Next
                                                      End SyncLock

                                                      Dim controlForm As New DeviceControlForm(_deviceId, _deviceName, _apiClient, propertiesCopy)
                                                      controlForm.ShowDialog()
                                                  End Sub
        AddHandler controlItem.Click, controlClickHandler
        contextMenu.Items.Add(controlItem)

        contextMenu.Items.Add(New ToolStripSeparator())

        Dim renameItem As New ToolStripMenuItem("✏️ Renommer l'appareil")
        renameItem.ForeColor = Color.White
        AddHandler renameItem.Click, AddressOf RenameDevice_Click
        contextMenu.Items.Add(renameItem)

        contextMenu.Show(Me, Me.PointToClient(Cursor.Position))
    End Sub

    Private Async Sub RenameDevice_Click(sender As Object, e As EventArgs)
        Try
            Dim inputForm As New Form()
            inputForm.Text = "Renommer l'appareil"
            inputForm.Size = New Size(400, 150)
            inputForm.StartPosition = FormStartPosition.CenterParent
            inputForm.FormBorderStyle = FormBorderStyle.FixedDialog
            inputForm.MaximizeBox = False
            inputForm.MinimizeBox = False
            inputForm.BackColor = Color.FromArgb(242, 242, 247)

            Dim label As New Label()
            label.Text = "Nouveau nom :"
            label.Location = New Point(20, 20)
            label.Size = New Size(100, 20)
            label.Font = New Font("Segoe UI", 10)
            inputForm.Controls.Add(label)

            Dim textBox As New TextBox()
            textBox.Text = _deviceName
            textBox.Location = New Point(20, 45)
            textBox.Size = New Size(340, 25)
            textBox.Font = New Font("Segoe UI", 10)
            textBox.SelectAll()
            inputForm.Controls.Add(textBox)

            Dim okButton As New Button()
            okButton.Text = "OK"
            okButton.DialogResult = DialogResult.OK
            okButton.Location = New Point(200, 80)
            okButton.Size = New Size(75, 30)
            okButton.BackColor = Color.FromArgb(0, 122, 255)
            okButton.ForeColor = Color.White
            okButton.FlatStyle = FlatStyle.Flat
            okButton.Font = New Font("Segoe UI", 10)
            inputForm.Controls.Add(okButton)
            inputForm.AcceptButton = okButton

            Dim cancelButton As New Button()
            cancelButton.Text = "Annuler"
            cancelButton.DialogResult = DialogResult.Cancel
            cancelButton.Location = New Point(285, 80)
            cancelButton.Size = New Size(75, 30)
            cancelButton.BackColor = Color.FromArgb(142, 142, 147)
            cancelButton.ForeColor = Color.White
            cancelButton.FlatStyle = FlatStyle.Flat
            cancelButton.Font = New Font("Segoe UI", 10)
            inputForm.Controls.Add(cancelButton)
            inputForm.CancelButton = cancelButton

            If inputForm.ShowDialog() = DialogResult.OK Then
                Dim newName As String = textBox.Text.Trim()

                If String.IsNullOrEmpty(newName) Then
                    MessageBox.Show("Le nom ne peut pas être vide", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Return
                End If

                If newName = _deviceName Then Return

                Dim originalBackColor = Me.BackColor
                Me.BackColor = Color.FromArgb(255, 250, 200)
                Me.Update()

                Dim success As Boolean = Await _apiClient.RenameDeviceAsync(_deviceId, newName)

                Me.BackColor = originalBackColor

                If success Then
                    _deviceName = newName
                    _titleLabel.Text = newName
                    MessageBox.Show($"✅ Appareil renommé en '{newName}'", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Else
                    MessageBox.Show("❌ Échec du renommage. Vérifiez les logs pour plus de détails.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End If
            End If

        Catch ex As Exception
            MessageBox.Show($"❌ Erreur : {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

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
            _borderColor = Color.FromArgb(0, 122, 255)
            _backgroundColor = Color.FromArgb(230, 245, 255)
        Else
            _flashTimer.Stop()
            _flashCount = 0
            _isFlashing = False
            _borderColor = _originalBorderColor
            _backgroundColor = _originalBackgroundColor
        End If

        Me.Invalidate()
    End Sub

End Class