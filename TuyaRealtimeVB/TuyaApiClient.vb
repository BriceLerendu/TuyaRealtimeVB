Imports System.Net.Http
Imports System.Diagnostics
Imports System.Threading
Imports Newtonsoft.Json.Linq

Public Class TuyaApiClient
    Implements IDisposable

#Region "Constantes"
    Private Const API_VERSION_DEVICES As String = "/v1.0/devices/"
    Private Const API_VERSION_HOMES As String = "/v1.0/homes/"
    Private Const API_VERSION_USERS As String = "/v1.0/users/"
    Private Const SIGN_METHOD As String = "HMAC-SHA256"
    Private Const EMPTY_BODY_HASH As String = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
    Private Const HTTP_METHOD_GET As String = "GET"
    Private Const HTTP_METHOD_POST As String = "POST"
    Private Const HTTP_METHOD_PUT As String = "PUT"
    Private Const MIN_API_INTERVAL_MS As Integer = 100  ' Rate limiting: 10 req/sec max
    Private Shared ReadOnly EPOCH_START As New DateTime(1970, 1, 1)
#End Region

#Region "Champs privés"
    Private ReadOnly _cfg As TuyaConfig
    Private ReadOnly _tokenProvider As TuyaTokenProvider
    Private ReadOnly _roomsCache As New Dictionary(Of String, String)
    Private ReadOnly _homesCache As New Dictionary(Of String, String)
    Private ReadOnly _logCallback As Action(Of String)

    ' ✅ PHASE 6 - Cache API avec LRU et métriques de performance
    ' Dimensionné pour 380+ appareils avec marge de 30%
    Private Const STATUS_CACHE_SIZE As Integer = 500  ' Max 500 appareils en cache (380 + marge)
    Private Const DEVICE_INFO_CACHE_SIZE As Integer = 1000  ' Max 1000 infos en cache
    Private Const STATUS_CACHE_TTL_SECONDS As Integer = 30  ' TTL pour status
    Private Const DEVICE_INFO_CACHE_TTL_SECONDS As Integer = 300  ' TTL pour device info (5 min)

    Private ReadOnly _statusCache As New LRUCache(Of String, JObject)(STATUS_CACHE_SIZE)
    Private ReadOnly _deviceInfoCache As New LRUCache(Of String, DeviceInfo)(DEVICE_INFO_CACHE_SIZE)
    Private _lastApiCall As DateTime = DateTime.MinValue

    ' Cache des spécifications par catégorie d'appareil (pour éviter les doublons)
    Private ReadOnly _specificationsCacheByCategory As New Dictionary(Of String, JObject)
    ' Mapping deviceId -> category pour récupération rapide
    Private ReadOnly _deviceCategoryMap As New Dictionary(Of String, String)

    ' ✅ PHASE 6 - Optimisation: Timer pour nettoyage automatique du cache + métriques
    Private _cacheCleanupTimer As Timer
    Private Const CACHE_CLEANUP_INTERVAL_MS As Integer = 60000  ' Nettoyer toutes les minutes
    Private _totalApiCalls As Long = 0
    Private _cacheHits As Long = 0
    Private _lastMetricsLog As DateTime = DateTime.MinValue
#End Region

