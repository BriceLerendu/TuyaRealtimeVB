# 🔧 Solution : Erreur API Message Center (Code 28841101)

## 🎯 Problème Identifié

**Erreur reçue :**
```json
{
  "code": 28841101,
  "msg": "No permissions. This API is not subscribed.",
  "success": false
}
```

**Cause :** L'API "App Push Notification Service" n'est **pas souscrite** dans votre compte Tuya Cloud.

**Endpoint concerné :** `GET /v1.0/sdf/notifications/messages`

---

## ✅ État du Code

Le code est **correct** et contient déjà le paramètre obligatoire `recipient_id` :

```vb
' TuyaMessageCenter.vb:152
Dim url = $"{_cfg.OpenApiBase}{endpoint}?recipient_id={_cfg.Uid}&page_no={pageNo}&page_size={pageSize}"
```

Le problème est **uniquement au niveau de la souscription API** dans votre compte Tuya IoT.

---

## 🚀 Solution 1 : Souscrire à l'API (Recommandé)

### Étape 1 : Accéder à la Plateforme Tuya IoT

1. Allez sur **https://iot.tuya.com/**
2. Connectez-vous avec votre compte

### Étape 2 : Sélectionner Votre Projet

1. Cliquez sur **Cloud** → **Development** → **Projects**
2. Sélectionnez le projet que vous utilisez (celui avec votre Access ID/Secret)

### Étape 3 : Rechercher l'API

1. Cliquez sur **API Products** (ou **API Groups**)
2. Cliquez sur **Browse All** ou **All Products**
3. Utilisez la barre de recherche pour trouver :
   - **"App Push Notification Service"**
   - ou **"Mobile Push Notification Service"**
   - ou **"Push Notification"**

### Étape 4 : Souscrire à l'API

1. Cliquez sur l'API trouvée
2. Cliquez sur **Subscribe** (ou **Request Trial**)
3. Sélectionnez le plan :
   - **Free Trial** (essai gratuit)
   - **Standard Version** (version standard)
   - **Professional Version** (version pro, si disponible)

### Étape 5 : Autoriser l'API pour Votre Projet

1. Retournez sur votre projet
2. Allez dans **Authorized APIs** (ou **API Authorization**)
3. Vérifiez que **"App Push Notification Service"** apparaît dans la liste
4. Si ce n'est pas le cas, cliquez sur **Authorize** ou activez le bouton

### Étape 6 : Attendre l'Activation (Si Nécessaire)

- Certaines APIs peuvent nécessiter quelques minutes pour s'activer
- Vérifiez que le statut passe de "Pending" à "Active"

### Étape 7 : Tester l'Application

1. Redémarrez votre application VB.NET
2. Ouvrez le **Message Center** (Menu Fichier → 📬 Centre de Messages Tuya)
3. Cliquez sur **🔄 Actualiser**
4. Vérifiez les logs dans la console

**Résultat attendu :**
```
=== Récupération des messages du Message Center ===
Tentative d'appel API: https://openapi.tuyaeu.com/v1.0/sdf/notifications/messages?recipient_id=eu163490097472464a5I&page_no=1&page_size=50
📥 Réponse API complète:
{
  "success": true,
  "result": [
    {
      "id": "msg123",
      "title": "Titre du message",
      "content": "Contenu...",
      ...
    }
  ]
}
✅ Réponse API success=true
```

---

## 🔄 Solution 2 : Utiliser les Device Logs (Alternative)

Si l'API Push Notification n'est **pas disponible** pour votre compte (restrictions régionales, type de compte, etc.), vous pouvez utiliser les **Device Logs** comme alternative.

### Avantages des Device Logs

✅ **Déjà fonctionnel** dans votre application
✅ **Affiche les événements des appareils** :
   - Alarmes de sécurité (serrures, capteurs)
   - Changements d'état (on/off, température, etc.)
   - Historique des actions
✅ **Pas de souscription API supplémentaire nécessaire**

### Comment Accéder aux Device Logs

1. Dans votre application, les logs des appareils sont déjà visibles dans le **panneau de logs** du MessageCenterForm
2. Le fichier `TestDeviceLogAPI.vb` permet de tester cette fonctionnalité

### Endpoints Device Logs Disponibles

```vb
' Logs généraux d'un appareil
GET /v1.0/devices/{device_id}/logs

' Logs d'alarmes (serrures intelligentes)
GET /v1.0/devices/{device_id}/door-lock/alarm-logs

' Historique des événements (déjà implémenté dans votre code)
GET /v1.0/devices/{device_id}/report-logs
```

### Affichage dans le Message Center

Vous pourriez modifier `TuyaMessageCenter.vb` pour combiner :
1. Les messages Push (si API activée)
2. Les Device Logs (toujours disponibles)
3. Les événements Pulsar en temps réel

Cela donnerait une vue complète des "messages" de vos appareils.

---

## 📊 Solution 3 : Utiliser les Événements Pulsar

Vous recevez déjà des événements en temps réel via **Pulsar**. Ces événements pourraient être affichés comme "messages" dans le Message Center.

