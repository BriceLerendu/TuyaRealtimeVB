Imports System.IO
Imports Newtonsoft.Json.Linq

Public Class TuyaDeviceCategories
    Private Shared _instance As TuyaDeviceCategories
    Private _categories As Dictionary(Of String, DeviceCategoryInfo)
    Private _defaultIcon As String
    Private _defaultName As String

    Public Class DeviceCategoryInfo
        Public Property Name As String
        Public Property Icon As String
    End Class

    Private Sub New()
        LoadCategories()
    End Sub

    Public Shared Function GetInstance() As TuyaDeviceCategories
        If _instance Is Nothing Then
            _instance = New TuyaDeviceCategories()
        End If
        Return _instance
    End Function

    Private Sub LoadCategories()
        Try
            Dim jsonPath As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tuya_devices.json")

            ' LOGS DE DIAGNOSTIC
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")
            Console.WriteLine("🔍 DIAGNOSTIC CHARGEMENT JSON")
            Console.WriteLine($"🔍 Chemin recherché : {jsonPath}")
            Console.WriteLine($"🔍 Répertoire de base : {AppDomain.CurrentDomain.BaseDirectory}")
            Console.WriteLine($"🔍 Fichier existe : {File.Exists(jsonPath)}")

            If Not File.Exists(jsonPath) Then
                Console.WriteLine("❌ FICHIER JSON NON TROUVÉ !")
                Console.WriteLine("📂 Fichiers présents dans le répertoire :")
                Try
                    For Each file In Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.json")
                        Console.WriteLine($"   ✓ {Path.GetFileName(file)}")
                    Next

                    If Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.json").Length = 0 Then
                        Console.WriteLine("   (aucun fichier .json trouvé)")
                    End If
                Catch ex As Exception
                    Console.WriteLine($"   Erreur listage : {ex.Message}")
                End Try
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")
                InitializeDefaults()
                Return
            End If

            Console.WriteLine("✓ Fichier JSON trouvé, chargement en cours...")
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")

            Dim jsonContent As String = File.ReadAllText(jsonPath)
            Dim jsonObj As JObject = JObject.Parse(jsonContent)

            ' Charger les catégories - AVEC CAST EXPLICITE
            _categories = New Dictionary(Of String, DeviceCategoryInfo)
            Dim categoriesObj As JObject = CType(jsonObj("categories"), JObject)

            For Each prop In categoriesObj.Properties()
                Dim categoryId As String = prop.Name
                Dim categoryData As JObject = CType(prop.Value, JObject)

                _categories(categoryId) = New DeviceCategoryInfo With {
                .Name = categoryData("name")?.ToString(),
                .Icon = categoryData("icon")?.ToString()
            }
            Next

            ' Charger les valeurs par défaut
            _defaultIcon = jsonObj("default_icon")?.ToString()
            _defaultName = jsonObj("default_name")?.ToString()

            Console.WriteLine($"✅ {_categories.Count} catégories d'appareils chargées depuis JSON")

        Catch ex As Exception
            Console.WriteLine($"❌ Erreur lors du chargement du JSON : {ex.Message}")
            InitializeDefaults()
        End Try
    End Sub

    Private Sub InitializeDefaults()
        _categories = New Dictionary(Of String, DeviceCategoryInfo)
        _defaultIcon = "📱"
        _defaultName = "Appareil"
    End Sub

    Public Function GetDeviceIcon(category As String) As String
        If String.IsNullOrEmpty(category) Then Return _defaultIcon

        If _categories.ContainsKey(category) Then
            Return _categories(category).Icon
        End If

        Return _defaultIcon
    End Function

    Public Function GetDeviceName(category As String) As String
        If String.IsNullOrEmpty(category) Then Return _defaultName

        If _categories.ContainsKey(category) Then
            Return _categories(category).Name
        End If

        Return _defaultName
    End Function

    Public Function GetDeviceInfo(category As String) As (icon As String, name As String)
        Dim icon As String = GetDeviceIcon(category)
        Dim name As String = GetDeviceName(category)
        Return (icon, name)
    End Function

    Public Function GetAllCategories() As Dictionary(Of String, DeviceCategoryInfo)
        Return _categories
    End Function
End Class