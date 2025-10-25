Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

Public Class RoomTableView
    Inherits DataGridView

    Private _devices As New Dictionary(Of String, List(Of DeviceCard))

    Public Sub New()
        ' Configuration du DataGridView - ULTRA COMPACT
        Me.RowHeadersVisible = False
        Me.AllowUserToAddRows = False
        Me.AllowUserToDeleteRows = False
        Me.AllowUserToResizeRows = False
        Me.ReadOnly = True
        Me.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        Me.MultiSelect = False
        Me.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
        Me.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None
        Me.BorderStyle = BorderStyle.None
        Me.CellBorderStyle = DataGridViewCellBorderStyle.Single
        Me.GridColor = Color.FromArgb(230, 230, 235)
        Me.BackgroundColor = Color.FromArgb(250, 250, 252)

        ' Style des cellules - COMPACT
        Me.DefaultCellStyle.BackColor = Color.White
        Me.DefaultCellStyle.ForeColor = Color.FromArgb(28, 28, 30)
        Me.DefaultCellStyle.Font = New Font("Segoe UI", 7)
        Me.DefaultCellStyle.Padding = New Padding(2, 1, 2, 1)
        Me.DefaultCellStyle.WrapMode = DataGridViewTriState.False
        Me.RowTemplate.Height = 24

        ' Style des en-têtes - COMPACT
        Me.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(0, 122, 255)
        Me.ColumnHeadersDefaultCellStyle.ForeColor = Color.White
        Me.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI Semibold", 7)
        Me.ColumnHeadersDefaultCellStyle.Padding = New Padding(3, 2, 3, 2)
        Me.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
        Me.ColumnHeadersHeight = 28
        Me.EnableHeadersVisualStyles = False

        ' Style de sélection
        Me.DefaultCellStyle.SelectionBackColor = Color.FromArgb(230, 240, 255)
        Me.DefaultCellStyle.SelectionForeColor = Color.FromArgb(28, 28, 30)
    End Sub

    ' Ajouter un appareil à une pièce
    Public Sub AddDevice(roomName As String, device As DeviceCard)
        If Not _devices.ContainsKey(roomName) Then
            _devices(roomName) = New List(Of DeviceCard)
        End If

        _devices(roomName).Add(device)
    End Sub

    ' Construire le tableau avec propriétés
    Public Sub BuildTable()
        Try
            Me.SuspendLayout()

            Me.Rows.Clear()
            Me.Columns.Clear()

            If _devices.Count = 0 Then
                Me.ResumeLayout()
                Return
            End If

            ' Trouver le nombre maximum d'appareils dans une pièce (TOUS les appareils)
            Dim maxDevices As Integer = _devices.Values.Max(Function(devs) devs.Count)
            ' Pas de limite - afficher tous les appareils !

            ' Calculer la largeur optimale pour la colonne Room basée sur le nom le plus long
            Dim maxRoomNameLength As Integer = 0
            Dim longestRoomDisplay As String = ""

            For Each roomName In _devices.Keys
                Dim roomDisplay = GetRoomIcon(roomName) & " " & roomName.ToUpper()
                If roomDisplay.Length > maxRoomNameLength Then
                    maxRoomNameLength = roomDisplay.Length
                    longestRoomDisplay = roomDisplay
                End If
            Next

            ' Calculer la largeur en pixels (environ 6 pixels par caractère pour la police 7pt)
            ' On ajoute un padding pour être sûr que le texte passe
            Dim roomColumnWidth As Integer = Math.Max(50, (maxRoomNameLength * 6) + 10)

            ' Colonne 1 : Pièce (largeur optimisée au nom le plus long)
            Dim roomColumn As New DataGridViewTextBoxColumn With {
                .Name = "Room",
                .HeaderText = "🏠 PIÈCE",
                .Width = roomColumnWidth,
                .MinimumWidth = roomColumnWidth,
                .DefaultCellStyle = New DataGridViewCellStyle With {
                    .BackColor = Color.FromArgb(245, 245, 250),
                    .Font = New Font("Segoe UI Semibold", 7),
                    .ForeColor = Color.FromArgb(0, 122, 255),
                    .Alignment = DataGridViewContentAlignment.MiddleLeft,
                    .Padding = New Padding(2, 1, 2, 1)
                }
            }
            Me.Columns.Add(roomColumn)

            ' Colonnes pour chaque appareil (largeur ultra-compacte)
            For i As Integer = 0 To maxDevices - 1
                Dim deviceColumn As New DataGridViewTextBoxColumn With {
                    .Name = $"Device{i + 1}",
                    .HeaderText = $"APP {i + 1}",
                    .Width = 65,
                    .MinimumWidth = 50
                }
                Me.Columns.Add(deviceColumn)
            Next

            ' Remplir les lignes avec 3 lignes par pièce (Nom + 2 propriétés)
            For Each room In _devices
                Dim roomName As String = room.Key
                Dim devices As List(Of DeviceCard) = room.Value.Take(maxDevices).ToList()

                ' LIGNE 1 : Nom des appareils
                Dim row1 As Integer = Me.Rows.Add()
                Dim roomDisplay = GetRoomIcon(roomName) & " " & roomName.ToUpper()
                Me.Rows(row1).Cells("Room").Value = roomDisplay
                Me.Rows(row1).Cells("Room").Style.Font = New Font("Segoe UI Semibold", 7)

                For i As Integer = 0 To devices.Count - 1
                    Dim device As DeviceCard = devices(i)
                    Dim deviceName As String = device.DeviceName
                    If String.IsNullOrEmpty(deviceName) Then deviceName = "App."

                    ' Tronquer le nom si trop long
                    If deviceName.Length > 12 Then
                        deviceName = deviceName.Substring(0, 10) & ".."
                    End If

                    Me.Rows(row1).Cells($"Device{i + 1}").Value = deviceName
                    Me.Rows(row1).Cells($"Device{i + 1}").Style.Font = New Font("Segoe UI", 7, FontStyle.Bold)
                    Me.Rows(row1).Cells($"Device{i + 1}").Style.BackColor = Color.FromArgb(240, 248, 255)
                    Me.Rows(row1).Cells($"Device{i + 1}").Style.Alignment = DataGridViewContentAlignment.MiddleCenter
                Next

                ' LIGNE 2 : Première propriété
                Dim row2 As Integer = Me.Rows.Add()
                Me.Rows(row2).Cells("Room").Value = ""
                Me.Rows(row2).Height = 22

                For i As Integer = 0 To devices.Count - 1
                    Dim device As DeviceCard = devices(i)
                    Dim props = GetTopProperties(device)

                    If props.Count > 0 Then
                        Me.Rows(row2).Cells($"Device{i + 1}").Value = props(0)
                        Me.Rows(row2).Cells($"Device{i + 1}").Style.Font = New Font("Segoe UI", 7)
                        Me.Rows(row2).Cells($"Device{i + 1}").Style.Alignment = DataGridViewContentAlignment.MiddleCenter
                    End If
                Next

                ' LIGNE 3 : Deuxième propriété
                Dim row3 As Integer = Me.Rows.Add()
                Me.Rows(row3).Cells("Room").Value = ""
                Me.Rows(row3).Height = 22

                For i As Integer = 0 To devices.Count - 1
                    Dim device As DeviceCard = devices(i)
                    Dim props = GetTopProperties(device)

                    If props.Count > 1 Then
                        Me.Rows(row3).Cells($"Device{i + 1}").Value = props(1)
                        Me.Rows(row3).Cells($"Device{i + 1}").Style.Font = New Font("Segoe UI", 7)
                        Me.Rows(row3).Cells($"Device{i + 1}").Style.Alignment = DataGridViewContentAlignment.MiddleCenter
                    End If
                Next
            Next

        Catch ex As Exception
            Debug.WriteLine($"Erreur BuildTable: {ex.Message}")
            Debug.WriteLine($"StackTrace: {ex.StackTrace}")
        Finally
            Me.ResumeLayout()
        End Try
    End Sub

    ' Extraire les 2 propriétés les plus pertinentes d'un appareil (format compact)
    Private Function GetTopProperties(device As DeviceCard) As List(Of String)
        Dim properties As New List(Of String)
        Dim allProperties As New Dictionary(Of String, String)

        Try
            ' Scanner tous les contrôles de la DeviceCard pour extraire les propriétés
            For Each ctrl As Control In device.Controls
                If TypeOf ctrl Is Panel Then
                    Dim propPanel As Panel = DirectCast(ctrl, Panel)
                    Dim propName As String = ""
                    Dim propValue As String = ""

                    For Each subCtrl As Control In propPanel.Controls
                        If TypeOf subCtrl Is Label Then
                            Dim lbl As Label = DirectCast(subCtrl, Label)
                            ' Le nom de la propriété est dans le label de gauche
                            If lbl.Location.X >= 20 AndAlso lbl.Location.X < 100 Then
                                propName = lbl.Text
                                ' La valeur est dans le label de droite
                            ElseIf lbl.Location.X > 150 Then
                                propValue = lbl.Text
                            End If
                        End If
                    Next

                    If Not String.IsNullOrEmpty(propName) AndAlso Not String.IsNullOrEmpty(propValue) Then
                        allProperties(propName) = propValue
                    End If
                End If
            Next

            ' Sélectionner les 2 propriétés les plus pertinentes (format ultra-compact)
            ' Priorité 1 : Température
            If allProperties.ContainsKey("Température") Then
                properties.Add($"🌡️{allProperties("Température")}")
            End If

            ' Priorité 2 : Humidité
            If allProperties.ContainsKey("Humidité") AndAlso properties.Count < 2 Then
                properties.Add($"💧{allProperties("Humidité")}")
            End If

            ' Priorité 3 : Puissance
            If properties.Count < 2 Then
                If allProperties.ContainsKey("Puissance") Then
                    Dim val = allProperties("Puissance")
                    ' Abréger les unités
                    val = val.Replace("Watts", "W").Replace("watts", "W")
                    properties.Add($"⚡{val}")
                ElseIf allProperties.ContainsKey("Puissance actuelle") Then
                    Dim val = allProperties("Puissance actuelle")
                    val = val.Replace("Watts", "W").Replace("watts", "W")
                    properties.Add($"⚡{val}")
                End If
            End If

            ' Priorité 4 : Tension
            If properties.Count < 2 AndAlso allProperties.ContainsKey("Tension") Then
                Dim val = allProperties("Tension")
                val = val.Replace("Volts", "V").Replace("volts", "V")
                properties.Add($"🔋{val}")
            End If

            ' Priorité 5 : Courant
            If properties.Count < 2 AndAlso allProperties.ContainsKey("Courant") Then
                Dim val = allProperties("Courant")
                val = val.Replace("Ampères", "A").Replace("ampères", "A")
                properties.Add($"🔌{val}")
            End If

            ' Priorité 6 : État (Switch, Contact, Mouvement) - format compact
            If properties.Count < 2 Then
                If allProperties.ContainsKey("Interrupteur") Then
                    Dim val = allProperties("Interrupteur")
                    properties.Add(If(val.Contains("ON"), "✅ON", "❌OFF"))
                ElseIf allProperties.ContainsKey("Contact") Then
                    Dim val = allProperties("Contact")
                    properties.Add(If(val.Contains("ON"), "🔓Ouv", "🔒Ferm"))
                ElseIf allProperties.ContainsKey("Mouvement") Then
                    Dim val = allProperties("Mouvement")
                    properties.Add(If(val.Contains("Détecté"), "👁️Oui", "😴Non"))
                End If
            End If

            ' Priorité 7 : Batterie
            If properties.Count < 2 AndAlso allProperties.ContainsKey("Batterie") Then
                properties.Add($"🔋{allProperties("Batterie")}")
            End If

            ' Si on n'a toujours rien, prendre les 2 premières propriétés disponibles (format compact)
            If properties.Count = 0 AndAlso allProperties.Count > 0 Then
                For Each kvp In allProperties.Take(2)
                    Dim shortVal = kvp.Value
                    If shortVal.Length > 10 Then
                        shortVal = shortVal.Substring(0, 8) & ".."
                    End If
                    properties.Add($"📊{shortVal}")
                Next
            End If

        Catch ex As Exception
            Debug.WriteLine($"Erreur GetTopProperties: {ex.Message}")
        End Try

        Return properties
    End Function

    Private Function GetRoomIcon(roomName As String) As String
        Dim lowerName As String = roomName.ToLower()

        If lowerName.Contains("salon") OrElse lowerName.Contains("living") Then Return "🛋️"
        If lowerName.Contains("chambre") OrElse lowerName.Contains("bedroom") Then Return "🛏️"
        If lowerName.Contains("cuisine") OrElse lowerName.Contains("kitchen") Then Return "🍳"
        If lowerName.Contains("salle de bain") OrElse lowerName.Contains("bathroom") Then Return "🚿"
        If lowerName.Contains("bureau") OrElse lowerName.Contains("office") Then Return "💼"
        If lowerName.Contains("garage") Then Return "🚗"
        If lowerName.Contains("jardin") OrElse lowerName.Contains("garden") Then Return "🌳"
        If lowerName.Contains("entrée") OrElse lowerName.Contains("entrance") Then Return "🚪"
        If lowerName.Contains("couloir") OrElse lowerName.Contains("hallway") Then Return "🚶"

        Return "🏠"
    End Function

End Class