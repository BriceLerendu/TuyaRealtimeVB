Imports System.IO
Imports System.Media
Imports System.Linq
Imports Newtonsoft.Json

Public Enum NotificationType
    Info
    Warning
    Critical
End Enum

Public Enum ComparisonOperator
    GreaterThan      ' >
    GreaterOrEqual   ' >=
    LessThan         ' <
    LessOrEqual      ' <=
    Equal            ' =
End Enum

Public Class NotificationRule
    Public Property Name As String
    Public Property IsEnabled As Boolean = True
    Public Property DeviceCategory As String
    Public Property PropertyCode As String
    Public Property TriggerValue As String
    Public Property ComparisonOperator As ComparisonOperator = ComparisonOperator.GreaterOrEqual
    Public Property NotificationType As NotificationType
    Public Property Message As String
    Public Property PlaySound As Boolean
    Public Property CooldownMinutes As Integer
    <JsonIgnore>
    Public Property LastTriggered As DateTime = DateTime.MinValue
End Class

Public Class NotificationEntry
    Public Property Timestamp As DateTime
    Public Property DeviceName As String
    Public Property RoomName As String = ""
    Public Property Message As String
    Public Property Type As NotificationType
    Public Property NotificationType As NotificationType
    Public Property IsRead As Boolean

    Public Function GetTimeAgo() As String
        Dim span As TimeSpan = DateTime.Now - Timestamp

        If span.TotalMinutes < 1 Then
            Return "À l'instant"
        ElseIf span.TotalMinutes < 60 Then
            Return $"Il y a {CInt(span.TotalMinutes)} min"
        ElseIf span.TotalHours < 24 Then
            Return $"Il y a {CInt(span.TotalHours)}h"
        Else
            Return $"Il y a {CInt(span.TotalDays)}j"
        End If
    End Function
End Class

