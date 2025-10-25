Imports System.Security.Cryptography
Imports System.Text
Imports Newtonsoft.Json.Linq

''' <summary>
''' Décrypteur de messages Tuya Pulsar
''' Les messages Tuya sont chiffrés en AES-128-ECB avec AccessSecret comme clé
''' </summary>
Public Class TuyaMessageDecryptor
    Private ReadOnly _accessId As String
    Private ReadOnly _accessSecret As String

    Public Sub New(accessId As String, accessSecret As String)
        _accessId = accessId
        _accessSecret = accessSecret
    End Sub

    ''' <summary>
    ''' Décrypte un message Pulsar Tuya
    ''' </summary>
    ''' <param name="encryptedData">Données chiffrées en Base64</param>
    ''' <returns>Données JSON décryptées</returns>
    Public Function DecryptMessage(encryptedData As String) As String
        Try
            ' Convertir Base64 en bytes
            Dim encryptedBytes = Convert.FromBase64String(encryptedData)

            ' Créer la clé AES à partir des 16 premiers caractères du secret
            Dim keyBytes = Encoding.UTF8.GetBytes(_accessSecret.Substring(8, 16))

            ' Décrypter avec AES-128-ECB
            Using aes As Aes = Aes.Create()
                aes.Key = keyBytes
                aes.Mode = CipherMode.ECB
                aes.Padding = PaddingMode.PKCS7

                Using decryptor = aes.CreateDecryptor()
                    Dim decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length)
                    Return Encoding.UTF8.GetString(decryptedBytes)
                End Using
            End Using

        Catch ex As Exception
            Throw New Exception($"Erreur décryptage message: {ex.Message}", ex)
        End Try
    End Function

    ''' <summary>
    ''' Vérifie la signature du message
    ''' </summary>
    Public Function VerifySignature(data As String, timestamp As Long, sign As String) As Boolean
        Try
            ' Signature = MD5(AccessId + timestamp + data + AccessSecret)
            Dim content = $"{_accessId}{timestamp}{data}{_accessSecret}"
            Dim md5Hash = ComputeMD5(content)
            Return md5Hash.Equals(sign, StringComparison.OrdinalIgnoreCase)
        Catch
            Return False
        End Try
    End Function

    Private Function ComputeMD5(input As String) As String
        Using md5 As MD5 = MD5.Create()
            Dim inputBytes = Encoding.UTF8.GetBytes(input)
            Dim hashBytes = md5.ComputeHash(inputBytes)
            Return BitConverter.ToString(hashBytes).Replace("-", "").ToLower()
        End Using
    End Function

    ''' <summary>
    ''' Parse et décrypte un message Pulsar complet
    ''' </summary>
    Public Function ParsePulsarMessage(payloadJson As JObject) As JObject
        Try
            ' Extraire les champs
            Dim encryptedData = payloadJson("data")?.ToString()
            Dim sign = payloadJson("sign")?.ToString()
            Dim timestamp = CLng(payloadJson("t"))
            Dim protocol = CInt(payloadJson("protocol"))

            If String.IsNullOrEmpty(encryptedData) Then
                Throw New Exception("Champ 'data' manquant")
            End If

            ' Vérifier la signature (optionnel mais recommandé)
            If Not String.IsNullOrEmpty(sign) Then
                If Not VerifySignature(encryptedData, timestamp, sign) Then
                    Throw New Exception("Signature invalide")
                End If
            End If

            ' Décrypter les données
            Dim decryptedJson = DecryptMessage(encryptedData)

            ' Parser le JSON décrypté
            Return JObject.Parse(decryptedJson)

        Catch ex As Exception
            Throw New Exception($"Erreur parsing message Pulsar: {ex.Message}", ex)
        End Try
    End Function
End Class
