# 🔧 Proposition de correctif pour la fonctionnalité Historique

## 📋 Analyse du problème actuel

Après analyse de la **documentation officielle Tuya** et du code actuel, j'ai identifié plusieurs problèmes critiques :

---

## ❌ Problèmes identifiés

### 1. Statistiques - Mauvais format de paramètres

**Code actuel (INCORRECT)** :
```vb
' Ligne 44-45 de TuyaHistoryService.vb
Dim startTimestamp = CLng((startTime.ToUniversalTime() - New DateTime(1970, 1, 1)).TotalSeconds)
Dim queryParams = $"?code={code}&start_time={startTimestamp}&end_time={endTimestamp}&type=sum"
```

**Problème** :
- ❌ L'API `/v1.0/devices/{device_id}/statistics/days` **n'accepte PAS** `start_time`/`end_time`
- ❌ Elle attend `start_day` et `end_day` au format **`YYYYMMDD`**
- ❌ Le paramètre est `stat_type` (pas `type`)

**Solution documentée par Tuya** :
```
GET /v1.0/devices/{device_id}/statistics/days?code=add_ele&start_day=20190101&end_day=20190107&stat_type=sum
```

---

### 2. Logs - Mauvais format de timestamps

**Code actuel (INCORRECT)** :
```vb
' Ligne 169-170 de TuyaHistoryService.vb
Dim startTimestamp = CLng((intervalStart.ToUniversalTime() - New DateTime(1970, 1, 1)).TotalSeconds)
```

**Problème** :
- ❌ Utilise `TotalSeconds` alors que l'API attend des **millisecondes**
- ❌ Exemple de la doc Tuya : `start_time=1545898159931` (13 chiffres)

**Solution** :
```vb
Dim startTimestamp = CLng((intervalStart.ToUniversalTime() - New DateTime(1970, 1, 1)).TotalMilliseconds)
```

---

### 3. Codes DP incorrects ou manquants

**Codes actuels testés** :
- `cur_power` - Puissance instantanée (W) ❌ Pas adapté pour statistiques historiques
- `add_ele` - Électricité ajoutée/cumulée (kWh) ✅ CORRECT

**Selon la documentation Tuya**, les codes courants sont :
- `add_ele` - Consommation électrique cumulée (kWh)
- `cur_current` - Courant (mA)
- `cur_voltage` - Tension (V)
- `cur_power` - Puissance instantanée (W)

---

### 4. Structure de réponse API non prise en compte

**Réponse API `/v1.0/devices/{device_id}/statistics/days`** :
```json
{
  "success": true,
  "result": {
    "days": {
      "20190101": "0.5",
      "20190102": "1.2",
      "20190103": "0.8"
    }
  }
}
```

**Code actuel** :
- ❌ Cherche un `JArray` avec des objets `{time, value}`
- ✅ Devrait chercher un `JObject` avec des propriétés au format date

---

## ✅ Solution proposée

### Option 1 : Stratégie multi-codes robuste

Essayer **plusieurs codes DP** dans l'ordre jusqu'à trouver des données :

```vb
Public Async Function GetDeviceStatisticsAsync(
    deviceId As String,
    period As HistoryPeriod
) As Task(Of DeviceStatistics)

    ' Essayer plusieurs codes dans l'ordre
    Dim codesToTry = {"add_ele", "cur_power", "cur_voltage", "cur_current"}

    For Each code In codesToTry
        Dim stats = Await GetDeviceStatisticsForCodeAsync(deviceId, period, code)
        If stats IsNot Nothing AndAlso stats.DataPoints.Count > 0 Then
            Log($"✅ Données trouvées avec le code '{code}'")
            Return stats
        End If
    Next

    Log($"❌ Aucune donnée trouvée pour aucun code")
    Return Nothing
End Function
```

---

### Option 2 : API alternative - Report Logs

Si les statistiques ne fonctionnent pas (permissions manquantes), utiliser l'API **Report Logs** :

```
GET /v1.0/iot-03/devices/{device_id}/report-logs
  ?codes=add_ele
  &start_time=1676944806000
  &end_time=1677141431000
  &size=100
```

**Avantages** :
- Timestamps en millisecondes ✓
- Support de codes multiples ✓
- Peut retourner plus de détails ✓

---

### Option 3 : API v2.0 (plus récente)

```
GET /v2.0/cloud/thing/{device_id}/report-logs
  ?codes=switch_led_1
  &start_time=1676944806000
  &end_time=1677141431000
  &last_row_key=
  &size=20
```

---

## 🔨 Implémentation recommandée

### Étape 1 : Corriger GetDeviceStatisticsAsync

