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
    Private _chartPanel As Panel
    Private _statsChart As FormsPlot
    Private _logsListView As ListView
    Private _loadingLabel As System.Windows.Forms.Label
    Private _closeButton As Button
    Private _refreshButton As Button

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

        _refreshButton = New Button With {
            .Text = "🔄 Actualiser",
            .Location = New Point(310, 14),
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
            .Location = New Point(450, 18),
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
            _refreshButton.Enabled = False

            ' Charger statistiques et logs en parallèle
            Dim statsTask = _historyService.GetDeviceStatisticsAsync(_deviceId, _currentPeriod)
            Dim logsTask = _historyService.GetDeviceLogsAsync(_deviceId, _currentPeriod)

            Await Task.WhenAll(statsTask, logsTask)

            Dim stats = Await statsTask
            Dim logs = Await logsTask

            ' Afficher graphique
            If stats IsNot Nothing AndAlso stats.DataPoints.Count > 0 Then
                DrawStatisticsChart(stats)
            Else
                Dim codeInfo = If(stats?.Code, "cur_power")
                DrawNoDataMessage("Aucune donnée disponible" & vbCrLf &
                                 "Code: " & codeInfo & vbCrLf &
                                 "Consultez les logs du dashboard")
            End If

            ' Afficher timeline
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
            _refreshButton.Enabled = True
        End Try
    End Sub

    Private Sub DrawStatisticsChart(stats As DeviceStatistics)
        ' Réinitialiser le graphique
        _statsChart.Plot.Clear()

        ' Extraire données
        Dim values = stats.DataPoints.Select(Function(p) p.Value).ToArray()
        Dim labels = stats.DataPoints.Select(Function(p) p.Label).ToArray()
        Dim positions = Enumerable.Range(0, values.Length).Select(Function(i) CDbl(i)).ToArray()

        ' Créer graphique en barres
        Dim bar = _statsChart.Plot.Add.Bars(positions, values)
        bar.Color = ScottPlot.Color.FromHex("#2E5BFF") ' Bleu Tuya

        ' Configuration des axes
        _statsChart.Plot.Axes.Bottom.TickGenerator = New ScottPlot.TickGenerators.NumericManual(
            positions, labels
        )

        _statsChart.Plot.Axes.Bottom.MajorTickStyle.Length = 0
        _statsChart.Plot.Axes.Left.Label.Text = $"Consommation ({stats.Unit})"
        _statsChart.Plot.Axes.Left.Label.ForeColor = ScottPlot.Color.FromHex("#1C1C1E")
        _statsChart.Plot.Axes.Left.Label.FontSize = 12
        _statsChart.Plot.Axes.Left.Label.Bold = True

        ' Titre (24h uniquement)
        _statsChart.Plot.Title("Consommation - Dernières 24 heures")

        ' Style
        _statsChart.Plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#E5E5EA")
        _statsChart.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#FFFFFF")
        _statsChart.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#FFFFFF")

        _statsChart.Refresh()
    End Sub

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
        LoadHistoryAsync()
    End Sub

    Private Sub RefreshButton_Click(sender As Object, e As EventArgs)
        LoadHistoryAsync()
    End Sub
End Class
