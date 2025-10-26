# Corrections de la fonctionnalité Historique

## Problèmes identifiés et corrigés

### 1. Timestamps en millisecondes au lieu de secondes ⚠️ **CRITIQUE**

**Problème**:
- L'API Tuya attend des timestamps Unix en **secondes**
- Le code VB utilisait des timestamps en **millisecondes** (1000x trop grands)
- Résultat: L'API rejetait toutes les requêtes

**Correction**:
```vb
' AVANT (INCORRECT)
Dim startTimestamp = CLng((startTime.ToUniversalTime() - New DateTime(1970, 1, 1)).TotalMilliseconds)

' APRÈS (CORRECT)
Dim startTimestamp = CLng((startTime.ToUniversalTime() - New DateTime(1970, 1, 1)).TotalSeconds)
```

**Fichiers modifiés**:
- `TuyaHistoryService.vb` lignes 44-45 (GetDeviceStatisticsAsync)
- `TuyaHistoryService.vb` lignes 151-152 (GetDeviceLogsAsync)

---

### 2. Paramètres API incorrects

**Problème**:
- Paramètre `type=7` non reconnu par l'API
- Le code Python fonctionnel utilise `types=report` (pluriel + valeur textuelle)

**Correction**:
```vb
' AVANT (INCORRECT)
Dim queryParams = $"?start_time={startTimestamp}&end_time={endTimestamp}&size=100&type=7"

' APRÈS (CORRECT)
Dim queryParams = $"?start_time={startTimestamp}&end_time={endTimestamp}&types=report&size=100"
```

**Fichiers modifiés**:
- `TuyaHistoryService.vb` ligne 165

---

### 3. Une seule requête vs multiples requêtes

**Problème**:
- Une seule requête pour toute la période dépassait les limites de l'API
- Le code Python fonctionnel divise la période en **24 requêtes** pour une journée

**Correction**:
- Ajout d'un système de multiples requêtes avec intervalles
- 24 requêtes pour 24h (1 par heure)
- 28 requêtes pour 7 jours (~4 par jour)
- 30 requêtes pour 30 jours (1 par jour)
- Délai de 50ms entre chaque requête pour éviter le rate limiting
- Déduplication des logs via HashSet (évite les doublons entre intervalles)

**Fichiers modifiés**:
- `TuyaHistoryService.vb` lignes 103-237 (Réécriture complète de GetDeviceLogsAsync)

---

### 4. Parsing flexible de la structure des logs

**Problème**:
- La structure de la réponse API peut varier (`result.logs` ou `result` directement)

**Correction**:
```vb
' Parser les logs - la structure peut être dans result.logs ou directement result
Dim logsArray As JArray = Nothing

If result IsNot Nothing Then
    If TypeOf result Is JObject AndAlso CType(result, JObject)("logs") IsNot Nothing Then
        logsArray = CType(CType(result, JObject)("logs"), JArray)
    ElseIf TypeOf result Is JArray Then
        logsArray = CType(result, JArray)
    End If
End If
```

**Fichiers modifiés**:
- `TuyaHistoryService.vb` lignes 174-183

---

### 5. Amélioration des messages d'erreur

**Ajout**:
- Messages d'erreur plus détaillés avec code d'erreur
- Détection spécifique des erreurs de permissions
- Conseils pour résoudre les problèmes de permissions API

**Exemple**:
```vb
If errorCode.Contains("permission") OrElse errorMsg.Contains("permission") Then
    Log($"   💡 Conseil: Vérifiez les permissions API dans Tuya IoT Platform")
    Log($"   Activez 'Device Statistics' dans les API Products")
End If
```

**Fichiers modifiés**:
- `TuyaHistoryService.vb` lignes 103-106

---

### 6. Support des timestamps mixtes en réponse

**Problème**:
- L'API peut retourner des timestamps en secondes OU en millisecondes selon l'endpoint

