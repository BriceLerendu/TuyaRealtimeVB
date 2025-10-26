Imports System.Drawing
Imports System.Windows.Forms
Imports ScottPlot.WinForms
Imports WinColor = System.Drawing.Color
Imports WinFontStyle = System.Drawing.FontStyle

''' <summary>
''' Fenêtre d'affichage de l'historique et des statistiques d'un appareil
''' </summary>
Public Class HistoryForm
    Inherits Form

    Private _deviceId As String
    Private _deviceName As String
    Private _historyService As TuyaHistoryService
    Private _currentPeriod As HistoryPeriod = HistoryPeriod.Last24Hours

    ' Contrôles UI
    Private _periodComboBox As ComboBox
    Private _propertyComboBox As ComboBox
    Private _chartPanel As Panel
    Private _statsChart As FormsPlot
    Private _logsListView As ListView
    Private _loadingLabel As System.Windows.Forms.Label
    Private _closeButton As Button
    Private _refreshButton As Button
    Private _availableCodes As List(Of String)
    Private _selectedCode As String = Nothing

    Public Sub New(deviceId As String, deviceName As String, historyService As TuyaHistoryService)
        _deviceId = deviceId
        _deviceName = deviceName
        _historyService = historyService

        InitializeComponent()
        LoadHistoryAsync()
    End Sub

    Private Sub InitializeComponent()
        Me.Text = $"📊 Historique - {_deviceName}"
        Me.Size = New Size(1000, 700)
        Me.StartPosition = FormStartPosition.CenterParent
        Me.FormBorderStyle = FormBorderStyle.Sizable
        Me.MinimumSize = New Size(800, 600)
        Me.BackColor = WinColor.FromArgb(242, 242, 247)

        ' Panel principal
        Dim mainPanel As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .Padding = New Padding(20)
        }

        ' Configuration des lignes
        mainPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 60))  ' Header
        mainPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 60))   ' Graphique
        mainPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 40))   ' Timeline

        ' ═══════════════════════════════════════════════════════
        ' HEADER avec sélecteur de période
        ' ═══════════════════════════════════════════════════════
        Dim headerPanel As New Panel With {
            .Dock = DockStyle.Fill,
            .BackColor = WinColor.White,
            .Padding = New Padding(15)
        }

        Dim periodLabel As New System.Windows.Forms.Label With {
            .Text = "Période :",
            .Location = New Point(15, 18),
            .AutoSize = True,
            .Font = New Font("Segoe UI", 10, WinFontStyle.Bold),
            .ForeColor = WinColor.FromArgb(28, 28, 30)
        }
        headerPanel.Controls.Add(periodLabel)

        _periodComboBox = New ComboBox With {
            .Location = New Point(90, 15),
            .Size = New Size(200, 25),
            .Font = New Font("Segoe UI", 10),
            .DropDownStyle = ComboBoxStyle.DropDownList
        }
        _periodComboBox.Items.AddRange(New String() {
            "Dernières 24 heures"
        })
        _periodComboBox.SelectedIndex = 0 ' 24h uniquement
        AddHandler _periodComboBox.SelectedIndexChanged, AddressOf PeriodComboBox_SelectedIndexChanged
        headerPanel.Controls.Add(_periodComboBox)

        ' Sélecteur de propriété
        Dim propertyLabel As New System.Windows.Forms.Label With {
            .Text = "Propriété :",
            .Location = New Point(310, 18),
            .AutoSize = True,
            .Font = New Font("Segoe UI", 10, WinFontStyle.Bold),
            .ForeColor = WinColor.FromArgb(28, 28, 30)
        }
        headerPanel.Controls.Add(propertyLabel)

        _propertyComboBox = New ComboBox With {
            .Location = New Point(395, 15),
            .Size = New Size(250, 25),
            .Font = New Font("Segoe UI", 10),
            .DropDownStyle = ComboBoxStyle.DropDownList,
            .Enabled = False
        }
        AddHandler _propertyComboBox.SelectedIndexChanged, AddressOf PropertyComboBox_SelectedIndexChanged
        headerPanel.Controls.Add(_propertyComboBox)

        _refreshButton = New Button With {
            .Text = "🔄 Actualiser",
            .Location = New Point(665, 14),
            .Size = New Size(120, 30),
            .Font = New Font("Segoe UI", 9, WinFontStyle.Bold),
            .BackColor = WinColor.FromArgb(0, 122, 255),
            .ForeColor = WinColor.White,
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand
        }
        _refreshButton.FlatAppearance.BorderSize = 0
        AddHandler _refreshButton.Click, AddressOf RefreshButton_Click
        headerPanel.Controls.Add(_refreshButton)

        _loadingLabel = New System.Windows.Forms.Label With {
            .Text = "Chargement...",
            .Location = New Point(805, 18),
            .AutoSize = True,
            .Font = New Font("Segoe UI", 9, WinFontStyle.Italic),
            .ForeColor = WinColor.FromArgb(128, 128, 128),
            .Visible = False
        }
        headerPanel.Controls.Add(_loadingLabel)

        mainPanel.Controls.Add(headerPanel, 0, 0)

        ' ═══════════════════════════════════════════════════════
        ' GRAPHIQUE
        ' ═══════════════════════════════════════════════════════
        _chartPanel = New Panel With {
            .Dock = DockStyle.Fill,
            .BackColor = WinColor.White,
            .Margin = New Padding(0, 10, 0, 10)
        }

        _statsChart = New FormsPlot With {
            .Dock = DockStyle.Fill
        }
        _chartPanel.Controls.Add(_statsChart)

        mainPanel.Controls.Add(_chartPanel, 0, 1)

        ' ═══════════════════════════════════════════════════════
        ' TIMELINE DES ÉVÉNEMENTS
        ' ═══════════════════════════════════════════════════════
        Dim timelinePanel As New Panel With {
            .Dock = DockStyle.Fill,
            .BackColor = WinColor.White
        }

        Dim timelineLabel As New System.Windows.Forms.Label With {
            .Text = "📝 Historique des événements",
            .Location = New Point(15, 10),
            .AutoSize = True,
            .Font = New Font("Segoe UI", 10, WinFontStyle.Bold),
            .ForeColor = WinColor.FromArgb(28, 28, 30)
        }
        timelinePanel.Controls.Add(timelineLabel)

        _logsListView = New ListView With {
            .Location = New Point(15, 40),
            .Size = New Size(timelinePanel.Width - 30, timelinePanel.Height - 50),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right,
            .View = View.Details,
            .FullRowSelect = True,
            .GridLines = True,
            .Font = New Font("Segoe UI", 9)
        }

        _logsListView.Columns.Add("Heure", 150)
        _logsListView.Columns.Add("Événement", 500)

        timelinePanel.Controls.Add(_logsListView)

        mainPanel.Controls.Add(timelinePanel, 0, 2)

        ' ═══════════════════════════════════════════════════════
        ' BOUTON FERMER EN BAS
        ' ═══════════════════════════════════════════════════════
        Dim buttonPanel As New Panel With {
            .Dock = DockStyle.Bottom,
            .Height = 60,
            .BackColor = WinColor.White,
            .Padding = New Padding(20, 10, 20, 10)
        }

        _closeButton = New Button With {
            .Text = "✖ Fermer",
            .Location = New Point(buttonPanel.Width - 130, 15),
            .Size = New Size(100, 35),
            .Anchor = AnchorStyles.Right,
            .Font = New Font("Segoe UI", 10, WinFontStyle.Bold),
            .BackColor = WinColor.FromArgb(142, 142, 147),
            .ForeColor = WinColor.White,
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand
        }
        _closeButton.FlatAppearance.BorderSize = 0
        AddHandler _closeButton.Click, Sub() Me.Close()
        buttonPanel.Controls.Add(_closeButton)

        Me.Controls.Add(mainPanel)
        Me.Controls.Add(buttonPanel)
    End Sub

    Private Async Sub LoadHistoryAsync()
        Try
            _loadingLabel.Visible = True
            _periodComboBox.Enabled = False
            _propertyComboBox.Enabled = False
            _refreshButton.Enabled = False

            ' 1. Charger la liste des codes disponibles si pas encore fait
            If _availableCodes Is Nothing OrElse _availableCodes.Count = 0 Then
                _availableCodes = Await _historyService.GetAvailableCodesAsync(_deviceId, _currentPeriod)

                If _availableCodes IsNot Nothing AndAlso _availableCodes.Count > 0 Then
                    ' Remplir le ComboBox avec les codes disponibles
                    _propertyComboBox.Items.Clear()
                    _propertyComboBox.Items.Add("(Auto-détection)")
                    For Each code In _availableCodes
                        _propertyComboBox.Items.Add(code)
                    Next
                    _propertyComboBox.SelectedIndex = 0 ' Sélectionner auto-détection par défaut
                    _propertyComboBox.Enabled = True
                End If
            End If

            ' 2. Charger statistiques et logs en parallèle
            Dim statsTask = _historyService.GetDeviceStatisticsAsync(_deviceId, _currentPeriod, _selectedCode)
            Dim logsTask = _historyService.GetDeviceLogsAsync(_deviceId, _currentPeriod)

            Await Task.WhenAll(statsTask, logsTask)

            Dim stats = Await statsTask
            Dim logs = Await logsTask

            ' 3. Afficher graphique
            If stats IsNot Nothing AndAlso stats.DataPoints.Count > 0 Then
                DrawStatisticsChart(stats)
            Else
                Dim codeInfo = If(_selectedCode, If(stats?.Code, "aucun code"))
                DrawNoDataMessage("Aucune donnée disponible" & vbCrLf &
                                 "Code: " & codeInfo & vbCrLf &
                                 "Essayez de sélectionner une autre propriété")
            End If

            ' 4. Afficher timeline
            If logs IsNot Nothing AndAlso logs.Count > 0 Then
                DisplayLogs(logs)
            Else
                ' Afficher un message dans la liste si aucun log
                _logsListView.Items.Clear()
                Dim item As New ListViewItem("Aucun événement trouvé")
                item.SubItems.Add("")
                item.ForeColor = WinColor.Gray
                _logsListView.Items.Add(item)
            End If

        Catch ex As Exception
            MessageBox.Show($"Erreur lors du chargement de l'historique:{vbCrLf}{ex.Message}" & vbCrLf & vbCrLf &
                          $"Type: {ex.GetType().Name}",
                          "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            _loadingLabel.Visible = False
            _periodComboBox.Enabled = True
            _propertyComboBox.Enabled = True
            _refreshButton.Enabled = True
        End Try
    End Sub

    Private Sub DrawStatisticsChart(stats As DeviceStatistics)
        ' Dispatcher selon le type de visualisation détecté automatiquement
        Select Case stats.VisualizationType
            Case SensorVisualizationType.NumericContinuous
                DrawNumericChart(stats)
            Case SensorVisualizationType.BinaryState
                DrawBinaryStateChart(stats)
            Case SensorVisualizationType.DiscreteEvents
                DrawDiscreteEventsChart(stats)
            Case Else
                ' Fallback: afficher comme numérique
                DrawNumericChart(stats)
        End Select
    End Sub

    ''' <summary>
    ''' Affiche un graphique en courbe pour les valeurs numériques continues
    ''' (température, humidité, puissance, tension, etc.)
    ''' </summary>
    Private Sub DrawNumericChart(stats As DeviceStatistics)
        _statsChart.Plot.Clear()

        ' Vérification de sécurité : s'assurer qu'il y a des données
        If stats Is Nothing OrElse stats.DataPoints Is Nothing OrElse stats.DataPoints.Count = 0 Then
            DrawNoDataMessage("Aucune donnée disponible" & vbCrLf & "pour cette période")
            Return
        End If

        Try
            ' Extraire données
            Dim timestamps = stats.DataPoints.Select(Function(p) p.Timestamp.ToOADate()).ToArray()
            Dim values = stats.DataPoints.Select(Function(p) p.Value).ToArray()

            ' Vérification supplémentaire
            If timestamps.Length = 0 OrElse values.Length = 0 Then
                DrawNoDataMessage("Aucune donnée disponible" & vbCrLf & "pour cette période")
                Return
            End If

            ' Créer graphique en courbe lisse
            Dim scatter = _statsChart.Plot.Add.Scatter(timestamps, values)
            scatter.Color = ScottPlot.Color.FromHex("#2E5BFF") ' Bleu Tuya
            scatter.LineWidth = 2.5
            scatter.MarkerSize = 6
            scatter.Smooth = True ' Courbe lissée pour meilleure lisibilité

            ' Configuration de l'axe X (temps)
            _statsChart.Plot.Axes.DateTimeTicksBottom()

            ' Label de l'axe Y avec unité
            Dim yAxisLabel = GetYAxisLabel(stats.Code, stats.Unit)
            _statsChart.Plot.Axes.Left.Label.Text = yAxisLabel
            _statsChart.Plot.Axes.Left.Label.ForeColor = ScottPlot.Color.FromHex("#1C1C1E")
            _statsChart.Plot.Axes.Left.Label.FontSize = 12
            _statsChart.Plot.Axes.Left.Label.Bold = True

            ' Titre adapté au type de donnée
            Dim title = GetChartTitle(stats.Code)
            _statsChart.Plot.Title(title)

            ' Style
            _statsChart.Plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#E5E5EA")
            _statsChart.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#FFFFFF")
            _statsChart.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#FFFFFF")

            _statsChart.Refresh()

        Catch ex As Exception
            ' En cas d'erreur lors de la création du graphique, afficher un message
            DrawNoDataMessage("Erreur lors de l'affichage" & vbCrLf &
                            "du graphique:" & vbCrLf &
                            ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' Affiche un graphique en escalier pour les états binaires
    ''' (switch on/off, porte ouverte/fermée, contact, etc.)
    ''' </summary>
    Private Sub DrawBinaryStateChart(stats As DeviceStatistics)
        _statsChart.Plot.Clear()

        ' Vérification de sécurité : s'assurer qu'il y a des données
        If stats Is Nothing OrElse stats.DataPoints Is Nothing OrElse stats.DataPoints.Count = 0 Then
            DrawNoDataMessage("Aucune donnée d'état" & vbCrLf & "pour cette période")
            Return
        End If

        Try
            ' Extraire données
            Dim timestamps = stats.DataPoints.Select(Function(p) p.Timestamp.ToOADate()).ToArray()
            Dim values = stats.DataPoints.Select(Function(p) p.Value).ToArray()

            ' Vérification supplémentaire
            If timestamps.Length = 0 OrElse values.Length = 0 Then
                DrawNoDataMessage("Aucune donnée d'état" & vbCrLf & "pour cette période")
                Return
            End If

            ' Créer graphique en escalier (step plot)
            Dim scatter = _statsChart.Plot.Add.Scatter(timestamps, values)
            scatter.Color = ScottPlot.Color.FromHex("#34C759") ' Vert iOS pour état actif
            scatter.LineWidth = 3
            scatter.MarkerSize = 0
            scatter.LinePattern = ScottPlot.LinePattern.Solid

            ' Note: FillY peut ne pas être disponible dans toutes les versions de ScottPlot
            ' Laissons juste la ligne pour la visualisation

            ' Configuration de l'axe X (temps)
            _statsChart.Plot.Axes.DateTimeTicksBottom()

            ' Configuration de l'axe Y (0 = inactif, 1 = actif)
            _statsChart.Plot.Axes.Left.Min = -0.1
            _statsChart.Plot.Axes.Left.Max = 1.1
            _statsChart.Plot.Axes.Left.Label.Text = "État"
            _statsChart.Plot.Axes.Left.Label.ForeColor = ScottPlot.Color.FromHex("#1C1C1E")
            _statsChart.Plot.Axes.Left.Label.FontSize = 12
            _statsChart.Plot.Axes.Left.Label.Bold = True

            ' Ticks personnalisés pour l'axe Y
            Dim tickPositions As Double() = {0.0, 1.0}
            Dim tickLabels As String() = {"Inactif", "Actif"}
            _statsChart.Plot.Axes.Left.TickGenerator = New ScottPlot.TickGenerators.NumericManual(tickPositions, tickLabels)

            ' Titre adapté
            Dim title = GetChartTitle(stats.Code)
            _statsChart.Plot.Title(title)

            ' Style
            _statsChart.Plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#E5E5EA")
            _statsChart.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#FFFFFF")
            _statsChart.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#FFFFFF")

            _statsChart.Refresh()

        Catch ex As Exception
            ' En cas d'erreur lors de la création du graphique, afficher un message
            DrawNoDataMessage("Erreur lors de l'affichage" & vbCrLf &
                            "du graphique:" & vbCrLf &
                            ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' Affiche un graphique temporel pour les événements discrets
    ''' (PIR, détection de fumée, alarme, tamper, etc.)
    ''' Chaque événement est représenté par un marqueur vertical sur la timeline
    ''' </summary>
    Private Sub DrawDiscreteEventsChart(stats As DeviceStatistics)
        _statsChart.Plot.Clear()

        ' Vérification de sécurité : s'assurer qu'il y a des données
        If stats Is Nothing OrElse stats.DataPoints Is Nothing OrElse stats.DataPoints.Count = 0 Then
            DrawNoDataMessage("Aucun événement détecté" & vbCrLf & "pour cette période")
            Return
        End If

        Try
            ' Extraire les timestamps des événements
            Dim timestamps = stats.DataPoints.Select(Function(p) p.Timestamp.ToOADate()).ToArray()
            Dim eventCount = timestamps.Length

            ' Vérification supplémentaire
            If timestamps.Length = 0 Then
                DrawNoDataMessage("Aucun événement détecté" & vbCrLf & "pour cette période")
                Return
            End If

            ' Créer un tableau de valeurs à 1 pour chaque événement (pour visualisation)
            Dim yValues = Enumerable.Repeat(1.0, eventCount).ToArray()

            ' Option 1: Afficher les événements comme des marqueurs verticaux (lollipop/spike)
            For i As Integer = 0 To timestamps.Length - 1
                ' Créer une ligne verticale du bas (0) vers le haut (1) pour chaque événement
                Dim xCoords As Double() = {timestamps(i), timestamps(i)}
                Dim yCoords As Double() = {0.0, 1.0}

                Dim line = _statsChart.Plot.Add.Scatter(xCoords, yCoords)
                line.Color = ScottPlot.Color.FromHex("#FF9500") ' Orange pour attirer l'attention
                line.LineWidth = 3
                line.MarkerSize = 0
            Next

            ' Ajouter des marqueurs en haut de chaque ligne pour mieux les voir
            Dim markers = _statsChart.Plot.Add.Scatter(timestamps, yValues)
            markers.Color = ScottPlot.Color.FromHex("#FF3B30") ' Rouge vif
            markers.MarkerSize = 10
            markers.MarkerShape = ScottPlot.MarkerShape.FilledCircle
            markers.LineWidth = 0 ' Pas de ligne entre les marqueurs

            ' Configuration de l'axe X (temps)
            _statsChart.Plot.Axes.DateTimeTicksBottom()

            ' Configuration de l'axe Y (0 à 1.2 pour laisser de l'espace)
            _statsChart.Plot.Axes.Left.Min = 0
            _statsChart.Plot.Axes.Left.Max = 1.2
            _statsChart.Plot.Axes.Left.Label.Text = "Événements détectés"
            _statsChart.Plot.Axes.Left.Label.ForeColor = ScottPlot.Color.FromHex("#1C1C1E")
            _statsChart.Plot.Axes.Left.Label.FontSize = 12
            _statsChart.Plot.Axes.Left.Label.Bold = True

            ' Masquer les ticks de l'axe Y (pas pertinents pour ce type de visualisation)
            Dim tickPositions As Double() = {}
            Dim tickLabels As String() = {}
            _statsChart.Plot.Axes.Left.TickGenerator = New ScottPlot.TickGenerators.NumericManual(tickPositions, tickLabels)

            ' Titre avec informations supplémentaires
            Dim title = GetChartTitle(stats.Code)
            If stats.TotalEvents > 0 Then
                title &= $" ({stats.TotalEvents} événement(s))"
                If Not String.IsNullOrEmpty(stats.PeakActivityHour) Then
                    title &= $" - Pic: {stats.PeakActivityHour}"
                End If
            End If
            _statsChart.Plot.Title(title)

            ' Style
            _statsChart.Plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#E5E5EA")
            _statsChart.Plot.Grid.XAxisStyle.IsVisible = True
            _statsChart.Plot.Grid.YAxisStyle.IsVisible = False ' Pas de grille Y pour ce type de graphique
            _statsChart.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#FFFFFF")
            _statsChart.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#FFFFFF")

            _statsChart.Refresh()

        Catch ex As Exception
            ' En cas d'erreur lors de la création du graphique, afficher un message
            DrawNoDataMessage("Erreur lors de l'affichage" & vbCrLf &
                            "du graphique:" & vbCrLf &
                            ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' Détermine le label de l'axe Y selon le code DP
    ''' </summary>
    Private Function GetYAxisLabel(code As String, unit As String) As String
        Dim codeLower = code.ToLower()

        ' Labels spécifiques selon le type de capteur
        If codeLower.Contains("temp") Then
            Return $"Température ({unit})"
        ElseIf codeLower.Contains("humid") Then
            Return $"Humidité ({unit})"
        ElseIf codeLower.Contains("power") OrElse codeLower.Contains("phase") Then
            Return $"Puissance ({unit})"
        ElseIf codeLower.Contains("voltage") Then
            Return $"Tension ({unit})"
        ElseIf codeLower.Contains("current") Then
            Return $"Courant ({unit})"
        ElseIf codeLower.Contains("energy") OrElse codeLower.Contains("ele") Then
            Return $"Énergie ({unit})"
        ElseIf codeLower.Contains("battery") Then
            Return $"Batterie ({unit})"
        ElseIf codeLower.Contains("bright") OrElse codeLower.Contains("lux") Then
            Return $"Luminosité ({unit})"
        ElseIf Not String.IsNullOrEmpty(unit) Then
            Return $"{code} ({unit})"
        Else
            Return code
        End If
    End Function

    ''' <summary>
    ''' Détermine le titre du graphique selon le code DP
    ''' </summary>
    Private Function GetChartTitle(code As String) As String
        Dim codeLower = code.ToLower()
        Dim periodText = "Dernières 24 heures"

        ' Titres spécifiques selon le type de capteur
        If codeLower.Contains("temp") Then
            Return $"Température - {periodText}"
        ElseIf codeLower.Contains("humid") Then
            Return $"Humidité - {periodText}"
        ElseIf codeLower.Contains("power") OrElse codeLower.Contains("phase") Then
            Return $"Consommation - {periodText}"
        ElseIf codeLower.Contains("voltage") Then
            Return $"Tension - {periodText}"
        ElseIf codeLower.Contains("current") Then
            Return $"Courant - {periodText}"
        ElseIf codeLower.Contains("energy") OrElse codeLower.Contains("ele") Then
            Return $"Énergie cumulée - {periodText}"
        ElseIf codeLower.Contains("battery") Then
            Return $"Niveau de batterie - {periodText}"
        ElseIf codeLower.Contains("bright") OrElse codeLower.Contains("lux") Then
            Return $"Luminosité - {periodText}"
        ElseIf codeLower.Contains("switch") Then
            Return $"État du switch - {periodText}"
        ElseIf codeLower.Contains("door") OrElse codeLower.Contains("contact") Then
            Return $"État du capteur - {periodText}"
        ElseIf codeLower.Contains("pir") OrElse codeLower.Contains("motion") Then
            Return $"Détections de mouvement - {periodText}"
        ElseIf codeLower.Contains("smoke") Then
            Return $"Détections de fumée - {periodText}"
        ElseIf codeLower.Contains("tamper") Then
            Return $"Alertes tamper - {periodText}"
        Else
            Return $"{code} - {periodText}"
        End If
    End Function

    Private Sub DrawNoDataMessage(message As String)
        _statsChart.Plot.Clear()

        Dim text = _statsChart.Plot.Add.Text(message, 0.5, 0.5)
        text.LabelFontSize = 16
        text.LabelFontColor = ScottPlot.Color.FromHex("#8E8E93")
        text.LabelAlignment = ScottPlot.Alignment.MiddleCenter

        _statsChart.Plot.Axes.AutoScale()
        _statsChart.Refresh()
    End Sub

    Private Sub DisplayLogs(logs As List(Of DeviceLog))
        _logsListView.Items.Clear()

        If logs Is Nothing OrElse logs.Count = 0 Then
            Dim item As New ListViewItem("Aucun événement")
            item.SubItems.Add("")
            item.ForeColor = WinColor.Gray
            _logsListView.Items.Add(item)
            Return
        End If

        For Each log In logs
            Dim item As New ListViewItem(log.EventTime.ToString("dd/MM/yyyy HH:mm:ss"))
            item.SubItems.Add(log.Description)

            ' Couleur selon type
            Select Case log.EventType
                Case "switch_on"
                    item.ForeColor = Color.Green
                Case "switch_off"
                    item.ForeColor = Color.Red
                Case "online"
                    item.ForeColor = Color.Blue
                Case "offline"
                    item.ForeColor = Color.DarkRed
            End Select

            _logsListView.Items.Add(item)
        Next
    End Sub

    Private Sub PeriodComboBox_SelectedIndexChanged(sender As Object, e As EventArgs)
        _currentPeriod = CType(_periodComboBox.SelectedIndex, HistoryPeriod)
        ' Réinitialiser la liste des codes quand on change de période
        _availableCodes = Nothing
        _selectedCode = Nothing
        LoadHistoryAsync()
    End Sub

    Private Sub PropertyComboBox_SelectedIndexChanged(sender As Object, e As EventArgs)
        If _propertyComboBox.SelectedIndex <= 0 Then
            ' Auto-détection
            _selectedCode = Nothing
        Else
            ' Code spécifique sélectionné
            _selectedCode = _propertyComboBox.SelectedItem.ToString()
        End If

        ' Recharger uniquement le graphique (pas besoin de recharger la liste des codes)
        LoadChartOnlyAsync()
    End Sub

    Private Async Sub LoadChartOnlyAsync()
        Try
            _loadingLabel.Visible = True
            _propertyComboBox.Enabled = False
            _periodComboBox.Enabled = False
            _refreshButton.Enabled = False

            ' Charger les statistiques pour le code sélectionné
            Dim stats = Await _historyService.GetDeviceStatisticsAsync(_deviceId, _currentPeriod, _selectedCode)

            ' Afficher le graphique
            If stats IsNot Nothing AndAlso stats.DataPoints.Count > 0 Then
                DrawStatisticsChart(stats)
            Else
                Dim codeInfo = If(_selectedCode, "auto-détection")
                DrawNoDataMessage("Aucune donnée disponible" & vbCrLf &
                                 "Code: " & codeInfo & vbCrLf &
                                 "Essayez de sélectionner une autre propriété")
            End If

        Catch ex As Exception
            MessageBox.Show($"Erreur lors du chargement du graphique:{vbCrLf}{ex.Message}",
                          "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            _loadingLabel.Visible = False
            _propertyComboBox.Enabled = True
            _periodComboBox.Enabled = True
            _refreshButton.Enabled = True
        End Try
    End Sub

    Private Sub RefreshButton_Click(sender As Object, e As EventArgs)
        ' Réinitialiser la liste des codes pour forcer une nouvelle détection
        _availableCodes = Nothing
        _selectedCode = Nothing
        LoadHistoryAsync()
    End Sub
End Class
