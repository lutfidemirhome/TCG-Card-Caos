#!/usr/bin/env python3
"""Copy Normal shelf sign textures/materials for a new cabinet set (e.g. fire_*).

Each cabinet type must have its own sign PNG + material so editing one set
never affects another. Updates matching Assets/Data/ShelfCategories/*.asset.

Usage:
  python3 Tools/copy_shelf_sign_set.py fire
  python3 Tools/copy_shelf_sign_set.py water --from-prefix normal
"""
import argparse
import re
import shutil
import sys
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "Tools"))
from shelf_sign_paths import material_path, texture_path

SHELF_CATS = ROOT / "Assets/Data/ShelfCategories"
GUID_RE = re.compile(r"^guid: ([0-9a-f]{32})$", re.M)

RARITY_SOURCES = [
    ("common", "normal_common_sign", "normal_common_sign"),
    ("uncommon", "normal_uncommon_sign", "normal_uncommon_sign"),
    ("rare", "normal_common_rare", "normal_rare_sign"),
]


def new_guid():
    return uuid.uuid4().hex


def copy_texture(src_stem: str, dst_stem: str) -> str:
    src = texture_path(src_stem)
    dst = texture_path(dst_stem)
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dst)
    meta = src.with_suffix(".png.meta").read_text(encoding="utf-8")
    guid = new_guid()
    meta = GUID_RE.sub(f"guid: {guid}", meta, count=1)
    dst.with_suffix(".png.meta").write_text(meta, encoding="utf-8")
    return guid


def copy_material(src_stem: str, dst_stem: str, texture_guid: str) -> str:
    src = material_path(src_stem)
    dst = material_path(dst_stem)
    dst.parent.mkdir(parents=True, exist_ok=True)
    text = src.read_text(encoding="utf-8")
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
    dst.write_text(text, encoding="utf-8")

    meta = src.with_suffix(".mat.meta").read_text(encoding="utf-8")
    mat_guid = new_guid()
    meta = GUID_RE.sub(f"guid: {mat_guid}", meta, count=1)
    dst.with_suffix(".mat.meta").write_text(meta, encoding="utf-8")
    return mat_guid


def update_shelf_category(category_id: str, material_guid: str):
    path = SHELF_CATS / f"{category_id}.asset"
    text = path.read_text(encoding="utf-8")
    text = re.sub(
        r"signMaterial: \{fileID: 2100000, guid: [0-9a-f]{32}, type: 2\}",
        f"signMaterial: {{fileID: 2100000, guid: {material_guid}, type: 2}}",
        text,
    )
    path.write_text(text, encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description="Copy shelf sign set for a new cabinet type.")
    parser.add_argument("target_prefix", help="New prefix, e.g. fire")
    parser.add_argument("--from-prefix", default="normal", help="Source prefix (default: normal)")
    args = parser.parse_args()

    for rarity, src_tex, src_mat in RARITY_SOURCES:
        dst_stem = f"{args.target_prefix}_{rarity}_sign"
        category_id = f"{args.target_prefix}_{rarity}"
        tex_guid = copy_texture(src_tex, dst_stem)
        mat_guid = copy_material(src_mat, dst_stem, tex_guid)
        update_shelf_category(category_id, mat_guid)
        print(f"{category_id}: {dst_stem}.png + {dst_stem}.mat")


if __name__ == "__main__":
    main()
