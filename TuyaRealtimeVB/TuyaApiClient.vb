Imports System.Net.Http
Imports System.Diagnostics
Imports Newtonsoft.Json.Linq

Public Class TuyaApiClient
    Private ReadOnly _cfg As TuyaConfig
    Private ReadOnly _tokenProvider As TuyaTokenProvider
    Private ReadOnly _roomsCache As New Dictionary(Of String, String) ' room_id -> room_name

    Public Sub New(cfg As TuyaConfig, tokenProvider As TuyaTokenProvider)
        _cfg = cfg
        _tokenProvider = tokenProvider
    End Sub

    Public Async Function InitializeRoomsCacheAsync() As Task
        Try
            Debug.WriteLine("=== Chargement des pièces ===")
            Dim token = Await _tokenProvider.GetAccessTokenAsync()

            ' 1. Récupérer tous les homes de l'utilisateur
            Dim homesUrl = $"{_cfg.OpenApiBase}/v1.0/users/{_cfg.Uid}/homes"
            Debug.WriteLine($"URL Homes: {homesUrl}")
            Dim homesJson = Await MakeApiCallAsync(homesUrl, token)

            If homesJson("result") Is Nothing Then
                Debug.WriteLine("AUCUN HOME TROUVÉ dans la réponse API")
                Debug.WriteLine($"Réponse complète: {homesJson.ToString()}")
                Return
            End If

            ' 2. Pour chaque home, récupérer les rooms
            Dim homesList = homesJson("result")

            ' Vérifier si c'est un tableau
            If TypeOf homesList Is JArray Then
                For Each home In CType(homesList, JArray)
                    Dim homeId = home("home_id")?.ToString()
                    Dim homeName = home("name")?.ToString()

                    Debug.WriteLine($"  Home: {homeName} (ID: {homeId})")

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
                                        Debug.WriteLine($"    ✓ {roomName} (RoomID: {roomId})")
                                    End If
                                Next
                            Else
                                Debug.WriteLine($"    Aucune pièce dans ce home")
                            End If
                        End If
                    End If
                Next
            Else
                Debug.WriteLine($"ERREUR: result n'est pas un tableau, type = {homesList.Type}")
            End If

            Debug.WriteLine($"=== Cache initialisé : {_roomsCache.Count} pièces chargées ===")

        Catch ex As Exception
            Debug.WriteLine($"ERREUR initialisation cache pièces : {ex.Message}")
            Debug.WriteLine($"Stack: {ex.StackTrace}")
        End Try
    End Function

    Public Async Function GetDeviceInfoAsync(deviceId As String) As Task(Of DeviceInfo)
        Try
            Debug.WriteLine($"--- GetDeviceInfo pour {deviceId} ---")

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

                    Debug.WriteLine($"  API Room réponse: {roomJson.ToString()}")

                    If roomJson("result") IsNot Nothing Then
                        ' Le champ s'appelle "id" pas "room_id", et c'est un nombre
                        roomId = roomJson("result")("id")?.ToString()
                        Debug.WriteLine($"  room_id trouvé: '{roomId}'")

                        If Not String.IsNullOrEmpty(roomId) AndAlso _roomsCache.ContainsKey(roomId) Then
                            roomName = _roomsCache(roomId)
                            Debug.WriteLine($"  ✓ Pièce trouvée: {roomName}")
                        Else
                            Debug.WriteLine($"  room_id '{roomId}' pas dans le cache")
                        End If
                    Else
                        Debug.WriteLine($"  API Room: pas de result")
                    End If
                Catch ex As Exception
                    Debug.WriteLine($"  Erreur API Room: {ex.Message}")
                End Try

                Debug.WriteLine($"  Nom appareil: {deviceName}")

                Return New DeviceInfo With {
                .Id = deviceId,
                .Name = deviceName,
                .ProductName = result("product_name")?.ToString(),
                .Category = result("category")?.ToString(),
                .Icon = result("icon")?.ToString(),
                .IsOnline = If(result("online")?.Value(Of Boolean)(), False),
                .RoomId = roomId,
                .RoomName = roomName
            }
            End If

        Catch ex As Exception
            Debug.WriteLine($"ERREUR GetDeviceInfo pour {deviceId}: {ex.Message}")
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
End Class