# ✅ Vérification de l'API Device Log Query

Ce document vous guide pour vérifier si l'API **"Device Log Query"** fonctionne correctement avec votre compte Tuya.

---

## 📋 Noms Exacts des API Tuya à Souscrire

### 1️⃣ **Device Log Query** (que vous avez ✅)
- **Nom dans la plateforme Tuya** : `Device Log Query` ou `[Deprecate]Device Log Query`
- **Nom dans la documentation** : `Device Log Service`
- **Endpoints** :
  - `GET /v1.0/devices/{device_id}/logs`
  - `GET /v2.0/cloud/thing/{device_id}/report-logs`

### 2️⃣ **Statistics Service** (optionnel, nécessite configuration manuelle ⚠️)
- **Nom dans la plateforme** : `Statistics Service`
- **Endpoints** :
  - `GET /v1.0/devices/{device_id}/statistics/days`
  - `GET /v1.0/devices/{device_id}/statistics/hours`
  - Nécessite un ticket de support Tuya pour activation

---

## 🔬 Test Rapide (5 minutes)

### Option A : Via l'application (recommandé)

#### Étape 1 : Compiler et lancer
```bash
cd TuyaRealtimeVB
dotnet build
dotnet run
```

#### Étape 2 : Ouvrir l'historique d'un appareil
1. Dans l'application, cliquez sur un appareil qui a eu de l'activité récente
2. Cliquez sur le bouton **"Historique"** ou **"📊"**
3. Regardez les logs dans la console

#### Étape 3 : Interpréter les résultats

**✅ SI VOUS VOYEZ :**
```
📊 API v1.0 retourné 15 logs
✅ 15 logs récupérés pour vdevoXXXXXXX
```
→ **L'API fonctionne parfaitement !** 🎉

**❌ SI VOUS VOYEZ :**
```
[28841101]: No permissions. This API is not subscribed
⚠️ API v1.0: No permissions
```
→ L'API n'est pas activée (voir section "Activation" ci-dessous)

**⚠️ SI VOUS VOYEZ :**
```
✅ 0 logs récupérés pour vdevoXXXXXXX
```
→ L'API fonctionne mais l'appareil n'a pas eu d'activité récente. Essayez :
- D'allumer/éteindre l'appareil
- D'attendre quelques minutes
- De tester avec un autre appareil

---

### Option B : Test programmatique (avancé)

Un module de test `TestDeviceLogAPI.vb` a été créé. Pour l'utiliser :

#### 1. Ajouter le fichier au projet
```bash
# Le fichier TestDeviceLogAPI.vb est déjà créé dans le dossier racine
```

#### 2. Appeler le test depuis MainForm
Ajoutez ce code dans `MainForm.vb` après la connexion réussie :

```vb
' Après connexion réussie dans ConnectToTuya ou LoadDevices
Private Async Sub TestDeviceLogAPI_Click(sender As Object, e As EventArgs)
    If _devices Is Nothing OrElse _devices.Count = 0 Then
        MessageBox.Show("Aucun appareil disponible pour le test")
        Return
    End If

    ' Prendre le premier appareil pour le test
    Dim testDevice = _devices.First()

    ' Lancer le test
    Dim result = Await TestDeviceLogAPI.TestDeviceLogsAsync(
        _apiClient,
        _historyService,
        testDevice.Id
    )

    ' Afficher le résultat
    If result.AllTestsPassed Then
        MessageBox.Show("✅ L'API Device Log Query fonctionne !", "Test réussi", MessageBoxButtons.OK, MessageBoxIcon.Information)
    Else
        MessageBox.Show($"❌ Test échoué: {result.ErrorMessage}", "Test échoué", MessageBoxButtons.OK, MessageBoxIcon.Warning)
    End If
End Sub
```

---

## 🔧 Activation de l'API Device Log Query

Si vous obtenez l'erreur **28841101**, suivez ces étapes :

### Étape 1 : Vérifier sur la plateforme Tuya

1. Allez sur https://iot.tuya.com/
2. Cliquez sur **Cloud** → **Development** → **My Service**
3. Cherchez : **"Device Log Query"** ou **"[Deprecate]Device Log Query"**

### Étape 2 : Activer le service

**Si vous le trouvez :**
- Statut = **"Disabled"** → Cliquez sur **"Enable"**
- Statut = **"Enabled"** → C'est déjà activé, vérifiez les autorisations du projet

**Si vous ne le trouvez pas :**
- Cherchez aussi : **"Device Log Service"**
- Ou allez dans **Cloud** → **API Explorer** → Testez `/v1.0/devices/{device_id}/logs`

### Étape 3 : Autoriser l'API dans votre projet

