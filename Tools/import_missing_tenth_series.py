#!/usr/bin/env python3
"""Create CardDefinitions for the 10th series that already have art but no assets."""
import os
import re
import uuid

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEF_DIR = os.path.join(ROOT, "Assets/Resources/Cards/Definitions")
ART_ROOT = os.path.join(ROOT, "Assets/Art/Cards")
SCRIPT_GUID = "7d0dd49612e9649128599303e4c70cba"
GUID_RE = re.compile(r"^[0-9a-fA-F]{32}$")

SERIES = (
    ("lightning_uncommon", "Lightning_Uncommon_Cards", "nimbuspark", "lightning_nimbuspark_{slot}.png", 5),
    ("ice_rare", "Ice_Rare_Cards", "rimefang", "ice_rimefang_{slot}.png", 3),
    ("flying_rare", "Flying_Rare_Cards", "glaciri", "flying_glaciri_{slot}.png", 3),
    ("ground_rare", "Ground_Rare_Cards", "orbcrab", "ground_orbcrab_{slot}.png", 3),
    ("dragon_uncommon", "Dragon_Uncommon_Cards", "rhazakor", "dragon_rhazakor_{slot}.png", 5),
    ("rock_common", "Rock_Common_Cards", "stonewyrm", "rock_stonewyrm_{slot}.png", 10),
)

ASSET_TEMPLATE = """%YAML 1.1
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
  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}
  m_Name: {definition_id}
  m_EditorClassIdentifier: 
  definitionId: {definition_id}
  displayName: {display_name}
  shelfCategoryId: {category}
  shelfSlotNumber: {slot}
  frontTexture: {{fileID: 2800000, guid: {texture_guid}, type: 3}}
  categorySymbol: {{fileID: 0}}
"""

ASSET_META_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def read_guid(meta_path):
    with open(meta_path, encoding="utf-8") as handle:
        for line in handle:
            if line.startswith("guid:"):
                guid = line.split(":", 1)[1].strip()
                if GUID_RE.match(guid):
                    return guid
    raise ValueError("No guid in " + meta_path)


def main():
    created = 0
    for category, folder, character, png_name, count in SERIES:
        for slot in range(1, count + 1):
            definition_id = f"{category}_{character}_{slot}"
            asset_path = os.path.join(DEF_DIR, definition_id + ".asset")
            if os.path.exists(asset_path):
                continue

            png_path = os.path.join(ART_ROOT, folder, character, png_name.format(slot=slot))
            meta_path = png_path + ".meta"
            if not os.path.exists(png_path) or not os.path.exists(meta_path):
                raise FileNotFoundError(png_path)

            texture_guid = read_guid(meta_path)
            display_name = f"{character.capitalize()} {slot}"
            with open(asset_path, "w", encoding="utf-8") as handle:
                handle.write(ASSET_TEMPLATE.format(
                    script_guid=SCRIPT_GUID,
                    definition_id=definition_id,
                    display_name=display_name,
                    category=category,
                    slot=slot,
                    texture_guid=texture_guid,
                ))
            with open(asset_path + ".meta", "w", encoding="utf-8") as handle:
                handle.write(ASSET_META_TEMPLATE.format(guid=uuid.uuid4().hex))
            created += 1
            print("created", definition_id)

    print(f"Generated {created} missing tenth-series CardDefinitions")


if __name__ == "__main__":
    main()
