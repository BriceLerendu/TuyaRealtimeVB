Imports System
Imports System.Net.Http
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

''' <summary>
''' Service pour récupérer l'historique et les statistiques des appareils Tuya
''' </summary>
Public Class TuyaHistoryService
    Private ReadOnly _apiClient As TuyaApiClient
    Private ReadOnly _logCallback As Action(Of String)

    Public Sub New(apiClient As TuyaApiClient, Optional logCallback As Action(Of String) = Nothing)
        _apiClient = apiClient
        _logCallback = logCallback
    End Sub

    ''' <summary>
    ''' Récupère les statistiques d'un appareil sur une période en essayant plusieurs codes
    ''' </summary>
    Public Async Function GetDeviceStatisticsAsync(
        deviceId As String,
        period As HistoryPeriod,
        Optional code As String = "cur_power"
    ) As Task(Of DeviceStatistics)
        ' Essayer le code spécifié d'abord
        Dim stats = Await GetDeviceStatisticsForCodeAsync(deviceId, period, code)

        ' Si aucune donnée, essayer d'autres codes courants
        If stats Is Nothing OrElse stats.DataPoints.Count = 0 Then
            Log($"🔄 Tentative avec d'autres codes courants...")
            Dim alternativeCodes = {"add_ele", "cur_voltage", "cur_current", "switch_1"}

            For Each altCode In alternativeCodes
                If altCode <> code Then
                    stats = Await GetDeviceStatisticsForCodeAsync(deviceId, period, altCode)
                    If stats IsNot Nothing AndAlso stats.DataPoints.Count > 0 Then
                        Log($"✅ Données trouvées avec le code '{altCode}'!")
                        Exit For
                    End If
                End If
            Next
        End If

        Return stats
    End Function

    ''' <summary>
    ''' Récupère les statistiques pour un code spécifique
    ''' </summary>
    Private Async Function GetDeviceStatisticsForCodeAsync(
        deviceId As String,
        period As HistoryPeriod,
        code As String
    ) As Task(Of DeviceStatistics)

        Try
            Dim endDate = DateTime.Now.Date
            Dim startDate As DateTime

            ' Définir la période
            Select Case period
                Case HistoryPeriod.Last24Hours
                    startDate = endDate.AddDays(-1)
                Case HistoryPeriod.Last7Days
                    startDate = endDate.AddDays(-7)
                Case HistoryPeriod.Last30Days
                    startDate = endDate.AddDays(-30)
                Case Else
                    startDate = endDate.AddDays(-7)
            End Select

            ' Convertir en format yyyyMMdd (selon documentation Tuya)
            Dim startDay = startDate.ToString("yyyyMMdd")
            Dim endDay = endDate.ToString("yyyyMMdd")

            ' Endpoint API Tuya pour statistiques par jours
            Dim endpoint = $"/v1.0/devices/{deviceId}/statistics/days"
            Dim queryParams = $"?code={code}&start_day={startDay}&end_day={endDay}&stat_type=sum"

            Log($"Récupération statistiques: {deviceId}, période: {period} ({startDay} → {endDay})")

            ' Appel API
            Dim response = Await _apiClient.GetAsync(endpoint & queryParams)

            ' Log de la réponse complète pour débogage
            If response IsNot Nothing Then
                Log($"📥 Réponse API: {response.ToString(Formatting.None)}")
            Else
                Log($"❌ Réponse API est Nothing")
            End If

            If response IsNot Nothing AndAlso response("success")?.ToObject(Of Boolean)() = True Then
                Dim result = response("result")

                Dim stats As New DeviceStatistics With {
                    .DeviceId = deviceId,
                    .StatType = "sum",
                    .Code = code,
                    .Unit = "kWh",
                    .DataPoints = New List(Of StatisticPoint)
                }

                ' Parser les points de données
                ' Format réponse: { "result": { "days": { "20190101": "0.5", "20190102": "1.2" } } }
                If result IsNot Nothing Then
                    Dim daysData = TryCast(result("days"), JObject)
                    If daysData IsNot Nothing Then
                        For Each prop As JProperty In daysData.Properties()
                            Dim dateKey = prop.Name  ' Format: "yyyyMMdd"
                            Dim valueStr = prop.Value?.ToString()

                            If Not String.IsNullOrEmpty(dateKey) AndAlso Not String.IsNullOrEmpty(valueStr) Then
                                ' Parser la date (format yyyyMMdd)
                                Dim dt As DateTime
                                If DateTime.TryParseExact(dateKey, "yyyyMMdd", Nothing,
                                                         Globalization.DateTimeStyles.None, dt) Then
                                    Dim value As Double
                                    If Double.TryParse(valueStr, Globalization.NumberStyles.Any,
                                                      Globalization.CultureInfo.InvariantCulture, value) Then
                                        stats.DataPoints.Add(New StatisticPoint With {
                                            .Timestamp = dt,
                                            .Value = value,
                                            .Label = dt.ToString("dd/MM")
                                        })
                                    End If
                                End If
                            End If
                        Next
                    End If
                End If

                ' Trier par date
                stats.DataPoints.Sort(Function(a, b) a.Timestamp.CompareTo(b.Timestamp))

                If stats.DataPoints.Count = 0 Then
                    Log($"⚠️ 0 points de données pour le code '{code}'. L'appareil ne mesure peut-être pas cette donnée.")
                    Log($"💡 Essayez avec d'autres codes: add_ele, cur_voltage, cur_current")
                Else
                    Log($"✅ {stats.DataPoints.Count} points de données récupérés pour '{code}'")
                End If
                Return stats
            Else
                Dim errorMsg = If(response?("msg")?.ToString(), "Erreur inconnue")
                Log($"❌ Erreur API statistiques: {errorMsg}")
                Return Nothing
            End If

        Catch ex As Exception
            Log($"❌ Exception GetDeviceStatisticsAsync: {ex.Message}")
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Récupère les logs d'événements d'un appareil
    ''' </summary>
    Public Async Function GetDeviceLogsAsync(
        deviceId As String,
        period As HistoryPeriod
    ) As Task(Of List(Of DeviceLog))

        Try
            Dim endTime = DateTime.Now
            Dim startTime As DateTime

            ' Définir la période
            Select Case period
                Case HistoryPeriod.Last24Hours
                    startTime = endTime.AddHours(-24)
                Case HistoryPeriod.Last7Days
                    startTime = endTime.AddDays(-7)
                Case HistoryPeriod.Last30Days
                    startTime = endTime.AddDays(-30)
                Case Else
                    startTime = endTime.AddDays(-7)
            End Select

            ' Convertir en timestamps Unix (millisecondes)
            Dim startTimestamp = CLng((startTime.ToUniversalTime() - New DateTime(1970, 1, 1)).TotalMilliseconds)
            Dim endTimestamp = CLng((endTime.ToUniversalTime() - New DateTime(1970, 1, 1)).TotalMilliseconds)

            ' Endpoint API Tuya pour logs
            Dim endpoint = $"/v1.0/devices/{deviceId}/logs"
            Dim queryParams = $"?start_time={startTimestamp}&end_time={endTimestamp}&size=100&type=7"

            Log($"Récupération logs: {deviceId}, période: {period}")

            ' Appel API
            Dim response = Await _apiClient.GetAsync(endpoint & queryParams)

            Dim logs As New List(Of DeviceLog)

            If response IsNot Nothing AndAlso response("success")?.ToObject(Of Boolean)() = True Then
                Dim result = response("result")

                ' Parser les logs
                If result IsNot Nothing AndAlso TypeOf result Is JArray Then
                    For Each item As JToken In CType(result, JArray)
                        Dim jItem = CType(item, JObject)
                        Dim timestamp = jItem("event_time")?.ToObject(Of Long)()
                        Dim code = jItem("code")?.ToString()
                        Dim value = jItem("value")?.ToString()
                        Dim eventId = jItem("event_id")?.ToString()

                        If timestamp.HasValue Then
                            Dim dt = DateTimeOffset.FromUnixTimeMilliseconds(timestamp.Value).LocalDateTime
                            Dim eventType = DetermineEventType(code, value)
                            Dim description = CreateEventDescription(code, value, eventType)

                            logs.Add(New DeviceLog With {
                                .EventTime = dt,
                                .Code = code,
                                .Value = value,
                                .EventType = eventType,
                                .Description = description
                            })
                        End If
                    Next
                End If

                Log($"✅ {logs.Count} logs récupérés")
            Else
                Dim errorMsg = If(response?("msg")?.ToString(), "Erreur inconnue")
                Log($"❌ Erreur API logs: {errorMsg}")
            End If

            ' Trier par date décroissante (plus récent en premier)
            logs.Sort(Function(a, b) b.EventTime.CompareTo(a.EventTime))

            Return logs

        Catch ex As Exception
            Log($"❌ Exception GetDeviceLogsAsync: {ex.Message}")
            Return New List(Of DeviceLog)
        End Try
    End Function

    ''' <summary>
    ''' Détermine le type d'événement à partir du code et de la valeur
    ''' </summary>
    Private Function DetermineEventType(code As String, value As String) As String
        If String.IsNullOrEmpty(code) Then Return "unknown"

        Select Case code.ToLower()
            Case "switch_led", "switch", "switch_1", "switch_2"
                Return If(value = "true" Or value = "1", "switch_on", "switch_off")
            Case "online"
                Return If(value = "true", "online", "offline")
            Case Else
                Return "status_change"
        End Select
    End Function

    ''' <summary>
    ''' Crée une description lisible pour un événement
    ''' </summary>
    Private Function CreateEventDescription(code As String, value As String, eventType As String) As String
        Select Case eventType
            Case "switch_on"
                Return "🟢 Allumé"
            Case "switch_off"
                Return "🔴 Éteint"
            Case "online"
                Return "✅ En ligne"
            Case "offline"
                Return "❌ Hors ligne"
            Case Else
                Return $"{code} = {value}"
        End Select
    End Function

    ''' <summary>
    ''' Log un message
    ''' </summary>
    Private Sub Log(message As String)
        _logCallback?.Invoke($"[HistoryService] {message}")
    End Sub
End Class
