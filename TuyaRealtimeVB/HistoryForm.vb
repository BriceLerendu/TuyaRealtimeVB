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
    ''' Affiche une timeline visuelle pour les états binaires
    ''' (switch on/off, porte ouverte/fermée, contact, etc.)
    ''' Affiche des blocs de couleur représentant les périodes actives/inactives
    ''' </summary>
    Private Sub DrawBinaryStateChart(stats As DeviceStatistics)
        _statsChart.Plot.Clear()

        ' Vérification de sécurité : s'assurer qu'il y a des données
        If stats Is Nothing OrElse stats.DataPoints Is Nothing OrElse stats.DataPoints.Count = 0 Then
            DrawNoDataMessage("Aucune donnée d'état" & vbCrLf & "pour cette période")
            Return
        End If

        Try
            ' Trier les points par timestamp
            Dim sortedPoints = stats.DataPoints.OrderBy(Function(p) p.Timestamp).ToList()

            ' Vérification supplémentaire
            If sortedPoints.Count = 0 Then
                DrawNoDataMessage("Aucune donnée d'état" & vbCrLf & "pour cette période")
                Return
            End If

            ' Déterminer la plage temporelle
            Dim startTime = sortedPoints.First().Timestamp
            Dim endTime = sortedPoints.Last().Timestamp

            ' Si un seul point, étendre la période pour la visualisation
            If sortedPoints.Count = 1 Then
                endTime = startTime.AddHours(1)
            End If

            ' Créer des rectangles pour chaque période d'état
            For i As Integer = 0 To sortedPoints.Count - 1
                Dim currentPoint = sortedPoints(i)
                Dim nextPoint As DateTime

                ' Déterminer la fin de cette période
                If i < sortedPoints.Count - 1 Then
                    nextPoint = sortedPoints(i + 1).Timestamp
                Else
                    ' Dernier point : étendre jusqu'à maintenant ou +1h
                    nextPoint = If(endTime > DateTime.Now, endTime, DateTime.Now)
                End If

                ' Créer un rectangle pour cette période
                Dim x1 = currentPoint.Timestamp.ToOADate()
                Dim x2 = nextPoint.ToOADate()
                Dim y1 = 0.0
                Dim y2 = 1.0

                ' ✅ CHANGEMENT: Couleur selon la VALEUR RÉELLE de l'événement
                ' Rouge pour alertes/détections (pir, open, true, 1, on)
                ' Vert pour états normaux (none, close, false, 0, off)
                ' Orange pour mode chaud (hot)
                ' Bleu pour mode froid (cool)
                Dim originalValue = currentPoint.OriginalValue?.ToLower()
                Dim stateColor As ScottPlot.Color

                ' Déterminer la couleur selon le type d'événement
                If originalValue = "hot" Then
                    ' Mode chauffage : ORANGE
                    stateColor = ScottPlot.Color.FromHex("#FF9500")
                ElseIf originalValue = "cool" OrElse originalValue = "cold" Then
                    ' Mode refroidissement : BLEU
                    stateColor = ScottPlot.Color.FromHex("#007AFF")
                ElseIf originalValue = "pir" OrElse originalValue = "motion" OrElse
                   originalValue = "open" OrElse originalValue = "true" OrElse
                   originalValue = "1" OrElse originalValue = "on" OrElse
                   originalValue = "detected" Then
                    ' État d'alerte/détection : ROUGE
                    stateColor = ScottPlot.Color.FromHex("#FF3B30")
                Else
                    ' État normal : VERT
                    stateColor = ScottPlot.Color.FromHex("#34C759")
                End If

                ' Ajouter le rectangle
                Dim rect = _statsChart.Plot.Add.Rectangle(x1, x2, y1, y2)
                rect.FillColor = stateColor.WithAlpha(0.6)
                rect.LineColor = stateColor
                rect.LineWidth = 1
            Next

            ' Ajouter une ligne de séparation au milieu pour faciliter la lecture
            Dim midLine = _statsChart.Plot.Add.HorizontalLine(0.5)
            midLine.Color = ScottPlot.Color.FromHex("#E5E5EA")
            midLine.LineWidth = 1
            midLine.LinePattern = ScottPlot.LinePattern.Dotted

            ' Configuration de l'axe X (temps)
            _statsChart.Plot.Axes.DateTimeTicksBottom()

            ' Configuration de l'axe Y (timeline)
            _statsChart.Plot.Axes.Left.Min = 0
            _statsChart.Plot.Axes.Left.Max = 1
            _statsChart.Plot.Axes.Left.Label.Text = "Timeline"
            _statsChart.Plot.Axes.Left.Label.ForeColor = ScottPlot.Color.FromHex("#1C1C1E")
            _statsChart.Plot.Axes.Left.Label.FontSize = 12
            _statsChart.Plot.Axes.Left.Label.Bold = True

            ' Ticks personnalisés pour l'axe Y (adapter selon le type d'événements)
            Dim hasHotOrCool = sortedPoints.Any(Function(p) p.OriginalValue?.ToLower() = "hot" OrElse
                                                              p.OriginalValue?.ToLower() = "cool" OrElse
                                                              p.OriginalValue?.ToLower() = "cold")
            Dim tickPositions As Double() = {0.25, 0.75}
            Dim tickLabels As String()
            If hasHotOrCool Then
                tickLabels = New String() {"COOL/NORMAL", "HOT/ALERTE"}
            Else
                tickLabels = New String() {"NORMAL", "ALERTE"}
            End If
            _statsChart.Plot.Axes.Left.TickGenerator = New ScottPlot.TickGenerators.NumericManual(tickPositions, tickLabels)

            ' Calculer les statistiques d'état (temps en alerte/hot/cool)
            Dim totalDuration = endTime - startTime
            Dim alertDuration As TimeSpan = TimeSpan.Zero
            Dim hotDuration As TimeSpan = TimeSpan.Zero
            Dim coolDuration As TimeSpan = TimeSpan.Zero

            For i As Integer = 0 To sortedPoints.Count - 1
                Dim currentPoint = sortedPoints(i)
                ' Vérifier le type d'état en utilisant la valeur originale
                Dim originalValue = currentPoint.OriginalValue?.ToLower()
                Dim isAlert = originalValue = "pir" OrElse originalValue = "motion" OrElse
                             originalValue = "open" OrElse originalValue = "true" OrElse
                             originalValue = "1" OrElse originalValue = "on" OrElse
                             originalValue = "detected"
                Dim isHot = originalValue = "hot"
                Dim isCool = originalValue = "cool" OrElse originalValue = "cold"

                Dim nextTime As DateTime
                If i < sortedPoints.Count - 1 Then
                    nextTime = sortedPoints(i + 1).Timestamp
                Else
                    nextTime = If(endTime > DateTime.Now, endTime, DateTime.Now)
                End If

                If isAlert Then
                    alertDuration += nextTime - currentPoint.Timestamp
                ElseIf isHot Then
                    hotDuration += nextTime - currentPoint.Timestamp
                ElseIf isCool Then
                    coolDuration += nextTime - currentPoint.Timestamp
                End If
            Next

            Dim alertPercent = If(totalDuration.TotalSeconds > 0,
                (alertDuration.TotalSeconds / totalDuration.TotalSeconds) * 100, 0)
            Dim hotPercent = If(totalDuration.TotalSeconds > 0,
                (hotDuration.TotalSeconds / totalDuration.TotalSeconds) * 100, 0)
            Dim coolPercent = If(totalDuration.TotalSeconds > 0,
                (coolDuration.TotalSeconds / totalDuration.TotalSeconds) * 100, 0)

            ' Titre adapté avec statistiques
            Dim title = GetChartTitle(stats.Code)
            If hotPercent > 0 OrElse coolPercent > 0 Then
                ' Pour les modes de chauffage
                title &= $" - Hot: {hotPercent:F1}% | Cool: {coolPercent:F1}% ({sortedPoints.Count} changements)"
            Else
                ' Pour les alertes classiques
                title &= $" - Alerte: {alertPercent:F1}% ({sortedPoints.Count} changements d'état)"
            End If
            _statsChart.Plot.Title(title)

            ' Style
            _statsChart.Plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#E5E5EA")
            _statsChart.Plot.Grid.XAxisStyle.IsVisible = True
            _statsChart.Plot.Grid.YAxisStyle.IsVisible = False ' Masquer la grille Y pour timeline
            _statsChart.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#FFFFFF")
            _statsChart.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#F8F8F8")

            _statsChart.Refresh()

        Catch ex As Exception
            ' En cas d'erreur lors de la création du graphique, afficher un message
            DrawNoDataMessage("Erreur lors de l'affichage" & vbCrLf &
                            "du graphique:" & vbCrLf &
                            ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' Affiche une timeline visuelle pour les événements discrets
    ''' (PIR, détection de fumée, alarme, tamper, etc.)
    ''' Affiche des marqueurs temporels pour chaque événement
    ''' ✅ CHANGEMENT: Affiche TOUJOURS une timeline (jamais d'histogramme)
    ''' </summary>
    Private Sub DrawDiscreteEventsChart(stats As DeviceStatistics)
        _statsChart.Plot.Clear()

        ' Vérification de sécurité : s'assurer qu'il y a des données
        If stats Is Nothing OrElse stats.DataPoints Is Nothing OrElse stats.DataPoints.Count = 0 Then
            DrawNoDataMessage("Aucun événement détecté" & vbCrLf & "pour cette période")
            Return
        End If

        Try
            ' Extraire les événements et TRIER PAR ORDRE CHRONOLOGIQUE
            Dim events = stats.DataPoints.OrderBy(Function(p) p.Timestamp).ToList()
            Dim eventCount = events.Count

            ' Vérification supplémentaire
            If eventCount = 0 Then
                DrawNoDataMessage("Aucun événement détecté" & vbCrLf & "pour cette période")
                Return
            End If

            ' ✅ MODE TIMELINE UNIQUEMENT : Afficher chaque événement sur une timeline
            DrawTimelineWithMarkers(events, stats.Code)

        Catch ex As Exception
            ' En cas d'erreur lors de la création du graphique, afficher un message
            DrawNoDataMessage("Erreur lors de l'affichage" & vbCrLf &
                            "du graphique:" & vbCrLf &
                            ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' Affiche une timeline avec des marqueurs pour chaque événement individuel
    ''' ✅ CHANGEMENT: Affiche des couleurs différentes selon le type d'événement
    ''' ✅ CORRECTION: Les événements sont déjà triés chronologiquement en entrée
    ''' </summary>
    Private Sub DrawTimelineWithMarkers(events As List(Of StatisticPoint), code As String)
        ' Vérifier que les événements sont bien triés chronologiquement (ordre croissant)
        ' Note: La liste est déjà triée par DrawDiscreteEventsChart, mais on re-vérifie
        Dim sortedEvents = events.OrderBy(Function(e) e.Timestamp).ToList()

        ' Séparer les événements en groupes selon leur type
        Dim alertEvents As New List(Of StatisticPoint)
        Dim normalEvents As New List(Of StatisticPoint)
        Dim hotEvents As New List(Of StatisticPoint)
        Dim coolEvents As New List(Of StatisticPoint)

        For Each evt In sortedEvents
            Dim originalValue = evt.OriginalValue?.ToLower()
            Dim isAlert = originalValue = "pir" OrElse originalValue = "motion" OrElse
                         originalValue = "open" OrElse originalValue = "true" OrElse
                         originalValue = "1" OrElse originalValue = "on" OrElse
                         originalValue = "detected"
            Dim isHot = originalValue = "hot"
            Dim isCool = originalValue = "cool" OrElse originalValue = "cold"

            If isHot Then
                hotEvents.Add(evt)
            ElseIf isCool Then
                coolEvents.Add(evt)
            ElseIf isAlert Then
                alertEvents.Add(evt)
            Else
                normalEvents.Add(evt)
            End If
        Next

        ' Ajouter les marqueurs pour les événements d'alerte (ROUGE)
        If alertEvents.Count > 0 Then
            Dim alertTimestamps = alertEvents.Select(Function(e) e.Timestamp.ToOADate()).ToArray()
            Dim alertYValues = alertEvents.Select(Function(e) 0.75).ToArray() ' Position haute

            Dim alertScatter = _statsChart.Plot.Add.Scatter(alertTimestamps, alertYValues)
            alertScatter.Color = ScottPlot.Color.FromHex("#FF3B30") ' Rouge pour alertes
            alertScatter.MarkerSize = 14
            alertScatter.MarkerShape = ScottPlot.MarkerShape.FilledDiamond
            alertScatter.LineWidth = 0
            alertScatter.Label = "Alertes"

            ' Lignes verticales pour alertes
            For Each evt In alertEvents
                Dim vLine = _statsChart.Plot.Add.VerticalLine(evt.Timestamp.ToOADate())
                vLine.Color = ScottPlot.Color.FromHex("#FF3B30").WithAlpha(0.3)
                vLine.LineWidth = 2
                vLine.LinePattern = ScottPlot.LinePattern.Solid
            Next
        End If

        ' Ajouter les marqueurs pour les événements normaux (VERT)
        If normalEvents.Count > 0 Then
            Dim normalTimestamps = normalEvents.Select(Function(e) e.Timestamp.ToOADate()).ToArray()
            Dim normalYValues = normalEvents.Select(Function(e) 0.25).ToArray() ' Position basse

            Dim normalScatter = _statsChart.Plot.Add.Scatter(normalTimestamps, normalYValues)
            normalScatter.Color = ScottPlot.Color.FromHex("#34C759") ' Vert pour normal
            normalScatter.MarkerSize = 12
            normalScatter.MarkerShape = ScottPlot.MarkerShape.FilledCircle
            normalScatter.LineWidth = 0
            normalScatter.Label = "Normal"

            ' Lignes verticales pour événements normaux
            For Each evt In normalEvents
                Dim vLine = _statsChart.Plot.Add.VerticalLine(evt.Timestamp.ToOADate())
                vLine.Color = ScottPlot.Color.FromHex("#34C759").WithAlpha(0.2)
                vLine.LineWidth = 1
                vLine.LinePattern = ScottPlot.LinePattern.Dotted
            Next
        End If

        ' Ajouter les marqueurs pour les événements HOT (ORANGE)
        If hotEvents.Count > 0 Then
            Dim hotTimestamps = hotEvents.Select(Function(e) e.Timestamp.ToOADate()).ToArray()
            Dim hotYValues = hotEvents.Select(Function(e) 0.75).ToArray() ' Position haute

            Dim hotScatter = _statsChart.Plot.Add.Scatter(hotTimestamps, hotYValues)
            hotScatter.Color = ScottPlot.Color.FromHex("#FF9500") ' Orange pour hot
            hotScatter.MarkerSize = 14
            hotScatter.MarkerShape = ScottPlot.MarkerShape.FilledSquare
            hotScatter.LineWidth = 0
            hotScatter.Label = "Mode Hot"

            ' Lignes verticales pour hot
            For Each evt In hotEvents
                Dim vLine = _statsChart.Plot.Add.VerticalLine(evt.Timestamp.ToOADate())
                vLine.Color = ScottPlot.Color.FromHex("#FF9500").WithAlpha(0.3)
                vLine.LineWidth = 2
                vLine.LinePattern = ScottPlot.LinePattern.Solid
            Next
        End If

        ' Ajouter les marqueurs pour les événements COOL (BLEU)
        If coolEvents.Count > 0 Then
            Dim coolTimestamps = coolEvents.Select(Function(e) e.Timestamp.ToOADate()).ToArray()
            Dim coolYValues = coolEvents.Select(Function(e) 0.25).ToArray() ' Position basse

            Dim coolScatter = _statsChart.Plot.Add.Scatter(coolTimestamps, coolYValues)
            coolScatter.Color = ScottPlot.Color.FromHex("#007AFF") ' Bleu pour cool
            coolScatter.MarkerSize = 14
            coolScatter.MarkerShape = ScottPlot.MarkerShape.FilledSquare
            coolScatter.LineWidth = 0
            coolScatter.Label = "Mode Cool"

            ' Lignes verticales pour cool
            For Each evt In coolEvents
                Dim vLine = _statsChart.Plot.Add.VerticalLine(evt.Timestamp.ToOADate())
                vLine.Color = ScottPlot.Color.FromHex("#007AFF").WithAlpha(0.3)
                vLine.LineWidth = 2
                vLine.LinePattern = ScottPlot.LinePattern.Solid
            Next
        End If

        ' Ajouter une ligne horizontale de base
        Dim baseLine = _statsChart.Plot.Add.HorizontalLine(0.5)
        baseLine.Color = ScottPlot.Color.FromHex("#8E8E93")
        baseLine.LineWidth = 2

        ' Configuration de l'axe X (temps)
        _statsChart.Plot.Axes.DateTimeTicksBottom()

        ' Configuration de l'axe Y (masqué car juste une timeline)
        _statsChart.Plot.Axes.Left.Min = 0
        _statsChart.Plot.Axes.Left.Max = 1
        _statsChart.Plot.Axes.Left.Label.Text = "Événements"
        _statsChart.Plot.Axes.Left.Label.ForeColor = ScottPlot.Color.FromHex("#1C1C1E")
        _statsChart.Plot.Axes.Left.Label.FontSize = 12
        _statsChart.Plot.Axes.Left.Label.Bold = True

        ' Pas de ticks sur l'axe Y
        _statsChart.Plot.Axes.Left.TickGenerator = New ScottPlot.TickGenerators.NumericManual(New Double() {}, New String() {})

        ' Calculer l'intervalle moyen entre événements (utiliser les événements triés)
        Dim intervals As New List(Of TimeSpan)
        For i As Integer = 1 To sortedEvents.Count - 1
            intervals.Add(sortedEvents(i).Timestamp - sortedEvents(i - 1).Timestamp)
        Next

        Dim avgInterval = If(intervals.Count > 0,
            TimeSpan.FromSeconds(intervals.Average(Function(t) t.TotalSeconds)),
            TimeSpan.Zero)

        ' Titre avec statistiques incluant le nombre de chaque type d'événement
        Dim title = GetChartTitle(code)
        If hotEvents.Count > 0 OrElse coolEvents.Count > 0 Then
            ' Afficher les stats des modes de chauffage
            title &= $" - Hot: {hotEvents.Count}, Cool: {coolEvents.Count}"
            If normalEvents.Count > 0 Then
                title &= $", Autre: {normalEvents.Count}"
            End If
        Else
            ' Afficher les stats d'alertes classiques
            title &= $" - {alertEvents.Count} alerte(s), {normalEvents.Count} normal"
        End If

        If avgInterval.TotalSeconds > 0 Then
            If avgInterval.TotalMinutes < 1 Then
                title &= $" (intervalle moyen: {avgInterval.TotalSeconds:F0}s)"
            ElseIf avgInterval.TotalHours < 1 Then
                title &= $" (intervalle moyen: {avgInterval.TotalMinutes:F0}min)"
            Else
                title &= $" (intervalle moyen: {avgInterval.TotalHours:F1}h)"
            End If
        End If
        _statsChart.Plot.Title(title)

        ' Style
        _statsChart.Plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#E5E5EA")
        _statsChart.Plot.Grid.XAxisStyle.IsVisible = True
        _statsChart.Plot.Grid.YAxisStyle.IsVisible = False
        _statsChart.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#FFFFFF")
        _statsChart.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#FFF9F0")

        _statsChart.Refresh()
    End Sub

    ''' <summary>
    ''' Affiche un histogramme groupé pour les événements nombreux ou sur longue période
    ''' </summary>
    Private Sub DrawHistogramGrouped(events As List(Of StatisticPoint), duration As TimeSpan, stats As DeviceStatistics)
        Dim eventCount = events.Count

        ' Choisir la taille des bins selon la durée
        Dim binSizeMinutes As Integer
        Dim labelFormat As String

        If duration.TotalHours <= 6 Then
            binSizeMinutes = 15 ' 15 minutes pour moins de 6h
            labelFormat = "HH:mm"
        ElseIf duration.TotalHours <= 24 Then
            binSizeMinutes = 60 ' 1 heure pour 24h
            labelFormat = "HH:mm"
        Else
            binSizeMinutes = 120 ' 2 heures pour plus d'un jour
            labelFormat = "dd/MM HH:mm"
        End If

        ' Créer les bins (tranches de temps)
        Dim bins As New Dictionary(Of DateTime, Integer)
        For Each evt In events
            ' Arrondir au bin le plus proche
            Dim binTime = New DateTime(
                evt.Timestamp.Year,
                evt.Timestamp.Month,
                evt.Timestamp.Day,
                evt.Timestamp.Hour,
                (evt.Timestamp.Minute \ binSizeMinutes) * binSizeMinutes,
                0
            )

            If bins.ContainsKey(binTime) Then
                bins(binTime) += 1
            Else
                bins(binTime) = 1
            End If
        Next

        ' Convertir en tableaux pour ScottPlot
        Dim binTimes = bins.Keys.OrderBy(Function(t) t).Select(Function(t) t.ToOADate()).ToArray()
        Dim binCounts = bins.Keys.OrderBy(Function(t) t).Select(Function(t) CDbl(bins(t))).ToArray()

        ' Si un seul bin, ajouter des bins vides avant et après pour la visualisation
        If binTimes.Length = 1 Then
            Dim singleTime = DateTime.FromOADate(binTimes(0))
            Dim beforeTime = singleTime.AddMinutes(-binSizeMinutes).ToOADate()
            Dim afterTime = singleTime.AddMinutes(binSizeMinutes).ToOADate()

            binTimes = New Double() {beforeTime, binTimes(0), afterTime}
            binCounts = New Double() {0, binCounts(0), 0}
        End If

        ' Créer un bar chart
        Dim barWidth = binSizeMinutes / (24.0 * 60.0) ' Convertir en jours pour OADate
        Dim barPlot = _statsChart.Plot.Add.Bars(binTimes, binCounts)

        ' Styling des barres avec dégradé de couleur selon l'intensité
        Dim maxCount = binCounts.Max()
        For i As Integer = 0 To barPlot.Bars.Count - 1
            Dim bar = barPlot.Bars(i)
            Dim intensity = If(maxCount > 0, binCounts(i) / maxCount, 0)

            ' Couleur du rouge vif au rouge foncé selon l'intensité
            If intensity > 0.7 Then
                bar.FillColor = ScottPlot.Color.FromHex("#FF3B30") ' Rouge vif pour haute activité
                bar.LineColor = ScottPlot.Color.FromHex("#D32F2F")
            ElseIf intensity > 0.4 Then
                bar.FillColor = ScottPlot.Color.FromHex("#FF9500") ' Orange pour activité moyenne
                bar.LineColor = ScottPlot.Color.FromHex("#F57C00")
            Else
                bar.FillColor = ScottPlot.Color.FromHex("#FFCC00") ' Jaune pour faible activité
                bar.LineColor = ScottPlot.Color.FromHex("#FFA000")
            End If

            bar.LineWidth = 1
            bar.Size = barWidth * 0.8 ' 80% de la largeur du bin pour laisser un peu d'espace
        Next

        ' Configuration de l'axe X (temps)
        _statsChart.Plot.Axes.DateTimeTicksBottom()

        ' Configuration de l'axe Y (nombre d'événements)
        _statsChart.Plot.Axes.Left.Label.Text = "Nombre de détections"
        _statsChart.Plot.Axes.Left.Label.ForeColor = ScottPlot.Color.FromHex("#1C1C1E")
        _statsChart.Plot.Axes.Left.Label.FontSize = 12
        _statsChart.Plot.Axes.Left.Label.Bold = True

        ' Forcer l'axe Y à commencer à 0
        _statsChart.Plot.Axes.Left.Min = 0

        ' Si max < 5, arrondir à 5 pour une meilleure lisibilité
        If maxCount < 5 Then
            _statsChart.Plot.Axes.Left.Max = 5
        End If

        ' Titre avec informations supplémentaires
        Dim title = GetChartTitle(stats.Code)
        Dim binLabel = If(binSizeMinutes = 60, "heure", If(binSizeMinutes = 15, "15min", "2h"))
        title &= $" ({eventCount} événement(s), groupés par {binLabel})"
        If stats.TotalEvents > 0 AndAlso Not String.IsNullOrEmpty(stats.PeakActivityHour) Then
            title &= $" - Pic: {stats.PeakActivityHour}"
        End If
        _statsChart.Plot.Title(title)

        ' Style
        _statsChart.Plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#E5E5EA")
        _statsChart.Plot.Grid.XAxisStyle.IsVisible = True
        _statsChart.Plot.Grid.YAxisStyle.IsVisible = True
        _statsChart.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#FFFFFF")
        _statsChart.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#FFFFFF")

        _statsChart.Refresh()
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

        ' ✅ CORRECTION: Trier les logs par ordre chronologique (du plus récent au plus ancien pour la liste)
        Dim sortedLogs = logs.OrderByDescending(Function(l) l.EventTime).ToList()

        For Each log In sortedLogs
            Dim item As New ListViewItem(log.EventTime.ToString("dd/MM/yyyy HH:mm:ss"))
            item.SubItems.Add(log.Description)

            ' Couleur selon la valeur (concordance avec les couleurs du graphique)
            Dim valueLower = log.Value?.ToLower()

            ' Déterminer la couleur selon le type d'événement
            If valueLower = "hot" Then
                ' Mode chauffage : ORANGE (#FF9500)
                item.ForeColor = Color.FromArgb(255, 149, 0)
            ElseIf valueLower = "cool" OrElse valueLower = "cold" Then
                ' Mode refroidissement : BLEU (#007AFF)
                item.ForeColor = Color.FromArgb(0, 122, 255)
            ElseIf valueLower = "pir" OrElse valueLower = "motion" OrElse
               valueLower = "open" OrElse valueLower = "true" OrElse
               valueLower = "1" OrElse valueLower = "on" OrElse
               valueLower = "detected" Then
                ' État d'alerte/détection : ROUGE (#FF3B30)
                item.ForeColor = Color.FromArgb(255, 59, 48)
            ElseIf valueLower = "none" OrElse valueLower = "close" OrElse
                   valueLower = "false" OrElse valueLower = "0" OrElse
                   valueLower = "off" Then
                ' État normal : VERT (#34C759)
                item.ForeColor = Color.FromArgb(52, 199, 89)
            Else
                ' Autre : couleur par défaut (noir)
                item.ForeColor = Color.FromArgb(28, 28, 30)
            End If

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