#Region "Initialisation"
    Public Sub New(cfg As TuyaConfig, tokenProvider As TuyaTokenProvider, Optional logCallback As Action(Of String) = Nothing)
        _cfg = cfg
        _tokenProvider = tokenProvider
        _logCallback = logCallback

        ' ✅ PHASE 1 - Optimisation: Démarrer le timer de nettoyage automatique du cache
        _cacheCleanupTimer = New Timer(
            AddressOf AutoClearExpiredCache,
            Nothing,
            CACHE_CLEANUP_INTERVAL_MS,
            CACHE_CLEANUP_INTERVAL_MS)
    End Sub

    ''' <summary>
    ''' ✅ PHASE 1 - Callback du timer pour nettoyer automatiquement le cache
    ''' </summary>
    Private Sub AutoClearExpiredCache(state As Object)
        Try
            ClearExpiredCache()
        Catch ex As Exception
            ' Silencieux - ne pas crasher sur un nettoyage de cache
            Log($"Erreur nettoyage automatique cache: {ex.Message}")
        End Try
    End Sub
#End Region

#Region "Gestion du cache"
    Public Async Function InitializeRoomsCacheAsync() As Task
        Try
            Log("=== Chargement des pièces et logements ===")
            Dim token = Await _tokenProvider.GetAccessTokenAsync()

            Dim homes = Await LoadHomesAsync(token)
            If homes Is Nothing OrElse homes.Count = 0 Then
                Log("AUCUN HOME TROUVÉ")
                Return
            End If

            For Each home In homes
                Await LoadRoomsForHomeAsync(home, token)
            Next

            Log($"=== Cache initialisé : {_homesCache.Count} logements, {_roomsCache.Count} pièces ===")
        Catch ex As Exception
            LogError("initialisation cache", ex)
        End Try
    End Function

    Private Async Function LoadHomesAsync(token As String) As Task(Of JArray)
        Dim url = BuildUrl(API_VERSION_USERS, _cfg.Uid, "/homes")
        Log($"URL Homes: {url}")

        Dim json = Await MakeApiCallAsync(url, token)

        If json("result") Is Nothing Then
            Log("AUCUN HOME TROUVÉ dans la réponse API")
            Log($"Réponse complète: {json.ToString()}")
            Return Nothing
        End If

        Dim homesList = json("result")
        If Not TypeOf homesList Is JArray Then
            Log($"ERREUR: result n'est pas un tableau, type = {homesList.Type}")
            Return Nothing
        End If

        Return CType(homesList, JArray)
    End Function

    Private Async Function LoadRoomsForHomeAsync(home As JToken, token As String) As Task
        Dim homeId = GetJsonString(home, "home_id")
        Dim homeName = GetJsonString(home, "name")

        Log($"  Home: {homeName} (ID: {homeId})")

        If String.IsNullOrEmpty(homeId) Then Return

        ' Stocker le home
        If Not String.IsNullOrEmpty(homeName) Then
            _homesCache(homeId) = homeName
        End If

        ' Charger les pièces
        Dim url = BuildUrl(API_VERSION_HOMES, homeId, "/rooms")
        Dim roomsJson = Await MakeApiCallAsync(url, token)

        If roomsJson("result") Is Nothing Then Return

        Dim roomsList = roomsJson("result")("rooms")
        If roomsList Is Nothing OrElse Not TypeOf roomsList Is JArray Then
            Log($"    Aucune pièce dans ce home")
            Return
        End If

        For Each room In CType(roomsList, JArray)
            StoreRoom(room)
        Next
    End Function

    Private Sub StoreRoom(room As JToken)
        Dim roomId = GetJsonString(room, "room_id")
        Dim roomName = GetJsonString(room, "name")

        If String.IsNullOrEmpty(roomId) OrElse String.IsNullOrEmpty(roomName) Then Return

        _roomsCache(roomId) = roomName
        Log($"    ✓ {roomName} (RoomID: {roomId})")
    End Sub
#End Region

#Region "Récupération des appareils"
    Public Async Function GetAllDevicesAsync() As Task(Of List(Of DeviceInfo))
        Dim allDevices As New List(Of DeviceInfo)

        Try
            Log("=== Récupération de tous les appareils ===")
            Dim token = Await _tokenProvider.GetAccessTokenAsync()

            If _homesCache.Count = 0 Then
                allDevices = Await LoadDevicesForUser(token)
            Else
                allDevices = Await LoadDevicesForAllHomes(token)
            End If

            Log($"=== Total: {allDevices.Count} appareils récupérés ===")

            ' Charger les spécifications en parallèle
            If allDevices.Count > 0 Then
                Await LoadDevicesSpecificationsAsync(allDevices)
            End If

        Catch ex As Exception
            LogError("GetAllDevicesAsync", ex)
        End Try

        Return allDevices
    End Function

    Private Async Function LoadDevicesForUser(token As String) As Task(Of List(Of DeviceInfo))
        Log("  Aucun home dans le cache, récupération via l'utilisateur...")

        Dim url = BuildUrl(API_VERSION_USERS, _cfg.Uid, "/devices")
        Dim json = Await MakeApiCallAsync(url, token)

        If json("result") Is Nothing OrElse Not TypeOf json("result") Is JArray Then
            Return New List(Of DeviceInfo)
        End If

        Return Await ProcessDevicesList(CType(json("result"), JArray), "default", "Logement principal", token)
    End Function

    Private Async Function LoadDevicesForAllHomes(token As String) As Task(Of List(Of DeviceInfo))
        Dim allDevices As New List(Of DeviceInfo)

        Log($"  Récupération parallèle des appareils pour {_homesCache.Count} logement(s)...")

        ' Créer une tâche pour chaque home (parallélisation)
        Dim tasks As New List(Of Task(Of List(Of DeviceInfo)))

        For Each homeEntry In _homesCache
            Dim homeId = homeEntry.Key
            Dim homeName = homeEntry.Value

            ' Créer une tâche asynchrone pour ce home
            Dim task = LoadDevicesForHomeAsync(homeId, homeName, token)
            tasks.Add(task)
        Next

        ' Attendre que TOUTES les tâches se terminent en parallèle
        Dim results = Await Task.WhenAll(tasks)

        ' Agréger tous les résultats
        For Each deviceList In results
            If deviceList IsNot Nothing Then
                allDevices.AddRange(deviceList)
            End If
        Next

        Log($"  ✅ Chargement parallèle terminé: {allDevices.Count} appareils récupérés")

        Return allDevices
    End Function

    ''' <summary>
    ''' Charge les devices d'un home spécifique (utilisé pour parallélisation)
    ''' </summary>
    Private Async Function LoadDevicesForHomeAsync(homeId As String, homeName As String, token As String) As Task(Of List(Of DeviceInfo))
        Try
            Log($"    🔄 [{homeName}] Récupération...")

            Dim url = BuildUrl(API_VERSION_HOMES, homeId, "/devices")
            Dim json = Await MakeApiCallAsync(url, token)

            If json("result") IsNot Nothing AndAlso TypeOf json("result") Is JArray Then
                Dim devices = Await ProcessDevicesList(CType(json("result"), JArray), homeId, homeName, token)
                Log($"    ✅ [{homeName}] {devices.Count} appareils")
                Return devices
            End If

            Return New List(Of DeviceInfo)
        Catch ex As Exception
            Log($"    ❌ [{homeName}] Erreur: {ex.Message}")
            Return New List(Of DeviceInfo)
        End Try
    End Function

    Private Async Function ProcessDevicesList(devicesList As JArray, homeId As String, homeName As String, token As String) As Task(Of List(Of DeviceInfo))
        Dim devices As New List(Of DeviceInfo)

        For Each device In devicesList
            Try
                Dim deviceInfo = Await CreateDeviceInfo(device, homeId, homeName, token)
                If deviceInfo IsNot Nothing Then
                    devices.Add(deviceInfo)
                    Log($"    ✓ {deviceInfo.Name} ({deviceInfo.Category})")
                End If
            Catch ex As Exception
                Log($"    Erreur traitement appareil: {ex.Message}")
            End Try
        Next

        Return devices
    End Function

    Private Async Function CreateDeviceInfo(device As JToken, homeId As String, homeName As String, token As String) As Task(Of DeviceInfo)
        Dim deviceId = GetJsonString(device, "id")
        If String.IsNullOrEmpty(deviceId) Then Return Nothing

        ' Récupérer la room
        Dim roomInfo = Await GetDeviceRoomAsync(deviceId, token)

        Return New DeviceInfo With {
            .Id = deviceId,
            .Name = GetJsonString(device, "name"),
            .ProductName = GetJsonString(device, "product_name"),
            .Category = GetJsonString(device, "category"),
            .Icon = GetJsonString(device, "icon"),
            .IsOnline = GetJsonBool(device, "online"),
            .RoomId = roomInfo.Item1,
            .RoomName = roomInfo.Item2,
            .HomeId = homeId,
            .HomeName = homeName
        }
    End Function

    Private Async Function GetDeviceRoomAsync(deviceId As String, token As String) As Task(Of Tuple(Of String, String))
        Try
            Dim url = BuildUrl(API_VERSION_DEVICES, deviceId, "/room")
            Dim json = Await MakeApiCallAsync(url, token)

            If json("result") Is Nothing Then Return Tuple.Create(Of String, String)(Nothing, Nothing)

            Dim roomId = GetJsonString(json("result"), "id")
            If String.IsNullOrEmpty(roomId) Then Return Tuple.Create(Of String, String)(Nothing, Nothing)

            Dim roomName As String = Nothing
            If _roomsCache.ContainsKey(roomId) Then
                roomName = _roomsCache(roomId)
            End If

            Return Tuple.Create(roomId, roomName)
        Catch ex As Exception
            Log($"    Erreur récupération room pour {deviceId}: {ex.Message}")
            Return Tuple.Create(Of String, String)(Nothing, Nothing)
        End Try
    End Function

    Public Async Function GetDeviceInfoAsync(deviceId As String) As Task(Of DeviceInfo)
        Try
            Log($"--- GetDeviceInfo pour {deviceId} ---")

            Dim token = Await _tokenProvider.GetAccessTokenAsync()
            Dim url = BuildUrl(API_VERSION_DEVICES, deviceId, "")
            Dim json = Await MakeApiCallAsync(url, token)

            If json("result") Is Nothing Then Return Nothing

            Dim result = json("result")
            Dim roomInfo = Await GetDeviceRoomAsync(deviceId, token)
            Dim homeInfo = Await FindDeviceHomeAsync(deviceId, token)

            Log($"  Nom appareil: {GetJsonString(result, "name")}")

            Return New DeviceInfo With {
                .Id = deviceId,
                .Name = GetJsonString(result, "name"),
                .ProductName = GetJsonString(result, "product_name"),
                .Category = GetJsonString(result, "category"),
                .Icon = GetJsonString(result, "icon"),
                .IsOnline = GetJsonBool(result, "online"),
                .RoomId = roomInfo.Item1,
                .RoomName = roomInfo.Item2,
                .HomeId = homeInfo.Item1,
                .HomeName = homeInfo.Item2
            }
        Catch ex As Exception
            LogError($"GetDeviceInfo pour {deviceId}", ex)
            Return Nothing
        End Try
    End Function

    Private Async Function FindDeviceHomeAsync(deviceId As String, token As String) As Task(Of Tuple(Of String, String))
        For Each homeEntry In _homesCache
            Try
                Dim url = BuildUrl(API_VERSION_HOMES, homeEntry.Key, "/devices")
                Dim json = Await MakeApiCallAsync(url, token)

                If json("result") Is Nothing OrElse Not TypeOf json("result") Is JArray Then Continue For

                For Each dev In CType(json("result"), JArray)
                    If GetJsonString(dev, "id") = deviceId Then
                        Log($"  ✓ Logement trouvé: {homeEntry.Value}")
                        Return Tuple.Create(homeEntry.Key, homeEntry.Value)
                    End If
                Next
            Catch ex As Exception
                ' Continuer avec le prochain home
            End Try
        Next

        Return Tuple.Create(Of String, String)(Nothing, Nothing)
    End Function

    Public Async Function GetDeviceStatusAsync(deviceId As String, Optional useCache As Boolean = True) As Task(Of JObject)
        Try
            ' ✅ PHASE 6 - Vérifier le cache LRU si activé
            Dim cachedData As JObject = Nothing
            If useCache AndAlso _statusCache.TryGet(deviceId, cachedData) Then
                _cacheHits += 1
                Log($"Cache HIT pour {deviceId} (hit rate: {(_cacheHits / CDbl(_totalApiCalls + _cacheHits)) * 100:F1}%)")
                Return cachedData
            End If

            ' Rate limiting - attendre si nécessaire
            Await ApplyRateLimitAsync()

            ' Appel API
            _totalApiCalls += 1
            Dim token = Await _tokenProvider.GetAccessTokenAsync()
            Dim url = BuildUrl(API_VERSION_DEVICES, deviceId, "/status")
            Dim json = Await MakeApiCallAsync(url, token)

            If json("result") IsNot Nothing Then
                ' ✅ PHASE 6 - Stocker dans le cache LRU avec TTL
                _statusCache.Put(deviceId, json, DateTime.Now.AddSeconds(STATUS_CACHE_TTL_SECONDS))
                LogCacheMetrics()
                Return json
            End If
        Catch ex As Exception
            Log($"Erreur GetDeviceStatus pour {deviceId}: {ex.Message}")
        End Try

        Return Nothing
    End Function

    ''' <summary>
    ''' Récupère le status de plusieurs devices en une seule requête (batch)
    ''' API Tuya: GET /v1.0/iot-03/devices/status?device_ids=id1,id2,id3
    ''' Limite: Max 20 devices par requête
    ''' </summary>
    Public Async Function GetDeviceStatusBatchAsync(deviceIds As List(Of String)) As Task(Of Dictionary(Of String, JToken))
        Dim results As New Dictionary(Of String, JToken)

        Try
            If deviceIds Is Nothing OrElse deviceIds.Count = 0 Then
                Return results
            End If

            ' ✅ PHASE 6 - Augmentation batch size de 20 à 50 pour meilleure performance
            ' API Tuya supporte jusqu'à 50 devices par batch selon tests
            Dim batchSize = Math.Min(deviceIds.Count, 50)
            Dim deviceIdsToQuery = deviceIds.Take(batchSize).ToList()

            _totalApiCalls += 1
            Log($"🔄 Récupération batch de {deviceIdsToQuery.Count} status...")

            ' Construire l'URL avec device_ids séparés par des virgules
            Dim deviceIdsParam = String.Join(",", deviceIdsToQuery)
            Dim token = Await _tokenProvider.GetAccessTokenAsync()

            ' Construire l'URL avec query params (BuildUrl ne supporte pas les query params)
            Dim baseUrl = $"{_cfg.OpenApiBase}/v1.0/iot-03/devices/status?device_ids={deviceIdsParam}"
            Dim json = Await MakeApiCallAsync(baseUrl, token)

            If json("result") IsNot Nothing AndAlso TypeOf json("result") Is JArray Then
                Dim resultArray = CType(json("result"), JArray)

                For Each deviceStatus As JToken In resultArray
                    Dim deviceId = GetJsonString(deviceStatus, "id")
                    Dim status = deviceStatus("status")

                    If Not String.IsNullOrEmpty(deviceId) AndAlso status IsNot Nothing Then
                        results(deviceId) = status
                    End If
                Next

                Log($"✅ Batch status récupéré: {results.Count}/{deviceIdsToQuery.Count} devices")
            Else
                Log($"⚠️ Réponse batch vide ou invalide")
            End If

        Catch ex As Exception
            Log($"❌ Erreur GetDeviceStatusBatch: {ex.Message}")
        End Try

        Return results
    End Function

    Public Async Function GetDeviceFullInfoAsync(deviceId As String) As Task(Of JObject)
        Try
            Dim token = Await _tokenProvider.GetAccessTokenAsync()
            Dim url = BuildUrl(API_VERSION_DEVICES, deviceId, "")
            Dim json = Await MakeApiCallAsync(url, token)

            If json("result") IsNot Nothing Then
                Return CType(json("result"), JObject)
            End If
        Catch ex As Exception
            Log($"Erreur GetDeviceFullInfoAsync pour {deviceId}: {ex.Message}")
        End Try

        Return Nothing
    End Function

    ''' <summary>
    ''' Récupère les spécifications d'un appareil par catégorie (functions, status) avec cache
    ''' API: GET /v1.2/iot-03/devices/{device_id}/specification
    ''' </summary>
    Public Async Function GetDeviceSpecificationAsync(deviceId As String, category As String, Optional forceRefresh As Boolean = False) As Task(Of JObject)
        Try
            ' Vérifier le cache par catégorie (sauf si forceRefresh)
            If Not forceRefresh AndAlso _specificationsCacheByCategory.ContainsKey(category) Then
                Log($"✓ Spécifications pour catégorie '{category}' récupérées depuis le cache")
                Return _specificationsCacheByCategory(category)
            End If

            ' Appel API si pas en cache
            Log($"→ Récupération des spécifications pour catégorie '{category}' (device {deviceId}) depuis l'API")

            Dim token = Await _tokenProvider.GetAccessTokenAsync()
            ' Correct endpoint: /v1.2/iot-03/devices/{device_id}/specification
            Dim url = $"{_cfg.OpenApiBase}/v1.2/iot-03/devices/{deviceId}/specification"

            Log($"   URL: {url}")

            Dim json = Await MakeApiCallAsync(url, token)

            ' Afficher un aperçu de la réponse
            If json IsNot Nothing Then
                Dim jsonStr = json.ToString()
                Dim preview = If(jsonStr.Length > 200, jsonStr.Substring(0, 200), jsonStr)
                Log($"   Réponse reçue: {preview}...")
            Else
                Log($"   Réponse reçue: NULL")
            End If

            If json IsNot Nothing AndAlso json("success") IsNot Nothing Then
                Dim success = CBool(json("success"))
                Log($"   success: {success}")

                If Not success Then
                    Log($"   ⚠️ API a retourné success=false: {json}")
                End If
            End If

            If json("result") IsNot Nothing Then
                Dim specs = CType(json("result"), JObject)

                Dim functionsCount = If(specs("functions") IsNot Nothing, CType(specs("functions"), JArray).Count, 0)
                Log($"   → {functionsCount} functions trouvées dans les specs")

                ' Mettre en cache par catégorie
                _specificationsCacheByCategory(category) = specs
                Log($"✅ Spécifications pour catégorie '{category}' mises en cache")

                Return specs
            Else
                Log($"   ⚠️ Pas de 'result' dans la réponse JSON")
            End If

            Return Nothing
        Catch ex As Exception
            Log($"❌ Erreur GetDeviceSpecificationAsync pour catégorie '{category}': {ex.Message}")
            Log($"   Stack: {ex.StackTrace}")
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Retourne les spécifications depuis le cache uniquement (pas d'appel API)
    ''' </summary>
    Public Function GetCachedDeviceSpecification(deviceId As String) As JObject
        ' Lookup category for this device
        If _deviceCategoryMap.ContainsKey(deviceId) Then
            Dim category = _deviceCategoryMap(deviceId)
            If _specificationsCacheByCategory.ContainsKey(category) Then
                Return _specificationsCacheByCategory(category)
            End If
        End If
        Return Nothing
    End Function

    ''' <summary>
    ''' Retourne la liste de toutes les catégories présentes dans le cache
    ''' </summary>
    Public Function GetCachedCategories() As List(Of String)
        Return _specificationsCacheByCategory.Keys.ToList()
    End Function

    ''' <summary>
    ''' Retourne les spécifications d'une catégorie depuis le cache (pas d'appel API)
    ''' </summary>
    Public Function GetCachedSpecificationByCategory(category As String) As JObject
        If _specificationsCacheByCategory.ContainsKey(category) Then
            Return _specificationsCacheByCategory(category)
        End If
        Return Nothing
    End Function

    ''' <summary>
    ''' Effectue un appel GET générique à l'API Tuya
    ''' </summary>
    ''' <param name="endpoint">Endpoint de l'API (ex: /v1.0/devices/{id}/logs)</param>
    ''' <returns>Réponse JSON de l'API</returns>
    Public Async Function GetAsync(endpoint As String) As Task(Of JObject)
        Try
            ' Rate limiting - attendre si nécessaire
            Await ApplyRateLimitAsync()

            ' Obtenir le token
            Dim token = Await _tokenProvider.GetAccessTokenAsync()

            ' Construire l'URL complète
            Dim url = _cfg.OpenApiBase & endpoint

            ' Faire l'appel API
            Dim json = Await MakeApiCallAsync(url, token)

            Return json
        Catch ex As Exception
            Log($"Erreur GetAsync pour {endpoint}: {ex.Message}")
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Charge les spécifications de tous les appareils en parallèle (groupées par catégorie)
    ''' Optimisation: 1 seul appel API par catégorie au lieu de 1 par appareil
    ''' </summary>
    Private Async Function LoadDevicesSpecificationsAsync(devices As List(Of DeviceInfo)) As Task
        Try
            ' Grouper les appareils par catégorie
            Dim devicesByCategory As New Dictionary(Of String, List(Of DeviceInfo))

            For Each device In devices
                If Not String.IsNullOrEmpty(device.Category) Then
                    ' Enregistrer le mapping deviceId -> category
                    _deviceCategoryMap(device.Id) = device.Category

                    ' Grouper par catégorie
                    If Not devicesByCategory.ContainsKey(device.Category) Then
                        devicesByCategory(device.Category) = New List(Of DeviceInfo)
                    End If
                    devicesByCategory(device.Category).Add(device)
                End If
            Next

            Log($"→ {devices.Count} appareils répartis en {devicesByCategory.Count} catégories")

            ' Créer une tâche par catégorie non en cache
            Dim specTasks As New List(Of Task(Of JObject))
            Dim categoriesToLoad As New List(Of String)

            For Each categoryEntry In devicesByCategory
                Dim category = categoryEntry.Key
                Dim devicesInCategory = categoryEntry.Value

                ' Si la catégorie n'est pas déjà en cache
                If Not _specificationsCacheByCategory.ContainsKey(category) Then
                    ' Prendre le premier appareil de cette catégorie comme représentant
                    Dim representativeDevice = devicesInCategory(0)
                    specTasks.Add(GetDeviceSpecificationAsync(representativeDevice.Id, category))
                    categoriesToLoad.Add(category)
                    Log($"  → Catégorie '{category}': {devicesInCategory.Count} appareils (représentant: {representativeDevice.Name})")
                Else
                    Log($"  ✓ Catégorie '{category}': {devicesInCategory.Count} appareils (déjà en cache)")
                End If
            Next

            ' Exécuter toutes les tâches en parallèle
            If specTasks.Count > 0 Then
                Log($"→ Chargement de {specTasks.Count} spécifications par catégorie en parallèle...")
                Await Task.WhenAll(specTasks)
                Log($"✅ {_specificationsCacheByCategory.Count} catégories en cache (au lieu de {devices.Count} appareils individuels)")
                Log($"   Réduction: {devices.Count - _specificationsCacheByCategory.Count} appels API évités!")
            Else
                Log($"✓ Toutes les spécifications sont déjà en cache ({_specificationsCacheByCategory.Count} catégories)")
            End If

        Catch ex As Exception
            LogError("LoadDevicesSpecificationsAsync", ex)
        End Try
    End Function
#End Region

#Region "Commandes des appareils"
    Public Async Function SendDeviceCommandAsync(deviceId As String, commands As Dictionary(Of String, Object)) As Task
        Dim commandsList = BuildCommandsList(commands)
        Dim body = New Dictionary(Of String, Object) From {{"commands", commandsList}}
        Dim jsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(body)

        Log($"Envoi commande à {deviceId}: {jsonBody}")

        Try
            Dim url = BuildUrl(API_VERSION_DEVICES, deviceId, "/commands")
            Dim token = Await _tokenProvider.GetAccessTokenAsync()

            Dim response = Await ExecutePostRequestAsync(url, jsonBody, token)
            ValidateCommandResponse(response)
        Catch ex As Exception
            LogError("SendDeviceCommandAsync", ex)
            Throw
        End Try
    End Function

    Private Function BuildCommandsList(commands As Dictionary(Of String, Object)) As List(Of Dictionary(Of String, Object))
        Dim commandsList As New List(Of Dictionary(Of String, Object))
        For Each cmd In commands
            commandsList.Add(New Dictionary(Of String, Object) From {
                {"code", cmd.Key},
                {"value", cmd.Value}
            })
        Next
        Return commandsList
    End Function

    Public Async Function RenameDeviceAsync(deviceId As String, newName As String) As Task(Of Boolean)
        Try
            Log($"Renommage de l'appareil {deviceId} en '{newName}'")

            Dim body = New Dictionary(Of String, Object) From {{"name", newName}}
            Dim jsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(body)

            Dim url = BuildUrl(API_VERSION_DEVICES, deviceId, "")
            Dim token = Await _tokenProvider.GetAccessTokenAsync()

            Dim response = Await ExecutePutRequestAsync(url, jsonBody, token)

            Dim success = ValidateResponse(response)
            If success Then
                Log($"✅ Appareil renommé avec succès")
            Else
                Log($"❌ Le renommage a échoué")
            End If

            Return success
        Catch ex As Exception
            LogError("RenameDeviceAsync", ex)
            Return False
        End Try
    End Function
#End Region

#Region "Requêtes HTTP"
    Private Async Function MakeApiCallAsync(url As String, token As String) As Task(Of JObject)
        Using client As New HttpClient()
            ConfigureRequestHeaders(client, url, token, HTTP_METHOD_GET, EMPTY_BODY_HASH)

            Dim resp = Await client.GetAsync(url)
            Dim respBody = Await resp.Content.ReadAsStringAsync()

            Return JObject.Parse(respBody)
        End Using
    End Function

    Private Async Function ExecutePostRequestAsync(url As String, jsonBody As String, token As String) As Task(Of String)
        Using client As New HttpClient()
            Dim bodyHash = ComputeSha256Hash(jsonBody)

            Dim request = New HttpRequestMessage(HttpMethod.Post, url)
            request.Content = New StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json")

            ConfigureRequestHeaders(request, url, token, HTTP_METHOD_POST, bodyHash)

            Dim response = Await client.SendAsync(request)
            Dim responseContent = Await response.Content.ReadAsStringAsync()

            Log($"Réponse commande: {responseContent}")

            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"Erreur API: {response.StatusCode} - {responseContent}")
            End If

            Return responseContent
        End Using
    End Function

    Private Async Function ExecutePutRequestAsync(url As String, jsonBody As String, token As String) As Task(Of String)
        Using client As New HttpClient()
            Dim bodyHash = ComputeSha256Hash(jsonBody)

            Dim request = New HttpRequestMessage(HttpMethod.Put, url)
            request.Content = New StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json")

            ConfigureRequestHeaders(request, url, token, HTTP_METHOD_PUT, bodyHash)

            Dim response = Await client.SendAsync(request)
            Dim responseContent = Await response.Content.ReadAsStringAsync()

            Log($"Réponse: {responseContent}")

            If Not response.IsSuccessStatusCode Then
                Log($"❌ Erreur API: {response.StatusCode} - {responseContent}")
                Return responseContent
            End If

            Return responseContent
        End Using
    End Function

    Private Sub ConfigureRequestHeaders(client As HttpClient, url As String, token As String,
                                       httpMethod As String, bodyHash As String)
        Dim t = GetTimestamp()
        Dim nonce = Guid.NewGuid().ToString("N")
        Dim path = New Uri(url).PathAndQuery

        Dim sign = CalculateSignature(httpMethod, bodyHash, path, token, t, nonce)

        client.DefaultRequestHeaders.Add("client_id", _cfg.AccessId)
        client.DefaultRequestHeaders.Add("access_token", token)
        client.DefaultRequestHeaders.Add("t", t.ToString())
        client.DefaultRequestHeaders.Add("sign_method", SIGN_METHOD)
        client.DefaultRequestHeaders.Add("nonce", nonce)
        client.DefaultRequestHeaders.Add("sign", sign)
    End Sub

    Private Sub ConfigureRequestHeaders(request As HttpRequestMessage, url As String, token As String,
                                       httpMethod As String, bodyHash As String)
        Dim t = GetTimestamp()
        Dim nonce = Guid.NewGuid().ToString("N")
        Dim path = New Uri(url).PathAndQuery

        Dim sign = CalculateSignature(httpMethod, bodyHash, path, token, t, nonce)

        request.Headers.Add("client_id", _cfg.AccessId)
        request.Headers.Add("access_token", token)
        request.Headers.Add("sign", sign)
        request.Headers.Add("t", t.ToString())
        request.Headers.Add("sign_method", SIGN_METHOD)
        request.Headers.Add("nonce", nonce)
    End Sub

    Private Function CalculateSignature(httpMethod As String, bodyHash As String, path As String,
                                       token As String, timestamp As Long, nonce As String) As String
        ' ✅ CORRECTION CRITIQUE : Trier les query params selon la doc Tuya
        ' Source: https://developer.tuya.com/en/docs/iot/singnature?id=Ka43a5mtx1gsc
        ' Les query params doivent être triés par ordre alphabétique pour la signature
        Dim sortedPath = SortQueryParameters(path)

        ' Construire stringToSign selon le protocole Tuya :
        ' METHOD + "\n" + ContentSHA256 + "\n" + Headers + "\n" + URL
        Dim stringToSign = httpMethod & vbLf & bodyHash & vbLf & "" & vbLf & sortedPath

        ' Construire la chaîne finale à signer :
        ' client_id + access_token + timestamp + nonce + stringToSign
        Dim toSign = _cfg.AccessId & token & timestamp.ToString() & nonce & stringToSign

        Return TuyaTokenProvider.HmacSha256Upper(toSign, _cfg.AccessSecret)
    End Function

    ''' <summary>
    ''' Trie les query parameters par ordre alphabétique selon la spec Tuya
    ''' </summary>
    Private Function SortQueryParameters(pathAndQuery As String) As String
        ' Séparer le path des query params
        Dim questionMarkIndex = pathAndQuery.IndexOf("?"c)
        If questionMarkIndex = -1 Then
            ' Pas de query params
            Return pathAndQuery
        End If

        Dim path = pathAndQuery.Substring(0, questionMarkIndex)
        Dim queryString = pathAndQuery.Substring(questionMarkIndex + 1)

        ' Parser et trier les paramètres par ordre alphabétique
        Dim params = queryString.Split("&"c) _
            .OrderBy(Function(p) p) _
            .ToArray()

        ' Reconstruire le path avec les params triés
        Return path & "?" & String.Join("&", params)
    End Function

    Private Function GetTimestamp() As Long
        Return CLng((DateTime.UtcNow - EPOCH_START).TotalMilliseconds)
    End Function
