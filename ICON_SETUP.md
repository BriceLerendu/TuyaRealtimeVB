# 🎨 Configuration de l'icône de l'application

## ⚡ Méthode RAPIDE (Recommandée)

### Double-cliquez sur `create_icon.bat`

C'est tout! Le script va:
1. ✅ Vérifier Python et installer Pillow si besoin
2. ✅ Générer `app_icon.ico` automatiquement
3. ✅ Créer `app_icon_preview.png` pour voir le résultat
4. ✅ Ouvrir l'aperçu automatiquement

Ensuite:
1. Fermez Visual Studio si ouvert
2. Rouvrez le projet
3. Recompilez (Build → Rebuild Solution)
4. Votre .exe aura la nouvelle icône! 🎉

---

## 🔧 Méthode MANUELLE (Alternative)

### Étape 1: Générer l'icône

#### Option A - Script Python (recommandé)
```bash
# Installer Pillow si nécessaire
pip install Pillow

# Générer l'icône
python generate_icon.py
```

#### Option B - Navigateur HTML
1. Ouvrez le fichier `icon_generator.html` dans votre navigateur web
2. Vous verrez un aperçu de l'icône en 3 tailles (256x256, 64x64, 32x32)
3. Téléchargez les 3 tailles en PNG

### Étape 2: Convertir en .ico (si Option B)

1. Allez sur https://convertio.co/fr/png-ico/ ou https://icoconvert.com/
2. Uploadez les 3 PNG (256, 64, 32)
3. Téléchargez le fichier .ico généré
4. Renommez-le en `app_icon.ico`

### Étape 3: Ajouter l'icône au projet VB.NET

### 3.1 Copier l'icône
Placez `app_icon.ico` dans le dossier racine du projet:
```
TuyaRealtimeVB/
  ├── TuyaRealtimeVB/
  ├── app_icon.ico  ← Ici
  └── ...
```

### 3.2 Modifier le fichier .vbproj

Ouvrez `TuyaRealtimeVB/TuyaRealtimeVB.vbproj` et ajoutez dans `<PropertyGroup>`:

```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net8.0-windows</TargetFramework>
  <UseWindowsForms>true</UseWindowsForms>

  <!-- Ajouter cette ligne -->
  <ApplicationIcon>..\app_icon.ico</ApplicationIcon>

  <!-- Reste de la config... -->
</PropertyGroup>
```

### 3.3 Recompiler

1. Recompiler le projet dans Visual Studio (Build → Rebuild Solution)
2. L'icône apparaîtra sur l'exécutable `.exe`

## Étape 4: Icône dans la barre des tâches (optionnel)

Pour afficher l'icône dans la barre des tâches, ajoutez dans `DashboardForm.vb`:

```vb
Public Sub New()
    InitializeComponent()

    ' Charger l'icône depuis les ressources
    Try
        Me.Icon = New Icon("app_icon.ico")
    Catch ex As Exception
        ' Icône non trouvée, utiliser l'icône par défaut
    End Try

    ' Reste de l'initialisation...
End Sub
```

## 🎨 Description de l'icône

L'icône créée représente:
- **Nuage (cloud)** avec dégradé bleu → orange (couleurs Tuya)
- **Points connectés** symbolisant l'IoT et les appareils connectés
- **Maison au centre** représentant la domotique
- **Style moderne** avec ombres et effets de lueur

## 🔧 Personnalisation

Pour modifier l'icône, éditez `icon_generator.html`:
- Lignes 106-108: Couleurs du gradient
- Lignes 129-142: Position des points de connexion
- Lignes 165-182: Forme de la maison

Puis regénérez les PNG et reconvertissez en .ico.

## 📝 Notes

- Format .ico multi-résolution recommandé (contient 256x256, 64x64, 32x32)
- L'icône s'affichera sur l'`.exe`, dans la barre des tâches, et dans l'explorateur Windows
- Pour une meilleure qualité, utilisez toujours un fond transparent (PNG)
