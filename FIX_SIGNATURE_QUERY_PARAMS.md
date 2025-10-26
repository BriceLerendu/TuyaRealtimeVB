# Fix : Signature API invalide - Query params non triés

## 🐛 Problème identifié

### Symptômes

Toutes les requêtes vers `/v1.0/devices/{device_id}/logs` retournaient :
```
⚠️ API v1.0 success = false, msg = sign invalid
```

### Cause racine

Les **query parameters n'étaient PAS triés** par ordre alphabétique avant de calculer la signature HMAC-SHA256.

Selon la [documentation officielle Tuya sur la signature](https://developer.tuya.com/en/docs/iot/singnature?id=Ka43a5mtx1gsc) :

> The URL is the Form parameter in Path + Query + Body, **with keys sorted according to the dictionary**.

### Exemple concret

**Requête générée** (INCORRECT) :
```
GET /v1.0/devices/bfc9cf5e938bdf6ac6gidc/logs?type=7&start_time=1729900000000&end_time=1729914000000&size=100
```

**Requête attendue** (CORRECT) :
```
GET /v1.0/devices/bfc9cf5e938bdf6ac6gidc/logs?end_time=1729914000000&size=100&start_time=1729900000000&type=7
                                                 ↑ Paramètres triés alphabétiquement ↑
```

**Impact** : La signature HMAC-SHA256 calculée avec les params non triés ne correspondait pas à celle attendue par l'API Tuya → `sign invalid`.

---

## ✅ Solution implémentée

### Code modifié : `TuyaApiClient.vb`

#### 1. Nouvelle fonction `SortQueryParameters`

```vb
''' <summary>
''' Trie les query parameters par ordre alphabétique selon la spec Tuya
''' </summary>
Private Function SortQueryParameters(pathAndQuery As String) As String
    ' Séparer le path des query params
    Dim questionMarkIndex = pathAndQuery.IndexOf("?"c)
    If questionMarkIndex = -1 Then
        ' Pas de query params
        Return pathAndQuery
    End If

    Dim path = pathAndQuery.Substring(0, questionMarkIndex)
    Dim queryString = pathAndQuery.Substring(questionMarkIndex + 1)

    ' Parser et trier les paramètres par ordre alphabétique
    Dim params = queryString.Split("&"c) _
        .OrderBy(Function(p) p) _
        .ToArray()

    ' Reconstruire le path avec les params triés
    Return path & "?" & String.Join("&", params)
End Function
```

**Fonctionnement** :
1. Sépare le path des query params au niveau du `?`
2. Split les params sur `&`
3. Trie alphabétiquement avec `OrderBy`
4. Reconstruit la chaîne complète

**Exemple de transformation** :
```
Input : /v1.0/devices/abc/logs?type=7&start_time=123&end_time=456&size=100
Output: /v1.0/devices/abc/logs?end_time=456&size=100&start_time=123&type=7
```

#### 2. Modification de `CalculateSignature`

```vb
Private Function CalculateSignature(httpMethod As String, bodyHash As String, path As String,
                                   token As String, timestamp As Long, nonce As String) As String
    ' ✅ CORRECTION CRITIQUE : Trier les query params selon la doc Tuya
    ' Source: https://developer.tuya.com/en/docs/iot/singnature?id=Ka43a5mtx1gsc
    ' Les query params doivent être triés par ordre alphabétique pour la signature
    Dim sortedPath = SortQueryParameters(path)

    ' Construire stringToSign selon le protocole Tuya :
    ' METHOD + "\n" + ContentSHA256 + "\n" + Headers + "\n" + URL
    Dim stringToSign = httpMethod & vbLf & bodyHash & vbLf & "" & vbLf & sortedPath

    ' ... reste du code inchangé
End Function
```

**Changement** : Ajout de `Dim sortedPath = SortQueryParameters(path)` avant de construire le `stringToSign`.

---

## 📋 Spécification Tuya : Structure de `stringToSign`

Selon la documentation officielle :

```
stringToSign = HTTPMethod + "\n" + Content-SHA256 + "\n" + Optional_Signature_key + "\n" + URL
```

Où :
- **HTTPMethod** : GET, POST, PUT, DELETE
- **Content-SHA256** : Hash SHA256 du body (ou `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` si vide)
- **Optional_Signature_key** : Headers optionnels triés (vide dans notre cas)
- **URL** : Path + Query params **triés alphabétiquement**

**Exemple de `stringToSign` valide** :
```
GET
e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855

/v1.0/devices/abc/logs?end_time=456&size=100&start_time=123&type=7
```

---

## 🔍 Impact et tests

### Endpoints affectés

Tous les endpoints avec query parameters :
- ✅ `/v1.0/devices/{device_id}/logs?...`
- ✅ `/v1.0/devices?page_no=...&page_size=...`
- ✅ `/v1.0/users/{uid}/devices?from=...&page_no=...`

### Tests recommandés

1. **Test de l'API logs** (principal fix) :
   ```
   GET /v1.0/devices/{device_id}/logs?type=7&start_time={ts1}&end_time={ts2}&size=100
   ```
   **Attendu** : `success: true` au lieu de `sign invalid`

2. **Test sans query params** (régression) :
   ```
   GET /v1.0/devices/{device_id}
   ```
   **Attendu** : Toujours fonctionnel

3. **Test avec 1 seul param** :
   ```
   GET /v1.0/devices?schema=testApp
   ```
   **Attendu** : Pas de changement de comportement

### Résultats attendus

**AVANT** (avec query params non triés) :
```
[HistoryService]     ⚠️ API v1.0 success = false, msg = sign invalid
[HistoryService]     ⚠️ API v1.0 success = false, msg = sign invalid
[HistoryService]   ⚠️ 0 logs après 13 tranches
```

**APRÈS** (avec query params triés) :
```
[HistoryService]     📊 API v1.0 retourné 45 logs
[HistoryService]   ✅ 450 logs uniques après 13 tranches
[HistoryService] ✅ 450 logs récupérés pour bfc9cf5e938bdf6ac6gidc
```

---

## 📚 Références

### Documentation officielle Tuya

1. **Sign Requests (Documentation principale)** :
   https://developer.tuya.com/en/docs/iot/singnature?id=Ka43a5mtx1gsc

2. **Sign Requests for Cloud Authorization (Nouvelle version)** :
   https://developer.tuya.com/en/docs/iot/new-singnature?id=Kbw0q34cs2e5g

3. **Verify Signature Result (Outil de debug)** :
   https://developer.tuya.com/en/docs/iot/check-postman-sign?id=Kavfn3820sxg4

### Points clés de la spec

**Structure de signature complète** :
```
sign = HMAC-SHA256(client_id + access_token + timestamp + nonce + stringToSign, client_secret)
```

**Ordre des query params** :
> If Query or Form parameters are empty, the URL is just the Path value and a connector '?' is not required, otherwise:
> ```
> String url = Path + "?" + Key1 + "=" + Value1 + "&" + Key2 + "=" + Value2 + ... + KeyN + "=" + ValueN
> ```
> **with keys sorted according to the dictionary**

---

## 🎯 Checklist de validation

- [x] Fonction `SortQueryParameters` ajoutée
- [x] `CalculateSignature` modifiée pour utiliser le tri
- [x] Documentation de référence Tuya consultée
- [x] Exemple de transformation avant/après documenté
- [ ] **Tests sur application Windows** (à faire)
- [ ] **Vérification logs API** (devrait retourner `success: true`)
- [ ] **Test récupération historique** (graphiques devraient s'afficher)

---

## 🚀 Déploiement

### Étapes de compilation et test

1. Ouvrir le projet dans **Visual Studio** sur Windows
2. Compiler le projet (Build → Rebuild Solution)
3. Lancer l'application
4. Cliquer sur le bouton **📊 Historique** d'un appareil
5. Vérifier dans les logs :
   - ✅ Pas de message `sign invalid`
   - ✅ Message `📊 API v1.0 retourné X logs`
   - ✅ Graphiques affichés

### Rollback si nécessaire

Si le problème persiste, les commits peuvent être annulés :
```bash
git revert HEAD
```

---

## 📝 Notes additionnelles

### Historique des tentatives précédentes

Plusieurs commits avaient tenté de résoudre le problème `sign invalid` :

1. **Commit 0ed21a8** : "utiliser AbsolutePath sans query params" → REVERT
2. **Commit a67525e** : "utiliser Path sans query params" → REVERT
3. **Commit a710fdd** : "utiliser types=report au lieu de type=7" → INCORRECT

**Raison des échecs** : Ces tentatives retiraient les query params de la signature au lieu de les trier. Selon la spec Tuya, les query params **doivent être inclus** dans la signature, mais **triés alphabétiquement**.

### Pourquoi ça marchait sans query params ?

Les endpoints sans query params (ex: `GET /v1.0/devices/{id}`) fonctionnaient car :
- Pas de paramètres à trier → ordre toujours correct
- La signature était donc correcte

Les endpoints **avec** query params échouaient car :
- L'ordre des params dans l'URL générée était arbitraire (ordre d'ajout)
- La signature calculée ne correspondait pas à celle attendue par Tuya

---

## ✅ Conclusion

**Problème** : Query parameters non triés → signature invalide
**Solution** : Tri alphabétique des query params avant calcul de signature
**Impact** : Tous les endpoints avec query params (logs, listes paginées, etc.)
**Conformité** : Respecte maintenant la spec officielle Tuya

Cette correction devrait résoudre définitivement les erreurs `sign invalid` sur l'API logs et autres endpoints avec query parameters.
