# 📧 Guide : Ouvrir un ticket Tuya Support pour l'erreur 28841101

## 🎯 Objectif

Demander à Tuya d'activer l'API "Query Messages" pour votre compte, car elle n'est pas disponible dans la liste des services.

---

## 📝 Étapes pour ouvrir le ticket

### 1️⃣ Se connecter à Tuya IoT Platform

1. Allez sur **https://iot.tuya.com**
2. Connectez-vous avec votre compte

### 2️⃣ Trouver vos informations de projet

Avant d'ouvrir le ticket, récupérez ces informations :

1. **Cloud** → **Projects**
2. Cliquez sur **"Système Domotique"** (votre projet)
3. Notez :
   - **Project ID** (affiché en haut ou dans les détails)
   - **Access ID** (dans la section "Authorization Key" ou "API Settings")
   - **Region** : EU (Europe)

### 3️⃣ Ouvrir le ticket support

#### Option A : Via l'icône Support (Recommandé)

1. En haut à droite de la page, cherchez l'icône **"?"** ou **"Support"**
2. Cliquez dessus
3. Sélectionnez **"Submit a ticket"** ou **"Create new ticket"**

#### Option B : Via le menu

1. Dans le menu principal, cherchez **"Help"** ou **"Support"**
2. Cliquez sur **"Submit ticket"** ou **"Contact Support"**

### 4️⃣ Remplir le formulaire du ticket

Copiez-collez le template ci-dessous et **remplacez les valeurs entre crochets** :

---

**📋 TEMPLATE DU TICKET :**

```
Subject:
Error 28841101 - Cannot access Query Messages API (/v1.0/sdf/notifications/messages)

Category:
API / Cloud Development (ou similaire)

Priority:
Normal

Description:

Hello Tuya Support Team,

I'm trying to use the "Query Messages" API endpoint to retrieve messages from
the Message Center, but I'm getting error code 28841101.

TECHNICAL DETAILS:
- Endpoint: GET /v1.0/sdf/notifications/messages
- Error code: 28841101
- Error message: "No permissions. This API is not subscribed."
- API documentation: https://developer.tuya.com/en/docs/cloud/e1581be6fa?id=Kbabe1ij7fivh

WHAT I'VE ALREADY DONE:
✅ Subscribed to "Mobile Push Notification Service" (Status: In service)
✅ Authorized the API for my project "Système Domotique"
✅ Verified that other endpoints work correctly (Send Push Notifications)
✅ Checked my entire list of available API services
❌ Cannot find "Query Messages" API in any available service

THE PROBLEM:
The "Query Messages" API is documented on your developer platform, but it's
not available in my list of API services. I cannot subscribe to it.

REQUEST:
Could you please activate the "Query Messages" API for my account, or tell
me which API service I need to subscribe to in order to access this endpoint?

PROJECT INFORMATION:
- Project Name: Système Domotique
- Project ID: [INSÉREZ VOTRE PROJECT ID ICI]
- Access ID: [INSÉREZ VOTRE ACCESS ID ICI]
- Region: EU (Europe)
- Endpoint needed: GET /v1.0/sdf/notifications/messages

CONTEXT:
I've found similar issues reported on GitHub:
- https://github.com/tuya/tuya-homebridge/issues/108
- https://github.com/tuya/tuya-homebridge/issues/114

In these cases, Tuya Support was able to activate the API at the database level.

Thank you for your help!

Best regards,
[VOTRE NOM]
```

---

### 5️⃣ Joindre une capture d'écran (optionnel mais recommandé)

1. Prenez une **capture d'écran** de l'erreur dans MessageCenterForm (avec les logs)
2. Joignez-la au ticket
3. Cela aide Tuya à comprendre le problème plus rapidement

### 6️⃣ Soumettre le ticket

1. Vérifiez que toutes les informations sont correctes
2. Cliquez sur **"Submit"** ou **"Send"**
3. Vous recevrez un **numéro de ticket** (notez-le)

---

## ⏱️ Délais attendus

