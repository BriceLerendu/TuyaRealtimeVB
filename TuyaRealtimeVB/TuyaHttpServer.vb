Imports System.Net
Imports System.IO
Imports Newtonsoft.Json.Linq

Public Class TuyaHttpServer
    Implements IDisposable

#Region "Constantes"
    Private Const SERVER_URL As String = "http://localhost:5000/"
    Private Const EVENT_ENDPOINT As String = "/tuya-event"
    Private Const HTTP_METHOD_POST As String = "POST"
    Private Const RESPONSE_OK As String = "{""status"":""ok""}"
    Private Const HTTP_STATUS_NOT_FOUND As Integer = 404
#End Region

#Region "Champs privés"
    Private _listener As HttpListener
    Private _isRunning As Boolean = False
    Private _logCallback As Action(Of String, ConsoleColor)
    Private _disposed As Boolean = False
#End Region

#Region "Événements"
    Public Event EventReceived As Action(Of String)
#End Region

#Region "Initialisation"
    Public Sub New(Optional logCallback As Action(Of String, ConsoleColor) = Nothing)
        _logCallback = logCallback
    End Sub
#End Region

#Region "Démarrage et arrêt"
    Public Sub Start()
        Try
            If _isRunning Then
                Log("Le serveur est déjà démarré", ConsoleColor.Yellow)
                Return
            End If

            _listener = New HttpListener()
            _listener.Prefixes.Add(SERVER_URL)
            _listener.Start()
            _isRunning = True

            Log($"Serveur HTTP démarré sur {SERVER_URL}", ConsoleColor.Green)

            ' Démarrer l'écoute en arrière-plan sans bloquer
            Dim listenTask = ListenLoopAsync()
        Catch ex As Exception
            LogError("Erreur démarrage serveur HTTP", ex)
            Throw
        End Try
    End Sub

    Public Sub [Stop]()
        If Not _isRunning Then Return

        _isRunning = False

        Try
            _listener?.Stop()
            _listener?.Close()
            Log("Serveur HTTP arrêté", ConsoleColor.Gray)
        Catch ex As Exception
            LogError("Erreur arrêt serveur", ex)
        End Try
    End Sub
#End Region

#Region "Boucle d'écoute"
    Private Async Function ListenLoopAsync() As Task
        While _isRunning AndAlso _listener IsNot Nothing AndAlso _listener.IsListening
            Try
                Dim context = Await _listener.GetContextAsync()

                ' Traiter la requête de manière asynchrone sans bloquer la boucle
                ' OPTIMISÉ : Capturer les exceptions des tasks fire-and-forget
                Dim handleTask = Task.Run(
                    Async Function()
                        Try
                            Await Task.Run(Sub() HandleRequest(context))
                        Catch ex As Exception
                            LogError("Erreur HandleRequest non gérée", ex)
                        End Try
                    End Function)
            Catch ex As HttpListenerException
                ' Erreur normale lors de l'arrêt du serveur
                If _isRunning Then
                    LogError("Erreur HttpListener", ex)
                End If
                Exit While
            Catch ex As ObjectDisposedException
                ' Le listener a été disposé
                Exit While
            Catch ex As Exception
                If _isRunning Then
                    LogError("Erreur boucle d'écoute", ex)
                End If
            End Try
        End While

        Log("Boucle d'écoute terminée", ConsoleColor.Gray)
    End Function
#End Region

