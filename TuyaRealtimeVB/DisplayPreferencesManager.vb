Imports System.IO
Imports Newtonsoft.Json.Linq

''' <summary>
''' Gestionnaire des préférences d'affichage des propriétés sur les tuiles
''' Permet de choisir quelles propriétés afficher pour chaque catégorie d'appareil
''' </summary>
Public Class DisplayPreferencesManager

#Region "Singleton"
    Private Shared _instance As DisplayPreferencesManager

    Public Shared ReadOnly Property Instance As DisplayPreferencesManager
        Get
            If _instance Is Nothing Then
                _instance = New DisplayPreferencesManager()
            End If
            Return _instance
        End Get
    End Property

    Private Sub New()
        _preferencesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "display_preferences.json")
        LoadPreferences()
    End Sub
#End Region

#Region "Champs privés"
    Private _preferencesPath As String
    Private _preferences As JObject
    Private ReadOnly _lockObject As New Object()
#End Region

#Region "Chargement et Sauvegarde"
    ''' <summary>
    ''' Charge les préférences depuis le fichier JSON
    ''' </summary>
    Public Sub LoadPreferences()
        SyncLock _lockObject
            Try
                If File.Exists(_preferencesPath) Then
                    Dim jsonContent = File.ReadAllText(_preferencesPath)
                    _preferences = JObject.Parse(jsonContent)
                    Debug.WriteLine($"✓ Préférences d'affichage chargées: {_preferencesPath}")
                Else
                    ' Créer une structure vide
                    _preferences = New JObject(
                        New JProperty("version", "1.0"),
                        New JProperty("categories", New JObject())
                    )
                    Debug.WriteLine($"✓ Préférences d'affichage initialisées (nouveau fichier)")
                End If
            Catch ex As Exception
                Debug.WriteLine($"✗ Erreur chargement préférences: {ex.Message}")
                ' En cas d'erreur, créer une structure vide
                _preferences = New JObject(
                    New JProperty("version", "1.0"),
                    New JProperty("categories", New JObject())
                )
            End Try
        End SyncLock
    End Sub

    ''' <summary>
    ''' Sauvegarde les préférences dans le fichier JSON
    ''' </summary>
    Public Sub SavePreferences()
        SyncLock _lockObject
            Try
                Dim jsonContent = _preferences.ToString(Newtonsoft.Json.Formatting.Indented)
                File.WriteAllText(_preferencesPath, jsonContent)
                Debug.WriteLine($"✓ Préférences d'affichage sauvegardées: {_preferencesPath}")
            Catch ex As Exception
                Debug.WriteLine($"✗ Erreur sauvegarde préférences: {ex.Message}")
                Throw
            End Try
        End SyncLock
    End Sub
#End Region

