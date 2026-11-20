# -*- coding: utf-8 -*-
"""
Прописывает music/ambience в каждой сцене Data/Scenario/*.json согласно карте
background -> (music, ambience). Карта соответствует 24 трекам, сгенерированным
Tools/AudioGeneration/generate_ambient_audio.py.

Запуск: python3 update_scene_audio.py   (из корня проекта ZeroFloor)
"""
import json
import glob
import os

SCENARIO_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Data", "Scenario")
SCENARIO_DIR = os.path.abspath(SCENARIO_DIR)

# background id -> (musicId, ambienceId)
MAPPING = {
    "dispatch_room_6": ("mus_low_city_theme", "amb_dispatch_hum"),
    "shadow_market_approach": ("mus_market_theme", "amb_market_crowd"),
    "gosha_stall": ("mus_market_theme", "amb_market_crowd"),
    "shadow_market_after_raid": ("mus_market_theme", "amb_market_crowd"),
    "technical_shafts": ("mus_stealth_tension", "amb_tech_shafts"),
    "dark_technical_corridor": ("mus_stealth_tension", "amb_tech_shafts"),
    "archive_terminal": ("mus_archive_mystery", "amb_archive_drone"),
    "ventilation_station": ("mus_low_city_theme", "amb_ventilation"),
    "administration_corridor": ("mus_interrogation_dread", "amb_admin_sterile"),
    "interrogation_hall": ("mus_interrogation_dread", "amb_admin_sterile"),
    "ania_apartment": ("mus_apartment_intimate", "amb_apartment_quiet"),
    "keepers_archive": ("mus_archive_mystery", "amb_archive_drone"),
    "wessel_office": ("mus_power_intrigue", "amb_office_tension"),
    "crown_corridors": ("mus_crown_cold", "amb_crown_airy"),
    "crown_reception": ("mus_crown_cold", "amb_crown_airy"),
    "core_hidden_corridor": ("mus_core_climax", "amb_core_cathedral"),
    "server_cathedral": ("mus_core_climax", "amb_core_cathedral"),
    "keeper_post": ("mus_watcher_calm", "amb_core_cathedral"),
    "core_corridors": ("mus_core_climax", "amb_core_cathedral"),
    "three_terminals_hall": ("mus_core_climax", "amb_core_cathedral"),
    "elevator_shaft": ("mus_finale_escape", "amb_tech_shafts"),
    "ending_a_escape": ("mus_finale_escape", "amb_crown_airy"),
    "ending_b_collapse": ("mus_finale_collapse", "amb_core_cathedral"),
    "ending_c_successor": ("mus_finale_successor", "amb_core_cathedral"),
    "ending_d_balance": ("mus_finale_balance", "amb_core_cathedral"),
}


def reorder_with_audio(scene, music_id, ambience_id):
    new_scene = {}
    inserted = False
    for k, v in scene.items():
        new_scene[k] = v
        if k == "background" and not inserted:
            new_scene["music"] = music_id
            new_scene["ambience"] = ambience_id
            inserted = True
    if not inserted:
        # На случай отсутствия поля "background" — добавляем в конец.
        new_scene["music"] = music_id
        new_scene["ambience"] = ambience_id
    return new_scene


def main():
    files = sorted(glob.glob(os.path.join(SCENARIO_DIR, "*.json")))
    total_scenes = 0
    changed_files = 0
    unmapped = set()
    for path in files:
        with open(path, "r", encoding="utf-8") as f:
            scenes = json.load(f)

        file_changed = False
        new_scenes = []
        for scene in scenes:
            total_scenes += 1
            bg = scene.get("background")
            if bg in MAPPING:
                music_id, ambience_id = MAPPING[bg]
                if scene.get("music") != music_id or scene.get("ambience") != ambience_id:
                    scene = reorder_with_audio(scene, music_id, ambience_id)
                    file_changed = True
            else:
                unmapped.add((bg, scene.get("sceneId")))
            new_scenes.append(scene)

        if file_changed:
            with open(path, "w", encoding="utf-8") as f:
                json.dump(new_scenes, f, ensure_ascii=False, indent=2)
                f.write("\n")
            changed_files += 1
            print(f"updated: {os.path.basename(path)}")

    print(f"\nScenes processed: {total_scenes}, files updated: {changed_files}")
    if unmapped:
        print("WARNING: scenes with unmapped background (left unchanged):")
        for bg, sid in sorted(unmapped):
            print(f"  background={bg!r} sceneId={sid!r}")


if __name__ == "__main__":
    main()
