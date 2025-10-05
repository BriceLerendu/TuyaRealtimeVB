Imports System.Net.Http
Imports System.Text
Imports System.Threading
Imports Newtonsoft.Json.Linq

Public Class TuyaPulsarClient
    Private ReadOnly _cfg As TuyaConfig
    Private ReadOnly _tokenProvider As TuyaTokenProvider
    Private _cancellationToken As CancellationTokenSource
    Private _messageId As String = ""

    Public Event MessageReceived As Action(Of String)

    Public Sub New(cfg As TuyaConfig, tokenProvider As TuyaTokenProvider)
        _cfg = cfg
        _tokenProvider = tokenProvider
    End Sub

    Public Async Function StartAsync() As Task
        Try
            _cancellationToken = New CancellationTokenSource()

            Console.WriteLine("🔹 Démarrage du client Pulsar (mode polling)")

            ' Démarrer la boucle de récupération des messages
            Await PollMessagesLoop()

        Catch ex As Exception
            Console.WriteLine($"❌ Erreur Pulsar : {ex.Message}")
            Throw
        End Try
    End Function

    Private Async Function PollMessagesLoop() As Task
        While Not _cancellationToken.Token.IsCancellationRequested
            Try
                ' Récupérer les messages en attente
                Dim messages = Await PullMessagesAsync()

                If messages IsNot Nothing AndAlso messages.Count > 0 Then
                    For Each msg In messages
                        Console.WriteLine($"📨 Message Pulsar reçu")
                        RaiseEvent MessageReceived(msg.ToString())
                        ProcessPulsarMessage(msg.ToString())
                    Next
                End If

            Catch ex As Exception
                Console.WriteLine($"⚠️ Erreur polling : {ex.Message}")
            End Try

            ' Attendre avant la prochaine vérification (hors du Try/Catch)
            If Not _cancellationToken.Token.IsCancellationRequested Then
                Try
                    Await Task.Delay(2000, _cancellationToken.Token)
                Catch ex As TaskCanceledException
                    ' Normal lors de l'arrêt
                    Exit While
                End Try
            End If
        End While

        Console.WriteLine("⚠️ Polling Pulsar arrêté")
    End Function

    Private Async Function PullMessagesAsync() As Task(Of List(Of JObject))
        Dim url = $"{_cfg.OpenApiBase}/v1.0/open-hub/message/pull"
        Dim messages As New List(Of JObject)

        Using client As New HttpClient()
            Try
                Dim token = Await _tokenProvider.GetAccessTokenAsync()
                Dim t As Long = CLng((DateTime.UtcNow - New DateTime(1970, 1, 1)).TotalMilliseconds)
                Dim nonce As String = Guid.NewGuid().ToString("N")

                ' Headers
                client.DefaultRequestHeaders.Add("client_id", _cfg.AccessId)
                client.DefaultRequestHeaders.Add("access_token", token)
                client.DefaultRequestHeaders.Add("t", t.ToString())
                client.DefaultRequestHeaders.Add("sign_method", "HMAC-SHA256")
                client.DefaultRequestHeaders.Add("nonce", nonce)

                ' Signature
                Dim stringToSign = "GET" & vbLf & "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855" & vbLf & vbLf & "/v1.0/open-hub/message/pull"
                Dim toSign = _cfg.AccessId & token & t.ToString() & nonce & stringToSign
                Dim sign = TuyaTokenProvider.HmacSha256Upper(toSign, _cfg.AccessSecret)
                client.DefaultRequestHeaders.Add("sign", sign)

                Dim resp = Await client.GetAsync(url)
                Dim respBody = Await resp.Content.ReadAsStringAsync()

                Dim json = JObject.Parse(respBody)

                If json("success")?.Value(Of Boolean)() = True Then
                    Dim result = json("result")

                    If result IsNot Nothing Then
                        ' Les messages sont dans un tableau
                        Dim list = TryCast(result("list"), JArray)
                        If list IsNot Nothing Then
                            For Each item In list
                                ' Extraire le message réel (peut être encodé)
                                Dim msgData = item("data")?.ToString()
                                If Not String.IsNullOrEmpty(msgData) Then
                                    Try
                                        Dim msgJson = JObject.Parse(msgData)
                                        messages.Add(msgJson)
                                    Catch
                                        ' Si ce n'est pas du JSON, créer un objet simple
                                        messages.Add(New JObject(New JProperty("raw", msgData)))
                                    End Try
                                End If
                            Next
                        End If
                    End If
                Else
                    ' Pas d'erreur si pas de nouveau message
                    If json("code")?.Value(Of Integer)() <> 1106 Then
                        Console.WriteLine($"⚠️ Réponse API : {respBody}")
                    End If
                End If

            Catch ex As Exception
                Console.WriteLine($"❌ Erreur pull messages : {ex.Message}")
            End Try
        End Using

        Return messages
    End Function

    Private Sub ProcessPulsarMessage(message As String)
        Try
            Dim json = JObject.Parse(message)

            ' Vérifier si c'est un message de données d'appareil
            Dim devId = json.SelectToken("devId")?.ToString()
            Dim status = json.SelectToken("status")
            Dim bizCode = json.SelectToken("bizCode")?.ToString()

            If devId IsNot Nothing Then
                Console.WriteLine($"📱 Appareil : {devId}")

                If Not String.IsNullOrEmpty(bizCode) Then
                    Console.WriteLine($"🔔 Événement : {bizCode}")
                End If

                If status IsNot Nothing Then
                    Console.WriteLine("📊 Statuts :")
                    For Each item In status
                        Dim code = item.SelectToken("code")?.ToString()
                        Dim value = item.SelectToken("value")?.ToString()
                        Console.WriteLine($"   • {code} = {value}")
                    Next
                End If
            End If

            ' Afficher le message complet pour debug
            Console.WriteLine("📋 Message JSON :")
            Console.WriteLine(json.ToString(Newtonsoft.Json.Formatting.Indented))
            Console.WriteLine("────────────────────────────────")

        Catch ex As Exception
            Console.WriteLine($"⚠️ Erreur traitement : {ex.Message}")
            Console.WriteLine($"Message brut : {message}")
        End Try
    End Sub

    Public Sub [Stop]()
        _cancellationToken?.Cancel()
        Console.WriteLine("🛑 Client Pulsar arrêté")
    End Sub

    Public Sub Dispose()
        _cancellationToken?.Dispose()
    End Sub
End Class