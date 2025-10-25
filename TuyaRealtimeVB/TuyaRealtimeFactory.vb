Imports System.IO

''' <summary>
''' Factory pour créer le client temps réel approprié selon la configuration
''' </summary>
Public Class TuyaRealtimeFactory
    ''' <summary>
    ''' Crée une instance du client temps réel selon le mode configuré
    ''' </summary>
    ''' <param name="config">Configuration Tuya contenant le mode et les credentials</param>
    ''' <param name="logCallback">Callback pour les logs</param>
    ''' <returns>Instance du client temps réel (Python Bridge ou SDK Officiel Tuya .NET)</returns>
    Public Shared Function CreateClient(config As TuyaConfig, logCallback As Action(Of String)) As ITuyaRealtimeClient
        Select Case config.RealtimeMode
            Case RealtimeMode.PythonBridge
                ' Mode script Python externe avec tuya_connector
                Dim scriptPath = config.GetPythonScriptPath()
                If String.IsNullOrEmpty(scriptPath) Then
                    Throw New FileNotFoundException("Script Python introuvable. Vérifiez la configuration.")
                End If
                logCallback?.Invoke($"[Factory] Mode Python Bridge - Script: {scriptPath}")
                Return New PythonBridge(scriptPath, logCallback)

            Case RealtimeMode.DotNetPulsarOfficial
                ' Mode SDK officiel Tuya en .NET - RECOMMANDÉ
                logCallback?.Invoke("[Factory] Mode SDK officiel Tuya .NET")
                Return New TuyaPulsarOfficialClient(config, logCallback)

            Case Else
                Throw New ArgumentException($"Mode temps réel non supporté: {config.RealtimeMode}")
        End Select
    End Function

    ''' <summary>
    ''' Retourne le nom convivial du mode
    ''' </summary>
    Public Shared Function GetModeName(mode As RealtimeMode) As String
        Select Case mode
            Case RealtimeMode.PythonBridge
                Return "Python Bridge (Script)"
            Case RealtimeMode.DotNetPulsarOfficial
                Return "SDK Officiel Tuya .NET"
            Case Else
                Return "Inconnu"
        End Select
    End Function

    ''' <summary>
    ''' Retourne la description du mode
    ''' </summary>
    Public Shared Function GetModeDescription(mode As RealtimeMode) As String
        Select Case mode
            Case RealtimeMode.PythonBridge
                Return "Lance un script Python externe pour écouter Tuya Pulsar. Nécessite Python installé."
            Case RealtimeMode.DotNetPulsarOfficial
                Return "Utilise le SDK officiel Tuya en .NET. Authentification propriétaire gérée automatiquement. ✅ Recommandé."
            Case Else
                Return String.Empty
        End Select
    End Function
End Class
