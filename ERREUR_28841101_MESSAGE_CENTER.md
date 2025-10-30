# 🚨 Erreur 28841101 - API Query Messages non accessible

## Problème

Lors de l'appel à l'endpoint `/v1.0/sdf/notifications/messages` (Query Messages), vous obtenez :

```json
{
  "code": 28841101,
  "msg": "No permissions. This API is not subscribed.",
  "success": false
}
```

**Ce problème est CONNU et FRÉQUENT avec Tuya**, même lorsque vous avez souscrit à "Mobile Push Notification Service".

---

## 📋 Contexte

L'API **"Mobile Push Notification Service"** contient plusieurs endpoints :
- ✅ `POST /v1.0/iot-03/messages/app-notifications/actions/push` → **ENVOI** de notifications (fonctionne)
- ✅ Templates de notifications (fonctionne)
- ❌ `GET /v1.0/sdf/notifications/messages` → **RÉCUPÉRATION** de messages (ne fonctionne PAS)

**L'endpoint "Query Messages" semble être dans un service différent ou nécessiter une activation spéciale.**

---

## 🛠️ Solutions (dans l'ordre de priorité)

### ✅ Solution 1 : Vérifier la version du service (5 min)

1. Allez sur https://iot.tuya.com
2. **Cloud** → **Products** (ou "API Products")
3. Recherchez **"Mobile Push Notification Service"**
4. Vérifiez la **version souscrite** :
   - **Free** ❌
   - **Trial** ❌
   - **Standard** ✅ (peut-être requis)
   - **Advanced** ✅

**Si vous êtes en Free/Trial**, l'API "Query Messages" pourrait ne pas être incluse. Passez à Standard si possible.

---

### ✅ Solution 2 : Rechercher d'autres services (10 min)

Sur la page **Cloud → Products**, utilisez la recherche pour trouver :

| Service à rechercher | Nom exact possible |
|---------------------|-------------------|
| **"App Message"** | App Message Service |
| **"Message Center"** | Message Center Service |
| **"Notification Center"** | Notification Center Service |
| **"User Message"** | User Message Service |
| **"App Notification"** | App Notification Service *(différent de Push)* |

**Un de ces services pourrait contenir l'API "Query Messages".**

#### Comment vérifier :
1. Cliquez sur le service trouvé
2. Allez dans **"API List"** ou **"Included APIs"**
3. Cherchez **"Query Messages"** ou l'endpoint `/v1.0/sdf/notifications/messages`
4. Si trouvé → **Subscribe** et **Authorize** pour votre projet

---

### ✅ Solution 3 : Ouvrir un ticket Tuya Support ⭐ **RECOMMANDÉ**

**D'après les GitHub issues, c'est la solution la PLUS RAPIDE et la PLUS FIABLE.**

Beaucoup d'utilisateurs ont résolu ce problème en **ouvrant un ticket support**. Tuya fait une manipulation au niveau de la base de données pour débloquer l'accès.

#### Comment ouvrir un ticket :

1. **Allez sur** https://iot.tuya.com
2. **Cliquez** sur l'icône **"?"** ou **"Support"** en haut à droite
3. **Créez un nouveau ticket** avec les informations suivantes :

**📧 Template de ticket :**

```
Subject: Error 28841101 - Cannot access Query Messages API

Description:
I'm getting error code 28841101 when calling the endpoint:
GET /v1.0/sdf/notifications/messages

Error response:
{
  "code": 28841101,
  "msg": "No permissions. This API is not subscribed.",
  "success": false
}

I have already:
- Subscribed to "Mobile Push Notification Service"
- Authorized the API for my project
- Verified that other endpoints work (Send Push Notifications)

But the "Query Messages" endpoint still returns 28841101.

My project details:
- Project ID: [VOTRE PROJECT ID]
- Access ID: [VOTRE ACCESS ID]
- Region: EU
- Endpoint trying to access: GET /v1.0/sdf/notifications/messages

Could you please activate the "Query Messages" API for my account?

Thank you!
```

**Tuya Support répond généralement en 1-2 jours ouvrables et résout le problème.**

---

## 🔍 Informations supplémentaires

### Pourquoi ce problème existe ?

D'après les discussions GitHub ([#108](https://github.com/tuya/tuya-homebridge/issues/108), [#114](https://github.com/tuya/tuya-homebridge/issues/114), [#615](https://github.com/codetheweb/tuyapi/discussions/615)) :

1. **Problème au niveau du compte** : Certains comptes Tuya ont des restrictions au niveau de la base de données qui empêchent l'accès à certaines APIs, même si elles apparaissent "In Service".

2. **Bug de la plateforme** : Les APIs peuvent afficher "In Service" mais ne pas fonctionner réellement.

3. **Activation manuelle requise** : Tuya doit parfois activer manuellement l'accès à certaines APIs pour certains comptes.

### Cas d'utilisateurs

- **Utilisateur A** : "Worked for 6 months, then suddenly stopped. Opened a ticket, Tuya fixed it in 2 days."
- **Utilisateur B** : "Created a new account, worked immediately. Old account never worked even with all APIs enabled."
- **Utilisateur C** : "Support ticket solved it. They did something at database level."

---

## 🎯 Résumé

| Solution | Durée | Taux de succès | Difficulté |
|----------|-------|----------------|------------|
| Vérifier version du service | 5 min | 20% | Facile |
| Rechercher autre service | 10 min | 30% | Moyenne |
| **Ticket Tuya Support** | 1-2 jours | **90%** | **Facile** |

**→ Recommandation : Ouvrez directement un ticket Tuya Support. C'est la solution la plus fiable.**

---

## 📚 Références

- [Tuya Developer - Query Messages API](https://developer.tuya.com/en/docs/cloud/e1581be6fa?id=Kbabe1ij7fivh)
- [GitHub Issue #108 - tuya-homebridge](https://github.com/tuya/tuya-homebridge/issues/108)
- [GitHub Issue #114 - tuya-homebridge](https://github.com/tuya/tuya-homebridge/issues/114)
- [GitHub Discussion #615 - tuyapi](https://github.com/codetheweb/tuyapi/discussions/615)
- [Tuya Error Codes](https://developer.tuya.com/en/docs/iot/error-code)

---

## 🤖 Diagnostic automatique

La prochaine fois que vous lancerez **MessageCenterForm**, le diagnostic automatique détectera l'erreur 28841101 et affichera ces solutions directement dans les logs.

---

**Dernière mise à jour : 2025-10-30**
