#!/usr/bin/env python3
"""Import prism elite card sets and 50-slot (uncommon-size) cabinets.

Does not enable ground scatter. Sign art is copied from fire_uncommon as a placeholder.
Darkness PNGs may omit `_elite` in the filename (`darkness_prism_name_1.png`).
"""
import os
import re
import shutil
import sys
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DESKTOP = Path.home() / "Desktop"
ART_ROOT = ROOT / "Assets/Art/Cards"
OUT_DIR = ROOT / "Assets/Resources/Cards/Definitions"
SHELF_CATS = ROOT / "Assets/Data/ShelfCategories"
CABINETS = ROOT / "Assets/Prefabs/Cabinets"
SCENE_PATH = ROOT / "Assets/Scenes/MainScene.unity"

SCRIPT_GUID = "7d0dd49612e9649128599303e4c70cba"
GUID_RE = re.compile(r"^[0-9a-f]{32}$")
META_GUID_RE = re.compile(r"^guid: ([0-9a-f]{32})$", re.M)

FIRE_UNCOMMON_PREFAB = CABINETS / "Cabinets_Fire/Cabinets_FireUncommon.prefab"
FIRE_UNCOMMON_PREFAB_GUID = "3561578d971e4cce84957628607c95ec"
FIRE_UNCOMMON_MAT_GUID = "5eb2d578bdb3418e9a3d9054e842cafe"
FIRE_UNCOMMON_INSTANCE_ID = "1600078461"

TEXTURE_META_TEMPLATE = (ART_ROOT / "Fire_Common_Cards/emberkit/fire_emberkit_1.png.meta").read_text(
    encoding="utf-8"
)

FOLDER_META_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

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

CATEGORIES = [
    {
        "id": "darkness_prism_elite",
        "desktop": "darkness_prism_elite",
        "art_folder": "Darkness_Prism_Elite_Cards",
        "cab_dir": "Cabinets_Darkness",
        "prefab": "Cabinets_DarknessPrismElite",
        "instance_id": 910000601,
        "pos": (12.1, 0.060001135, -15.2),
    },
    {
        "id": "fighting_prism_elite",
        "desktop": "fighting_prism_elite",
        "art_folder": "Fighting_Prism_Elite_Cards",
        "cab_dir": "Cabinets_Fighting",
        "prefab": "Cabinets_FightingPrismElite",
        "instance_id": 910000602,
        "pos": (10.46, 0.060001135, -15.2),
    },
    {
        "id": "ice_prism_elite",
        "desktop": "ice_prism_elite",
        "art_folder": "Ice_Prism_Elite_Cards",
        "cab_dir": "Cabinets_Ice",
        "prefab": "Cabinets_IcePrismElite",
        "instance_id": 910000603,
        "pos": (8.82, 0.060001135, -15.2),
    },
    {
        "id": "lightning_prism_elite",
        "desktop": "lightning_prism_elite",
        "art_folder": "Lightning_Prism_Elite_Cards",
        "cab_dir": "Cabinets_Lightning",
        "prefab": "Cabinets_LightningPrismElite",
        "instance_id": 910000604,
        "pos": (7.18, 0.060001135, -15.2),
    },
    {
        "id": "psychic_prism_elite",
        "desktop": "psychic_prism_elite",
        "art_folder": "Psychic_Prism_Elite_Cards",
        "cab_dir": "Cabinets_Psychic",
        "prefab": "Cabinets_PsychicPrismElite",
        "instance_id": 910000605,
        "pos": (5.54, 0.060001135, -15.2),
    },
]


def new_guid():
    return uuid.uuid4().hex


def read_guid(meta_path: Path) -> str:
    for line in meta_path.read_text(encoding="utf-8").splitlines():
        if line.startswith("guid:"):
            guid = line.split(":", 1)[1].strip()
            if GUID_RE.match(guid):
                return guid
    raise ValueError(f"No guid in {meta_path}")


def ensure_folder_meta(folder: Path):
    folder.mkdir(parents=True, exist_ok=True)
    meta_path = Path(str(folder) + ".meta")
    if meta_path.exists():
        return read_guid(meta_path)
    guid = new_guid()
    meta_path.write_text(FOLDER_META_TEMPLATE.format(guid=guid), encoding="utf-8")
    return guid


