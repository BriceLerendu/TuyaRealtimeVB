Imports System.IO
Imports System.Windows.Forms
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Public Class TuyaConfig
    Public Property Region As String
    Public Property OpenApiBase As String
    Public Property AccessId As String
    Public Property AccessSecret As String
    Public Property Uid As String

    ' Propri�t�s pour Python Bridge
    Public Property PythonScriptPath As String
    Public Property PythonFallbackPath As String

    ' Propri�t�s pour le logging
    Public Property ShowRawPayloads As Boolean

    ' Mode temps r�el (Python Bridge ou SDK Officiel Tuya .NET)
    Public Property RealtimeMode As RealtimeMode = RealtimeMode.DotNetPulsarOfficial ' Défaut: SDK officiel

    Public Shared Function Load() As TuyaConfig
        Dim configPath As String = Path.Combine(Application.StartupPath, "appsettings.json")

        If Not File.Exists(configPath) Then
            Throw New FileNotFoundException($"Fichier de configuration introuvable: {configPath}")
        End If

        Dim json As String = File.ReadAllText(configPath)
        Dim obj As JObject = JObject.Parse(json)

        Dim config As New TuyaConfig()

        ' Configuration Tuya
        Dim tuya = obj("Tuya")
        config.Region = tuya("Region")?.ToString()
        config.OpenApiBase = tuya("OpenApiBase")?.ToString()
        config.AccessId = tuya("AccessId")?.ToString()
        config.AccessSecret = tuya("AccessSecret")?.ToString()
        config.Uid = tuya("Uid")?.ToString()

        ' Configuration Python
        Dim python = obj("Python")
        If python IsNot Nothing Then
            config.PythonScriptPath = If(python("ScriptPath")?.ToString(), "tuya_bridge.py")
            config.PythonFallbackPath = If(python("FallbackPath")?.ToString(), "")
        Else
            config.PythonScriptPath = "tuya_bridge.py"
            config.PythonFallbackPath = ""
        End If

        ' Configuration Logging
        Dim logging = obj("Logging")
        If logging IsNot Nothing Then
            Dim rawPayloads = logging("ShowRawPayloads")
            config.ShowRawPayloads = If(rawPayloads IsNot Nothing, CBool(rawPayloads), False)
        Else
            config.ShowRawPayloads = True
        End If

        ' Configuration Realtime
        Dim realtime = obj("Realtime")
        If realtime IsNot Nothing Then
            Dim modeValue = realtime("Mode")?.ToString()
            If Not String.IsNullOrEmpty(modeValue) Then
                ' Parser le mode (PythonBridge ou DotNetPulsarOfficial)
                Select Case modeValue.ToLower()
                    Case "pythonbridge", "python"
                        config.RealtimeMode = RealtimeMode.PythonBridge
                    Case "dotnetpulsarofficial", "tuyasdk", "official", "dotnetpulsar", "pulsar", "dotnet"
                        config.RealtimeMode = RealtimeMode.DotNetPulsarOfficial
                    Case Else
                        config.RealtimeMode = RealtimeMode.DotNetPulsarOfficial ' Défaut: SDK officiel
                End Select
            End If
        Else
            config.RealtimeMode = RealtimeMode.DotNetPulsarOfficial ' Défaut: SDK officiel
        End If

        Return config
    End Function

    Public Sub Save()
        Dim configPath As String = Path.Combine(Application.StartupPath, "appsettings.json")

        Dim obj As New JObject()

        ' Section Tuya
        obj("Tuya") = New JObject(
            New JProperty("Region", Region),
            New JProperty("OpenApiBase", OpenApiBase),
            New JProperty("AccessId", AccessId),
            New JProperty("AccessSecret", AccessSecret),
            New JProperty("Uid", Uid)
        )

        ' Section Python
        obj("Python") = New JObject(
            New JProperty("ScriptPath", PythonScriptPath),
            New JProperty("FallbackPath", PythonFallbackPath)
        )

        ' Section Logging
        obj("Logging") = New JObject(
            New JProperty("ShowRawPayloads", ShowRawPayloads)
        )

        ' Section Realtime
        Dim modeString As String = If(RealtimeMode = RealtimeMode.PythonBridge, "PythonBridge", "DotNetPulsarOfficial")
        obj("Realtime") = New JObject(
            New JProperty("Mode", modeString)
        )

        ' Sauvegarder avec indentation
        Dim json As String = obj.ToString(Formatting.Indented)
        File.WriteAllText(configPath, json)
    End Sub

    ' M�thode pour obtenir le chemin du script Python
    Public Function GetPythonScriptPath() As String
        ' Essayer d'abord le chemin dans le r�pertoire de l'application
        Dim appPath As String = Path.Combine(Application.StartupPath, PythonScriptPath)
        If File.Exists(appPath) Then
            Return appPath
        End If

        ' Essayer le chemin relatif/absolu direct
        If File.Exists(PythonScriptPath) Then
            Return PythonScriptPath
        End If

        ' Essayer le chemin de fallback
        If Not String.IsNullOrEmpty(PythonFallbackPath) AndAlso File.Exists(PythonFallbackPath) Then
            Return PythonFallbackPath
        End If

        ' Aucun fichier trouv�
        Return Nothing
    End Function
End Class