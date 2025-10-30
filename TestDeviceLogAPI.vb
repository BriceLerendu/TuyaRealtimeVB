Imports System
Imports System.Threading.Tasks

''' <summary>
''' Script de test simple pour vérifier si l'API Device Log Query fonctionne
''' Usage: Lancez l'application normalement, puis vérifiez les logs dans la console
''' </summary>
Module TestDeviceLogAPI

    ''' <summary>
    ''' Test de l'API Device Log Query sur un appareil
    ''' À appeler depuis MainForm après connexion
    ''' </summary>
    Public Async Function TestDeviceLogsAsync(
        apiClient As TuyaApiClient,
        historyService As TuyaHistoryService,
        deviceId As String
    ) As Task(Of TestResult)

        Console.WriteLine("═══════════════════════════════════════════")
        Console.WriteLine("🔬 TEST API DEVICE LOG QUERY")
        Console.WriteLine("═══════════════════════════════════════════")
        Console.WriteLine($"Device ID: {deviceId}")
        Console.WriteLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
        Console.WriteLine()

        Dim result As New TestResult With {
            .DeviceId = deviceId,
            .TestDate = DateTime.Now
        }

        Try
            ' Test 1: Récupérer les logs des dernières 24h
            Console.WriteLine("📊 Test 1: Récupération des logs (24 dernières heures)")
            Console.WriteLine("─────────────────────────────────────────")

            Dim logs = Await historyService.GetDeviceLogsAsync(deviceId, HistoryPeriod.Last24Hours)

            If logs Is Nothing Then
                Console.WriteLine("❌ ÉCHEC: logs = Nothing")
                result.LogsTestPassed = False
                result.ErrorMessage = "L'API a retourné Nothing (voir logs pour détails)"
            ElseIf logs.Count = 0 Then
                Console.WriteLine("⚠️  ATTENTION: 0 logs récupérés")
                Console.WriteLine("   Possibilités:")
                Console.WriteLine("   - L'appareil n'a pas eu d'activité dans les 24h")
                Console.WriteLine("   - L'API Device Log Query n'est pas activée")
                Console.WriteLine("   - L'appareil n'enregistre pas de logs")
                result.LogsTestPassed = False
                result.ErrorMessage = "0 logs récupérés (vérifier activité appareil)"
            Else
                Console.WriteLine($"✅ SUCCÈS: {logs.Count} logs récupérés")
                Console.WriteLine()
                Console.WriteLine("Détails des logs:")
                Console.WriteLine($"  - Premier log: {logs.First().EventTime:yyyy-MM-dd HH:mm:ss}")
                Console.WriteLine($"  - Dernier log: {logs.Last().EventTime:yyyy-MM-dd HH:mm:ss}")

                ' Afficher les codes DP uniques
                Dim uniqueCodes = logs.Where(Function(l) Not String.IsNullOrEmpty(l.Code)) _
                                     .Select(Function(l) l.Code) _
                                     .Distinct() _
                                     .ToList()
                Console.WriteLine($"  - Codes DP trouvés: {String.Join(", ", uniqueCodes)}")

                result.LogsTestPassed = True
                result.LogsCount = logs.Count
                result.UniqueCodes = uniqueCodes
            End If

            Console.WriteLine()

            ' Test 2: Détection automatique des codes DP disponibles
            Console.WriteLine("📊 Test 2: Détection des codes DP disponibles")
            Console.WriteLine("─────────────────────────────────────────")

            Dim availableCodes = Await historyService.GetAvailableCodesAsync(deviceId, HistoryPeriod.Last24Hours)

            If availableCodes Is Nothing OrElse availableCodes.Count = 0 Then
                Console.WriteLine("⚠️  Aucun code DP détecté")
                result.CodesDetectionPassed = False
            Else
                Console.WriteLine($"✅ {availableCodes.Count} codes DP détectés:")
                For Each code In availableCodes
                    Console.WriteLine($"   - {code}")
                Next
                result.CodesDetectionPassed = True
            End If

            Console.WriteLine()

            ' Test 3: Récupération des statistiques (calculées depuis les logs)
            Console.WriteLine("📊 Test 3: Calcul des statistiques")
            Console.WriteLine("─────────────────────────────────────────")

            Dim stats = Await historyService.GetDeviceStatisticsAsync(deviceId, HistoryPeriod.Last24Hours)

            If stats Is Nothing Then
                Console.WriteLine("⚠️  Aucune statistique disponible")
                Console.WriteLine("   Raison: Aucun code DP numérique trouvé dans les logs")
                result.StatsTestPassed = False
            Else
                Console.WriteLine($"✅ Statistiques calculées pour '{stats.Code}'")
                Console.WriteLine($"   - Type: {stats.VisualizationType}")
                Console.WriteLine($"   - Points de données: {stats.DataPoints.Count}")
                Console.WriteLine($"   - Unité: {If(String.IsNullOrEmpty(stats.Unit), "(aucune)", stats.Unit)}")
                result.StatsTestPassed = True
                result.StatsCode = stats.Code
                result.DataPointsCount = stats.DataPoints.Count
            End If

            Console.WriteLine()

        Catch ex As Exception
            Console.WriteLine($"❌ EXCEPTION: {ex.Message}")
            Console.WriteLine($"Stack trace: {ex.StackTrace}")
            result.LogsTestPassed = False
            result.ErrorMessage = ex.Message
        End Try

        ' Résumé final
        Console.WriteLine("═══════════════════════════════════════════")
        Console.WriteLine("📋 RÉSUMÉ DU TEST")
        Console.WriteLine("═══════════════════════════════════════════")
        Console.WriteLine($"✓ Logs API: {If(result.LogsTestPassed, "✅ OK", "❌ ÉCHEC")}")
        Console.WriteLine($"✓ Détection codes: {If(result.CodesDetectionPassed, "✅ OK", "⚠️  N/A")}")
        Console.WriteLine($"✓ Statistiques: {If(result.StatsTestPassed, "✅ OK", "⚠️  N/A")}")

        If result.LogsTestPassed Then
            Console.WriteLine()
            Console.WriteLine("🎉 L'API Device Log Query FONCTIONNE !")
            Console.WriteLine("   Vous pouvez utiliser la fonctionnalité d'historique.")
        Else
            Console.WriteLine()
            Console.WriteLine("⚠️  L'API Device Log Query ne fonctionne pas correctement.")
            Console.WriteLine()
            Console.WriteLine("💡 Actions recommandées:")
            Console.WriteLine("   1. Vérifiez que 'Device Log Query' est activé dans:")
            Console.WriteLine("      https://iot.tuya.com/ → Cloud → My Service")
            Console.WriteLine("   2. Vérifiez que l'appareil testé a eu de l'activité récente")
            Console.WriteLine("   3. Essayez avec un autre appareil")
            Console.WriteLine()
            If Not String.IsNullOrEmpty(result.ErrorMessage) Then
                Console.WriteLine($"Erreur: {result.ErrorMessage}")
            End If
        End If

        Console.WriteLine("═══════════════════════════════════════════")
        Console.WriteLine()

        Return result

    End Function

    ''' <summary>
    ''' Résultat du test
    ''' </summary>
    Public Class TestResult
        Public Property DeviceId As String
        Public Property TestDate As DateTime
        Public Property LogsTestPassed As Boolean
        Public Property CodesDetectionPassed As Boolean
        Public Property StatsTestPassed As Boolean
        Public Property LogsCount As Integer
        Public Property UniqueCodes As List(Of String)
        Public Property StatsCode As String
        Public Property DataPointsCount As Integer
        Public Property ErrorMessage As String

        Public ReadOnly Property AllTestsPassed As Boolean
            Get
                Return LogsTestPassed AndAlso CodesDetectionPassed
            End Get
        End Property
    End Class

End Module
