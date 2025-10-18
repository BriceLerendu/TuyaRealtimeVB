Imports System.Drawing
Imports System.Windows.Forms

Public Class NotificationsPopup
    Inherits Form

    Private _notificationManager As NotificationManager
    Private _notificationsPanel As FlowLayoutPanel
    Private _emptyLabel As Label
    Private _headerLabel As Label
    Private _clearButton As Button

    Public Sub New(notificationManager As NotificationManager)
        _notificationManager = notificationManager
        InitializeComponent()
        LoadNotifications()
    End Sub

    Private Sub InitializeComponent()
        Me.Text = "Notifications"
        Me.Size = New Size(450, 600)
        Me.StartPosition = FormStartPosition.Manual
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.BackColor = Color.FromArgb(242, 242, 247)

        ' Header
        Dim headerPanel As New Panel With {
            .Dock = DockStyle.Top,
            .Height = 60,
            .BackColor = Color.FromArgb(45, 45, 48),
            .Padding = New Padding(15)
        }

        _headerLabel = New Label With {
            .Text = "Notifications",
            .Font = New Font("Segoe UI", 14, FontStyle.Bold),
            .ForeColor = Color.White,
            .AutoSize = True,
            .Location = New Point(15, 18)
        }
        headerPanel.Controls.Add(_headerLabel)

        ' Bouton "Tout marquer comme lu"
        Dim markReadButton As New Button With {
            .Text = "✓ Tout lire",
            .Font = New Font("Segoe UI", 9),
            .Size = New Size(90, 28),
            .Location = New Point(240, 16),
            .BackColor = Color.FromArgb(0, 122, 255),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand
        }
        markReadButton.FlatAppearance.BorderSize = 0
        AddHandler markReadButton.Click, AddressOf MarkAllRead_Click
        headerPanel.Controls.Add(markReadButton)

        ' Bouton "Effacer tout"
        _clearButton = New Button With {
            .Text = "🗑️ Effacer",
            .Font = New Font("Segoe UI", 9),
            .Size = New Size(90, 28),
            .Location = New Point(340, 16),
            .BackColor = Color.FromArgb(255, 59, 48),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand
        }
        _clearButton.FlatAppearance.BorderSize = 0
        AddHandler _clearButton.Click, AddressOf ClearAll_Click
        headerPanel.Controls.Add(_clearButton)

        Me.Controls.Add(headerPanel)

        ' Panel de notifications avec scroll
        ' Panel de notifications avec scroll
        _notificationsPanel = New FlowLayoutPanel With {
            .Dock = DockStyle.Fill,
            .AutoScroll = True,
            .FlowDirection = FlowDirection.TopDown,
            .WrapContents = False,
            .Padding = New Padding(10, 80, 10, 10),
            .BackColor = Color.FromArgb(242, 242, 247)
}
        Me.Controls.Add(_notificationsPanel)

        ' Label "Aucune notification"
        _emptyLabel = New Label With {
            .Text = "Aucune notification",
            .Font = New Font("Segoe UI", 12),
            .ForeColor = Color.FromArgb(142, 142, 147),
            .AutoSize = True,
            .Visible = False
        }
        _notificationsPanel.Controls.Add(_emptyLabel)
    End Sub

    Private Sub LoadNotifications()
        _notificationsPanel.Controls.Clear()

        Dim notifications = _notificationManager.GetNotifications(includeRead:=True)

        If notifications.Count = 0 Then
            _emptyLabel.Visible = True
            _emptyLabel.Location = New Point(
                (_notificationsPanel.Width - _emptyLabel.Width) \ 2,
                100
            )
            _notificationsPanel.Controls.Add(_emptyLabel)

            _headerLabel.Text = "Notifications"
        Else
            Dim unreadCount = _notificationManager.GetUnreadCount()
            If unreadCount > 0 Then
                _headerLabel.Text = $"Notifications ({unreadCount})"
            Else
                _headerLabel.Text = "Notifications"
            End If

            For Each notification In notifications
                Dim card = CreateNotificationCard(notification)
                _notificationsPanel.Controls.Add(card)
            Next
        End If
    End Sub

    Private Function CreateNotificationCard(notification As NotificationEntry) As Panel
        Dim card As New Panel With {
            .Width = _notificationsPanel.ClientSize.Width - 30,
            .Height = 85,
            .Margin = New Padding(5),
            .Padding = New Padding(12),
            .BackColor = If(notification.IsRead, Color.White, Color.FromArgb(230, 245, 255)),
            .Cursor = Cursors.Hand
        }

        ' Bordure gauche colorée selon le type
        Dim borderColor As Color
        Select Case notification.Type
            Case NotificationType.Critical
                borderColor = Color.FromArgb(255, 59, 48)
            Case NotificationType.Warning
                borderColor = Color.FromArgb(255, 149, 0)
            Case NotificationType.Info
                borderColor = Color.FromArgb(0, 122, 255)
        End Select

        AddHandler card.Paint, Sub(sender, e)
                                   Dim g = e.Graphics
                                   Using pen As New Pen(borderColor, 4)
                                       g.DrawLine(pen, 0, 0, 0, card.Height)
                                   End Using
                               End Sub

        ' Icône/Message (gros et bold)
        Dim messageLabel As New Label With {
            .Text = notification.Message,
            .Font = New Font("Segoe UI", 11, FontStyle.Bold),
            .ForeColor = Color.FromArgb(28, 28, 30),
            .AutoSize = True,
            .Location = New Point(15, 10),
            .BackColor = Color.Transparent
        }
        card.Controls.Add(messageLabel)

        ' Nom de l'appareil
        Dim deviceLabel As New Label With {
            .Text = notification.DeviceName,
            .Font = New Font("Segoe UI", 10),
            .ForeColor = Color.FromArgb(60, 60, 67),
            .AutoSize = True,
            .Location = New Point(15, 35),
            .BackColor = Color.Transparent
        }
        card.Controls.Add(deviceLabel)

        ' Pièce + Temps
        Dim detailsText As String = ""
        If Not String.IsNullOrEmpty(notification.RoomName) Then
            detailsText = notification.RoomName & " • "
        End If
        detailsText &= notification.GetTimeAgo()

        Dim detailsLabel As New Label With {
            .Text = detailsText,
            .Font = New Font("Segoe UI", 9),
            .ForeColor = Color.FromArgb(142, 142, 147),
            .AutoSize = True,
            .Location = New Point(15, 57),
            .BackColor = Color.Transparent
        }
        card.Controls.Add(detailsLabel)

        ' Badge "Non lu" si applicable
        If Not notification.IsRead Then
            Dim unreadBadge As New Label With {
                .Text = "●",
                .Font = New Font("Segoe UI", 20),
                .ForeColor = Color.FromArgb(0, 122, 255),
                .AutoSize = True,
                .Location = New Point(card.Width - 30, 30),
                .BackColor = Color.Transparent
            }
            card.Controls.Add(unreadBadge)
        End If

        ' Handler pour marquer comme lu
        Dim clickHandler As EventHandler = Sub(s, e)
                                               If Not notification.IsRead Then
                                                   _notificationManager.MarkAsRead(notification)
                                                   LoadNotifications()
                                               End If
                                           End Sub

        ' Ajouter le handler au panel
        AddHandler card.Click, clickHandler

        ' Propager le clic des labels vers le panel
        For Each ctrl As Control In card.Controls
            If TypeOf ctrl Is Label Then
                AddHandler ctrl.Click, clickHandler
            End If
        Next

        Return card
    End Function

    Private Sub MarkAllRead_Click(sender As Object, e As EventArgs)
        _notificationManager.MarkAllAsRead()
        LoadNotifications()
    End Sub

    Private Sub ClearAll_Click(sender As Object, e As EventArgs)
        Dim result = MessageBox.Show(
            "Voulez-vous vraiment effacer toutes les notifications ?",
            "Confirmation",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question)

        If result = DialogResult.Yes Then
            _notificationManager.ClearNotifications()
            LoadNotifications()
        End If
    End Sub

    Public Sub ShowAtLocation(x As Integer, y As Integer)
        Me.StartPosition = FormStartPosition.Manual
        Me.Location = New Point(x, y)
        Me.Show()
        Me.BringToFront()
    End Sub
End Class