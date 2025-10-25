Imports System.Net.Http
Imports System.Security.Cryptography
Imports System.Text
Imports Newtonsoft.Json.Linq

Public Class TuyaTokenProvider
    Private ReadOnly _cfg As TuyaConfig
    Private ReadOnly _http As HttpClient
    Private _token As String = Nothing
    Private _expiryUtc As DateTime = Date.MinValue

    Public Sub New(cfg As TuyaConfig)
        _cfg = cfg
        _http = New HttpClient()
        _http.Timeout = TimeSpan.FromSeconds(30)
    End Sub

    ' === Helpers ===
    Public Shared Function Sha256Hex(input As String) As String
        Using sha As SHA256 = SHA256.Create()
            Dim bytes = Encoding.UTF8.GetBytes(input)
            Dim hash = sha.ComputeHash(bytes)
            Dim sb As New StringBuilder(CInt(hash.Length) * 2)
            For Each b As Byte In hash
                sb.Append(b.ToString("x2"))
            Next
            Return sb.ToString()
        End Using
    End Function

    Public Shared Function HmacSha256Upper(data As String, secret As String) As String
        Using hmac As New HMACSHA256(Encoding.UTF8.GetBytes(secret))
            Dim hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data))
            Dim sb As New StringBuilder()
            For Each b In hash
                sb.Append(b.ToString("x2"))
            Next
            Return sb.ToString().ToUpperInvariant()
        End Using
    End Function

    Private Function BuildStringToSign(method As String, urlPathAndQuery As String, body As String, optionalHeaders As String) As String
        Dim contentSha256 As String
        If String.IsNullOrEmpty(body) Then
            contentSha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855" ' SHA256("")
        Else
            contentSha256 = Sha256Hex(body)
        End If

        Return method & vbLf &
               contentSha256 & vbLf &
               optionalHeaders & vbLf &
               urlPathAndQuery
    End Function

    ' === Token retrieval ===
    Public Async Function GetAccessTokenAsync() As Task(Of String)
        If _token Is Nothing OrElse DateTime.UtcNow > _expiryUtc.AddSeconds(-60) Then
            Await RefreshAsync()
        End If
        Return _token
    End Function

    Public Async Function RefreshAsync() As Task
        Dim t As Long = CLng((DateTime.UtcNow - New DateTime(1970, 1, 1)).TotalMilliseconds)
        Dim nonce As String = Guid.NewGuid().ToString("N")
        Dim pathAndQuery As String = "/v1.0/token?grant_type=1"

        Dim stringToSign As String = BuildStringToSign("GET", pathAndQuery, "", "")
        Dim toHmac As String = _cfg.AccessId & t.ToString() & nonce & stringToSign
        Dim sign As String = HmacSha256Upper(toHmac, _cfg.AccessSecret)

        Dim url As String = _cfg.OpenApiBase & pathAndQuery
        Console.WriteLine("🔹 URL: " & url)
        Console.WriteLine("🔹 Sign: " & sign)
        Console.WriteLine("🔹 StringToSign: " & stringToSign)

        Dim req As New HttpRequestMessage(HttpMethod.Get, url)
        req.Headers.Add("client_id", _cfg.AccessId)
        req.Headers.Add("t", t.ToString())
        req.Headers.Add("nonce", nonce)
        req.Headers.Add("sign_method", "HMAC-SHA256")
        req.Headers.Add("sign", sign)
        req.Headers.Add("Accept", "application/json")

        Using resp = Await _http.SendAsync(req)
            Dim body = Await resp.Content.ReadAsStringAsync()
            Console.WriteLine("🔹 Réponse brute: " & body)
            Dim json = JObject.Parse(body)
            Dim ok = json.Value(Of Boolean?)("success")

            If Not ok.GetValueOrDefault(False) Then
                Throw New Exception("Tuya token response not success: " & body)
            End If

            Dim result = json("result")
            _token = result.Value(Of String)("access_token")
            Dim expiresIn = result.Value(Of Integer?)("expire_time")

            ' ✅ CORRECTION : Récupération du UID

            If Not expiresIn.HasValue OrElse expiresIn.Value = 0 Then
                _expiryUtc = DateTime.UtcNow.AddHours(2)
            Else
                _expiryUtc = DateTime.UtcNow.AddSeconds(expiresIn.Value) ' ✅ correction ici
            End If

            Console.WriteLine("✅ Token obtenu avec succès, expiration à " & _expiryUtc)
        End Using
    End Function

    Protected Overrides Sub Finalize()
        _http.Dispose()
        MyBase.Finalize()
    End Sub

End Class
