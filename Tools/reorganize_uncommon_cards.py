#!/usr/bin/env python3
"""Move Normal Uncommon card PNGs into family subfolders and fix Unity texture .meta files."""
import os
import re
import shutil
import uuid

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART_ROOT = os.path.join(ROOT, "Assets/Art/Cards/Normal_Uncommon_Cards")
DEF_DIR = os.path.join(ROOT, "Assets/Resources/Cards/Definitions")
SCRIPT_GUID = "7d0dd49612e9649128599303e4c70cba"
COMMON_META = os.path.join(
    ROOT, "Assets/Art/Cards/Normal_Common_Cards/bloomini/normal_common_bloomini_01.png.meta"
)
FILE_PATTERN = re.compile(r"^Normal_(?P<character>[a-z]+)_(?P<slot>\d+)\.png$", re.IGNORECASE)


def read_guid(meta_path):
    if not os.path.exists(meta_path):
        return uuid.uuid4().hex
    with open(meta_path, encoding="utf-8") as handle:
        for line in handle:
            if line.startswith("guid:"):
                return line.split(":", 1)[1].strip()
    return uuid.uuid4().hex


def texture_meta_template(guid):
    with open(COMMON_META, encoding="utf-8") as handle:
        text = handle.read()
    lines = text.splitlines()
    out = []
    replaced = False
    for line in lines:
        if line.startswith("guid:"):
            out.append(f"guid: {guid}")
            replaced = True
        else:
            out.append(line)
    if not replaced:
        out.insert(1, f"guid: {guid}")
    return "\n".join(out) + "\n"


def folder_meta_template(guid):
    return f"""fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def ensure_folder(folder_path):
    os.makedirs(folder_path, exist_ok=True)
    meta_path = folder_path + ".meta"
    if not os.path.exists(meta_path):
        with open(meta_path, "w", encoding="utf-8") as handle:
            handle.write(folder_meta_template(uuid.uuid4().hex))


def display_name(character, slot):
    return f"{character.capitalize()} {slot}"


def write_definition(asset_path, meta_path, definition_id, character, slot, texture_guid):
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


def collect_pngs():
    found = []
    for dirpath, _, filenames in os.walk(ART_ROOT):
        for filename in filenames:
            if not filename.lower().endswith(".png"):
                continue
            match = FILE_PATTERN.match(filename)
            if not match:
                continue
            found.append(
                (
                    os.path.join(dirpath, filename),
                    match.group("character").lower(),
                    int(match.group("slot")),
                    filename,
                )
            )
    return found


def main():
    pngs = collect_pngs()
    if not pngs:
        raise SystemExit("No Normal_* uncommon PNGs found.")

    families = sorted({character for _, character, _, _ in pngs})
    for family in families:
        ensure_folder(os.path.join(ART_ROOT, family))

    moved = 0
    for src_path, character, slot, filename in sorted(pngs):
        dst_dir = os.path.join(ART_ROOT, character)
        dst_path = os.path.join(dst_dir, filename)
        if os.path.abspath(src_path) != os.path.abspath(dst_path):
            shutil.move(src_path, dst_path)
            src_meta = src_path + ".meta"
            if os.path.exists(src_meta):
                shutil.move(src_meta, dst_path + ".meta")
        moved += 1

        texture_guid = read_guid(dst_path + ".meta")
        with open(dst_path + ".meta", "w", encoding="utf-8") as handle:
            handle.write(texture_meta_template(texture_guid))

        definition_id = f"Normal_{character}_{slot}"
        asset_path = os.path.join(DEF_DIR, definition_id + ".asset")
        write_definition(
            asset_path,
            asset_path + ".meta",
            definition_id,
            character,
            slot,
            texture_guid,
        )

    # Remove stray root-level metas left behind after moves.
    for entry in os.listdir(ART_ROOT):
        path = os.path.join(ART_ROOT, entry)
        if entry.endswith(".meta") and not entry.endswith(".png.meta"):
            if os.path.isfile(path) and not os.path.isdir(path[:-5]):
                os.remove(path)

    print(f"Reorganized {moved} uncommon cards into {len(families)} family folders under {ART_ROOT}")


if __name__ == "__main__":
    main()
