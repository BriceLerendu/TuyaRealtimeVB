Imports System.IO
Imports System.Windows.Forms
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Public Class TuyaConfig
    Public Property Region As String
    Public Property OpenApiBase As String
    Public Property MqttHost As String
    Public Property MqttPort As Integer
    Public Property AccessId As String
    Public Property AccessSecret As String
    Public Property Uid As String

    ' Nouvelles propriétés pour Python
    Public Property PythonScriptPath As String
    Public Property PythonFallbackPath As String

    ' Propriétés pour le logging
    Public Property ShowRawPayloads As Boolean

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
        config.MqttHost = tuya("MqttHost")?.ToString()
        config.MqttPort = CInt(tuya("MqttPort"))
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

        Return config
    End Function

    Public Sub Save()
        Dim configPath As String = Path.Combine(Application.StartupPath, "appsettings.json")

        Dim obj As New JObject()

        ' Section Tuya
        obj("Tuya") = New JObject(
            New JProperty("Region", Region),
            New JProperty("OpenApiBase", OpenApiBase),
            New JProperty("MqttHost", MqttHost),
            New JProperty("MqttPort", MqttPort),
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

        ' Sauvegarder avec indentation
        Dim json As String = obj.ToString(Formatting.Indented)
        File.WriteAllText(configPath, json)
    End Sub

    ' Méthode pour obtenir le chemin du script Python
    Public Function GetPythonScriptPath() As String
        ' Essayer d'abord le chemin dans le répertoire de l'application
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

        ' Aucun fichier trouvé
        Return Nothing
    End Function
End Class