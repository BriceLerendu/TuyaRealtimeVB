# 📬 Noms Exacts des API pour le Message Center Tuya

## 🎯 Résumé Rapide

Pour que le **Message Center** fonctionne, vous devez souscrire à cette API :

**Nom exact : "App Push Notification Service"** (ou "Mobile Push Notification Service")

---

## 📋 API à Souscrire

### **App Push Notification Service** ✅

**Noms possibles dans la plateforme** :
- `App Push Notification Service`
- `Mobile Push Notification Service`
- `Mobile Push Notification`

**Endpoints concernés** :
- `GET /v1.0/sdf/notifications/messages` - Query Messages (celui qui échoue avec code 28841101)
- `PUT /v1.0/sdf/notifications/messages/actions-read` - Mark Messages as Read/Unread

**Documentation officielle** :
- https://developer.tuya.com/en/docs/cloud/e1581be6fa?id=Kbabe1ij7fivh
- https://developer.tuya.com/en/docs/cloud/app-push?id=Kaiuye3tb3yho

---

## 🔧 Comment Souscrire à l'API

### Méthode 1 : Via Projects → API Products (Recommandé)

1. Allez sur https://iot.tuya.com/
2. Sélectionnez votre projet (Cloud → Projects → Select your project)
3. Cliquez sur **API Products** ou **API Groups**
4. Cliquez sur **All Products** ou **Browse All**
5. Cherchez : **"App Push Notification Service"** ou **"Mobile Push"**
6. Cliquez sur **Subscribe**
7. Sélectionnez **Standard Version** (gratuit ou payant selon vos besoins)
8. Après souscription, **autorisez l'API pour votre projet** :
   - Retournez sur votre projet
   - Allez dans **Authorized APIs** ou **API Authorization**
   - Activez **App Push Notification Service**

### Méthode 2 : Via Cloud → My Service

1. Allez sur https://iot.tuya.com/
2. Cliquez sur **Cloud** → **Development** → **My Service**
3. Cherchez : **"App Push Notification Service"** ou **"Mobile Push"**
4. Si vous le trouvez :
   - Statut **"Disabled"** → Cliquez **"Enable"**
   - Statut **"Enabled"** → Déjà activé, vérifiez l'autorisation du projet

---

## ⚠️ Erreurs Actuelles et Leurs Causes

### Erreur 1 : Code 28841101
```
[28841101]: No permissions. This API is not subscribed.
Endpoint: /v1.0/sdf/notifications/messages
```

**Cause** : L'API "App Push Notification Service" n'est pas souscrite ou pas autorisée pour votre projet.

**Solution** : Suivre la procédure de souscription ci-dessus.

---

### Erreur 2 : Code 1108
```
[1108]: uri path invalid
Endpoint: /v1.0/users/{uid}/messages
```

**Cause** : Cet endpoint n'existe pas ou n'est pas valide dans l'API Tuya actuelle.

**Solution** : L'endpoint correct est `/v1.0/sdf/notifications/messages` (pas `/v1.0/users/{uid}/messages`).

---

## 🔍 Paramètres Requis pour l'API

L'endpoint `/v1.0/sdf/notifications/messages` accepte ces paramètres :

### Paramètres obligatoires
- **recipient_id** : L'ID du destinataire (votre UID Tuya) ⚠️ MANQUANT dans le code actuel

### Paramètres optionnels
- **message_type** : Type de message (1=Home, 2=Bulletin, 3=Alarm)
- **message_sub_type** : Sous-type de message
- **read_flag** : Filtrer par lu/non-lu (0=non lu, 1=lu)
- **page_no** : Numéro de page (défaut: 1)
- **page_size** : Nombre de messages par page (défaut: 10, max: 50)

### ⚠️ Problème dans le Code Actuel

Le code actuel dans `TuyaMessageCenter.vb` ligne 147 :
```vb
Dim url = $"{_cfg.OpenApiBase}{endpoint}?page_no={pageNo}&page_size={pageSize}"
```

**Il manque le paramètre `recipient_id` !**

**Correction nécessaire** :
```vb
Dim url = $"{_cfg.OpenApiBase}{endpoint}?recipient_id={_cfg.Uid}&page_no={pageNo}&page_size={pageSize}"
```

---

## 🔨 Corrections à Apporter au Code

### Fichier : TuyaMessageCenter.vb

**Ligne ~147** - Méthode `TryGetMessagesAsync`

**AVANT** :
```vb
Dim url = $"{_cfg.OpenApiBase}{endpoint}?page_no={pageNo}&page_size={pageSize}"
```