def write_meta_from_template(dst_meta: Path, src_meta: Path, guid: str):
    text = META_GUID_RE.sub(f"guid: {guid}", src_meta.read_text(encoding="utf-8"), count=1)
    dst_meta.write_text(text, encoding="utf-8")


def ensure_texture_meta(png_path: Path) -> str:
    meta_path = Path(str(png_path) + ".meta")
    if meta_path.exists():
        return read_guid(meta_path)
    guid = new_guid()
    meta_path.write_text(META_GUID_RE.sub(f"guid: {guid}", TEXTURE_META_TEMPLATE, count=1), encoding="utf-8")
    return guid


def parse_png(filename: str, category: str):
    patterns = [
        rf"^{re.escape(category)}_(?P<character>[a-z0-9_]+)_(?P<slot>\d+)\.png$",
    ]
    if category.endswith("_elite"):
        short = category[: -len("_elite")]
        patterns.append(
            rf"^{re.escape(short)}_(?P<character>[a-z0-9_]+)_(?P<slot>\d+)\.png$"
        )
    for pattern in patterns:
        match = re.match(pattern, filename, re.IGNORECASE)
        if match:
            return match.group("character").lower(), int(match.group("slot"))
    return None


def reuse_or_new_guid(meta_path: Path) -> str:
    if meta_path.exists():
        return read_guid(meta_path)
    return new_guid()


def display_name(character: str, slot: int) -> str:
    titled = " ".join(part.capitalize() for part in character.split("_"))
    return f"{titled} {slot}"


def write_definition(def_id: str, character: str, slot: int, category: str, texture_guid: str):
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    asset_path = OUT_DIR / f"{def_id}.asset"
    meta_path = Path(str(asset_path) + ".meta")
    asset_guid = read_guid(meta_path) if meta_path.exists() else new_guid()
    asset_path.write_text(
        ASSET_TEMPLATE.format(
            script_guid=SCRIPT_GUID,
            definition_id=def_id,
            display_name=display_name(character, slot),
            category=category,
            slot=slot,
            texture_guid=texture_guid,
        ),
        encoding="utf-8",
    )
    meta_path.write_text(ASSET_META_TEMPLATE.format(guid=asset_guid), encoding="utf-8")


def import_cards():
    copied = 0
    created = 0
    skipped = []
    per_category = {}

    for spec in CATEGORIES:
        category = spec["id"]
        src_root = DESKTOP / spec["desktop"]
        if not src_root.is_dir():
            raise SystemExit(f"Source folder missing: {src_root}")

        art_set = ART_ROOT / spec["art_folder"]
        ensure_folder_meta(art_set)
        per_category[category] = 0

        for dirpath, _, filenames in os.walk(src_root):
            for filename in filenames:
                if not filename.lower().endswith(".png"):
                    continue
                parsed = parse_png(filename, category)
                if not parsed:
                    skipped.append(filename)
                    continue

                character, slot = parsed
                if slot < 1 or slot > 5:
                    skipped.append(filename)
                    continue

                dest_dir = art_set / character
                ensure_folder_meta(dest_dir)
                dest_png = dest_dir / filename.lower()
                src_png = Path(dirpath) / filename
                if not dest_png.exists() or dest_png.stat().st_size != src_png.stat().st_size:
                    shutil.copy2(src_png, dest_png)
                    copied += 1

                texture_guid = ensure_texture_meta(dest_png)
                def_id = f"{category}_{character}_{slot}"
                write_definition(def_id, character, slot, category, texture_guid)
                created += 1
                per_category[category] += 1

    print(f"Copied {copied} PNGs")
    print(f"Generated {created} CardDefinition assets")
    for category in sorted(per_category):
        print(f"  {category}: {per_category[category]}")
    if skipped:
        print(f"Skipped {len(skipped)}")
        for name in skipped:
            print(f"  skip: {name}")
    return per_category


