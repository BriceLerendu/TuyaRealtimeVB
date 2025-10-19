Imports System.Net.Http
Imports System.Diagnostics
Imports Newtonsoft.Json.Linq

Public Class TuyaApiClient

#Region "Constantes"
    Private Const API_VERSION_DEVICES As String = "/v1.0/devices/"
    Private Const API_VERSION_HOMES As String = "/v1.0/homes/"
    Private Const API_VERSION_USERS As String = "/v1.0/users/"
    Private Const SIGN_METHOD As String = "HMAC-SHA256"
    Private Const EMPTY_BODY_HASH As String = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
    Private Const HTTP_METHOD_GET As String = "GET"
    Private Const HTTP_METHOD_POST As String = "POST"
    Private Const HTTP_METHOD_PUT As String = "PUT"
    Private Shared ReadOnly EPOCH_START As New DateTime(1970, 1, 1)
#End Region

#Region "Champs privés"
    Private ReadOnly _cfg As TuyaConfig
    Private ReadOnly _tokenProvider As TuyaTokenProvider
    Private ReadOnly _roomsCache As New Dictionary(Of String, String)
    Private ReadOnly _homesCache As New Dictionary(Of String, String)
    Private ReadOnly _logCallback As Action(Of String)
#End Region

#Region "Initialisation"
    Public Sub New(cfg As TuyaConfig, tokenProvider As TuyaTokenProvider, Optional logCallback As Action(Of String) = Nothing)
        _cfg = cfg
        _tokenProvider = tokenProvider
        _logCallback = logCallback
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

        For Each homeEntry In _homesCache
            Dim homeId = homeEntry.Key
            Dim homeName = homeEntry.Value

            Log($"  Récupération des appareils pour {homeName}...")

            Dim url = BuildUrl(API_VERSION_HOMES, homeId, "/devices")
            Dim json = Await MakeApiCallAsync(url, token)

            If json("result") IsNot Nothing AndAlso TypeOf json("result") Is JArray Then
                Dim devices = Await ProcessDevicesList(CType(json("result"), JArray), homeId, homeName, token)
                allDevices.AddRange(devices)
            End If
        Next

        Return allDevices
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

    Public Async Function GetDeviceStatusAsync(deviceId As String) As Task(Of JObject)
        Try
            Dim token = Await _tokenProvider.GetAccessTokenAsync()
            Dim url = BuildUrl(API_VERSION_DEVICES, deviceId, "/status")
            Dim json = Await MakeApiCallAsync(url, token)

            If json("result") IsNot Nothing Then Return json
        Catch ex As Exception
            Log($"Erreur GetDeviceStatus pour {deviceId}: {ex.Message}")
        End Try

        Return Nothing
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
        Dim stringToSign = httpMethod & vbLf & bodyHash & vbLf & vbLf & path
        Dim toSign = _cfg.AccessId & token & timestamp.ToString() & nonce & stringToSign
        Return TuyaTokenProvider.HmacSha256Upper(toSign, _cfg.AccessSecret)
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