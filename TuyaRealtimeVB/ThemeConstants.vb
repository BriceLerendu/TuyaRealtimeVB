Imports System.Drawing

''' <summary>
''' Constantes de thème et couleurs centralisées pour toute l'application
''' </summary>
Public Module ThemeConstants

#Region "Couleurs principales"
    Public ReadOnly DarkBg As Color = Color.FromArgb(45, 45, 48)
    Public ReadOnly LightBg As Color = Color.FromArgb(242, 242, 247)
    Public ReadOnly SecondaryBg As Color = Color.FromArgb(55, 55, 58)
    Public ReadOnly ActiveBlue As Color = Color.FromArgb(0, 122, 255)
    Public ReadOnly InactiveGray As Color = Color.FromArgb(142, 142, 147)
    Public ReadOnly CriticalRed As Color = Color.FromArgb(255, 59, 48)
    Public ReadOnly SuccessGreen As Color = Color.FromArgb(52, 199, 89)
    Public ReadOnly WarningOrange As Color = Color.FromArgb(255, 149, 0)
    Public ReadOnly InfoBlue As Color = Color.FromArgb(0, 122, 255)
    Public ReadOnly PurplePower As Color = Color.FromArgb(175, 82, 222)
#End Region

#Region "Couleurs DeviceCard"
    Public ReadOnly CardDefaultBackColor As Color = Color.FromArgb(250, 250, 252)
    Public ReadOnly CardDefaultBorderColor As Color = Color.FromArgb(230, 230, 235)
    Public ReadOnly CardDefaultFooterBackColor As Color = Color.FromArgb(248, 248, 250)

    Public ReadOnly CardOnlineBackColor As Color = Color.White
    Public ReadOnly CardOnlineBorderColor As Color = Color.FromArgb(52, 199, 89)
    Public ReadOnly CardOnlineFooterBackColor As Color = Color.FromArgb(240, 255, 245)

    Public ReadOnly CardOfflineBackColor As Color = Color.White
    Public ReadOnly CardOfflineBorderColor As Color = Color.FromArgb(255, 59, 48)
    Public ReadOnly CardOfflineFooterBackColor As Color = Color.FromArgb(255, 245, 245)

    Public ReadOnly CardFlashBackColor As Color = Color.FromArgb(230, 245, 255)
    Public ReadOnly CardFlashBorderColor As Color = Color.FromArgb(0, 122, 255)
#End Region

#Region "Couleurs de texte"
    Public ReadOnly TextPrimary As Color = Color.FromArgb(28, 28, 30)
    Public ReadOnly TextSecondary As Color = Color.FromArgb(99, 99, 102)
    Public ReadOnly TextTertiary As Color = Color.FromArgb(142, 142, 147)
    Public ReadOnly TextWhite As Color = Color.White
    Public ReadOnly TextLightGray As Color = Color.LightGray
#End Region

#Region "Couleurs spécifiques"
    Public ReadOnly RoomHeaderBg As Color = Color.FromArgb(60, 60, 65)
    Public ReadOnly DebugConsoleBg As Color = Color.FromArgb(20, 20, 20)
    Public ReadOnly DebugConsoleText As Color = Color.LightGray
    Public ReadOnly DebugTitleGreen As Color = Color.LightGreen
#End Region

#Region "Tailles et dimensions"
    Public Const CARD_WIDTH As Integer = 320
    Public Const CARD_HEIGHT As Integer = 260
    Public Const CARD_CORNER_RADIUS As Integer = 20
    Public Const CARD_FOOTER_HEIGHT As Integer = 35
    Public Const CARD_SHADOW_LAYERS As Integer = 4

    Public Const ROOM_HEADER_HEIGHT As Integer = 40
    Public Const HEADER_PANEL_HEIGHT As Integer = 80
    Public Const BOTTOM_PANEL_HEIGHT As Integer = 30
#End Region

#Region "Timings"
    Public Const RESIZE_TIMER_INTERVAL As Integer = 150
    Public Const FLASH_TIMER_INTERVAL As Integer = 300
    Public Const FLASH_COUNT As Integer = 6
    Public Const CARD_FLASH_INTERVAL As Integer = 200
    Public Const DEBOUNCE_INTERVAL As Integer = 100
#End Region

#Region "Limites"
    Public Const MAX_DEBUG_LINES As Integer = 10000
    Public Const LINES_TO_REMOVE As Integer = 1000
    Public Const MAX_CARD_PROPERTIES As Integer = 5
    Public Const API_CACHE_SECONDS As Integer = 30
    Public Const MIN_API_INTERVAL_MS As Integer = 100
#End Region

#Region "Fonts"
    Public Function GetTitleFont() As Font
        Return New Font("Segoe UI", 18, FontStyle.Bold)
    End Function

    Public Function GetHeaderFont() As Font
        Return New Font("Segoe UI", 12, FontStyle.Bold)
    End Function

    Public Function GetNormalFont() As Font
        Return New Font("Segoe UI", 10)
    End Function

    Public Function GetSmallFont() As Font
        Return New Font("Segoe UI", 9)
    End Function

    Public Function GetConsoleFont() As Font
        Return New Font("Consolas", 9)
    End Function

    Public Function GetEmojiFont() As Font
        Return New Font("Segoe UI Emoji", 22, FontStyle.Regular)
    End Function
#End Region

End Module
