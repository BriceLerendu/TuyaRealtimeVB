Imports Newtonsoft.Json.Linq

''' <summary>
''' Interface commune pour les clients temps réel Tuya (Python Bridge ou .NET Pulsar)
''' </summary>
Public Interface ITuyaRealtimeClient
    ''' <summary>
    ''' Événement déclenché lorsqu'un appareil change d'état
    ''' </summary>
    Event DeviceStatusChanged(deviceId As String, statusData As JObject)

    ''' <summary>
    ''' Démarre la connexion temps réel de manière asynchrone
    ''' </summary>
    ''' <returns>True si la connexion a réussi, False sinon</returns>
    Function StartAsync() As Task(Of Boolean)

    ''' <summary>
    ''' Arrête la connexion temps réel
    ''' </summary>
    Sub [Stop]()

    ''' <summary>
    ''' Vérifie si le client est en cours d'exécution
    ''' </summary>
    Function IsRunning() As Boolean

    ''' <summary>
    ''' Retourne le mode de connexion utilisé
    ''' </summary>
    ReadOnly Property Mode As RealtimeMode
End Interface

''' <summary>
''' Modes de connexion temps réel disponibles
''' </summary>
Public Enum RealtimeMode
    ''' <summary>
    ''' Mode script Python externe utilisant tuya_connector
    ''' ✅ Fonctionne, testé et validé
    ''' </summary>
    PythonBridge = 0

    ''' <summary>
    ''' Mode SDK officiel Tuya en .NET - RECOMMANDÉ
    ''' ✅ Authentification propriétaire Tuya gérée automatiquement
    ''' ✅ Basé sur github.com/tuya/tuya-pulsar-sdk-dotnet
    ''' ✅ Pas de dépendance Python
    ''' </summary>
    DotNetPulsarOfficial = 1
End Enum
