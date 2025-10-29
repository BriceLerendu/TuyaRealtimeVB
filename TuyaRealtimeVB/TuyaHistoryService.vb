Imports System
Imports System.Net.Http
Imports Newtonsoft.Json.Linq

''' <summary>
''' Service optimisé pour récupérer l'historique et les statistiques des appareils Tuya
''' Version complète avec cache local et limitation des appels API
''' </summary>
Public Class TuyaHistoryService
    Private ReadOnly _apiClient As TuyaApiClient
    Private ReadOnly _logCallback As Action(Of String)

    ' Cache local pour éviter les appels API répétés
    Private ReadOnly _statisticsCache As New Dictionary(Of String, CachedStatistics)
    Private ReadOnly _logsCache As New Dictionary(Of String, CachedLogs)
    Private Const CACHE_TTL_MINUTES As Integer = 5

    ' Codes DP à essayer par ordre de priorité (réduit à 3 codes les plus courants)
    ' Énergie/Puissance
    Private ReadOnly _electricityCodesPriority As String() = {
        "cur_power",             ' Puissance actuelle (le plus courant)
        "add_ele",               ' Énergie cumulée
        "switch_1"               ' État switch (pour événements on/off)
    }

    Public Sub New(apiClient As TuyaApiClient, Optional logCallback As Action(Of String) = Nothing)
        _apiClient = apiClient
        _logCallback = logCallback
    End Sub

