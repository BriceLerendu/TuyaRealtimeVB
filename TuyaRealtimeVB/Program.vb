Imports System
Imports System.Threading.Tasks
Imports Newtonsoft.Json.Linq

Module Program
    <STAThread>
    Sub Main()
        RunAsync().GetAwaiter().GetResult()
    End Sub

    Private Async Function RunAsync() As Task
        Console.OutputEncoding = System.Text.Encoding.UTF8
        Console.WriteLine("TuyaRealtimeVB - Python Bridge (VB.NET)")
        Console.WriteLine("Chargement de la configuration...")

        Dim httpServer As TuyaHttpServer = Nothing
        Dim pythonBridge As PythonBridge = Nothing

        Try
            ' 1. Démarrer le serveur HTTP pour recevoir les événements
            httpServer = New TuyaHttpServer()
            AddHandler httpServer.EventReceived, AddressOf OnTuyaEventReceived
            httpServer.Start()

            ' 2. Démarrer le script Python
            ' IMPORTANT: Modifiez ce chemin vers votre fichier main.py
            Dim pythonScriptPath = "C:\Users\leren\Downloads\tuya_bridge.py"

            Console.WriteLine($"Démarrage du script Python : {pythonScriptPath}")
            pythonBridge = New PythonBridge(pythonScriptPath)
            pythonBridge.Start()

            Console.WriteLine("")
            Console.WriteLine("✅ Système démarré !")
            Console.WriteLine("   - Serveur HTTP : http://localhost:5000/")
            Console.WriteLine("   - Script Python : En écoute Pulsar")
            Console.WriteLine("")
            Console.WriteLine("Appuyez sur Entrée pour quitter...")
            Console.ReadLine()

        Catch ex As Exception
            Console.WriteLine($"❌ Erreur: {ex.Message}")
            Console.WriteLine(ex.StackTrace)
        Finally
            ' Arrêter proprement
            pythonBridge?.Stop()
            httpServer?.Stop()
            Console.WriteLine("Système arrêté")
        End Try
    End Function

    Private Sub OnTuyaEventReceived(eventData As String)
        Try
            Console.WriteLine("📨 ======================================")
            Console.WriteLine("📨 ÉVÉNEMENT TUYA REÇU")
            Console.WriteLine("📨 ======================================")

            Dim json = JObject.Parse(eventData)

            Dim devId = json.SelectToken("devId")?.ToString()
            Dim status = json.SelectToken("status")
            Dim bizCode = json.SelectToken("bizCode")?.ToString()

            If devId IsNot Nothing Then
                Console.WriteLine($"📱 Appareil : {devId}")

                If Not String.IsNullOrEmpty(bizCode) Then
                    Console.WriteLine($"🔔 Type : {bizCode}")
                End If

                If status IsNot Nothing Then
                    Console.WriteLine("📊 Changements d'état :")
                    For Each item In status
                        Dim code = item.SelectToken("code")?.ToString()
                        Dim value = item.SelectToken("value")?.ToString()
                        Console.WriteLine($"   • {code} = {value}")
                    Next
                End If
            End If

            ' Message complet pour debug
            Console.WriteLine("")
            Console.WriteLine("Message complet :")
            Console.WriteLine(json.ToString(Newtonsoft.Json.Formatting.Indented))
            Console.WriteLine("========================================")
            Console.WriteLine("")

            ' ICI: Ajoutez votre logique métier
            ' - Mettre à jour une interface
            ' - Enregistrer dans une base de données
            ' - Déclencher des actions
            ' etc.

        Catch ex As Exception
            Console.WriteLine($"❌ Erreur traitement événement : {ex.Message}")
        End Try
    End Sub

End Module