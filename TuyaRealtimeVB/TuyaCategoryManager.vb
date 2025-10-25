Imports System.IO
Imports Newtonsoft.Json.Linq

Public Class TuyaCategoryManager
    Private Shared _instance As TuyaCategoryManager
    Private _config As JObject
    Private ReadOnly _configPath As String

    Private Sub New()
        _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "categories_config.json")
        LoadConfiguration()
    End Sub

    Public Shared ReadOnly Property Instance As TuyaCategoryManager
        Get
            If _instance Is Nothing Then
                _instance = New TuyaCategoryManager()
            End If
            Return _instance
        End Get
    End Property

    ''' <summary>
    ''' Charge la configuration depuis le fichier JSON
    ''' </summary>
    Public Sub LoadConfiguration()
        Try
            If File.Exists(_configPath) Then
                Dim json = File.ReadAllText(_configPath)
                _config = JObject.Parse(json)
            Else
                ' Créer une configuration par défaut
                CreateDefaultConfiguration()
            End If
        Catch ex As Exception
            Debug.WriteLine($"Erreur chargement configuration: {ex.Message}")
            CreateDefaultConfiguration()
        End Try
    End Sub

    ''' <summary>
    ''' Sauvegarde la configuration dans le fichier JSON
    ''' </summary>
    Public Sub SaveConfiguration()
        Try
            Dim json = _config.ToString(Newtonsoft.Json.Formatting.Indented)
            File.WriteAllText(_configPath, json)
        Catch ex As Exception
            Throw New Exception($"Erreur sauvegarde configuration: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Formate une valeur selon les règles de la catégorie
    ''' </summary>
    Public Function FormatValue(category As String, code As String, value As String) As String
        Try
            Dim propertyConfig = GetPropertyConfig(category, code)

            If propertyConfig Is Nothing Then
                ' Pas de config, retourner la valeur brute
                Return value
            End If

            Dim conversion = propertyConfig("conversion")?.ToString()

            Select Case conversion
                Case "divide"
                    Return FormatDivision(value, propertyConfig)

                Case "multiply"
                    Return FormatMultiplication(value, propertyConfig)

                Case "boolean"
                    Return FormatBoolean(value, propertyConfig)

                Case "enum"
                    Return FormatEnum(value, propertyConfig)

                Case "conditional"
                    Return FormatConditional(value, propertyConfig)

                Case Else
                    Return FormatSimple(value, propertyConfig)
            End Select

        Catch ex As Exception
            Debug.WriteLine($"Erreur formatage {category}/{code}: {ex.Message}")
            Return value
        End Try
    End Function

    ''' <summary>
    ''' Obtient le nom d'affichage d'une propriété
    ''' </summary>
    Public Function GetDisplayName(category As String, code As String) As String
        Try
            Dim propertyConfig = GetPropertyConfig(category, code)

            If propertyConfig IsNot Nothing Then
                Dim displayName = propertyConfig("displayName")?.ToString()
                Dim icon = propertyConfig("icon")?.ToString()

                If Not String.IsNullOrEmpty(icon) AndAlso Not String.IsNullOrEmpty(displayName) Then
                    Return $"{icon} {displayName}"
                ElseIf Not String.IsNullOrEmpty(displayName) Then
                    Return displayName
                End If
            End If

            ' Nom par défaut : nettoyer le code
            Return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
                code.Replace("_", " ")
            )
        Catch ex As Exception
            Return code
        End Try
    End Function

    ''' <summary>
    ''' Obtient la configuration d'une propriété
    ''' </summary>
    Private Function GetPropertyConfig(category As String, code As String) As JToken
        Try
            ' Chercher dans la catégorie spécifique
            Dim categoryConfig = _config("categories")(category)

            If categoryConfig IsNot Nothing Then
                Dim properties = categoryConfig("properties")

                If properties IsNot Nothing Then
                    ' Chercher la propriété exacte
                    Dim propertyConfig = properties(code)
                    If propertyConfig IsNot Nothing Then Return propertyConfig

                    ' Chercher une propriété wildcard
                    Dim wildcardConfig = properties("*")
                    If wildcardConfig IsNot Nothing Then Return wildcardConfig
                End If
            End If

            ' Fallback sur la configuration par défaut
            Dim defaultConfig = _config("categories")("default")
            If defaultConfig IsNot Nothing Then
                Return defaultConfig("properties")("*")
            End If

        Catch ex As Exception
            Debug.WriteLine($"Erreur GetPropertyConfig: {ex.Message}")
        End Try

        Return Nothing
    End Function

    ''' <summary>
    ''' Formate une valeur avec division
    ''' </summary>
    Private Function FormatDivision(value As String, config As JToken) As String
        Dim numValue As Double
        If Not Double.TryParse(value, numValue) Then Return value

        Dim divisor = config("divisor")?.Value(Of Double)()
        Dim unit = config("unit")?.ToString()
        Dim decimals = config("decimals")?.Value(Of Integer)()

        If divisor.HasValue AndAlso divisor.Value > 0 Then
            numValue /= divisor.Value
        End If

        ' Vérifier les seuils (ex: 1000W -> 1kW)
        Dim thresholds = config("thresholds")
        If thresholds IsNot Nothing Then
            Dim high = thresholds("high")?.Value(Of Double)()
            If high.HasValue AndAlso numValue >= high.Value Then
                Dim unitHigh = thresholds("unit_high")?.ToString()
                Dim divisorHigh = thresholds("divisor_high")?.Value(Of Double)()

                If divisorHigh.HasValue Then
                    numValue /= divisorHigh.Value
                End If

                If Not String.IsNullOrEmpty(unitHigh) Then
                    unit = unitHigh
                End If
            End If
        End If

        Dim format = If(decimals.HasValue, $"F{decimals.Value}", "F2")
        Return $"{numValue.ToString(format)} {unit}".Trim()
    End Function

    ''' <summary>
    ''' Formate une valeur avec multiplication
    ''' </summary>
    Private Function FormatMultiplication(value As String, config As JToken) As String
        Dim numValue As Double
        If Not Double.TryParse(value, numValue) Then Return value

        Dim multiplier = config("multiplier")?.Value(Of Double)()
        Dim unit = config("unit")?.ToString()
        Dim decimals = config("decimals")?.Value(Of Integer)()

        If multiplier.HasValue Then
            numValue *= multiplier.Value
        End If

        Dim format = If(decimals.HasValue, $"F{decimals.Value}", "F2")
        Return $"{numValue.ToString(format)} {unit}".Trim()
    End Function

    ''' <summary>
    ''' Formate une valeur booléenne
    ''' </summary>
    Private Function FormatBoolean(value As String, config As JToken) As String
        Dim boolDisplay = config("booleanDisplay")

        If boolDisplay IsNot Nothing Then
            ' Chercher la valeur exacte
            Dim displayValue = boolDisplay(value)?.ToString()
            If displayValue IsNot Nothing Then Return displayValue

            ' Chercher true/false
            If value.Equals("true", StringComparison.OrdinalIgnoreCase) OrElse value = "1" Then
                displayValue = boolDisplay("true")?.ToString()
                If displayValue IsNot Nothing Then Return displayValue
            ElseIf value.Equals("false", StringComparison.OrdinalIgnoreCase) OrElse value = "0" Then
                displayValue = boolDisplay("false")?.ToString()
                If displayValue IsNot Nothing Then Return displayValue
            End If

            ' Valeur par défaut
            displayValue = boolDisplay("default")?.ToString()
            If displayValue IsNot Nothing Then Return displayValue
        End If

        ' Fallback standard
        If value.Equals("true", StringComparison.OrdinalIgnoreCase) OrElse value = "1" Then
            Return "✓ Activé"
        Else
            Return "○ Désactivé"
        End If
    End Function

    ''' <summary>
    ''' Formate une valeur énumérée
    ''' </summary>
    Private Function FormatEnum(value As String, config As JToken) As String
        Dim enumValues = config("enumValues")

        If enumValues IsNot Nothing Then
            Dim displayValue = enumValues(value)?.ToString()
            If displayValue IsNot Nothing Then Return displayValue
        End If

        Return value
    End Function

    ''' <summary>
    ''' Formate une valeur simple (avec unité)
    ''' </summary>
    Private Function FormatSimple(value As String, config As JToken) As String
        Dim unit = config("unit")?.ToString()
        Dim decimals = config("decimals")?.Value(Of Integer)()

        Dim numValue As Double
        If Double.TryParse(value, numValue) AndAlso decimals.HasValue Then
            Dim format = $"F{decimals.Value}"
            Return $"{numValue.ToString(format)} {unit}".Trim()
        End If

        If Not String.IsNullOrEmpty(unit) Then
            Return $"{value} {unit}"
        End If

        Return value
    End Function

    ''' <summary>
    ''' Formate avec condition (ex: diviser seulement si > 100)
    ''' </summary>
    Private Function FormatConditional(value As String, config As JToken) As String
        Dim condition = config("condition")

        If condition IsNot Nothing Then
            Dim numValue As Double
            If Double.TryParse(value, numValue) Then
                Dim threshold = condition("threshold")?.Value(Of Double)()
                Dim operator_str = condition("operator")?.ToString()

                Dim conditionMet As Boolean = False

                If threshold.HasValue Then
                    Select Case operator_str
                        Case "gt"
                            conditionMet = numValue > threshold.Value
                        Case "lt"
                            conditionMet = numValue < threshold.Value
                        Case "gte"
                            conditionMet = numValue >= threshold.Value
                        Case "lte"
                            conditionMet = numValue <= threshold.Value
                    End Select
                End If

                If conditionMet Then
                    ' Appliquer la conversion
                    Return FormatDivision(value, config)
                End If
            End If
        End If

        ' Sinon, formatage simple
        Return FormatSimple(value, config)
    End Function

    ''' <summary>
    ''' Obtient la liste de toutes les catégories
    ''' </summary>
    Public Function GetAllCategories() As Dictionary(Of String, String)
        Dim categories As New Dictionary(Of String, String)

        Try
            Dim categoriesNode = _config("categories")

            If categoriesNode IsNot Nothing Then
                For Each prop As JProperty In categoriesNode.Children(Of JProperty)()
                    Dim categoryId = prop.Name
                    Dim categoryName = prop.Value("name")?.ToString()

                    If Not String.IsNullOrEmpty(categoryName) Then
                        categories(categoryId) = categoryName
                    End If
                Next
            End If
        Catch ex As Exception
            Debug.WriteLine($"Erreur GetAllCategories: {ex.Message}")
        End Try

        Return categories
    End Function

    ''' <summary>
    ''' Obtient la configuration complète (pour édition)
    ''' </summary>
    Public Function GetConfiguration() As JObject
        Return _config
    End Function

    ''' <summary>
    ''' Met à jour la configuration
    ''' </summary>
    Public Sub UpdateConfiguration(newConfig As JObject)
        _config = newConfig
        SaveConfiguration()
    End Sub

    ''' <summary>
    ''' Crée une configuration par défaut minimale
    ''' </summary>
    Private Sub CreateDefaultConfiguration()
        _config = JObject.Parse("{
            ""version"": ""1.0"",
            ""categories"": {
                ""default"": {
                    ""name"": ""Appareil générique"",
                    ""properties"": {
                        ""*"": {
                            ""conversion"": ""none"",
                            ""decimals"": 2
                        }
                    }
                }
            }
        }")

        Try
            SaveConfiguration()
        Catch ex As Exception
            Debug.WriteLine($"Erreur création config par défaut: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Obtient le chemin du fichier de configuration
    ''' </summary>
    Public Function GetConfigPath() As String
        Return _configPath
    End Function

    ''' <summary>
    ''' Détermine le type de graphique à utiliser pour une propriété
    ''' </summary>
    ''' <param name="category">Catégorie de l'appareil (ex: "wsdcg", "cz")</param>
    ''' <param name="code">Code de la propriété (ex: "va_temperature", "switch")</param>
    ''' <returns>"state" pour états discrets, "numeric" pour valeurs continues</returns>
    Public Function GetPropertyChartType(category As String, code As String) As String
        Try
            Dim propertyConfig = GetPropertyConfig(category, code)

            If propertyConfig Is Nothing Then
                Return "numeric" ' Par défaut : numérique
            End If

            Dim conversion = propertyConfig("conversion")?.ToString()

            Select Case conversion
                Case "boolean", "enum"
                    Return "state" ' Timeline d'états
                Case Else
                    Return "numeric" ' Courbe/Barres
            End Select

        Catch ex As Exception
            Debug.WriteLine($"Erreur GetPropertyChartType: {ex.Message}")
            Return "numeric"
        End Try
    End Function

    ''' <summary>
    ''' Obtient l'unité d'une propriété pour l'affichage sur les graphiques
    ''' </summary>
    ''' <param name="category">Catégorie de l'appareil</param>
    ''' <param name="code">Code de la propriété</param>
    ''' <returns>Unité (ex: "°C", "W", "%") ou chaîne vide</returns>
    Public Function GetPropertyUnit(category As String, code As String) As String
        Try
            Dim propertyConfig = GetPropertyConfig(category, code)
            If propertyConfig Is Nothing Then Return ""

            Return If(propertyConfig("unit")?.ToString(), "")

        Catch ex As Exception
            Debug.WriteLine($"Erreur GetPropertyUnit: {ex.Message}")
            Return ""
        End Try
    End Function

    ''' <summary>
    ''' Obtient la liste des propriétés d'une catégorie qui peuvent être affichées dans l'historique
    ''' </summary>
    ''' <param name="category">Catégorie de l'appareil</param>
    ''' <returns>Dictionnaire code -> displayName</returns>
    Public Function GetHistoricalProperties(category As String) As Dictionary(Of String, String)
        Dim properties As New Dictionary(Of String, String)

        Try
            Dim categoryConfig = _config("categories")(category)

            If categoryConfig IsNot Nothing Then
                Dim propertiesNode = categoryConfig("properties")

                If propertiesNode IsNot Nothing Then
                    For Each prop As JProperty In propertiesNode.Children(Of JProperty)()
                        If prop.Name <> "*" Then ' Ignorer les wildcards
                            Dim displayName = prop.Value("displayName")?.ToString()
                            Dim icon = prop.Value("icon")?.ToString()

                            If Not String.IsNullOrEmpty(displayName) Then
                                Dim fullName = If(Not String.IsNullOrEmpty(icon), $"{icon} {displayName}", displayName)
                                properties(prop.Name) = fullName
                            End If
                        End If
                    Next
                End If
            End If

        Catch ex As Exception
            Debug.WriteLine($"Erreur GetHistoricalProperties: {ex.Message}")
        End Try

        Return properties
    End Function
End Class