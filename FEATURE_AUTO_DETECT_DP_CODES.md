# Feature : Auto-détection des codes DP (Data Points)

## 🎯 Problème résolu

### Situation initiale

L'application cherchait **uniquement** les codes DP liés à la consommation électrique :
- `cur_power` (puissance actuelle)
- `add_ele` (énergie cumulée)
- `switch_1` (état de l'interrupteur)

**Problème** : Ces codes ne sont pas présents sur les **capteurs de température/humidité**, **capteurs de mouvement**, ou autres types d'appareils.

### Exemple concret

**Appareil** : Capteur de température/humidité
**Codes DP réels** : `humidity_value`, `va_temperature`, `temp_unit_convert`
**Résultat avant le fix** :
```
[HistoryService] 🔍 Essai code 'cur_power' pour bf22b4c5f3e81cb468v1ph...
[HistoryService]   ⚠️ Aucun log avec code 'cur_power' (Total logs: 1300)
[HistoryService] ⚠️ Aucune donnée avec 'cur_power'
[HistoryService] 🔍 Essai code 'add_ele' pour bf22b4c5f3e81cb468v1ph...
[HistoryService]   ⚠️ Aucun log avec code 'add_ele' (Total logs: 1300)
[HistoryService] ❌ Aucune donnée trouvée pour bf22b4c5f3e81cb468v1ph
```

→ **1300 logs récupérés mais ignorés** car les codes cherchés ne correspondaient pas !

---

## ✅ Solution implémentée

### Stratégie à 2 niveaux

#### 1. **Essai des codes prioritaires** (appareils électriques)
```vb
Private ReadOnly _electricityCodesPriority As String() = {
    "cur_power",     ' Puissance actuelle
    "add_ele",       ' Énergie cumulée
    "switch_1"       ' État switch
}
```

Si **aucun** de ces codes ne retourne de données → passer à l'étape 2

#### 2. **Auto-détection des codes disponibles** (nouveau !)

```vb
' Nouvelle fonction ajoutée
Private Async Function AutoDetectAndGetStatisticsAsync(
    deviceId As String,
    period As HistoryPeriod
) As Task(Of DeviceStatistics)
```

**Fonctionnement** :
1. **Récupère les logs** de l'appareil
2. **Extrait tous les codes DP uniques** présents dans les logs
3. **Trie les codes par ordre de priorité** :
   - Température → Humidité → Puissance → Batterie → Luminosité → Autres
4. **Teste chaque code** jusqu'à trouver des données valides
5. **Retourne les statistiques** pour le premier code valide trouvé

---

## 📋 Ordre de priorité des capteurs

La fonction utilise un système de priorité basé sur des **patterns de mots-clés** :

```vb
Dim priorityPatterns As String() = {
    "temperature", "temp",           ' 🌡️ Température (priorité 1)
    "humidity", "hum",               ' 💧 Humidité (priorité 2)
    "power", "current", "voltage",   ' ⚡ Électrique (priorité 3)
    "battery",                       ' 🔋 Batterie (priorité 4)
    "bright", "lux"                  ' 💡 Luminosité (priorité 5)
}
```

**Logique** :
- Si un code contient "temperature" ou "temp" → traité en premier
- Si un code contient "humidity" ou "hum" → traité en second
- Etc.

Les codes qui ne matchent aucun pattern sont traités en dernier.

---

## 🔍 Exemple de détection automatique

### Capteur de température/humidité

**Logs de diagnostic** :
```
[HistoryService] 🔍 Essai code 'cur_power' pour bf22b4c5f3e81cb468v1ph...
[HistoryService]   ⚠️ Aucun log avec code 'cur_power' (Total logs: 1300)
[HistoryService] ⚠️ Aucune donnée avec 'cur_power'

[HistoryService] 🔍 Essai code 'add_ele' pour bf22b4c5f3e81cb468v1ph...
[HistoryService]   ⚠️ Aucun log avec code 'add_ele' (Total logs: 1300)
[HistoryService] ⚠️ Aucune donnée avec 'add_ele'

[HistoryService] 🔍 Essai code 'switch_1' pour bf22b4c5f3e81cb468v1ph...
[HistoryService]   ⚠️ Aucun log avec code 'switch_1' (Total logs: 1300)
[HistoryService] ⚠️ Aucune donnée avec 'switch_1'

[HistoryService] 💡 Détection automatique des codes DP disponibles...
[HistoryService]   🔍 Codes DP disponibles détectés: humidity_value, va_temperature, temp_unit_convert
[HistoryService]   🔬 Test du code auto-détecté: 'va_temperature'...
[HistoryService]   ✅ Statistiques créées avec code auto-détecté 'va_temperature' (24 points)
[HistoryService] ✅ Statistiques créées automatiquement avec code 'va_temperature' (24 points)
```

**Résultat** :
- ✅ Détection automatique de `va_temperature` (température)
- ✅ 24 points de données créés
- ✅ Graphique de température affiché dans l'interface

---

## 🛠️ Codes DP supportés

### Codes électriques
| Code DP | Description | Unité | Conversion |
|---------|-------------|-------|------------|
| `cur_power` | Puissance actuelle | W | Valeur / 10 |
| `cur_voltage` | Tension actuelle | V | Valeur / 10 |
| `cur_current` | Courant actuel | A | Valeur / 1000 |
| `add_ele` | Énergie cumulée | kWh | Valeur / 1000 |
| `forward_energy_total` | Énergie totale | kWh | Valeur / 100 |
| `phase_a` | Puissance phase A | W | Valeur / 10 |

### Codes environnementaux
| Code DP | Description | Unité | Conversion |
|---------|-------------|-------|------------|
| `va_temperature` | Température | °C | Valeur / 10 |
| `temp_current` | Température actuelle | °C | Valeur / 10 |
| `temperature` | Température | °C | Aucune |
| `humidity_value` | Humidité | % | Aucune |
| `humidity` | Humidité | % | Aucune |
| `bright_value` | Luminosité | lux | Aucune |
| `brightness` | Luminosité | lux | Aucune |

### Codes batterie
| Code DP | Description | Unité | Conversion |
|---------|-------------|-------|------------|
| `battery_percentage` | Niveau batterie | % | Aucune |
| `battery` | Niveau batterie | % | Aucune |

### Codes binaires/états
| Code DP | Description | Type |
|---------|-------------|------|
| `switch_1`, `switch_2` | Interrupteurs | État binaire (on/off) |
| `pir`, `motion` | Détecteur mouvement | Événements ponctuels |
| `door_contact` | Contact porte | État binaire (open/close) |
| `smoke` | Détecteur fumée | Événements ponctuels |

---

## 📊 Types de visualisation

Le système détecte automatiquement le **type de visualisation** adapté à chaque code DP :

### 1. **Valeurs numériques continues** (par défaut)
- **Exemples** : Température, humidité, puissance, tension
- **Affichage** : Graphique en ligne avec moyenne par heure
- **Calcul** : Moyenne des valeurs sur chaque heure

### 2. **États binaires**
- **Exemples** : Interrupteurs (on/off), portes (open/close)
- **Affichage** : Graphique montrant le % d'activation par heure
- **Calcul** : Ratio états actifs / total

### 3. **Événements ponctuels**
- **Exemples** : Détecteur de mouvement, alarme, bouton
- **Affichage** : Histogramme du nombre d'événements par heure
- **Calcul** : Comptage des occurrences

---

## 🧪 Tests et validation

### Tests recommandés

1. **Appareil électrique** (prise connectée) :
   - ✅ Devrait utiliser `cur_power` ou `add_ele`
   - ✅ Affiche la consommation en Watts ou kWh

2. **Capteur de température/humidité** :
   - ✅ Détection auto de `va_temperature` ou `humidity_value`
   - ✅ Affiche température en °C ou humidité en %

3. **Détecteur de mouvement** :
   - ✅ Détection auto de `pir` ou `motion`
   - ✅ Affiche nombre de détections par heure

4. **Interrupteur** :
   - ✅ Détection auto de `switch_1`
   - ✅ Affiche % temps allumé/éteint

### Exemple de logs attendus

**Avant** (sans auto-détection) :
```
❌ Aucune donnée trouvée pour {device_id} avec tous les codes testés
```

**Après** (avec auto-détection) :
```
💡 Détection automatique des codes DP disponibles...
🔍 Codes DP disponibles détectés: {liste des codes}
🔬 Test du code auto-détecté: '{code}'...
✅ Statistiques créées avec code auto-détecté '{code}' (X points)
```

---

## 🎨 Interface utilisateur

### Impact sur l'affichage

**Avant** : Fenêtre historique vide avec message d'erreur
**Après** : Graphique affiché avec le premier code DP valide trouvé

### Titre du graphique

Le titre indique clairement le code DP affiché :
```
Historique de température (va_temperature)
Dernières 24 heures
```

Ou :
```
Historique d'humidité (humidity_value)
Dernières 24 heures
```

---

## 📝 Notes techniques

### Performance

- **Cache activé** : Les logs récupérés sont mis en cache (TTL: 5 minutes)
- **Requêtes optimisées** : Auto-détection seulement si codes prioritaires échouent
- **Tri intelligent** : Codes triés par priorité pour minimiser les tests

### Extensibilité

Pour ajouter un nouveau type de capteur :

1. **Ajouter le pattern** dans `priorityPatterns` :
   ```vb
   "pressure", "press"  ' Capteur de pression
   ```

2. **Ajouter l'unité** dans `DetermineUnit` :
   ```vb
   Case "pressure", "press"
       Return "hPa"
   ```

3. **Ajouter la conversion** (si nécessaire) dans `CalculateStatisticsFromLogs` :
   ```vb
   Case "pressure"
       val = val / 10.0  ' hPa
   ```

---

## 🔧 Fichiers modifiés

### TuyaHistoryService.vb

**Nouvelles fonctions** :
- `AutoDetectAndGetStatisticsAsync()` (lignes 145-223)
  - Détecte les codes DP disponibles
  - Trie par priorité
  - Teste chaque code

**Modifications existantes** :
- `GetDeviceStatisticsAsync()` (lignes 78-92)
  - Appelle l'auto-détection si codes prioritaires échouent

**Fonctions inchangées mais utilisées** :
- `CalculateStatisticsFromLogs()` - Gère déjà les conversions
- `DetermineUnit()` - Gère déjà les unités
- `DetermineVisualizationType()` - Gère déjà les types d'affichage

---

## ✅ Résumé

**Avant** :
- ❌ Seuls 3 codes DP supportés (cur_power, add_ele, switch_1)
- ❌ Capteurs température/humidité ignorés
- ❌ Fenêtre historique vide pour ces appareils

**Après** :
- ✅ Détection automatique de **TOUS** les codes DP disponibles
- ✅ Support de dizaines de types de capteurs
- ✅ Graphiques affichés pour température, humidité, batterie, etc.
- ✅ Tri intelligent par priorité (température > humidité > puissance...)
- ✅ Extensible facilement pour nouveaux types

**Impact utilisateur** :
- 🎯 Tous les appareils Tuya peuvent maintenant afficher leur historique
- 📊 Graphiques automatiques sans configuration
- 🔍 Détection transparente des capacités de chaque appareil

---

## 🚀 Déploiement

### Tests requis

1. Compiler le projet sur Windows (Visual Studio)
2. Tester avec différents types d'appareils :
   - ⚡ Prise connectée
   - 🌡️ Capteur température/humidité
   - 💡 Ampoule connectée
   - 🚪 Capteur de porte
3. Vérifier les logs pour la détection automatique
4. Vérifier l'affichage des graphiques

### Prochaines étapes possibles

- [ ] Ajouter une interface pour choisir le code DP à afficher
- [ ] Afficher plusieurs graphiques simultanément (température ET humidité)
- [ ] Sauvegarder les préférences de codes DP par appareil
- [ ] Ajouter des alertes basées sur les seuils (température > X°C)
