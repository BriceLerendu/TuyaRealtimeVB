Imports System.Net.Http
Imports System.Linq
Imports Newtonsoft.Json.Linq

''' <summary>
''' Classe pour gérer les accès au Message Center de Tuya
''' Permet de récupérer les messages, notifications et alarmes comme dans l'application SmartLife
''' </summary>
Public Class TuyaMessageCenter
    Implements IDisposable

#Region "Constantes"
    Private Const API_VERSION_MESSAGES As String = "/v1.0/sdf/notifications/"
    Private Const API_VERSION_USERS As String = "/v1.0/users/"
    Private Const API_VERSION_DEVICES As String = "/v1.0/devices/"
    Private Const SIGN_METHOD As String = "HMAC-SHA256"
    Private Const EMPTY_BODY_HASH As String = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
    Private Shared ReadOnly EPOCH_START As New DateTime(1970, 1, 1)
#End Region

#Region "Champs privés"
    Private ReadOnly _cfg As TuyaConfig
    Private ReadOnly _tokenProvider As TuyaTokenProvider
    Private ReadOnly _logCallback As Action(Of String)
#End Region

#Region "Types de messages"
    ''' <summary>
    ''' Types de messages disponibles dans le Message Center Tuya
    ''' </summary>
    Public Enum MessageType
        All = 0          ' Tous les messages
        Home = 1         ' Messages famille (ajout/suppression membres, partage appareils)
        Bulletin = 2     ' Notifications (notifications officielles, retours utilisateur)
        Alarm = 3        ' Alarmes (alarmes appareils, notifications automatiques)
    End Enum
#End Region

#Region "Classes de données"
    ''' <summary>
    ''' Représente un message du Message Center
    ''' </summary>
    Public Class TuyaMessage
        Public Property Id As String
        Public Property MessageType As MessageType
        Public Property Title As String
        Public Property Content As String
        Public Property IsRead As Boolean
        Public Property Timestamp As DateTime
        Public Property DeviceId As String
        Public Property DeviceName As String
        Public Property Icon As String
        Public Property RawData As JObject
    End Class
#End Region

#Region "Initialisation"
    Public Sub New(cfg As TuyaConfig, tokenProvider As TuyaTokenProvider, Optional logCallback As Action(Of String) = Nothing)
        _cfg = cfg
        _tokenProvider = tokenProvider
        _logCallback = logCallback
    End Sub
#End Region

