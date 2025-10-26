# 🔍 Analyse : APIs Manquantes pour la Fonctionnalité Historique

## ❌ Problème Identifié

Erreur : `28841101: No permissions. This API is not subscribed`

Vous n'avez **pas les APIs nécessaires** activées dans votre projet Tuya IoT Platform.

---

## 📋 APIs que VOUS AVEZ actuellement

✅ IoT Core
✅ Authorization Token Management
✅ Smart Home Basic Service
✅ **Data Dashboard Service** (statistiques globales du projet)
✅ Device Status Notification
✅ Industry Basic Service
✅ [Deprecate]Smart Home Scene Linkage

---

## ❌ APIs qui MANQUENT pour l'historique

### Pour `/v1.0/devices/{id}/statistics/days`
❌ **"Device Data Service"** ou **"Data Statistics"**
- Nécessaire pour les statistiques de consommation par appareil
- Permet d'obtenir l'historique kWh, voltage, courant, etc.

### Pour `/v1.0/devices/{id}/logs`
❌ **"Device Management"** (si pas déjà inclus dans IoT Core)
- Nécessaire pour les logs d'événements
- Permet d'obtenir l'historique on/off, online/offline, etc.

---

## 💡 DEUX SOLUTIONS POSSIBLES

---

## ✅ SOLUTION 1 : Activer les APIs Manquantes (Recommandée si disponible)

### Étapes

1. **Aller sur Tuya IoT Platform**
   - https://iot.tuya.com/
   - Cloud → Development → Votre Projet

2. **Onglet "API Products"**
   - Rechercher **"Device Management"**
   - Rechercher **"Device Data Service"** ou **"Data Statistics"** ou **"Device Statistics Service"**

3. **Cliquer sur "Subscribe"** pour chaque API trouvée

4. **Attendre 1-2 minutes** que les changements soient propagés

5. **Relancer votre application** et tester

### ✅ Avantages
- Historique complet depuis le début (selon rétention Tuya)
- Statistiques précises par jour
- Standardisé et supporté officiellement

### ❌ Inconvénients
- Nécessite abonnement API (peut être payant selon forfait)
- Appels API comptabilisés dans votre quota
- Dépend de la disponibilité régionale de ces APIs

---

## ✅ SOLUTION 2 : Historique Local Basé sur Temps Réel (Alternative Intelligente)

### Concept

Au lieu d'interroger l'API Tuya pour l'historique passé, **enregistrer localement les événements** que vous recevez déjà en temps réel via Pulsar !

### Comment ça marche

```
Événement Pulsar reçu → Enregistrer dans SQLite → Afficher dans HistoryForm
```

**Vous recevez DÉJÀ ces événements en temps réel** :
- Changements d'état (on/off)
- Valeurs de puissance, voltage, courant
- Connexion/déconnexion

**Il suffit de les SAUVEGARDER** pour créer un historique !

### ✅ Avantages ÉNORMES

1. **0 appel API supplémentaire**
   - Pas de quota consommé
   - Pas de rate limiting
   - Gratuit

2. **Données instantanées**
   - Pas de délai de synchronisation
   - Mise à jour en temps réel

3. **Fonctionne MAINTENANT**
   - Pas besoin d'activer de nouvelles APIs
   - Utilise uniquement ce que vous avez déjà

4. **Historique personnalisable**
   - Rétention configurable (7 jours, 30 jours, 1 an, etc.)
   - Données précises à la seconde
   - Plus de détails que l'API Tuya

### ❌ Inconvénients

- Historique commence à partir de maintenant (pas de données passées)
- Nécessite stockage local (SQLite)
- Si l'app est fermée, les événements ne sont pas enregistrés (sauf si on fait un service Windows)

### 🏗️ Implémentation

**Architecture** :
```
TuyaPulsarClient (existant)
    ↓ (événement reçu)
HistoryLogger (nouveau)
    ↓ (enregistre)
SQLite Database (nouveau)
    ↓ (lit)
HistoryService (modifié)
    ↓ (affiche)
HistoryForm (existant)
```

**Base de données SQLite** :
```sql
CREATE TABLE device_events (
    id INTEGER PRIMARY KEY,
    device_id TEXT NOT NULL,
    event_time DATETIME NOT NULL,
    event_type TEXT, -- 'switch', 'power', 'voltage', 'online', etc.
    code TEXT,
    value TEXT,
    INDEX idx_device_time (device_id, event_time)
);

CREATE TABLE device_statistics (
    id INTEGER PRIMARY KEY,
    device_id TEXT NOT NULL,
    date DATE NOT NULL,
    code TEXT, -- 'cur_power', 'add_ele', etc.
    sum_value REAL,
    avg_value REAL,
    min_value REAL,
    max_value REAL,
    UNIQUE(device_id, date, code)
);
```

**Composants à créer** :
1. `HistoryDatabase.vb` - Gestion SQLite
2. `LocalHistoryLogger.vb` - Intercepte les événements Pulsar et les sauvegarde
3. Modifier `TuyaHistoryService.vb` - Lire depuis SQLite au lieu de l'API

**Effort estimé** : 2-3 heures de développement

---

## 🎯 Ma Recommandation

### Court terme (MAINTENANT)
**→ SOLUTION 1** : Essayez d'activer les APIs manquantes
- Allez dans votre Tuya IoT Platform
- Cherchez "Device Management" et "Device Data Service"
- Si disponibles gratuitement → Activez-les
- Testez si ça fonctionne

### Moyen/Long terme (MIEUX)
**→ SOLUTION 2** : Implémentez l'historique local
- Beaucoup plus performant (0 appel API)
- Plus de contrôle sur les données
- Fonctionne même si APIs Tuya sont indisponibles
- Peut coexister avec la Solution 1

### ⭐ IDÉAL : Combiner les deux
```
┌─────────────────────────────────────┐
│  Démarrage de l'application         │
└─────────────────┬───────────────────┘
                  │
                  ▼
      ┌───────────────────────┐
      │ Historique local       │ ← Priorité 1 (rapide)
      │ (SQLite)              │
      └───────────┬───────────┘
                  │
                  │ Si pas de données locales
                  │ (première utilisation)
                  ▼
      ┌───────────────────────┐
      │ API Tuya              │ ← Fallback (si activé)
      │ (historique passé)    │
      └───────────────────────┘
```

---

## ❓ Quelle Solution Voulez-Vous ?

**Option A** : Vous allez activer les APIs manquantes dans Tuya IoT Platform
- → Je vous guide pour vérifier quelles APIs activer

**Option B** : Vous préférez l'historique local (recommandé pour performance)
- → Je développe le système d'historique local avec SQLite

**Option C** : Les deux (idéal)
- → On commence par vérifier les APIs, puis on ajoute l'historique local

Dites-moi ce que vous préférez !
