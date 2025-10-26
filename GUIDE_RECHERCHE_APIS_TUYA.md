# 🔍 Guide : Trouver les Bonnes APIs dans Tuya IoT Platform

## 📋 Noms Possibles des APIs (selon la documentation officielle)

Basé sur la documentation Tuya, voici les **noms possibles** des APIs que vous devez chercher :

---

## 🎯 APIs à Rechercher

### Pour les Logs d'Événements (`/v1.0/devices/{id}/logs`)

Cherchez l'un de ces noms dans l'onglet **API Products** :

1. **"Device Log Service"** ⭐ (nom officiel dans la doc)
2. **"Device Logs"**
3. **"Logs Storage"**
4. **"Device Log Query"**

**Indices visuels** :
- Peut mentionner "device logs", "event logs", "operation history"
- Peut être dans une catégorie "Device Management" ou "IoT Core"

---

### Pour les Statistiques (`/v1.0/devices/{id}/statistics/days`)

Cherchez l'un de ces noms :

1. **"Device Data Statistics"** ⭐ (nom officiel dans la doc)
2. **"Device Statistics"**
3. **"Device Data Service"**
4. **"Statistics Service"**

**Indices visuels** :
- Peut mentionner "consumption", "power", "energy", "statistics"
- Description peut inclure "historical data", "aggregate data"

---

## 🔍 Comment Chercher dans Tuya IoT Platform

### Méthode 1 : Recherche par Mot-Clé

1. Allez sur https://iot.tuya.com/
2. **Cloud** → **Development** → Sélectionnez votre projet
3. Onglet **"Service API"** ou **"API Products"**
4. Utilisez la barre de recherche et cherchez :
   - `device log`
   - `statistics`
   - `data service`
   - `device data`

### Méthode 2 : Parcourir par Catégorie

Les APIs peuvent être regroupées par catégorie :
- **Device Management** (peut contenir les logs)
- **Data Services** (peut contenir les statistiques)
- **IoT Core** (peut tout contenir)

---

## 📸 Ce Que Vous Devez Chercher

### Pour les Logs

**Mots-clés dans la description** :
- "Query device logs"
- "Device operation history"
- "Event logs"
- "Device status report"

**Endpoints mentionnés** :
- `/v1.0/devices/{device_id}/logs`
- `/v1.0/iot-03/devices/{device_id}/report-logs`

---

### Pour les Statistiques

**Mots-clés dans la description** :
- "Device data statistics"
- "Historical data"
- "Consumption data"
- "Aggregate data"
- "Statistics by day/hour/month"

**Endpoints mentionnés** :
- `/v1.0/devices/{device_id}/statistics/days`
- `/v1.0/devices/{device_id}/statistics/total`
- `/v1.0/devices/{device_id}/all-statistic-type`

---

## 🎯 Action Immédiate

### Étape 1 : Vérifier IoT Core

Vous avez déjà **"IoT Core"**. Vérifiez si cela inclut les logs :

1. Dans votre projet, allez dans **Service API**
2. Cliquez sur **"IoT Core"** → **"View Details"**
3. Cherchez dans la liste des endpoints disponibles :
   - Y a-t-il `/v1.0/devices/{device_id}/logs` ?
   - Y a-t-il `/v1.0/devices/{device_id}/statistics/days` ?

**Si OUI** → Vous avez déjà les APIs ! Le problème est ailleurs.
**Si NON** → Continuez à l'étape 2.

---

### Étape 2 : Chercher dans "Cloud Services"

1. **Cloud** → **Cloud Services** (pas Development)
2. Cherchez dans la liste des services disponibles :
   - "Device Log Service"
   - "Device Data Statistics"
   - "Device Data Service"

---

### Étape 3 : Vérifier les Services Gratuits

Certaines APIs ont une **version gratuite** (Free Trial) :

1. Sur la page **Cloud Services** ou **My Service**
2. Cherchez les services avec mention :
   - "Free Trial"
   - "7 days free"
   - "Pay-as-you-go"

---

## ❓ Si Vous Ne Trouvez PAS ces APIs

### Scénario 1 : APIs Non Disponibles dans Votre Région/Forfait

Certaines APIs ne sont **pas disponibles** selon :
- Votre région (EU, US, CN, etc.)
- Votre type de compte (Free, Standard, Professional)
- Votre forfait Tuya

**→ Solution** : Passer à l'**Historique Local** (Solution 2)

---

### Scénario 2 : APIs Incluses dans un Autre Nom

Les APIs peuvent être incluses dans un service plus large :

**Vérifiez ces services** :
- ✅ **IoT Core** (que vous avez) → Peut-être que les logs sont déjà inclus
- ✅ **Smart Home Basic Service** (que vous avez) → Peut contenir les statistiques de base
- ✅ **Industry Basic Service** (que vous avez) → Peut contenir des fonctionnalités avancées

---

## 🧪 Test Rapide : Vérifier si les APIs Fonctionnent

Même si l'API n'est pas listée, elle peut fonctionner ! Testons :

### Test 1 : API Explorer

1. **Cloud** → **Development** → **API Explorer**
2. Cherchez l'endpoint : `/v1.0/devices/{device_id}/logs`
3. Entrez un de vos device_id
4. Cliquez "**Debug**" ou "**Send Request**"

**Si ça retourne des données** → L'API est accessible !
**Si erreur 28841101** → API non souscrite

### Test 2 : Vérifier les Endpoints Disponibles

Dans votre projet :
1. **Service API** → Chaque API a un bouton "**View Details**"
2. Cliquez sur **"Smart Home Basic Service"**
3. Listez tous les endpoints disponibles
4. Cherchez :
   - Contient-il `/devices/{id}/logs` ?
   - Contient-il `/devices/{id}/statistics` ?

---

## 📊 Que Me Dire

**Faites ceci** et dites-moi le résultat :

1. **Vérifiez "IoT Core"** → Listez les endpoints disponibles
2. **Vérifiez "Smart Home Basic Service"** → Listez les endpoints disponibles
3. **Allez dans Cloud Services** → Listez TOUS les services disponibles (même ceux non souscrits)
4. **Essayez l'API Explorer** avec `/v1.0/devices/{votre_device_id}/logs`

Avec ces informations, je saurai exactement :
- Si les APIs sont disponibles mais sous un autre nom
- Si elles sont dans un service que vous avez déjà
- Si elles ne sont vraiment pas disponibles → On passe à la Solution 2 (Historique Local)

---

## 💡 Alternative Recommandée

**Si vous ne trouvez vraiment pas ces APIs**, je recommande fortement la **Solution 2 : Historique Local**.

**Avantages** :
- ✅ Fonctionne MAINTENANT avec vos APIs actuelles
- ✅ 0 appel API supplémentaire
- ✅ Plus rapide que l'API Tuya
- ✅ Gratuit et sans quota

**Voulez-vous que je commence à développer l'historique local pendant que vous cherchez ?**