#Region "Statistiques avec cache et optimisation"

    ''' <summary>
    ''' Récupère la liste de tous les codes DP (data points) disponibles pour un appareil
    ''' ✅ MODIFIÉ: Explose les propriétés JSON en sous-propriétés
    ''' </summary>
    Public Async Function GetAvailableCodesAsync(
        deviceId As String,
        period As HistoryPeriod
    ) As Task(Of List(Of String))

        Try
            ' Récupérer les logs pour analyser les codes disponibles
            Dim logs = Await GetDeviceLogsAsync(deviceId, period)

            If logs Is Nothing OrElse logs.Count = 0 Then
                Log($"⚠️ Aucun log disponible pour détecter les codes DP")
                Return New List(Of String)
            End If

            ' Extraire tous les codes DP uniques des logs
            Dim parentCodes = logs.Where(Function(l) Not String.IsNullOrEmpty(l.Code)) _
                                 .Select(Function(l) l.Code) _
                                 .Distinct() _
                                 .ToList()

            ' ✅ NOUVEAU: Exploser les propriétés JSON en sous-propriétés
            Dim allCodes As New HashSet(Of String)

            For Each parentCode In parentCodes
                ' Ajouter le code parent
                allCodes.Add(parentCode)

                ' Chercher des valeurs JSON pour ce code
                Dim logsForCode = logs.Where(Function(l) l.Code = parentCode).Take(5).ToList()

                For Each logEntry In logsForCode
                    ' Détecter si la valeur est du JSON
                    If IsJsonValue(logEntry.Value) Then
                        Try
                            Dim jsonObj = JObject.Parse(logEntry.Value)

                            ' Ajouter chaque sous-propriété
                            For Each prop In jsonObj.Properties()
                                Dim subPropertyCode = $"{parentCode}.{prop.Name}"
                                allCodes.Add(subPropertyCode)
                            Next

                            ' Une fois qu'on a trouvé du JSON, pas besoin de continuer
                            Exit For
                        Catch ex As Exception
                            ' Pas du JSON valide, ignorer
                        End Try
                    End If
                Next
            Next

            Dim sortedCodes = allCodes.OrderBy(Function(c) c).ToList()
            Log($"🔍 Codes DP disponibles (avec sous-propriétés): {String.Join(", ", sortedCodes)}")

            Return sortedCodes

        Catch ex As Exception
            Log($"❌ Exception GetAvailableCodesAsync: {ex.Message}")
            Return New List(Of String)
        End Try
    End Function

    ''' <summary>
    ''' Vérifie si une valeur est du JSON
    ''' </summary>
    Private Function IsJsonValue(value As String) As Boolean
        If String.IsNullOrWhiteSpace(value) Then Return False
        Dim trimmed = value.Trim()
        Return (trimmed.StartsWith("{") AndAlso trimmed.EndsWith("}")) OrElse
               (trimmed.StartsWith("[") AndAlso trimmed.EndsWith("]"))
    End Function

    ''' <summary>
    ''' Récupère les statistiques d'un appareil avec cache et stratégie multi-codes intelligente
    ''' OPTIMISÉ: Arrêt dès qu'on trouve des données, cache local, limitation des appels API
    ''' </summary>
    Public Async Function GetDeviceStatisticsAsync(
        deviceId As String,
        period As HistoryPeriod,
        Optional specificCode As String = Nothing
    ) As Task(Of DeviceStatistics)

        Try
            ' Adapter la clé de cache si un code spécifique est demandé
            Dim cacheKey = If(String.IsNullOrEmpty(specificCode),
                            $"{deviceId}_{period}",
                            $"{deviceId}_{period}_{specificCode}")

            ' Vérifier le cache d'abord
            If _statisticsCache.ContainsKey(cacheKey) Then
                Dim cached = _statisticsCache(cacheKey)
                If (DateTime.Now - cached.Timestamp).TotalMinutes < CACHE_TTL_MINUTES Then
                    Log($"📦 Cache hit pour {deviceId} ({period})")
                    Return cached.Data
                Else
                    ' Cache expiré, le retirer
                    _statisticsCache.Remove(cacheKey)
                    Log($"🕐 Cache expiré pour {deviceId} ({period})")
                End If
            End If

            ' Si un code spécifique est demandé, l'essayer directement
            If Not String.IsNullOrEmpty(specificCode) Then
                Log($"🎯 Récupération des statistiques pour code spécifique '{specificCode}'...")

                Dim stats = Await GetDeviceStatisticsForCodeAsync(deviceId, period, specificCode)

                If stats IsNot Nothing AndAlso stats.DataPoints.Count > 0 Then
                    Log($"✅ Données trouvées avec '{specificCode}' ({stats.DataPoints.Count} points)")

                    ' Mettre en cache
                    _statisticsCache(cacheKey) = New CachedStatistics With {
                        .Data = stats,
                        .Timestamp = DateTime.Now
                    }

                    Return stats
                Else
                    Log($"⚠️ Aucune donnée disponible pour le code '{specificCode}'")
                    Return Nothing
                End If
            End If

            ' Si aucun code spécifique, essayer les codes par ordre de priorité (arrêt dès succès)
            For Each code In _electricityCodesPriority
                Log($"🔍 Essai code '{code}' pour {deviceId}...")

                Dim stats = Await GetDeviceStatisticsForCodeAsync(deviceId, period, code)

                If stats IsNot Nothing AndAlso stats.DataPoints.Count > 0 Then
                    Log($"✅ Données trouvées avec '{code}' ({stats.DataPoints.Count} points)")

                    ' Mettre en cache
                    _statisticsCache(cacheKey) = New CachedStatistics With {
                        .Data = stats,
                        .Timestamp = DateTime.Now
                    }

                    Return stats
                End If

                Log($"⚠️ Aucune donnée avec '{code}'")
            Next

            ' ✅ NOUVEAU : Si aucun code prioritaire ne fonctionne, détecter automatiquement les codes disponibles
            Log($"💡 Détection automatique des codes DP disponibles...")
            Dim autoStats = Await AutoDetectAndGetStatisticsAsync(deviceId, period)

            If autoStats IsNot Nothing AndAlso autoStats.DataPoints.Count > 0 Then
                Log($"✅ Statistiques créées automatiquement avec code '{autoStats.Code}' ({autoStats.DataPoints.Count} points)")

                ' Mettre en cache
                _statisticsCache(cacheKey) = New CachedStatistics With {
                    .Data = autoStats,
                    .Timestamp = DateTime.Now
                }

                Return autoStats
            End If

            Log($"❌ Aucune donnée trouvée pour {deviceId} avec tous les codes testés")
            Return Nothing

        Catch ex As Exception
            Log($"❌ Exception GetDeviceStatisticsAsync: {ex.Message}")
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Récupère les statistiques pour un code spécifique
    ''' MODIFIÉ: Calcule les stats depuis les logs au lieu d'utiliser l'API Statistics (non disponible)
    ''' </summary>
    Private Async Function GetDeviceStatisticsForCodeAsync(
        deviceId As String,
        period As HistoryPeriod,
        code As String
    ) As Task(Of DeviceStatistics)

        Try
            ' ✅ NOUVELLE APPROCHE: Utiliser les logs pour calculer les statistiques
            ' L'API /v1.0/devices/{id}/statistics/days nécessite une configuration manuelle par Tuya
            ' On calcule les stats depuis les logs qui fonctionnent déjà !

            Log($"  📊 Calcul des statistiques depuis les logs pour '{code}'...")

            ' Récupérer les logs pour la période
            Dim logs = Await GetDeviceLogsAsync(deviceId, period)

            If logs Is Nothing OrElse logs.Count = 0 Then
                Log($"  ⚠️ Aucun log disponible pour calculer les statistiques")
                Return Nothing
            End If

            ' Calculer les statistiques depuis les logs
            Dim stats = CalculateStatisticsFromLogs(deviceId, code, logs, period)

            If stats IsNot Nothing AndAlso stats.DataPoints.Count > 0 Then
                Log($"  ✅ {stats.DataPoints.Count} points de statistiques calculés depuis {logs.Count} logs")
            Else
                Log($"  ⚠️ Aucune donnée '{code}' trouvée dans les logs")
            End If

            Return stats

        Catch ex As Exception
            Log($"  ❌ Exception pour code '{code}': {ex.Message}")
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Détecte automatiquement les codes DP disponibles et retourne des statistiques pour le premier code valide
    ''' Utilisé quand aucun des codes prioritaires (cur_power, add_ele, switch_1) ne fonctionne
    ''' </summary>
    Private Async Function AutoDetectAndGetStatisticsAsync(
        deviceId As String,
        period As HistoryPeriod
    ) As Task(Of DeviceStatistics)

        Try
            ' Récupérer les logs pour analyser les codes disponibles
            Dim logs = Await GetDeviceLogsAsync(deviceId, period)

            If logs Is Nothing OrElse logs.Count = 0 Then
                Log($"  ⚠️ Aucun log disponible pour la détection automatique")
                Return Nothing
            End If

            ' Extraire tous les codes DP uniques des logs
            Dim availableCodes = logs.Where(Function(l) Not String.IsNullOrEmpty(l.Code)) _
                                    .Select(Function(l) l.Code) _
                                    .Distinct() _
                                    .ToList()

            Log($"  🔍 Codes DP disponibles détectés: {String.Join(", ", availableCodes)}")

            If availableCodes.Count = 0 Then
                Log($"  ⚠️ Aucun code DP trouvé dans les logs")
                Return Nothing
            End If

            ' Ordre de priorité pour les types de capteurs
            Dim priorityPatterns As String() = {
                "temperature", "temp",           ' Température en priorité
                "humidity", "hum",               ' Humidité
                "power", "current", "voltage",   ' Électrique
                "battery",                       ' Batterie
                "bright", "lux"                  ' Luminosité
            }

            ' Trier les codes selon la priorité
            Dim sortedCodes As New List(Of String)

            ' D'abord ajouter les codes qui matchent les patterns prioritaires
            For Each pattern In priorityPatterns
                For Each code In availableCodes
                    If code.ToLower().Contains(pattern) AndAlso Not sortedCodes.Contains(code) Then
                        sortedCodes.Add(code)
                    End If
                Next
            Next

            ' Puis ajouter les codes restants
            For Each code In availableCodes
                If Not sortedCodes.Contains(code) Then
                    sortedCodes.Add(code)
                End If
            Next

            ' Essayer chaque code jusqu'à trouver des données valides
            For Each code In sortedCodes
                Log($"  🔬 Test du code auto-détecté: '{code}'...")

                Dim stats = CalculateStatisticsFromLogs(deviceId, code, logs, period)

                If stats IsNot Nothing AndAlso stats.DataPoints.Count > 0 Then
                    Log($"  ✅ Statistiques créées avec code auto-détecté '{code}' ({stats.DataPoints.Count} points)")
                    Return stats
                End If
            Next

            Log($"  ⚠️ Aucun code valide trouvé parmi: {String.Join(", ", sortedCodes)}")
            Return Nothing

        Catch ex As Exception
            Log($"  ❌ Exception AutoDetectAndGetStatisticsAsync: {ex.Message}")
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Calcule les statistiques à partir des logs récupérés
    ''' ✅ MODIFIÉ: Supporte les sous-propriétés explosées (ex: phase_a.power)
    ''' </summary>
    Private Function CalculateStatisticsFromLogs(
        deviceId As String,
        code As String,
        logs As List(Of DeviceLog),
        period As HistoryPeriod
    ) As DeviceStatistics

        Try
            ' 🔍 DIAGNOSTIC: Afficher tous les codes DP présents dans les logs
            Dim allCodes = logs.Where(Function(l) Not String.IsNullOrEmpty(l.Code)) _
                              .Select(Function(l) l.Code) _
                              .Distinct() _
                              .ToList()
            Log($"  🔍 Codes DP trouvés dans les logs: {String.Join(", ", allCodes)}")

            ' ✅ NOUVEAU: Détecter si c'est une sous-propriété (ex: phase_a.power)
            Dim isSubProperty = code.Contains(".")
            Dim parentCode As String = Nothing
            Dim subPropertyName As String = Nothing

            If isSubProperty Then
                Dim parts = code.Split("."c)
                parentCode = parts(0)
                subPropertyName = parts(1)
                Log($"  🔍 Sous-propriété détectée: parent='{parentCode}', propriété='{subPropertyName}'")
            End If

            ' Filtrer les logs pour le code (parent si sous-propriété)
            Dim relevantLogs As List(Of DeviceLog)

            If isSubProperty Then
                ' Pour une sous-propriété, chercher les logs du parent
                relevantLogs = logs.Where(Function(l) l.Code?.ToLower() = parentCode.ToLower()).ToList()
            Else
                ' Code simple
                relevantLogs = logs.Where(Function(l) l.Code?.ToLower() = code.ToLower()).ToList()
            End If

            If relevantLogs.Count = 0 Then
                Log($"  ⚠️ Aucun log avec code '{code}' (Total logs: {logs.Count})")
                Return Nothing
            End If

            ' 📊 DIAGNOSTIC: Afficher la plage de dates des logs
            Dim distinctDays = relevantLogs.Select(Function(l) l.EventTime.Date).Distinct().OrderBy(Function(d) d).ToList()
            Log($"  📊 {relevantLogs.Count} logs pour '{code}' sur {distinctDays.Count} jour(s): {distinctDays.First().ToString("dd/MM")} → {distinctDays.Last().ToString("dd/MM")}")

            ' 🔍 DIAGNOSTIC: Afficher les heures des premiers et derniers logs
            If relevantLogs.Count > 0 Then
                Dim firstLog = relevantLogs.OrderBy(Function(l) l.EventTime).First()
                Dim lastLog = relevantLogs.OrderBy(Function(l) l.EventTime).Last()
                Log($"  🕐 Plage horaire: {firstLog.EventTime:dd/MM HH:mm} → {lastLog.EventTime:dd/MM HH:mm}")
            End If

            ' 🎯 NOUVEAU: Déterminer le type de visualisation
            Dim vizType = DetermineVisualizationType(code, logs)
            Log($"  🎨 Type de visualisation: {vizType}")

            ' Calcul selon le type de visualisation
            Dim hourlyStats As List(Of StatisticPoint)
            Dim totalEvents As Integer = 0
            Dim peakHour As String = ""

            Select Case vizType
                Case SensorVisualizationType.DiscreteEvents
                    ' ✅ CHANGEMENT: Afficher TOUS les événements (pas seulement les détections)
                    ' pour permettre de visualiser les transitions (pir/none, open/close, etc.)
                    ' avec des couleurs différentes selon l'état
                    Log($"  📊 Événements discrets: {relevantLogs.Count} au total")

                    ' Créer un point pour chaque événement avec sa valeur brute
                    hourlyStats = relevantLogs _
                        .OrderBy(Function(l) l.EventTime) _
                        .Select(Function(l)
                                    Return New StatisticPoint With {
                                        .Timestamp = l.EventTime,
                                        .Value = 1.0,
                                        .Label = l.EventTime.ToString("HH:mm:ss"),
                                        .OriginalValue = l.Value
                                    }
                                End Function) _
                        .ToList()

                    ' Compter les événements actifs pour les statistiques
                    totalEvents = relevantLogs _
                        .Where(Function(l)
                                   Dim v = l.Value?.ToLower()
                                   Return Not String.IsNullOrEmpty(v) AndAlso
                                          v <> "none" AndAlso v <> "clear" AndAlso
                                          v <> "0" AndAlso v <> "false" AndAlso v <> "off"
                               End Function) _
                        .Count()

                    ' Calculer le pic d'activité par heure
                    Dim grouped = hourlyStats _
                        .GroupBy(Function(p) New DateTime(p.Timestamp.Year, p.Timestamp.Month, p.Timestamp.Day, p.Timestamp.Hour, 0, 0)) _
                        .Select(Function(g) New With {
                            .Hour = g.Key,
                            .Count = g.Count()
                        }) _
                        .OrderByDescending(Function(x) x.Count) _
                        .FirstOrDefault()

                    If grouped IsNot Nothing Then
                        peakHour = grouped.Hour.ToString("HH:mm")
                    End If

                Case SensorVisualizationType.BinaryState
                    ' États binaires: créer un point pour chaque changement d'état réel
                    ' Cela permet d'afficher la vraie timeline avec tous les changements (PIR, switch, portes, etc.)
                    hourlyStats = relevantLogs _
                        .OrderBy(Function(l) l.EventTime) _
                        .Select(Function(l)
                                    ' Convertir chaque log en point de données avec la vraie valeur d'état
                                    Dim v = l.Value?.ToLower()
                                    Dim stateValue = If(v = "1" OrElse v = "true" OrElse v = "on" OrElse v = "open", 1.0, 0.0)
                                    Return New StatisticPoint With {
                                        .Timestamp = l.EventTime,
                                        .Value = stateValue,
                                        .Label = l.EventTime.ToString("HH:mm:ss"),
                                        .OriginalValue = l.Value
                                    }
                                End Function) _
                        .ToList()

                Case Else ' NumericContinuous
                    ' Valeurs numériques continues: moyenne par heure
                    Dim isCumulativeValue = code.ToLower() = "forward_energy_total" OrElse code.ToLower() = "add_ele"
                    Dim parsedCount As Integer = 0
                    Dim failedCount As Integer = 0

                    hourlyStats = relevantLogs _
                        .GroupBy(Function(l) New DateTime(l.EventTime.Year, l.EventTime.Month, l.EventTime.Day, l.EventTime.Hour, 0, 0)) _
                        .Select(Function(g)
                                    ' Convertir les valeurs en nombres
                                    Dim numericValues As New List(Of Double)
                                    For Each logEntry In g
                                        Dim val As Double
                                        Dim valueToparse As String = logEntry.Value

                                        ' ✅ NOUVEAU: Si c'est une sous-propriété, extraire du JSON
                                        If isSubProperty AndAlso IsJsonValue(logEntry.Value) Then
                                            Try
                                                Dim jsonObj = JObject.Parse(logEntry.Value)
                                                Dim subProp = jsonObj(subPropertyName)
                                                If subProp IsNot Nothing Then
                                                    valueToparse = subProp.ToString()
                                                Else
                                                    ' Sous-propriété non trouvée dans ce log
                                                    Continue For
                                                End If
                                            Catch ex As Exception
                                                ' JSON invalide, ignorer cette entrée
                                                failedCount += 1
                                                Continue For
                                            End Try
                                        End If

                                        If Double.TryParse(valueToparse, Globalization.NumberStyles.Any,
                                                          Globalization.CultureInfo.InvariantCulture, val) Then
                                            parsedCount += 1
                                            ' Conversions d'unités selon le code DP
                                            ' ✅ MODIFIÉ: Gérer les conversions pour les sous-propriétés aussi
                                            Dim codeForConversion = If(isSubProperty, code.ToLower(), code.ToLower())
                                            Select Case codeForConversion
                                                Case "cur_power", "phase_a.power"
                                                    val = val / 10.0 ' Watts
                                                Case "cur_voltage", "phase_a.voltage"
                                                    val = val / 10.0 ' Volts
                                                Case "cur_current", "phase_a.electriccurrent"
                                                    val = val / 1000.0 ' Amperes (mA → A)
                                                Case "add_ele"
                                                    val = val / 1000.0 ' kWh
                                                Case "forward_energy_total"
                                                    val = val / 100.0 ' kWh (compteur Tuya standard)
                                                Case "phase_a"
                                                    val = val / 10.0 ' Watts
                                                Case "va_temperature"
                                                    val = val / 10.0 ' °C (température * 10)
                                                Case "humidity_value"
                                                    ' Humidité déjà en %
                                                    ' Pas de conversion
                                            End Select
                                            numericValues.Add(val)
                                        Else
                                            failedCount += 1
                                        End If
                                    Next

                                    ' Pour les valeurs cumulatives (énergie), prendre la valeur max de l'heure
                                    ' Pour les valeurs instantanées (température, puissance), prendre la moyenne
                                    Dim finalValue As Double
                                    If isCumulativeValue Then
                                        finalValue = If(numericValues.Count > 0, numericValues.Max(), 0.0)
                                    Else
                                        finalValue = If(numericValues.Count > 0, numericValues.Average(), 0.0)
                                    End If

                                    Return New StatisticPoint With {
                                        .Timestamp = g.Key,
                                        .Value = finalValue,
                                        .Label = g.Key.ToString("HH:mm")
                                    }
                                End Function) _
                        .OrderBy(Function(s) s.Timestamp) _
                        .ToList()

                    Log($"  📊 Parsing valeurs: {parsedCount} réussies, {failedCount} échouées")
            End Select

            ' Vérifier si nous avons des données à afficher
            If hourlyStats Is Nothing OrElse hourlyStats.Count = 0 Then
                Log($"  ⚠️ Aucun point de données calculé pour '{code}' (type: {vizType})")
                Return Nothing
            End If

            ' Créer l'objet DeviceStatistics
            Dim stats As New DeviceStatistics With {
                .DeviceId = deviceId,
                .StatType = "avg",
                .Code = code,
                .Unit = DetermineUnit(code),
                .DataPoints = hourlyStats,
                .VisualizationType = vizType,
                .TotalEvents = totalEvents,
                .PeakActivityHour = peakHour
            }

            Log($"  ✅ Statistiques créées: {hourlyStats.Count} points, type={vizType}")
            Return stats

        Catch ex As Exception
            Log($"  ❌ Erreur CalculateStatisticsFromLogs: {ex.Message}")
            Log($"  📍 Stack trace: {ex.StackTrace}")
            Return Nothing
        End Try
    End Function

#End Region

#Region "Logs avec cache et optimisation"

    ''' <summary>
    ''' Récupère les logs d'événements avec cache
    ''' OPTIMISÉ: Cache local + regroupement des requêtes
    ''' </summary>
    Public Async Function GetDeviceLogsAsync(
        deviceId As String,
        period As HistoryPeriod
    ) As Task(Of List(Of DeviceLog))

        Try
            ' Vérifier le cache
            Dim cacheKey = $"{deviceId}_{period}"
            If _logsCache.ContainsKey(cacheKey) Then
                Dim cached = _logsCache(cacheKey)
                If (DateTime.Now - cached.Timestamp).TotalMinutes < CACHE_TTL_MINUTES Then
                    Log($"📦 Cache hit pour logs {deviceId} ({period})")
                    Return cached.Data
                Else
                    _logsCache.Remove(cacheKey)
                End If
            End If

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

            ' ✅ CORRECTION CRITIQUE: Timestamps en MILLISECONDES pour l'endpoint /v1.0/devices/{id}/logs
            ' Source: Documentation officielle Tuya Device Management API
            ' https://developer.tuya.com/en/docs/cloud/device-management?id=K9g6rfntdz78a
            Dim startTimestamp = CLng((startTime.ToUniversalTime() - New DateTime(1970, 1, 1)).TotalMilliseconds)
            Dim endTimestamp = CLng((endTime.ToUniversalTime() - New DateTime(1970, 1, 1)).TotalMilliseconds)

            ' 🔧 CONTOURNEMENT BUG PAGINATION TUYA : Diviser en tranches de 4 heures
            Dim logs = Await GetLogsWithTimeSlicesAsync(deviceId, startTimestamp, endTimestamp)

            ' Si échec, essayer v2.0 en fallback
            If logs Is Nothing OrElse logs.Count = 0 Then
                Log($"🔄 Tentative avec API v2.0...")
                logs = Await GetDeviceLogsV2Async(deviceId, startTimestamp, endTimestamp)
            End If

            If logs IsNot Nothing AndAlso logs.Count > 0 Then
                ' 📊 DIAGNOSTIC: Afficher la plage de dates TOTALE des logs
                Dim allDates = logs.Select(Function(l) l.EventTime.Date).Distinct().OrderBy(Function(d) d).ToList()
                If allDates.Count > 0 Then
                    Log($"✅ {logs.Count} logs récupérés pour {deviceId} - Plage: {allDates.First().ToString("dd/MM")} → {allDates.Last().ToString("dd/MM")} ({allDates.Count} jour(s))")
                Else
                    Log($"✅ {logs.Count} logs récupérés pour {deviceId}")
                End If

                ' Mettre en cache
                _logsCache(cacheKey) = New CachedLogs With {
                    .Data = logs,
                    .Timestamp = DateTime.Now
                }
            ElseIf logs IsNot Nothing Then
                Log($"⚠️ 0 logs récupérés pour {deviceId}")
            End If

            Return If(logs, New List(Of DeviceLog))

        Catch ex As Exception
            Log($"❌ Exception GetDeviceLogsAsync: {ex.Message}")
            Return New List(Of DeviceLog)
        End Try
    End Function

    ''' <summary>
    ''' CONTOURNEMENT BUG PAGINATION TUYA : Divise la période en tranches adaptatives
    ''' </summary>
    Private Async Function GetLogsWithTimeSlicesAsync(
        deviceId As String,
        startTimestamp As Long,
        endTimestamp As Long
    ) As Task(Of List(Of DeviceLog))

        Try
            Dim allLogs As New List(Of DeviceLog)

            ' CONTOURNEMENT PAGINATION TUYA : L'API retourne max 100 logs par appel
            ' Solution: diviser en tranches de 2h pour toutes les périodes
            ' 24h = 12 tranches, 7j = 84 tranches, 30j = 360 tranches
            ' ✅ CORRECTION: Taille en MILLISECONDES car timestamps sont en millisecondes
            Dim sliceSizeMs As Long = CLng(2 * 60 * 60 * 1000)  ' 2 heures en millisecondes

            Dim currentStart As Long = startTimestamp
            Dim sliceCount As Integer = 0

            While currentStart < endTimestamp
                sliceCount += 1
                Dim currentEnd As Long = Math.Min(currentStart + sliceSizeMs, endTimestamp)

                ' Appeler l'API pour cette tranche spécifique (sans pagination)
                Dim sliceLogs = Await GetDeviceLogsV1Async(deviceId, currentStart, currentEnd)

                If sliceLogs IsNot Nothing AndAlso sliceLogs.Count > 0 Then
                    allLogs.AddRange(sliceLogs)
                End If

                currentStart = currentEnd
            End While

            If allLogs.Count > 0 Then
                ' Trier par date décroissante et retirer les doublons éventuels
                allLogs = allLogs.OrderByDescending(Function(l) l.EventTime).ToList()

                ' Retirer les doublons basés sur EventTime + Code + Value
                Dim uniqueLogs As New List(Of DeviceLog)
                Dim seen As New HashSet(Of String)

                For Each logEntry In allLogs
                    Dim key = $"{logEntry.EventTime:yyyy-MM-dd HH:mm:ss}|{logEntry.Code}|{logEntry.Value}"
                    If Not seen.Contains(key) Then
                        seen.Add(key)
                        uniqueLogs.Add(logEntry)
                    End If
                Next

                Log($"  ✅ {uniqueLogs.Count} logs uniques après {sliceCount} tranches ({allLogs.Count - uniqueLogs.Count} doublons retirés)")
                Return uniqueLogs
            Else
                Log($"  ⚠️ 0 logs après {sliceCount} tranches")
            End If

            Return Nothing
        Catch ex As Exception
            Log($"❌ Erreur GetLogsWithTimeSlicesAsync: {ex.Message}")
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' API v1.0 pour les logs
    ''' </summary>
    Private Async Function GetDeviceLogsV1Async(
        deviceId As String,
        startTimestamp As Long,
        endTimestamp As Long
    ) As Task(Of List(Of DeviceLog))

        Try
            Dim endpoint = $"/v1.0/devices/{deviceId}/logs"
            Dim allLogs As New List(Of DeviceLog)

            ' Paramètres selon documentation officielle Tuya Device Management API :
            ' - type=7 : Tous les types de logs
            ' - size=100 : L'API limite à 100 de toute façon
            ' - query_type=2 : Type de requête (optionnel)
            ' Source: https://developer.tuya.com/en/docs/cloud/device-management?id=K9g6rfntdz78a
            Dim queryParams = $"?type=7&start_time={startTimestamp}&end_time={endTimestamp}&size=100"

            Dim response = Await _apiClient.GetAsync(endpoint & queryParams)

            ' 🔍 DIAGNOSTIC: Vérifier la réponse de l'API
            If response Is Nothing Then
                Log($"    ⚠️ API v1.0 response = null")
                Return Nothing
            End If

            Dim success = response("success")?.ToObject(Of Boolean)()
            If Not success.HasValue OrElse Not success.Value Then
                Dim msg = response("msg")?.ToString()
                Log($"    ⚠️ API v1.0 success = false, msg = {msg}")
                Return Nothing
            End If

            Dim result = response("result")
            If result Is Nothing Then
                Log($"    ⚠️ API v1.0 result = null")
                Return Nothing
            End If

            ' Parser les logs (structure variable)
            Dim logsArray As JArray = Nothing

            If TypeOf result Is JObject Then
                Dim resultObj = CType(result, JObject)
                If resultObj("logs") IsNot Nothing Then
                    logsArray = CType(resultObj("logs"), JArray)
                Else
                    Log($"    ⚠️ API v1.0 result.logs n'existe pas. Keys: {String.Join(", ", resultObj.Properties().Select(Function(p) p.Name))}")
                End If
            ElseIf TypeOf result Is JArray Then
                logsArray = CType(result, JArray)
            Else
                Log($"    ⚠️ API v1.0 result type inconnu: {result.GetType().Name}")
            End If

            If logsArray IsNot Nothing Then
                Log($"    📊 API v1.0 retourné {logsArray.Count} logs")
                For Each item As JToken In logsArray
                    Dim jItem = CType(item, JObject)
                    Dim timestamp = jItem("event_time")?.ToObject(Of Long)()
                    Dim code = jItem("code")?.ToString()
                    Dim value = jItem("value")?.ToString()

                    If timestamp.HasValue Then
                        ' event_time est en millisecondes
                        Dim dt = DateTimeOffset.FromUnixTimeMilliseconds(timestamp.Value).LocalDateTime
                        Dim eventType = DetermineEventType(code, value)
                        Dim description = CreateEventDescription(code, value, eventType)

                        allLogs.Add(New DeviceLog With {
                                .EventTime = dt,
                                .Code = code,
                                .Value = value,
                                .EventType = eventType,
                                .Description = description
                            })
                    End If
                Next
            Else
                Log($"    ⚠️ API v1.0 logsArray = null")
            End If

            Return If(allLogs.Count > 0, allLogs, Nothing)

        Catch ex As Exception
            Log($"    ❌ Exception GetDeviceLogsV1Async: {ex.Message}")
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' API v2.0 en fallback
    ''' </summary>
    Private Async Function GetDeviceLogsV2Async(
        deviceId As String,
        startTimestamp As Long,
        endTimestamp As Long
    ) As Task(Of List(Of DeviceLog))

        Try
            Dim endpoint = $"/v2.0/cloud/thing/{deviceId}/report-logs"
            Dim allLogs As New List(Of DeviceLog)

            Dim queryParams = $"?start_time={startTimestamp}&end_time={endTimestamp}&size=100&type=7&last_row_key="

            Dim response = Await _apiClient.GetAsync(endpoint & queryParams)

            If response IsNot Nothing AndAlso response("success")?.ToObject(Of Boolean)() = True Then
                Dim result = response("result")

                If result IsNot Nothing Then
                    Dim logsArray As JArray = Nothing

                    If TypeOf result Is JObject Then
                        Dim resultObj = CType(result, JObject)
                        If resultObj("list") IsNot Nothing Then
                            logsArray = CType(resultObj("list"), JArray)
                        End If
                    ElseIf TypeOf result Is JArray Then
                        logsArray = CType(result, JArray)
                    End If

                    If logsArray IsNot Nothing Then
                        For Each item As JToken In logsArray
                            Dim jItem = CType(item, JObject)
                            Dim timestamp = jItem("event_time")?.ToObject(Of Long)()
                            Dim code = jItem("code")?.ToString()
                            Dim value = jItem("value")?.ToString()

                            If timestamp.HasValue Then
                                Dim dt = DateTimeOffset.FromUnixTimeMilliseconds(timestamp.Value).LocalDateTime
                                Dim eventType = DetermineEventType(code, value)
                                Dim description = CreateEventDescription(code, value, eventType)

                                allLogs.Add(New DeviceLog With {
                                    .EventTime = dt,
                                    .Code = code,
                                    .Value = value,
                                    .EventType = eventType,
                                    .Description = description
                                })
                            End If
                        Next
                    End If
                End If
            End If

            Return If(allLogs.Count > 0, allLogs, Nothing)

        Catch ex As Exception
            Return Nothing
        End Try
    End Function

