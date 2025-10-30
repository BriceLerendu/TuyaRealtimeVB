Imports System
Imports System.Threading.Tasks
Imports System.Net.Http
Imports Newtonsoft.Json.Linq

''' <summary>
''' Outil de diagnostic pour tester l'API Message Center de Tuya
''' Ce programme teste différents endpoints pour identifier pourquoi aucun message n'est retourné
''' </summary>
Module TestMessageCenterAPI
    Sub Main()
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║     DIAGNOSTIC API MESSAGE CENTER TUYA                        ║")
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Try
            ' Charger la configuration
            Dim cfg = TuyaConfig.LoadFromFile("config.json")
            Console.WriteLine($"✓ Configuration chargée")
            Console.WriteLine($"  - Region: {cfg.Region}")
            Console.WriteLine($"  - User ID: {cfg.Uid}")
            Console.WriteLine($"  - API Base: {cfg.OpenApiBase}")
            Console.WriteLine()

            ' Créer le token provider
            Dim tokenProvider = New TuyaTokenProvider(cfg)
            Console.WriteLine("✓ Token provider créé")
            Console.WriteLine()

            ' Exécuter les tests
            TestMessageCenterEndpoints(cfg, tokenProvider).Wait()

        Catch ex As Exception
            Console.WriteLine()
            Console.WriteLine($"❌ ERREUR FATALE: {ex.Message}")
            Console.WriteLine($"Stack Trace: {ex.StackTrace}")
        End Try

        Console.WriteLine()
        Console.WriteLine("Appuyez sur une touche pour quitter...")
        Console.ReadKey()
    End Sub

    Private Async Function TestMessageCenterEndpoints(cfg As TuyaConfig, tokenProvider As TuyaTokenProvider) As Task
        Console.WriteLine("═══════════════════════════════════════════════════════════════")
        Console.WriteLine("TEST 1: Endpoint /v1.0/sdf/notifications/messages")
        Console.WriteLine("═══════════════════════════════════════════════════════════════")

        Try
            Dim token = Await tokenProvider.GetAccessTokenAsync()
            Console.WriteLine($"✓ Token obtenu: {token.Substring(0, Math.Min(20, token.Length))}...")
            Console.WriteLine()

            ' Test 1: Endpoint standard avec tous les paramètres
            Console.WriteLine("➤ Test avec paramètres: recipient_id, page_no, page_size")
            Dim url1 = $"{cfg.OpenApiBase}/v1.0/sdf/notifications/messages?recipient_id={cfg.Uid}&page_no=1&page_size=50"
            Await TestEndpoint(url1, token, cfg, "GET")
            Console.WriteLine()

            ' Test 2: Endpoint avec message_type
            Console.WriteLine("➤ Test avec message_type=1 (Home/Famille)")
            Dim url2 = $"{cfg.OpenApiBase}/v1.0/sdf/notifications/messages?recipient_id={cfg.Uid}&page_no=1&page_size=50&message_type=1"
            Await TestEndpoint(url2, token, cfg, "GET")
            Console.WriteLine()

            ' Test 3: Endpoint avec message_type=2 (Bulletin)
            Console.WriteLine("➤ Test avec message_type=2 (Bulletin)")
            Dim url3 = $"{cfg.OpenApiBase}/v1.0/sdf/notifications/messages?recipient_id={cfg.Uid}&page_no=1&page_size=50&message_type=2"
            Await TestEndpoint(url3, token, cfg, "GET")
            Console.WriteLine()

            ' Test 4: Endpoint avec message_type=3 (Alarmes)
            Console.WriteLine("➤ Test avec message_type=3 (Alarmes)")
            Dim url4 = $"{cfg.OpenApiBase}/v1.0/sdf/notifications/messages?recipient_id={cfg.Uid}&page_no=1&page_size=50&message_type=3"
            Await TestEndpoint(url4, token, cfg, "GET")
            Console.WriteLine()

            ' Test 5: Endpoint sans recipient_id (pour voir l'erreur)
            Console.WriteLine("➤ Test SANS recipient_id (pour voir le comportement)")
            Dim url5 = $"{cfg.OpenApiBase}/v1.0/sdf/notifications/messages?page_no=1&page_size=50"
            Await TestEndpoint(url5, token, cfg, "GET")
            Console.WriteLine()

            Console.WriteLine("═══════════════════════════════════════════════════════════════")
            Console.WriteLine("TEST 2: Endpoints alternatifs")
            Console.WriteLine("═══════════════════════════════════════════════════════════════")

            ' Test 6: Endpoint notifications (alternatif)
            Console.WriteLine("➤ Test endpoint alternatif: /v1.0/users/{uid}/notifications")
            Dim url6 = $"{cfg.OpenApiBase}/v1.0/users/{cfg.Uid}/notifications?page_no=1&page_size=50"
            Await TestEndpoint(url6, token, cfg, "GET")
            Console.WriteLine()

            ' Test 7: Endpoint messages (alternatif)
            Console.WriteLine("➤ Test endpoint alternatif: /v1.0/users/{uid}/messages")
            Dim url7 = $"{cfg.OpenApiBase}/v1.0/users/{cfg.Uid}/messages?page_no=1&page_size=50"
            Await TestEndpoint(url7, token, cfg, "GET")
            Console.WriteLine()

            Console.WriteLine("═══════════════════════════════════════════════════════════════")
            Console.WriteLine("RÉSUMÉ DES TESTS")
            Console.WriteLine("═══════════════════════════════════════════════════════════════")
            Console.WriteLine()
            Console.WriteLine("Si tous les tests retournent success=false ou code d'erreur:")
            Console.WriteLine("  → Vérifiez que l'API 'Message Service' est activée dans votre projet Tuya")
            Console.WriteLine("  → Allez sur https://iot.tuya.com → Cloud → Project → Votre Projet → API")
            Console.WriteLine("  → Recherchez 'Message' et activez les services nécessaires")
            Console.WriteLine()
            Console.WriteLine("Si tous les tests retournent success=true mais result vide:")
            Console.WriteLine("  → Il n'y a probablement aucun message dans votre Message Center")
            Console.WriteLine("  → Testez en envoyant une notification depuis l'app SmartLife")
            Console.WriteLine()

        Catch ex As Exception
            Console.WriteLine($"❌ Erreur lors des tests: {ex.Message}")
            Console.WriteLine($"Stack: {ex.StackTrace}")
        End Try
    End Function

    Private Async Function TestEndpoint(url As String, token As String, cfg As TuyaConfig, httpMethod As String) As Task
        Try
            Console.WriteLine($"📡 URL: {url}")

            Using client As New HttpClient()
                ' Configurer les headers Tuya
                Dim t = GetTimestamp()
                Dim nonce = Guid.NewGuid().ToString("N")
                Dim path = New Uri(url).PathAndQuery
                Dim bodyHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855" ' Empty body SHA256

                Dim sign = CalculateSignature(cfg, httpMethod, bodyHash, path, token, t, nonce)

                client.DefaultRequestHeaders.Add("client_id", cfg.AccessId)
                client.DefaultRequestHeaders.Add("access_token", token)
                client.DefaultRequestHeaders.Add("t", t.ToString())
                client.DefaultRequestHeaders.Add("sign_method", "HMAC-SHA256")
                client.DefaultRequestHeaders.Add("nonce", nonce)
                client.DefaultRequestHeaders.Add("sign", sign)

                ' Faire la requête
                Dim response = Await client.GetAsync(url)
                Dim responseBody = Await response.Content.ReadAsStringAsync()

                Console.WriteLine($"📥 HTTP Status: {response.StatusCode}")

                ' Parser le JSON
                Try
                    Dim json = JObject.Parse(responseBody)
                    Console.WriteLine($"📄 Réponse JSON:")
                    Console.WriteLine(json.ToString(Newtonsoft.Json.Formatting.Indented))

                    ' Analyser la réponse
                    Dim success = If(json("success") IsNot Nothing, CBool(json("success").ToString()), False)
                    Console.WriteLine()
                    If success Then
                        Console.WriteLine($"✅ SUCCESS = true")

                        ' Vérifier le contenu de result
                        Dim result = json("result")
                        If result IsNot Nothing Then
                            If TypeOf result Is JArray Then
                                Dim arr = CType(result, JArray)
                                Console.WriteLine($"   → result est un tableau avec {arr.Count} élément(s)")
                            ElseIf TypeOf result Is JObject Then
                                Dim obj = CType(result, JObject)
                                Console.WriteLine($"   → result est un objet avec {obj.Properties().Count()} propriété(s)")

                                ' Chercher un tableau dans l'objet
                                If obj("list") IsNot Nothing Then
                                    Console.WriteLine($"   → Trouvé 'list' dans result")
                                End If
                                If obj("messages") IsNot Nothing Then
                                    Console.WriteLine($"   → Trouvé 'messages' dans result")
                                End If
                            End If
                        Else
                            Console.WriteLine($"   → result est NULL")
                        End If
                    Else
                        Console.WriteLine($"❌ SUCCESS = false")
                        Dim code = If(json("code")?.ToString(), "N/A")
                        Dim msg = If(json("msg")?.ToString(), "N/A")
                        Console.WriteLine($"   → Code: {code}")
                        Console.WriteLine($"   → Message: {msg}")
                    End If
                Catch jsonEx As Exception
                    Console.WriteLine($"⚠️ Impossible de parser le JSON: {jsonEx.Message}")
                    Console.WriteLine($"Réponse brute: {responseBody}")
                End Try
            End Using

        Catch ex As Exception
            Console.WriteLine($"❌ Erreur: {ex.Message}")
        End Try
    End Function

    Private Function CalculateSignature(cfg As TuyaConfig, httpMethod As String, bodyHash As String, path As String,
                                       token As String, timestamp As Long, nonce As String) As String
        ' Trier les query params selon la doc Tuya
        Dim sortedPath = SortQueryParameters(path)

        ' Construire stringToSign selon le protocole Tuya
        Dim stringToSign = httpMethod & vbLf & bodyHash & vbLf & "" & vbLf & sortedPath

        ' Construire la chaîne finale à signer
        Dim toSign = cfg.AccessId & token & timestamp.ToString() & nonce & stringToSign

        Return TuyaTokenProvider.HmacSha256Upper(toSign, cfg.AccessSecret)
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
        Dim epochStart As New DateTime(1970, 1, 1)
        Return CLng((DateTime.UtcNow - epochStart).TotalMilliseconds)
    End Function
End Module
