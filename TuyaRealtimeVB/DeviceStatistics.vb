Imports System

''' <summary>
''' Statistiques d'un appareil sur une période donnée
''' </summary>
Public Class DeviceStatistics
    ''' <summary>
    ''' ID de l'appareil
    ''' </summary>
    Public Property DeviceId As String

    ''' <summary>
    ''' Type de statistique: "sum", "avg", "min", "max"
    ''' </summary>
    Public Property StatType As String

    ''' <summary>
    ''' Points de données (timestamp + valeur)
    ''' </summary>
    Public Property DataPoints As New List(Of StatisticPoint)

    ''' <summary>
    ''' Unité de mesure (ex: "kWh", "hours", "°C")
    ''' </summary>
    Public Property Unit As String

    ''' <summary>
    ''' Code du DP (data point) Tuya
    ''' </summary>
    Public Property Code As String
End Class

''' <summary>
''' Point de donnée statistique à un instant T
''' </summary>
Public Class StatisticPoint
    ''' <summary>
    ''' Horodatage du point
    ''' </summary>
    Public Property Timestamp As DateTime

    ''' <summary>
    ''' Valeur mesurée
    ''' </summary>
    Public Property Value As Double

    ''' <summary>
    ''' Label pour affichage (ex: "Lun 12:00")
    ''' </summary>
    Public Property Label As String
End Class

''' <summary>
''' Événement d'un appareil (log)
''' </summary>
Public Class DeviceLog
    ''' <summary>
    ''' Horodatage de l'événement
    ''' </summary>
    Public Property EventTime As DateTime

    ''' <summary>
    ''' Type d'événement: "online", "offline", "switch_on", "switch_off", etc.
    ''' </summary>
    Public Property EventType As String

    ''' <summary>
    ''' Code du DP concerné
    ''' </summary>
    Public Property Code As String

    ''' <summary>
    ''' Valeur de l'événement (ex: "true", "false", "100")
    ''' </summary>
    Public Property Value As String

    ''' <summary>
    ''' Description lisible de l'événement
    ''' </summary>
    Public Property Description As String
End Class

''' <summary>
''' Période pour les requêtes d'historique
''' </summary>
Public Enum HistoryPeriod
    ''' <summary>Dernières 24 heures</summary>
    Last24Hours = 0

    ''' <summary>Derniers 7 jours</summary>
    Last7Days = 1

    ''' <summary>Derniers 30 jours</summary>
    Last30Days = 2
End Enum