#Region "Gestion des propriétés visibles"
    ''' <summary>
    ''' Retourne la liste des propriétés visibles pour une catégorie
    ''' </summary>
    ''' <returns>Liste des codes de propriétés à afficher, ou Nothing si aucune préférence définie</returns>
    Public Function GetVisibleProperties(category As String) As List(Of String)
        SyncLock _lockObject
            Try
                If String.IsNullOrEmpty(category) Then Return Nothing

                Dim categories = CType(_preferences("categories"), JObject)
                If categories Is Nothing Then Return Nothing

                Dim categoryObj = CType(categories(category), JObject)
                If categoryObj Is Nothing Then Return Nothing

                Dim visibleProps = CType(categoryObj("visible_properties"), JArray)
                If visibleProps Is Nothing Then Return Nothing

                Return visibleProps.Select(Function(t) t.ToString()).ToList()
            Catch ex As Exception
                Debug.WriteLine($"✗ Erreur GetVisibleProperties pour '{category}': {ex.Message}")
                Return Nothing
            End Try
        End SyncLock
    End Function

    ''' <summary>
    ''' Définit les propriétés visibles pour une catégorie
    ''' </summary>
    Public Sub SetVisibleProperties(category As String, properties As List(Of String))
        SyncLock _lockObject
            Try
                If String.IsNullOrEmpty(category) Then Return

                Dim categories = CType(_preferences("categories"), JObject)
                If categories Is Nothing Then
                    categories = New JObject()
                    _preferences("categories") = categories
                End If

                ' Créer ou mettre à jour la catégorie
                Dim categoryObj As JObject
                If categories(category) Is Nothing Then
                    categoryObj = New JObject()
                    categories(category) = categoryObj
                Else
                    categoryObj = CType(categories(category), JObject)
                End If

                ' Mettre à jour les propriétés visibles
                categoryObj("visible_properties") = New JArray(properties)

                Debug.WriteLine($"✓ Préférences mises à jour pour '{category}': {properties.Count} propriétés visibles")
            Catch ex As Exception
                Debug.WriteLine($"✗ Erreur SetVisibleProperties pour '{category}': {ex.Message}")
                Throw
            End Try
        End SyncLock
    End Sub

    ''' <summary>
    ''' Vérifie si une propriété doit être affichée
    ''' </summary>
    ''' <returns>True si la propriété doit être affichée, False sinon. Si aucune préférence n'est définie, retourne True (afficher tout)</returns>
    Public Function IsPropertyVisible(category As String, propertyCode As String) As Boolean
        Dim visibleProps = GetVisibleProperties(category)

        ' Si aucune préférence définie, afficher toutes les propriétés
        If visibleProps Is Nothing OrElse visibleProps.Count = 0 Then
            Return True
        End If

        ' Sinon, vérifier si la propriété est dans la liste
        Return visibleProps.Contains(propertyCode)
    End Function

    ''' <summary>
    ''' Retourne l'ordre des propriétés pour une catégorie
    ''' </summary>
    Public Function GetPropertyOrder(category As String) As List(Of String)
        SyncLock _lockObject
            Try
                If String.IsNullOrEmpty(category) Then Return Nothing

                Dim categories = CType(_preferences("categories"), JObject)
                If categories Is Nothing Then Return Nothing

                Dim categoryObj = CType(categories(category), JObject)
                If categoryObj Is Nothing Then Return Nothing

                Dim propertyOrder = CType(categoryObj("property_order"), JArray)
                If propertyOrder Is Nothing Then Return Nothing

                Return propertyOrder.Select(Function(t) t.ToString()).ToList()
            Catch ex As Exception
                Debug.WriteLine($"✗ Erreur GetPropertyOrder pour '{category}': {ex.Message}")
                Return Nothing
            End Try
        End SyncLock
    End Function

    ''' <summary>
    ''' Définit l'ordre des propriétés pour une catégorie
    ''' </summary>
    Public Sub SetPropertyOrder(category As String, propertyOrder As List(Of String))
        SyncLock _lockObject
            Try
                If String.IsNullOrEmpty(category) Then Return

                Dim categories = CType(_preferences("categories"), JObject)
                If categories Is Nothing Then
                    categories = New JObject()
                    _preferences("categories") = categories
                End If

                ' Créer ou mettre à jour la catégorie
                Dim categoryObj As JObject
                If categories(category) Is Nothing Then
                    categoryObj = New JObject()
                    categories(category) = categoryObj
                Else
                    categoryObj = CType(categories(category), JObject)
                End If

                ' Mettre à jour l'ordre des propriétés
                categoryObj("property_order") = New JArray(propertyOrder)

                Debug.WriteLine($"✓ Ordre des propriétés mis à jour pour '{category}': {propertyOrder.Count} propriétés")
            Catch ex As Exception
                Debug.WriteLine($"✗ Erreur SetPropertyOrder pour '{category}': {ex.Message}")
                Throw
            End Try
        End SyncLock
    End Sub
#End Region

#Region "Réinitialisation"
    ''' <summary>
    ''' Réinitialise les préférences pour une catégorie
    ''' </summary>
    Public Sub ResetCategory(category As String)
        SyncLock _lockObject
            Try
                If String.IsNullOrEmpty(category) Then Return

                Dim categories = CType(_preferences("categories"), JObject)
                If categories IsNot Nothing AndAlso categories(category) IsNot Nothing Then
                    categories.Remove(category)
                    Debug.WriteLine($"✓ Préférences réinitialisées pour '{category}'")
                End If
            Catch ex As Exception
                Debug.WriteLine($"✗ Erreur ResetCategory pour '{category}': {ex.Message}")
                Throw
            End Try
        End SyncLock
    End Sub

    ''' <summary>
    ''' Réinitialise toutes les préférences
    ''' </summary>
    Public Sub ResetAll()
        SyncLock _lockObject
            _preferences = New JObject(
                New JProperty("version", "1.0"),
                New JProperty("categories", New JObject())
            )
            Debug.WriteLine($"✓ Toutes les préférences ont été réinitialisées")
        End SyncLock
    End Sub
#End Region

End Class
