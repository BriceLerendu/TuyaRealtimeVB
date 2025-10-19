Imports System.Drawing
Imports System.Windows.Forms

Public Class IconPickerForm
    Inherits Form

    Private _selectedIcon As String = ""
    Private ReadOnly _iconCategories As New Dictionary(Of String, String())

    Public ReadOnly Property SelectedIcon As String
        Get
            Return _selectedIcon
        End Get
    End Property

    Public Sub New(Optional currentIcon As String = "")
        _selectedIcon = currentIcon
        InitializeIcons()
        InitializeComponent()
    End Sub

    Private Sub InitializeIcons()
        ' 🔋 ÉNERGIE & ÉLECTRICITÉ
        _iconCategories("⚡ Énergie & Électricité") = {
            "⚡", "🔋", "🔌", "💡", "🔆", "🌟", "✨", "💫",
            "⏱️", "📊", "📈", "📉", "🔢", "🎚️", "🎛️", "⏲️"
        }

        ' 🌡️ TEMPÉRATURE & CLIMAT
        _iconCategories("🌡️ Température & Climat") = {
            "🌡️", "🔥", "❄️", "💧", "☀️", "🌙", "🌤️", "⛅",
            "🌦️", "🌧️", "🌨️", "💨", "🌬️", "🍃", "🌊", "💦"
        }

        ' 🚪 SÉCURITÉ & CAPTEURS
        _iconCategories("🚪 Sécurité & Capteurs") = {
            "🚪", "🔐", "🔓", "🔒", "🗝️", "🚨", "🔔", "📢",
            "🎵", "🔊", "🔇", "👁️", "👀", "🎥", "📷", "📹",
            "🚦", "🚥", "⚠️", "☢️", "☣️", "⛔"
        }

        ' 🏠 CONFORT & MAISON
        _iconCategories("🏠 Confort & Maison") = {
            "🏠", "🏡", "🏢", "🏭", "🏗️", "🛋️", "🛏️", "🚿",
            "🚽", "🪟", "🚪", "🔑", "🪑", "🧹", "🧺", "🧼"
        }

        ' 💡 ÉCLAIRAGE
        _iconCategories("💡 Éclairage") = {
            "💡", "🕯️", "🔦", "🏮", "🪔", "💫", "✨", "⭐",
            "🌟", "💥", "🔆", "🔅", "🌞", "🌝", "🌛", "🌜"
        }

        ' 🎨 COULEURS & ÉTATS
        _iconCategories("🎨 Couleurs & États") = {
            "🔴", "🟠", "🟡", "🟢", "🔵", "🟣", "⚫", "⚪",
            "🟤", "🔶", "🔷", "🔸", "🔹", "🔺", "🔻", "💠"
        }

        ' ⏰ TEMPS & PROGRAMMATION
        _iconCategories("⏰ Temps & Programmation") = {
            "⏰", "⏱️", "⏲️", "⌚", "🕐", "🕑", "🕒", "🕓",
            "🕔", "🕕", "🕖", "🕗", "📅", "📆", "🗓️", "⏳"
        }

        ' 🎮 CONTRÔLE & COMMANDES
        _iconCategories("🎮 Contrôle & Commandes") = {
            "🎮", "🕹️", "🎛️", "🎚️", "📡", "📻", "📺", "🖥️",
            "⌨️", "🖱️", "🖲️", "💾", "💿", "📀", "☎️", "📱"
        }

        ' 🌐 RÉSEAU & CONNECTIVITÉ
        _iconCategories("🌐 Réseau & Connectivité") = {
            "🌐", "📶", "📡", "🛰️", "📞", "📟", "📠", "✉️",
            "📧", "📨", "📩", "🔗", "⛓️", "🔀", "🔁", "🔂"
        }

        ' 🧪 QUALITÉ AIR & ENVIRONNEMENT
        _iconCategories("🧪 Air & Environnement") = {
            "🧪", "🌫️", "☁️", "🌪️", "🍂", "🍁", "🌿", "🌱",
            "🌲", "🌳", "🌴", "🌵", "🪴", "🌾", "🍀", "☘️"
        }

        ' ⚙️ SYSTÈME & PARAMÈTRES
        _iconCategories("⚙️ Système & Paramètres") = {
            "⚙️", "🔧", "🔨", "🛠️", "⚒️", "🔩", "⚡", "🔄",
            "🔃", "♻️", "📋", "📌", "📍", "🎯", "✅", "❌"
        }

        ' 🚗 MOBILITÉ & TRANSPORT
        _iconCategories("🚗 Mobilité") = {
            "🚗", "🚙", "🚕", "🚌", "🚎", "🏎️", "🚓", "🚑",
            "🚒", "🚐", "🚚", "🚛", "🚜", "🛵", "🏍️", "🚲"
        }

        ' 🎵 AUDIO & MULTIMÉDIA
        _iconCategories("🎵 Audio & Multimédia") = {
            "🎵", "🎶", "🎼", "🎹", "🎸", "🎺", "🎷", "🔊",
            "🔉", "🔈", "📢", "📣", "📯", "🔔", "🔕", "🎧"
        }

        ' 🌈 AUTRES SYMBOLES
        _iconCategories("🌈 Autres") = {
            "❤️", "💚", "💙", "💛", "🧡", "💜", "🖤", "🤍",
            "🤎", "❓", "❔", "❕", "❗", "‼️", "⁉️", "🆘"
        }
    End Sub

    Private Sub InitializeComponent()
        Me.Text = "Sélectionner une icône"
        Me.Size = New Size(650, 700)
        Me.StartPosition = FormStartPosition.CenterParent
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.BackColor = Color.FromArgb(242, 242, 247)

        ' === HEADER ===
        Dim header = New Panel With {
            .Dock = DockStyle.Top,
            .Height = 70,
            .BackColor = Color.FromArgb(45, 45, 48)
        }

        Dim title = New Label With {
            .Text = "🎨 Sélectionner une icône",
            .Font = New Font("Segoe UI", 14, FontStyle.Bold),
            .ForeColor = Color.White,
            .Location = New Point(20, 15),
            .AutoSize = True
        }

        Dim subtitle = New Label With {
            .Text = "Cliquez sur une icône pour la sélectionner",
            .Font = New Font("Segoe UI", 9),
            .ForeColor = Color.FromArgb(180, 180, 180),
            .Location = New Point(20, 45),
            .AutoSize = True
        }

        header.Controls.AddRange({title, subtitle})

        ' Zone de prévisualisation améliorée
        Dim previewPanel = New Panel With {
    .Dock = DockStyle.Top,
    .Height = 90,  ' Légèrement plus haut
    .BackColor = Color.White,
    .Padding = New Padding(20, 15, 20, 15)
}

        Dim lblPreview = New Label With {
    .Text = "Icône sélectionnée :",
    .Font = New Font("Segoe UI", 10, FontStyle.Bold),
    .ForeColor = Color.FromArgb(99, 99, 102),
    .Location = New Point(20, 25),
    .AutoSize = True
}

        ' Container pour l'icône avec bordure arrondie visuelle
        Dim iconContainer = New Panel With {
    .Location = New Point(200, 15),
    .Size = New Size(60, 60),
    .BackColor = Color.FromArgb(240, 245, 255),
    .BorderStyle = BorderStyle.FixedSingle
}

        Dim previewIcon = New Label With {
    .Name = "previewIcon",
    .Text = If(String.IsNullOrEmpty(_selectedIcon), "❓", _selectedIcon),
    .Font = New Font("Segoe UI Emoji", 28),
    .ForeColor = Color.FromArgb(0, 122, 255),
    .Dock = DockStyle.Fill,
    .TextAlign = ContentAlignment.MiddleCenter,
    .BackColor = Color.Transparent
}

        iconContainer.Controls.Add(previewIcon)
        previewPanel.Controls.AddRange({lblPreview, iconContainer})

        ' === ZONE DE SCROLL AVEC CATÉGORIES ===
        Dim scrollPanel = New Panel With {
            .Dock = DockStyle.Fill,
            .AutoScroll = True,
            .BackColor = Color.White,
            .Padding = New Padding(20)
        }

        Dim yPos As Integer = 10

        ' ✅ FIX: Utiliser des noms de variables différents
        For Each categoryPair In _iconCategories
            ' Titre de catégorie
            Dim categoryLabel = New Label With {
                .Text = categoryPair.Key,
                .Font = New Font("Segoe UI", 11, FontStyle.Bold),
                .ForeColor = Color.FromArgb(45, 45, 48),
                .Location = New Point(10, yPos),
                .AutoSize = True
            }
            scrollPanel.Controls.Add(categoryLabel)
            yPos += 35

            ' Grille d'icônes
            Dim iconsPanel = New FlowLayoutPanel With {
                .Location = New Point(10, yPos),
                .Width = 580,
                .AutoSize = True,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Padding = New Padding(5)
            }

            ' ✅ FIX: Renommer 'icon' en 'iconText'
            For Each iconText As String In categoryPair.Value
                Dim iconButton = CreateIconButton(iconText, previewIcon)
                iconsPanel.Controls.Add(iconButton)
            Next

            scrollPanel.Controls.Add(iconsPanel)
            yPos += iconsPanel.Height + 20
        Next

        ' === FOOTER AVEC BOUTONS ===
        Dim footer = New Panel With {
            .Dock = DockStyle.Bottom,
            .Height = 70,
            .BackColor = Color.White,
            .Padding = New Padding(20, 15, 20, 15)
        }

        Dim btnOK = New Button With {
            .Text = "✓ Valider",
            .DialogResult = DialogResult.OK,
            .Location = New Point(350, 15),
            .Size = New Size(120, 40),
            .BackColor = Color.FromArgb(52, 199, 89),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 10, FontStyle.Bold),
            .Cursor = Cursors.Hand
        }
        btnOK.FlatAppearance.BorderSize = 0

        Dim btnCancel = New Button With {
            .Text = "✕ Annuler",
            .DialogResult = DialogResult.Cancel,
            .Location = New Point(480, 15),
            .Size = New Size(120, 40),
            .BackColor = Color.FromArgb(142, 142, 147),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 10, FontStyle.Bold),
            .Cursor = Cursors.Hand
        }
        btnCancel.FlatAppearance.BorderSize = 0

        footer.Controls.AddRange({btnOK, btnCancel})

        ' === ASSEMBLAGE ===
        Me.Controls.Add(scrollPanel)
        Me.Controls.Add(previewPanel)
        Me.Controls.Add(footer)
        Me.Controls.Add(header)

        Me.AcceptButton = btnOK
        Me.CancelButton = btnCancel
    End Sub

    Private Function CreateIconButton(iconText As String, previewLabel As Label) As Button
        Dim btn = New Button With {
            .Text = iconText,
            .Size = New Size(55, 55),
            .Font = New Font("Segoe UI Emoji", 20),
            .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.FromArgb(248, 248, 250),
            .ForeColor = Color.FromArgb(28, 28, 30),
            .Cursor = Cursors.Hand,
            .Margin = New Padding(3),
            .Tag = iconText
        }
        btn.FlatAppearance.BorderColor = Color.FromArgb(220, 220, 225)
        btn.FlatAppearance.BorderSize = 1

        ' Effet survol
        AddHandler btn.MouseEnter, Sub(s, e)
                                       btn.BackColor = Color.FromArgb(230, 245, 255)
                                       btn.FlatAppearance.BorderColor = Color.FromArgb(0, 122, 255)
                                   End Sub

        AddHandler btn.MouseLeave, Sub(s, e)
                                       If Not _selectedIcon.Equals(iconText) Then
                                           btn.BackColor = Color.FromArgb(248, 248, 250)
                                           btn.FlatAppearance.BorderColor = Color.FromArgb(220, 220, 225)
                                       End If
                                   End Sub

        ' Clic sur icône
        AddHandler btn.Click, Sub(s, e)
                                  _selectedIcon = iconText
                                  previewLabel.Text = iconText

                                  ' Réinitialiser tous les boutons du même panneau
                                  If btn.Parent IsNot Nothing Then
                                      For Each ctrl As Control In btn.Parent.Controls
                                          Dim otherBtn As Button = TryCast(ctrl, Button)
                                          If otherBtn IsNot Nothing Then
                                              otherBtn.BackColor = Color.FromArgb(248, 248, 250)
                                              otherBtn.FlatAppearance.BorderColor = Color.FromArgb(220, 220, 225)
                                          End If
                                      Next
                                  End If

                                  ' Highlight le bouton sélectionné
                                  btn.BackColor = Color.FromArgb(230, 245, 255)
                                  btn.FlatAppearance.BorderColor = Color.FromArgb(0, 122, 255)
                              End Sub

        ' Si c'est l'icône actuellement sélectionnée, la mettre en surbrillance
        If iconText.Equals(_selectedIcon) Then
            btn.BackColor = Color.FromArgb(230, 245, 255)
            btn.FlatAppearance.BorderColor = Color.FromArgb(0, 122, 255)
        End If

        Return btn
    End Function
End Class