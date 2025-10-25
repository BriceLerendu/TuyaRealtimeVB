Imports System
Imports System.Net.Http
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
    ''' Récupère les statistiques d'un appareil sur une période
    ''' </summary>
    ''' <param name="deviceId">ID de l'appareil</param>
    ''' <param name="period">Période de temps à récupérer</param>
    ''' <param name="code">Code de la propriété (ex: "cur_power", "va_temperature")</param>
    ''' <param name="category">Catégorie de l'appareil pour déterminer l'unité</param>
    Public Async Function GetDeviceStatisticsAsync(
        deviceId As String,
        period As HistoryPeriod,
        code As String,
        category As String
    ) As Task(Of DeviceStatistics)

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

            ' Endpoint API Tuya pour statistiques
            Dim endpoint = $"/v1.0/devices/{deviceId}/statistics/days"
            Dim queryParams = $"?code={code}&start_time={startTimestamp}&end_time={endTimestamp}&type=sum"

            Log($"Récupération statistiques: {deviceId}, code: {code}, période: {period}")

            ' Appel API
            Dim response = Await _apiClient.ExecuteGetRequestAsync(endpoint & queryParams)

            If response IsNot Nothing AndAlso response("success")?.ToObject(Of Boolean)() = True Then
                Dim result = response("result")

                ' Obtenir l'unité depuis le TuyaCategoryManager
                Dim unit = TuyaCategoryManager.Instance.GetPropertyUnit(category, code)
                If String.IsNullOrEmpty(unit) Then
                    ' Unités par défaut selon le code
                    unit = GetDefaultUnit(code)
                End If

                Dim stats As New DeviceStatistics With {
                    .DeviceId = deviceId,
                    .StatType = "sum",
                    .Code = code,
                    .Unit = unit,
                    .DataPoints = New List(Of StatisticPoint)
                }

                ' Parser les points de données
                If result IsNot Nothing AndAlso TypeOf result Is JArray Then
                    For Each item As JToken In CType(result, JArray)
                        Dim jItem = CType(item, JObject)
                        Dim timestamp = jItem("time")?.ToObject(Of Long)()
                        Dim value = jItem("value")?.ToString()

                        If timestamp.HasValue AndAlso Not String.IsNullOrEmpty(value) Then
                            Dim dt = DateTimeOffset.FromUnixTimeMilliseconds(timestamp.Value).LocalDateTime

                            ' Convertir la valeur en tenant compte du diviseur
                            Dim numValue As Double = 0
                            If Double.TryParse(value, numValue) Then
                                ' Appliquer la conversion depuis la config
                                numValue = ApplyPropertyConversion(category, code, numValue)
                            End If

                            stats.DataPoints.Add(New StatisticPoint With {
                                .Timestamp = dt,
                                .Value = numValue,
                                .Label = dt.ToString("dd/MM")
                            })
                        End If
                    Next
                End If

                Log($"✅ {stats.DataPoints.Count} points de données récupérés")
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
    ''' Applique la conversion d'une propriété (ex: diviseur)
    ''' </summary>
    Private Function ApplyPropertyConversion(category As String, code As String, value As Double) As Double
        Try
            Dim propertyConfig = TuyaCategoryManager.Instance.GetConfiguration()("categories")?(category)?("properties")?(code)

            If propertyConfig IsNot Nothing Then
                Dim conversion = propertyConfig("conversion")?.ToString()

                Select Case conversion
                    Case "divide"
                        Dim divisor = propertyConfig("divisor")?.ToObject(Of Double)()
                        If divisor.HasValue AndAlso divisor.Value > 0 Then
                            Return value / divisor.Value
                        End If
                    Case "multiply"
                        Dim multiplier = propertyConfig("multiplier")?.ToObject(Of Double)()
                        If multiplier.HasValue Then
                            Return value * multiplier.Value
                        End If
                End Select
            End If

        Catch ex As Exception
            Debug.WriteLine($"Erreur ApplyPropertyConversion: {ex.Message}")
        End Try

        Return value
    End Function

    ''' <summary>
    ''' Obtient une unité par défaut selon le code de propriété
    ''' </summary>
    Private Function GetDefaultUnit(code As String) As String
        Select Case code.ToLower()
            Case "cur_power"
                Return "W"
            Case "cur_voltage"
                Return "V"
            Case "cur_current"
                Return "A"
            Case "add_ele"
                Return "kWh"
            Case "va_temperature", "temp_current", "temp_set"
                Return "°C"
            Case "humidity_value", "humidity", "battery_percentage"
                Return "%"
            Case "switch", "doorcontact_state", "pir"
                Return "état"
            Case Else
                Return "valeur"
        End Select
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
            Dim response = Await _apiClient.ExecuteGetRequestAsync(endpoint & queryParams)

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
