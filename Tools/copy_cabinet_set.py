#!/usr/bin/env python3
"""Copy a full cabinet set: sign PNGs/materials, shelf categories, and prefabs.

Each cabinet type keeps its own assets so editing one set never affects another.

Usage:
  python3 Tools/copy_cabinet_set.py grass --from fire
  python3 Tools/copy_cabinet_set.py water --from normal
"""
import argparse
import re
import shutil
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TEXTURES = ROOT / "Assets/Art/ShelfSigns/Textures"
MATERIALS = ROOT / "Assets/Art/ShelfSigns/Materials"
SHELF_CATS = ROOT / "Assets/Data/ShelfCategories"
CABINETS = ROOT / "Assets/Prefabs/Cabinets"

GUID_RE = re.compile(r"^guid: ([0-9a-f]{32})$", re.M)
CATEGORY_SCRIPT_GUID = "b4e8f1a23c5d6470891ab2c3d4e5f678"

RARITIES = ["common", "uncommon", "rare"]


def new_guid() -> str:
    return uuid.uuid4().hex


def write_meta(path: Path, template: Path, guid: str):
    text = template.read_text(encoding="utf-8")
    text = GUID_RE.sub(f"guid: {guid}", text, count=1)
    path.write_text(text, encoding="utf-8")


def copy_texture(src_stem: str, dst_stem: str) -> str:
    shutil.copy2(TEXTURES / f"{src_stem}.png", TEXTURES / f"{dst_stem}.png")
    guid = new_guid()
    write_meta(TEXTURES / f"{dst_stem}.png.meta", TEXTURES / f"{src_stem}.png.meta", guid)
    return guid


def copy_material(src_stem: str, dst_stem: str, texture_guid: str) -> str:
    text = (MATERIALS / f"{src_stem}.mat").read_text(encoding="utf-8")
    text = text.replace(f"m_Name: {src_stem}", f"m_Name: {dst_stem}")

    src_tex_guid = None
    for line in text.splitlines():
        if "2800000" in line and "guid:" in line:
            m = re.search(r"guid: ([0-9a-f]{32})", line)
            if m:
                src_tex_guid = m.group(1)
                break
    if src_tex_guid:
        text = text.replace(src_tex_guid, texture_guid)

    (MATERIALS / f"{dst_stem}.mat").write_text(text, encoding="utf-8")
    mat_guid = new_guid()
    write_meta(MATERIALS / f"{dst_stem}.mat.meta", MATERIALS / f"{src_stem}.mat.meta", mat_guid)
    return mat_guid


def copy_shelf_category(src_id: str, dst_id: str, material_guid: str) -> str:
    src_path = SHELF_CATS / f"{src_id}.asset"
    dst_path = SHELF_CATS / f"{dst_id}.asset"
    text = src_path.read_text(encoding="utf-8")
    text = text.replace(f"m_Name: {src_id}", f"m_Name: {dst_id}")
    text = re.sub(rf"categoryId: {re.escape(src_id)}", f"categoryId: {dst_id}", text)
    text = re.sub(
        r"signMaterial: \{fileID: 2100000, guid: [0-9a-f]{32}, type: 2\}",
        f"signMaterial: {{fileID: 2100000, guid: {material_guid}, type: 2}}",
        text,
    )
    dst_path.write_text(text, encoding="utf-8")
    cat_guid = new_guid()
    write_meta(dst_path.with_suffix(".asset.meta"), src_path.with_suffix(".asset.meta"), cat_guid)
    return cat_guid


def ensure_folder_meta(folder: Path, template_folder: Path):
    meta = folder.with_suffix(".meta")
    if meta.exists():
        return
    guid = new_guid()
    write_meta(meta, template_folder.with_suffix(".meta"), guid)


def main():
    parser = argparse.ArgumentParser(description="Copy a full cabinet set (signs + categories + prefabs).")
    parser.add_argument("target", help="New set name, e.g. grass")
    parser.add_argument("--from", dest="source", default="fire", help="Source set prefix (default: fire)")
    args = parser.parse_args()

    src = args.source.lower()
    dst = args.target.lower()
    src_title = src.title()
    dst_title = dst.title()

    src_cab_dir = CABINETS / f"Cabinets_{src_title}"
    dst_cab_dir = CABINETS / f"Cabinets_{dst_title}"
    if not src_cab_dir.exists():
        raise SystemExit(f"Source folder not found: {src_cab_dir}")

    dst_cab_dir.mkdir(parents=True, exist_ok=True)
    ensure_folder_meta(dst_cab_dir, src_cab_dir)

    created = []
    for rarity in RARITIES:
        src_tex = f"{src}_{rarity}_sign"
        dst_tex = f"{dst}_{rarity}_sign"
        src_mat = f"{src}_{rarity}_sign"
        dst_mat = f"{dst}_{rarity}_sign"
        src_cat = f"{src}_{rarity}"
        dst_cat = f"{dst}_{rarity}"

        copy_texture(src_tex, dst_tex)
        mat_guid = copy_material(src_mat, dst_mat, GUID_RE.search(
            (TEXTURES / f"{dst_tex}.png.meta").read_text(encoding="utf-8")
        ).group(1))
        cat_guid = copy_shelf_category(src_cat, dst_cat, mat_guid)

        src_prefab = f"Cabinets_{src_title}{rarity.title()}"
        dst_prefab = f"Cabinets_{dst_title}{rarity.title()}"
        src_mat_guid = GUID_RE.search((MATERIALS / f"{src_mat}.mat.meta").read_text(encoding="utf-8")).group(1)
        dst_mat_guid = GUID_RE.search((MATERIALS / f"{dst_mat}.mat.meta").read_text(encoding="utf-8")).group(1)

        src_path = src_cab_dir / f"{src_prefab}.prefab"
        dst_path = dst_cab_dir / f"{dst_prefab}.prefab"
        text = src_path.read_text(encoding="utf-8")
        text = text.replace(src_prefab, dst_prefab)
        text = text.replace(src_cat, dst_cat)
        text = re.sub(
            r"categoryDefinition: \{fileID: 11400000, guid: [0-9a-f]{32}, type: 2\}",
            f"categoryDefinition: {{fileID: 11400000, guid: {cat_guid}, type: 2}}",
            text,
            count=1,
        )
        text = text.replace(src_mat_guid, dst_mat_guid)
        dst_path.write_text(text, encoding="utf-8")
        prefab_guid = new_guid()
        write_meta(dst_path.with_suffix(".prefab.meta"), src_path.with_suffix(".prefab.meta"), prefab_guid)

        created.append((dst_prefab, dst_cat, dst_tex, dst_mat))

    print(f"Created Cabinets_{dst_title}/ with {len(created)} prefabs from {src}:")
    for row in created:
        print(" ", row)


if __name__ == "__main__":
    main()
