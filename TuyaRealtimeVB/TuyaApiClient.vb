Imports System.Net.Http
Imports System.Diagnostics
Imports Newtonsoft.Json.Linq

Public Class TuyaApiClient
    Private ReadOnly _cfg As TuyaConfig
    Private ReadOnly _tokenProvider As TuyaTokenProvider
    Private ReadOnly _roomsCache As New Dictionary(Of String, String) ' room_id -> room_name
    Private ReadOnly _homesCache As New Dictionary(Of String, String) ' home_id -> home_name
    Private ReadOnly _logCallback As Action(Of String) ' Callback pour les logs

    Public Sub New(cfg As TuyaConfig, tokenProvider As TuyaTokenProvider, Optional logCallback As Action(Of String) = Nothing)
        _cfg = cfg
        _tokenProvider = tokenProvider
        _logCallback = logCallback
    End Sub

    ' Méthode helper pour écrire les logs
    Private Sub Log(message As String)
        If _logCallback IsNot Nothing Then
            _logCallback(message)
        Else
            Console.WriteLine(message)
        End If
    End Sub

    Public Async Function InitializeRoomsCacheAsync() As Task
        Try
            Log("=== Chargement des pièces et logements ===")
            Dim token = Await _tokenProvider.GetAccessTokenAsync()

            ' 1. Récupérer tous les homes de l'utilisateur
            Dim homesUrl = $"{_cfg.OpenApiBase}/v1.0/users/{_cfg.Uid}/homes"
            Log($"URL Homes: {homesUrl}")
            Dim homesJson = Await MakeApiCallAsync(homesUrl, token)

            If homesJson("result") Is Nothing Then
                Log("AUCUN HOME TROUVÉ dans la réponse API")
                Log($"Réponse complète: {homesJson.ToString()}")
                Return
            End If

            ' 2. Pour chaque home, récupérer les rooms
            Dim homesList = homesJson("result")

            ' Vérifier si c'est un tableau
            If TypeOf homesList Is JArray Then
                For Each home In CType(homesList, JArray)
                    Dim homeId = home("home_id")?.ToString()
                    Dim homeName = home("name")?.ToString()

                    Log($"  Home: {homeName} (ID: {homeId})")

                    ' Stocker le home dans le cache
                    If Not String.IsNullOrEmpty(homeId) AndAlso Not String.IsNullOrEmpty(homeName) Then
                        _homesCache(homeId) = homeName
                    End If

                    If Not String.IsNullOrEmpty(homeId) Then
                        Dim roomsUrl = $"{_cfg.OpenApiBase}/v1.0/homes/{homeId}/rooms"
                        Dim roomsJson = Await MakeApiCallAsync(roomsUrl, token)

                        ' Les rooms sont dans result.rooms (pas directement dans result)
                        If roomsJson("result") IsNot Nothing Then
                            Dim roomsList = roomsJson("result")("rooms")

                            If roomsList IsNot Nothing AndAlso TypeOf roomsList Is JArray Then
                                For Each room In CType(roomsList, JArray)
                                    Dim roomId = room("room_id")?.ToString()
                                    Dim roomName = room("name")?.ToString()

                                    If Not String.IsNullOrEmpty(roomId) AndAlso Not String.IsNullOrEmpty(roomName) Then
                                        _roomsCache(roomId) = roomName
                                        Log($"    ✓ {roomName} (RoomID: {roomId})")
                                    End If
                                Next
                            Else
                                Log($"    Aucune pièce dans ce home")
                            End If
                        End If
                    End If
                Next
            Else
                Log($"ERREUR: result n'est pas un tableau, type = {homesList.Type}")
            End If

            Log($"=== Cache initialisé : {_homesCache.Count} logements, {_roomsCache.Count} pièces ===")

        Catch ex As Exception
            Log($"ERREUR initialisation cache : {ex.Message}")
            Log($"Stack: {ex.StackTrace}")
        End Try
    End Function

    ''' <summary>
    ''' Récupère tous les appareils de tous les logements
    ''' </summary>
    Public Async Function GetAllDevicesAsync() As Task(Of List(Of DeviceInfo))
        Dim allDevices As New List(Of DeviceInfo)

        Try
            Log("=== Récupération de tous les appareils ===")
            Dim token = Await _tokenProvider.GetAccessTokenAsync()

            ' Si pas de homes dans le cache, utiliser l'utilisateur directement
            If _homesCache.Count = 0 Then
                Log("  Aucun home dans le cache, récupération via l'utilisateur...")
                Dim devicesUrl = $"{_cfg.OpenApiBase}/v1.0/users/{_cfg.Uid}/devices"
                Dim devicesJson = Await MakeApiCallAsync(devicesUrl, token)

                If devicesJson("result") IsNot Nothing Then
                    Dim devicesList = devicesJson("result")
                    If TypeOf devicesList Is JArray Then
                        allDevices = Await ProcessDevicesList(CType(devicesList, JArray), "default", "Logement principal", token)
                    End If
                End If
            Else
                ' Pour chaque home dans le cache, récupérer ses appareils
                For Each homeEntry In _homesCache
                    Dim homeId = homeEntry.Key
                    Dim homeName = homeEntry.Value

                    Log($"  Récupération des appareils pour {homeName}...")

                    ' URL pour récupérer les devices d'un home
                    Dim devicesUrl = $"{_cfg.OpenApiBase}/v1.0/homes/{homeId}/devices"
                    Dim devicesJson = Await MakeApiCallAsync(devicesUrl, token)

                    If devicesJson("result") IsNot Nothing Then
                        Dim devicesList = devicesJson("result")
                        If TypeOf devicesList Is JArray Then
                            Dim devices = Await ProcessDevicesList(CType(devicesList, JArray), homeId, homeName, token)
                            allDevices.AddRange(devices)
                        End If
                    End If
                Next
            End If

            Log($"=== Total: {allDevices.Count} appareils récupérés ===")

        Catch ex As Exception
            Log($"ERREUR GetAllDevicesAsync: {ex.Message}")
            Log($"Stack: {ex.StackTrace}")
        End Try

        Return allDevices
    End Function

    Private Async Function ProcessDevicesList(devicesList As JArray, homeId As String, homeName As String, token As String) As Task(Of List(Of DeviceInfo))
        Dim devices As New List(Of DeviceInfo)

        For Each device In devicesList
            Try
                Dim deviceId = device("id")?.ToString()
                Dim deviceName = device("name")?.ToString()
                Dim productName = device("product_name")?.ToString()
                Dim category = device("category")?.ToString()
                Dim icon = device("icon")?.ToString()
                Dim isOnline = CBool(device("online"))

                ' Récupérer la room si disponible
                Dim roomId As String = Nothing
                Dim roomName As String = Nothing

                Try
                    Dim roomUrl = $"{_cfg.OpenApiBase}/v1.0/devices/{deviceId}/room"
                    Dim roomJson = Await MakeApiCallAsync(roomUrl, token)

                    If roomJson("result") IsNot Nothing Then
                        roomId = roomJson("result")("id")?.ToString()
                        If Not String.IsNullOrEmpty(roomId) AndAlso _roomsCache.ContainsKey(roomId) Then
                            roomName = _roomsCache(roomId)
                        End If
                    End If
                Catch ex As Exception
                    Log($"    Erreur récupération room pour {deviceId}: {ex.Message}")
                End Try

                Dim deviceInfo As New DeviceInfo With {
                    .Id = deviceId,
                    .Name = deviceName,
                    .ProductName = productName,
                    .Category = category,
                    .Icon = icon,
                    .IsOnline = isOnline,
                    .RoomId = roomId,
                    .RoomName = roomName,
                    .HomeId = homeId,
                    .HomeName = homeName
                }

                devices.Add(deviceInfo)
                Log($"    ✓ {deviceName} ({category})")
            Catch ex As Exception
                Log($"    Erreur traitement appareil: {ex.Message}")
            End Try
        Next

        Return devices
    End Function

    Public Async Function GetDeviceInfoAsync(deviceId As String) As Task(Of DeviceInfo)
        Try
            Log($"--- GetDeviceInfo pour {deviceId} ---")

            Dim token = Await _tokenProvider.GetAccessTokenAsync()
            Dim url = $"{_cfg.OpenApiBase}/v1.0/devices/{deviceId}"
            Dim json = Await MakeApiCallAsync(url, token)

            If json("result") IsNot Nothing Then
                Dim result = json("result")
                Dim deviceName = result("name")?.ToString()

                ' Récupérer la pièce via l'API spécifique
                Dim roomId As String = Nothing
                Dim roomName As String = Nothing

                Try
                    Dim roomUrl = $"{_cfg.OpenApiBase}/v1.0/devices/{deviceId}/room"
                    Dim roomJson = Await MakeApiCallAsync(roomUrl, token)

                    Log($"  API Room réponse: {roomJson.ToString()}")

                    If roomJson("result") IsNot Nothing Then
                        roomId = roomJson("result")("id")?.ToString()
                        Log($"  room_id trouvé: '{roomId}'")

                        If Not String.IsNullOrEmpty(roomId) AndAlso _roomsCache.ContainsKey(roomId) Then
                            roomName = _roomsCache(roomId)
                            Log($"  ✓ Pièce trouvée: {roomName}")
                        Else
                            Log($"  room_id '{roomId}' pas dans le cache")
                        End If
                    Else
                        Log($"  API Room: pas de result")
                    End If
                Catch ex As Exception
                    Log($"  Erreur API Room: {ex.Message}")
                End Try

                ' Essayer de trouver le home en parcourant tous les homes
                Dim homeId As String = Nothing
                Dim homeName As String = Nothing

                For Each homeEntry In _homesCache
                    Try
                        Dim checkUrl = $"{_cfg.OpenApiBase}/v1.0/homes/{homeEntry.Key}/devices"
                        Dim checkJson = Await MakeApiCallAsync(checkUrl, token)

                        If checkJson("result") IsNot Nothing AndAlso TypeOf checkJson("result") Is JArray Then
                            For Each dev In CType(checkJson("result"), JArray)
                                If dev("id")?.ToString() = deviceId Then
                                    homeId = homeEntry.Key
                                    homeName = homeEntry.Value
                                    Log($"  ✓ Logement trouvé: {homeName}")
                                    Exit For
                                End If
                            Next
                        End If

                        If Not String.IsNullOrEmpty(homeId) Then Exit For
                    Catch ex As Exception
                        ' Continuer avec le prochain home
                    End Try
                Next

                Log($"  Nom appareil: {deviceName}")

                Return New DeviceInfo With {
                    .Id = deviceId,
                    .Name = deviceName,
                    .ProductName = result("product_name")?.ToString(),
                    .Category = result("category")?.ToString(),
                    .Icon = result("icon")?.ToString(),
                    .IsOnline = CBool(result("online")),
                    .RoomId = roomId,
                    .RoomName = roomName,
                    .HomeId = homeId,
                    .HomeName = homeName
                }
            End If

        Catch ex As Exception
            Log($"ERREUR GetDeviceInfo pour {deviceId}: {ex.Message}")
        End Try

        Return Nothing
    End Function

    ''' <summary>
    ''' Récupère l'état actuel d'un appareil
    ''' </summary>
    Public Async Function GetDeviceStatusAsync(deviceId As String) As Task(Of JObject)
        Try
            Dim token = Await _tokenProvider.GetAccessTokenAsync()
            Dim url = $"{_cfg.OpenApiBase}/v1.0/devices/{deviceId}/status"
            Dim json = Await MakeApiCallAsync(url, token)

            If json("result") IsNot Nothing Then
                Return json
            End If

        Catch ex As Exception
            Log($"Erreur GetDeviceStatus pour {deviceId}: {ex.Message}")
        End Try

        Return Nothing
    End Function

    Private Async Function MakeApiCallAsync(url As String, token As String) As Task(Of JObject)
        Using client As New HttpClient()
            Dim t As Long = CLng((DateTime.UtcNow - New DateTime(1970, 1, 1)).TotalMilliseconds)
            Dim nonce As String = Guid.NewGuid().ToString("N")

            client.DefaultRequestHeaders.Add("client_id", _cfg.AccessId)
            client.DefaultRequestHeaders.Add("access_token", token)
            client.DefaultRequestHeaders.Add("t", t.ToString())
            client.DefaultRequestHeaders.Add("sign_method", "HMAC-SHA256")
            client.DefaultRequestHeaders.Add("nonce", nonce)

            Dim path = New Uri(url).PathAndQuery
            Dim stringToSign = "GET" & vbLf & "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855" & vbLf & vbLf & path
            Dim toSign = _cfg.AccessId & token & t.ToString() & nonce & stringToSign
            Dim sign = TuyaTokenProvider.HmacSha256Upper(toSign, _cfg.AccessSecret)
            client.DefaultRequestHeaders.Add("sign", sign)

            Dim resp = Await client.GetAsync(url)
            Dim respBody = Await resp.Content.ReadAsStringAsync()

            Return JObject.Parse(respBody)
        End Using
    End Function

    ''' <summary>
    ''' Envoie une commande à un appareil Tuya
    ''' </summary>
    Public Async Function SendDeviceCommandAsync(deviceId As String, commands As Dictionary(Of String, Object)) As Task
        ' Construire le body de la requête
        Dim commandsList As New List(Of Dictionary(Of String, Object))
        For Each cmd In commands
            commandsList.Add(New Dictionary(Of String, Object) From {
                {"code", cmd.Key},
                {"value", cmd.Value}
            })
        Next

        Dim body As New Dictionary(Of String, Object) From {
            {"commands", commandsList}
        }

        Dim jsonBody As String = Newtonsoft.Json.JsonConvert.SerializeObject(body)

        Log($"Envoi commande à {deviceId}: {jsonBody}")

        Try
            Using client As New HttpClient()
                ' Obtenir le token
                Dim token As String = Await _tokenProvider.GetAccessTokenAsync()

                ' Construire l'URL
                Dim url As String = $"{_cfg.OpenApiBase}/v1.0/devices/{deviceId}/commands"
                Dim path As String = $"/v1.0/devices/{deviceId}/commands"

                ' Timestamp
                Dim t As Long = CLng((DateTime.UtcNow - New DateTime(1970, 1, 1)).TotalMilliseconds)
                Dim nonce As String = Guid.NewGuid().ToString("N")

                ' Préparer la requête
                Dim request As New HttpRequestMessage(HttpMethod.Post, url)
                request.Content = New StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json")

                ' Calculer le hash du body (SHA256 en lowercase hex)
                Dim bodyHash As String = ComputeSha256Hash(jsonBody)

                ' Calculer la signature
                Dim stringToSign As String = "POST" & vbLf & bodyHash & vbLf & vbLf & path
                Dim toSign As String = _cfg.AccessId & token & t.ToString() & nonce & stringToSign
                Dim sign As String = TuyaTokenProvider.HmacSha256Upper(toSign, _cfg.AccessSecret)

                ' Headers
                request.Headers.Add("client_id", _cfg.AccessId)
                request.Headers.Add("access_token", token)
                request.Headers.Add("sign", sign)
                request.Headers.Add("t", t.ToString())
                request.Headers.Add("sign_method", "HMAC-SHA256")
                request.Headers.Add("nonce", nonce)

                ' Envoyer la requête
                Dim response As HttpResponseMessage = Await client.SendAsync(request)
                Dim responseContent As String = Await response.Content.ReadAsStringAsync()

                Log($"Réponse commande: {responseContent}")

                If Not response.IsSuccessStatusCode Then
                    Throw New Exception($"Erreur API: {response.StatusCode} - {responseContent}")
                End If

                ' Vérifier la réponse
                Dim jsonResponse = JObject.Parse(responseContent)
                Dim successValue = jsonResponse("success")

                Dim success As Boolean = False
                If successValue IsNot Nothing Then
                    success = If(TypeOf successValue Is JValue,
                                CBool(CType(successValue, JValue).Value),
                                False)
                End If

                If Not success Then
                    Dim errorMsg As String = If(jsonResponse("msg")?.ToString(), "Erreur inconnue")
                    Throw New Exception($"La commande a échoué: {errorMsg}")
                End If

            End Using
        Catch ex As Exception
            Log($"ERREUR SendDeviceCommandAsync: {ex.Message}")
            Throw
        End Try
    End Function

    Private Function ComputeSha256Hash(text As String) As String
        Using sha256 As System.Security.Cryptography.SHA256 = System.Security.Cryptography.SHA256.Create()
            Dim bytes As Byte() = System.Text.Encoding.UTF8.GetBytes(text)
            Dim hashBytes As Byte() = sha256.ComputeHash(bytes)
            Return BitConverter.ToString(hashBytes).Replace("-", "").ToLower()
        End Using
    End Function

    ''' <summary>
    ''' Récupère toutes les informations brutes d'un appareil (JSON complet)
    ''' </summary>
    Public Async Function GetDeviceFullInfoAsync(deviceId As String) As Task(Of JObject)
        Try
            Dim token = Await _tokenProvider.GetAccessTokenAsync()
            Dim url = $"{_cfg.OpenApiBase}/v1.0/devices/{deviceId}"
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
    ''' Renomme un appareil Tuya
    ''' </summary>
    Public Async Function RenameDeviceAsync(deviceId As String, newName As String) As Task(Of Boolean)
        Try
            Log($"Renommage de l'appareil {deviceId} en '{newName}'")

            Using client As New HttpClient()
                ' Obtenir le token
                Dim token As String = Await _tokenProvider.GetAccessTokenAsync()

                ' Construire l'URL
                Dim url As String = $"{_cfg.OpenApiBase}/v1.0/devices/{deviceId}"
                Dim path As String = $"/v1.0/devices/{deviceId}"

                ' Body de la requête
                Dim body As New Dictionary(Of String, Object) From {
                    {"name", newName}
                }
                Dim jsonBody As String = Newtonsoft.Json.JsonConvert.SerializeObject(body)

                ' Timestamp
                Dim t As Long = CLng((DateTime.UtcNow - New DateTime(1970, 1, 1)).TotalMilliseconds)
                Dim nonce As String = Guid.NewGuid().ToString("N")

                ' Préparer la requête PUT
                Dim request As New HttpRequestMessage(HttpMethod.Put, url)
                request.Content = New StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json")

                ' Calculer le hash du body
                Dim bodyHash As String = ComputeSha256Hash(jsonBody)

                ' Calculer la signature
                Dim stringToSign As String = "PUT" & vbLf & bodyHash & vbLf & vbLf & path
                Dim toSign As String = _cfg.AccessId & token & t.ToString() & nonce & stringToSign
                Dim sign As String = TuyaTokenProvider.HmacSha256Upper(toSign, _cfg.AccessSecret)

                ' Headers
                request.Headers.Add("client_id", _cfg.AccessId)
                request.Headers.Add("access_token", token)
                request.Headers.Add("sign", sign)
                request.Headers.Add("t", t.ToString())
                request.Headers.Add("sign_method", "HMAC-SHA256")
                request.Headers.Add("nonce", nonce)

                ' Envoyer la requête
                Dim response As HttpResponseMessage = Await client.SendAsync(request)
                Dim responseContent As String = Await response.Content.ReadAsStringAsync()

                Log($"Réponse renommage: {responseContent}")

                If Not response.IsSuccessStatusCode Then
                    Log($"❌ Erreur API: {response.StatusCode} - {responseContent}")
                    Return False
                End If

                ' Vérifier la réponse
                Dim jsonResponse = JObject.Parse(responseContent)
                Dim successValue = jsonResponse("success")

                Dim success As Boolean = False
                If successValue IsNot Nothing Then
                    success = If(TypeOf successValue Is JValue,
                                CBool(CType(successValue, JValue).Value),
                                False)
                End If

                If success Then
                    Log($"✅ Appareil renommé avec succès")
                    Return True
                Else
                    Dim errorMsg As String = If(jsonResponse("msg")?.ToString(), "Erreur inconnue")
                    Log($"❌ Le renommage a échoué: {errorMsg}")
                    Return False
                End If

            End Using

        Catch ex As Exception
            Log($"❌ ERREUR RenameDeviceAsync: {ex.Message}")
            Return False
        End Try
    End Function


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