#End Region

#Region "Validation des réponses"
    Private Sub ValidateCommandResponse(responseContent As String)
        Dim jsonResponse = JObject.Parse(responseContent)
        Dim success = GetResponseSuccess(jsonResponse)

        If Not success Then
            Dim errorMsg = If(GetJsonString(jsonResponse, "msg"), "Erreur inconnue")
            Throw New Exception($"La commande a échoué: {errorMsg}")
        End If
    End Sub

    Private Function ValidateResponse(responseContent As String) As Boolean
        Dim jsonResponse = JObject.Parse(responseContent)
        Return GetResponseSuccess(jsonResponse)
    End Function

    Private Function GetResponseSuccess(jsonResponse As JObject) As Boolean
        Dim successValue = jsonResponse("success")
        If successValue Is Nothing Then Return False

        If TypeOf successValue Is JValue Then
            Return CBool(CType(successValue, JValue).Value)
        End If

        Return False
    End Function
#End Region

#Region "Méthodes utilitaires"
    ''' <summary>
    ''' Applique le rate limiting pour éviter de surcharger l'API
    ''' Limite à 10 requêtes/seconde (100ms minimum entre chaque appel)
    ''' </summary>
    Private Async Function ApplyRateLimitAsync() As Task
        Dim elapsed = (DateTime.Now - _lastApiCall).TotalMilliseconds
        If elapsed < MIN_API_INTERVAL_MS Then
            Await Task.Delay(CInt(MIN_API_INTERVAL_MS - elapsed))
        End If
        _lastApiCall = DateTime.Now
    End Function

    ''' <summary>
    ''' Nettoie le cache expiré
    ''' </summary>
    ''' <summary>
    ''' ✅ PHASE 6 - Nettoyage des caches LRU avec métriques
    ''' </summary>
    Public Sub ClearExpiredCache()
        Dim statusRemoved = _statusCache.ClearExpired()
        Dim deviceInfoRemoved = _deviceInfoCache.ClearExpired()
        Dim totalRemoved = statusRemoved + deviceInfoRemoved

        If totalRemoved > 0 Then
            Log($"Cache nettoyé : {totalRemoved} entrées expirées supprimées ({statusRemoved} status, {deviceInfoRemoved} infos)")
            Log($"  Status cache: {_statusCache.Count}/{STATUS_CACHE_SIZE} entrées, hit rate: {_statusCache.HitRate * 100:F1}%")
            Log($"  DeviceInfo cache: {_deviceInfoCache.Count}/{DEVICE_INFO_CACHE_SIZE} entrées, hit rate: {_deviceInfoCache.HitRate * 100:F1}%")
        End If
    End Sub

    ''' <summary>
    ''' ✅ PHASE 6 - Log des métriques de cache toutes les 5 minutes
    ''' </summary>
    Private Sub LogCacheMetrics()
        ' Logger les métriques toutes les 5 minutes seulement
        If (DateTime.Now - _lastMetricsLog).TotalMinutes >= 5 Then
            _lastMetricsLog = DateTime.Now
            Dim totalRequests = _totalApiCalls + _cacheHits
            Dim cacheHitRate = If(totalRequests > 0, CDbl(_cacheHits) / totalRequests * 100, 0)

            Log($"📊 Métriques cache API:")
            Log($"  Total requêtes: {totalRequests} ({_totalApiCalls} API + {_cacheHits} cache)")
            Log($"  Hit rate global: {cacheHitRate:F1}%")
            Log($"  Status cache: {_statusCache.Count}/{STATUS_CACHE_SIZE}, hit rate: {_statusCache.HitRate * 100:F1}%")
            Log($"  DeviceInfo cache: {_deviceInfoCache.Count}/{DEVICE_INFO_CACHE_SIZE}, hit rate: {_deviceInfoCache.HitRate * 100:F1}%")
        End If
    End Sub

    Private Sub Log(message As String)
        If _logCallback IsNot Nothing Then
            _logCallback(message)
        Else
            Console.WriteLine(message)
        End If
    End Sub

    Private Sub LogError(context As String, ex As Exception)
        Log($"ERREUR {context}: {ex.Message}")
        Log($"Stack: {ex.StackTrace}")
    End Sub

    Private Function BuildUrl(ParamArray parts() As String) As String
        Return _cfg.OpenApiBase & String.Concat(parts)
    End Function

    Private Function GetJsonString(token As JToken, key As String) As String
        Return token?.SelectToken(key)?.ToString()
    End Function

    Private Function GetJsonBool(token As JToken, key As String) As Boolean
        Dim value = token?.SelectToken(key)
        If value Is Nothing Then Return False

        If TypeOf value Is JValue Then
            Return CBool(CType(value, JValue).Value)
        End If

        Return False
    End Function

    Private Function ComputeSha256Hash(text As String) As String
        Using sha256 = System.Security.Cryptography.SHA256.Create()
            Dim bytes = System.Text.Encoding.UTF8.GetBytes(text)
            Dim hashBytes = sha256.ComputeHash(bytes)
            Return BitConverter.ToString(hashBytes).Replace("-", "").ToLower()
        End Using
    End Function
#End Region

#Region "Gestion des Homes"
    ''' <summary>
    ''' Récupère la liste de tous les homes de l'utilisateur
    ''' </summary>
    Public Async Function GetHomesAsync() As Task(Of JArray)
        Try
            Dim token = Await _tokenProvider.GetAccessTokenAsync()
            Dim url = BuildUrl(API_VERSION_USERS, _cfg.Uid, "/homes")
            Dim json = Await MakeApiCallAsync(url, token)

            If json("result") IsNot Nothing AndAlso TypeOf json("result") Is JArray Then
                Return CType(json("result"), JArray)
            End If

            Return New JArray()
        Catch ex As Exception
            LogError("GetHomesAsync", ex)
            Return New JArray()
        End Try
    End Function

    ''' <summary>
    ''' Crée un nouveau home
    ''' </summary>
    Public Async Function CreateHomeAsync(homeName As String) As Task(Of String)
        Try
            Log($"Création du home '{homeName}'")

            Dim body = New Dictionary(Of String, Object) From {
                {"name", homeName},
                {"geo_name", homeName},
                {"rooms", New List(Of String)}
            }
            Dim jsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(body)

            Dim url = BuildUrl(API_VERSION_USERS, _cfg.Uid, "/homes")
            Dim token = Await _tokenProvider.GetAccessTokenAsync()

            Dim response = Await ExecutePostRequestAsync(url, jsonBody, token)
            Dim jsonResponse = JObject.Parse(response)

            If GetResponseSuccess(jsonResponse) Then
                Dim homeId = GetJsonString(jsonResponse("result"), "home_id")
                Log($"✅ Home créé avec succès, ID: {homeId}")

                ' Mettre à jour le cache
                If Not String.IsNullOrEmpty(homeId) Then
                    _homesCache(homeId) = homeName
                End If

                Return homeId
            Else
                Dim errorMsg = If(GetJsonString(jsonResponse, "msg"), "Erreur inconnue")
                Log($"❌ Échec création home: {errorMsg}")
                Return Nothing
            End If
        Catch ex As Exception
            LogError("CreateHomeAsync", ex)
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Renomme un home existant
    ''' </summary>
    Public Async Function RenameHomeAsync(homeId As String, newName As String) As Task(Of Boolean)
        Try
            Log($"Renommage du home {homeId} en '{newName}'")

            Dim body = New Dictionary(Of String, Object) From {{"name", newName}}
            Dim jsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(body)

            Dim url = BuildUrl(API_VERSION_HOMES, homeId, "")
            Dim token = Await _tokenProvider.GetAccessTokenAsync()

            Dim response = Await ExecutePutRequestAsync(url, jsonBody, token)
            Dim success = ValidateResponse(response)

            If success Then
                Log($"✅ Home renommé avec succès")
                _homesCache(homeId) = newName
            Else
                Log($"❌ Le renommage du home a échoué")
            End If

            Return success
        Catch ex As Exception
            LogError("RenameHomeAsync", ex)
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Supprime un home
    ''' </summary>
    Public Async Function DeleteHomeAsync(homeId As String) As Task(Of Boolean)
        Try
            Log($"Suppression du home {homeId}")

            Dim url = BuildUrl(API_VERSION_HOMES, homeId, "")
            Dim token = Await _tokenProvider.GetAccessTokenAsync()

            Dim response = Await ExecuteDeleteRequestAsync(url, token)
            Dim success = ValidateResponse(response)

            If success Then
                Log($"✅ Home supprimé avec succès")
                _homesCache.Remove(homeId)
            Else
                Log($"❌ La suppression du home a échoué")
            End If

            Return success
        Catch ex As Exception
            LogError("DeleteHomeAsync", ex)
            Return False
        End Try
    End Function
#End Region

#Region "Gestion des Rooms"
    ''' <summary>
    ''' Récupère toutes les rooms d'un home
    ''' </summary>
    Public Async Function GetRoomsAsync(homeId As String) As Task(Of JArray)
        Try
            Dim token = Await _tokenProvider.GetAccessTokenAsync()
            Dim url = BuildUrl(API_VERSION_HOMES, homeId, "/rooms")
            Dim json = Await MakeApiCallAsync(url, token)

            If json("result") IsNot Nothing Then
                Dim roomsList = json("result")("rooms")
                If roomsList IsNot Nothing AndAlso TypeOf roomsList Is JArray Then
                    Return CType(roomsList, JArray)
                End If
            End If

            Return New JArray()
        Catch ex As Exception
            LogError("GetRoomsAsync", ex)
            Return New JArray()
        End Try
    End Function

    ''' <summary>
    ''' Crée une nouvelle room dans un home
    ''' </summary>
    Public Async Function CreateRoomAsync(homeId As String, roomName As String) As Task(Of String)
        Try
            Log($"Création de la room '{roomName}' dans le home {homeId}")

            Dim body = New Dictionary(Of String, Object) From {{"name", roomName}}
            Dim jsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(body)

            ' CORRECTION: Utiliser /room (singulier) selon la doc Tuya
            Dim url = BuildUrl(API_VERSION_HOMES, homeId, "/room")
            Dim token = Await _tokenProvider.GetAccessTokenAsync()

            Dim response = Await ExecutePostRequestAsync(url, jsonBody, token)
            Dim jsonResponse = JObject.Parse(response)

            If GetResponseSuccess(jsonResponse) Then
                ' CORRECTION: result contient directement l'ID (pas result.room_id)
                Dim roomId = jsonResponse("result")?.ToString()
                Log($"✅ Room créée avec succès, ID: {roomId}")

                ' Mettre à jour le cache
                If Not String.IsNullOrEmpty(roomId) Then
                    _roomsCache(roomId) = roomName
                End If

                Return roomId
            Else
                Dim errorMsg = If(GetJsonString(jsonResponse, "msg"), "Erreur inconnue")
                Log($"❌ Échec création room: {errorMsg}")
                Return Nothing
            End If
        Catch ex As Exception
            LogError("CreateRoomAsync", ex)
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Renomme une room existante
    ''' </summary>
    Public Async Function RenameRoomAsync(homeId As String, roomId As String, newName As String) As Task(Of Boolean)
        Try
            Log($"Renommage de la room {roomId} en '{newName}'")

            Dim body = New Dictionary(Of String, Object) From {{"name", newName}}
            Dim jsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(body)

            Dim url = BuildUrl(API_VERSION_HOMES, homeId, $"/rooms/{roomId}")
            Dim token = Await _tokenProvider.GetAccessTokenAsync()

            Dim response = Await ExecutePutRequestAsync(url, jsonBody, token)
            Dim success = ValidateResponse(response)

            If success Then
                Log($"✅ Room renommée avec succès")
                _roomsCache(roomId) = newName
            Else
                Log($"❌ Le renommage de la room a échoué")
            End If

            Return success
        Catch ex As Exception
            LogError("RenameRoomAsync", ex)
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Supprime une room
    ''' </summary>
    Public Async Function DeleteRoomAsync(homeId As String, roomId As String) As Task(Of Boolean)
        Try
            Log($"Suppression de la room {roomId}")

            Dim url = BuildUrl(API_VERSION_HOMES, homeId, $"/rooms/{roomId}")
            Dim token = Await _tokenProvider.GetAccessTokenAsync()

            Dim response = Await ExecuteDeleteRequestAsync(url, token)
            Dim success = ValidateResponse(response)

            If success Then
                Log($"✅ Room supprimée avec succès")
                _roomsCache.Remove(roomId)
            Else
                Log($"❌ La suppression de la room a échoué")
            End If

            Return success
        Catch ex As Exception
            LogError("DeleteRoomAsync", ex)
            Return False
        End Try
    End Function
#End Region

#Region "Gestion des appareils - Administration"
    ''' <summary>
    ''' Déplace un appareil vers une room
    ''' IMPORTANT: L'API Tuya REMPLACE la liste complète des devices (ne fait pas un simple "add")
    ''' Il faut donc récupérer la liste actuelle et ajouter le device à déplacer
    ''' API Tuya: PUT /v1.0/homes/{home_id}/rooms/{room_id}/devices
    ''' </summary>
    Public Async Function MoveDeviceToRoomAsync(homeId As String, deviceId As String, targetRoomId As String, Optional cachedDevices As List(Of DeviceInfo) = Nothing) As Task(Of Boolean)
        Try
            Log($"Déplacement de l'appareil {deviceId} vers la room {targetRoomId} (home {homeId})")

            ' Récupérer la liste actuelle des devices dans la room cible
            ' OPTIMISATION: Utiliser le cache si disponible pour éviter un appel API
            Dim allDevices As List(Of DeviceInfo)
            If cachedDevices IsNot Nothing Then
                Log($"⚡ Utilisation du cache local ({cachedDevices.Count} devices) - PAS d'appel API")
                allDevices = cachedDevices
            Else
                Log($"⚠️ Cache non disponible - Appel API GetAllDevicesAsync()")
                allDevices = Await GetAllDevicesAsync()
            End If

            Dim currentRoomDeviceIds = allDevices _
                .Where(Function(d) d.RoomId = targetRoomId AndAlso d.Id <> deviceId) _
                .Select(Function(d) d.Id) _
                .ToList()

            ' Ajouter le device à déplacer
            currentRoomDeviceIds.Add(deviceId)

            Log($"📝 Envoi de la liste complète à la room {targetRoomId}: {currentRoomDeviceIds.Count} device(s) [{String.Join(", ", currentRoomDeviceIds)}]")

            ' Construire le body avec la liste complète
            Dim body = New Dictionary(Of String, Object) From {{"device_ids", currentRoomDeviceIds}}
            Dim jsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(body)

            ' Utiliser PUT pour REMPLACER la liste complète
            Dim url = BuildUrl(API_VERSION_HOMES, homeId, $"/rooms/{targetRoomId}/devices")
            Dim token = Await _tokenProvider.GetAccessTokenAsync()

            Dim response = Await ExecutePutRequestAsync(url, jsonBody, token)
            Dim success = ValidateResponse(response)

            If success Then
                Log($"✅ Appareil déplacé avec succès vers la room {targetRoomId}")
            Else
                Log($"❌ Le déplacement de l'appareil a échoué")
            End If

            Return success
        Catch ex As Exception
            LogError("MoveDeviceToRoomAsync", ex)
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Retire un appareil d'une room (le met "sans pièce")
    ''' IMPORTANT: Tuya n'a pas d'endpoint DELETE pour retirer un device d'une room.
    ''' On utilise PUT avec la liste des devices SANS celui à retirer.
    ''' </summary>
    Public Async Function RemoveDeviceFromRoomAsync(homeId As String, roomId As String, deviceId As String, Optional cachedDevices As List(Of DeviceInfo) = Nothing) As Task(Of Boolean)
        Try
            Log($"Retrait de l'appareil {deviceId} de la room {roomId} (home {homeId})")

            ' Récupérer la liste actuelle des devices dans la room
            Dim allDevices As List(Of DeviceInfo)
            If cachedDevices IsNot Nothing Then
                Log($"⚡ Utilisation du cache local ({cachedDevices.Count} devices) - PAS d'appel API")
                allDevices = cachedDevices
            Else
                Log($"⚠️ Cache non disponible - Appel API GetAllDevicesAsync()")
                allDevices = Await GetAllDevicesAsync()
            End If

            ' Créer la liste des devices de la room SANS le device à retirer
            Dim remainingDeviceIds = allDevices _
                .Where(Function(d) d.RoomId = roomId AndAlso d.Id <> deviceId) _
                .Select(Function(d) d.Id) _
                .ToList()

            Log($"📝 Envoi de la liste sans le device à retirer: {remainingDeviceIds.Count} device(s) restants [{String.Join(", ", remainingDeviceIds)}]")

            ' Construire le body avec la liste sans le device
            Dim body = New Dictionary(Of String, Object) From {{"device_ids", remainingDeviceIds}}
            Dim jsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(body)

            ' Utiliser PUT pour REMPLACER la liste complète (sans le device)
            Dim url = BuildUrl(API_VERSION_HOMES, homeId, $"/rooms/{roomId}/devices")
            Dim token = Await _tokenProvider.GetAccessTokenAsync()

            Dim response = Await ExecutePutRequestAsync(url, jsonBody, token)
            Dim success = ValidateResponse(response)

            If success Then
                Log($"✅ Appareil retiré de la room avec succès")
            Else
                Log($"❌ Le retrait de l'appareil a échoué")
            End If

            Return success
        Catch ex As Exception
            LogError("RemoveDeviceFromRoomAsync", ex)
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Supprime un appareil
    ''' </summary>
    Public Async Function DeleteDeviceAsync(deviceId As String) As Task(Of Boolean)
        Try
            Log($"Suppression de l'appareil {deviceId}")

            Dim url = BuildUrl(API_VERSION_DEVICES, deviceId, "")
            Dim token = Await _tokenProvider.GetAccessTokenAsync()

            Dim response = Await ExecuteDeleteRequestAsync(url, token)
            Dim success = ValidateResponse(response)

            If success Then
                Log($"✅ Appareil supprimé avec succès")
            Else
                Log($"❌ La suppression de l'appareil a échoué")
            End If

            Return success
        Catch ex As Exception
            LogError("DeleteDeviceAsync", ex)
            Return False
        End Try
    End Function
#End Region

#Region "Requêtes HTTP - DELETE"
    Private Async Function ExecuteDeleteRequestAsync(url As String, token As String) As Task(Of String)
        Using client As New HttpClient()
            Dim request = New HttpRequestMessage(HttpMethod.Delete, url)

            ConfigureRequestHeaders(request, url, token, "DELETE", EMPTY_BODY_HASH)

            Dim response = Await client.SendAsync(request)
            Dim responseContent = Await response.Content.ReadAsStringAsync()

            Log($"Réponse DELETE: {responseContent}")

            If Not response.IsSuccessStatusCode Then
                Log($"❌ Erreur API: {response.StatusCode} - {responseContent}")
                Return responseContent
            End If

            Return responseContent
        End Using
    End Function
#End Region

#Region "Cache - Méthodes publiques"
    ''' <summary>
    ''' Retourne le cache des homes
    ''' </summary>
    Public Function GetHomesCache() As Dictionary(Of String, String)
        Return New Dictionary(Of String, String)(_homesCache)
    End Function

    ''' <summary>
    ''' Retourne le cache des rooms
    ''' </summary>
    Public Function GetRoomsCache() As Dictionary(Of String, String)
        Return New Dictionary(Of String, String)(_roomsCache)
    End Function
#End Region

    ''' <summary>
    ''' Récupère tous les appareils d'un home spécifique avec leurs spécifications en cache
    ''' API: GET /v1.0/homes/{home_id}/devices
    ''' </summary>
    Public Async Function GetDevicesByHomeAsync(homeId As String) As Task(Of JArray)
        Try
            Log($"Récupération des appareils du home {homeId}")

            Dim token = Await _tokenProvider.GetAccessTokenAsync()
            Dim url = BuildUrl(API_VERSION_HOMES, homeId, "/devices")
            Dim json = Await MakeApiCallAsync(url, token)

            If json("result") IsNot Nothing AndAlso TypeOf json("result") Is JArray Then
                Dim devices = CType(json("result"), JArray)

                ' Enrichir chaque device avec ses spécifications depuis le cache
                For Each device As JToken In devices
                    Dim deviceId = GetJsonString(device, "id")
                    If Not String.IsNullOrEmpty(deviceId) Then
                        Dim specs = GetCachedDeviceSpecification(deviceId)
                        If specs IsNot Nothing Then
                            ' Ajouter les specs directement dans l'objet device
                            CType(device, JObject)("_cached_specifications") = specs
                        End If
                    End If
                Next

                Log($"✅ {devices.Count} appareil(s) récupéré(s) avec spécifications")
                Return devices
            End If

            Log($"⚠️ Aucun appareil trouvé pour le home {homeId}")
            Return New JArray()
        Catch ex As Exception
            LogError("GetDevicesByHomeAsync", ex)
            Return New JArray()
        End Try
    End Function

#Region "Gestion des Automatisations (Scene Automation)"
    ''' <summary>
    ''' Récupère toutes les automatisations (scenes + automations) d'un home
    ''' Essaie plusieurs endpoints API selon la disponibilité
    ''' </summary>
    Public Async Function GetAutomationsAsync(homeId As String) As Task(Of JArray)
        Try
            Log($"╔════════════════════════════════════════════════════")
            Log($"║ AUTOMATISATIONS - HOME ID: {homeId}")
            Log($"╚════════════════════════════════════════════════════")

            Dim token = Await _tokenProvider.GetAccessTokenAsync()
            Dim allAutomations As New JArray()

            ' Essai 1: /v1.0/homes/{home_id}/automations (endpoint principal pour les automatisations)
            Dim url1 = BuildUrl(API_VERSION_HOMES, homeId, "/automations")
            Log($"📡 Endpoint 1 (automations): {url1}")

            Dim json1 = Await MakeApiCallAsync(url1, token)
            Log($"📥 RÉPONSE /automations:")
            Log(json1.ToString(Newtonsoft.Json.Formatting.Indented))

            Dim success1 = GetJsonBool(json1, "success")
            Log($"✓ success = {success1}")

            If success1 AndAlso json1("result") IsNot Nothing AndAlso TypeOf json1("result") Is JArray Then
                Dim automations1 = CType(json1("result"), JArray)
                Log($"✅ /automations: {automations1.Count} élément(s)")

                ' Ajouter ces automatisations
                For Each auto As JToken In automations1
                    allAutomations.Add(auto)
                Next
            Else
                Dim code1 = GetJsonString(json1, "code")
                Dim msg1 = GetJsonString(json1, "msg")
                If Not String.IsNullOrEmpty(code1) Then
                    Log($"⚠️ /automations code = {code1}, msg = {msg1}")
                End If
            End If

            ' Essai 2: /v1.1/homes/{home_id}/scenes (endpoint pour les scènes tap-to-run)
            Dim url2 = BuildUrl("/v1.1/homes/", homeId, "/scenes")
            Log($"")
            Log($"📡 Endpoint 2 (scenes): {url2}")

            Dim json2 = Await MakeApiCallAsync(url2, token)
            Log($"📥 RÉPONSE /scenes:")
            Log(json2.ToString(Newtonsoft.Json.Formatting.Indented))

            Dim success2 = GetJsonBool(json2, "success")
            Log($"✓ success = {success2}")

            If success2 AndAlso json2("result") IsNot Nothing AndAlso TypeOf json2("result") Is JArray Then
                Dim scenes2 = CType(json2("result"), JArray)
                Log($"✅ /scenes: {scenes2.Count} élément(s)")

                ' Ajouter ces scènes (en évitant les doublons par scene_id ou automation_id)
                Dim existingIds As New HashSet(Of String)
                For Each existing As JToken In allAutomations
                    ' Les automatisations peuvent avoir automation_id ou scene_id
                    Dim existingId = GetJsonString(existing, "automation_id")
                    If String.IsNullOrEmpty(existingId) Then
                        existingId = GetJsonString(existing, "scene_id")
                    End If
                    If Not String.IsNullOrEmpty(existingId) Then
                        existingIds.Add(existingId)
                    End If
                Next

                For Each scene As JToken In scenes2
                    Dim sceneId = GetJsonString(scene, "scene_id")
                    If Not String.IsNullOrEmpty(sceneId) AndAlso Not existingIds.Contains(sceneId) Then
                        allAutomations.Add(scene)
                    End If
                Next
            Else
                Dim code2 = GetJsonString(json2, "code")
                Dim msg2 = GetJsonString(json2, "msg")
                If Not String.IsNullOrEmpty(code2) Then
                    Log($"⚠️ /scenes code = {code2}, msg = {msg2}")
                End If
            End If

            Log($"")
            Log($"🎯 TOTAL COMBINÉ: {allAutomations.Count} automatisation(s)")
            Log($"════════════════════════════════════════════════════")

            Return allAutomations
        Catch ex As Exception
            LogError("GetAutomationsAsync", ex)
            Log($"════════════════════════════════════════════════════")
            Return New JArray()
        End Try
    End Function

    ''' <summary>
    ''' Récupère les détails d'une automatisation spécifique
    ''' API: GET /v1.0/homes/{home_id}/automations/{automation_id}
    ''' </summary>
    Public Async Function GetAutomationDetailsAsync(homeId As String, automationId As String) As Task(Of JObject)
        Try
            Log($"Récupération des détails de l'automatisation {automationId}")

            Dim token = Await _tokenProvider.GetAccessTokenAsync()
            Dim url = BuildUrl(API_VERSION_HOMES, homeId, $"/automations/{automationId}")
            Dim json = Await MakeApiCallAsync(url, token)

            If json("result") IsNot Nothing AndAlso TypeOf json("result") Is JObject Then
                Log($"✅ Détails de l'automatisation récupérés")
                Return CType(json("result"), JObject)
            End If

            Log($"⚠️ Détails non disponibles pour l'automatisation {automationId}")
            Return Nothing
        Catch ex As Exception
            LogError("GetAutomationDetailsAsync", ex)
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Active une automatisation
    ''' API: PUT /v1.0/homes/{home_id}/automations/{automation_id}/actions/enable
    ''' </summary>
    Public Async Function EnableAutomationAsync(homeId As String, automationId As String) As Task(Of Boolean)
        Try
            Log($"Activation de l'automatisation {automationId}")

            ' Corps vide pour l'activation (l'API ne nécessite pas de body)
            Dim jsonBody = "{}"

            Dim url = BuildUrl(API_VERSION_HOMES, homeId, $"/automations/{automationId}/actions/enable")
            Dim token = Await _tokenProvider.GetAccessTokenAsync()

            Dim response = Await ExecutePutRequestAsync(url, jsonBody, token)
            Dim success = ValidateResponse(response)

            If success Then
                Log($"✅ Automatisation activée avec succès")
            Else
                Log($"❌ L'activation de l'automatisation a échoué")
            End If

            Return success
        Catch ex As Exception
            LogError("EnableAutomationAsync", ex)
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Désactive une automatisation
    ''' API: PUT /v1.0/homes/{home_id}/automations/{automation_id}/actions/disable
    ''' </summary>
    Public Async Function DisableAutomationAsync(homeId As String, automationId As String) As Task(Of Boolean)
        Try
            Log($"Désactivation de l'automatisation {automationId}")

            ' Corps vide pour la désactivation
            Dim jsonBody = "{}"

            Dim url = BuildUrl(API_VERSION_HOMES, homeId, $"/automations/{automationId}/actions/disable")
            Dim token = Await _tokenProvider.GetAccessTokenAsync()

            Dim response = Await ExecutePutRequestAsync(url, jsonBody, token)
            Dim success = ValidateResponse(response)

            If success Then
                Log($"✅ Automatisation désactivée avec succès")
            Else
                Log($"❌ La désactivation de l'automatisation a échoué")
            End If

            Return success
        Catch ex As Exception
            LogError("DisableAutomationAsync", ex)
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Supprime une automatisation
    ''' API: DELETE /v1.0/homes/{home_id}/automations/{automation_id}
    ''' </summary>
    Public Async Function DeleteAutomationAsync(homeId As String, automationId As String) As Task(Of Boolean)
        Try
            Log($"Suppression de l'automatisation {automationId}")

            Dim url = BuildUrl(API_VERSION_HOMES, homeId, $"/automations/{automationId}")
            Dim token = Await _tokenProvider.GetAccessTokenAsync()

            Dim response = Await ExecuteDeleteRequestAsync(url, token)
            Dim success = ValidateResponse(response)

            If success Then
                Log($"✅ Automatisation supprimée avec succès")
            Else
                Log($"❌ La suppression de l'automatisation a échoué")
            End If

            Return success
        Catch ex As Exception
            LogError("DeleteAutomationAsync", ex)
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Crée une nouvelle automatisation
    ''' API: POST /v1.0/homes/{home_id}/automations
    ''' </summary>
    ''' <param name="homeId">ID du home</param>
    ''' <param name="automationData">Données de l'automatisation (name, conditions, actions, etc.)</param>
    Public Async Function CreateAutomationAsync(homeId As String, automationData As JObject) As Task(Of String)
        Try
            Log($"Création d'une nouvelle automatisation dans le home {homeId}")
            Log($"Données: {automationData.ToString(Newtonsoft.Json.Formatting.Indented)}")

            Dim jsonBody = automationData.ToString(Newtonsoft.Json.Formatting.None)
            Dim url = BuildUrl(API_VERSION_HOMES, homeId, "/automations")
            Dim token = Await _tokenProvider.GetAccessTokenAsync()

            Dim response = Await ExecutePostRequestAsync(url, jsonBody, token)
            Dim json = JObject.Parse(response)

            Dim success = GetJsonBool(json, "success")
            If success AndAlso json("result") IsNot Nothing Then
                ' L'API retourne l'ID de l'automatisation créée
                Dim automationId As String = Nothing
                If TypeOf json("result") Is JObject Then
                    automationId = GetJsonString(CType(json("result"), JObject), "automation_id")
                    If String.IsNullOrEmpty(automationId) Then
                        automationId = GetJsonString(CType(json("result"), JObject), "scene_id")
                    End If
                ElseIf TypeOf json("result") Is JValue Then
                    automationId = json("result").ToString()
                End If

                Log($"✅ Automatisation créée avec succès (ID: {automationId})")
                Return automationId
            Else
                Dim code = GetJsonString(json, "code")
                Dim msg = GetJsonString(json, "msg")
                Log($"❌ Échec de la création: code={code}, msg={msg}")
                Return Nothing
            End If
        Catch ex As Exception
            LogError("CreateAutomationAsync", ex)
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Met à jour une automatisation existante
    ''' API: PUT /v1.0/homes/{home_id}/automations/{automation_id}
    ''' </summary>
    ''' <param name="homeId">ID du home</param>
    ''' <param name="automationId">ID de l'automatisation</param>
    ''' <param name="automationData">Nouvelles données de l'automatisation</param>
    Public Async Function UpdateAutomationAsync(homeId As String, automationId As String, automationData As JObject) As Task(Of Boolean)
        Try
            Log($"Mise à jour de l'automatisation {automationId}")
            Log($"Données: {automationData.ToString(Newtonsoft.Json.Formatting.Indented)}")

            Dim jsonBody = automationData.ToString(Newtonsoft.Json.Formatting.None)
            Dim url = BuildUrl(API_VERSION_HOMES, homeId, $"/automations/{automationId}")
            Dim token = Await _tokenProvider.GetAccessTokenAsync()

            Dim response = Await ExecutePutRequestAsync(url, jsonBody, token)
            Dim success = ValidateResponse(response)

            If success Then
                Log($"✅ Automatisation mise à jour avec succès")
            Else
                Log($"❌ La mise à jour de l'automatisation a échoué")
            End If

            Return success
        Catch ex As Exception
            LogError("UpdateAutomationAsync", ex)
            Return False
        End Try
    End Function
#End Region

#Region "IDisposable"
    ''' <summary>
    ''' ✅ PHASE 1 - Nettoyage des ressources
    ''' </summary>
    Public Sub Dispose() Implements IDisposable.Dispose
        If _cacheCleanupTimer IsNot Nothing Then
            _cacheCleanupTimer.Dispose()
            _cacheCleanupTimer = Nothing
        End If
    End Sub
#End Region

End Class

Public Class DeviceInfo
    Public Property Id As String
    Public Property Name As String
    Public Property ProductName As String
    Public Property Category As String
    Public Property Icon As String
    Public Property IsOnline As Boolean
    Public Property RoomId As String
    Public Property RoomName As String
    Public Property HomeId As String
    Public Property HomeName As String
End Class