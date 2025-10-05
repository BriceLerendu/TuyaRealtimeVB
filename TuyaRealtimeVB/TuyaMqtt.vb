Imports System.Net.Http
Imports System.Text
Imports MQTTnet
Imports MQTTnet.Client
Imports MQTTnet.Client.Options
Imports MQTTnet.Protocol
Imports Newtonsoft.Json.Linq

Public Class TuyaMqttClient
    Private ReadOnly _cfg As TuyaConfig
    Private ReadOnly _tokenProvider As TuyaTokenProvider
    Private _client As IMqttClient
    Private _currentTopic As String

    Public Sub New(cfg As TuyaConfig, tokenProvider As TuyaTokenProvider)
        _cfg = cfg
        _tokenProvider = tokenProvider
    End Sub

    Public Async Function ConnectAndSubscribeAsync() As Task
        Dim token = Await _tokenProvider.GetAccessTokenAsync()

        Dim factory As New MqttFactory()
        _client = factory.CreateMqttClient()

        ' Gestion des événements
        AddHandler _client.ApplicationMessageReceivedAsync, AddressOf OnMessageReceived
        AddHandler _client.ConnectedAsync, AddressOf OnConnected
        AddHandler _client.DisconnectedAsync, AddressOf OnDisconnected

        Try
            Console.WriteLine("🔹 Tentative de connexion au broker MQTT Tuya...")

            Dim options = Await BuildOptionsAsync()
            Dim connectTask = _client.ConnectAsync(options)

            If Await Task.WhenAny(connectTask, Task.Delay(8000)) Is connectTask Then
                Console.WriteLine("✅ Connecté au broker MQTT Tuya !")
                Dim refreshTask = Task.Run(AddressOf RefreshLoop)
            Else
                Throw New TimeoutException("La connexion MQTT a expiré (timeout 8s).")
            End If

        Catch ex As Exception
            Console.WriteLine("❌ Erreur de connexion MQTT : " & ex.Message)
        End Try
    End Function

    ' === Réception des messages MQTT ===
    Private Function OnMessageReceived(e As MqttApplicationMessageReceivedEventArgs) As Task
        Try
            Dim topic As String = e.ApplicationMessage.Topic
            Dim payload As String = If(e.ApplicationMessage.Payload IsNot Nothing, Encoding.UTF8.GetString(e.ApplicationMessage.Payload), "")

            Console.WriteLine("📨 --- MESSAGE MQTT REÇU ---")
            Console.WriteLine($"📡 Topic : {topic}")
            Console.WriteLine($"📦 Payload brut : {payload}")

            If String.IsNullOrWhiteSpace(payload) Then
                Console.WriteLine("⚠️ Payload vide reçu (aucun contenu).")
                Return Task.CompletedTask
            End If

            ' --- Parsing JSON ---
            Dim json As JObject
            Try
                json = JObject.Parse(payload)
            Catch ex As Exception
                Console.WriteLine($"⚠️ Erreur de parsing JSON : {ex.Message}")
                Return Task.CompletedTask
            End Try

            ' --- Détection du protocole TYLINK (v2.0) ---
            If topic.StartsWith("tylink/") Then
                Console.WriteLine("🚀 Message TYLINK (v2.0) reçu.")
                HandleTylinkMessage(topic, json)
            Else
                HandleLegacyMessage(topic, json)
            End If

        Catch ex As Exception
            Console.WriteLine("💥 Erreur inattendue dans OnMessageReceived : " & ex.Message)
        End Try

        Console.WriteLine("────────────────────────────────────────────")
        Return Task.CompletedTask
    End Function

    ' === Gestion du format TYLINK (v2.0) ===
    Private Sub HandleTylinkMessage(topic As String, json As JObject)
        Try
            Dim msgType = json.SelectToken("type")?.ToString()
            Dim devId = json.SelectToken("data.id")?.ToString()

            If msgType = "thing.property.report" Then
                Console.WriteLine($"💡 Propriétés rapportées pour {devId}")
                Dim props = json.SelectToken("data.properties")
                If props IsNot Nothing Then
                    For Each prop In props.Children(Of JProperty)()
                        Console.WriteLine($"   • {prop.Name} = {prop.Value}")
                    Next
                End If
            ElseIf msgType = "thing.event.trigger" Then
                Console.WriteLine($"⚙️ Événement déclenché sur {devId}")
            Else
                Console.WriteLine($"📚 Message TYLINK non reconnu (type={msgType})")
            End If
        Catch ex As Exception
            Console.WriteLine($"⚠️ Erreur traitement TYLINK : {ex.Message}")
        End Try
    End Sub

    ' === Gestion de l'ancien format Tuya MQTT (v1.0) ===
    Private Sub HandleLegacyMessage(topic As String, json As JObject)
        Try
            Dim eventType As String = json.SelectToken("type")?.ToString()
            Dim devId As String = json.SelectToken("data.devId")?.ToString()
            Dim statusList As JToken = json.SelectToken("data.status")

            If eventType = "thing.status.report" Then
                Console.WriteLine("💡 Notification d’état d’appareil reçue (v1.0).")
                If devId IsNot Nothing AndAlso statusList IsNot Nothing Then
                    Console.WriteLine($"🔔 Appareil : {devId}")
                    For Each s In statusList
                        Dim code = s("code")?.ToString()
                        Dim value = s("value")?.ToString()
                        Console.WriteLine($"   • {code} = {value}")
                    Next
                End If
            Else
                Console.WriteLine($"📚 Message MQTT (v1.0) non reconnu : {eventType}")
            End If
        Catch ex As Exception
            Console.WriteLine($"⚠️ Erreur traitement legacy : {ex.Message}")
        End Try
    End Sub

    ' === Connexion MQTT ===
    Private Async Function OnConnected(e As MqttClientConnectedEventArgs) As Task
        Try
            Console.WriteLine("✅ Connecté au broker MQTT Tuya (événement)")

            Dim token = Await _tokenProvider.GetAccessTokenAsync()
            Dim cfgResp = Await GetMqttCredentialsAsync(token)
            Dim creds = CType(cfgResp("result"), JObject)

            Dim sourceTopic = creds.SelectToken("source_topic.device")?.ToString()
            Dim sinkTopic = creds.SelectToken("sink_topic.device")?.ToString()

            Console.WriteLine($"📡 Topic source MQTT : {sourceTopic}")
            Console.WriteLine($"📡 Topic sink MQTT : {sinkTopic}")

            ' 🔸 Abonnement legacy (v1)
            Dim wildcardTopic = sinkTopic.Replace("{device_id}", "#")
            Await _client.SubscribeAsync(wildcardTopic, MqttQualityOfServiceLevel.AtMostOnce)
            Console.WriteLine($"📡 Abonné au topic global (legacy) : {wildcardTopic}")

            ' 🔸 Abonnement TYLINK (v2.0)
            Await _client.SubscribeAsync("tylink/#", MqttQualityOfServiceLevel.AtMostOnce)
            Console.WriteLine("📡 Abonné aux topics TYLINK (v2.0) : tylink/#")

            ' 🔸 Abonnement source topic
            If Not String.IsNullOrEmpty(sourceTopic) Then
                Await _client.SubscribeAsync(sourceTopic, MqttQualityOfServiceLevel.AtMostOnce)
                Console.WriteLine($"📡 Abonné aussi à : {sourceTopic}")
            End If

        Catch ex As Exception
            Console.WriteLine("❌ Erreur abonnement MQTT : " & ex.Message)
        End Try
    End Function

    ' === Déconnexion ===
    Private Async Function OnDisconnected(e As MqttClientDisconnectedEventArgs) As Task
        Console.WriteLine("⚠️ Déconnecté du broker MQTT Tuya.")
        Await Task.Delay(5000)
        Try
            Console.WriteLine("🔄 Recréation du client MQTT...")
            Dim factory As New MqttFactory()
            _client = factory.CreateMqttClient()
            AddHandler _client.ApplicationMessageReceivedAsync, AddressOf OnMessageReceived
            AddHandler _client.ConnectedAsync, AddressOf OnConnected
            AddHandler _client.DisconnectedAsync, AddressOf OnDisconnected
            Dim options = Await BuildOptionsAsync()
            Await _client.ConnectAsync(options)
            Console.WriteLine("✅ Reconnecté au broker MQTT Tuya !")
        Catch ex As Exception
            Console.WriteLine("❌ Erreur reconnexion MQTT : " & ex.Message)
        End Try
    End Function

    ' === Boucle de rafraîchissement du token ===
    Private Async Function RefreshLoop() As Task
        While True
            Await Task.Delay(TimeSpan.FromMinutes(5))
            Try
                Await _tokenProvider.RefreshAsync()
                Dim newToken = Await _tokenProvider.GetAccessTokenAsync()
                Console.WriteLine("🔁 Token Tuya rafraîchi.")
            Catch ex As Exception
                Console.WriteLine("[Erreur refresh token] " & ex.Message)
            End Try
        End While
    End Function

    ' === Options MQTT ===
    Private Async Function BuildOptionsAsync() As Task(Of MqttClientOptions)
        ' 🔹 Étape 1 — Récupération du token Tuya
        Dim token = Await _tokenProvider.GetAccessTokenAsync()

        ' 🔹 Étape 2 — Appel à l’API MQTT Tuya
        Dim cfgResp = Await GetMqttCredentialsAsync(token)

        If cfgResp Is Nothing Then
            Throw New Exception("❌ Erreur : la requête GetMqttCredentialsAsync a renvoyé Nothing (aucune réponse de Tuya).")
        End If

        ' 🔹 Étape 3 — Vérification du champ "result"
        If cfgResp("result") Is Nothing Then
            Console.WriteLine("⚠️ Réponse Tuya sans champ 'result' :")
            Console.WriteLine(cfgResp.ToString())
            Throw New Exception("⚠️ La réponse Tuya ne contient pas de résultat MQTT valide.")
        End If

        Dim creds As JObject = CType(cfgResp("result"), JObject)

        ' 🔹 Étape 4 — Vérification de tous les champs attendus
        If creds("url") Is Nothing OrElse creds("client_id") Is Nothing OrElse creds("username") Is Nothing OrElse creds("password") Is Nothing Then
            Console.WriteLine("⚠️ Réponse incomplète de Tuya :")
            Console.WriteLine(creds.ToString(Newtonsoft.Json.Formatting.Indented))
            Throw New Exception("⚠️ Un ou plusieurs champs MQTT manquent dans la réponse Tuya.")
        End If

        ' 🔹 Étape 5 — Extraction des informations
        Dim urlStr As String = creds("url").ToString()
        Dim clientId As String = creds("client_id").ToString()
        Dim username As String = creds("username").ToString()
        Dim password As String = creds("password").ToString()

        ' 🔹 Extraction du topic source (si présent)
        _currentTopic = creds.SelectToken("source_topic.device")?.ToString()

        If String.IsNullOrEmpty(_currentTopic) Then
            Console.WriteLine("⚠️ Aucun topic source détecté dans la config MQTT.")
        End If

        ' 🔹 Logging des infos pour debug
        Console.WriteLine($"🌐 MQTT config : {urlStr}")
        Console.WriteLine($"🔑 ClientID    : {clientId}")
        Console.WriteLine($"👤 Username    : {username}")
        Console.WriteLine($"📡 Topic source: {_currentTopic}")

        ' 🔹 Étape 6 — Construction des options MQTT
        Dim builder As New MqttClientOptionsBuilder()
        builder.WithClientId(clientId)
        builder.WithCredentials(username, password)
        builder.WithCleanSession()

        builder.WithTlsOptions(Function()
                                   Return New MqttClientTlsOptions With {
                                   .UseTls = True,
                                   .IgnoreCertificateChainErrors = True,
                                   .IgnoreCertificateRevocationErrors = True
                               }
                               End Function)

        ' 🔹 Analyse de l’URL et connexion au bon serveur
        Try
            Dim uri As New Uri(urlStr)
            builder.WithTcpServer(uri.Host, uri.Port)
        Catch ex As Exception
            Console.WriteLine($"❌ Erreur d’analyse de l’URL MQTT '{urlStr}' : {ex.Message}")
            Throw
        End Try

        Return builder.Build()
    End Function


    ' === Requête d’identifiants MQTT Tuya ===
    Private Async Function GetMqttCredentialsAsync(token As String) As Task(Of JObject)
        Dim url = $"{_cfg.OpenApiBase}/v1.0/iot-03/open-hub/access-config"
        Dim body As String = $"{{""uid"":""{_cfg.Uid}"",""link_id"":""{Guid.NewGuid().ToString("N")}"",""link_type"":""mqtt"",""topics"":""device"",""msg_encrypted_version"":""2.0""}}"

        Using client As New HttpClient()
            Dim t As Long = CLng((DateTime.UtcNow - New DateTime(1970, 1, 1)).TotalMilliseconds)
            Dim nonce As String = Guid.NewGuid().ToString("N")

            client.DefaultRequestHeaders.Add("client_id", _cfg.AccessId)
            client.DefaultRequestHeaders.Add("t", t.ToString())
            client.DefaultRequestHeaders.Add("nonce", nonce)
            client.DefaultRequestHeaders.Add("sign_method", "HMAC-SHA256")
            client.DefaultRequestHeaders.Add("access_token", token)

            Dim contentHash = ComputeSHA256(body)
            Dim stringToSign = "POST" & vbLf & contentHash & vbLf & vbLf & "/v1.0/iot-03/open-hub/access-config"
            Dim toSign = _cfg.AccessId & token & t.ToString() & nonce & stringToSign
            Dim sign = TuyaTokenProvider.HmacSha256Upper(toSign, _cfg.AccessSecret)
            client.DefaultRequestHeaders.Add("sign", sign)

            Dim httpContent As New StringContent(body, Encoding.UTF8, "application/json")

            Console.WriteLine("🌍 MQTT config request:")
            Console.WriteLine(body)

            Dim resp = Await client.PostAsync(url, httpContent)
            Dim respBody = Await resp.Content.ReadAsStringAsync()
            Console.WriteLine("🌍 MQTT config response:")
            Console.WriteLine(respBody)

            Dim json = JObject.Parse(respBody)
            Return json
        End Using
    End Function

    Private Function ComputeSHA256(text As String) As String
        Using sha As Security.Cryptography.SHA256 = Security.Cryptography.SHA256.Create()
            Dim bytes = Encoding.UTF8.GetBytes(text)
            Dim hash = sha.ComputeHash(bytes)
            Return BitConverter.ToString(hash).Replace("-", "").ToLower()
        End Using
    End Function
End Class
