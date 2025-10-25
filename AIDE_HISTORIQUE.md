# 🔍 Dépannage - Fonctionnalité Historique

## Problème: Aucune donnée n'est affichée

Si vous cliquez sur le bouton 📊 d'un appareil et qu'aucune donnée n'apparaît, voici les raisons possibles et solutions:

### 1. L'appareil ne mesure pas la consommation

**Symptôme**: Message "Aucune donnée disponible" dans le graphique

**Explication**: Tous les appareils Tuya ne mesurent pas la consommation électrique. Seuls certains types d'appareils (prises intelligentes avec mesure, interrupteurs avec compteur) fournissent ces données.

**Solution**:
- Vérifiez dans l'application Tuya/Smart Life si l'appareil affiche la consommation
- Si l'appareil ne mesure pas la consommation, cette fonctionnalité ne sera pas disponible
- Les événements on/off devraient quand même apparaître dans la timeline

### 2. Aucune donnée pour la période sélectionnée

**Symptôme**: Pas de données pour "Derniers 7 jours" mais données présentes pour "Dernières 24 heures"

**Explication**: L'appareil peut être récemment connecté ou n'avoir pas été utilisé pendant la période.

**Solution**:
- Essayez différentes périodes (24h, 7 jours, 30 jours)
- Vérifiez que l'appareil était en ligne pendant la période

### 3. Vérifier les logs de l'application

**Comment activer/consulter les logs**:

Les logs sont affichés dans la zone de texte en bas du dashboard principal. Pour diagnostiquer:

1. Cliquez sur le bouton 📊 d'un appareil
2. Consultez les logs dans le dashboard
3. Recherchez les messages commençant par `[HistoryService]`

**Exemples de logs normaux**:
```
[HistoryService] Récupération statistiques: bf123456789abcdef, période: Last7Days
[HistoryService] ✅ 7 points de données récupérés
[HistoryService] Récupération logs: bf123456789abcdef, période: Last7Days
[HistoryService] ✅ 15 logs récupérés
```

**Exemples de logs d'erreur**:
```
[HistoryService] ❌ Erreur API statistiques: permission deny
[HistoryService] ❌ Exception GetDeviceStatisticsAsync: timeout
```

### 4. Problèmes d'API Tuya

**Symptôme**: Erreur "permission deny" dans les logs

**Explication**: L'API Tuya nécessite des permissions spéciales pour accéder aux statistiques historiques.

**Solution**:
1. Connectez-vous à [Tuya IoT Platform](https://iot.tuya.com/)
2. Allez dans votre projet Cloud
3. Vérifiez que ces API sont activées:
   - **Device Management** → `Query Device Information`
   - **Data Service** → `Device Statistics`
   - **Device Logs** → `Query Device Logs`
4. Si manquantes, cliquez sur "API Products" et ajoutez-les
5. Redémarrez l'application

### 5. Rate Limiting

**Symptôme**: Erreur "too many requests" ou données partielles

**Explication**: L'API Tuya limite le nombre de requêtes par seconde.

**Solution**:
- Attendez quelques secondes avant de recharger
- Ne cliquez pas rapidement sur plusieurs appareils
- Le rate limiting est automatiquement géré par l'application

### 6. Codes d'appareil non standards

**Symptôme**: Pas de données de consommation mais l'appareil en mesure

**Explication**: Par défaut, l'application cherche le code `cur_power` (consommation courante). Certains appareils utilisent d'autres codes comme:
- `add_ele` - Énergie ajoutée (kWh)
- `cur_voltage` - Tension (V)
- `cur_current` - Courant (mA)
- `switch_1`, `switch_2` - États d'interrupteurs

**Comment vérifier les codes disponibles**:

1. Utilisez l'API Tuya pour obtenir les types de statistiques disponibles:
   ```
   GET /v1.0/devices/{device_id}/all-statistic-type
   ```

2. Vous pouvez tester manuellement avec Postman ou un autre outil API

3. Exemple de réponse:
   ```json
   {
     "result": [
       { "code": "add_ele", "stat_type": "sum" },
       { "code": "cur_voltage", "stat_type": "avg" }
     ]
   }
   ```

**Solution actuelle**:
- L'application utilise actuellement `cur_power` par défaut
- Pour tester avec un autre code, vous devrez modifier le code source dans `TuyaHistoryService.vb` ligne 23
- Une prochaine version permettra de sélectionner le code depuis l'interface

## Vérification rapide

Checklist pour diagnostiquer:
- [ ] L'appareil mesure-t-il la consommation dans l'app Tuya/Smart Life?
- [ ] L'appareil est-il en ligne et connecté?
- [ ] Les logs montrent-ils des erreurs API?
- [ ] Les permissions API sont-elles correctement configurées?
- [ ] Avez-vous essayé différentes périodes?

## Informations techniques sur l'API

### Endpoints utilisés

1. **Statistiques par jours**:
   ```
   GET /v1.0/devices/{device_id}/statistics/days
   Paramètres: code, start_day (yyyyMMdd), end_day (yyyyMMdd), stat_type
   ```

2. **Logs d'événements**:
   ```
   GET /v1.0/devices/{device_id}/logs
   Paramètres: start_time (ms), end_time (ms), size, type
   ```

### Format des données

Les statistiques retournent un objet avec les jours comme clés:
```json
{
  "success": true,
  "result": {
    "days": {
      "20241020": "1.5",
      "20241021": "2.3",
      "20241022": "1.8"
    }
  }
}
```

## Support

Si le problème persiste après ces vérifications:
1. Copiez les logs de l'application
2. Notez le type d'appareil et son ID
3. Vérifiez la configuration de votre projet Tuya IoT Platform
4. Consultez la documentation Tuya: https://developer.tuya.com/en/docs/cloud/device-data-statistics
