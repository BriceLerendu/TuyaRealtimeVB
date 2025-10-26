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
    ''' Formate une valeur en utilisant le scale automatique depuis les spécifications Tuya API
    ''' </summary>
    Public Function FormatValueWithScale(category As String, deviceId As String, code As String, rawValue As String, apiClient As TuyaApiClient) As String
        Try
            Debug.WriteLine($"[FormatValueWithScale] APPELÉ: category={category}, deviceId={deviceId}, code={code}, rawValue={rawValue}")

            ' Gérer les sous-propriétés JSON (ex: phase_a.electricCurrent)
            If code.Contains(".") Then
                Return FormatSubPropertyValue(code, rawValue)
            End If

            ' Récupérer les specs depuis le cache
            Dim specs = apiClient.GetCachedDeviceSpecification(deviceId)
            If specs Is Nothing Then
                Debug.WriteLine($"[FormatValueWithScale] FALLBACK: Pas de specs pour {deviceId}")
                ' Fallback sur l'ancien système si pas de specs
                Return FormatValue(category, code, rawValue)
            End If

            Debug.WriteLine($"[FormatValueWithScale] Specs trouvées pour {deviceId}")

            ' Chercher dans functions ou status
            Dim specItem As JToken = Nothing
            If specs("functions") IsNot Nothing Then
                specItem = FindSpecByCode(CType(specs("functions"), JArray), code)
            End If
            If specItem Is Nothing AndAlso specs("status") IsNot Nothing Then
                specItem = FindSpecByCode(CType(specs("status"), JArray), code)
            End If

            If specItem Is Nothing Then
                ' Fallback sur l'ancien système
                Return FormatValue(category, code, rawValue)
            End If

            ' Extraire type, scale, unit
            Dim specType = specItem("type")?.ToString()

            Select Case specType
                Case "Integer", "Decimal"
                    Return FormatNumericWithScale(code, rawValue, specItem)

                Case "Boolean"
                    Return FormatBooleanValue(rawValue)

                Case "Enum"
                    Return rawValue

                Case "Json"
                    Return FormatJsonValue(code, rawValue)

                Case Else
                    Return rawValue
            End Select

        Catch ex As Exception
            Debug.WriteLine($"Erreur FormatValueWithScale {category}/{code}: {ex.Message}")
            ' Fallback sur l'ancien système en cas d'erreur
            Return FormatValue(category, code, rawValue)
        End Try
    End Function

    ''' <summary>
    ''' Formate une valeur numérique avec le scale et conversions d'unités intelligentes
    ''' </summary>
    Private Function FormatNumericWithScale(code As String, rawValue As String, specItem As JToken) As String
        Try
            Dim valuesJson = specItem("values")?.ToString()
            If String.IsNullOrEmpty(valuesJson) Then
                Debug.WriteLine($"[FormatNumericWithScale] Pas de values pour {code}")
                Return rawValue
            End If

            Dim values = JObject.Parse(valuesJson)
            Dim scale = If(values("scale") IsNot Nothing, CInt(values("scale")), 0)
            Dim unit = If(values("unit") IsNot Nothing, values("unit").ToString(), "")

            ' Convertir avec scale
            Dim numValue As Decimal
            If Not Decimal.TryParse(rawValue, numValue) Then Return rawValue

            Dim scaleFactor = CDec(Math.Pow(10, scale))
            Dim displayValue = numValue / scaleFactor

            Debug.WriteLine($"[FormatNumericWithScale] code={code}, raw={rawValue}, scale={scale}, unit={unit}, display={displayValue}")

            ' === CONVERSIONS D'UNITÉS INTELLIGENTES ===

            ' 1. Énergies totales (forward_energy, add_ele, etc.) : toujours en kWh
            ' Note: Après le scale, la valeur est déjà en kWh (pas besoin de diviser par 1000)
            If code.ToLower().Contains("forward") OrElse
               code.ToLower().Contains("total") OrElse
               code.ToLower().Contains("add_ele") OrElse
               code.ToLower() = "energy" Then

                Debug.WriteLine($"[FormatNumericWithScale] → Détection énergie totale pour {code}")
                Debug.WriteLine($"[FormatNumericWithScale] → Affichage direct en kWh: {displayValue}")

                ' La valeur après scale est déjà en kWh
                Return $"{displayValue.ToString("F2")} kWh"
            End If

            ' 2. Puissance instantanée (cur_power) : W ou kW selon la valeur
            If code.ToLower().Contains("power") AndAlso Not code.ToLower().Contains("total") Then
                If unit.ToLower() = "w" OrElse String.IsNullOrEmpty(unit) Then
                    ' Si > 1000W, afficher en kW
                    If displayValue >= 1000 Then
                        Dim kwValue = displayValue / 1000
                        Return $"{kwValue.ToString("F2")} kW"
                    Else
                        Return $"{displayValue.ToString("F0")} W"
                    End If
                End If
            End If

            ' 3. Courant (cur_current) : toujours en A avec 2 décimales
            If code.ToLower().Contains("current") Then
                If unit.ToLower() = "a" OrElse String.IsNullOrEmpty(unit) Then
                    Return $"{displayValue.ToString("F2")} A"
                End If
            End If

            ' 4. Tension (cur_voltage) : toujours en V avec 1 décimale
            If code.ToLower().Contains("voltage") Then
                If unit.ToLower() = "v" OrElse String.IsNullOrEmpty(unit) Then
                    Return $"{displayValue.ToString("F1")} V"
                End If
            End If

            ' === FORMATAGE PAR DÉFAUT ===
            Dim formatted = displayValue.ToString($"F{scale}")
            Return If(String.IsNullOrEmpty(unit), formatted, $"{formatted} {unit}")

        Catch ex As Exception
            Debug.WriteLine($"Erreur FormatNumericWithScale: {ex.Message}")
            Return rawValue
        End Try
    End Function

    ''' <summary>
    ''' Formate une valeur booléenne simple
    ''' </summary>
    Private Function FormatBooleanValue(rawValue As String) As String
        If rawValue.ToLower() = "true" OrElse rawValue = "1" Then
            Return "✓ Activé"
        Else
            Return "○ Désactivé"
        End If
    End Function

    ''' <summary>
    ''' Formate une valeur JSON (objet imbriqué)
    ''' </summary>
    Private Function FormatJsonValue(code As String, rawValue As String) As String
        Try
            ' Tenter de parser le JSON
            Dim jsonObj = JObject.Parse(rawValue)
            Dim formattedParts As New List(Of String)

            ' Traitement spécial pour les phases (phase_a, phase_b, phase_c)
            If code.ToLower().StartsWith("phase_") Then
                ' Extraire les valeurs communes des phases
                Dim current = jsonObj("electricCurrent")
                Dim voltage = jsonObj("voltage")
                Dim power = jsonObj("power")

                If current IsNot Nothing Then
                    Dim currentValue = CDec(current)
                    formattedParts.Add($"I:{currentValue.ToString("F3")}A")
                End If

                If voltage IsNot Nothing Then
                    Dim voltageValue = CDec(voltage)
                    formattedParts.Add($"U:{voltageValue.ToString("F1")}V")
                End If

                If power IsNot Nothing Then
                    Dim powerValue = CDec(power)
                    If powerValue >= 1 Then
                        formattedParts.Add($"P:{powerValue.ToString("F0")}W")
                    Else
                        formattedParts.Add($"P:{(powerValue * 1000).ToString("F0")}mW")
                    End If
                End If

                Return String.Join(" / ", formattedParts)
            End If

            ' Pour les autres types de JSON, afficher les propriétés génériques
            For Each prop In jsonObj.Properties()
                Dim propName = prop.Name
                Dim propValue = prop.Value

                If propValue.Type = JTokenType.Float OrElse propValue.Type = JTokenType.Integer Then
                    formattedParts.Add($"{propName}:{propValue}")
                ElseIf propValue.Type = JTokenType.String Then
                    formattedParts.Add($"{propName}:{propValue}")
                End If
            Next

            If formattedParts.Count > 0 Then
                Return String.Join(" / ", formattedParts)
            End If

            ' Fallback: retourner le JSON brut
            Return rawValue
        Catch ex As Exception
            Debug.WriteLine($"Erreur FormatJsonValue pour {code}: {ex.Message}")
            ' En cas d'erreur, retourner le JSON brut
            Return rawValue
        End Try
    End Function

    ''' <summary>
    ''' Formate une sous-propriété extraite d'un JSON
    ''' </summary>
    Private Function FormatSubPropertyValue(fullCode As String, rawValue As String) As String
        Try
            ' Extraire le nom de la sous-propriété (ex: phase_a.electricCurrent → electricCurrent)
            Dim parts = fullCode.Split("."c)
            If parts.Length <> 2 Then Return rawValue

            Dim subPropertyName = parts(1).ToLower()

            ' Parser la valeur numérique
            Dim numValue As Decimal
            If Not Decimal.TryParse(rawValue, numValue) Then Return rawValue

            ' Formater selon le type de sous-propriété
            Select Case subPropertyName
                Case "electriccurrent", "current"
                    ' Courant en ampères avec 2 décimales
                    Return $"{numValue.ToString("F2")} A"

                Case "voltage"
                    ' Tension en volts avec 1 décimale
                    Return $"{numValue.ToString("F1")} V"

                Case "power"
                    ' Puissance en watts ou milliwatts
                    If numValue >= 1 Then
                        Return $"{numValue.ToString("F0")} W"
                    Else
                        Return $"{(numValue * 1000).ToString("F0")} mW"
                    End If

                Case "energy"
                    ' Énergie en kWh avec 2 décimales
                    Return $"{numValue.ToString("F2")} kWh"

                Case "temperature"
                    ' Température en °C avec 1 décimale
                    Return $"{numValue.ToString("F1")} °C"

                Case "humidity"
                    ' Humidité en % avec 1 décimale
                    Return $"{numValue.ToString("F1")} %"

                Case Else
                    ' Par défaut, 2 décimales
                    Return numValue.ToString("F2")
            End Select

        Catch ex As Exception
            Debug.WriteLine($"Erreur FormatSubPropertyValue pour {fullCode}: {ex.Message}")
            Return rawValue
        End Try
    End Function

    ''' <summary>
    ''' Trouve une spécification par code dans un tableau
    ''' </summary>
    Private Function FindSpecByCode(specs As JArray, code As String) As JToken
        Try
            For Each spec As JToken In specs
                If spec("code")?.ToString() = code Then
                    Return spec
                End If
            Next
        Catch ex As Exception
            Debug.WriteLine($"Erreur FindSpecByCode: {ex.Message}")
        End Try
        Return Nothing
    End Function

    ''' <summary>
    ''' Obtient le nom d'affichage d'une propriété
    ''' </summary>
    Public Function GetDisplayName(category As String, code As String) As String
        Try
            ' Gérer les sous-propriétés JSON (ex: phase_a.electricCurrent)
            If code.Contains(".") Then
                Return GetSubPropertyDisplayName(code)
            End If

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
    ''' Obtient le nom d'affichage d'une sous-propriété JSON
    ''' </summary>
    Private Function GetSubPropertyDisplayName(fullCode As String) As String
        Try
            Dim parts = fullCode.Split("."c)
            If parts.Length <> 2 Then Return fullCode

            Dim parentCode = parts(0)
            Dim subPropertyName = parts(1)

            ' Formater le nom du parent (ex: phase_a → Phase A)
            Dim parentDisplay = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
                parentCode.Replace("_", " ")
            )

            ' Traduire les noms de sous-propriétés courants
            Dim subPropertyDisplay = subPropertyName
            Select Case subPropertyName.ToLower()
                Case "electriccurrent"
                    subPropertyDisplay = "Courant"
                Case "voltage"
                    subPropertyDisplay = "Tension"
                Case "power"
                    subPropertyDisplay = "Puissance"
                Case "energy"
                    subPropertyDisplay = "Énergie"
                Case "temperature"
                    subPropertyDisplay = "Température"
                Case "humidity"
                    subPropertyDisplay = "Humidité"
                Case Else
                    ' Par défaut, nettoyer le nom
                    subPropertyDisplay = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
                        subPropertyName.Replace("_", " ")
                    )
            End Select

            Return $"{parentDisplay} - {subPropertyDisplay}"
        Catch ex As Exception
            Return fullCode
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
    ''' Détermine le type de graphique adapté pour une propriété (numeric ou state)
    ''' </summary>
    ''' <param name="category">Catégorie de l'appareil</param>
    ''' <param name="code">Code de la propriété</param>
    ''' <returns>"numeric" pour graphique de valeurs, "state" pour timeline d'états</returns>
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