# Message Center Tuya - Guide de débogage

## Si vous n'avez aucun message chargé

### Étapes de diagnostic

1. **Vérifier que l'application est démarrée**
   - Menu Fichier > Démarrer
   - Attendre que les données soient chargées

2. **Ouvrir la console de debug**
   - Dans le Dashboard, la console de debug est visible en bas
   - Tous les logs du Message Center s'affichent dans cette console

3. **Ouvrir le Message Center**
   - Menu Fichier > 📬 Centre de Messages Tuya...
   - Cliquer sur "🔄 Actualiser"

4. **Vérifier les logs dans la console**

   Les logs vont montrer :
   ```
   === Récupération des messages du Message Center ===
   Tentative d'appel API: https://openapi.tuyaXX.com/v1.0/sdf/notifications/messages?page_no=1&page_size=50
   📥 Réponse API complète:
   {
     "success": true/false,
     "code": "...",
     "msg": "...",
     "result": [...]
   }
   ```

## Problèmes courants et solutions

### Problème 1 : success=false avec un code d'erreur

**Exemple de log :**
```
⚠️ Endpoint /v1.0/sdf/notifications/messages - success=false, code: 1106, msg: permission deny
```

**Solution :**
- Votre Access ID Tuya n'a pas les permissions nécessaires pour accéder aux messages
- Connectez-vous au Tuya IoT Platform (https://iot.tuya.com/)
- Allez dans Cloud > Development > API Groups
- Assurez-vous que ces API sont activées :
  - **Message Center** ou **Smart Home Message Service**
  - **Device Management**

### Problème 2 : result est vide ou null

**Exemple de log :**
```
✅ Réponse API success=true
⚠️ Aucun message trouvé dans la réponse
```

**Solutions possibles :**
1. **Vous n'avez vraiment aucun message**
   - Ouvrez l'application SmartLife sur votre téléphone
   - Allez dans le Message Center (icône cloche ou messages)
   - Vérifiez s'il y a des messages

2. **L'endpoint utilisé n'est pas le bon pour votre région**
   - Vérifiez votre région Tuya dans `appsettings.json`
   - Régions disponibles : EU, US, CN, IN
   - L'OpenApiBase doit correspondre (ex: `https://openapi.tuyaeu.com` pour EU)

3. **Les messages sont dans un format différent**
   - Regardez le JSON retourné dans les logs
   - Le format attendu est :
     ```json
     {
       "success": true,
       "result": [
         {
           "id": "...",
           "title": "...",
           "content": "...",
           "message_type": "1/2/3",
           "read_flag": "0/1",
           "timestamp": 1234567890
         }
       ]
     }
     ```

### Problème 3 : Erreur de connexion ou timeout

**Solution :**
- Vérifiez votre connexion Internet
- Vérifiez que l'OpenApiBase est correct
- Essayez de recharger le token : Menu Fichier > Arrêter puis Démarrer

## Endpoints API testés

Le Message Center essaie automatiquement ces endpoints :

1. **`GET /v1.0/sdf/notifications/messages`**
   - Endpoint principal pour les messages système
   - Documentation : https://developer.tuya.com/en/docs/cloud/e1581be6fa

2. **`GET /v1.0/users/{uid}/messages`**
   - Endpoint alternatif pour les messages utilisateur

3. **`GET /v1.0/devices/{device_id}/door-lock/alarm-logs`** (pour les alarmes)
   - Spécifique aux serrures connectées et appareils avec alarmes

## Comment tester si l'API fonctionne

Vous pouvez tester manuellement l'API avec Postman ou curl :

```bash
curl -X GET "https://openapi.tuyaeu.com/v1.0/sdf/notifications/messages?page_no=1&page_size=10" \
  -H "client_id: YOUR_ACCESS_ID" \
  -H "access_token: YOUR_TOKEN" \
  -H "sign: YOUR_SIGNATURE" \
  -H "t: TIMESTAMP" \
  -H "sign_method: HMAC-SHA256" \
  -H "nonce: RANDOM_NONCE"
```

## Logs utiles

Quand vous ouvrez une issue, incluez ces informations :

1. **Version de votre région Tuya** : EU, US, CN, IN (depuis appsettings.json)
2. **Logs complets** de la console de debug (copiez tout le JSON retourné)
3. **Capture d'écran** de l'application SmartLife montrant vos messages
4. **Permissions API** activées dans votre projet Tuya IoT

## Contact et support

Si le problème persiste après avoir suivi ce guide :
- Créez une issue sur GitHub avec les logs
- Vérifiez la documentation Tuya officielle : https://developer.tuya.com/
