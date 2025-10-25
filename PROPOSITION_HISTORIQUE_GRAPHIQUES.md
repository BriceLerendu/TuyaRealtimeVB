# 📊 Proposition: Historique et Graphiques - Dashboard Tuya

## 🎯 Objectifs

Permettre aux utilisateurs de:
1. Visualiser l'historique d'utilisation de leurs appareils
2. Voir des graphiques de consommation énergétique
3. Analyser les tendances (journalières, hebdomadaires, mensuelles)
4. Exporter les données pour analyse externe

---

## 📡 APIs Tuya disponibles

### 1. **Statistiques d'appareil** (Recommandé pour consommation)
```
GET /v1.0/devices/{device_id}/statistics/days
GET /v1.0/devices/{device_id}/statistics/months
```
- Données agrégées par jour/mois
- Parfait pour consommation électrique
- Types: `sum`, `avg`, `min`, `max`
- Intervalle: 15min (7 jours) ou 1h (plus longtemps)

### 2. **Logs d'événements**
```
GET /v1.0/devices/{device_id}/logs
```
- Historique des actions (on/off, changement état)
- Derniers 7 jours par défaut
- Paginé (start_row_key, size)

### 3. **Logs de statut**
```
GET /v1.0/iot-03/devices/{device_id}/report-logs
```
- Rapports d'état détaillés
- Online/offline, activation, reset

---

## 🎨 Propositions de Design

### **Option A: Fenêtre dédiée "Historique"** (Recommandé)
```
┌─────────────────────────────────────────────────┐
│ 📊 Historique - Lampe Salon                    │
├─────────────────────────────────────────────────┤
│ Période: [Derniers 7 jours ▼]                  │
│ Type:    [● Graphique  ○ Liste]                │
├─────────────────────────────────────────────────┤
│                                                 │
│     Consommation (kWh)                         │
│  15 │                    ▄▄                    │
│     │          ▄▄▄      ████                   │
│  10 │    ▄▄   █████    ██████                  │
│     │   ████ ███████  ████████                 │
│   5 │  ██████████████████████████              │
│     │ ███████████████████████████              │
│   0 └────────────────────────────────          │
│      Lun Mar Mer Jeu Ven Sam Dim               │
│                                                 │
│ Total semaine: 45.2 kWh                        │
│ Coût estimé: 8.14 €                           │
│                                                 │
├─────────────────────────────────────────────────┤
│ [📥 Exporter CSV] [🖨️ Imprimer] [✖ Fermer]    │
└─────────────────────────────────────────────────┘
```

**Avantages:**
- ✅ Focus total sur l'analyse
- ✅ Plus d'espace pour graphiques détaillés
- ✅ Peut afficher plusieurs appareils
- ✅ Meilleure expérience utilisateur

### **Option B: Panneau latéral dans Dashboard**
```
┌──────────────┬──────────────────────┐
│              │ 📊 Historique        │
│  Tuiles      │ ────────────────     │
│  appareils   │ Lampe Salon          │
│              │                      │
│  [Salon]     │ Dernières 24h:       │
│  [Chambre]   │                      │
│  [Cuisine]   │   ▄▄  ▄▄            │
│              │  ████ ████           │
│              │ ████████████         │
│              │                      │
│              │ On: 8h 24min         │
│              │ Conso: 2.4 kWh       │
│              │                      │
│              │ [Voir détails +]     │
└──────────────┴──────────────────────┘
```

**Avantages:**
- ✅ Toujours visible
- ✅ Aperçu rapide
- ⚠️ Moins d'espace

### **Option C: Tab "Historique" dans Dashboard**
Ajout d'onglets dans le Dashboard:
```
[🏠 Appareils] [📊 Historique] [⚙️ Paramètres]
```

**Avantages:**
- ✅ Navigation simple
- ✅ Espace dédié
- ✅ Pas de nouvelle fenêtre

---