**APRÈS** :
```vb
' Ajouter le recipient_id qui est obligatoire selon la documentation
Dim url = $"{_cfg.OpenApiBase}{endpoint}?recipient_id={_cfg.Uid}&page_no={pageNo}&page_size={pageSize}"
```

---

**Ligne ~186** - Méthode `TryGetUserNotificationsAsync`

**PROBLÈME** : L'endpoint `/v1.0/users/{uid}/messages` n'existe pas.

**SOLUTION** : Supprimer cette méthode ou la remplacer par un autre endpoint valide.

**RECOMMANDATION** : Utiliser uniquement `/v1.0/sdf/notifications/messages` avec `recipient_id`.

---

## 📊 Format de Réponse Attendu

Après correction et activation de l'API, vous devriez recevoir :

```json
{
  "success": true,
  "result": [
    {
      "id": "message_id_123",
      "message_type": 1,
      "message_sub_type": 41,
      "title": "Titre du message",
      "content": "Contenu du message",
      "read_flag": 0,
      "create_time": 1234567890,
      "device_id": "vdevo123456",
      "device_name": "Mon appareil"
    }
  ],
  "t": 1234567890,
  "success": true
}
```

---

## 🧪 Plan de Test

### Étape 1 : Souscrire à l'API
1. Suivre la procédure de souscription ci-dessus
2. Vérifier que "App Push Notification Service" est activé
3. Vérifier l'autorisation dans votre projet

### Étape 2 : Corriger le Code
1. Modifier `TuyaMessageCenter.vb` ligne ~147
2. Ajouter `recipient_id={_cfg.Uid}` dans l'URL
3. Recompiler l'application

### Étape 3 : Tester
1. Lancer l'application
2. Ouvrir le Message Center (Menu Fichier → 📬 Centre de Messages Tuya)
3. Cliquer sur "🔄 Actualiser"
4. Vérifier les logs dans la console

**Résultat attendu** :
```
=== Récupération des messages du Message Center ===
Tentative d'appel API: https://openapi.tuyaeu.com/v1.0/sdf/notifications/messages?recipient_id=eu163490097472464a5I&page_no=1&page_size=50
📥 Réponse API complète:
{
  "success": true,
  "result": [...]
}
✅ Réponse API success=true
```

---

## 📋 Checklist de Vérification

Avant de continuer, vérifiez :

- [ ] **API souscrite** : "App Push Notification Service" est souscrit
- [ ] **API autorisée** : L'API est autorisée pour votre projet spécifique
- [ ] **Code corrigé** : Le paramètre `recipient_id` est ajouté à l'URL
- [ ] **UID configuré** : Votre UID est correct dans `appsettings.json`
- [ ] **Région correcte** : Votre `OpenApiBase` correspond à votre région (EU, US, CN, IN)

---

## 💡 Alternative : Si l'API n'est Pas Disponible

Si "App Push Notification Service" n'est vraiment pas disponible pour votre compte :

### Option 1 : Vérifier dans l'Application Mobile
1. Ouvrez l'application **Smart Life** ou **Tuya Smart** sur votre téléphone
2. Allez dans **Message Center** (icône cloche)
3. Vérifiez si vous avez des messages

Si vous voyez des messages sur mobile mais pas via l'API, c'est que l'API n'est pas activée.

### Option 2 : Utiliser les Logs d'Événements Pulsar
Les événements Pulsar (que vous recevez déjà en temps réel) contiennent certaines informations :
- Événements d'appareil (on/off, alarmes)
- Changements d'état
- Alertes

Vous pourriez afficher ces événements dans le Message Center au lieu des messages officiels.

---

## 🔗 Liens Utiles

- **Documentation Query Messages** : https://developer.tuya.com/en/docs/cloud/e1581be6fa?id=Kbabe1ij7fivh
- **Mobile Push Service** : https://developer.tuya.com/en/docs/cloud/app-push?id=Kaiuye3tb3yho
- **API Explorer** : https://iot.tuya.com/ → Cloud → API Explorer
- **My Service** : https://iot.tuya.com/ → Cloud → My Service

---

## 🆘 Besoin d'Aide ?

Si après avoir suivi ce guide le problème persiste :

1. Vérifiez que vous utilisez le bon **UID** dans `appsettings.json`
2. Testez l'API dans l'**API Explorer** de Tuya avec les bons paramètres
3. Vérifiez les **logs de la console** pour voir la réponse exacte de l'API
4. Contactez le support Tuya si l'API n'apparaît pas dans votre compte

---

**Voulez-vous que je corrige le code maintenant pour ajouter le paramètre `recipient_id` ?**
