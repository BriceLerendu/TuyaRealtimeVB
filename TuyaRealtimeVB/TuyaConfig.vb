Imports Newtonsoft.Json.Linq

Public Class TuyaConfig
    Public Property Region As String
    Public Property OpenApiBase As String
    Public Property MqttHost As String
    Public Property MqttPort As Integer
    Public Property AccessId As String
    Public Property AccessSecret As String
    Public Property ShowRawPayloads As Boolean

    Public Property Uid As String


    Public Shared Function Load() As TuyaConfig
        Dim json = IO.File.ReadAllText("appsettings.json")
        Dim root = JObject.Parse(json)
        Dim t = root("Tuya")
        Dim cfg As New TuyaConfig With {
        .Region = t.Value(Of String)("Region"),
        .OpenApiBase = t.Value(Of String)("OpenApiBase"),
        .MqttHost = t.Value(Of String)("MqttHost"),
        .MqttPort = t.Value(Of Integer)("MqttPort"),
        .AccessId = t.Value(Of String)("AccessId"),
        .AccessSecret = t.Value(Of String)("AccessSecret"),
        .ShowRawPayloads = If(root.SelectToken("Logging.ShowRawPayloads")?.Value(Of Boolean)(), False),
        .Uid = t.Value(Of String)("Uid")
    }
        Return cfg
    End Function

End Class