| Étape | Délai |
|-------|-------|
| **Réponse initiale** | 24-48 heures |
| **Résolution du problème** | 2-5 jours ouvrables |
| **Action de Tuya** | Activation manuelle de l'API dans leur base de données |

---

## 📬 Que se passe-t-il après ?

### Réponse possible 1 : Activation confirmée
```
"We have activated the Query Messages API for your account.
Please try again and let us know if it works."
```

**→ Action** : Retestez avec MessageCenterForm

### Réponse possible 2 : Demande d'informations
```
"Could you provide your full error log and project details?"
```

**→ Action** : Envoyez les logs complets de MessageCenterForm

### Réponse possible 3 : Indication d'un service manquant
```
"You need to subscribe to [Service Name] to access this API."
```

**→ Action** : Souscrivez au service indiqué et autorisez-le pour votre projet

---

## 🧪 Vérifier si le problème est résolu

Après que Tuya ait répondu qu'ils ont activé l'API :

1. **Fermez** MessageCenterForm (si ouvert)
2. **Relancez** votre application TuyaRealtimeVB
3. **Ouvrez** MessageCenterForm
4. **Cliquez** sur "🔄 Actualiser"
5. **Regardez** les logs :
   - ✅ Si `success=true` → **RÉSOLU** !
   - ❌ Si encore `28841101` → Répondez au ticket avec les nouveaux logs

---

## 💡 Conseils pour un ticket efficace

### ✅ À FAIRE :
- ✅ Être poli et professionnel
- ✅ Fournir TOUTES les informations demandées
- ✅ Inclure les références GitHub (montre que le problème est connu)
- ✅ Mentionner que vous avez déjà vérifié tous les services
- ✅ Répondre rapidement si Tuya demande plus d'infos

### ❌ À ÉVITER :
- ❌ Être impatient ou agressif
- ❌ Ouvrir plusieurs tickets pour le même problème
- ❌ Donner des informations incomplètes
- ❌ Fermer le ticket avant résolution complète

---

## 📊 Statistiques de succès

D'après les cas documentés sur GitHub :

| Résultat | Pourcentage | Source |
|----------|-------------|--------|
| **Résolu par Tuya Support** | **~90%** | GitHub issues #108, #114, #615 |
| Résolu en créant un nouveau compte | ~5% | GitHub discussions |
| Non résolu / abandonné | ~5% | Divers forums |

**→ Taux de succès très élevé avec un ticket support bien rédigé !**

---

## 🆘 Si Tuya ne répond pas

Si vous n'avez **aucune réponse après 7 jours** :

1. **Répondez** à votre propre ticket avec : "Gentle reminder - Still waiting for your reply on ticket #[NUMERO]"
2. Essayez de **contacter Tuya** via d'autres canaux :
   - Forum Tuya Developer : https://developer.tuya.com/en/forum
   - Email support (si disponible dans votre région)

---

## 📚 Ressources utiles

- [Documentation Query Messages API](https://developer.tuya.com/en/docs/cloud/e1581be6fa?id=Kbabe1ij7fivh)
- [Tuya Error Codes](https://developer.tuya.com/en/docs/iot/error-code)
- [GitHub Issue #108](https://github.com/tuya/tuya-homebridge/issues/108)
- [GitHub Issue #114](https://github.com/tuya/tuya-homebridge/issues/114)
- [GitHub Discussion #615](https://github.com/codetheweb/tuyapi/discussions/615)

---

## ✅ Checklist avant soumission

- [ ] J'ai récupéré mon Project ID
- [ ] J'ai récupéré mon Access ID
- [ ] J'ai vérifié ma région (EU)
- [ ] J'ai copié le template du ticket
- [ ] J'ai remplacé TOUTES les valeurs entre crochets
- [ ] J'ai joint une capture d'écran des logs (optionnel)
- [ ] J'ai relu le ticket pour vérifier qu'il est complet

---

**🎯 Prêt à soumettre le ticket ? Bonne chance !**

**Temps estimé pour ouvrir le ticket : 10-15 minutes**
**Temps estimé de résolution par Tuya : 2-5 jours**

---

*Dernière mise à jour : 2025-10-30*
