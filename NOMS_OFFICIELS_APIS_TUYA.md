# ✅ Noms Officiels des APIs Tuya (Documentation 2024-2025)

## 🔍 Résultats de Recherche dans la Documentation Tuya

Voici les **noms officiels exacts** des APIs manquantes selon la documentation Tuya mise à jour en 2024-2025 :

---

## 1️⃣ Pour les Logs d'Événements

### Nom Officiel : **"Device Log Service"** ✅

**Endpoints concernés** :
- `GET /v1.0/devices/{device_id}/logs`
- `GET /v1.0/iot-03/devices/{device_id}/logs`
- `GET /v1.0/iot-03/devices/{device_id}/report-logs`

### 📦 Offres Disponibles

**Version GRATUITE** :
- ✅ Rétention de **7 jours** de logs
- ✅ Disponible directement dans Tuya Developer Platform
- ✅ Accès via : Product → Device → Device Logs

**Versions PAYANTES** :
- Rétention de 1 mois, 3 mois, 6 mois, 1 an, 2 ans ou 3 ans
- Configuration par PID (Product ID)
- Après souscription, **contacter le support Tuya** pour activer les APIs payantes

### ⚠️ Information Importante

> **La version gratuite (7 jours) et la version payante utilisent des APIs DIFFÉRENTES !**
>
> Après souscription à l'édition payante, vous devez contacter le support client Tuya pour obtenir les APIs de débogage de l'édition payante.

### 🔗 Documentation Officielle
- https://developer.tuya.com/en/docs/iot/device-log-service

---

## 2️⃣ Pour les Statistiques de Consommation

### Nom Officiel : **"Statistics Service"** ✅

**Endpoints concernés** :
- `GET /v1.0/devices/{device_id}/statistics/days`
- `GET /v1.0/devices/{device_id}/statistics/hours`
- `GET /v1.0/devices/{device_id}/statistics/weeks`
- `GET /v1.0/devices/{device_id}/statistics/months`

### 🎫 Activation Requise

**⚠️ IMPORTANT** : Cette API nécessite une **configuration manuelle** par Tuya.

**Procédure d'activation** :
1. Soumettre un **ticket de support** (service ticket) avec :
   - Votre compte associé au produit
   - Le **Product ID (PID)** de vos appareils
   - Le **Data Point ID (DP ID)** qui nécessite les statistiques (ex: `cur_power`, `add_ele`)

2. Le support Tuya configurera le produit avec ces fonctionnalités

3. Une fois configuré, l'API fonctionnera pour tous les appareils de ce PID

### 📋 Prérequis

- ✅ Souscription à **IoT Core** (que vous avez déjà !)
- ✅ Pack de ressources de base actif
- ✅ Configuration produit par le support Tuya

### 🔗 Documentation Officielle
- https://developer.tuya.com/en/docs/iot/server?id=K9wj1rwrr2rsg
- https://developer.tuya.com/en/docs/cloud/device-data-statistic

---

## ❌ Service que Vous Avez DÉJÀ mais qui N'est PAS le Bon

### "Data Dashboard Service" (Vous l'avez déjà)

**Ce que ça fait** :
- Statistiques **globales** de tous les appareils
- Aperçu des données au niveau de l'application
- Distribution géographique des appareils actifs
- Nombre d'appareils actifs par jour

**Endpoints** :
- `GET /v1.0/statistics-datas-survey` (aperçu global)
- Pas d'endpoint `/v1.0/devices/{id}/statistics/days` ❌

**Pourquoi ce n'est PAS suffisant** :
- ❌ Ne permet PAS de récupérer les statistiques **par appareil**
- ❌ Données agrégées uniquement (tous appareils confondus)
- ✅ Utile pour un dashboard global, mais pas pour l'historique individuel

---

## 🎯 Actions Recommandées

### Option 1 : Activer "Device Log Service" (GRATUIT !) ⭐

**BONNE NOUVELLE** : Il existe une version **GRATUITE avec 7 jours de rétention** !

1. Allez sur https://iot.tuya.com/
2. Vérifiez si "Device Log Service" est disponible dans **Cloud Services** ou **My Service**
3. Si oui, activez la version gratuite
4. Testez l'endpoint `/v1.0/devices/{device_id}/logs`

**Avantages** :
- ✅ Gratuit pour 7 jours de rétention
- ✅ Permet d'afficher les événements on/off dans la timeline
- ✅ Pas besoin de contacter le support pour la version gratuite

---

### Option 2 : Demander Activation "Statistics Service"

**Si vous voulez vraiment les graphiques de consommation** :

1. Ouvrir un **ticket de support Tuya** : https://service.tuya.com/
2. Demander l'activation de **"Statistics Service"**
3. Fournir :
   - Votre PID (Product ID)
   - Les codes DP : `add_ele`, `cur_power`, `cur_voltage`, `cur_current`

**Délai** : Configuration manuelle par Tuya (peut prendre quelques jours)

---

### Option 3 : Historique Local (RECOMMANDÉ si Options 1-2 échouent) ⭐

Si les APIs Tuya ne sont vraiment pas disponibles :

**Avantages** :
- ✅ **0 appel API** supplémentaire
- ✅ Fonctionne **immédiatement** avec vos APIs actuelles
- ✅ Rétention illimitée (selon taille disque)
- ✅ Plus rapide que l'API Tuya
- ✅ Pas de quota ni de limitations

**Fonctionnement** :
1. Intercepter les événements Pulsar en temps réel (que vous recevez déjà)
2. Stocker dans SQLite locale
3. Afficher depuis la base locale

**Temps de développement** : ~2-3 heures

---

## 🧪 Test Immédiat Recommandé

### Testez si l'API Logs Fonctionne Quand Même

Même sans souscription visible, testez :

```bash
GET /v1.0/devices/{votre_device_id}/logs?start_time=1729756800000&end_time=1729843200000&types=report&size=100
```

**Si ça marche** → Vous avez peut-être déjà accès via IoT Core !
**Si erreur 28841101** → Confirmé que l'API n'est pas souscrite

---

## 📊 Résumé

| API | Nom Officiel | Statut | Accès |
|-----|--------------|--------|-------|
| Logs | **Device Log Service** | ⚠️ Vérifier disponibilité | Version gratuite 7j disponible |
| Statistiques | **Statistics Service** | ❌ Pas souscrit | Ticket support Tuya requis |
| Dashboard | **Data Dashboard Service** | ✅ Vous l'avez | Mais pas pour stats par appareil |

---

## 💡 Ma Recommandation

**Plan d'action suggéré** :

1. **Immédiat** : Tester l'endpoint `/v1.0/devices/{id}/logs` pour voir s'il fonctionne malgré tout
2. **Si échec** : Chercher "Device Log Service" dans votre Cloud Services et activer la version gratuite
3. **En parallèle** : Développer l'historique local (Solution 3) qui fonctionnera à coup sûr
4. **Optionnel** : Ouvrir ticket support pour "Statistics Service" si vraiment nécessaire

**Voulez-vous que je** :
- A) Développe l'historique local pendant que vous vérifiez les APIs ?
- B) Attende que vous testiez l'API logs d'abord ?
- C) Les deux en parallèle ?