Public Class NotificationManager
    Private Shared _instance As NotificationManager
    Private _rules As List(Of NotificationRule)
    Private _notifications As List(Of NotificationEntry)
    Private _soundPlayer As SoundPlayer
    Private ReadOnly _configFilePath As String

    Public Event NotificationAdded(sender As Object, entry As NotificationEntry)

    Public Shared ReadOnly Property Instance As NotificationManager
        Get
            If _instance Is Nothing Then
                _instance = New NotificationManager()
            End If
            Return _instance
        End Get
    End Property

    Private Sub New()
        _rules = New List(Of NotificationRule)
        _notifications = New List(Of NotificationEntry)
        _soundPlayer = New SoundPlayer()

        ' ✅ Essayer d'abord le répertoire de l'exécutable
        Try
            Dim appPath As String = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
            _configFilePath = Path.Combine(appPath, "notification_rules.json")
            Console.WriteLine($"📂 Tentative chemin 1: {_configFilePath}")

            ' Tester si on peut écrire dans ce répertoire
            Dim testFile = Path.Combine(appPath, "test_write.tmp")
            File.WriteAllText(testFile, "test")
            File.Delete(testFile)
            Console.WriteLine("✅ Répertoire accessible en écriture")
        Catch ex As Exception
            ' Si échec, utiliser AppData
            Console.WriteLine($"⚠️ Répertoire non accessible: {ex.Message}")
            Dim appDataPath As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            Dim appFolder As String = Path.Combine(appDataPath, "TuyaRealtimeVB")

            If Not Directory.Exists(appFolder) Then
                Directory.CreateDirectory(appFolder)
                Console.WriteLine($"📁 Dossier créé: {appFolder}")
            End If

            _configFilePath = Path.Combine(appFolder, "notification_rules.json")
            Console.WriteLine($"📂 Utilisation chemin alternatif: {_configFilePath}")
        End Try

        LoadRulesFromFile()

        ' Si aucune règle chargée, initialiser les règles par défaut
        If _rules.Count = 0 Then
            Console.WriteLine("📝 Aucune règle trouvée, initialisation des règles par défaut...")
            InitializeDefaultRules()
            SaveRulesToFile()
        Else
            Console.WriteLine($"✅ {_rules.Count} règles chargées depuis le fichier")
        End If
    End Sub

    Private Sub InitializeDefaultRules()
        ' 🚨 ALARMES CRITIQUES
        _rules.Add(New NotificationRule With {
            .Name = "🔥 Alarme Fumée",
            .DeviceCategory = "ywbj",
            .PropertyCode = "smoke_sensor_status",
            .TriggerValue = "alarm",
            .ComparisonOperator = ComparisonOperator.Equal,
            .NotificationType = NotificationType.Critical,
            .Message = "🚨 FUMÉE DÉTECTÉE !",
            .PlaySound = True,
            .CooldownMinutes = 2
        })

        _rules.Add(New NotificationRule With {
            .Name = "⚠️ Alarme Gaz",
            .DeviceCategory = "rqbj",
            .PropertyCode = "gas_sensor_status",
            .TriggerValue = "alarm",
            .ComparisonOperator = ComparisonOperator.Equal,
            .NotificationType = NotificationType.Critical,
            .Message = "🚨 GAZ DÉTECTÉ !",
            .PlaySound = True,
            .CooldownMinutes = 2
        })

        _rules.Add(New NotificationRule With {
            .Name = "💧 Fuite d'eau",
            .DeviceCategory = "sj",
            .PropertyCode = "watersensor_state",
            .TriggerValue = "alarm",
            .ComparisonOperator = ComparisonOperator.Equal,
            .NotificationType = NotificationType.Critical,
            .Message = "💧 FUITE D'EAU DÉTECTÉE !",
            .PlaySound = True,
            .CooldownMinutes = 5
        })

        _rules.Add(New NotificationRule With {
            .Name = "🚪 Alarme Intrusion",
            .DeviceCategory = "mal",
            .PropertyCode = "alarm_state",
            .TriggerValue = "alarm",
            .ComparisonOperator = ComparisonOperator.Equal,
            .NotificationType = NotificationType.Critical,
            .Message = "🚨 INTRUSION DÉTECTÉE !",
            .PlaySound = True,
            .CooldownMinutes = 1
        })

        ' ⚡ ALERTES ÉLECTRIQUES
        _rules.Add(New NotificationRule With {
            .Name = "⚡ Consommation très élevée",
            .DeviceCategory = "zndb",
            .PropertyCode = "cur_power",
            .TriggerValue = "50000",
            .ComparisonOperator = ComparisonOperator.GreaterOrEqual,
            .NotificationType = NotificationType.Critical,
            .Message = "⚡ CONSOMMATION CRITIQUE > 50kW !",
            .PlaySound = True,
            .CooldownMinutes = 10
        })

        _rules.Add(New NotificationRule With {
            .Name = "⚡ Consommation élevée",
            .DeviceCategory = "zndb",
            .PropertyCode = "cur_power",
            .TriggerValue = "30000",
            .ComparisonOperator = ComparisonOperator.GreaterOrEqual,
            .NotificationType = NotificationType.Warning,
            .Message = "⚠️ Consommation élevée > 30kW",
            .PlaySound = True,
            .CooldownMinutes = 15
        })

        _rules.Add(New NotificationRule With {
            .Name = "⚡ Surtension détectée",
            .DeviceCategory = "",
            .PropertyCode = "cur_voltage",
            .TriggerValue = "250",
            .ComparisonOperator = ComparisonOperator.GreaterOrEqual,
            .NotificationType = NotificationType.Warning,
            .Message = "⚠️ Surtension > 250V",
            .PlaySound = True,
            .CooldownMinutes = 30
        })

        _rules.Add(New NotificationRule With {
            .Name = "⚡ Sous-tension détectée",
            .DeviceCategory = "",
            .PropertyCode = "cur_voltage",
            .TriggerValue = "200",
            .ComparisonOperator = ComparisonOperator.LessOrEqual,
            .NotificationType = NotificationType.Warning,
            .Message = "⚠️ Sous-tension < 200V",
            .PlaySound = True,
            .CooldownMinutes = 30
        })

        ' 🌡️ TEMPÉRATURE & CLIMAT
        _rules.Add(New NotificationRule With {
            .Name = "🌡️ Température très élevée",
            .DeviceCategory = "",
            .PropertyCode = "temp_current",
            .TriggerValue = "35",
            .ComparisonOperator = ComparisonOperator.GreaterOrEqual,
            .NotificationType = NotificationType.Warning,
            .Message = "🔥 Température > 35°C",
            .PlaySound = False,
            .CooldownMinutes = 60
        })

        _rules.Add(New NotificationRule With {
            .Name = "❄️ Température très basse",
            .DeviceCategory = "",
            .PropertyCode = "temp_current",
            .TriggerValue = "5",
            .ComparisonOperator = ComparisonOperator.LessOrEqual,
            .NotificationType = NotificationType.Warning,
            .Message = "❄️ Température < 5°C",
            .PlaySound = False,
            .CooldownMinutes = 60
        })

        _rules.Add(New NotificationRule With {
            .Name = "💧 Humidité élevée",
            .DeviceCategory = "",
            .PropertyCode = "humidity_value",
            .TriggerValue = "80",
            .ComparisonOperator = ComparisonOperator.GreaterOrEqual,
            .NotificationType = NotificationType.Info,
            .Message = "💧 Humidité > 80%",
            .PlaySound = False,
            .CooldownMinutes = 120
        })

        _rules.Add(New NotificationRule With {
            .Name = "🏜️ Humidité faible",
            .DeviceCategory = "",
            .PropertyCode = "humidity_value",
            .TriggerValue = "30",
            .ComparisonOperator = ComparisonOperator.LessOrEqual,
            .NotificationType = NotificationType.Info,
            .Message = "🏜️ Humidité < 30%",
            .PlaySound = False,
            .CooldownMinutes = 120
        })

        ' 🚪 OUVERTURES
        _rules.Add(New NotificationRule With {
            .Name = "🚪 Porte ouverte",
            .DeviceCategory = "mcs",
            .PropertyCode = "doorcontact_state",
            .TriggerValue = "true",
            .ComparisonOperator = ComparisonOperator.Equal,
            .NotificationType = NotificationType.Info,
            .Message = "🚪 Porte ouverte",
            .PlaySound = True,
            .CooldownMinutes = 5
        })

        _rules.Add(New NotificationRule With {
            .Name = "🪟 Fenêtre ouverte",
            .DeviceCategory = "mc",
            .PropertyCode = "doorcontact_state",
            .TriggerValue = "true",
            .ComparisonOperator = ComparisonOperator.Equal,
            .NotificationType = NotificationType.Info,
            .Message = "🪟 Fenêtre ouverte",
            .PlaySound = False,
            .CooldownMinutes = 10
        })

        ' 👁️ DÉTECTION MOUVEMENT
        _rules.Add(New NotificationRule With {
            .Name = "👁️ Mouvement détecté",
            .DeviceCategory = "pir",
            .PropertyCode = "pir",
            .TriggerValue = "pir",
            .ComparisonOperator = ComparisonOperator.Equal,
            .NotificationType = NotificationType.Info,
            .Message = "👁️ Mouvement détecté",
            .PlaySound = False,
            .CooldownMinutes = 1
        })

        _rules.Add(New NotificationRule With {
            .Name = "📹 Personne détectée",
            .DeviceCategory = "sp",
            .PropertyCode = "ipc_work_mode",
            .TriggerValue = "motion",
            .ComparisonOperator = ComparisonOperator.Equal,
            .NotificationType = NotificationType.Info,
            .Message = "📹 Personne détectée par caméra",
            .PlaySound = True,
            .CooldownMinutes = 5
        })

        ' 🔋 BATTERIE
        _rules.Add(New NotificationRule With {
            .Name = "🔋 Batterie critique",
            .DeviceCategory = "",
            .PropertyCode = "battery_percentage",
            .TriggerValue = "10",
            .ComparisonOperator = ComparisonOperator.LessOrEqual,
            .NotificationType = NotificationType.Warning,
            .Message = "🔋 Batterie critique < 10%",
            .PlaySound = False,
            .CooldownMinutes = 1440
        })

        _rules.Add(New NotificationRule With {
            .Name = "🔋 Batterie faible",
            .DeviceCategory = "",
            .PropertyCode = "battery_percentage",
            .TriggerValue = "20",
            .ComparisonOperator = ComparisonOperator.LessOrEqual,
            .NotificationType = NotificationType.Info,
            .Message = "🔋 Batterie faible < 20%",
            .PlaySound = False,
            .CooldownMinutes = 720
        })

        ' 💡 ÉTAT APPAREILS
        _rules.Add(New NotificationRule With {
            .Name = "📴 Appareil hors ligne",
            .DeviceCategory = "",
            .PropertyCode = "online",
            .TriggerValue = "false",
            .ComparisonOperator = ComparisonOperator.Equal,
            .NotificationType = NotificationType.Warning,
            .Message = "📴 Appareil hors ligne",
            .PlaySound = False,
            .CooldownMinutes = 30
        })
    End Sub

    ' 💾 SAUVEGARDE
    Private Sub SaveRulesToFile()
        Try
            Console.WriteLine($"💾 Tentative de sauvegarde dans: {_configFilePath}")
            Console.WriteLine($"   Nombre de règles à sauvegarder: {_rules.Count}")

            Dim json As String = JsonConvert.SerializeObject(_rules, Formatting.Indented)
            Console.WriteLine($"   JSON généré: {json.Length} caractères")

            ' S'assurer que le répertoire existe
            Dim folderPath As String = Path.GetDirectoryName(_configFilePath)
            If Not Directory.Exists(folderPath) Then
                Directory.CreateDirectory(folderPath)
                Console.WriteLine($"   Dossier créé: {folderPath}")
            End If

            File.WriteAllText(_configFilePath, json)

            ' Vérifier que le fichier a bien été créé
            If File.Exists(_configFilePath) Then
                Dim fileInfo As New FileInfo(_configFilePath)
                Console.WriteLine($"✅ Fichier sauvegardé avec succès!")
                Console.WriteLine($"   Taille: {fileInfo.Length} octets")
                Console.WriteLine($"   Dernière modification: {fileInfo.LastWriteTime}")
            Else
                Console.WriteLine($"❌ ERREUR: Le fichier n'existe pas après l'écriture!")
            End If
        Catch ex As Exception
            Console.WriteLine($"❌ ERREUR sauvegarde règles: {ex.Message}")
            Console.WriteLine($"   Type: {ex.GetType().Name}")
            Console.WriteLine($"   StackTrace: {ex.StackTrace}")
        End Try
    End Sub

    ' 📂 CHARGEMENT
    Private Sub LoadRulesFromFile()
        Try
            Console.WriteLine($"📂 Tentative de chargement: {_configFilePath}")

            If File.Exists(_configFilePath) Then
                Dim fileInfo As New FileInfo(_configFilePath)
                Console.WriteLine($"   Fichier trouvé - Taille: {fileInfo.Length} octets")
                Console.WriteLine($"   Dernière modification: {fileInfo.LastWriteTime}")

                Dim json As String = File.ReadAllText(_configFilePath)
                Console.WriteLine($"   JSON lu: {json.Length} caractères")

                _rules = JsonConvert.DeserializeObject(Of List(Of NotificationRule))(json)

                If _rules Is Nothing Then
                    Console.WriteLine("⚠️ Désérialisation a retourné Nothing")
                    _rules = New List(Of NotificationRule)
                Else
                    Console.WriteLine($"✅ {_rules.Count} règles désérialisées")
                    ' 🧪 DEBUG: Afficher les opérateurs chargés
                    For Each rule In _rules
                        Console.WriteLine($"  📋 {rule.Name}: {rule.PropertyCode} {GetOperatorSymbol(rule.ComparisonOperator)} {rule.TriggerValue}")
                    Next
                End If
            Else
                Console.WriteLine($"📂 Fichier non trouvé: {_configFilePath}")
            End If
        Catch ex As Exception
            Console.WriteLine($"❌ ERREUR chargement règles: {ex.Message}")
            Console.WriteLine($"   Type: {ex.GetType().Name}")
            _rules = New List(Of NotificationRule)
        End Try
    End Sub

    ' 🔄 RÉINITIALISATION
    Public Sub ResetToDefaults()
        _rules.Clear()
        InitializeDefaultRules()
        SaveRulesToFile()
        Console.WriteLine("🔄 Règles réinitialisées aux valeurs par défaut")
    End Sub

    ' 📤 EXPORT / IMPORT
    Public Function ExportRules() As String
        Return JsonConvert.SerializeObject(_rules, Formatting.Indented)
    End Function

    Public Sub ImportRules(jsonContent As String)
        Try
            Dim importedRules = JsonConvert.DeserializeObject(Of List(Of NotificationRule))(jsonContent)
            If importedRules IsNot Nothing Then
                _rules = importedRules
                SaveRulesToFile()
                Console.WriteLine($"📥 {_rules.Count} règles importées avec succès")
            End If
        Catch ex As Exception
            Console.WriteLine($"❌ Erreur import: {ex.Message}")
            Throw New Exception($"Erreur import: {ex.Message}")
        End Try
    End Sub

    Public Function GetRules() As List(Of NotificationRule)
        Return _rules
    End Function

    Public Function GetConfigFilePath() As String
        Return _configFilePath
    End Function

    ' 🧪 TEST: Forcer une sauvegarde immédiate
    Public Sub ForceSave()
        Console.WriteLine("🧪 FORCE SAVE appelé")
        SaveRulesToFile()
    End Sub

    Public Sub AddRule(rule As NotificationRule)
        Console.WriteLine($"➕ AddRule appelé: {rule.Name}")
        _rules.Add(rule)
        Console.WriteLine($"   Total de règles: {_rules.Count}")
        SaveRulesToFile()
    End Sub

    Public Sub UpdateRule(index As Integer, rule As NotificationRule)
        Console.WriteLine($"✏️ UpdateRule appelé: index={index}, nom={rule.Name}")
        If index >= 0 AndAlso index < _rules.Count Then
            _rules(index) = rule
            Console.WriteLine($"   Règle mise à jour avec succès")
            SaveRulesToFile()
        Else
            Console.WriteLine($"❌ Index invalide: {index} (total: {_rules.Count})")
        End If
    End Sub

    Public Sub DeleteRule(index As Integer)
        Console.WriteLine($"🗑️ DeleteRule appelé: index={index}")
        If index >= 0 AndAlso index < _rules.Count Then
            Dim ruleName = _rules(index).Name
            _rules.RemoveAt(index)
            Console.WriteLine($"   Règle supprimée: {ruleName}")
            Console.WriteLine($"   Total de règles: {_rules.Count}")
            SaveRulesToFile()
        Else
            Console.WriteLine($"❌ Index invalide: {index} (total: {_rules.Count})")
        End If
    End Sub

    Public Sub ToggleRule(index As Integer)
        Console.WriteLine($"🔘 ToggleRule appelé: index={index}")
        If index >= 0 AndAlso index < _rules.Count Then
            _rules(index).IsEnabled = Not _rules(index).IsEnabled
            Dim status = If(_rules(index).IsEnabled, "activée", "désactivée")
            Console.WriteLine($"   Règle {status}: {_rules(index).Name}")
            SaveRulesToFile()
        Else
            Console.WriteLine($"❌ Index invalide: {index} (total: {_rules.Count})")
        End If
    End Sub

    Public Sub CheckDevice(device As Object)
        ' Support pour les objets TuyaDevice ou tout objet avec Name, Category et Status
        Dim deviceName As String = ""
        Dim deviceCategory As String = ""
        Dim deviceStatus As Dictionary(Of String, Object) = Nothing

        Try
            ' Récupération dynamique des propriétés
            Dim deviceType = device.GetType()

            Dim nameProp = deviceType.GetProperty("Name")
            If nameProp IsNot Nothing Then
                deviceName = nameProp.GetValue(device)?.ToString()
            End If

            Dim categoryProp = deviceType.GetProperty("Category")
            If categoryProp IsNot Nothing Then
                deviceCategory = categoryProp.GetValue(device)?.ToString()
            End If

            Dim statusProp = deviceType.GetProperty("Status")
            If statusProp IsNot Nothing Then
                deviceStatus = TryCast(statusProp.GetValue(device), Dictionary(Of String, Object))
            End If
        Catch ex As Exception
            Console.WriteLine($"Erreur lecture appareil: {ex.Message}")
            Return
        End Try

        If String.IsNullOrEmpty(deviceName) OrElse deviceStatus Is Nothing Then
            Return
        End If

        For Each rule In _rules
            If Not rule.IsEnabled Then Continue For

            ' Vérifier si le cooldown est actif
            If (DateTime.Now - rule.LastTriggered).TotalMinutes < rule.CooldownMinutes Then
                Continue For
            End If

            ' ✅ PLUS DE FILTRE SUR DeviceCategory - seul le PropertyCode compte
            ' Plusieurs catégories peuvent avoir le même PropertyCode

            ' Vérifier propriété
            If deviceStatus.ContainsKey(rule.PropertyCode) Then
                Dim value As Object = deviceStatus(rule.PropertyCode)
                Dim triggered As Boolean = False

                If IsNumeric(rule.TriggerValue) AndAlso IsNumeric(value) Then
                    ' Comparaison numérique avec opérateur
                    Dim threshold As Double = CDbl(rule.TriggerValue)
                    Dim currentValue As Double = CDbl(value)

                    Console.WriteLine($"  🔍 Test règle '{rule.Name}': {currentValue} {GetOperatorSymbol(rule.ComparisonOperator)} {threshold}")

                    Select Case rule.ComparisonOperator
                        Case ComparisonOperator.GreaterThan
                            triggered = currentValue > threshold
                        Case ComparisonOperator.GreaterOrEqual
                            triggered = currentValue >= threshold
                        Case ComparisonOperator.LessThan
                            triggered = currentValue < threshold
                        Case ComparisonOperator.LessOrEqual
                            triggered = currentValue <= threshold
                        Case ComparisonOperator.Equal
                            triggered = currentValue = threshold
                    End Select

                    If triggered Then
                        Console.WriteLine($"  ✅ DÉCLENCHÉE! {rule.Message}")
                    End If
                Else
                    ' Comparaison texte
                    Console.WriteLine($"  🔍 Test règle '{rule.Name}': '{value}' = '{rule.TriggerValue}'")
                    triggered = value.ToString().ToLower() = rule.TriggerValue.ToLower()

                    If triggered Then
                        Console.WriteLine($"  ✅ DÉCLENCHÉE! {rule.Message}")
                    End If
                End If

                If triggered Then
                    rule.LastTriggered = DateTime.Now
                    AddNotification(deviceName, rule.Message, rule.NotificationType, rule.PlaySound)
                End If
            End If
        Next
    End Sub

    ' Alias pour compatibilité avec l'ancien code
    Public Sub CheckAndNotify(device As Object, roomName As String)
        CheckDevice(device)
    End Sub

    Private Sub AddNotification(deviceName As String, message As String, type As NotificationType, playSound As Boolean)
        Dim entry As New NotificationEntry With {
            .Timestamp = DateTime.Now,
            .DeviceName = deviceName,
            .Message = message,
            .Type = type,
            .NotificationType = type,
            .IsRead = False
        }

        _notifications.Insert(0, entry)

        If playSound Then
            PlayNotificationSound(type)
        End If

        RaiseEvent NotificationAdded(Me, entry)
    End Sub

    Private Sub PlayNotificationSound(type As NotificationType)
        Try
            Select Case type
                Case NotificationType.Critical
                    SystemSounds.Hand.Play()
                Case NotificationType.Warning
                    SystemSounds.Exclamation.Play()
                Case NotificationType.Info
                    SystemSounds.Asterisk.Play()
            End Select
        Catch ex As Exception
            ' Son non disponible
        End Try
    End Sub

    Public Function GetNotifications(Optional includeRead As Boolean = True) As List(Of NotificationEntry)
        If includeRead Then
            Return _notifications
        Else
            Return _notifications.Where(Function(n) Not n.IsRead).ToList()
        End If
    End Function

    Public Function GetUnreadCount() As Integer
        Return _notifications.Where(Function(n) Not n.IsRead).Count()
    End Function

    Public Sub MarkAsRead(notification As NotificationEntry)
        notification.IsRead = True
    End Sub

    Public Sub MarkAllAsRead()
        For Each n In _notifications
            n.IsRead = True
        Next
    End Sub

    Public Sub ClearNotifications()
        _notifications.Clear()
    End Sub

    ' Helper pour afficher le symbole de l'opérateur
    Public Function GetOperatorSymbol(op As ComparisonOperator) As String
        Select Case op
            Case ComparisonOperator.GreaterThan
                Return ">"
            Case ComparisonOperator.GreaterOrEqual
                Return ">="
            Case ComparisonOperator.LessThan
                Return "<"
            Case ComparisonOperator.LessOrEqual
                Return "<="
            Case ComparisonOperator.Equal
                Return "="
            Case Else
                Return "?"
        End Select
    End Function
End Class