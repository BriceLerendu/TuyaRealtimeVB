# 📊 Fonctionnalité Historique et Graphiques

## ✅ Implémentée!

La fonctionnalité d'historique et de graphiques de consommation est maintenant disponible dans l'application.

---

## 🎯 Comment utiliser

### 1. Ouvrir l'historique d'un appareil

Sur chaque tuile d'appareil, cliquez sur le bouton **📊** (en bas à droite).

```
┌─────────────────────────────┐
│  💡 Lampe Salon            │
│  bf...123                   │
│  Salon                      │
│                             │
│  switch_led: true           │
│  ...                        │
│                             │
├─────────────────────────────┤
│ ● En ligne    🕐 12:34:56  📊│ ← Cliquez ici
└─────────────────────────────┘
```

### 2. Fenêtre d'historique

Une fenêtre dédiée s'ouvre avec:

**En haut:**
- Sélecteur de période: 24h / 7 jours / 30 jours
- Bouton 🔄 Actualiser

**Au milieu:**
- Graphique en barres de la consommation (kWh)
- Axe horizontal: Dates
- Axe vertical: Consommation

**En bas:**
- Timeline des événements:
  - 🟢 Allumé
  - 🔴 Éteint
  - ✅ En ligne
  - ❌ Hors ligne

---

## 📈 Données affichées

### Consommation électrique (Graphique)
- **Source**: API Tuya Statistics
- **Endpoint**: `/v1.0/devices/{id}/statistics/days`
- **Type**: Somme (sum) par jour
- **Unité**: kWh
- **Disponible pour**: Prises connectées avec mesure énergie

### Historique événements (Timeline)
- **Source**: API Tuya Logs
- **Endpoint**: `/v1.0/devices/{id}/logs`
- **Types d'événements**:
  - Allumage/Extinction (switch)
  - En ligne/Hors ligne (online/offline)
  - Changements d'état
- **Disponible pour**: Tous les appareils

---

## 🔧 Architecture technique

### Fichiers créés

1. **DeviceStatistics.vb**
   - Modèles de données
   - `DeviceStatistics`, `StatisticPoint`, `DeviceLog`

2. **TuyaHistoryService.vb**
   - Service API pour récupérer données
   - Méthodes:
     - `GetDeviceStatisticsAsync()`
     - `GetDeviceLogsAsync()`

3. **HistoryForm.vb**
   - Interface graphique
   - Utilise ScottPlot pour graphiques
   - Layout en 3 sections

### Package ajouté

- **ScottPlot.WinForms** v5.0.42
  - Bibliothèque de graphiques moderne
  - Installation automatique via NuGet

---

## 📊 Périodes disponibles

| Période | Durée | Détail |
|---------|-------|--------|
| **24 heures** | Dernières 24h | Points par heure |
| **7 jours** | Dernière semaine | Points par jour |
| **30 jours** | Dernier mois | Points par jour |

---

## ⚠️ Limitations connues

### API Tuya
- **Rétention logs**: 7 jours par défaut (gratuit)
- **Statistiques**: Dépend du type d'appareil
- **Données manquantes**: Affiche "Aucune donnée disponible"

### Appareils compatibles
- ✅ **Prises connectées**: Consommation + événements
- ✅ **Lumières**: Événements on/off
- ✅ **Thermostats**: Événements + température (si stats disponibles)
- ⚠️ **Capteurs**: Événements uniquement (pas de consommation)

---

## 🎨 Personnalisation future

Possibilités d'évolution (non implémentées):

- [ ] Export CSV des données
- [ ] Calcul coût électricité (€)
- [ ] Comparaison périodes (semaine vs semaine)
- [ ] Graphiques multiples (température, luminosité)
- [ ] Alertes consommation anormale
- [ ] Rapports automatiques
- [ ] Plus de 30 jours (service Tuya payant)

---

## 🐛 Dépannage

### "Aucune donnée de consommation disponible"

**Cause possible:**
- L'appareil ne supporte pas les statistiques
- Pas de données dans la période sélectionnée
- Service statistiques Tuya non activé

**Solution:**
- Vérifier type d'appareil (prises avec mesure énergie)
- Essayer une autre période
- Consulter console Tuya IoT Platform

### "Erreur lors du chargement"

**Cause possible:**
- Problème de connexion API Tuya
- Token expiré
- Appareil supprimé

**Solution:**
- Vérifier connexion internet
- Redémarrer l'application
- Vérifier logs dans console

---

## 📝 Notes techniques

### Format données API

**Statistics:**
```json
{
  "result": [
    {
      "time": 1706140800000,
      "value": "2.45"
    }
  ]
}
```

**Logs:**
```json
{
  "result": [
    {
      "event_time": 1706140800000,
      "code": "switch_led",
      "value": "true"
    }
  ]
}
```

### Conversion timestamps

- API Tuya: Unix timestamp en **millisecondes**
- Application: `DateTime` .NET local
- Conversion: `DateTimeOffset.FromUnixTimeMilliseconds()`

---

## ✨ Enjoy!

La fonctionnalité est prête à l'emploi. Compilez le projet dans Visual Studio et testez avec vos appareils Tuya!

**Questions? Problèmes?**
Consultez les logs de l'application ou la documentation API Tuya.
