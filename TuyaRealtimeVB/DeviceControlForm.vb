Imports System.Drawing
Imports System.Windows.Forms

Public Class DeviceControlForm
    Inherits Form

    Private _deviceId As String
    Private _deviceName As String
    Private _deviceCategory As String
    Private _apiClient As TuyaApiClient
    Private _currentProperties As Dictionary(Of String, String)
    Private _controlsPanel As FlowLayoutPanel
    Private _statusLabel As Label

    Public Sub New(deviceId As String, deviceName As String, deviceCategory As String, apiClient As TuyaApiClient, currentProperties As Dictionary(Of String, String))
        _deviceId = deviceId
        _deviceName = deviceName
        _deviceCategory = deviceCategory
        _apiClient = apiClient
        _currentProperties = currentProperties

        InitializeComponent()
        LoadControls()
    End Sub

    Private Sub InitializeComponent()
        Me.Text = $"Contrôle - {_deviceName}"
        Me.Size = New Size(500, 800)
        Me.StartPosition = FormStartPosition.CenterParent
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.BackColor = Color.FromArgb(242, 242, 247)

        ' Header FIXE (position et taille fixes, pas de Dock)
        Dim headerPanel As New Panel With {
            .Location = New Point(0, 0),
            .Size = New Size(Me.ClientSize.Width, 80),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right,
            .BackColor = Color.White,
            .Padding = New Padding(20)
        }

        Dim titleLabel As New Label With {
            .Text = _deviceName,
            .Font = New Font("Segoe UI", 16, FontStyle.Bold),
            .ForeColor = Color.FromArgb(28, 28, 30),
            .AutoSize = True,
            .Location = New Point(20, 15)
        }
        headerPanel.Controls.Add(titleLabel)

        Dim idLabel As New Label With {
            .Text = $"ID: {_deviceId}",
            .Font = New Font("Segoe UI", 8),
            .ForeColor = Color.FromArgb(142, 142, 147),
            .AutoSize = True,
            .Location = New Point(20, 48)
        }
        headerPanel.Controls.Add(idLabel)
        Me.Controls.Add(headerPanel)

        ' Status bar (Dock.Bottom)
        Dim bottomPanel As New Panel With {
            .Dock = DockStyle.Bottom,
            .Height = 50,
            .BackColor = Color.White,
            .Padding = New Padding(20, 15, 20, 15)
        }

        _statusLabel = New Label With {
            .Text = "Prêt",
            .Font = New Font("Segoe UI", 9),
            .ForeColor = Color.FromArgb(99, 99, 102),
            .Dock = DockStyle.Fill
        }
        bottomPanel.Controls.Add(_statusLabel)
        Me.Controls.Add(bottomPanel)

        ' SplitContainer (positionné SOUS le header, au dessus de la status bar)
        Dim mainSplit As New SplitContainer With {
            .Location = New Point(0, 80),
            .Size = New Size(Me.ClientSize.Width, Me.ClientSize.Height - 80 - 50),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right,
            .Orientation = Orientation.Horizontal,
            .BackColor = Color.FromArgb(242, 242, 247)
        }

        ' Panel de contrôles (en haut)
        _controlsPanel = New FlowLayoutPanel With {
            .Dock = DockStyle.Fill,
            .AutoScroll = True,
            .FlowDirection = FlowDirection.TopDown,
            .WrapContents = False,
            .Padding = New Padding(20),
            .BackColor = Color.FromArgb(242, 242, 247)
        }
        mainSplit.Panel1.Controls.Add(_controlsPanel)

        ' Panel des infos JSON (en bas)
        Dim jsonPanel As New Panel With {
            .Dock = DockStyle.Fill,
            .BackColor = Color.White,
            .Padding = New Padding(20)
        }

        Dim jsonTitleLabel As New Label With {
            .Text = "Informations techniques",
            .Font = New Font("Segoe UI", 11, FontStyle.Bold),
            .ForeColor = Color.FromArgb(28, 28, 30),
            .AutoSize = True,
            .Location = New Point(20, 10)
        }
        jsonPanel.Controls.Add(jsonTitleLabel)

        Dim jsonTextBox As New TextBox With {
            .Location = New Point(20, 40),
            .Size = New Size(jsonPanel.Width - 40, jsonPanel.Height - 60),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right,
            .Multiline = True,
            .ReadOnly = True,
            .ScrollBars = ScrollBars.Vertical,
            .Font = New Font("Consolas", 9),
            .BackColor = Color.FromArgb(248, 248, 250),
            .ForeColor = Color.FromArgb(28, 28, 30),
            .BorderStyle = BorderStyle.FixedSingle
        }
        jsonPanel.Controls.Add(jsonTextBox)

        mainSplit.Panel2.Controls.Add(jsonPanel)

        ' Calculer dynamiquement la distance du splitter selon le contenu
        Dim estimatedHeight As Integer = 120 ' Base réduite

        If _currentProperties IsNot Nothing Then
            For Each prop In _currentProperties
                Dim code As String = prop.Key

                If code.Contains("switch") OrElse code = "doorcontact_state" Then
                    estimatedHeight += 80
                ElseIf code.Contains("mode") Then
                    estimatedHeight += 100
                ElseIf code.Contains("temp_set") OrElse code.Contains("target_temp") Then
                    estimatedHeight += 110
                Else
                    estimatedHeight += 58
                End If
            Next
        End If

        ' Limites ajustées
        If estimatedHeight < 120 Then estimatedHeight = 120
        If estimatedHeight > 400 Then estimatedHeight = 400

        mainSplit.SplitterDistance = estimatedHeight

        Me.Controls.Add(mainSplit)

        ' Charger les infos JSON de l'appareil
        LoadDeviceJsonAsync(jsonTextBox)
    End Sub

    Private Async Sub LoadDeviceJsonAsync(jsonTextBox As TextBox)
        Try
            jsonTextBox.Text = "Chargement des informations..."

            ' Récupérer les infos complètes de l'appareil
            Dim deviceJson = Await _apiClient.GetDeviceFullInfoAsync(_deviceId)

            If deviceJson IsNot Nothing Then
                ' Formater le JSON pour l'affichage
                Dim formattedJson As String = Newtonsoft.Json.JsonConvert.SerializeObject(
                    deviceJson,
                    Newtonsoft.Json.Formatting.Indented
                )
                jsonTextBox.Text = formattedJson
            Else
                jsonTextBox.Text = "Erreur : Impossible de récupérer les informations de l'appareil"
            End If

        Catch ex As Exception
            jsonTextBox.Text = $"Erreur lors du chargement des informations :{Environment.NewLine}{ex.Message}"
        End Try
    End Sub

    Private Sub LoadControls()
        _controlsPanel.Controls.Clear()

        If _currentProperties Is Nothing OrElse _currentProperties.Count = 0 Then
            Dim noDataLabel As New Label With {
                .Text = "Aucune propriété contrôlable disponible",
                .Font = New Font("Segoe UI", 10),
                .ForeColor = Color.FromArgb(142, 142, 147),
                .AutoSize = True,
                .Padding = New Padding(10)
            }
            _controlsPanel.Controls.Add(noDataLabel)
            Return
        End If

        Dim hasInteractiveControls As Boolean = False

        ' Créer les contrôles pour chaque propriété
        For Each prop In _currentProperties
            Dim code As String = prop.Key
            Dim value As String = prop.Value

            ' Interrupteurs (switch)
            If code.Contains("switch") OrElse code = "doorcontact_state" Then
                CreateSwitchControl(code, value)
                hasInteractiveControls = True

                ' Mode (pour les chauffages)
            ElseIf code.Contains("mode") Then
                CreateModeControl(code, value)
                hasInteractiveControls = True

                ' Température (si réglable)
            ElseIf code.Contains("temp_set") OrElse code.Contains("target_temp") Then
                CreateTemperatureControl(code, value)
                hasInteractiveControls = True

                ' Autres propriétés en lecture seule pour le moment
            Else
                CreateReadOnlyControl(code, value)
            End If
        Next

        ' Si aucun contrôle interactif, afficher un message
        If Not hasInteractiveControls Then
            Dim infoLabel As New Label With {
                .Text = "Cet appareil n'a que des propriétés en lecture seule",
                .Font = New Font("Segoe UI", 9, FontStyle.Italic),
                .ForeColor = Color.FromArgb(142, 142, 147),
                .AutoSize = True,
                .Padding = New Padding(10, 5, 10, 10)
            }
            _controlsPanel.Controls.Add(infoLabel)
        End If
    End Sub

    Private Sub CreateSwitchControl(code As String, currentValue As String)
        Dim controlCard As New Panel With {
            .Width = _controlsPanel.ClientSize.Width - 40,
            .Height = 70,
            .BackColor = Color.White,
            .Margin = New Padding(0, 0, 0, 12),
            .Padding = New Padding(15)
        }

        ' Arrondir les coins
        AddHandler controlCard.Paint, Sub(sender, e)
                                          Dim g As Graphics = e.Graphics
                                          g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
                                          Using path As New Drawing2D.GraphicsPath()
                                              Dim radius As Integer = 12
                                              Dim rect As New Rectangle(0, 0, controlCard.Width - 1, controlCard.Height - 1)
                                              path.AddArc(rect.X, rect.Y, radius, radius, 180, 90)
                                              path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90)
                                              path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90)
                                              path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90)
                                              path.CloseFigure()
                                              controlCard.Region = New Region(path)
                                          End Using
                                      End Sub

        ' Label du nom
        Dim nameLabel As New Label With {
            .Text = GetFriendlyName(code),
            .Font = New Font("Segoe UI", 11, FontStyle.Bold),
            .ForeColor = Color.FromArgb(28, 28, 30),
            .AutoSize = True,
            .Location = New Point(15, 12)
        }
        controlCard.Controls.Add(nameLabel)

        ' État actuel
        Dim isOn As Boolean = currentValue.Equals("true", StringComparison.OrdinalIgnoreCase)
        Dim stateLabel As New Label With {
            .Text = If(isOn, "Activé", "Désactivé"),
            .Font = New Font("Segoe UI", 9),
            .ForeColor = Color.FromArgb(142, 142, 147),
            .AutoSize = True,
            .Location = New Point(15, 35)
        }
        controlCard.Controls.Add(stateLabel)

        ' Toggle button
        Dim toggleButton As New Button With {
            .Text = If(isOn, "✓ ON", "OFF"),
            .Font = New Font("Segoe UI", 10, FontStyle.Bold),
            .Size = New Size(80, 35),
            .Location = New Point(controlCard.Width - 95, 17),
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand,
            .BackColor = If(isOn, Color.FromArgb(52, 199, 89), Color.FromArgb(142, 142, 147)),
            .ForeColor = Color.White
        }
        toggleButton.FlatAppearance.BorderSize = 0

        AddHandler toggleButton.Click, Async Sub(s, e)
                                           Dim newState As Boolean = Not isOn
                                           toggleButton.Enabled = False
                                           _statusLabel.Text = "Envoi de la commande..."
                                           _statusLabel.ForeColor = Color.FromArgb(0, 122, 255)

                                           Try
                                               ' Envoyer la commande avec la valeur booléenne
                                               Await SendCommandAsync(code, newState)

                                               isOn = newState
                                               toggleButton.Text = If(isOn, "✓ ON", "OFF")
                                               toggleButton.BackColor = If(isOn, Color.FromArgb(52, 199, 89), Color.FromArgb(142, 142, 147))
                                               stateLabel.Text = If(isOn, "Activé", "Désactivé")

                                               _statusLabel.Text = "Commande envoyée avec succès"
                                               _statusLabel.ForeColor = Color.FromArgb(52, 199, 89)
                                           Catch ex As Exception
                                               _statusLabel.Text = $"Erreur : {ex.Message}"
                                               _statusLabel.ForeColor = Color.FromArgb(255, 59, 48)
                                               MessageBox.Show($"Erreur lors de l'envoi de la commande :{Environment.NewLine}{ex.Message}",
                                                             "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
                                           Finally
                                               toggleButton.Enabled = True
                                           End Try
                                       End Sub

        controlCard.Controls.Add(toggleButton)
        _controlsPanel.Controls.Add(controlCard)
    End Sub

    Private Sub CreateModeControl(code As String, currentValue As String)
        Dim controlCard As New Panel With {
            .Width = _controlsPanel.ClientSize.Width - 40,
            .Height = 90,
            .BackColor = Color.White,
            .Margin = New Padding(0, 0, 0, 12),
            .Padding = New Padding(15)
        }

        AddHandler controlCard.Paint, Sub(sender, e)
                                          Dim g As Graphics = e.Graphics
                                          g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
                                          Using path As New Drawing2D.GraphicsPath()
                                              Dim radius As Integer = 12
                                              Dim rect As New Rectangle(0, 0, controlCard.Width - 1, controlCard.Height - 1)
                                              path.AddArc(rect.X, rect.Y, radius, radius, 180, 90)
                                              path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90)
                                              path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90)
                                              path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90)
                                              path.CloseFigure()
                                              controlCard.Region = New Region(path)
                                          End Using
                                      End Sub

        ' Label du nom
        Dim nameLabel As New Label With {
            .Text = GetFriendlyName(code),
            .Font = New Font("Segoe UI", 11, FontStyle.Bold),
            .ForeColor = Color.FromArgb(28, 28, 30),
            .AutoSize = True,
            .Location = New Point(15, 12)
        }
        controlCard.Controls.Add(nameLabel)

        ' Valeur actuelle
        Dim stateLabel As New Label With {
            .Text = $"Actuel : {currentValue}",
            .Font = New Font("Segoe UI", 9),
            .ForeColor = Color.FromArgb(142, 142, 147),
            .AutoSize = True,
            .Location = New Point(15, 35)
        }
        controlCard.Controls.Add(stateLabel)

        ' ComboBox pour sélectionner le mode
        Dim modeCombo As New ComboBox With {
            .Location = New Point(15, 55),
            .Width = controlCard.Width - 30,
            .Font = New Font("Segoe UI", 10),
            .DropDownStyle = ComboBoxStyle.DropDownList
        }

        ' Modes typiques pour les chauffages Tuya (basé sur la doc)
        Dim modes As String() = {"hot", "eco", "cold", "auto", "manual", "comfort", "holiday", "program"}
        modeCombo.Items.AddRange(modes)

        ' Sélectionner le mode actuel s'il existe dans la liste
        If modeCombo.Items.Contains(currentValue) Then
            modeCombo.SelectedItem = currentValue
        Else
            ' Ajouter le mode actuel s'il n'est pas dans la liste
            modeCombo.Items.Insert(0, currentValue)
            modeCombo.SelectedItem = currentValue
        End If

        AddHandler modeCombo.SelectedIndexChanged, Async Sub(s, e)
                                                       If modeCombo.SelectedItem IsNot Nothing Then
                                                           Dim selectedMode As String = modeCombo.SelectedItem.ToString()
                                                           If selectedMode = currentValue Then Return ' Pas de changement

                                                           modeCombo.Enabled = False
                                                           _statusLabel.Text = "Envoi de la commande..."
                                                           _statusLabel.ForeColor = Color.FromArgb(0, 122, 255)

                                                           Try
                                                               Await SendCommandAsync(code, selectedMode)

                                                               currentValue = selectedMode
                                                               stateLabel.Text = $"Actuel : {selectedMode}"

                                                               _statusLabel.Text = "Mode changé avec succès"
                                                               _statusLabel.ForeColor = Color.FromArgb(52, 199, 89)
                                                           Catch ex As Exception
                                                               _statusLabel.Text = $"Erreur : {ex.Message}"
                                                               _statusLabel.ForeColor = Color.FromArgb(255, 59, 48)
                                                               MessageBox.Show($"Erreur lors du changement de mode :{Environment.NewLine}{ex.Message}",
                                                                             "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
                                                               ' Remettre le mode précédent
                                                               modeCombo.SelectedItem = currentValue
                                                           Finally
                                                               modeCombo.Enabled = True
                                                           End Try
                                                       End If
                                                   End Sub

        controlCard.Controls.Add(modeCombo)
        _controlsPanel.Controls.Add(controlCard)
    End Sub

    Private Sub CreateTemperatureControl(code As String, currentValue As String)
        ' Récupérer les specs pour extraire le scale et les limites
        Dim specs = _apiClient.GetCachedDeviceSpecification(_deviceId)
        Dim specItem As Newtonsoft.Json.Linq.JToken = Nothing
        Dim scale As Integer = 0
        Dim unit As String = "°C"
        Dim minValue As Decimal = 15
        Dim maxValue As Decimal = 30
        Dim stepValue As Decimal = 1

        If specs IsNot Nothing AndAlso specs("functions") IsNot Nothing Then
            For Each func As Newtonsoft.Json.Linq.JToken In specs("functions")
                If func("code")?.ToString() = code Then
                    specItem = func
                    Exit For
                End If
            Next
        End If

        If specItem IsNot Nothing Then
            Dim valuesJson = specItem("values")?.ToString()
            If Not String.IsNullOrEmpty(valuesJson) Then
                Try
                    Dim values = Newtonsoft.Json.Linq.JObject.Parse(valuesJson)
                    scale = If(values("scale") IsNot Nothing, CInt(values("scale")), 0)
                    unit = If(values("unit") IsNot Nothing, values("unit").ToString(), "°C")

                    Dim scaleFactor = CDec(Math.Pow(10, scale))
                    minValue = CDec(CInt(values("min")) / scaleFactor)
                    maxValue = CDec(CInt(values("max")) / scaleFactor)
                    stepValue = CDec(If(values("step") IsNot Nothing, CInt(values("step")), 1) / scaleFactor)
                Catch ex As Exception
                    ' Utiliser les valeurs par défaut
                End Try
            End If
        End If

        Dim controlCard As New Panel With {
            .Width = _controlsPanel.ClientSize.Width - 40,
            .Height = 100,
            .BackColor = Color.White,
            .Margin = New Padding(0, 0, 0, 12),
            .Padding = New Padding(15)
        }

        AddHandler controlCard.Paint, Sub(sender, e)
                                          Dim g As Graphics = e.Graphics
                                          g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
                                          Using path As New Drawing2D.GraphicsPath()
                                              Dim radius As Integer = 12
                                              Dim rect As New Rectangle(0, 0, controlCard.Width - 1, controlCard.Height - 1)
                                              path.AddArc(rect.X, rect.Y, radius, radius, 180, 90)
                                              path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90)
                                              path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90)
                                              path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90)
                                              path.CloseFigure()
                                              controlCard.Region = New Region(path)
                                          End Using
                                      End Sub

        Dim nameLabel As New Label With {
            .Text = GetFriendlyName(code),
            .Font = New Font("Segoe UI", 11, FontStyle.Bold),
            .ForeColor = Color.FromArgb(28, 28, 30),
            .AutoSize = True,
            .Location = New Point(15, 12)
        }
        controlCard.Controls.Add(nameLabel)

        ' Convertir la valeur actuelle en appliquant le scale
        Dim rawValue As Decimal
        Dim displayValue As Decimal = minValue
        If Decimal.TryParse(currentValue, rawValue) Then
            Dim scaleFactor = CDec(Math.Pow(10, scale))
            displayValue = rawValue / scaleFactor
        End If

        Dim valueLabel As New Label With {
            .Text = $"{displayValue.ToString($"F{scale}")}{unit}",
            .Font = New Font("Segoe UI", 10, FontStyle.Bold),
            .ForeColor = Color.FromArgb(255, 149, 0),
            .Location = New Point(controlCard.Width - 100, 12),
            .AutoSize = True
        }
        controlCard.Controls.Add(valueLabel)

        Dim slider As New TrackBar With {
            .Minimum = CInt(minValue),
            .Maximum = CInt(maxValue),
            .Value = Math.Max(CInt(minValue), Math.Min(CInt(maxValue), CInt(displayValue))),
            .TickFrequency = Math.Max(1, CInt(stepValue)),
            .Location = New Point(15, 45),
            .Width = controlCard.Width - 30
        }

        AddHandler slider.ValueChanged, Sub(s, e)
                                            valueLabel.Text = $"{slider.Value.ToString($"F{scale}")}{unit}"
                                        End Sub

        AddHandler slider.MouseUp, Async Sub(s, e)
                                       slider.Enabled = False
                                       _statusLabel.Text = "Envoi de la commande..."
                                       _statusLabel.ForeColor = Color.FromArgb(0, 122, 255)

                                       Try
                                           ' Multiplier par le scale avant envoi
                                           Dim scaleFactor = CDec(Math.Pow(10, scale))
                                           Dim apiValue = CInt(slider.Value * scaleFactor)

                                           Await SendCommandAsync(code, apiValue.ToString())
                                           _statusLabel.Text = "Température mise à jour"
                                           _statusLabel.ForeColor = Color.FromArgb(52, 199, 89)
                                       Catch ex As Exception
                                           _statusLabel.Text = $"Erreur : {ex.Message}"
                                           _statusLabel.ForeColor = Color.FromArgb(255, 59, 48)
                                       Finally
                                           slider.Enabled = True
                                       End Try
                                   End Sub

        controlCard.Controls.Add(slider)
        _controlsPanel.Controls.Add(controlCard)
    End Sub

    Private Sub CreateReadOnlyControl(code As String, value As String)
        Dim controlCard As New Panel With {
            .Width = _controlsPanel.ClientSize.Width - 40,
            .Height = 50,
            .BackColor = Color.FromArgb(250, 250, 252),
            .Margin = New Padding(0, 0, 0, 8),
            .Padding = New Padding(12)
        }

        AddHandler controlCard.Paint, Sub(sender, e)
                                          Dim g As Graphics = e.Graphics
                                          g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
                                          Using path As New Drawing2D.GraphicsPath()
                                              Dim radius As Integer = 12
                                              Dim rect As New Rectangle(0, 0, controlCard.Width - 1, controlCard.Height - 1)
                                              path.AddArc(rect.X, rect.Y, radius, radius, 180, 90)
                                              path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90)
                                              path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90)
                                              path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90)
                                              path.CloseFigure()
                                              controlCard.Region = New Region(path)
                                          End Using
                                      End Sub

        Dim nameLabel As New Label With {
            .Text = GetFriendlyName(code),
            .Font = New Font("Segoe UI", 10),
            .ForeColor = Color.FromArgb(99, 99, 102),
            .AutoSize = True,
            .Location = New Point(15, 12)
        }
        controlCard.Controls.Add(nameLabel)

        Dim valueLabel As New Label With {
            .Text = FormatValue(code, value),
            .Font = New Font("Segoe UI", 11, FontStyle.Bold),
            .ForeColor = Color.FromArgb(28, 28, 30),
            .AutoSize = True,
            .Location = New Point(15, 32)
        }
        controlCard.Controls.Add(valueLabel)

        Dim readOnlyLabel As New Label With {
            .Text = "(lecture seule)",
            .Font = New Font("Segoe UI", 8),
            .ForeColor = Color.FromArgb(142, 142, 147),
            .Location = New Point(controlCard.Width - 100, 20),
            .AutoSize = True
        }
        controlCard.Controls.Add(readOnlyLabel)

        _controlsPanel.Controls.Add(controlCard)
    End Sub

    Private Async Function SendCommandAsync(code As String, value As Object) As Task
        ' Utiliser l'API Tuya pour envoyer la commande
        Dim commands As New Dictionary(Of String, Object) From {
            {code, value}
        }

        Await _apiClient.SendDeviceCommandAsync(_deviceId, commands)
    End Function

    Private Function GetFriendlyName(code As String) As String
        Dim categoryManager = TuyaCategoryManager.Instance
        Return categoryManager.GetDisplayName(_deviceCategory, code)
    End Function

    Private Function FormatValue(code As String, value As String) As String
        Dim categoryManager = TuyaCategoryManager.Instance
        Return categoryManager.FormatValue(_deviceCategory, code, value)
    End Function
End Class