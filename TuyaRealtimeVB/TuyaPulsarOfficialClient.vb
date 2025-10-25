Imports System.Buffers
Imports System.Security.Cryptography
Imports System.Text
Imports System.Threading
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports DotPulsar
Imports DotPulsar.Abstractions
Imports DotPulsar.Extensions

''' <summary>
''' Client Tuya Pulsar utilisant le protocole d'authentification officiel Tuya
''' Basé sur le SDK officiel : https://github.com/tuya/tuya-pulsar-sdk-dotnet
''' </summary>
Public Class TuyaPulsarOfficialClient
    Implements ITuyaRealtimeClient

    Private _config As TuyaConfig
    Private _logCallback As Action(Of String)
    Private _pulsarClient As IPulsarClient
    Private _consumer As IConsumer(Of ReadOnlySequence(Of Byte))
    Private _isRunning As Boolean = False
    Private _cancellationTokenSource As CancellationTokenSource
    Private _receiveTask As Task

    ' URLs Pulsar par région
    Private Const CN_SERVER_URL As String = "pulsar+ssl://mqe.tuyacn.com:7285/"
    Private Const US_SERVER_URL As String = "pulsar+ssl://mqe.tuyaus.com:7285/"
    Private Const EU_SERVER_URL As String = "pulsar+ssl://mqe.tuyaeu.com:7285/"
    Private Const IND_SERVER_URL As String = "pulsar+ssl://mqe.tuyain.com:7285/"

    ' Environnements
    Private Const MQ_ENV_PROD As String = "event"
    Private Const MQ_ENV_TEST As String = "event-test"

    Public Event DeviceStatusChanged(deviceId As String, statusData As JObject) _
        Implements ITuyaRealtimeClient.DeviceStatusChanged

    Public Sub New(config As TuyaConfig, Optional logCallback As Action(Of String) = Nothing)
        _config = config
        _logCallback = logCallback
    End Sub

    Public Async Function StartAsync() As Task(Of Boolean) Implements ITuyaRealtimeClient.StartAsync
        Try
            Log("=== CONNEXION TUYA PULSAR (SDK OFFICIEL) ===")
            Log($"AccessId: {_config.AccessId}")

            ' 1. Déterminer l'URL du serveur selon la région
            Dim serverUrl = GetServerUrl()
            Log($"Server URL: {serverUrl}")

            ' 2. Créer l'authentification Tuya personnalisée
            Dim auth As New TuyaAuthentication(_config.AccessId, _config.AccessSecret)
            Log("✅ Authentification Tuya créée")

            ' 3. Construire le topic Tuya : persistent://{ACCESS_ID}/out/{MQ_ENV}
            Dim mqEnv = MQ_ENV_PROD ' Production par défaut
            Dim topic = $"persistent://{_config.AccessId}/out/{mqEnv}"
            Dim subscription = $"{_config.AccessId}-sub"
            Log($"Topic: {topic}")
            Log($"Subscription: {subscription}")

            ' 4. Créer le client Pulsar avec l'auth Tuya
            _pulsarClient = PulsarClient.Builder() _
                .ServiceUrl(New Uri(serverUrl)) _
                .Authentication(auth) _
                .Build()

            Log("✅ Client Pulsar créé")

            ' 5. Créer le consumer
            _consumer = _pulsarClient.NewConsumer() _
                .SubscriptionName(subscription) _
                .Topic(topic) _
                .SubscriptionType(SubscriptionType.Failover) _
                .Create()

            Log("✅ Consumer Pulsar créé")

            _isRunning = True
            _cancellationTokenSource = New CancellationTokenSource()

            ' 6. Démarrer la réception des messages
            _receiveTask = Task.Run(AddressOf ReceiveMessagesAsync)

            Log("✅ Client temps réel démarré (SDK Officiel Tuya)")
            Return True

        Catch ex As Exception
            Log($"❌ Erreur StartAsync: {ex.GetType().Name}")
            Log($"   Message: {ex.Message}")
            If ex.InnerException IsNot Nothing Then
                Log($"   Inner: {ex.InnerException.Message}")
            End If
            Return False
        End Try
    End Function

    Private Function GetServerUrl() As String
        ' Détecter la région depuis la propriété Region
        Dim region = _config.Region.ToLower()

        Select Case region
            Case "eu"
                Return EU_SERVER_URL
            Case "us"
                Return US_SERVER_URL
            Case "cn"
                Return CN_SERVER_URL
            Case "in", "ind"
                Return IND_SERVER_URL
            Case Else
                ' Par défaut EU
                Return EU_SERVER_URL
        End Select
    End Function

    Private Async Function ReceiveMessagesAsync() As Task
        Try
            Log("📡 En écoute des messages Pulsar...")

            ' Boucle asynchrone sur les messages
            Dim messageEnumerator = _consumer.Messages(_cancellationTokenSource.Token).GetAsyncEnumerator(_cancellationTokenSource.Token)

            While _isRunning
                Try
                    If Not Await messageEnumerator.MoveNextAsync() Then
                        Exit While
                    End If

                    Dim message = messageEnumerator.Current

                    ' Traiter le message
                    ProcessMessage(message)

                    ' Acquitter le message
                    Await _consumer.AcknowledgeCumulative(message, _cancellationTokenSource.Token)

                Catch ex As OperationCanceledException
                    ' Normal lors de l'arrêt
                    Exit While
                Catch ex As Exception
                    Log($"❌ Erreur réception: {ex.GetType().Name} - {ex.Message}")
                    Threading.Thread.Sleep(1000)
                End Try
            End While

            Log("📡 Arrêt de l'écoute des messages Pulsar")

        Catch ex As Exception
            Log($"❌ Erreur ReceiveMessagesAsync: {ex.GetType().Name} - {ex.Message}")
        Finally
            _isRunning = False
        End Try
    End Function

    Private Sub ProcessMessage(message As IMessage(Of ReadOnlySequence(Of Byte)))
        Try
            ' Obtenir le mode de décryptage depuis les propriétés du message
            Dim decryptModel As String = Nothing
            message.Properties.TryGetValue("em", decryptModel)

            ' Convertir le payload en string
            Dim data = Encoding.UTF8.GetString(message.Data.ToArray())

            If _config.ShowRawPayloads Then
                Log($"📩 Message reçu (chiffré): {data}")
            End If

            ' Parser le JSON
            Dim payloadJson = JObject.Parse(data)

            ' Décrypter le champ "data"
            Dim encryptedData = payloadJson("data")?.ToString()
            If String.IsNullOrEmpty(encryptedData) Then
                Log("⚠️ Message sans champ 'data' chiffré")
                Return
            End If

            ' Choisir la méthode de décryptage
            Dim decryptedJson As String
            If decryptModel = "aes_gcm" Then
                decryptedJson = DecryptByGcm(encryptedData, _config.AccessSecret.Substring(8, 16))
            Else
                decryptedJson = DecryptByEcb(encryptedData, _config.AccessSecret.Substring(8, 16))
            End If

            ' Nettoyer les caractères de contrôle
            decryptedJson = CleanControlCharacters(decryptedJson)

            If _config.ShowRawPayloads Then
                Log($"📩 Message décrypté: {decryptedJson}")
            End If

            ' Parser le JSON décrypté
            Dim decryptedData = JObject.Parse(decryptedJson)

            ' Extraire deviceId et status
            Dim deviceId = decryptedData("devId")?.ToString()
            Dim status = decryptedData("status")

            If Not String.IsNullOrEmpty(deviceId) AndAlso status IsNot Nothing Then
                Log($"   Device: {deviceId}")

                ' Envoyer l'objet décrypté complet (contient devId, status, dataId, bizCode, etc.)
                RaiseEvent DeviceStatusChanged(deviceId, decryptedData)
            Else
                Log("⚠️ Message sans devId ou status")
            End If

        Catch ex As Exception
            Log($"❌ Erreur ProcessMessage: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Décrypte avec AES-GCM (mode moderne)
    ''' </summary>
    Private Function DecryptByGcm(encryptedData As String, key As String) As String
        Dim cadenaBytes = Convert.FromBase64String(encryptedData)
        Dim claveBytes = Encoding.UTF8.GetBytes(key)

        ' Les 12 premiers bytes sont le nonce
        Dim nonce(11) As Byte
        Array.Copy(cadenaBytes, 0, nonce, 0, nonce.Length)

        ' Les données chiffrées (excluant nonce et tag)
        Dim ciphertext(cadenaBytes.Length - nonce.Length - 16 - 1) As Byte
        Array.Copy(cadenaBytes, nonce.Length, ciphertext, 0, ciphertext.Length)

        ' Les 16 derniers bytes sont le tag d'authentification
        Dim tag(15) As Byte
        Array.Copy(cadenaBytes, cadenaBytes.Length - 16, tag, 0, tag.Length)

        Using aesGcm As New AesGcm(claveBytes)
            Dim plaintext(ciphertext.Length - 1) As Byte
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext)
            Return Encoding.UTF8.GetString(plaintext)
        End Using
    End Function

    ''' <summary>
    ''' Décrypte avec AES-ECB (mode ancien)
    ''' </summary>
    Private Function DecryptByEcb(encryptedData As String, key As String) As String
        Try
            Dim cadenaBytes = Convert.FromBase64String(encryptedData)
            Dim claveBytes = Encoding.UTF8.GetBytes(key)

            Using rijndael As New System.Security.Cryptography.RijndaelManaged()
                rijndael.Mode = CipherMode.ECB
                rijndael.BlockSize = 128
                rijndael.Padding = PaddingMode.Zeros

                Using decryptor = rijndael.CreateDecryptor(claveBytes, rijndael.IV)
                    Using memStream As New IO.MemoryStream(cadenaBytes)
                        Using cryptoStream As New CryptoStream(memStream, decryptor, CryptoStreamMode.Read)
                            Using streamReader As New IO.StreamReader(cryptoStream)
                                Return streamReader.ReadToEnd()
                            End Using
                        End Using
                    End Using
                End Using
            End Using

        Catch ex As Exception
            Log($"❌ Erreur décryptage ECB: {ex.Message}")
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Nettoie les caractères de contrôle du JSON décrypté
    ''' </summary>
    Private Function CleanControlCharacters(input As String) As String
        If String.IsNullOrEmpty(input) Then
            Return input
        End If

        ' Supprimer tous les caractères de contrôle et NULL bytes
        Return New String(input.Where(Function(c) Not Char.IsControl(c) OrElse c = vbLf OrElse c = vbCr OrElse c = vbTab).ToArray()).Trim()
    End Function

    Public Sub [Stop]() Implements ITuyaRealtimeClient.Stop
        Try
            Log("Arrêt du client Pulsar officiel...")
            _isRunning = False

            _cancellationTokenSource?.Cancel()

            If _receiveTask IsNot Nothing Then
                _receiveTask.Wait(TimeSpan.FromSeconds(3))
            End If

            _consumer?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2))
            _pulsarClient?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2))

            Log("✅ Client Pulsar officiel arrêté")

        Catch ex As Exception
            Log($"⚠️ Erreur Stop: {ex.Message}")
        Finally
            _consumer = Nothing
            _pulsarClient = Nothing
            _cancellationTokenSource?.Dispose()
            _cancellationTokenSource = Nothing
        End Try
    End Sub

    Public Function IsRunning() As Boolean Implements ITuyaRealtimeClient.IsRunning
        Return _isRunning AndAlso _consumer IsNot Nothing
    End Function

    Public ReadOnly Property Mode As RealtimeMode Implements ITuyaRealtimeClient.Mode
        Get
            Return RealtimeMode.DotNetPulsarOfficial
        End Get
    End Property

    Private Sub Log(message As String)
        _logCallback?.Invoke(message)
    End Sub
End Class

''' <summary>
''' Authentification personnalisée pour Tuya Pulsar
''' Format : {"username": accessId, "password": password dérivé}
''' Password = MD5(accessId + MD5(accessKey)).substring(8, 16)
''' </summary>
Public Class TuyaAuthentication
    Implements IAuthentication

    Private ReadOnly _authData As String

    Public Sub New(accessId As String, accessKey As String)
        ' Générer le password selon le format Tuya
        Dim password = GeneratePassword(accessId, accessKey)
        _authData = $"{{""username"":""{accessId}"", ""password"":""{password}""}}"
    End Sub

    Public ReadOnly Property AuthenticationMethodName As String Implements IAuthentication.AuthenticationMethodName
        Get
            Return "auth1"
        End Get
    End Property

    Public Function GetAuthenticationData(cancellationToken As CancellationToken) As ValueTask(Of Byte()) Implements IAuthentication.GetAuthenticationData
        Return New ValueTask(Of Byte())(Encoding.UTF8.GetBytes(_authData))
    End Function

    ''' <summary>
    ''' Génère le password Tuya : MD5(accessId + MD5(accessKey)).substring(8, 16)
    ''' </summary>
    Private Function GeneratePassword(accessId As String, accessKey As String) As String
        ' MD5 de l'accessKey
        Dim md5HexKey = ComputeMD5(accessKey)

        ' MD5 de (accessId + MD5(accessKey))
        Dim mixStr = accessId & md5HexKey
        Dim md5MixStr = ComputeMD5(mixStr)

        ' Prendre 16 caractères à partir de la position 8
        Return md5MixStr.Substring(8, 16)
    End Function

    ''' <summary>
    ''' Calcule le MD5 d'une chaîne et retourne le hash en hexadécimal lowercase
    ''' </summary>
    Private Function ComputeMD5(input As String) As String
        Using md5 As MD5 = MD5.Create()
            Dim dataHash = md5.ComputeHash(Encoding.UTF8.GetBytes(input))
            Dim sb As New StringBuilder()
            For Each b In dataHash
                sb.Append(b.ToString("x2").ToLower())
            Next
            Return sb.ToString()
        End Using
    End Function
End Class
