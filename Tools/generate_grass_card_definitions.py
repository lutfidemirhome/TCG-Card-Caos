#!/usr/bin/env python3
"""Generate CardDefinition assets for Grass Common / Uncommon / Rare card PNGs."""
import os
import re
import uuid

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets/Resources/Cards/Definitions")
SCRIPT_GUID = "7d0dd49612e9649128599303e4c70cba"
GUID_RE = re.compile(r"^[0-9a-f]{32}$")

SETS = [
    {
        "art_root": "Assets/Art/Cards/Grass_Common_Cards",
        "category": "grass_common",
        "max_slot": 10,
        "slot_width": 2,
        "patterns": [
            re.compile(r"^grass_(?P<character>[a-z]+)_(?P<slot>\d+)\.png$", re.IGNORECASE),
        ],
    },
    {
        "art_root": "Assets/Art/Cards/Grass_Uncommon_Cards",
        "category": "grass_uncommon",
        "max_slot": 5,
        "slot_width": 0,
        "patterns": [
            re.compile(r"^grass_(?P<character>[a-z]+)_(?P<slot>\d+)\.png$", re.IGNORECASE),
        ],
    },
    {
        "art_root": "Assets/Art/Cards/Grass_Rare_Cards",
        "category": "grass_rare",
        "max_slot": 3,
        "slot_width": 0,
        "patterns": [
            re.compile(r"^grass_(?P<character>[a-z]+)_(?P<slot>\d+)\.png$", re.IGNORECASE),
        ],
    },
]


def read_guid(meta_path):
    with open(meta_path, encoding="utf-8") as handle:
        for line in handle:
            if line.startswith("guid:"):
                guid = line.split(":", 1)[1].strip()
                if not GUID_RE.match(guid):
                    raise ValueError(f"Invalid guid in {meta_path}: {guid}")
                return guid
    raise ValueError(f"No guid in {meta_path}")


def new_guid():
    return uuid.uuid4().hex


def ensure_texture_meta(png_path):
    meta_path = png_path + ".meta"
    if os.path.exists(meta_path):
        return read_guid(meta_path)

    guid = new_guid()
    with open(meta_path, "w", encoding="utf-8") as handle:
        handle.write(f"fileFormatVersion: 2\nguid: {guid}\n")
    return guid


def parse_filename(dirpath, filename, patterns):
    for pattern in patterns:
        match = pattern.match(filename)
        if match:
            return match.group("character").lower(), int(match.group("slot"))

    folder = os.path.basename(dirpath).lower()
    slot_match = re.search(r"_(\d+)\.png$", filename, re.IGNORECASE)
    if folder and slot_match:
        return folder, int(slot_match.group(1))

    return None, None


def definition_id(category, character, slot, slot_width):
    if slot_width > 0:
        return f"{category}_{character}_{slot:0{slot_width}d}"
    return f"{category}_{character}_{slot}"


def display_name(character, slot):
    return f"{character.capitalize()} {slot}"


def write_asset(asset_path, meta_path, definition_id_value, character, slot, category, texture_guid):
    os.makedirs(OUT_DIR, exist_ok=True)
    asset_guid = read_guid(meta_path) if os.path.exists(meta_path) else new_guid()

    yaml = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorClassIdentifier: 
  m_Script: {{fileID: 11500000, guid: {SCRIPT_GUID}, type: 3}}
  m_Name: {definition_id_value}
  m_EditorClassIdentifier: 
  definitionId: {definition_id_value}
  displayName: {display_name(character, slot)}
  shelfCategoryId: {category}
  shelfSlotNumber: {slot}
  frontTexture: {{fileID: 2800000, guid: {texture_guid}, type: 3}}
  categorySymbol: {{fileID: 0}}
"""

    meta_yaml = f"""fileFormatVersion: 2
guid: {asset_guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

    with open(asset_path, "w", encoding="utf-8") as handle:
        handle.write(yaml)

    with open(meta_path, "w", encoding="utf-8") as handle:
        handle.write(meta_yaml)


def generate_set(config):
    art_root = os.path.join(ROOT, config["art_root"])
    created = 0
    skipped = []

    for dirpath, _, filenames in os.walk(art_root):
        for filename in sorted(filenames):
            if not filename.lower().endswith(".png"):
                continue

            character, slot = parse_filename(dirpath, filename, config["patterns"])
            if not character or slot is None:
                skipped.append(filename)
                continue

            if slot < 1 or slot > config["max_slot"]:
                skipped.append(filename)
                continue

            def_id = definition_id(config["category"], character, slot, config["slot_width"])
            png_path = os.path.join(dirpath, filename)
            texture_guid = ensure_texture_meta(png_path)
            asset_path = os.path.join(OUT_DIR, def_id + ".asset")
            meta_path = asset_path + ".meta"
            write_asset(
                asset_path,
                meta_path,
                def_id,
                character,
                slot,
                config["category"],
                texture_guid,
            )
            created += 1

    return created, skipped


def main():
    total = 0
    for config in SETS:
        created, skipped = generate_set(config)
        total += created
        print(f"{config['category']}: {created} definitions")
        for name in skipped:
            print(f"  skip: {name}")

    print(f"Generated {total} Grass CardDefinition assets in {OUT_DIR}")


if __name__ == "__main__":
    main()