#Region "Traitement des requêtes"
    Private Sub HandleRequest(context As HttpListenerContext)
        Try
            Dim request = context.Request
            Dim response = context.Response

            If IsValidEventRequest(request) Then
                ProcessEventRequest(request, response)
            Else
                SendNotFoundResponse(response)
            End If

            response.Close()
        Catch ex As Exception
            LogError("Erreur traitement requête", ex)
            Try
                context.Response?.Close()
            Catch
                ' Ignorer les erreurs de fermeture
            End Try
        End Try
    End Sub

    Private Function IsValidEventRequest(request As HttpListenerRequest) As Boolean
        Return request.HttpMethod = HTTP_METHOD_POST AndAlso
               request.Url.AbsolutePath = EVENT_ENDPOINT
    End Function

    Private Sub ProcessEventRequest(request As HttpListenerRequest, response As HttpListenerResponse)
        Try
            Dim eventData = ExtractEventData(request)

            If Not String.IsNullOrEmpty(eventData) Then
                Log("Événement Tuya reçu du script Python", ConsoleColor.Cyan)
                RaiseEvent EventReceived(eventData)
                SendSuccessResponse(response)
            Else
                Log("Événement reçu mais vide", ConsoleColor.Yellow)
                SendSuccessResponse(response)
            End If
        Catch ex As Exception
            LogError("Erreur traitement événement", ex)
            SendErrorResponse(response, ex.Message)
        End Try
    End Sub

    Private Function ExtractEventData(request As HttpListenerRequest) As String
        Using reader As New StreamReader(request.InputStream, request.ContentEncoding)
            Dim body = reader.ReadToEnd()

            If String.IsNullOrWhiteSpace(body) Then
                Log("Body de la requête vide", ConsoleColor.Yellow)
                Return Nothing
            End If

            Try
                Dim json = JObject.Parse(body)

                ' OPTIMISÉ : Validation du schéma JSON
                If Not ValidateEventPayload(json) Then
                    Log("Payload JSON invalide - schéma non conforme", ConsoleColor.Yellow)
                    Log($"Body reçu: {body}", ConsoleColor.DarkYellow)
                    Return Nothing
                End If

                Return json("event")?.ToString()
            Catch ex As Exception
                LogError("Erreur parsing JSON", ex)
                Log($"Body reçu: {body}", ConsoleColor.Yellow)
                Return Nothing
            End Try
        End Using
    End Function

    ''' <summary>
    ''' Valide le schéma du payload d'événement Tuya
    ''' </summary>
    Private Function ValidateEventPayload(json As JObject) As Boolean
        ' Vérifier que le champ "event" existe
        If json("event") Is Nothing Then
            Return False
        End If

        Dim eventObj = TryCast(json("event"), JObject)
        If eventObj Is Nothing Then
            ' Si "event" n'est pas un objet, accepter quand même (peut être une string)
            Return True
        End If

        ' Vérifier que devId existe dans l'événement
        If eventObj("devId") Is Nothing Then
            Log("⚠ Événement sans devId", ConsoleColor.Yellow)
            Return False
        End If

        Return True
    End Function
#End Region

#Region "Réponses HTTP"
    Private Sub SendSuccessResponse(response As HttpListenerResponse)
        SendJsonResponse(response, RESPONSE_OK, 200)
    End Sub

    Private Sub SendNotFoundResponse(response As HttpListenerResponse)
        response.StatusCode = HTTP_STATUS_NOT_FOUND
        SendJsonResponse(response, "{""error"":""Not Found""}", HTTP_STATUS_NOT_FOUND)
    End Sub

    Private Sub SendErrorResponse(response As HttpListenerResponse, errorMessage As String)
        Dim errorJson = $"{{""error"":""{EscapeJson(errorMessage)}""}}"
        SendJsonResponse(response, errorJson, 500)
    End Sub

    Private Sub SendJsonResponse(response As HttpListenerResponse, json As String, statusCode As Integer)
        Try
            response.StatusCode = statusCode
            response.ContentType = "application/json"

            Dim buffer = System.Text.Encoding.UTF8.GetBytes(json)
            response.ContentLength64 = buffer.Length
            response.OutputStream.Write(buffer, 0, buffer.Length)
        Catch ex As Exception
            LogError("Erreur envoi réponse", ex)
        End Try
    End Sub

    Private Function EscapeJson(text As String) As String
        If String.IsNullOrEmpty(text) Then Return text

        Return text.Replace("\", "\\") _
                   .Replace("""", "\""") _
                   .Replace(vbCr, "\r") _
                   .Replace(vbLf, "\n") _
                   .Replace(vbTab, "\t")
    End Function
#End Region

#Region "Logging"
    Private Sub Log(message As String, Optional color As ConsoleColor = ConsoleColor.White)
        If _logCallback IsNot Nothing Then
            _logCallback(message, color)
        Else
            Dim originalColor = Console.ForegroundColor
            Console.ForegroundColor = color
            Console.WriteLine(message)
            Console.ForegroundColor = originalColor
        End If
    End Sub

    Private Sub LogError(context As String, ex As Exception)
        Log($"✗ {context}: {ex.Message}", ConsoleColor.Red)

        If ex.InnerException IsNot Nothing Then
            Log($"  Inner: {ex.InnerException.Message}", ConsoleColor.DarkRed)
        End If
    End Sub
#End Region

#Region "IDisposable"
    Protected Overridable Sub Dispose(disposing As Boolean)
        If Not _disposed Then
            If disposing Then
                ' Libérer les ressources managées
                [Stop]()
                _listener = Nothing
            End If
            _disposed = True
        End If
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Protected Overrides Sub Finalize()
        Dispose(False)
        MyBase.Finalize()
    End Sub
#End Region

End Class