**Correction**:
```vb
' Le timestamp retourné peut être en secondes ou millisecondes
' Vérifier si le timestamp est en millisecondes (> 10000000000)
Dim dt As DateTime
If timestamp.Value > 10000000000 Then
    dt = DateTimeOffset.FromUnixTimeMilliseconds(timestamp.Value).LocalDateTime
Else
    dt = DateTimeOffset.FromUnixTimeSeconds(timestamp.Value).LocalDateTime
End If
```

**Fichiers modifiés**:
- `TuyaHistoryService.vb` lignes 77-84

---

## Nouveaux paramètres optionnels

### GetDeviceLogsAsync

Ajout de 2 paramètres optionnels:

```vb
Public Async Function GetDeviceLogsAsync(
    deviceId As String,
    period As HistoryPeriod,
    Optional types As String = "report",      ' Nouveau: Type de logs
    Optional codes As String = Nothing        ' Nouveau: Filtrer par codes spécifiques
) As Task(Of List(Of DeviceLog))
```

**Valeurs possibles pour `types`**:
- `"report"` (par défaut) - Changements d'état et valeurs
- `"online"` - Événements de connexion
- `"offline"` - Événements de déconnexion
- Peut combiner avec des virgules: `"report,online,offline"`

**Utilisation de `codes`**:
- Filtrer par codes spécifiques: `"cur_power,switch_led"`
- Laisser vide (Nothing) pour tous les codes

---

## Tests recommandés

1. **Test des logs basiques**:
   - Cliquer sur le bouton 📊 d'un appareil
   - Vérifier que les événements on/off apparaissent dans la timeline
   - Les logs devraient maintenant être récupérés même si pas de données de consommation

2. **Test des différentes périodes**:
   - Tester "Dernières 24 heures" (24 requêtes)
   - Tester "Derniers 7 jours" (28 requêtes)
   - Tester "Derniers 30 jours" (30 requêtes)

3. **Vérifier les logs dans le dashboard**:
   - Observer les messages `[HistoryService]` dans la zone de logs
   - Vérifier le nombre de requêtes effectuées
   - Chercher les messages d'erreur éventuels

4. **Test des statistiques**:
   - Si l'appareil mesure la consommation, le graphique devrait s'afficher
   - Si erreur de permission, suivre les conseils dans les logs

---

## Problèmes connus / Limitations

1. **API Statistics** (`/v1.0/devices/{deviceId}/statistics/days`):
   - Nécessite des permissions spéciales dans Tuya IoT Platform
   - Pourrait ne pas fonctionner même avec les corrections
   - Vérifier dans Tuya IoT Platform → API Products → "Device Statistics"

2. **Rate Limiting**:
   - Délai de 50ms entre chaque requête pour éviter "too many requests"
   - Pour 24h: 24 requêtes × 50ms = 1.2 secondes de chargement
   - Pour 30 jours: 30 requêtes × 50ms = 1.5 secondes

3. **Codes d'appareils non standards**:
   - Par défaut cherche `cur_power` pour la consommation
   - Certains appareils utilisent d'autres codes
   - Nécessite de vérifier les codes disponibles dans l'API Tuya

---

## Référence du code Python fonctionnel

Les corrections s'inspirent de ce code Python qui fonctionne:

```python
# Timestamps en SECONDES
start_time = int(s1.timestamp())
end_time = int(s2.timestamp())

# Endpoint avec types (pluriel) au lieu de type
api_logs = f"/v1.0/devices/{dev_id}/logs?start_time={start_time}&end_time={end_time}&types=report&size=1"

# Division en 24 requêtes pour une journée
for i in range(0, 86400, nbr_steps):  # 86400 secondes = 24h
    # ... créer une requête par intervalle
```

---

## Prochaines étapes

1. Compiler le projet sur Windows avec Visual Studio
2. Tester la récupération des logs
3. Si les statistiques ne fonctionnent pas, vérifier les permissions API Tuya
4. Potentiellement utiliser les logs pour calculer la consommation (au lieu de l'API statistics)
5. Ajouter une interface pour sélectionner les codes à afficher (au lieu de juste `cur_power`)