def copy_sign_and_category(dst_id: str):
    from shelf_sign_paths import material_path, texture_path

    src_tex_stem = "fire_uncommon_sign"
    dst_tex_stem = f"{dst_id}_sign"
    src_tex = texture_path(src_tex_stem)
    dst_tex = texture_path(dst_tex_stem)
    dst_tex.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src_tex, dst_tex)
    tex_guid = reuse_or_new_guid(Path(str(dst_tex) + ".meta"))
    write_meta_from_template(Path(str(dst_tex) + ".meta"), Path(str(src_tex) + ".meta"), tex_guid)
    ensure_folder_meta(dst_tex.parent)

    src_mat = material_path(src_tex_stem)
    dst_mat = material_path(dst_tex_stem)
    dst_mat.parent.mkdir(parents=True, exist_ok=True)
    mat_text = src_mat.read_text(encoding="utf-8")
    mat_text = mat_text.replace(f"m_Name: {src_tex_stem}", f"m_Name: {dst_tex_stem}")
    src_tex_guid = None
    for line in mat_text.splitlines():
        if "2800000" in line and "guid:" in line:
            found = re.search(r"guid: ([0-9a-f]{32})", line)
            if found:
                src_tex_guid = found.group(1)
                break
    if src_tex_guid:
        mat_text = mat_text.replace(src_tex_guid, tex_guid)
    dst_mat.write_text(mat_text, encoding="utf-8")
    mat_guid = reuse_or_new_guid(Path(str(dst_mat) + ".meta"))
    write_meta_from_template(Path(str(dst_mat) + ".meta"), Path(str(src_mat) + ".meta"), mat_guid)
    ensure_folder_meta(dst_mat.parent)

    src_cat = SHELF_CATS / "fire_uncommon.asset"
    dst_cat = SHELF_CATS / f"{dst_id}.asset"
    cat_text = src_cat.read_text(encoding="utf-8")
    cat_text = cat_text.replace("m_Name: fire_uncommon", f"m_Name: {dst_id}")
    cat_text = re.sub(r"categoryId: fire_uncommon", f"categoryId: {dst_id}", cat_text)
    cat_text = re.sub(
        r"signMaterial: \{fileID: 2100000, guid: [0-9a-f]{32}, type: 2\}",
        f"signMaterial: {{fileID: 2100000, guid: {mat_guid}, type: 2}}",
        cat_text,
    )
    dst_cat.write_text(cat_text, encoding="utf-8")
    cat_guid = reuse_or_new_guid(Path(str(dst_cat) + ".meta"))
    write_meta_from_template(Path(str(dst_cat) + ".meta"), Path(str(src_cat) + ".meta"), cat_guid)
    return cat_guid, mat_guid


def copy_cabinets():
    src_prefab = FIRE_UNCOMMON_PREFAB
    src_meta = Path(str(src_prefab) + ".meta")
    created = []
    for spec in CATEGORIES:
        dst_dir = CABINETS / spec["cab_dir"]
        if not dst_dir.is_dir():
            raise SystemExit(f"Cabinet folder missing: {dst_dir}")
        cat_guid, mat_guid = copy_sign_and_category(spec["id"])
        dst_prefab = dst_dir / f"{spec['prefab']}.prefab"
        text = src_prefab.read_text(encoding="utf-8")
        text = text.replace("Cabinets_FireUncommon", spec["prefab"])
        text = text.replace("fire_uncommon", spec["id"])
        text = re.sub(
            r"categoryDefinition: \{fileID: 11400000, guid: [0-9a-f]{32}, type: 2\}",
            f"categoryDefinition: {{fileID: 11400000, guid: {cat_guid}, type: 2}}",
            text,
            count=1,
        )
        text = text.replace(FIRE_UNCOMMON_MAT_GUID, mat_guid)
        dst_prefab.write_text(text, encoding="utf-8")
        prefab_guid = reuse_or_new_guid(Path(str(dst_prefab) + ".meta"))
        write_meta_from_template(Path(str(dst_prefab) + ".meta"), src_meta, prefab_guid)
        spec["prefab_guid"] = prefab_guid
        created.append((spec["prefab"], spec["id"], prefab_guid, cat_guid))
        print(f"  cabinet {spec['prefab']} -> {spec['id']} guid={prefab_guid}")
    return created


