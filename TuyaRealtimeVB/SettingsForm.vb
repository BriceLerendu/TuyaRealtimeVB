Imports System.Drawing
Imports System.Windows.Forms
Imports System.IO

Public Class SettingsForm
    Inherits Form

    Private _config As TuyaConfig

    ' Contrôles Tuya
    Private _regionTextBox As TextBox
    Private _openApiBaseTextBox As TextBox
    Private _mqttHostTextBox As TextBox
    Private _mqttPortTextBox As NumericUpDown
    Private _accessIdTextBox As TextBox
    Private _accessSecretTextBox As TextBox
    Private _uidTextBox As TextBox

    ' Contrôles Python
    Private _pythonScriptPathTextBox As TextBox
    Private _pythonFallbackPathTextBox As TextBox
    Private _browsePythonButton As Button
    Private _browseFallbackButton As Button

    ' Contrôles Logging
    Private _showRawPayloadsCheckBox As CheckBox

    ' Boutons
    Private _saveButton As Button
    Private _cancelButton As Button
    Private _testConnectionButton As Button

    Public Sub New(config As TuyaConfig)
        _config = config
        InitializeComponent()
        LoadSettings()
    End Sub

    Private Sub InitializeComponent()
        Me.Text = "Paramètres de l'application"
        Me.Size = New Size(600, 700)
        Me.StartPosition = FormStartPosition.CenterParent
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.BackColor = Color.FromArgb(242, 242, 247)

        Dim scrollPanel As New Panel With {
            .Dock = DockStyle.Fill,
            .AutoScroll = True,
            .Padding = New Padding(20)
        }

        Dim yPos As Integer = 20

        ' ═══════════════════════════════════════════════════════
        ' SECTION TUYA API
        ' ═══════════════════════════════════════════════════════
        yPos = AddSectionHeader(scrollPanel, "Configuration Tuya API", yPos)

        yPos = AddTextBoxField(scrollPanel, "Région :", _regionTextBox, yPos, "eu, us, cn, in")
        yPos = AddTextBoxField(scrollPanel, "OpenAPI Base :", _openApiBaseTextBox, yPos, "https://openapi.tuyaeu.com")
        yPos = AddTextBoxField(scrollPanel, "MQTT Host :", _mqttHostTextBox, yPos, "mqe.tuyaeu.com")

        ' Port MQTT (NumericUpDown)
        Dim portLabel As New Label With {
            .Text = "MQTT Port :",
            .Location = New Point(20, yPos),
            .Size = New Size(150, 20),
            .Font = New Font("Segoe UI", 9, FontStyle.Bold),
            .ForeColor = Color.FromArgb(28, 28, 30),
            .BackColor = Color.Transparent
        }
        scrollPanel.Controls.Add(portLabel)

        _mqttPortTextBox = New NumericUpDown With {
            .Location = New Point(180, yPos),
            .Size = New Size(380, 25),
            .Font = New Font("Segoe UI", 10),
            .Minimum = 1,
            .Maximum = 65535,
            .Value = 8285
        }
        scrollPanel.Controls.Add(_mqttPortTextBox)
        yPos += 35

        yPos = AddTextBoxField(scrollPanel, "Access ID :", _accessIdTextBox, yPos)
        yPos = AddPasswordField(scrollPanel, "Access Secret :", _accessSecretTextBox, yPos)
        yPos = AddTextBoxField(scrollPanel, "UID :", _uidTextBox, yPos)

        yPos += 10

        ' ═══════════════════════════════════════════════════════
        ' SECTION PYTHON
        ' ═══════════════════════════════════════════════════════
        yPos = AddSectionHeader(scrollPanel, "Configuration Python", yPos)

        ' Chemin du script Python
        Dim pythonLabel As New Label With {
            .Text = "Script Python :",
            .Location = New Point(20, yPos),
            .Size = New Size(150, 20),
            .Font = New Font("Segoe UI", 9, FontStyle.Bold),
            .ForeColor = Color.FromArgb(28, 28, 30),
            .BackColor = Color.Transparent
        }
        scrollPanel.Controls.Add(pythonLabel)

        _pythonScriptPathTextBox = New TextBox With {
            .Location = New Point(180, yPos),
            .Size = New Size(300, 25),
            .Font = New Font("Segoe UI", 10)
        }
        scrollPanel.Controls.Add(_pythonScriptPathTextBox)

        _browsePythonButton = New Button With {
            .Text = "...",
            .Location = New Point(490, yPos),
            .Size = New Size(70, 25),
            .Font = New Font("Segoe UI", 9),
            .BackColor = Color.FromArgb(0, 122, 255),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand
        }
        _browsePythonButton.FlatAppearance.BorderSize = 0
        AddHandler _browsePythonButton.Click, AddressOf BrowsePythonButton_Click
        scrollPanel.Controls.Add(_browsePythonButton)
        yPos += 35

        ' Chemin de fallback
        Dim fallbackLabel As New Label With {
            .Text = "Chemin alternatif :",
            .Location = New Point(20, yPos),
            .Size = New Size(150, 20),
            .Font = New Font("Segoe UI", 9, FontStyle.Bold),
            .ForeColor = Color.FromArgb(28, 28, 30),
            .BackColor = Color.Transparent
        }
        scrollPanel.Controls.Add(fallbackLabel)

        _pythonFallbackPathTextBox = New TextBox With {
            .Location = New Point(180, yPos),
            .Size = New Size(300, 25),
            .Font = New Font("Segoe UI", 10)
        }
        scrollPanel.Controls.Add(_pythonFallbackPathTextBox)

        _browseFallbackButton = New Button With {
            .Text = "...",
            .Location = New Point(490, yPos),
            .Size = New Size(70, 25),
            .Font = New Font("Segoe UI", 9),
            .BackColor = Color.FromArgb(0, 122, 255),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand
        }
        _browseFallbackButton.FlatAppearance.BorderSize = 0
        AddHandler _browseFallbackButton.Click, AddressOf BrowseFallbackButton_Click
        scrollPanel.Controls.Add(_browseFallbackButton)
        yPos += 45

        ' ═══════════════════════════════════════════════════════
        ' SECTION LOGGING
        ' ═══════════════════════════════════════════════════════
        yPos = AddSectionHeader(scrollPanel, "Options de débogage", yPos)

        _showRawPayloadsCheckBox = New CheckBox With {
            .Text = "Afficher les trames JSON brutes dans la console",
            .Location = New Point(20, yPos),
            .Size = New Size(540, 25),
            .Font = New Font("Segoe UI", 10),
            .ForeColor = Color.FromArgb(28, 28, 30),
            .BackColor = Color.Transparent,
            .Checked = True
        }
        scrollPanel.Controls.Add(_showRawPayloadsCheckBox)
        yPos += 45

        Me.Controls.Add(scrollPanel)

        ' ═══════════════════════════════════════════════════════
        ' PANEL BOUTONS EN BAS
        ' ═══════════════════════════════════════════════════════
        Dim buttonPanel As New Panel With {
            .Dock = DockStyle.Bottom,
            .Height = 70,
            .BackColor = Color.White,
            .Padding = New Padding(20)
        }

        _testConnectionButton = New Button With {
            .Text = "Tester la connexion",
            .Location = New Point(20, 20),
            .Size = New Size(150, 35),
            .Font = New Font("Segoe UI", 10, FontStyle.Bold),
            .BackColor = Color.FromArgb(255, 149, 0),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand
        }
        _testConnectionButton.FlatAppearance.BorderSize = 0
        AddHandler _testConnectionButton.Click, AddressOf TestConnectionButton_Click
        buttonPanel.Controls.Add(_testConnectionButton)

        _cancelButton = New Button With {
            .Text = "Annuler",
            .Location = New Point(320, 20),
            .Size = New Size(120, 35),
            .Font = New Font("Segoe UI", 10),
            .BackColor = Color.FromArgb(142, 142, 147),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand,
            .DialogResult = DialogResult.Cancel
        }
        _cancelButton.FlatAppearance.BorderSize = 0
        buttonPanel.Controls.Add(_cancelButton)

        _saveButton = New Button With {
            .Text = "Enregistrer",
            .Location = New Point(450, 20),
            .Size = New Size(120, 35),
            .Font = New Font("Segoe UI", 10, FontStyle.Bold),
            .BackColor = Color.FromArgb(52, 199, 89),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand
        }
        _saveButton.FlatAppearance.BorderSize = 0
        AddHandler _saveButton.Click, AddressOf SaveButton_Click
        buttonPanel.Controls.Add(_saveButton)

        Me.Controls.Add(buttonPanel)
        Me.CancelButton = _cancelButton
    End Sub

    Private Function AddSectionHeader(parent As Panel, title As String, yPos As Integer) As Integer
        Dim header As New Label With {
            .Text = title,
            .Location = New Point(20, yPos),
            .Size = New Size(540, 30),
            .Font = New Font("Segoe UI", 13, FontStyle.Bold),
            .ForeColor = Color.FromArgb(0, 122, 255),
            .BackColor = Color.Transparent
        }
        parent.Controls.Add(header)

        Dim line As New Panel With {
            .Location = New Point(20, yPos + 32),
            .Size = New Size(540, 2),
            .BackColor = Color.FromArgb(0, 122, 255)
        }
        parent.Controls.Add(line)

        Return yPos + 45
    End Function

    Private Function AddTextBoxField(parent As Panel, labelText As String, ByRef textBox As TextBox, yPos As Integer, Optional placeholder As String = "") As Integer
        Dim label As New Label With {
            .Text = labelText,
            .Location = New Point(20, yPos),
            .Size = New Size(150, 20),
            .Font = New Font("Segoe UI", 9, FontStyle.Bold),
            .ForeColor = Color.FromArgb(28, 28, 30),
            .BackColor = Color.Transparent
        }
        parent.Controls.Add(label)

        textBox = New TextBox With {
            .Location = New Point(180, yPos),
            .Size = New Size(380, 25),
            .Font = New Font("Segoe UI", 10)
        }

        If Not String.IsNullOrEmpty(placeholder) Then
            textBox.PlaceholderText = placeholder
        End If

        parent.Controls.Add(textBox)
        Return yPos + 35
    End Function

    Private Function AddPasswordField(parent As Panel, labelText As String, ByRef textBox As TextBox, yPos As Integer) As Integer
        Dim label As New Label With {
            .Text = labelText,
            .Location = New Point(20, yPos),
            .Size = New Size(150, 20),
            .Font = New Font("Segoe UI", 9, FontStyle.Bold),
            .ForeColor = Color.FromArgb(28, 28, 30),
            .BackColor = Color.Transparent
        }
        parent.Controls.Add(label)

        textBox = New TextBox With {
            .Location = New Point(180, yPos),
            .Size = New Size(380, 25),
            .Font = New Font("Segoe UI", 10),
            .UseSystemPasswordChar = True
        }
        parent.Controls.Add(textBox)

        Return yPos + 35
    End Function

    Private Sub LoadSettings()
        _regionTextBox.Text = _config.Region
        _openApiBaseTextBox.Text = _config.OpenApiBase
        _mqttHostTextBox.Text = _config.MqttHost
        _mqttPortTextBox.Value = _config.MqttPort
        _accessIdTextBox.Text = _config.AccessId
        _accessSecretTextBox.Text = _config.AccessSecret
        _uidTextBox.Text = _config.Uid

        _pythonScriptPathTextBox.Text = _config.PythonScriptPath
        _pythonFallbackPathTextBox.Text = _config.PythonFallbackPath

        _showRawPayloadsCheckBox.Checked = _config.ShowRawPayloads
    End Sub

    Private Sub SaveButton_Click(sender As Object, e As EventArgs)
        Try
            ' Validation
            If String.IsNullOrWhiteSpace(_accessIdTextBox.Text) Then
                MessageBox.Show("L'Access ID est requis.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                _accessIdTextBox.Focus()
                Return
            End If

            If String.IsNullOrWhiteSpace(_accessSecretTextBox.Text) Then
                MessageBox.Show("L'Access Secret est requis.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                _accessSecretTextBox.Focus()
                Return
            End If

            If String.IsNullOrWhiteSpace(_pythonScriptPathTextBox.Text) Then
                MessageBox.Show("Le chemin du script Python est requis.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                _pythonScriptPathTextBox.Focus()
                Return
            End If

            ' Sauvegarder
            _config.Region = _regionTextBox.Text.Trim()
            _config.OpenApiBase = _openApiBaseTextBox.Text.Trim()
            _config.MqttHost = _mqttHostTextBox.Text.Trim()
            _config.MqttPort = CInt(_mqttPortTextBox.Value)
            _config.AccessId = _accessIdTextBox.Text.Trim()
            _config.AccessSecret = _accessSecretTextBox.Text.Trim()
            _config.Uid = _uidTextBox.Text.Trim()

            _config.PythonScriptPath = _pythonScriptPathTextBox.Text.Trim()
            _config.PythonFallbackPath = _pythonFallbackPathTextBox.Text.Trim()

            _config.ShowRawPayloads = _showRawPayloadsCheckBox.Checked

            _config.Save()

            MessageBox.Show("Configuration enregistrée avec succès !" & Environment.NewLine & Environment.NewLine & "Redémarrez l'application pour appliquer les changements.", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information)

            Me.DialogResult = DialogResult.OK
            Me.Close()

        Catch ex As Exception
            MessageBox.Show($"Erreur lors de la sauvegarde : {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub BrowsePythonButton_Click(sender As Object, e As EventArgs)
        Using dialog As New OpenFileDialog()
            dialog.Title = "Sélectionner le script Python"
            dialog.Filter = "Fichiers Python (*.py)|*.py|Tous les fichiers (*.*)|*.*"
            dialog.InitialDirectory = Application.StartupPath

            If dialog.ShowDialog() = DialogResult.OK Then
                _pythonScriptPathTextBox.Text = dialog.FileName
            End If
        End Using
    End Sub

    Private Sub BrowseFallbackButton_Click(sender As Object, e As EventArgs)
        Using dialog As New OpenFileDialog()
            dialog.Title = "Sélectionner le chemin alternatif du script Python"
            dialog.Filter = "Fichiers Python (*.py)|*.py|Tous les fichiers (*.*)|*.*"

            If dialog.ShowDialog() = DialogResult.OK Then
                _pythonFallbackPathTextBox.Text = dialog.FileName
            End If
        End Using
    End Sub

    Private Async Sub TestConnectionButton_Click(sender As Object, e As EventArgs)
        _testConnectionButton.Enabled = False
        _testConnectionButton.Text = "Test en cours..."

        Try
            ' Créer une config temporaire pour tester
            Dim testConfig As New TuyaConfig()
            testConfig.Region = _regionTextBox.Text.Trim()
            testConfig.OpenApiBase = _openApiBaseTextBox.Text.Trim()
            testConfig.MqttHost = _mqttHostTextBox.Text.Trim()
            testConfig.MqttPort = CInt(_mqttPortTextBox.Value)
            testConfig.AccessId = _accessIdTextBox.Text.Trim()
            testConfig.AccessSecret = _accessSecretTextBox.Text.Trim()
            testConfig.Uid = _uidTextBox.Text.Trim()

            ' Tester la connexion
            Dim tokenProvider As New TuyaTokenProvider(testConfig)
            Dim apiClient As New TuyaApiClient(testConfig, tokenProvider, Nothing)

            Dim devices = Await apiClient.GetAllDevicesAsync()

            MessageBox.Show($"Connexion réussie !" & Environment.NewLine & Environment.NewLine & $"{devices.Count} appareil(s) trouvé(s).", "Test de connexion", MessageBoxButtons.OK, MessageBoxIcon.Information)

        Catch ex As Exception
            MessageBox.Show($"Échec de la connexion :" & Environment.NewLine & Environment.NewLine & ex.Message, "Test de connexion", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            _testConnectionButton.Enabled = True
            _testConnectionButton.Text = "Tester la connexion"
        End Try
    End Sub
End Class