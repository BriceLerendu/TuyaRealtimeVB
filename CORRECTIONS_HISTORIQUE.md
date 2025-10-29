# Corrections de la fonctionnalité Historique

## ⚠️ NOTE IMPORTANTE

Ce document a été corrigé pour refléter la **documentation officielle Tuya Device Management API** (mise à jour 2024-06-26).

**Voir ANALYSE_DOC_OFFICIELLE_DEVICE_MANAGEMENT.md pour les détails de l'analyse.**

---

## État actuel du code

### ✅ Code CORRECT et conforme à la documentation officielle

Le code actuel de `TuyaHistoryService.vb` est **CORRECT** et conforme à la documentation officielle Tuya.

#### 1. Timestamps en millisecondes ✅ **CORRECT**

**Documentation officielle Tuya** :
```
start_time: The 13-digit timestamp of the start time
end_time: The 13-digit timestamp of the end time
```

**13 chiffres = MILLISECONDES** (ex: 1545898159935)

**Code actuel (CORRECT)** :
```vb
' ✅ CORRECT: Timestamps en MILLISECONDES pour l'endpoint /v1.0/devices/{id}/logs
' Source: Documentation officielle Tuya Device Management API
Dim startTimestamp = CLng((startTime.ToUniversalTime() - New DateTime(1970, 1, 1)).TotalMilliseconds)
Dim endTimestamp = CLng((endTime.ToUniversalTime() - New DateTime(1970, 1, 1)).TotalMilliseconds)
```

**Fichiers** : `TuyaHistoryService.vb`

---

#### 2. Paramètre type ✅ **CORRECT**

**Documentation officielle Tuya** :
```
type: Event types supported by the device. You can query multiple event types,
      separated with commas (,). This parameter is required.

Valid values:
  1: Device goes online
  2: Device goes offline
  7: Data point reported (le plus utile pour les historiques)
```

**Code actuel (CORRECT)** :
```vb
' ✅ CORRECT: type=7 pour récupérer les data points
Dim queryParams = $"?type=7&start_time={startTimestamp}&end_time={endTimestamp}&size=100"
```

**Note** : Le paramètre s'appelle `type` (singulier), pas `types`.

**Fichiers** : `TuyaHistoryService.vb`

---

## Améliorations implémentées

### 1. Division en multiples requêtes

**Problème** :
- Une seule requête pour toute la période peut dépasser les limites de l'API

**Solution implémentée** :
- Système de multiples requêtes avec intervalles
- 24 requêtes pour 24h (1 par heure)
- 28 requêtes pour 7 jours (~4 par jour)
- 30 requêtes pour 30 jours (1 par jour)
- Délai de 50ms entre chaque requête pour éviter le rate limiting
- Déduplication des logs via HashSet (évite les doublons entre intervalles)

**Fichiers modifiés** :
- `TuyaHistoryService.vb` - Fonction GetLogsWithTimeSlicesAsync

---

### 2. Parsing flexible de la structure des logs

**Problème** :
- La structure de la réponse API peut varier (`result.logs` ou `result` directement)

**Solution** :
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

**Fichiers modifiés** :
- `TuyaHistoryService.vb`

---

### 3. Support des timestamps mixtes en réponse

**Problème** :
- L'API peut retourner des timestamps en secondes OU en millisecondes selon le contexte

**Solution** :
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

**Fichiers modifiés** :
- `TuyaHistoryService.vb`

---

### 4. Explosion des propriétés JSON en sous-propriétés

**Problème** :
- Les données triphasées (phase_a, phase_b, phase_c) sont encodées en base64 et doivent être décodées

**Solution implémentée** :
- Détection automatique des valeurs base64
- Décodage et parsing JSON
- Explosion en sous-propriétés avec notation pointée (ex: `phase_a.power`, `phase_a.voltage`)

**Fichiers modifiés** :
- `TuyaHistoryService.vb` - Fonction ExplodeJsonProperties

---

### 5. Auto-détection des codes DP disponibles

**Problème** :
- Les codes DP varient selon le type d'appareil (capteurs température, prises, etc.)

**Solution implémentée** :
- Détection automatique de tous les codes DP disponibles dans les logs
- Tri par priorité (température > humidité > puissance > batterie > luminosité)
- Fallback automatique si les codes prioritaires ne retournent pas de données

**Fichiers modifiés** :
- `TuyaHistoryService.vb` - Fonction AutoDetectAndGetStatisticsAsync

**Voir** : FEATURE_AUTO_DETECT_DP_CODES.md pour les détails

---

### 6. Amélioration des messages d'erreur

**Ajout** :
- Messages d'erreur plus détaillés avec code d'erreur
- Détection spécifique des erreurs de permissions
- Conseils pour résoudre les problèmes de permissions API

**Exemple** :
```vb
If errorCode.Contains("permission") OrElse errorMsg.Contains("permission") Then
    Log($"   💡 Conseil: Vérifiez les permissions API dans Tuya IoT Platform")
    Log($"   Activez 'Device Statistics' dans les API Products")
End If
```

