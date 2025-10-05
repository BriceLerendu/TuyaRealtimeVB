Imports System.Net.Http
Imports Newtonsoft.Json.Linq

Public Class TuyaApiClient
    Private ReadOnly _cfg As TuyaConfig
    Private ReadOnly _tokenProvider As TuyaTokenProvider

    Public Sub New(cfg As TuyaConfig, tokenProvider As TuyaTokenProvider)
        _cfg = cfg
        _tokenProvider = tokenProvider
    End Sub

    Public Async Function GetDeviceInfoAsync(deviceId As String) As Task(Of DeviceInfo)
        Try
            Dim token = Await _tokenProvider.GetAccessTokenAsync()
            Dim url = $"{_cfg.OpenApiBase}/v1.0/devices/{deviceId}"

            Using client As New HttpClient()
                Dim t As Long = CLng((DateTime.UtcNow - New DateTime(1970, 1, 1)).TotalMilliseconds)
                Dim nonce As String = Guid.NewGuid().ToString("N")

                client.DefaultRequestHeaders.Add("client_id", _cfg.AccessId)
                client.DefaultRequestHeaders.Add("access_token", token)
                client.DefaultRequestHeaders.Add("t", t.ToString())
                client.DefaultRequestHeaders.Add("sign_method", "HMAC-SHA256")
                client.DefaultRequestHeaders.Add("nonce", nonce)

                Dim stringToSign = "GET" & vbLf & "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855" & vbLf & vbLf & $"/v1.0/devices/{deviceId}"
                Dim toSign = _cfg.AccessId & token & t.ToString() & nonce & stringToSign
                Dim sign = TuyaTokenProvider.HmacSha256Upper(toSign, _cfg.AccessSecret)
                client.DefaultRequestHeaders.Add("sign", sign)

                Dim resp = Await client.GetAsync(url)
                Dim respBody = Await resp.Content.ReadAsStringAsync()

                Dim json = JObject.Parse(respBody)

                If json("success")?.Value(Of Boolean)() = True Then
                    Dim result = json("result")
                    If result IsNot Nothing Then
                        Return New DeviceInfo With {
                            .Id = deviceId,
                            .Name = result("name")?.ToString(),
                            .ProductName = result("product_name")?.ToString(),
                            .Category = result("category")?.ToString(),
                            .Icon = result("icon")?.ToString(),
                            .IsOnline = If(result("online")?.Value(Of Boolean)(), False)
                        }
                    End If
                End If

            End Using

        Catch ex As Exception
            Console.WriteLine($"Erreur récupération info appareil {deviceId}: {ex.Message}")
        End Try

        Return Nothing
    End Function
End Class

Public Class DeviceInfo
    Public Property Id As String
    Public Property Name As String
    Public Property ProductName As String
    Public Property Category As String
    Public Property Icon As String
    Public Property IsOnline As Boolean
End Class