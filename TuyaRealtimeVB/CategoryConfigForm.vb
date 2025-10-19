Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms
Imports Newtonsoft.Json.Linq

Public Class CategoryConfigForm
    Inherits Form

    Private ReadOnly _categoryManager As TuyaCategoryManager
    Private _treeView As TreeView
    Private _editorPanel As Panel
    Private _currentEditingNode As TreeNode
    Private _isDirty As Boolean = False
    Private _wasSaved As Boolean = False  ' ✅ NOUVEAU

    ' ✅ NOUVELLE PROPRIÉTÉ
    Public ReadOnly Property WasSaved As Boolean
        Get
            Return _wasSaved
        End Get
    End Property

    Public Sub New()
        _categoryManager = TuyaCategoryManager.Instance
        InitializeComponent()
        LoadConfiguration()
    End Sub

    Private Sub InitializeComponent()
        Me.Text = "Configuration des catégories Tuya - Éditeur visuel"
        Me.Size = New Size(1400, 900)
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.BackColor = Color.FromArgb(240, 240, 240)

        ' Vérifier si le fichier existe
        If Not File.Exists(_categoryManager.GetConfigPath()) Then
            If MessageBox.Show("Créer un fichier avec des exemples ?", "Config manquante",
                             MessageBoxButtons.YesNo) = DialogResult.Yes Then
                CreateSampleConfiguration()
            End If
        End If

        ' === HEADER ===
        Dim header = CreateHeader()

        ' === TOOLBAR ===
        Dim toolbar = CreateToolbar()

        ' === SPLIT CONTAINER ===
        Dim split = New SplitContainer With {
            .Dock = DockStyle.Fill,
            .Orientation = Orientation.Vertical,
            .SplitterDistance = 350,
            .BackColor = Color.Gray
        }

        ' GAUCHE: TreeView
        split.Panel1.Controls.Add(CreateTreeViewPanel())

        ' DROITE: Éditeur
        _editorPanel = CreateEditorPanel()
        split.Panel2.Controls.Add(_editorPanel)

        ' === ASSEMBLAGE ===
        Me.Controls.Add(split)
        Me.Controls.Add(toolbar)
        Me.Controls.Add(header)
    End Sub

    Private Function CreateHeader() As Panel
        Dim header = New Panel With {
            .Dock = DockStyle.Top,
            .Height = 70,
            .BackColor = Color.FromArgb(45, 45, 48)
        }

        Dim title = New Label With {
            .Text = "🏷️ Configuration des catégories d'appareils",
            .Font = New Font("Segoe UI", 16, FontStyle.Bold),
            .ForeColor = Color.White,
            .Location = New Point(20, 15),
            .AutoSize = True
        }

        Dim subtitle = New Label With {
            .Text = "Éditeur visuel - Modifiez facilement les paramètres de chaque catégorie",
            .Font = New Font("Segoe UI", 9),
            .ForeColor = Color.FromArgb(180, 180, 180),
            .Location = New Point(20, 45),
            .AutoSize = True
        }

        header.Controls.AddRange({title, subtitle})
        Return header
    End Function

    Private Function CreateToolbar() As Panel
        Dim toolbar = New Panel With {
            .Dock = DockStyle.Top,
            .Height = 60,
            .BackColor = Color.White,
            .Padding = New Padding(15, 10, 15, 10)
        }

        Dim btnSave = CreateToolbarButton("💾 Enregistrer", Color.FromArgb(52, 199, 89), AddressOf SaveButton_Click)
        btnSave.Location = New Point(15, 10)

        Dim btnReload = CreateToolbarButton("🔄 Recharger", Color.FromArgb(0, 122, 255), AddressOf ReloadButton_Click)
        btnReload.Location = New Point(170, 10)

        Dim btnAddCategory = CreateToolbarButton("➕ Catégorie", Color.FromArgb(88, 86, 214), AddressOf AddCategoryButton_Click)
        btnAddCategory.Location = New Point(325, 10)

        Dim btnDelete = CreateToolbarButton("🗑️ Supprimer", Color.FromArgb(255, 59, 48), AddressOf DeleteButton_Click)
        btnDelete.Location = New Point(480, 10)

        Dim btnOpen = CreateToolbarButton("📁 Ouvrir", Color.FromArgb(142, 142, 147), AddressOf OpenButton_Click)
        btnOpen.Location = New Point(635, 10)

        Dim btnSample = CreateToolbarButton("✨ Exemples", Color.FromArgb(175, 82, 222), AddressOf SampleButton_Click)
        btnSample.Location = New Point(790, 10)

        toolbar.Controls.AddRange({btnSave, btnReload, btnAddCategory, btnDelete, btnOpen, btnSample})
        Return toolbar
    End Function

    Private Function CreateToolbarButton(text As String, backColor As Color, handler As EventHandler) As Button
        Dim btn = New Button With {
            .Text = text,
            .Width = 145,
            .Height = 40,
            .BackColor = backColor,
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 10, FontStyle.Bold),
            .Cursor = Cursors.Hand
        }
        btn.FlatAppearance.BorderSize = 0
        AddHandler btn.Click, handler
        Return btn
    End Function

    Private Function CreateTreeViewPanel() As Panel
        Dim panel = New Panel With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(10),
            .BackColor = Color.White
        }

        Dim label = New Label With {
            .Text = "📂 Structure de la configuration",
            .Dock = DockStyle.Top,
            .Height = 35,
            .Font = New Font("Segoe UI", 11, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .BackColor = Color.FromArgb(230, 230, 230),
            .Padding = New Padding(10, 0, 0, 0)
        }

        _treeView = New TreeView With {
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 10),
            .BorderStyle = BorderStyle.None,
            .ShowLines = True,
            .ShowPlusMinus = True,
            .ShowRootLines = True,
            .FullRowSelect = True,
            .HideSelection = False
        }
        AddHandler _treeView.AfterSelect, AddressOf TreeView_AfterSelect

        panel.Controls.Add(_treeView)
        panel.Controls.Add(label)
        Return panel
    End Function

    Private Function CreateEditorPanel() As Panel
        Dim panel = New Panel With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(10),
            .BackColor = Color.White,
            .AutoScroll = True
        }

        Dim welcomeLabel = New Label With {
            .Text = "👈 Sélectionnez un élément dans l'arborescence pour l'éditer",
            .Font = New Font("Segoe UI", 12),
            .ForeColor = Color.FromArgb(142, 142, 147),
            .AutoSize = True,
            .Location = New Point(50, 100)
        }
        panel.Controls.Add(welcomeLabel)

        Return panel
    End Function

    Private Sub LoadConfiguration()
        _treeView.Nodes.Clear()

        Try
            Dim config = _categoryManager.GetConfiguration()
            Dim categoriesNode = New TreeNode("📦 Catégories") With {.Tag = "root"}

            Dim categories = config("categories")
            If categories IsNot Nothing Then
                For Each category As JProperty In categories.Children(Of JProperty)()
                    Dim categoryCode As String = category.Name
                    Dim categoryData As JObject = CType(category.Value, JObject)
                    Dim categoryNameToken = categoryData("name")
                    Dim categoryName As String = If(categoryNameToken IsNot Nothing, categoryNameToken.ToString(), categoryCode)

                    Dim catNode = New TreeNode(String.Format("{0} - {1}", categoryCode, categoryName)) With {
                        .Tag = New With {.Type = "category", .Code = categoryCode, .Data = categoryData}
                    }

                    ' Ajouter les propriétés
                    Dim properties = categoryData("properties")
                    If properties IsNot Nothing Then
                        For Each prop As JProperty In properties.Children(Of JProperty)()
                            Dim propCode As String = prop.Name
                            Dim propData As JObject = CType(prop.Value, JObject)
                            Dim displayNameToken = propData("displayName")
                            Dim displayName As String = If(displayNameToken IsNot Nothing, displayNameToken.ToString(), propCode)

                            Dim propNode = New TreeNode(String.Format("🔧 {0} - {1}", propCode, displayName)) With {
                                .Tag = New With {.Type = "property", .CategoryCode = categoryCode, .Code = propCode, .Data = propData}
                            }
                            catNode.Nodes.Add(propNode)
                        Next
                    End If

                    categoriesNode.Nodes.Add(catNode)
                Next
            End If

            _treeView.Nodes.Add(categoriesNode)
            categoriesNode.Expand()

            _isDirty = False
        Catch ex As Exception
            MessageBox.Show(String.Format("Erreur chargement : {0}", ex.Message), "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub TreeView_AfterSelect(sender As Object, e As TreeViewEventArgs)
        _currentEditingNode = e.Node
        ShowEditor(e.Node)
    End Sub

    Private Sub ShowEditor(node As TreeNode)
        _editorPanel.Controls.Clear()
        _editorPanel.AutoScroll = True

        If node.Tag Is Nothing Then Return

        Dim tagTypeObj = GetPropertyValue(node.Tag, "Type")
        If tagTypeObj Is Nothing Then Return

        Dim tagType As String = tagTypeObj.ToString()

        Select Case tagType
            Case "category"
                ShowCategoryEditor(node)
            Case "property"
                ShowPropertyEditor(node)
            Case Else
                ShowWelcomeMessage()
        End Select
    End Sub

    Private Sub ShowCategoryEditor(node As TreeNode)
        Dim categoryCodeObj = GetPropertyValue(node.Tag, "Code")
        Dim categoryCode As String = If(categoryCodeObj IsNot Nothing, categoryCodeObj.ToString(), "")
        Dim categoryData As JObject = CType(GetPropertyValue(node.Tag, "Data"), JObject)

        Dim y As Integer = 20

        ' Titre
        AddSectionTitle(_editorPanel, "📦 Édition de la catégorie", y)
        y += 40

        ' Code de la catégorie (lecture seule)
        AddLabel(_editorPanel, "Code de la catégorie:", y)
        Dim txtCode = AddTextBox(_editorPanel, categoryCode, y + 25, True)
        y += 70

        ' Nom de la catégorie
        AddLabel(_editorPanel, "Nom de la catégorie:", y)
        Dim nameToken = categoryData("name")
        Dim nameValue As String = If(nameToken IsNot Nothing, nameToken.ToString(), "")
        Dim txtName = AddTextBox(_editorPanel, nameValue, y + 25, False)
        txtName.Tag = New With {.Field = "name", .CategoryData = categoryData}
        AddHandler txtName.TextChanged, AddressOf CategoryField_Changed
        y += 70

        ' Bouton Ajouter propriété
        Dim btnAddProp = New Button With {
            .Text = "➕ Ajouter une propriété",
            .Location = New Point(20, y),
            .Size = New Size(200, 35),
            .BackColor = Color.FromArgb(0, 122, 255),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 10, FontStyle.Bold),
            .Cursor = Cursors.Hand,
            .Tag = New With {.CategoryCode = categoryCode, .Node = node}
        }
        btnAddProp.FlatAppearance.BorderSize = 0
        AddHandler btnAddProp.Click, AddressOf AddPropertyButton_Click
        _editorPanel.Controls.Add(btnAddProp)
    End Sub

    Private Sub ShowPropertyEditor(node As TreeNode)
        Dim categoryCodeObj = GetPropertyValue(node.Tag, "CategoryCode")
        Dim categoryCode As String = If(categoryCodeObj IsNot Nothing, categoryCodeObj.ToString(), "")

        Dim propCodeObj = GetPropertyValue(node.Tag, "Code")
        Dim propCode As String = If(propCodeObj IsNot Nothing, propCodeObj.ToString(), "")

        Dim propData As JObject = CType(GetPropertyValue(node.Tag, "Data"), JObject)

        Dim y As Integer = 20

        ' Titre
        AddSectionTitle(_editorPanel, String.Format("🔧 Propriété: {0}", propCode), y)
        y += 40

        ' DisplayName
        AddLabel(_editorPanel, "Nom affiché:", y)
        Dim displayNameToken = propData("displayName")
        Dim displayNameValue As String = If(displayNameToken IsNot Nothing, displayNameToken.ToString(), "")
        Dim txtDisplayName = AddTextBox(_editorPanel, displayNameValue, y + 25, False)
        txtDisplayName.Tag = New With {.Field = "displayName", .PropData = propData}
        AddHandler txtDisplayName.TextChanged, AddressOf PropertyField_Changed
        y += 70

        ' Icon avec bouton de sélection
        AddLabel(_editorPanel, "Icône (emoji):", y)

        Dim iconPanel = New Panel With {
    .Location = New Point(20, y + 25),
    .Size = New Size(_editorPanel.Width - 60, 35),
    .BackColor = Color.Transparent
}

        Dim iconToken = propData("icon")
        Dim iconValue As String = If(iconToken IsNot Nothing, iconToken.ToString(), "")
        Dim txtIcon = New TextBox With {
    .Text = iconValue,
    .Location = New Point(0, 0),
    .Size = New Size(iconPanel.Width - 110, 30),
    .Font = New Font("Segoe UI", 10),
    .Tag = New With {.Field = "icon", .PropData = propData}
}
        AddHandler txtIcon.TextChanged, AddressOf PropertyField_Changed

        Dim btnPickIcon = New Button With {
    .Text = "🎨 Choisir",
    .Location = New Point(iconPanel.Width - 100, 0),
    .Size = New Size(100, 30),
    .BackColor = Color.FromArgb(0, 122, 255),
    .ForeColor = Color.White,
    .FlatStyle = FlatStyle.Flat,
    .Font = New Font("Segoe UI", 9, FontStyle.Bold),
    .Cursor = Cursors.Hand,
    .Tag = txtIcon
}
        btnPickIcon.FlatAppearance.BorderSize = 0

        AddHandler btnPickIcon.Click, Sub(s, e)
                                          Using iconPicker As New IconPickerForm(txtIcon.Text)
                                              If iconPicker.ShowDialog() = DialogResult.OK Then
                                                  txtIcon.Text = iconPicker.SelectedIcon
                                              End If
                                          End Using
                                      End Sub

        iconPanel.Controls.AddRange({txtIcon, btnPickIcon})
        _editorPanel.Controls.Add(iconPanel)

        y += 70

        ' Conversion
        AddLabel(_editorPanel, "Type de conversion:", y)
        Dim conversionToken = propData("conversion")
        Dim conversionValue As String = If(conversionToken IsNot Nothing, conversionToken.ToString(), "simple")
        Dim cmbConversion = AddComboBox(_editorPanel, New String() {"none", "simple", "divide", "multiply"},
                                        conversionValue, y + 25)
        cmbConversion.Tag = New With {.Field = "conversion", .PropData = propData}
        AddHandler cmbConversion.SelectedIndexChanged, AddressOf PropertyField_Changed
        y += 70

        ' Divisor (si conversion = divide)
        Dim currentConversion As String = If(conversionToken IsNot Nothing, conversionToken.ToString(), "")
        If currentConversion = "divide" Then
            AddLabel(_editorPanel, "Diviseur:", y)
            Dim divisorToken = propData("divisor")
            Dim divisorValue As Decimal = If(divisorToken IsNot Nothing, divisorToken.ToObject(Of Decimal)(), 1D)
            Dim numDivisor = AddNumericUpDown(_editorPanel, divisorValue, y + 25, 0, 10000)
            numDivisor.Tag = New With {.Field = "divisor", .PropData = propData}
            AddHandler numDivisor.ValueChanged, AddressOf PropertyField_Changed
            y += 70
        End If

        ' Unit
        AddLabel(_editorPanel, "Unité:", y)
        Dim unitToken = propData("unit")
        Dim unitValue As String = If(unitToken IsNot Nothing, unitToken.ToString(), "")
        Dim txtUnit = AddTextBox(_editorPanel, unitValue, y + 25, False)
        txtUnit.Tag = New With {.Field = "unit", .PropData = propData}
        AddHandler txtUnit.TextChanged, AddressOf PropertyField_Changed
        y += 70

        ' Decimals
        AddLabel(_editorPanel, "Nombre de décimales:", y)
        Dim decimalsToken = propData("decimals")
        Dim decimalsValue As Decimal = If(decimalsToken IsNot Nothing, decimalsToken.ToObject(Of Decimal)(), 0D)
        Dim numDecimals = AddNumericUpDown(_editorPanel, decimalsValue, y + 25, 0, 5)
        numDecimals.Tag = New With {.Field = "decimals", .PropData = propData}
        AddHandler numDecimals.ValueChanged, AddressOf PropertyField_Changed
        y += 70

        ' Note d'info
        Dim lblInfo = New Label With {
            .Text = "💡 Les modifications sont appliquées automatiquement. N'oubliez pas de sauvegarder !",
            .Location = New Point(20, y),
            .Size = New Size(_editorPanel.Width - 60, 40),
            .Font = New Font("Segoe UI", 9),
            .ForeColor = Color.FromArgb(0, 122, 255),
            .BackColor = Color.FromArgb(230, 245, 255),
            .Padding = New Padding(10),
            .AutoSize = False
        }
        _editorPanel.Controls.Add(lblInfo)
    End Sub

    Private Sub ShowWelcomeMessage()
        Dim label = New Label With {
            .Text = "👈 Sélectionnez un élément dans l'arborescence pour l'éditer",
            .Font = New Font("Segoe UI", 12),
            .ForeColor = Color.FromArgb(142, 142, 147),
            .AutoSize = True,
            .Location = New Point(50, 100)
        }
        _editorPanel.Controls.Add(label)
    End Sub

    ' === HELPERS POUR CRÉER DES CONTRÔLES ===

    Private Sub AddSectionTitle(panel As Panel, text As String, y As Integer)
        Dim label = New Label With {
            .Text = text,
            .Location = New Point(20, y),
            .Size = New Size(panel.Width - 40, 30),
            .Font = New Font("Segoe UI", 14, FontStyle.Bold),
            .ForeColor = Color.FromArgb(45, 45, 48)
        }
        panel.Controls.Add(label)
    End Sub

    Private Sub AddLabel(panel As Panel, text As String, y As Integer)
        Dim label = New Label With {
            .Text = text,
            .Location = New Point(20, y),
            .Size = New Size(panel.Width - 40, 20),
            .Font = New Font("Segoe UI", 10, FontStyle.Bold),
            .ForeColor = Color.FromArgb(99, 99, 102)
        }
        panel.Controls.Add(label)
    End Sub

    Private Function AddTextBox(panel As Panel, text As String, y As Integer, isReadOnly As Boolean) As TextBox
        Dim txt = New TextBox With {
            .Text = text,
            .Location = New Point(20, y),
            .Size = New Size(panel.Width - 60, 30),
            .Font = New Font("Segoe UI", 10),
            .ReadOnly = isReadOnly,
            .BackColor = If(isReadOnly, Color.FromArgb(240, 240, 240), Color.White)
        }
        panel.Controls.Add(txt)
        Return txt
    End Function

    Private Function AddComboBox(panel As Panel, items As String(), selectedItem As String, y As Integer) As ComboBox
        Dim cmb = New ComboBox With {
            .Location = New Point(20, y),
            .Size = New Size(panel.Width - 60, 30),
            .Font = New Font("Segoe UI", 10),
            .DropDownStyle = ComboBoxStyle.DropDownList
        }
        cmb.Items.AddRange(items.Cast(Of Object).ToArray())
        If Not String.IsNullOrEmpty(selectedItem) AndAlso cmb.Items.Contains(selectedItem) Then
            cmb.SelectedItem = selectedItem
        End If
        panel.Controls.Add(cmb)
        Return cmb
    End Function

    Private Function AddNumericUpDown(panel As Panel, value As Decimal, y As Integer,
                                      minimum As Decimal, maximum As Decimal) As NumericUpDown
        Dim num = New NumericUpDown With {
            .Location = New Point(20, y),
            .Size = New Size(panel.Width - 60, 30),
            .Font = New Font("Segoe UI", 10),
            .Minimum = minimum,
            .Maximum = maximum,
            .Value = value
        }
        panel.Controls.Add(num)
        Return num
    End Function

    ' === GESTION DES CHANGEMENTS ===

    Private Sub CategoryField_Changed(sender As Object, e As EventArgs)
        Dim control As Control = CType(sender, Control)
        Dim fieldObj = GetPropertyValue(control.Tag, "Field")
        If fieldObj Is Nothing Then Return

        Dim field As String = fieldObj.ToString()
        Dim categoryData As JObject = CType(GetPropertyValue(control.Tag, "CategoryData"), JObject)

        If categoryData IsNot Nothing AndAlso Not String.IsNullOrEmpty(field) Then
            categoryData(field) = CType(control, TextBox).Text
            _isDirty = True
        End If
    End Sub

    Private Sub PropertyField_Changed(sender As Object, e As EventArgs)
        Dim control As Control = CType(sender, Control)
        Dim fieldObj = GetPropertyValue(control.Tag, "Field")
        If fieldObj Is Nothing Then Return

        Dim field As String = fieldObj.ToString()
        Dim propData As JObject = CType(GetPropertyValue(control.Tag, "PropData"), JObject)

        If propData IsNot Nothing AndAlso Not String.IsNullOrEmpty(field) Then
            If TypeOf control Is TextBox Then
                propData(field) = CType(control, TextBox).Text
            ElseIf TypeOf control Is ComboBox Then
                Dim cmbValue = CType(control, ComboBox).SelectedItem
                If cmbValue IsNot Nothing Then
                    propData(field) = cmbValue.ToString()
                End If
            ElseIf TypeOf control Is NumericUpDown Then
                propData(field) = CType(control, NumericUpDown).Value
            End If
            _isDirty = True
        End If
    End Sub

    ' === BOUTONS ACTIONS ===

    Private Sub AddCategoryButton_Click(sender As Object, e As EventArgs)
        Dim code As String = InputBox("Code de la nouvelle catégorie (ex: 'cz', 'kg'):", "Nouvelle catégorie")
        If String.IsNullOrWhiteSpace(code) Then Return

        Dim name As String = InputBox(String.Format("Nom de la catégorie '{0}':", code), "Nouvelle catégorie")
        If String.IsNullOrWhiteSpace(name) Then Return

        Try
            Dim config = _categoryManager.GetConfiguration()
            Dim categories = config("categories")

            If categories(code) IsNot Nothing Then
                MessageBox.Show("Cette catégorie existe déjà !", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim newCategory = New JObject From {
                {"name", name},
                {"properties", New JObject()}
            }

            categories(code) = newCategory
            _isDirty = True

            LoadConfiguration()
            MessageBox.Show(String.Format("Catégorie '{0}' créée !", code), "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show(String.Format("Erreur : {0}", ex.Message), "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub AddPropertyButton_Click(sender As Object, e As EventArgs)
        Dim btn As Button = CType(sender, Button)
        Dim categoryCodeObj = GetPropertyValue(btn.Tag, "CategoryCode")
        If categoryCodeObj Is Nothing Then Return

        Dim categoryCode As String = categoryCodeObj.ToString()
        Dim node As TreeNode = CType(GetPropertyValue(btn.Tag, "Node"), TreeNode)

        Dim propCode As String = InputBox("Code de la nouvelle propriété (ex: 'cur_power'):", "Nouvelle propriété")
        If String.IsNullOrWhiteSpace(propCode) Then Return

        Dim displayName As String = InputBox(String.Format("Nom affiché pour '{0}':", propCode), "Nouvelle propriété")
        If String.IsNullOrWhiteSpace(displayName) Then Return

        Try
            Dim config = _categoryManager.GetConfiguration()
            Dim category = config("categories")(categoryCode)
            Dim properties = category("properties")

            If properties(propCode) IsNot Nothing Then
                MessageBox.Show("Cette propriété existe déjà !", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim newProperty = New JObject From {
                {"displayName", displayName},
                {"icon", "📊"},
                {"conversion", "simple"},
                {"unit", ""},
                {"decimals", 0}
            }

            properties(propCode) = newProperty
            _isDirty = True

            LoadConfiguration()
            MessageBox.Show(String.Format("Propriété '{0}' créée !", propCode), "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show(String.Format("Erreur : {0}", ex.Message), "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub DeleteButton_Click(sender As Object, e As EventArgs)
        If _currentEditingNode Is Nothing OrElse _currentEditingNode.Tag Is Nothing Then
            MessageBox.Show("Sélectionnez un élément à supprimer", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim tagTypeObj = GetPropertyValue(_currentEditingNode.Tag, "Type")
        If tagTypeObj Is Nothing Then Return

        Dim tagType As String = tagTypeObj.ToString()

        If tagType = "category" Then
            DeleteCategory()
        ElseIf tagType = "property" Then
            DeleteProperty()
        End If
    End Sub

    Private Sub DeleteCategory()
        Dim categoryCodeObj = GetPropertyValue(_currentEditingNode.Tag, "Code")
        If categoryCodeObj Is Nothing Then Return

        Dim categoryCode As String = categoryCodeObj.ToString()

        Dim result = MessageBox.Show(String.Format("Supprimer la catégorie '{0}' et toutes ses propriétés ?", categoryCode),
                                     "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)

        If result = DialogResult.Yes Then
            Try
                Dim config = _categoryManager.GetConfiguration()
                CType(config("categories"), JObject).Remove(categoryCode)
                _isDirty = True
                LoadConfiguration()
                MessageBox.Show("Catégorie supprimée", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Catch ex As Exception
                MessageBox.Show(String.Format("Erreur : {0}", ex.Message), "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End If
    End Sub

    Private Sub DeleteProperty()
        Dim categoryCodeObj = GetPropertyValue(_currentEditingNode.Tag, "CategoryCode")
        Dim propCodeObj = GetPropertyValue(_currentEditingNode.Tag, "Code")

        If categoryCodeObj Is Nothing OrElse propCodeObj Is Nothing Then Return

        Dim categoryCode As String = categoryCodeObj.ToString()
        Dim propCode As String = propCodeObj.ToString()

        Dim result = MessageBox.Show(String.Format("Supprimer la propriété '{0}' ?", propCode),
                                     "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)

        If result = DialogResult.Yes Then
            Try
                Dim config = _categoryManager.GetConfiguration()
                Dim properties = config("categories")(categoryCode)("properties")
                CType(properties, JObject).Remove(propCode)
                _isDirty = True
                LoadConfiguration()
                MessageBox.Show("Propriété supprimée", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Catch ex As Exception
                MessageBox.Show(String.Format("Erreur : {0}", ex.Message), "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End If
    End Sub

    Private Sub SaveButton_Click(sender As Object, e As EventArgs)
        Try
            _categoryManager.UpdateConfiguration(_categoryManager.GetConfiguration())
            _isDirty = False
            MessageBox.Show("Configuration sauvegardée !", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show(String.Format("Erreur : {0}", ex.Message), "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ReloadButton_Click(sender As Object, e As EventArgs)
        If _isDirty Then
            Dim result = MessageBox.Show("Des modifications non sauvegardées seront perdues. Continuer ?",
                                        "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
            If result = DialogResult.No Then Return
        End If

        _categoryManager.LoadConfiguration()
        LoadConfiguration()
        MessageBox.Show("Configuration rechargée", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Private Sub OpenButton_Click(sender As Object, e As EventArgs)
        Try
            If File.Exists(_categoryManager.GetConfigPath()) Then
                Process.Start("notepad.exe", _categoryManager.GetConfigPath())
            End If
        Catch ex As Exception
            MessageBox.Show(String.Format("Erreur : {0}", ex.Message), "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub SampleButton_Click(sender As Object, e As EventArgs)
        If MessageBox.Show("Écraser la config actuelle avec des exemples ?", "Confirmation",
                         MessageBoxButtons.YesNo) = DialogResult.Yes Then
            CreateSampleConfiguration()
        End If
    End Sub

    Private Sub CreateSampleConfiguration()
        Try
            Dim config = JObject.Parse("{""version"":""1.0"",""categories"":{""default"":{""name"":""Appareil générique"",""properties"":{""*"":{""conversion"":""none"",""decimals"":2}}},""cz"":{""name"":""Compteur électrique"",""properties"":{""cur_power"":{""displayName"":""Puissance"",""icon"":""⚡"",""conversion"":""divide"",""divisor"":10,""unit"":""W"",""decimals"":0},""cur_voltage"":{""displayName"":""Tension"",""icon"":""🔋"",""conversion"":""divide"",""divisor"":10,""unit"":""V"",""decimals"":1},""cur_current"":{""displayName"":""Intensité"",""icon"":""🔌"",""conversion"":""divide"",""divisor"":1000,""unit"":""A"",""decimals"":3}}},""wsdcg"":{""name"":""Capteur température/humidité"",""properties"":{""va_temperature"":{""displayName"":""Température"",""icon"":""🌡️"",""conversion"":""divide"",""divisor"":10,""unit"":""°C"",""decimals"":1},""humidity_value"":{""displayName"":""Humidité"",""icon"":""💧"",""conversion"":""simple"",""unit"":""%"",""decimals"":0}}}}}")

            _categoryManager.UpdateConfiguration(config)
            LoadConfiguration()
            MessageBox.Show("Configuration d'exemple créée !", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show(String.Format("Erreur : {0}", ex.Message), "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' === HELPER ===
    Private Function GetPropertyValue(obj As Object, propertyName As String) As Object
        If obj Is Nothing Then Return Nothing
        Dim prop = obj.GetType().GetProperty(propertyName)
        Return If(prop IsNot Nothing, prop.GetValue(obj, Nothing), Nothing)
    End Function

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        If _isDirty Then
            Dim result = MessageBox.Show("Des modifications non sauvegardées seront perdues. Quitter quand même ?",
                                        "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
            If result = DialogResult.No Then
                e.Cancel = True
                Return
            End If
        End If
        MyBase.OnFormClosing(e)
    End Sub
End Class