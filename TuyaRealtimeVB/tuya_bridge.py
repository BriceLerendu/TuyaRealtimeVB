#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
Script Python minimal pour recevoir les événements Tuya Pulsar 
et les transmettre à l'application VB.NET via HTTP
"""
import sys
import requests
from tuya_connector import (
    TuyaOpenPulsar,
    TuyaCloudPulsarTopic
)

# CONFIGURATION TUYA
ACCESS_ID = "k75dg3udb1hyca3tog1e"
ACCESS_KEY = "785a01bab5c0485c85e0b40261aebb36"
MQ_ENDPOINT = "wss://mqe.tuyaeu.com:8285/"

# URL de votre application VB.NET
VBNET_URL = "http://localhost:5000/tuya-event"

# Variable globale pour éviter les doublons
_pulsar_instance = None

def msg_listener(data: str):
    """Gestionnaire des messages Pulsar"""
    # Utiliser print() avec flush pour éviter le buffering
    print("[PULSAR] Message reçu", flush=True)
    print(f"[PULSAR] {data}", flush=True)
    
    # Envoyer à l'application VB.NET
    try:
        response = requests.post(
            VBNET_URL,
            json={'event': data},
            timeout=1
        )
        if response.status_code == 200:
            print("[HTTP] Envoyé à VB.NET avec succès", flush=True)
        else:
            print(f"[HTTP] Erreur: {response.status_code}", flush=True)
    except requests.exceptions.ConnectionError:
        print("[HTTP] VB.NET non disponible (connexion refusée)", flush=True)
    except Exception as e:
        print(f"[HTTP] Erreur: {e}", flush=True)
    
    print("-" * 50, flush=True)

def main():
    """Point d'entrée principal"""
    global _pulsar_instance
    
    # Forcer l'encodage UTF-8 pour stdout
    if sys.stdout.encoding != 'utf-8':
        sys.stdout.reconfigure(encoding='utf-8')
    
    print("=" * 50, flush=True)
    print("TUYA PULSAR -> VB.NET Bridge", flush=True)
    print("=" * 50, flush=True)
    print(f"Access ID: {ACCESS_ID}", flush=True)
    print(f"Endpoint: {MQ_ENDPOINT}", flush=True)
    print(f"Target: {VBNET_URL}", flush=True)
    print("=" * 50, flush=True)
    
    try:
        # Vérifier qu'il n'y a pas déjà une instance
        if _pulsar_instance is not None:
            print("[ATTENTION] Une instance Pulsar existe déjà, arrêt...", flush=True)
            _pulsar_instance.stop()
            _pulsar_instance = None
        
        # Initialisation de Pulsar
        _pulsar_instance = TuyaOpenPulsar(
            ACCESS_ID,
            ACCESS_KEY,
            MQ_ENDPOINT,
            TuyaCloudPulsarTopic.PROD
        )
        
        # Ajout du listener UNE SEULE FOIS
        _pulsar_instance.add_message_listener(msg_listener)
        
        # Démarrage
        print("\n[PULSAR] Démarrage...", flush=True)
        _pulsar_instance.start()
        
        print("[PULSAR] En écoute des événements Tuya", flush=True)
        print("Appuyez sur Ctrl+C pour arrêter\n", flush=True)
        
        # Attendre indéfiniment
        while True:
            try:
                input()
            except EOFError:
                # Si lancé sans console interactive, attendre indéfiniment
                import time
                while True:
                    time.sleep(1)
        
    except KeyboardInterrupt:
        print("\n[PULSAR] Interruption par l'utilisateur", flush=True)
    except Exception as e:
        print(f"\n[ERREUR] {e}", flush=True)
        import traceback
        traceback.print_exc()
    finally:
        # Arrêt propre
        if _pulsar_instance is not None:
            print("[PULSAR] Arrêt...", flush=True)
            _pulsar_instance.stop()
            _pulsar_instance = None
            print("[PULSAR] Arrêté", flush=True)

if __name__ == '__main__':
    main()