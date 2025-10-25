Imports System.Drawing
Imports System.Windows.Forms
Imports Newtonsoft.Json.Linq

''' <summary>
''' Form pour gérer les préférences d'affichage des propriétés sur les tuiles
''' </summary>
Public Class DisplayPreferencesForm
    Inherits Form

#Region "Champs privés"
    Private _apiClient As TuyaApiClient
    Private _dashboardForm As DashboardForm
    Private _preferencesManager As DisplayPreferencesManager
    Private _categoryManager As TuyaCategoryManager
    Private _deviceCategories As TuyaDeviceCategories
    Private _changesSaved As Boolean = False

    ' Dictionnaire: category -> List of (code, name, type)
    Private _categoriesProperties As New Dictionary(Of String, List(Of PropertyInfo))

    ' Contrôles UI
    Private _categoryComboBox As ComboBox
    Private _propertiesCheckedListBox As CheckedListBox
    Private _countLabel As Label
    Private _moveUpButton As Button
    Private _moveDownButton As Button
    Private _selectAllButton As Button
    Private _deselectAllButton As Button
    Private _resetButton As Button
    Private _saveButton As Button
    Private _cancelButton As Button
#End Region

#Region "Classe interne pour stocker les infos de propriété"
    Private Class PropertyInfo
        Public Property Code As String
        Public Property Name As String
        Public Property Type As String
        Public Property Icon As String

        Public Overrides Function ToString() As String
            Return $"{Icon} {Name} ({Code})"
        End Function
    End Class
#End Region

#Region "Initialisation"
    Public Sub New(apiClient As TuyaApiClient, dashboardForm As DashboardForm)
        _apiClient = apiClient
        _dashboardForm = dashboardForm
        _preferencesManager = DisplayPreferencesManager.Instance
        _categoryManager = TuyaCategoryManager.Instance
        _deviceCategories = TuyaDeviceCategories.GetInstance()

        InitializeComponent()
        LoadCategoriesFromCache()
    End Sub

    Private Sub InitializeComponent()
        ' Configuration du form
        Me.Text = "Préférences d'affichage des propriétés"
        Me.Size = New Size(700, 600)
        Me.StartPosition = FormStartPosition.CenterParent
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.BackColor = Color.FromArgb(242, 242, 247)
        Me.Font = New Font("Segoe UI", 10)

        ' ComboBox de sélection de catégorie
        Dim categoryLabel = New Label With {
            .Text = "Catégorie :",
            .Location = New Point(20, 20),
            .Size = New Size(100, 25),
            .Font = New Font("Segoe UI", 10, FontStyle.Bold)
        }
        Me.Controls.Add(categoryLabel)

        _categoryComboBox = New ComboBox With {
            .Location = New Point(130, 18),
            .Size = New Size(540, 25),
            .DropDownStyle = ComboBoxStyle.DropDownList,
            .Font = New Font("Segoe UI", 10)
        }
        AddHandler _categoryComboBox.SelectedIndexChanged, AddressOf CategoryComboBox_SelectedIndexChanged
        Me.Controls.Add(_categoryComboBox)

        ' Label pour les propriétés disponibles
        Dim propertiesLabel = New Label With {
            .Text = "Propriétés disponibles (cochez pour afficher sur les tuiles) :",
            .Location = New Point(20, 60),
            .Size = New Size(650, 25),
            .Font = New Font("Segoe UI", 10, FontStyle.Bold)
        }
        Me.Controls.Add(propertiesLabel)

        ' CheckedListBox pour les propriétés
        _propertiesCheckedListBox = New CheckedListBox With {
            .Location = New Point(20, 90),
            .Size = New Size(560, 330),
            .Font = New Font("Segoe UI", 9),
            .CheckOnClick = True
        }
        AddHandler _propertiesCheckedListBox.ItemCheck, AddressOf PropertiesCheckedListBox_ItemCheck
        AddHandler _propertiesCheckedListBox.SelectedIndexChanged, AddressOf PropertiesCheckedListBox_SelectedIndexChanged
        Me.Controls.Add(_propertiesCheckedListBox)

        ' Boutons de déplacement (↑ et ↓)
        _moveUpButton = New Button With {
            .Text = "↑",
            .Location = New Point(590, 90),
            .Size = New Size(80, 40),
            .Font = New Font("Segoe UI", 16, FontStyle.Bold),
            .BackColor = Color.FromArgb(0, 122, 255),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Enabled = False
        }
        AddHandler _moveUpButton.Click, AddressOf MoveUpButton_Click
        Me.Controls.Add(_moveUpButton)

        _moveDownButton = New Button With {
            .Text = "↓",
            .Location = New Point(590, 140),
            .Size = New Size(80, 40),
            .Font = New Font("Segoe UI", 16, FontStyle.Bold),
            .BackColor = Color.FromArgb(0, 122, 255),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Enabled = False
        }
        AddHandler _moveDownButton.Click, AddressOf MoveDownButton_Click
        Me.Controls.Add(_moveDownButton)

        ' Label de comptage
        _countLabel = New Label With {
            .Text = "0 propriété(s) sélectionnée(s) sur 5 maximum",
            .Location = New Point(20, 430),
            .Size = New Size(560, 25),
            .Font = New Font("Segoe UI", 9, FontStyle.Italic),
            .ForeColor = Color.FromArgb(99, 99, 102)
        }
        Me.Controls.Add(_countLabel)

        ' Boutons d'action
        _selectAllButton = New Button With {
            .Text = "Tout sélectionner",
            .Location = New Point(20, 465),
            .Size = New Size(140, 35),
            .BackColor = Color.FromArgb(142, 142, 147),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 9)
        }
        AddHandler _selectAllButton.Click, AddressOf SelectAllButton_Click
        Me.Controls.Add(_selectAllButton)

        _deselectAllButton = New Button With {
            .Text = "Tout désélectionner",
            .Location = New Point(170, 465),
            .Size = New Size(140, 35),
            .BackColor = Color.FromArgb(142, 142, 147),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 9)
        }
        AddHandler _deselectAllButton.Click, AddressOf DeselectAllButton_Click
        Me.Controls.Add(_deselectAllButton)

        _resetButton = New Button With {
            .Text = "Réinitialiser",
            .Location = New Point(320, 465),
            .Size = New Size(120, 35),
            .BackColor = Color.FromArgb(255, 149, 0),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 9)
        }
        AddHandler _resetButton.Click, AddressOf ResetButton_Click
        Me.Controls.Add(_resetButton)

        ' Boutons de validation
        _saveButton = New Button With {
            .Text = "Enregistrer",
            .Location = New Point(470, 515),
            .Size = New Size(100, 35),
            .BackColor = Color.FromArgb(52, 199, 89),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 10, FontStyle.Bold)
        }
        AddHandler _saveButton.Click, AddressOf SaveButton_Click
        Me.Controls.Add(_saveButton)

        _cancelButton = New Button With {
            .Text = "Fermer",
            .Location = New Point(580, 515),
            .Size = New Size(90, 35),
            .BackColor = Color.FromArgb(142, 142, 147),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 10)
        }
        AddHandler _cancelButton.Click, AddressOf CloseButton_Click
        Me.Controls.Add(_cancelButton)

        Me.AcceptButton = _saveButton
        Me.CancelButton = _cancelButton
    End Sub
#End Region

#Region "Chargement des catégories et propriétés"
    ''' <summary>
    ''' Charge toutes les catégories depuis le cache
    ''' </summary>
    Private Sub LoadCategoriesFromCache()
        Try
            Dim categories = _apiClient.GetCachedCategories()
            If categories Is Nothing OrElse categories.Count = 0 Then
                MessageBox.Show("Aucune catégorie trouvée dans le cache." & Environment.NewLine &
                              "Veuillez d'abord charger les appareils depuis le dashboard.",
                              "Information", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            ' Charger les propriétés de chaque catégorie
            For Each category In categories
                LoadCategoryProperties(category)
            Next

            ' Créer la liste d'affichage pour le ComboBox
            Dim categoryDisplayItems As New List(Of CategoryDisplayItem)
            For Each category In categories
                Dim deviceInfo = _deviceCategories.GetDeviceInfo(category)
                categoryDisplayItems.Add(New CategoryDisplayItem With {
                    .Category = category,
                    .DisplayName = $"{deviceInfo.icon} {deviceInfo.name} ({category})"
                })
            Next

            _categoryComboBox.DisplayMember = "DisplayName"
            _categoryComboBox.ValueMember = "Category"
            _categoryComboBox.DataSource = categoryDisplayItems

            If categoryDisplayItems.Count > 0 Then
                _categoryComboBox.SelectedIndex = 0
            End If

            Debug.WriteLine($"✓ {categories.Count} catégories chargées dans DisplayPreferencesForm")
        Catch ex As Exception
            Debug.WriteLine($"✗ Erreur LoadCategoriesFromCache: {ex.Message}")
            MessageBox.Show($"Erreur lors du chargement des catégories : {ex.Message}",
                          "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ''' <summary>
    ''' Charge les propriétés d'une catégorie depuis le cache
    ''' </summary>
    Private Sub LoadCategoryProperties(category As String)
        Try
            Dim specs = _apiClient.GetCachedSpecificationByCategory(category)
            If specs Is Nothing Then
                Debug.WriteLine($"⚠️ Aucune spécification trouvée pour la catégorie '{category}'")
                Return
            End If

            Dim properties As New List(Of PropertyInfo)

            ' Extraire les propriétés depuis "status"
            If specs("status") IsNot Nothing Then
                For Each statusItem In CType(specs("status"), JArray)
                    Dim code = statusItem("code")?.ToString()
                    Dim name = statusItem("name")?.ToString()
                    Dim type = statusItem("type")?.ToString()

                    If Not String.IsNullOrEmpty(code) Then
                        properties.Add(New PropertyInfo With {
                            .Code = code,
                            .Name = If(String.IsNullOrEmpty(name), code, name),
                            .Type = If(String.IsNullOrEmpty(type), "Unknown", type),
                            .Icon = GetPropertyIcon(code)
                        })
                    End If
                Next
            End If

            ' Extraire les propriétés depuis "functions"
            If specs("functions") IsNot Nothing Then
                For Each funcItem In CType(specs("functions"), JArray)
                    Dim code = funcItem("code")?.ToString()
                    Dim name = funcItem("name")?.ToString()
                    Dim type = funcItem("type")?.ToString()

                    If Not String.IsNullOrEmpty(code) Then
                        ' Éviter les doublons
                        If Not properties.Any(Function(p) p.Code = code) Then
                            properties.Add(New PropertyInfo With {
                                .Code = code,
                                .Name = If(String.IsNullOrEmpty(name), code, name),
                                .Type = If(String.IsNullOrEmpty(type), "Unknown", type),
                                .Icon = GetPropertyIcon(code)
                            })
                        End If
                    End If
                Next
            End If

            ' ✅ NOUVEAU: Ajouter les propriétés dynamiques découvertes dans les DeviceCard
            ' Cela inclut les sous-propriétés JSON (ex: phase_a.electricCurrent)
            If _dashboardForm IsNot Nothing Then
                Dim knownProperties = _dashboardForm.GetKnownPropertiesForCategory(category)
                For Each code In knownProperties
                    ' Vérifier si la propriété n'est pas déjà dans la liste
                    If Not properties.Any(Function(p) p.Code = code) Then
                        ' Utiliser le CategoryManager pour obtenir le nom d'affichage
                        Dim displayName = _categoryManager.GetDisplayName(category, code)

                        properties.Add(New PropertyInfo With {
                            .Code = code,
                            .Name = displayName,
                            .Type = "Dynamic",
                            .Icon = GetPropertyIcon(code)
                        })
                    End If
                Next
                Debug.WriteLine($"✓ Ajout de {knownProperties.Count} propriétés dynamiques découvertes")
            End If

            _categoriesProperties(category) = properties
            Debug.WriteLine($"✓ {properties.Count} propriétés chargées pour '{category}'")
        Catch ex As Exception
            Debug.WriteLine($"✗ Erreur LoadCategoryProperties pour '{category}': {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Retourne l'icône appropriée pour une propriété
    ''' </summary>
    Private Function GetPropertyIcon(code As String) As String
        ' Pour les sous-propriétés JSON (ex: phase_a.electricCurrent)
        If code.Contains(".") Then
            Dim subPropertyName = code.Substring(code.IndexOf(".") + 1).ToLower()
            If subPropertyName.Contains("current") OrElse subPropertyName = "electriccurrent" Then Return "🔌"
            If subPropertyName.Contains("voltage") Then Return "🔋"
            If subPropertyName.Contains("power") Then Return "⚡"
            If subPropertyName.Contains("temperature") Then Return "🌡️"
            If subPropertyName.Contains("humidity") Then Return "💧"
            If subPropertyName.Contains("energy") Then Return "📊"
        End If

        ' Propriétés normales
        If code.Contains("temperature") OrElse code = "va_temperature" Then Return "🌡️"
        If code.Contains("humidity") OrElse code = "humidity_value" Then Return "💧"
        If code.Contains("power") OrElse code.EndsWith("_P") Then Return "⚡"
        If code.Contains("current") OrElse code.EndsWith("_I") Then Return "🔌"
        If code.Contains("voltage") OrElse code.EndsWith("_V") Then Return "🔋"
        If code.Contains("battery") Then Return "🔋"
        If code.Contains("energy") OrElse code = "add_ele" OrElse code.Contains("forward") Then Return "📊"
        If code = "pir" Then Return "👁️"
        If code.Contains("switch") OrElse code = "doorcontact_state" Then Return "🎚️"
        Return "📋"
    End Function
#End Region

#Region "Gestion de l'UI"
    ''' <summary>
    ''' Classe interne pour l'affichage des catégories dans le ComboBox
    ''' </summary>
    Private Class CategoryDisplayItem
        Public Property Category As String
        Public Property DisplayName As String
    End Class

    ''' <summary>
    ''' Gère le changement de catégorie sélectionnée
    ''' </summary>
    Private Sub CategoryComboBox_SelectedIndexChanged(sender As Object, e As EventArgs)
        Try
            Dim selectedItem = TryCast(_categoryComboBox.SelectedItem, CategoryDisplayItem)
            If selectedItem Is Nothing Then Return

            Dim category = selectedItem.Category
            LoadPropertiesForCategory(category)
        Catch ex As Exception
            Debug.WriteLine($"✗ Erreur CategoryComboBox_SelectedIndexChanged: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Charge les propriétés d'une catégorie dans le CheckedListBox
    ''' </summary>
    Private Sub LoadPropertiesForCategory(category As String)
        _propertiesCheckedListBox.Items.Clear()

        If Not _categoriesProperties.ContainsKey(category) Then
            Return
        End If

        Dim properties = _categoriesProperties(category)
        Dim visibleProps = _preferencesManager.GetVisibleProperties(category)
        Dim propertyOrder = _preferencesManager.GetPropertyOrder(category)

        ' Si un ordre est défini, l'utiliser
        If propertyOrder IsNot Nothing AndAlso propertyOrder.Count > 0 Then
            ' Ajouter d'abord les propriétés dans l'ordre défini
            For Each code In propertyOrder
                Dim prop = properties.FirstOrDefault(Function(p) p.Code = code)
                If prop IsNot Nothing Then
                    Dim isVisible = visibleProps IsNot Nothing AndAlso visibleProps.Contains(code)
                    _propertiesCheckedListBox.Items.Add(prop, isVisible)
                End If
            Next

            ' Ajouter les propriétés qui ne sont pas dans l'ordre (nouvelles propriétés)
            For Each prop In properties
                If Not propertyOrder.Contains(prop.Code) Then
                    Dim isVisible = visibleProps IsNot Nothing AndAlso visibleProps.Contains(prop.Code)
                    _propertiesCheckedListBox.Items.Add(prop, isVisible)
                End If
            Next
        Else
            ' Pas d'ordre défini, ajouter toutes les propriétés
            For Each prop In properties
                Dim isVisible = If(visibleProps Is Nothing OrElse visibleProps.Count = 0, False, visibleProps.Contains(prop.Code))
                _propertiesCheckedListBox.Items.Add(prop, isVisible)
            Next
        End If

        UpdateCountLabel()
    End Sub

    ''' <summary>
    ''' Mise à jour du label de comptage
    ''' </summary>
    Private Sub UpdateCountLabel()
        Dim checkedCount = _propertiesCheckedListBox.CheckedItems.Count

        If checkedCount > 5 Then
            _countLabel.Text = $"⚠️ {checkedCount} propriété(s) sélectionnée(s) - Maximum recommandé : 5"
            _countLabel.ForeColor = Color.FromArgb(255, 59, 48)
        ElseIf checkedCount = 5 Then
            _countLabel.Text = $"{checkedCount} propriété(s) sélectionnée(s) - Maximum atteint"
            _countLabel.ForeColor = Color.FromArgb(255, 149, 0)
        Else
            _countLabel.Text = $"{checkedCount} propriété(s) sélectionnée(s) sur 5 maximum"
            _countLabel.ForeColor = Color.FromArgb(99, 99, 102)
        End If
    End Sub

    ''' <summary>
    ''' Gère le changement d'état d'une case à cocher
    ''' </summary>
    Private Sub PropertiesCheckedListBox_ItemCheck(sender As Object, e As ItemCheckEventArgs)
        ' Utiliser BeginInvoke pour que le compteur se mette à jour après le changement d'état
        Me.BeginInvoke(Sub() UpdateCountLabel())
    End Sub

    ''' <summary>
    ''' Gère la sélection d'un élément dans la liste
    ''' </summary>
    Private Sub PropertiesCheckedListBox_SelectedIndexChanged(sender As Object, e As EventArgs)
        Dim selectedIndex = _propertiesCheckedListBox.SelectedIndex
        _moveUpButton.Enabled = selectedIndex > 0
        _moveDownButton.Enabled = selectedIndex >= 0 AndAlso selectedIndex < _propertiesCheckedListBox.Items.Count - 1
    End Sub
#End Region

#Region "Boutons d'action"
    ''' <summary>
    ''' Déplace la propriété sélectionnée vers le haut
    ''' </summary>
    Private Sub MoveUpButton_Click(sender As Object, e As EventArgs)
        Dim selectedIndex = _propertiesCheckedListBox.SelectedIndex
        If selectedIndex <= 0 Then Return

        Dim item = _propertiesCheckedListBox.Items(selectedIndex)
        Dim isChecked = _propertiesCheckedListBox.GetItemChecked(selectedIndex)

        _propertiesCheckedListBox.Items.RemoveAt(selectedIndex)
        _propertiesCheckedListBox.Items.Insert(selectedIndex - 1, item)
        _propertiesCheckedListBox.SetItemChecked(selectedIndex - 1, isChecked)
        _propertiesCheckedListBox.SelectedIndex = selectedIndex - 1
    End Sub

    ''' <summary>
    ''' Déplace la propriété sélectionnée vers le bas
    ''' </summary>
    Private Sub MoveDownButton_Click(sender As Object, e As EventArgs)
        Dim selectedIndex = _propertiesCheckedListBox.SelectedIndex
        If selectedIndex < 0 OrElse selectedIndex >= _propertiesCheckedListBox.Items.Count - 1 Then Return

        Dim item = _propertiesCheckedListBox.Items(selectedIndex)
        Dim isChecked = _propertiesCheckedListBox.GetItemChecked(selectedIndex)

        _propertiesCheckedListBox.Items.RemoveAt(selectedIndex)
        _propertiesCheckedListBox.Items.Insert(selectedIndex + 1, item)
        _propertiesCheckedListBox.SetItemChecked(selectedIndex + 1, isChecked)
        _propertiesCheckedListBox.SelectedIndex = selectedIndex + 1
    End Sub

    ''' <summary>
    ''' Sélectionne toutes les propriétés
    ''' </summary>
    Private Sub SelectAllButton_Click(sender As Object, e As EventArgs)
        For i = 0 To _propertiesCheckedListBox.Items.Count - 1
            _propertiesCheckedListBox.SetItemChecked(i, True)
        Next
        UpdateCountLabel()
    End Sub

    ''' <summary>
    ''' Désélectionne toutes les propriétés
    ''' </summary>
    Private Sub DeselectAllButton_Click(sender As Object, e As EventArgs)
        For i = 0 To _propertiesCheckedListBox.Items.Count - 1
            _propertiesCheckedListBox.SetItemChecked(i, False)
        Next
        UpdateCountLabel()
    End Sub

    ''' <summary>
    ''' Réinitialise les préférences pour la catégorie actuelle
    ''' </summary>
    Private Sub ResetButton_Click(sender As Object, e As EventArgs)
        Try
            Dim selectedItem = TryCast(_categoryComboBox.SelectedItem, CategoryDisplayItem)
            If selectedItem Is Nothing Then Return

            Dim result = MessageBox.Show(
                "Êtes-vous sûr de vouloir réinitialiser les préférences pour cette catégorie ?" & Environment.NewLine &
                "Toutes les propriétés seront affichées par défaut.",
                "Confirmation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question)

            If result = DialogResult.Yes Then
                _preferencesManager.ResetCategory(selectedItem.Category)
                LoadPropertiesForCategory(selectedItem.Category)
            End If
        Catch ex As Exception
            MessageBox.Show($"Erreur lors de la réinitialisation : {ex.Message}",
                          "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ''' <summary>
    ''' Sauvegarde les préférences et rafraîchit immédiatement les tuiles
    ''' </summary>
    Private Sub SaveButton_Click(sender As Object, e As EventArgs)
        ' Désactiver les contrôles pendant le traitement (feedback visuel)
        _saveButton.Enabled = False
        _categoryComboBox.Enabled = False
        _propertiesCheckedListBox.Enabled = False
        Me.Cursor = Cursors.WaitCursor

        Try
            ' Récupérer la catégorie actuellement sélectionnée
            Dim selectedItem = TryCast(_categoryComboBox.SelectedItem, CategoryDisplayItem)
            If selectedItem Is Nothing Then
                MessageBox.Show("Aucune catégorie sélectionnée.", "Attention", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim category = selectedItem.Category

            ' Récupérer les propriétés visibles
            Dim visibleProps As New List(Of String)
            For i = 0 To _propertiesCheckedListBox.Items.Count - 1
                If _propertiesCheckedListBox.GetItemChecked(i) Then
                    Dim prop = TryCast(_propertiesCheckedListBox.Items(i), PropertyInfo)
                    If prop IsNot Nothing Then
                        visibleProps.Add(prop.Code)
                    End If
                End If
            Next

            ' Récupérer l'ordre complet
            Dim propertyOrder As New List(Of String)
            For i = 0 To _propertiesCheckedListBox.Items.Count - 1
                Dim prop = TryCast(_propertiesCheckedListBox.Items(i), PropertyInfo)
                If prop IsNot Nothing Then
                    propertyOrder.Add(prop.Code)
                End If
            Next

            ' Sauvegarder les préférences
            _preferencesManager.SetVisibleProperties(category, visibleProps)
            _preferencesManager.SetPropertyOrder(category, propertyOrder)
            _preferencesManager.SavePreferences()

            ' Marquer que des changements ont été sauvegardés
            _changesSaved = True

            ' ✅ NOUVEAU: Rafraîchir immédiatement les tuiles de cette catégorie
            If _dashboardForm IsNot Nothing Then
                _dashboardForm.RefreshDeviceCardsByCategory(category)
            End If

            MessageBox.Show($"Préférences enregistrées avec succès pour la catégorie '{category}' !" & Environment.NewLine & Environment.NewLine &
                          "Les tuiles ont été rafraîchies. Vous pouvez continuer à modifier d'autres catégories.",
                          "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information)

        Catch ex As Exception
            MessageBox.Show($"Erreur lors de la sauvegarde : {ex.Message}",
                          "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            ' Réactiver les contrôles
            _saveButton.Enabled = True
            _categoryComboBox.Enabled = True
            _propertiesCheckedListBox.Enabled = True
            Me.Cursor = Cursors.Default
        End Try
    End Sub

    ''' <summary>
    ''' Ferme le formulaire (le rafraîchissement a déjà été fait après chaque sauvegarde)
    ''' </summary>
    Private Sub CloseButton_Click(sender As Object, e As EventArgs)
        ' Les tuiles ont déjà été rafraîchies après chaque sauvegarde
        ' Pas besoin de rafraîchir à nouveau à la fermeture
        Me.DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub
#End Region

End Class
