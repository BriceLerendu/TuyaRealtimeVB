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

            ' Pour Last24Hours, utiliser /statistics/hours avec découpage
            If period = HistoryPeriod.Last24Hours Then
                stats = Await GetHourlyStatisticsAsync(deviceId, code, category, unit)
            Else
                ' Pour 7 jours et 30 jours, utiliser /statistics/days (pas de découpage nécessaire)
                stats = Await GetDailyStatisticsAsync(deviceId, period, code, category, unit)
            End If

            Log($"✅ {stats.DataPoints.Count} points de données récupérés")
            Return stats

        Catch ex As Exception
            Log($"❌ Exception GetDeviceStatisticsAsync: {ex.Message}")
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Récupère les statistiques pour les dernières 24h
    ''' Note: L'API /statistics/days retourne automatiquement des données avec granularité horaire pour des périodes courtes
    ''' </summary>
    Private Async Function GetHourlyStatisticsAsync(
        deviceId As String,
        code As String,
        category As String,
        unit As String
    ) As Task(Of DeviceStatistics)

        Dim stats As New DeviceStatistics With {
            .DeviceId = deviceId,
            .StatType = "sum",
            .Code = code,
            .Unit = unit,
            .DataPoints = New List(Of StatisticPoint)
        }

        Try
            ' Pour les 24 dernières heures, on demande aujourd'hui et hier
            Dim endDate = DateTime.Now.Date
            Dim startDate = endDate.AddDays(-1)

            ' Convertir en format yyyyMMdd
            Dim startDay = startDate.ToString("yyyyMMdd")
            Dim endDay = endDate.ToString("yyyyMMdd")

            ' Endpoint API Tuya pour statistiques (granularité automatique selon la période)
            Dim endpoint = $"/v1.0/devices/{deviceId}/statistics/days"
            Dim queryParams = $"?code={code}&start_day={startDay}&end_day={endDay}&stat_type=sum"

            Log($"Récupération statistiques 24h: {deviceId}, code: {code} ({startDay} → {endDay})")

            ' Appel API
            Dim response = Await _apiClient.GetAsync(endpoint & queryParams)

            If response IsNot Nothing AndAlso response("success")?.ToObject(Of Boolean)() = True Then
                Dim result = response("result")

                ' Parser les points de données
                ' Format: { "result": { "days": { "20250101": "value1", "20250102": "value2" } } }
                If result IsNot Nothing Then
                    Dim daysData = TryCast(result("days"), JObject)
                    If daysData IsNot Nothing Then
                        For Each prop As JProperty In daysData.Properties()
                            Dim dateKey = prop.Name  ' Format: "yyyyMMdd" ou potentiellement "yyyyMMddHH" pour horaire
                            Dim valueStr = prop.Value?.ToString()

                            If Not String.IsNullOrEmpty(dateKey) AndAlso Not String.IsNullOrEmpty(valueStr) Then
                                ' Essayer de parser comme date complète (yyyyMMdd)
                                Dim dt As DateTime
                                If DateTime.TryParseExact(dateKey, "yyyyMMdd", Nothing,
                                                         Globalization.DateTimeStyles.None, dt) Then
                                    Dim numValue As Double
                                    If Double.TryParse(valueStr, Globalization.NumberStyles.Any,
                                                      Globalization.CultureInfo.InvariantCulture, numValue) Then
                                        ' Appliquer la conversion depuis la config
                                        numValue = ApplyPropertyConversion(category, code, numValue)

                                        stats.DataPoints.Add(New StatisticPoint With {
                                            .Timestamp = dt,
                                            .Value = numValue,
                                            .Label = dt.ToString("dd/MM HH:mm")
                                        })
                                    End If
                                End If
                            End If
                        Next
                    End If
                End If

                ' Trier par timestamp
                stats.DataPoints = stats.DataPoints.OrderBy(Function(p) p.Timestamp).ToList()

                If stats.DataPoints.Count = 0 Then
                    Log($"⚠️ 0 points de données pour le code '{code}' sur 24h")
                End If
            Else
                Dim errorMsg = If(response?("msg")?.ToString(), "Erreur inconnue")
                Log($"❌ Erreur API statistiques 24h: {errorMsg}")
            End If

        Catch ex As Exception
            Log($"❌ Exception GetHourlyStatisticsAsync: {ex.Message}")
        End Try

        Return stats
    End Function

    ''' <summary>
    ''' Récupère les statistiques journalières pour 7 ou 30 jours
    ''' </summary>
    Private Async Function GetDailyStatisticsAsync(
        deviceId As String,
        period As HistoryPeriod,
        code As String,
        category As String,
        unit As String
    ) As Task(Of DeviceStatistics)

        Dim stats As New DeviceStatistics With {
            .DeviceId = deviceId,
            .StatType = "sum",
            .Code = code,
            .Unit = unit,
            .DataPoints = New List(Of StatisticPoint)
        }

        Try
            Dim endDate = DateTime.Now.Date
            Dim startDate As DateTime

            ' Définir la période
            Select Case period
                Case HistoryPeriod.Last7Days
                    startDate = endDate.AddDays(-7)
                Case HistoryPeriod.Last30Days
                    startDate = endDate.AddDays(-30)
                Case Else
                    startDate = endDate.AddDays(-7)
            End Select

            ' Convertir en format yyyyMMdd (format requis par l'API Tuya)
            Dim startDay = startDate.ToString("yyyyMMdd")
            Dim endDay = endDate.ToString("yyyyMMdd")

            ' Endpoint API Tuya pour statistiques journalières
            Dim endpoint = $"/v1.0/devices/{deviceId}/statistics/days"
            Dim queryParams = $"?code={code}&start_day={startDay}&end_day={endDay}&stat_type=sum"

            Log($"Récupération statistiques journalières: {deviceId}, code: {code}, période: {period} ({startDay} → {endDay})")

            ' Appel API
            Dim response = Await _apiClient.GetAsync(endpoint & queryParams)

            If response IsNot Nothing AndAlso response("success")?.ToObject(Of Boolean)() = True Then
                Dim result = response("result")

                ' Parser les points de données
                ' Format: { "result": { "days": { "20250101": "value1", "20250102": "value2" } } }
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
                                    Dim numValue As Double
                                    If Double.TryParse(valueStr, Globalization.NumberStyles.Any,
                                                      Globalization.CultureInfo.InvariantCulture, numValue) Then
                                        ' Appliquer la conversion depuis la config
                                        numValue = ApplyPropertyConversion(category, code, numValue)

                                        stats.DataPoints.Add(New StatisticPoint With {
                                            .Timestamp = dt,
                                            .Value = numValue,
                                            .Label = dt.ToString("dd/MM")  ' Format journalier
                                        })
                                    End If
                                End If
                            End If
                        Next
                    End If
                End If

                ' Trier par date
                stats.DataPoints = stats.DataPoints.OrderBy(Function(p) p.Timestamp).ToList()

                If stats.DataPoints.Count = 0 Then
                    Log($"⚠️ 0 points de données pour le code '{code}'. L'appareil ne mesure peut-être pas cette donnée.")
                End If
            Else
                Dim errorMsg = If(response?("msg")?.ToString(), "Erreur inconnue")
                Log($"❌ Erreur API statistiques: {errorMsg}")
            End If

        Catch ex As Exception
            Log($"❌ Exception GetDailyStatisticsAsync: {ex.Message}")
        End Try

        Return stats
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
