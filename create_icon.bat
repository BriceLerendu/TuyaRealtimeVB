@echo off
echo ========================================
echo  Generation de l'icone Tuya Dashboard
echo ========================================
echo.

REM Vérifier si Python est installé
python --version >nul 2>&1
if errorlevel 1 (
    echo [ERREUR] Python n'est pas installe ou n'est pas dans le PATH
    echo.
    echo Telechargez Python depuis: https://www.python.org/downloads/
    echo.
    pause
    exit /b 1
)

echo [INFO] Python detecte
echo.

REM Vérifier si Pillow est installé
python -c "import PIL" >nul 2>&1
if errorlevel 1 (
    echo [INFO] Installation du module Pillow...
    pip install Pillow
    if errorlevel 1 (
        echo [ERREUR] Impossible d'installer Pillow
        pause
        exit /b 1
    )
    echo.
)

echo [INFO] Generation de l'icone en cours...
echo.

REM Générer l'icône
python generate_icon.py

if errorlevel 1 (
    echo.
    echo [ERREUR] Echec de la generation
    pause
    exit /b 1
)

echo.
echo ========================================
echo  TERMINE !
echo ========================================
echo.
echo L'icone a ete creee: app_icon.ico
echo Apercu disponible: app_icon_preview.png
echo.
echo Maintenant:
echo 1. Ouvrez app_icon_preview.png pour voir le resultat
echo 2. Recompilez votre projet dans Visual Studio
echo 3. Votre .exe aura la nouvelle icone !
echo.

REM Ouvrir l'aperçu automatiquement
if exist app_icon_preview.png (
    start app_icon_preview.png
)

pause