#End Region

#Region "Utilitaires"

    ''' <summary>
    ''' Détermine l'unité selon le code DP
    ''' ✅ MODIFIÉ: Supporte les sous-propriétés (ex: phase_a.power)
    ''' </summary>
    Private Function DetermineUnit(code As String) As String
        Dim codeLower = code.ToLower()

        ' ✅ NOUVEAU: Gérer les sous-propriétés en examinant le nom complet
        If codeLower.Contains(".power") Then
            Return "W"
        ElseIf codeLower.Contains(".voltage") Then
            Return "V"
        ElseIf codeLower.Contains(".electriccurrent") OrElse codeLower.Contains(".current") Then
            Return "mA"
        End If

        ' Codes standards
        Select Case codeLower
            Case "add_ele", "forward_energy_total"
                Return "kWh"
            Case "cur_power", "phase_a"
                Return "W"
            Case "cur_voltage"
                Return "V"
            Case "cur_current"
                Return "mA"
            Case "va_temperature", "temp_current", "temperature"
                Return "°C"
            Case "humidity_value", "humidity"
                Return "%"
            Case "bright_value", "brightness"
                Return "lux"
            Case "battery_percentage", "battery"
                Return "%"
            Case Else
                ' Pour les capteurs sans unité connue
                Return ""
        End Select
    End Function

    ''' <summary>
    ''' Détermine le type de visualisation adapté au capteur
    ''' </summary>
    Private Function DetermineVisualizationType(code As String, logs As List(Of DeviceLog)) As SensorVisualizationType
        Dim codeLower = code.ToLower()
        Log($"  🔍 Détection type visualisation pour code: '{code}'")

        ' 1. D'abord, vérifier si les valeurs sont uniquement binaires (0/1, true/false)
        ' ou des modes discrets (hot/cool/eco/etc.)
        ' Cette vérification est prioritaire pour gérer correctement les PIR et autres capteurs
        ' qui peuvent renvoyer des états binaires plutôt que des événements ponctuels
        If logs IsNot Nothing AndAlso logs.Count > 0 Then
            Dim relevantLogs = logs.Where(Function(l) l.Code?.ToLower() = codeLower).ToList()
            If relevantLogs.Count > 0 Then
                Dim uniqueValues = relevantLogs.Select(Function(l) l.Value?.ToLower()).Distinct().Where(Function(v) Not String.IsNullOrEmpty(v)).ToList()
                Log($"  🔍 Valeurs uniques trouvées: {String.Join(", ", uniqueValues)}")

                ' Si uniquement des valeurs binaires → Timeline avec états
                If uniqueValues.Count <= 2 AndAlso uniqueValues.Count > 0 AndAlso
                   uniqueValues.All(Function(v) v = "0" OrElse v = "1" OrElse
                                              v = "true" OrElse v = "false" OrElse
                                              v = "on" OrElse v = "off" OrElse
                                              v = "open" OrElse v = "close") Then
                    Log($"  ✅ Type détecté: BinaryState (valeurs binaires détectées)")
                    Return SensorVisualizationType.BinaryState
                End If

                ' ✅ NOUVEAU: Si c'est un code "mode" avec des valeurs discrètes (hot, cool, eco, etc.)
                ' → Timeline avec états pour visualiser les changements de mode
                If codeLower.Contains("mode") Then
                    Dim modeValues = New String() {"hot", "cool", "cold", "eco", "auto", "manual", "comfort", "holiday", "program", "away"}
                    If uniqueValues.Any(Function(v) modeValues.Contains(v)) Then
                        Log($"  ✅ Type détecté: BinaryState (mode de chauffage détecté)")
                        Return SensorVisualizationType.BinaryState
                    End If
                End If
            End If
        End If

        ' 2. États binaires par mots-clés (switch, porte, contact)
        If codeLower.Contains("switch") OrElse codeLower.Contains("door") OrElse
           codeLower.Contains("contact") OrElse codeLower.Contains("window") OrElse
           codeLower.Contains("lock") OrElse codeLower.Contains("opened") Then
            Log($"  ✅ Type détecté: BinaryState (mots-clés)")
            Return SensorVisualizationType.BinaryState
        End If

        ' 3. Événements ponctuels par mots-clés (PIR, fumée, tamper, alarme)
        ' Note: Cette vérification vient après la détection des valeurs binaires
        ' pour permettre aux PIR avec états binaires d'être affichés comme timeline
        If codeLower.Contains("pir") OrElse codeLower.Contains("motion") OrElse
           codeLower.Contains("presence") OrElse codeLower.Contains("smoke") OrElse
           codeLower.Contains("tamper") OrElse codeLower.Contains("alarm") OrElse
           codeLower.Contains("doorbell") OrElse codeLower.Contains("button") Then
            Log($"  ✅ Type détecté: DiscreteEvents (mots-clés)")
            Return SensorVisualizationType.DiscreteEvents
        End If

        ' 4. Par défaut: valeurs numériques continues
        Log($"  ✅ Type détecté: NumericContinuous (par défaut)")
        Return SensorVisualizationType.NumericContinuous
    End Function

    ''' <summary>
    ''' Détermine le type d'événement
    ''' </summary>
    Private Function DetermineEventType(code As String, value As String) As String
        If String.IsNullOrEmpty(code) Then Return "unknown"

        Dim codeLower = code.ToLower()
        Dim valueLower = value?.ToLower()

        ' Capteurs de mouvement et détection
        If codeLower.Contains("pir") OrElse codeLower.Contains("motion") Then
            Return If(valueLower = "pir" OrElse valueLower = "motion" OrElse valueLower = "detected", "motion_detected", "motion_clear")
        End If

        ' Capteurs de fumée
        If codeLower.Contains("smoke") Then
            Return If(valueLower?.Contains("alarm") OrElse valueLower = "1", "smoke_detected", "smoke_clear")
        End If

        ' Capteurs de porte/fenêtre
        If codeLower.Contains("door") OrElse codeLower.Contains("contact") OrElse codeLower.Contains("window") Then
            Return If(valueLower = "true" OrElse valueLower = "1" OrElse valueLower = "open", "door_open", "door_closed")
        End If

        ' Switch
        Select Case codeLower
            Case "switch_led", "switch", "switch_1", "switch_2"
                Return If(valueLower = "true" OrElse valueLower = "1", "switch_on", "switch_off")
            Case "online"
                Return If(valueLower = "true", "online", "offline")
            Case Else
                Return "status_change"
        End Select
    End Function

    ''' <summary>
    ''' Crée une description lisible
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
            Case "motion_detected"
                Return "🚶 Mouvement détecté"
            Case "motion_clear"
                Return "⚪ Pas de mouvement"
            Case "smoke_detected"
                Return "🔥 Fumée détectée !"
            Case "smoke_clear"
                Return "✅ Pas de fumée"
            Case "door_open"
                Return "🚪 Ouvert"
            Case "door_closed"
                Return "🔒 Fermé"
            Case Else
                Return $"{code} = {value}"
        End Select
    End Function

    ''' <summary>
    ''' Efface le cache (utile pour forcer un rafraîchissement)
    ''' </summary>
    Public Sub ClearCache()
        _statisticsCache.Clear()
        _logsCache.Clear()
        Log($"🗑️ Cache vidé")
    End Sub

    ''' <summary>
    ''' Log un message
    ''' </summary>
    Private Sub Log(message As String)
        _logCallback?.Invoke($"[HistoryService] {message}")
    End Sub

#End Region

#Region "Classes de cache"

    ''' <summary>
    ''' Cache pour les statistiques
    ''' </summary>
    Private Class CachedStatistics
        Public Property Data As DeviceStatistics
        Public Property Timestamp As DateTime
    End Class

    ''' <summary>
    ''' Cache pour les logs
    ''' </summary>
    Private Class CachedLogs
        Public Property Data As List(Of DeviceLog)
        Public Property Timestamp As DateTime
    End Class

#End Region

End Class