## 📈 Types de graphiques proposés

### 1. **Graphique en barres** (Consommation)
- Vue journalière/hebdomadaire/mensuelle
- Comparaison facile des valeurs
- **Bibliothèque**: ScottPlot, LiveCharts, OxyPlot

### 2. **Graphique en ligne** (Tendance)
- Évolution dans le temps
- Parfait pour température, luminosité
- **Bibliothèque**: ScottPlot (recommandé)

### 3. **Graphique circulaire** (Répartition)
- % consommation par appareil
- % temps d'utilisation par pièce
- **Bibliothèque**: LiveCharts

### 4. **Timeline** (Événements)
```
Lun 20 Jan
├─ 07:30 🟢 Allumé
├─ 12:15 🔴 Éteint
├─ 18:45 🟢 Allumé
└─ 23:00 🔴 Éteint
```

---

## 💻 Bibliothèques .NET recommandées

### **ScottPlot** ⭐ (Mon choix #1)
```bash
Install-Package ScottPlot.WinForms
```

**Avantages:**
- ✅ Gratuit et open source
- ✅ Excellent pour WinForms
- ✅ Performance élevée
- ✅ Beaux graphiques modernes
- ✅ Documentation excellente
- ✅ Zoom, pan interactif

**Exemple code:**
```vb
Dim plt As New ScottPlot.Plot(800, 600)
plt.AddBar(values:={5, 10, 7, 15, 12})
plt.XTicks(labels:={"Lun", "Mar", "Mer", "Jeu", "Ven"})
plt.YLabel("Consommation (kWh)")
plt.Title("Consommation hebdomadaire")

FormsPlot1.Plot = plt
FormsPlot1.Refresh()
```

### **LiveCharts** (Alternative)
- Animations fluides
- Style moderne
- Un peu plus complexe

### **OxyPlot** (Alternative)
- Mature et stable
- Moins de fonctionnalités

---

## 🗂️ Architecture proposée

### Nouvelle classe: `TuyaHistoryService.vb`
```vb
Public Class TuyaHistoryService
    Private _apiClient As TuyaApiClient

    ' Obtenir statistiques de consommation
    Public Async Function GetDeviceStatistics(
        deviceId As String,
        statType As String,
        startTime As DateTime,
        endTime As DateTime,
        periodType As String
    ) As Task(Of DeviceStatistics)

    ' Obtenir logs d'événements
    Public Async Function GetDeviceLogs(
        deviceId As String,
        startTime As DateTime,
        endTime As DateTime,
        Optional pageSize As Integer = 100
    ) As Task(Of List(Of DeviceLog))

    ' Calculer consommation totale
    Public Function CalculateTotalConsumption(
        stats As List(Of StatisticPoint)
    ) As Double

    ' Estimer coût (€)
    Public Function EstimateCost(
        consumption As Double,
        pricePerKwh As Double
    ) As Double
End Class
```

### Nouvelle classe: `DeviceStatistics.vb`
```vb
Public Class DeviceStatistics
    Public Property DeviceId As String
    Public Property StatType As String  ' "sum", "avg", "min", "max"
    Public Property DataPoints As List(Of StatisticPoint)
    Public Property Unit As String      ' "kWh", "hours", etc.
End Class

Public Class StatisticPoint
    Public Property Timestamp As DateTime
    Public Property Value As Double
End Class

Public Class DeviceLog
    Public Property EventTime As DateTime
    Public Property EventType As String  ' "online", "offline", "switch_on", "switch_off"
    Public Property Code As String
    Public Property Value As String
End Class
```

### Nouveau formulaire: `HistoryForm.vb`
```vb
Public Class HistoryForm
    Inherits Form

    Private _historyService As TuyaHistoryService
    Private _deviceId As String
    Private _formsPlot As ScottPlot.FormsPlot

    ' Charger et afficher historique
    Private Async Sub LoadHistory()

    ' Changer période (7j, 30j, 1 an)
    Private Sub PeriodComboBox_SelectedIndexChanged()

    ' Exporter en CSV
    Private Sub ExportButton_Click()

    ' Dessiner graphique
    Private Sub DrawChart(data As List(Of StatisticPoint))
End Class
```