**Fichiers modifiés** :
- `TuyaHistoryService.vb`

---

## Paramètres optionnels disponibles

### Paramètre `codes` (non implémenté)

**Documentation officielle** :
```
codes: The codes of data points supported by the device.
       You can query multiple data points, separated with commas (,).
       This parameter value is empty by default.
```

**Utilisation potentielle** :
```vb
' Filtrer uniquement les codes d'intérêt
Dim codes = "cur_power,switch_1,add_ele"
Dim queryParams = $"?type=7&start_time={startTimestamp}&end_time={endTimestamp}&codes={codes}&size=100"
```

**Avantage** : Réduire la quantité de données retournées et accélérer les requêtes.

---

### Combiner plusieurs types d'événements (non implémenté)

**Documentation officielle** :
```
type: You can query multiple event types, separated with commas (,)
```

**Utilisation actuelle** : `type=7` (data points seulement)

**Utilisation potentielle** :
```vb
' Inclure online/offline + data points
Dim queryParams = $"?type=1,2,7&start_time={startTimestamp}&end_time={endTimestamp}&size=100"
```

**Avantage** : Voir les événements de connexion/déconnexion dans la timeline.

---

## Tests recommandés

1. **Test des logs basiques** :
   - Cliquer sur le bouton 📊 d'un appareil
   - Vérifier que les données apparaissent dans le graphique
   - Les logs devraient être récupérés pour tous types d'appareils (prises, capteurs, etc.)

2. **Test des différentes périodes** :
   - Tester "Dernières 24 heures" (24 requêtes)
   - Tester "Derniers 7 jours" (28 requêtes)
   - Tester "Derniers 30 jours" (30 requêtes)

3. **Vérifier les logs dans le dashboard** :
   - Observer les messages `[HistoryService]` dans la zone de logs
   - Vérifier le nombre de requêtes effectuées
   - Chercher les messages d'erreur éventuels

4. **Test des appareils triphasés** :
   - Tester avec un appareil qui expose `phase_a`, `phase_b`, `phase_c`
   - Vérifier que les sous-propriétés sont explosées (`phase_a.power`, `phase_a.voltage`, etc.)

5. **Test auto-détection** :
   - Tester avec un capteur de température/humidité
   - Vérifier que le code DP est automatiquement détecté
   - Vérifier que le graphique affiche les bonnes données

---

## Problèmes connus / Limitations

1. **API Statistics** (`/v1.0/devices/{deviceId}/statistics/days`) :
   - Nécessite des permissions spéciales dans Tuya IoT Platform
   - Pourrait ne pas fonctionner même avec les corrections
   - Vérifier dans Tuya IoT Platform → API Products → "Device Statistics"

2. **Rate Limiting** :
   - Délai de 50ms entre chaque requête pour éviter "too many requests"
   - Pour 24h : 24 requêtes × 50ms = 1.2 secondes de chargement
   - Pour 30 jours : 30 requêtes × 50ms = 1.5 secondes

3. **Performance** :
   - Le système de multiples requêtes peut être lent pour les grandes périodes
   - Cache local implémenté avec TTL de 5 minutes pour améliorer les performances

---

## Améliorations futures possibles

1. **Paramètre `codes` optionnel** (Priorité BASSE) :
   - Ajouter support du paramètre `codes` pour filtrer les data points
   - Réduire la quantité de données retournées

2. **Support événements online/offline** (Optionnel) :
   - Tester `type=1,2,7` pour inclure les événements de connexion/déconnexion
   - Afficher ces événements dans la timeline

3. **Cache intelligent** :
   - Augmenter le TTL du cache pour les périodes anciennes (qui ne changent plus)
   - Système de cache progressif (données récentes = cache court, données anciennes = cache long)

4. **Sélecteur de codes DP** :
   - Interface pour choisir quel code DP afficher
   - Afficher plusieurs graphiques simultanément (température ET humidité)

---

## Références

- **Documentation officielle Tuya** : Device Management API (2024-06-26)
- **Analyse détaillée** : ANALYSE_DOC_OFFICIELLE_DEVICE_MANAGEMENT.md
- **Feature auto-détection** : FEATURE_AUTO_DETECT_DP_CODES.md
- **Exemple Python fonctionnel** : Code de référence avec `types=report` et timestamps en secondes (NON CONFORME à la doc officielle, mais fonctionne parfois selon la configuration du compte Tuya)

---

## Conclusion

✅ **Le code actuel est CORRECT et conforme à la documentation officielle Tuya.**

Les timestamps en millisecondes, le paramètre `type=7`, et la structure de parsing sont tous conformes à la documentation officielle Device Management API.

Si vous rencontrez des problèmes :
1. Vérifier les permissions API dans Tuya IoT Platform
2. Activer les API Products nécessaires (Device Management, Device Statistics)
3. Consulter les logs détaillés pour diagnostiquer les erreurs
4. Activer le mode diagnostic avec `SetDiagnosticMode(True)` pour plus de détails
