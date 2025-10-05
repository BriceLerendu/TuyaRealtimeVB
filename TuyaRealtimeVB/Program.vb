Imports System
Imports System.Threading.Tasks

Module Program
    <STAThread>
    Sub Main()
        RunAsync().GetAwaiter().GetResult()
    End Sub

    Private Async Function RunAsync() As Task
        Console.OutputEncoding = System.Text.Encoding.UTF8
        Console.WriteLine("TuyaRealtimeVB - MQTT EU (VB.NET)")
        Console.WriteLine("Chargement de la configuration...")

        Dim cfg = TuyaConfig.Load()
        If String.IsNullOrWhiteSpace(cfg.AccessId) OrElse String.IsNullOrWhiteSpace(cfg.AccessSecret) Then
            Console.WriteLine("❌ Veuillez renseigner AccessId et AccessSecret dans appsettings.json")
            Return
        End If

        Dim tokenProvider As New TuyaTokenProvider(cfg)
        Dim mqtt As New TuyaMqttClient(cfg, tokenProvider)

        Try
            Console.WriteLine("🔹 Tentative d’obtention du token...")
            Dim token = Await tokenProvider.GetAccessTokenAsync()
            Console.WriteLine("✅ Token obtenu : " & token.Substring(0, 12) & "...")
            Console.WriteLine("🔹 Connexion MQTT...")
            Await mqtt.ConnectAndSubscribeAsync()
            Console.WriteLine("✅ En écoute des événements. Appuyez sur Entrée pour quitter.")
            Console.ReadLine()
        Catch ex As Exception
            Console.WriteLine("❌ Erreur: " & ex.Message)
        End Try
    End Function

End Module