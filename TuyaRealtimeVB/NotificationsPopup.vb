Imports System.Drawing
Imports System.Windows.Forms

Public Class NotificationsPopup
    Inherits Form

#Region "Constantes"
    Private Const POPUP_WIDTH As Integer = 450
    Private Const POPUP_HEIGHT As Integer = 600
    Private Const HEADER_HEIGHT As Integer = 60
    Private Const CARD_HEIGHT As Integer = 85
    Private Const CARD_MARGIN As Integer = 5
    Private Const CARD_PADDING As Integer = 12
    Private Const BORDER_WIDTH As Integer = 4

    Private Shared ReadOnly ColorBackground As Color = Color.FromArgb(242, 242, 247)
    Private Shared ReadOnly ColorHeader As Color = Color.FromArgb(45, 45, 48)
    Private Shared ReadOnly ColorCardRead As Color = Color.White
    Private Shared ReadOnly ColorCardUnread As Color = Color.FromArgb(230, 245, 255)
    Private Shared ReadOnly ColorTextPrimary As Color = Color.FromArgb(28, 28, 30)
    Private Shared ReadOnly ColorTextSecondary As Color = Color.FromArgb(60, 60, 67)
    Private Shared ReadOnly ColorTextTertiary As Color = Color.FromArgb(142, 142, 147)
    Private Shared ReadOnly ColorCritical As Color = Color.FromArgb(255, 59, 48)
    Private Shared ReadOnly ColorWarning As Color = Color.FromArgb(255, 149, 0)
    Private Shared ReadOnly ColorInfo As Color = Color.FromArgb(0, 122, 255)
#End Region

#Region "Champs privés"
    Private ReadOnly _notificationManager As NotificationManager
    Private _notificationsPanel As FlowLayoutPanel
    Private _emptyLabel As Label
    Private _headerLabel As Label
    Private _clearButton As Button
#End Region