1. Allez sur https://iot.tuya.com/
2. **Cloud** → **Development** → **Your Project**
3. Onglet **API Groups** ou **Authorized APIs**
4. Vérifiez que **"Device Management"** est coché
5. Si ce n'est pas le cas, ajoutez-le et sauvegardez

### Étape 4 : Retester

Attendez 1-2 minutes pour la propagation, puis relancez le test.

---

## 📊 Interprétation des Résultats du Test

### Test complet réussi ✅
```
📋 RÉSUMÉ DU TEST
✓ Logs API: ✅ OK
✓ Détection codes: ✅ OK
✓ Statistiques: ✅ OK

🎉 L'API Device Log Query FONCTIONNE !
```

**Signification :**
- L'API est correctement configurée
- Les logs sont récupérés avec succès
- Les codes DP sont détectés automatiquement
- Les statistiques peuvent être calculées

**Prochaines étapes :**
- Utiliser la fonctionnalité Historique normalement
- Aucune action supplémentaire requise

---

### Test partiellement réussi ⚠️
```
📋 RÉSUMÉ DU TEST
✓ Logs API: ✅ OK
✓ Détection codes: ⚠️  N/A
✓ Statistiques: ⚠️  N/A
```

**Signification :**
- L'API fonctionne mais aucun code DP détecté
- L'appareil n'a pas eu d'activité récente
- Ou l'appareil ne remonte pas de données numériques

**Actions recommandées :**
1. Allumer/éteindre l'appareil plusieurs fois
2. Attendre 5 minutes
3. Retester avec un autre appareil (prise électrique, capteur, etc.)

---

### Test échoué ❌
```
📋 RÉSUMÉ DU TEST
✓ Logs API: ❌ ÉCHEC
✓ Détection codes: ⚠️  N/A
✓ Statistiques: ⚠️  N/A

⚠️  L'API Device Log Query ne fonctionne pas correctement.
```

**Signification :**
- L'API n'est pas activée ou accessible
- Erreur 28841101 probable

**Actions requises :**
1. Vérifier l'activation du service (voir section "Activation" ci-dessus)
2. Vérifier les autorisations du projet
3. Vérifier les credentials (Client ID, Secret)
4. Contacter le support Tuya si le problème persiste

---

## 🆘 Codes d'Erreur Courants

| Code | Message | Signification | Solution |
|------|---------|---------------|----------|
| **28841101** | No permissions. This API is not subscribed | L'API n'est pas activée pour votre projet | Activer "Device Log Query" dans My Service |
| **1004** | sign invalid | Signature incorrecte (problème d'authentification) | Vérifier les credentials et la signature |
| **1010** | token invalid | Token expiré ou invalide | Le token sera automatiquement rafraîchi |
| **1106** | permission deny | Permissions insuffisantes | Vérifier les API Groups autorisées |
| **2406** | skill id invalid | Device ID incorrect | Vérifier que le device_id existe |

---

## 📝 Informations Supplémentaires

### Version Gratuite vs Payante

**Version GRATUITE (Device Log Query)** :
- ✅ Rétention de **7 jours** de logs
- ✅ Disponible directement dans la plateforme
- ✅ Suffisant pour l'affichage d'historique 24h

**Version PAYANTE (Device Log Service)** :
- Rétention de 1 mois à 3 ans
- Configuration par Product ID (PID)
- Nécessite de contacter le support Tuya après souscription

### Alternative : Historique Local

Si l'API ne fonctionne vraiment pas, une alternative existe :
- Stocker les événements Pulsar en temps réel dans une base SQLite locale
- Aucun appel API supplémentaire
- Rétention illimitée
- Plus rapide que l'API Tuya

Documentation : Voir `GRAPHIQUES_DEPUIS_LOGS.md`

---

## 🔗 Liens Utiles

- **Documentation officielle** : https://developer.tuya.com/en/docs/iot/device-log-service
- **API Explorer** : https://iot.tuya.com/ → Cloud → API Explorer
- **My Service** : https://iot.tuya.com/ → Cloud → My Service
- **Support Tuya** : https://service.tuya.com/

---

## ✅ Checklist Finale

Avant de continuer, vérifiez que :

- [ ] Vous avez le service **"Device Log Query"** dans My Service
- [ ] Le service est **Enabled** (activé)
- [ ] Votre projet a les autorisations **Device Management**
- [ ] Le test retourne au moins quelques logs (> 0)
- [ ] Vous avez testé avec un appareil qui a eu de l'activité récente

Si tout est coché, vous êtes prêt à utiliser l'historique ! 🎉

---

**Besoin d'aide ?**
- Vérifiez les logs de l'application dans la console
- Regardez le fichier `TEST_RAPIDE_API_LOGS.md` pour un test encore plus rapide
- Consultez `NOMS_OFFICIELS_APIS_TUYA.md` pour plus de détails sur les API