---

## 🎯 Implémentation par phases

### **Phase 1: Foundation** (2-3h)
- [ ] Créer `TuyaHistoryService.vb`
- [ ] Implémenter appels API Tuya (logs + stats)
- [ ] Créer classes de modèle (`DeviceStatistics`, `DeviceLog`)
- [ ] Tests API avec un appareil

### **Phase 2: UI de base** (2-3h)
- [ ] Créer `HistoryForm.vb` (fenêtre dédiée)
- [ ] Installer ScottPlot (`Install-Package ScottPlot.WinForms`)
- [ ] Layout de base (ComboBox période, Panel graphique)
- [ ] Bouton "📊 Historique" sur chaque tuile

### **Phase 3: Graphiques** (3-4h)
- [ ] Graphique en barres (consommation journalière)
- [ ] Graphique en ligne (tendance)
- [ ] Sélecteur de période (7j/30j/1an)
- [ ] Légendes et axes

### **Phase 4: Fonctionnalités avancées** (2-3h)
- [ ] Export CSV
- [ ] Timeline des événements
- [ ] Calcul coût estimé
- [ ] Cache local (éviter trop d'appels API)

### **Phase 5: Polish** (1-2h)
- [ ] Animations de chargement
- [ ] Gestion erreurs
- [ ] Messages si pas de données
- [ ] Tests avec plusieurs types d'appareils

**Total estimé: 10-15 heures**

---

## 💰 Données disponibles selon type d'appareil

| Type d'appareil | Données disponibles |
|----------------|---------------------|
| **Prises connectées** | Consommation (kWh), tension, courant, on/off |
| **Lumières** | On/off, durée allumage, luminosité |
| **Thermostats** | Température, humidité, mode |
| **Capteurs** | Détection mouvement, ouverture porte |
| **Tous** | Online/offline, timestamp événements |

---

## 🚀 Proposition de démarrage

### Je recommande: **Option A + Phase 1-2**

**Pourquoi?**
1. ✅ Fenêtre dédiée = meilleure UX
2. ✅ ScottPlot = excellente bibliothèque, facile à utiliser
3. ✅ Phase 1-2 = MVP fonctionnel rapidement
4. ✅ Extensible facilement pour phases 3-5

**Premier sprint (4-6h):**
1. Créer service API historique
2. Créer fenêtre avec graphique simple
3. Afficher consommation derniers 7 jours
4. Bouton "Historique" sur tuiles appareils

---

## 📋 Questions pour vous

Avant de commencer l'implémentation:

1. **Design**: Préférez-vous Option A (fenêtre dédiée), B (panneau latéral) ou C (onglet)?

2. **Priorité données**:
   - Consommation électrique (kWh)?
   - Historique on/off?
   - Les deux?

3. **Période**: 7 jours suffit ou vous voulez aussi 30j/1an?

4. **Export**: CSV nécessaire dès le début ou plus tard?

5. **Coût électricité**: Voulez-vous afficher le coût estimé? (prix kWh à configurer)

---

## 💡 Bonus: Idées futures

Une fois la base en place, on pourrait ajouter:

- 📧 **Rapports email automatiques** (consommation hebdomadaire)
- ⚠️ **Alertes** (consommation anormale détectée)
- 🔄 **Comparaisons** (cette semaine vs semaine dernière)
- 🏆 **Objectifs** (réduire consommation de 10%)
- 📱 **Dashboard énergétique** (vue globale maison)

---

**Qu'en pensez-vous? Quelle option vous intéresse le plus?** 😊

Je peux commencer par créer un prototype avec Phase 1-2 si vous voulez voir le résultat rapidement!