def extract_fire_uncommon_instance(scene_text: str) -> str:
    marker = f"--- !u!1001 &{FIRE_UNCOMMON_INSTANCE_ID}\n"
    start = scene_text.find(marker)
    if start < 0:
        raise SystemExit("FireUncommon PrefabInstance not found in MainScene")
    end = scene_text.find("\n--- !u!", start + len(marker))
    if end < 0:
        raise SystemExit("Could not find end of FireUncommon PrefabInstance")
    return scene_text[start:end]


def rewire_existing_instances(scene_text: str) -> str:
    for spec in CATEGORIES:
        marker = f"value: {spec['prefab']}\n"
        idx = scene_text.find(marker)
        if idx < 0:
            continue
        block_start = scene_text.rfind("--- !u!1001 &", 0, idx)
        block_end = scene_text.find("\n--- !u!", idx)
        if block_start < 0 or block_end < 0:
            continue
        block = scene_text[block_start:block_end]
        found = re.search(r"guid: ([0-9a-f]{32})", block)
        if not found:
            continue
        old_guid = found.group(1)
        new_guid = spec["prefab_guid"]
        if old_guid == new_guid:
            continue
        print(f"  rewire {spec['prefab']} {old_guid} -> {new_guid}")
        scene_text = scene_text.replace(old_guid, new_guid)
    return scene_text


def place_cabinets_in_scene():
    scene_text = SCENE_PATH.read_text(encoding="utf-8")
    if "Cabinets_DarknessPrismElite" in scene_text:
        print("MainScene already has prism elite cabinets; rewiring guids if needed")
        scene_text = rewire_existing_instances(scene_text)
        SCENE_PATH.write_text(scene_text, encoding="utf-8")
        return

    source_block = extract_fire_uncommon_instance(scene_text)
    new_blocks = []
    for spec in CATEGORIES:
        block = source_block
        block = block.replace(f"&{FIRE_UNCOMMON_INSTANCE_ID}", f"&{spec['instance_id']}")
        block = block.replace(FIRE_UNCOMMON_PREFAB_GUID, spec["prefab_guid"])
        block = block.replace("value: Cabinets_FireUncommon", f"value: {spec['prefab']}")
        x, y, z = spec["pos"]

        def replace_root_axis(text, axis, value):
            pattern = (
                rf"(target: \{{fileID: 6362469198736883541, guid: {spec['prefab_guid']}, type: 3\}}\n"
                rf"      propertyPath: m_LocalPosition\.{axis}\n"
                rf"      value: )[^\n]+"
            )
            updated, count = re.subn(pattern, rf"\g<1>{value}", text, count=1)
            if count != 1:
                raise SystemExit(f"Failed to set {axis} for {spec['prefab']}")
            return updated

        block = replace_root_axis(block, "x", x)
        block = replace_root_axis(block, "y", y)
        block = replace_root_axis(block, "z", z)
        new_blocks.append(block)

    insert_at = scene_text.find(
        "\n--- !u!", scene_text.find(f"--- !u!1001 &{FIRE_UNCOMMON_INSTANCE_ID}\n") + 1
    )
    scene_text = scene_text[:insert_at] + "\n" + "\n".join(new_blocks) + scene_text[insert_at:]

    root_lines = "\n".join(f"  - {{fileID: {spec['instance_id']}}}" for spec in CATEGORIES)
    if not scene_text.endswith("\n"):
        scene_text += "\n"
    scene_text += root_lines + "\n"
    SCENE_PATH.write_text(scene_text, encoding="utf-8")
    print("Placed 5 prism elite cabinets in MainScene")


def main():
    os.chdir(ROOT)
    sys.path.insert(0, str(ROOT / "Tools"))

    print("== Import prism elite cards ==")
    per_category = import_cards()
    for spec in CATEGORIES:
        count = per_category.get(spec["id"], 0)
        if count != 50:
            raise SystemExit(f"{spec['id']} expected 50 cards, got {count}")

    print("== Copy 50-slot cabinets ==")
    copy_cabinets()
    print("== Place cabinets in MainScene ==")
    place_cabinets_in_scene()
    print("Done. Prism elite categories are catalog + cabinet only (no ground spawn).")


if __name__ == "__main__":
    main()