#Region "Événements"
    ''' <summary>
    ''' Déclenché quand les notifications ont changé (lu, effacé, etc.)
    ''' </summary>
    Public Event NotificationsChanged As EventHandler
#End Region

#Region "Initialisation"
    Public Sub New(notificationManager As NotificationManager)
        _notificationManager = notificationManager
        InitializeComponent()
        LoadNotifications()
    End Sub

    Private Sub InitializeComponent()
        ConfigureForm()

        Dim headerPanel = CreateHeaderPanel()
        Me.Controls.Add(headerPanel)

        _notificationsPanel = CreateNotificationsPanel()
        Me.Controls.Add(_notificationsPanel)

        _emptyLabel = CreateEmptyLabel()
        _notificationsPanel.Controls.Add(_emptyLabel)
    End Sub

    Private Sub ConfigureForm()
        Text = "Notifications"
        Size = New Size(POPUP_WIDTH, POPUP_HEIGHT)
        StartPosition = FormStartPosition.Manual
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        BackColor = ColorBackground
        ShowInTaskbar = False
    End Sub

    Private Function CreateHeaderPanel() As Panel
        Dim panel = New Panel With {
            .Dock = DockStyle.Top,
            .Height = HEADER_HEIGHT,
            .BackColor = ColorHeader,
            .Padding = New Padding(15)
        }

        _headerLabel = New Label With {
            .Text = "Notifications",
            .Font = New Font("Segoe UI", 14, FontStyle.Bold),
            .ForeColor = Color.White,
            .AutoSize = True,
            .Location = New Point(15, 18)
        }
        panel.Controls.Add(_headerLabel)

        Dim markReadButton = CreateButton("✓ Tout lire", 240, ColorInfo, AddressOf MarkAllRead_Click)
        panel.Controls.Add(markReadButton)

        _clearButton = CreateButton("🗑️ Effacer", 340, ColorCritical, AddressOf ClearAll_Click)
        panel.Controls.Add(_clearButton)

        Return panel
    End Function

    Private Function CreateButton(text As String, x As Integer, bgColor As Color, handler As EventHandler) As Button
        Dim btn = New Button With {
            .Text = text,
            .Font = New Font("Segoe UI", 9),
            .Size = New Size(90, 28),
            .Location = New Point(x, 16),
            .BackColor = bgColor,
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand
        }
        btn.FlatAppearance.BorderSize = 0
        AddHandler btn.Click, handler
        Return btn
    End Function

    Private Function CreateNotificationsPanel() As FlowLayoutPanel
        Return New FlowLayoutPanel With {
            .Dock = DockStyle.Fill,
            .AutoScroll = True,
            .FlowDirection = FlowDirection.TopDown,
            .WrapContents = False,
            .Padding = New Padding(10, 80, 10, 10),
            .BackColor = ColorBackground
        }
    End Function

    Private Function CreateEmptyLabel() As Label
        Return New Label With {
            .Text = "Aucune notification",
            .Font = New Font("Segoe UI", 12),
            .ForeColor = ColorTextTertiary,
            .AutoSize = True,
            .Visible = False
        }
    End Function
#End Region

#Region "Chargement des notifications"
    Private Sub LoadNotifications()
        _notificationsPanel.Controls.Clear()

        Dim notifications = _notificationManager.GetNotifications(includeRead:=True)

        If notifications.Count = 0 Then
            ShowEmptyState()
        Else
            ShowNotifications(notifications)
        End If
    End Sub

    Private Sub ShowEmptyState()
        _emptyLabel.Visible = True
        _emptyLabel.Location = New Point(
            (_notificationsPanel.Width - _emptyLabel.Width) \ 2,
            100
        )
        _notificationsPanel.Controls.Add(_emptyLabel)
        _headerLabel.Text = "Notifications"
    End Sub

    Private Sub ShowNotifications(notifications As List(Of NotificationEntry))
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
    End Sub
#End Region

#Region "Création des cartes"
    Private Function CreateNotificationCard(notification As NotificationEntry) As Panel
        Dim card = New Panel With {
            .Width = _notificationsPanel.ClientSize.Width - 30,
            .Height = CARD_HEIGHT,
            .Margin = New Padding(CARD_MARGIN),
            .Padding = New Padding(CARD_PADDING),
            .BackColor = If(notification.IsRead, ColorCardRead, ColorCardUnread),
            .Cursor = Cursors.Hand
        }

        AddLeftBorder(card, GetNotificationColor(notification.Type))
        AddCardControls(card, notification)
        AttachClickHandlers(card, notification)

        Return card
    End Function

    Private Sub AddLeftBorder(card As Panel, borderColor As Color)
        AddHandler card.Paint, Sub(sender, e)
                                   Dim g = e.Graphics
                                   Using pen As New Pen(borderColor, BORDER_WIDTH)
                                       g.DrawLine(pen, 0, 0, 0, card.Height)
                                   End Using
                               End Sub
    End Sub

    Private Function GetNotificationColor(type As NotificationType) As Color
        Select Case type
            Case NotificationType.Critical
                Return ColorCritical
            Case NotificationType.Warning
                Return ColorWarning
            Case NotificationType.Info
                Return ColorInfo
            Case Else
                Return ColorInfo
        End Select
    End Function

    Private Sub AddCardControls(card As Panel, notification As NotificationEntry)
        ' Message principal
        Dim messageLabel = New Label With {
            .Text = notification.Message,
            .Font = New Font("Segoe UI", 11, FontStyle.Bold),
            .ForeColor = ColorTextPrimary,
            .AutoSize = True,
            .Location = New Point(15, 10),
            .BackColor = Color.Transparent
        }
        card.Controls.Add(messageLabel)

        ' Nom de l'appareil
        Dim deviceLabel = New Label With {
            .Text = notification.DeviceName,
            .Font = New Font("Segoe UI", 10),
            .ForeColor = ColorTextSecondary,
            .AutoSize = True,
            .Location = New Point(15, 35),
            .BackColor = Color.Transparent
        }
        card.Controls.Add(deviceLabel)

        ' Détails (pièce + temps)
        Dim detailsLabel = New Label With {
            .Text = BuildDetailsText(notification),
            .Font = New Font("Segoe UI", 9),
            .ForeColor = ColorTextTertiary,
            .AutoSize = True,
            .Location = New Point(15, 57),
            .BackColor = Color.Transparent
        }
        card.Controls.Add(detailsLabel)

        ' Badge "Non lu"
        If Not notification.IsRead Then
            Dim unreadBadge = New Label With {
                .Text = "●",
                .Font = New Font("Segoe UI", 20),
                .ForeColor = ColorInfo,
                .AutoSize = True,
                .Location = New Point(card.Width - 30, 30),
                .BackColor = Color.Transparent
            }
            card.Controls.Add(unreadBadge)
        End If
    End Sub

    Private Function BuildDetailsText(notification As NotificationEntry) As String
        Dim details = ""

        If Not String.IsNullOrEmpty(notification.RoomName) Then
            details = notification.RoomName & " • "
        End If

        details &= notification.GetTimeAgo()

        Return details
    End Function

    Private Sub AttachClickHandlers(card As Panel, notification As NotificationEntry)
        Dim clickHandler As EventHandler = Sub(s, e) HandleCardClick(notification)

        AddHandler card.Click, clickHandler

        ' Propager le clic des labels vers le panel
        For Each ctrl As Control In card.Controls
            If TypeOf ctrl Is Label Then
                AddHandler ctrl.Click, clickHandler
            End If
        Next
    End Sub

    Private Sub HandleCardClick(notification As NotificationEntry)
        If Not notification.IsRead Then
            _notificationManager.MarkAsRead(notification)
            LoadNotifications()

            ' ✅ IMPORTANT : Déclencher l'événement pour mettre à jour le badge
            RaiseEvent NotificationsChanged(Me, EventArgs.Empty)
        End If
    End Sub
#End Region

#Region "Gestionnaires d'événements"
    Private Sub MarkAllRead_Click(sender As Object, e As EventArgs)
        _notificationManager.MarkAllAsRead()
        LoadNotifications()

        ' ✅ IMPORTANT : Déclencher l'événement pour mettre à jour le badge
        RaiseEvent NotificationsChanged(Me, EventArgs.Empty)
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

            ' ✅ IMPORTANT : Déclencher l'événement pour mettre à jour le badge
            RaiseEvent NotificationsChanged(Me, EventArgs.Empty)
        End If
    End Sub
#End Region

#Region "Méthodes publiques"
    Public Sub ShowAtLocation(x As Integer, y As Integer)
        Me.StartPosition = FormStartPosition.Manual
        Me.Location = New Point(x, y)
        Me.Show()
        Me.BringToFront()
    End Sub
#End Region

End Class