### Types d'Événements Pulsar

- **Status Events** : Changements d'état (on/off, température, etc.)
- **Alarm Events** : Alarmes de sécurité
- **Device Online/Offline** : Statut de connexion
- **Data Point Updates** : Mises à jour de valeurs

### Implémentation Possible

Modifier `MessageCenterForm.vb` pour :
1. Écouter les événements Pulsar
2. Les convertir en objets `TuyaMessage`
3. Les afficher dans le ListBox du Message Center

**Avantage :** Affichage en **temps réel** (pas besoin d'actualiser)

---

## 📋 Checklist de Vérification

Avant de continuer, vérifiez :

- [ ] **Compte Tuya IoT** : Vous avez accès à https://iot.tuya.com/
- [ ] **Projet sélectionné** : Vous avez sélectionné le bon projet
- [ ] **API recherchée** : "App Push Notification Service" existe dans votre région
- [ ] **API souscrite** : Vous avez cliqué sur "Subscribe"
- [ ] **API autorisée** : L'API apparaît dans "Authorized APIs" de votre projet
- [ ] **UID correct** : Votre `appsettings.json` contient le bon UID
- [ ] **Région correcte** : Votre `OpenApiBase` correspond à votre région (EU, US, CN, IN)

---

## 🔍 Vérification de l'UID

Assurez-vous que votre fichier `appsettings.json` contient le bon UID :

```json
{
  "Tuya": {
    "AccessId": "votre_access_id",
    "AccessSecret": "votre_access_secret",
    "Uid": "eu163490097472464a5I",  // ⚠️ Vérifiez cette valeur
    "OpenApiBase": "https://openapi.tuyaeu.com"
  }
}
```

### Comment Trouver Votre UID

1. Allez sur https://iot.tuya.com/
2. Cliquez sur **Cloud** → **Development** → **Projects**
3. Sélectionnez votre projet
4. L'UID est affiché dans les informations du projet
5. Ou utilisez l'API `GET /v1.0/users` pour obtenir votre UID

---

## 🧪 Test de l'API via API Explorer

Si vous voulez tester l'API avant de modifier le code :

1. Allez sur https://iot.tuya.com/
2. Cliquez sur **Cloud** → **API Explorer**
3. Recherchez l'endpoint : `GET /v1.0/sdf/notifications/messages`
4. Remplissez les paramètres :
   - **recipient_id** : votre UID (ex: eu163490097472464a5I)
   - **page_no** : 1
   - **page_size** : 10
5. Cliquez sur **Send Request**

**Résultat attendu si l'API est activée :**
```json
{
  "success": true,
  "result": [...]
}
```

**Résultat si l'API n'est pas activée :**
```json
{
  "code": 28841101,
  "msg": "No permissions. This API is not subscribed.",
  "success": false
}
```

---

## 🔗 Documentation Officielle

- **Query Messages API** : https://developer.tuya.com/en/docs/cloud/e1581be6fa?id=Kbabe1ij7fivh
- **App Push Service** : https://developer.tuya.com/en/docs/cloud/app-push?id=Kaiuye3tb3yho
- **API Products** : https://developer.tuya.com/en/docs/iot/api-products?id=K9m1dl5ey1wst
- **Device Logs** : https://developer.tuya.com/en/docs/cloud/device-log?id=K9m1dld43tfqj

---

## 💡 Recommandations

1. **Essayez d'abord la Solution 1** (souscrire à l'API) car c'est la méthode officielle
2. **Si l'API n'est pas disponible**, utilisez la Solution 2 (Device Logs) qui est déjà fonctionnelle
3. **Pour du temps réel**, combinez les Solutions 2 et 3 (Device Logs + Pulsar)

---

## 🆘 Support

Si après avoir suivi ce guide le problème persiste :

1. **Vérifiez les restrictions régionales** : Certaines APIs ne sont pas disponibles dans toutes les régions
2. **Vérifiez le type de compte** : Certaines APIs nécessitent un compte Developer ou Enterprise
3. **Contactez le support Tuya** : https://service.console.tuya.com/
4. **Vérifiez les logs de la console** pour voir la réponse exacte de l'API

---

## 📝 Résumé Rapide

| Solution | Difficulté | Fonctionnalités | Disponibilité |
|----------|-----------|-----------------|---------------|
| **API Push Notification** | Moyenne | Messages complets (famille, alarmes, bulletins) | Nécessite souscription |
| **Device Logs** | Facile | Logs et alarmes des appareils | ✅ Déjà disponible |
| **Événements Pulsar** | Facile | Événements en temps réel | ✅ Déjà disponible |

**Recommandation :** Souscrire à l'API Push Notification (Solution 1) pour avoir accès à toutes les fonctionnalités du Message Center de l'application Smart Life.

---

Voulez-vous que je vous aide à :
1. Implémenter l'affichage des Device Logs dans le Message Center ?
2. Combiner les événements Pulsar avec le Message Center ?
3. Autre chose ?
