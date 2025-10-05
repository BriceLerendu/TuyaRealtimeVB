Imports System.Net
Imports System.IO
Imports Newtonsoft.Json.Linq

Public Class TuyaHttpServer
    Private _listener As HttpListener
    Private _isRunning As Boolean = False

    Public Event EventReceived As Action(Of String)

    Public Sub Start()
        Try
            _listener = New HttpListener()
            _listener.Prefixes.Add("http://localhost:5000/")
            _listener.Start()
            _isRunning = True

            Console.WriteLine("Serveur HTTP démarré sur http://localhost:5000/")

            ' Démarrer l'écoute en arrière-plan
            Task.Run(AddressOf ListenLoop)

        Catch ex As Exception
            Console.WriteLine($"Erreur démarrage serveur HTTP : {ex.Message}")
        End Try
    End Sub

    Private Async Function ListenLoop() As Task
        While _isRunning
            Try
                Dim context = Await _listener.GetContextAsync()
                Task.Run(Sub() HandleRequest(context))
            Catch ex As Exception
                If _isRunning Then
                    Console.WriteLine($"Erreur serveur : {ex.Message}")
                End If
            End Try
        End While
    End Function

    Private Sub HandleRequest(context As HttpListenerContext)
        Try
            Dim request = context.Request
            Dim response = context.Response

            If request.HttpMethod = "POST" AndAlso request.Url.AbsolutePath = "/tuya-event" Then
                ' Lire le body JSON
                Using reader As New StreamReader(request.InputStream, request.ContentEncoding)
                    Dim body = reader.ReadToEnd()
                    Dim json = JObject.Parse(body)
                    Dim eventData = json("event")?.ToString()

                    If Not String.IsNullOrEmpty(eventData) Then
                        Console.WriteLine("Événement Tuya reçu du script Python")
                        RaiseEvent EventReceived(eventData)
                    End If
                End Using

                ' Répondre OK
                Dim responseString = "{""status"":""ok""}"
                Dim buffer = System.Text.Encoding.UTF8.GetBytes(responseString)
                response.ContentLength64 = buffer.Length
                response.OutputStream.Write(buffer, 0, buffer.Length)
            Else
                response.StatusCode = 404
            End If

            response.Close()

        Catch ex As Exception
            Console.WriteLine($"Erreur traitement requête : {ex.Message}")
        End Try
    End Sub

    Public Sub [Stop]()
        _isRunning = False
        _listener?.Stop()
        _listener?.Close()
        Console.WriteLine("Serveur HTTP arrêté")
    End Sub
End Class