```vb
Public Async Function GetDeviceStatisticsAsync(
    deviceId As String,
    period As HistoryPeriod,
    Optional code As String = "add_ele"
) As Task(Of DeviceStatistics)

    Try
        Dim endDate = DateTime.Now.Date
        Dim startDate As DateTime

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

        ' ✅ CORRECTION: Format YYYYMMDD
        Dim startDay = startDate.ToString("yyyyMMdd")
        Dim endDay = endDate.ToString("yyyyMMdd")

        Dim endpoint = $"/v1.0/devices/{deviceId}/statistics/days"
        Dim queryParams = $"?code={code}&start_day={startDay}&end_day={endDay}&stat_type=sum"

        Log($"📊 API Call: {endpoint}{queryParams}")

        Dim response = Await _apiClient.GetAsync(endpoint & queryParams)

        If response IsNot Nothing AndAlso response("success")?.ToObject(Of Boolean)() = True Then
            Dim result = response("result")

            Dim stats As New DeviceStatistics With {
                .DeviceId = deviceId,
                .StatType = "sum",
                .Code = code,
                .Unit = "kWh",
                .DataPoints = New List(Of StatisticPoint)
            }

            ' ✅ CORRECTION: Parser la structure { "days": { "20190101": "0.5" } }
            If result IsNot Nothing Then
                Dim daysData = TryCast(result("days"), JObject)
                If daysData IsNot Nothing Then
                    For Each prop As JProperty In daysData.Properties()
                        Dim dateKey = prop.Name  ' Format: "yyyyMMdd"
                        Dim valueStr = prop.Value?.ToString()

                        If Not String.IsNullOrEmpty(dateKey) AndAlso Not String.IsNullOrEmpty(valueStr) Then
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

            stats.DataPoints.Sort(Function(a, b) a.Timestamp.CompareTo(b.Timestamp))

            Log($"✅ {stats.DataPoints.Count} points de données récupérés")
            Return stats
        Else
            Dim errorMsg = If(response?("msg")?.ToString(), "Erreur inconnue")
            Dim errorCode = If(response?("code")?.ToString(), "")
            Log($"❌ Erreur API: [{errorCode}] {errorMsg}")

            ' Log la réponse complète pour debug
            If response IsNot Nothing Then
                Log($"📝 Réponse complète: {response.ToString()}")
            End If

            Return Nothing
        End If

    Catch ex As Exception
        Log($"❌ Exception: {ex.Message}")
        Log($"   Stack: {ex.StackTrace}")
        Return Nothing
    End Try
End Function
```

---

### Étape 2 : Corriger GetDeviceLogsAsync

```vb
Public Async Function GetDeviceLogsAsync(
    deviceId As String,
    period As HistoryPeriod
) As Task(Of List(Of DeviceLog))

    Try
        Dim endTime = DateTime.Now
        Dim startTime As DateTime

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

        ' ✅ CORRECTION: Timestamps en MILLISECONDES
        Dim startTimestamp = CLng((startTime.ToUniversalTime() - New DateTime(1970, 1, 1)).TotalMilliseconds)
        Dim endTimestamp = CLng((endTime.ToUniversalTime() - New DateTime(1970, 1, 1)).TotalMilliseconds)

        Dim endpoint = $"/v1.0/devices/{deviceId}/logs"
        Dim queryParams = $"?start_time={startTimestamp}&end_time={endTimestamp}&size=100"

        Log($"📊 API Call: {endpoint}{queryParams}")
        Log($"   Timestamps: {startTimestamp} -> {endTimestamp} (ms)")

        Dim response = Await _apiClient.GetAsync(endpoint & queryParams)

        Dim logs As New List(Of DeviceLog)

        If response IsNot Nothing AndAlso response("success")?.ToObject(Of Boolean)() = True Then
            Dim result = response("result")

            ' Parser les logs
            Dim logsArray As JArray = Nothing

            ' La structure peut varier
            If result IsNot Nothing Then
                If TypeOf result Is JObject AndAlso CType(result, JObject)("logs") IsNot Nothing Then
                    logsArray = CType(CType(result, JObject)("logs"), JArray)
                ElseIf TypeOf result Is JArray Then
                    logsArray = CType(result, JArray)
                End If
            End If

            If logsArray IsNot Nothing Then
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
            Dim errorCode = If(response?("code")?.ToString(), "")
            Log($"❌ Erreur API: [{errorCode}] {errorMsg}")

            If response IsNot Nothing Then
                Log($"📝 Réponse complète: {response.ToString()}")
            End If
        End If

        logs.Sort(Function(a, b) b.EventTime.CompareTo(a.EventTime))
        Return logs

    Catch ex As Exception
        Log($"❌ Exception: {ex.Message}")
        Log($"   Stack: {ex.StackTrace}")
        Return New List(Of DeviceLog)
    End Try
End Function
```

---

## 🎯 Plan d'action recommandé

### Phase 1 : Corrections immédiates
1. ✅ Corriger le format des timestamps pour les statistiques (YYYYMMDD)
2. ✅ Corriger les timestamps pour les logs (millisecondes)
3. ✅ Ajouter logging détaillé des réponses API
4. ✅ Essayer le code `add_ele` au lieu de `cur_power`

### Phase 2 : Robustesse
5. ✅ Implémenter la stratégie multi-codes
6. ✅ Ajouter fallback sur API alternative si permissions manquantes
7. ✅ Améliorer les messages d'erreur

### Phase 3 : Optimisation (optionnel)
8. ⚪ Implémenter cache local des données
9. ⚪ Support des endpoints v2.0
10. ⚪ Agrégation intelligente selon la période

---

## 📚 Références documentation Tuya

- [Query Device Log](https://developer.tuya.com/en/docs/cloud/0a30fc557f?id=Ka7kjybdo0jse)
- [Device Data Statistics](https://developer.tuya.com/en/docs/cloud/device-data-statistic?id=Ka7g7nvnad1rm)
- [Query Device Status Report Log](https://developer.tuya.com/en/docs/cloud/8eac85909d?id=Kalmcozgt7nl0)

---

## ❓ Question pour vous

**Quelle approche préférez-vous ?**

1. **Approche conservatrice** : Corriger uniquement les bugs identifiés
2. **Approche robuste** : Implémenter la stratégie multi-codes + fallback
3. **Approche complète** : Tout + support v2.0 + cache

Dites-moi et je commencerai l'implémentation !
