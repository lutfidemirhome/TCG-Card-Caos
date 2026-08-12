#!/usr/bin/env python3
"""Generate CardDefinition assets for Normal Uncommon card PNGs (Normal_name_1 … _5)."""
import os
import re
import uuid

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART_ROOT = os.path.join(ROOT, "Assets/Art/Cards/Normal_Uncommon_Cards")
OUT_DIR = os.path.join(ROOT, "Assets/Resources/Cards/Definitions")
SCRIPT_GUID = "7d0dd49612e9649128599303e4c70cba"
MAX_SLOT = 5
FILE_PATTERN = re.compile(r"^Normal_(?P<character>[a-z]+)_(?P<slot>\d+)\.png$", re.IGNORECASE)

# Reuse helpers from setup script style
def read_guid(meta_path):
    with open(meta_path, encoding="utf-8") as handle:
        for line in handle:
            if line.startswith("guid:"):
                return line.split(":", 1)[1].strip()
    raise ValueError(f"No guid in {meta_path}")


def ensure_texture_meta(png_path):
    meta_path = png_path + ".meta"
    if os.path.exists(meta_path):
        return read_guid(meta_path)

    meta_yaml = f"fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\n"
    with open(meta_path, "w", encoding="utf-8") as handle:
        handle.write(meta_yaml)
    return read_guid(meta_path)


def display_name(character, slot):
    return f"{character.capitalize()} {slot}"


def write_asset(asset_path, meta_path, definition_id, character, slot, texture_guid):
    os.makedirs(OUT_DIR, exist_ok=True)
    asset_guid = read_guid(meta_path) if os.path.exists(meta_path) else uuid.uuid4().hex

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
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {SCRIPT_GUID}, type: 3}}
  m_Name: {definition_id}
  m_EditorClassIdentifier: 
  definitionId: {definition_id}
  displayName: {display_name(character, slot)}
  shelfCategoryId: normal_uncommon
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


def main():
    created = 0

    for dirpath, _, filenames in os.walk(ART_ROOT):
        for filename in sorted(filenames):
            if not filename.lower().endswith(".png"):
                continue

            match = FILE_PATTERN.match(filename)
            if not match:
                print(f"Skip (name): {filename}")
                continue

            character = match.group("character").lower()
            slot = int(match.group("slot"))
            if slot < 1 or slot > MAX_SLOT:
                print(f"Skip (slot): {filename}")
                continue

            definition_id = f"Normal_{character}_{slot}"
            png_path = os.path.join(dirpath, filename)
            texture_guid = ensure_texture_meta(png_path)
            asset_path = os.path.join(OUT_DIR, definition_id + ".asset")
            meta_path = asset_path + ".meta"
            write_asset(asset_path, meta_path, definition_id, character, slot, texture_guid)
            created += 1

    print(f"Generated {created} Normal Uncommon CardDefinition assets in {OUT_DIR}")


if __name__ == "__main__":
    main()
