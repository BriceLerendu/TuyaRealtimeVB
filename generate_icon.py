"""
Générateur d'icône Tuya Dashboard
Crée un fichier .ico avec le design cloud Tuya
"""

from PIL import Image, ImageDraw
import os

def create_cloud_icon(size=256):
    """Crée une icône avec un nuage stylisé Tuya"""

    # Créer une image avec fond transparent
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Couleurs Tuya
    blue = (46, 91, 255)      # #2E5BFF
    orange = (255, 107, 0)    # #FF6B00

    # Dessiner le nuage (ellipses pour former un nuage)
    scale = size / 256

    # Nuage principal avec dégradé simulé (plusieurs couches)
    # Couche 1 - Base orange
    draw.ellipse([40*scale, 90*scale, 140*scale, 180*scale], fill=orange + (255,))
    draw.ellipse([120*scale, 90*scale, 220*scale, 180*scale], fill=orange + (255,))
    draw.ellipse([80*scale, 60*scale, 180*scale, 150*scale], fill=orange + (255,))

    # Couche 2 - Mélange
    mid_color = tuple((b + o) // 2 for b, o in zip(blue, orange))
    draw.ellipse([50*scale, 95*scale, 140*scale, 175*scale], fill=mid_color + (255,))
    draw.ellipse([120*scale, 95*scale, 210*scale, 175*scale], fill=mid_color + (255,))
    draw.ellipse([85*scale, 70*scale, 175*scale, 150*scale], fill=mid_color + (255,))

    # Couche 3 - Bleu sur le dessus
    draw.ellipse([60*scale, 100*scale, 140*scale, 170*scale], fill=blue + (255,))
    draw.ellipse([120*scale, 100*scale, 200*scale, 170*scale], fill=blue + (255,))
    draw.ellipse([90*scale, 80*scale, 170*scale, 150*scale], fill=blue + (255,))

    # Dessiner une maison au centre (blanc)
    house_color = (255, 255, 255, 255)

    # Corps de la maison
    draw.rectangle([110*scale, 120*scale, 150*scale, 160*scale], fill=house_color)

    # Toit (triangle)
    draw.polygon([
        (130*scale, 105*scale),  # sommet
        (105*scale, 120*scale),  # gauche
        (155*scale, 120*scale)   # droite
    ], fill=house_color)

    # Porte
    draw.rectangle([122*scale, 140*scale, 138*scale, 160*scale], fill=blue + (255,))

    # Fenêtre
    draw.rectangle([115*scale, 128*scale, 125*scale, 138*scale], fill=orange + (255,))
    draw.rectangle([135*scale, 128*scale, 145*scale, 138*scale], fill=orange + (255,))

    # Ajouter des points de connexion IoT (petits cercles blancs)
    dot_positions = [
        (70*scale, 120*scale),
        (130*scale, 95*scale),
        (190*scale, 120*scale),
        (130*scale, 165*scale)
    ]

    for x, y in dot_positions:
        # Cercle blanc avec contour
        draw.ellipse([x-6*scale, y-6*scale, x+6*scale, y+6*scale],
                    fill=(255, 255, 255, 255),
                    outline=blue + (255,),
                    width=int(2*scale))

    return img

def main():
    """Génère l'icône en différentes tailles et crée le fichier .ico"""

    print("🎨 Génération de l'icône Tuya Dashboard...")

    # Créer les différentes tailles
    sizes = [256, 128, 64, 48, 32, 16]
    images = []

    for size in sizes:
        print(f"  → Création de l'icône {size}x{size}...")
        img = create_cloud_icon(size)
        images.append(img)

    # Sauvegarder en .ico (multi-résolution)
    output_path = "app_icon.ico"
    images[0].save(output_path, format='ICO', sizes=[(s, s) for s in sizes])

    print(f"✅ Icône créée avec succès: {output_path}")
    print(f"   Tailles incluses: {', '.join(f'{s}x{s}' for s in sizes)}")

    # Sauvegarder aussi en PNG pour aperçu
    png_path = "app_icon_preview.png"
    images[0].save(png_path, format='PNG')
    print(f"✅ Aperçu PNG créé: {png_path}")

    print("\n📋 Prochaines étapes:")
    print("   1. L'icône app_icon.ico a été créée")
    print("   2. Ouvrez app_icon_preview.png pour voir le résultat")
    print("   3. Recompilez votre projet dans Visual Studio")
    print("   4. L'icône apparaîtra sur votre .exe!")

if __name__ == "__main__":
    try:
        main()
    except ImportError:
        print("❌ Erreur: Le module Pillow n'est pas installé")
        print("\n📦 Installation:")
        print("   pip install Pillow")
        print("\nPuis relancez ce script.")
    except Exception as e:
        print(f"❌ Erreur: {e}")