#Region "API - Récupération des messages"
    ''' <summary>
    ''' Récupère tous les messages du Message Center
    ''' API: GET /v1.0/sdf/notifications/messages
    ''' </summary>
    Public Async Function GetAllMessagesAsync(Optional pageNo As Integer = 1, Optional pageSize As Integer = 50) As Task(Of List(Of TuyaMessage))
        Try
            Log("=== Récupération des messages du Message Center ===")

            Dim allMessages As New List(Of TuyaMessage)

            ' Essayer différents endpoints API selon la documentation Tuya

            ' Essai 1: Endpoint standard pour les messages
            Dim messages1 = Await TryGetMessagesAsync("/v1.0/sdf/notifications/messages", pageNo, pageSize)
            If messages1 IsNot Nothing Then
                allMessages.AddRange(messages1)
            End If

            ' Essai 2: Endpoint pour les notifications utilisateur
            ' ⚠️ DÉSACTIVÉ: L'endpoint /v1.0/users/{uid}/messages n'existe pas selon la documentation Tuya
            ' Si vous avez besoin d'autres types de messages, utilisez les paramètres message_type et message_sub_type
            ' Documentation: https://developer.tuya.com/en/docs/cloud/e1581be6fa?id=Kbabe1ij7fivh
            ' Dim messages2 = Await TryGetUserNotificationsAsync(pageNo, pageSize)
            ' If messages2 IsNot Nothing Then
            '     allMessages.AddRange(messages2)
            ' End If

            Log($"=== Total: {allMessages.Count} message(s) récupéré(s) ===")

            Return allMessages
        Catch ex As Exception
            LogError("GetAllMessagesAsync", ex)
            Return New List(Of TuyaMessage)
        End Try
    End Function

    ''' <summary>
    ''' Récupère les messages par type (Famille, Bulletin, Alarmes)
    ''' </summary>
    Public Async Function GetMessagesByTypeAsync(msgType As MessageType, Optional pageNo As Integer = 1, Optional pageSize As Integer = 50) As Task(Of List(Of TuyaMessage))
        Try
            Log($"=== Récupération des messages de type {msgType} ===")

            Dim allMessages = Await GetAllMessagesAsync(pageNo, pageSize)

            ' Filtrer par type si nécessaire
            If msgType = MessageType.All Then
                Return allMessages
            Else
                Return allMessages.Where(Function(m) m.MessageType = msgType).ToList()
            End If
        Catch ex As Exception
            LogError("GetMessagesByTypeAsync", ex)
            Return New List(Of TuyaMessage)
        End Try
    End Function

    ''' <summary>
    ''' Récupère uniquement les messages non lus
    ''' </summary>
    Public Async Function GetUnreadMessagesAsync() As Task(Of List(Of TuyaMessage))
        Try
            Log("=== Récupération des messages non lus ===")

            Dim allMessages = Await GetAllMessagesAsync()
            Dim unreadMessages = allMessages.Where(Function(m) Not m.IsRead).ToList()

            Log($"=== {unreadMessages.Count} message(s) non lu(s) ===")

            Return unreadMessages
        Catch ex As Exception
            LogError("GetUnreadMessagesAsync", ex)
            Return New List(Of TuyaMessage)
        End Try
    End Function

    ''' <summary>
    ''' Essaie de récupérer les messages depuis un endpoint spécifique
    ''' </summary>
    Private Async Function TryGetMessagesAsync(endpoint As String, pageNo As Integer, pageSize As Integer) As Task(Of List(Of TuyaMessage))
        Try
            Dim token = Await _tokenProvider.GetAccessTokenAsync()

            ' Construire l'URL avec les paramètres de pagination
            ' ✅ CORRECTION: Ajouter recipient_id qui est obligatoire selon la documentation Tuya
            ' Documentation: https://developer.tuya.com/en/docs/cloud/e1581be6fa?id=Kbabe1ij7fivh
            Dim url = $"{_cfg.OpenApiBase}{endpoint}?recipient_id={_cfg.Uid}&page_no={pageNo}&page_size={pageSize}"

            Log($"Tentative d'appel API: {url}")

            Dim json = Await MakeApiCallAsync(url, token)

            ' Afficher la réponse complète pour debug
            If json IsNot Nothing Then
                Log("📥 Réponse API complète:")
                Log(json.ToString(Newtonsoft.Json.Formatting.Indented))
            Else
                Log("⚠️ Réponse API NULL")
            End If

            If json IsNot Nothing AndAlso GetJsonBool(json, "success") Then
                Log("✅ Réponse API success=true")

                ' Parser les messages selon le format de la réponse
                Dim messages = ParseMessagesFromResponse(json)
                Log($"   → {If(messages IsNot Nothing, messages.Count, 0)} message(s) parsé(s)")
                Return messages
            Else
                Dim errorCode = GetJsonString(json, "code")
                Dim errorMsg = GetJsonString(json, "msg")
                Log($"⚠️ Endpoint {endpoint} - success=false")
                Log($"   → Code erreur: {errorCode}")
                Log($"   → Message: {errorMsg}")
                Log("")
                Log("💡 DIAGNOSTIC:")
                If errorCode = "1106" OrElse errorCode = "1100" Then
                    Log("   → Erreur d'autorisation - Vérifiez que l'API 'Message Service' est activée")
                    Log("   → Allez sur https://iot.tuya.com → Cloud → Project → Votre Projet → API")
                    Log("   → Recherchez 'Message' et activez le service")
                ElseIf errorCode = "1004" Then
                    Log("   → Signature invalide - Problème de token ou de configuration")
                ElseIf errorCode = "2406" Then
                    Log("   → Paramètres manquants ou invalides")
                Else
                    Log($"   → Code d'erreur inconnu: {errorCode}")
                    Log("   → Consultez https://developer.tuya.com/en/docs/iot/error-code")
                End If
                Return Nothing
            End If
        Catch ex As Exception
            Log($"⚠️ Erreur sur endpoint {endpoint}: {ex.Message}")
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Essaie de récupérer les notifications utilisateur
    ''' </summary>
    Private Async Function TryGetUserNotificationsAsync(pageNo As Integer, pageSize As Integer) As Task(Of List(Of TuyaMessage))
        Try
            Dim token = Await _tokenProvider.GetAccessTokenAsync()

            ' Essayer l'endpoint utilisateur pour les notifications
            Dim url = $"{_cfg.OpenApiBase}/v1.0/users/{_cfg.Uid}/messages?page_no={pageNo}&page_size={pageSize}"

            Log($"Tentative d'appel API notifications utilisateur: {url}")

            Dim json = Await MakeApiCallAsync(url, token)

            ' Afficher la réponse complète pour debug
            If json IsNot Nothing Then
                Log("📥 Réponse API notifications utilisateur:")
                Log(json.ToString(Newtonsoft.Json.Formatting.Indented))
            Else
                Log("⚠️ Réponse API notifications utilisateur NULL")
            End If

            If json IsNot Nothing AndAlso GetJsonBool(json, "success") Then
                Log("✅ Notifications utilisateur success=true")
                Return ParseMessagesFromResponse(json)
            Else
                Dim errorCode = GetJsonString(json, "code")
                Dim errorMsg = GetJsonString(json, "msg")
                Log($"⚠️ Notifications utilisateur - code: {errorCode}, msg: {errorMsg}")
                Return Nothing
            End If
        Catch ex As Exception
            Log($"⚠️ Erreur notifications utilisateur: {ex.Message}")
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Parse les messages depuis la réponse JSON de l'API
    ''' </summary>
    Private Function ParseMessagesFromResponse(json As JObject) As List(Of TuyaMessage)
        Dim messages As New List(Of TuyaMessage)

        Try
            Dim result = json("result")
            If result Is Nothing Then
                Log("⚠️ Pas de 'result' dans la réponse")
                Return messages
            End If

            ' La réponse peut être un tableau ou un objet contenant un tableau
            Dim messageArray As JArray = Nothing

            If TypeOf result Is JArray Then
                messageArray = CType(result, JArray)
            ElseIf TypeOf result Is JObject Then
                ' Chercher un tableau dans l'objet result
                Dim resultObj = CType(result, JObject)
                If resultObj("list") IsNot Nothing AndAlso TypeOf resultObj("list") Is JArray Then
                    messageArray = CType(resultObj("list"), JArray)
                ElseIf resultObj("messages") IsNot Nothing AndAlso TypeOf resultObj("messages") Is JArray Then
                    messageArray = CType(resultObj("messages"), JArray)
                ElseIf resultObj("data") IsNot Nothing AndAlso TypeOf resultObj("data") Is JArray Then
                    messageArray = CType(resultObj("data"), JArray)
                End If
            End If

            If messageArray Is Nothing OrElse messageArray.Count = 0 Then
                Log("⚠️ Aucun message trouvé dans la réponse")
                Return messages
            End If

            Log($"📨 Parsing de {messageArray.Count} message(s)")

            ' Parser chaque message
            For Each msgToken As JToken In messageArray
                Try
                    Dim msg = ParseSingleMessage(msgToken)
                    If msg IsNot Nothing Then
                        messages.Add(msg)
                    End If
                Catch ex As Exception
                    Log($"⚠️ Erreur parsing message: {ex.Message}")
                End Try
            Next

            Log($"✅ {messages.Count} message(s) parsé(s) avec succès")
        Catch ex As Exception
            LogError("ParseMessagesFromResponse", ex)
        End Try

        Return messages
    End Function

    ''' <summary>
    ''' Parse un message individuel depuis le JSON
    ''' </summary>
    Private Function ParseSingleMessage(msgToken As JToken) As TuyaMessage
        Try
            Dim msg As New TuyaMessage()

            ' ID du message
            msg.Id = GetJsonString(msgToken, "id")
            If String.IsNullOrEmpty(msg.Id) Then
                msg.Id = GetJsonString(msgToken, "message_id")
            End If

            ' Titre et contenu
            msg.Title = GetJsonString(msgToken, "title")
            msg.Content = GetJsonString(msgToken, "content")
            If String.IsNullOrEmpty(msg.Content) Then
                msg.Content = GetJsonString(msgToken, "message")
            End If

            ' Statut lu/non-lu
            Dim readFlag = GetJsonString(msgToken, "read_flag")
            msg.IsRead = If(readFlag = "1" OrElse readFlag?.ToLower() = "true", True, False)

            ' Timestamp
            Dim timestampValue = msgToken("timestamp")
            If timestampValue IsNot Nothing Then
                Try
                    Dim timestamp = CLng(timestampValue.ToString())
                    msg.Timestamp = EPOCH_START.AddMilliseconds(timestamp)
                Catch
                    msg.Timestamp = DateTime.Now
                End Try
            Else
                msg.Timestamp = DateTime.Now
            End If

            ' Type de message
            Dim msgTypeStr = GetJsonString(msgToken, "message_type")
            If String.IsNullOrEmpty(msgTypeStr) Then
                msgTypeStr = GetJsonString(msgToken, "type")
            End If

            msg.MessageType = ParseMessageType(msgTypeStr)

            ' Informations sur l'appareil (si disponibles)
            msg.DeviceId = GetJsonString(msgToken, "device_id")
            msg.DeviceName = GetJsonString(msgToken, "device_name")
            msg.Icon = GetJsonString(msgToken, "icon")

            ' Stocker les données brutes pour référence
            If TypeOf msgToken Is JObject Then
                msg.RawData = CType(msgToken, JObject)
            End If

            Return msg
        Catch ex As Exception
            Log($"Erreur ParseSingleMessage: {ex.Message}")
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Parse le type de message depuis une string
    ''' </summary>
    Private Function ParseMessageType(typeStr As String) As MessageType
        If String.IsNullOrEmpty(typeStr) Then
            Return MessageType.Bulletin
        End If

        Select Case typeStr.ToLower()
            Case "home", "family", "1"
                Return MessageType.Home
            Case "bulletin", "notification", "2"
                Return MessageType.Bulletin
            Case "alarm", "alert", "3"
                Return MessageType.Alarm
            Case Else
                Return MessageType.Bulletin
        End Select
    End Function
#End Region

#Region "API - Alarmes des appareils"
    ''' <summary>
    ''' Récupère les alarmes d'un appareil spécifique
    ''' API: Utilise l'API d'historique des alarmes
    ''' </summary>
    Public Async Function GetDeviceAlarmsAsync(deviceId As String, Optional maxAlarms As Integer = 50) As Task(Of List(Of TuyaMessage))
        Try
            Log($"=== Récupération des alarmes pour l'appareil {deviceId} ===")

            Dim token = Await _tokenProvider.GetAccessTokenAsync()

            ' Endpoint pour les logs d'alarmes (fonctionne pour smart locks et autres appareils avec alarmes)
            Dim url = $"{_cfg.OpenApiBase}/v1.0/devices/{deviceId}/door-lock/alarm-logs?page_no=1&page_size={maxAlarms}"

            Log($"URL: {url}")

            Dim json = Await MakeApiCallAsync(url, token)

            If json IsNot Nothing AndAlso GetJsonBool(json, "success") Then
                Dim alarms As New List(Of TuyaMessage)

                Dim result = json("result")
                If result IsNot Nothing AndAlso TypeOf result Is JArray Then
                    Dim alarmArray = CType(result, JArray)

                    For Each alarmToken As JToken In alarmArray
                        Dim alarm As New TuyaMessage()
                        alarm.Id = GetJsonString(alarmToken, "id")
                        alarm.DeviceId = deviceId
                        alarm.Title = "Alarme appareil"
                        alarm.Content = GetJsonString(alarmToken, "content")
                        alarm.MessageType = MessageType.Alarm
                        alarm.IsRead = False

                        ' Timestamp
                        Dim ts = GetJsonString(alarmToken, "time")
                        If Not String.IsNullOrEmpty(ts) Then
                            Try
                                alarm.Timestamp = EPOCH_START.AddSeconds(CLng(ts))
                            Catch
                                alarm.Timestamp = DateTime.Now
                            End Try
                        End If

                        alarm.RawData = If(TypeOf alarmToken Is JObject, CType(alarmToken, JObject), Nothing)

                        alarms.Add(alarm)
                    Next
                End If

                Log($"✅ {alarms.Count} alarme(s) récupérée(s)")
                Return alarms
            Else
                Log("⚠️ Aucune alarme disponible pour cet appareil")
                Return New List(Of TuyaMessage)
            End If
        Catch ex As Exception
            LogError("GetDeviceAlarmsAsync", ex)
            Return New List(Of TuyaMessage)
        End Try
    End Function
#End Region

#Region "Requêtes HTTP"
    Private Async Function MakeApiCallAsync(url As String, token As String) As Task(Of JObject)
        Using client As New HttpClient()
            ConfigureRequestHeaders(client, url, token, "GET", EMPTY_BODY_HASH)

            Dim resp = Await client.GetAsync(url)
            Dim respBody = Await resp.Content.ReadAsStringAsync()

            Return JObject.Parse(respBody)
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

    Private Function CalculateSignature(httpMethod As String, bodyHash As String, path As String,
                                       token As String, timestamp As Long, nonce As String) As String
        ' Trier les query params selon la doc Tuya
        Dim sortedPath = SortQueryParameters(path)

        ' Construire stringToSign selon le protocole Tuya
        Dim stringToSign = httpMethod & vbLf & bodyHash & vbLf & "" & vbLf & sortedPath

        ' Construire la chaîne finale à signer
        Dim toSign = _cfg.AccessId & token & timestamp.ToString() & nonce & stringToSign

        Return TuyaTokenProvider.HmacSha256Upper(toSign, _cfg.AccessSecret)
    End Function

    Private Function SortQueryParameters(pathAndQuery As String) As String
        Dim questionMarkIndex = pathAndQuery.IndexOf("?"c)
        If questionMarkIndex = -1 Then
            Return pathAndQuery
        End If

        Dim path = pathAndQuery.Substring(0, questionMarkIndex)
        Dim queryString = pathAndQuery.Substring(questionMarkIndex + 1)

        Dim params = queryString.Split("&"c) _
            .OrderBy(Function(p) p) _
            .ToArray()

        Return path & "?" & String.Join("&", params)
    End Function

    Private Function GetTimestamp() As Long
        Return CLng((DateTime.UtcNow - EPOCH_START).TotalMilliseconds)
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

    Private Function GetJsonString(token As JToken, key As String) As String
        Return token?.SelectToken(key)?.ToString()
    End Function

    Private Function GetJsonBool(token As JToken, key As String) As Boolean
        Dim value = token?.SelectToken(key)
        If value Is Nothing Then Return False

        If TypeOf value Is JValue Then
            Try
                Return CBool(CType(value, JValue).Value)
            Catch
                Return False
            End Try
        End If

        Return False
    End Function
#End Region

#Region "IDisposable"
    Public Sub Dispose() Implements IDisposable.Dispose
        ' Pas de ressources à libérer pour l'instant
    End Sub
#End Region

End Class
