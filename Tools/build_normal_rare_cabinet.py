#!/usr/bin/env python3
"""Build Cabinet_NormalRare.prefab from Uncommon by removing slot columns 3-4 (keep 3 seats per row)."""

import copy
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "Assets/Prefabs/Cabinets/Cabinets_Normal/Cabinets_NormalUncommon.prefab"
DST = ROOT / "Assets/Prefabs/Cabinets/Cabinets_Normal/Cabinets_NormalRare.prefab"

SLOT_NAME_RE = re.compile(r"^CardShelfSlot_\d+_(\d+)$")
REMOVE_COLUMN_MIN = 3
FILEID_REF_RE = re.compile(r"\{fileID: (\d+)")
GAMEOBJECT_NAME_RE = re.compile(r"^  m_Name: (.+)$")
COMPONENT_LINE_RE = re.compile(r"  - component: \{fileID: (\d+)\}")
CHILD_LINE_RE = re.compile(r"  - \{fileID: (\d+)\}")
GO_FILEID_RE = re.compile(r"^  m_GameObject: \{fileID: (\d+)\}")

NEW_CATEGORY_GUID = "7f3a9c2e1b4d6f8a9c0e5d2b4f7a3c01"
OLD_CATEGORY_GUID = "f8761c6dee694106a48ac6f7b53e3540"
OLD_SIGN_MAT_GUID = "68a0d08112704ebd803d29166bd14653"
NEW_SIGN_MAT_GUID = "da76f1683bcd4e568167bc2e05d3d2b5"
GUID_RE = re.compile(r"^[0-9a-f]{32}$")


def assert_valid_guid(guid: str, label: str) -> None:
    if not GUID_RE.match(guid):
        raise ValueError(
            f"{label} geçersiz GUID: '{guid}' ({len(guid)} karakter). "
            "Unity GUID tam 32 hex karakter olmalı."
        )


def parse_blocks(text: str):
    parts = text.split("--- !u!")
    header = parts[0]
    blocks = []
    for part in parts[1:]:
        block = part.strip("\n")
        if not block:
            continue
        m = re.match(r"(\d+) &(\d+)( stripped)?\n", block)
        if not m:
            continue
        type_id, file_id = m.group(1), int(m.group(2))
        blocks.append({"type": type_id, "id": file_id, "text": block})
    return header, blocks


def block_lines(block):
    return block["text"].splitlines()


def get_gameobject_name(block):
    for line in block_lines(block):
        m = GAMEOBJECT_NAME_RE.match(line)
        if m:
            return m.group(1)
    return None


def get_component_ids(block):
    ids = []
    in_components = False
    for line in block_lines(block):
        if line.strip() == "m_Component:":
            in_components = True
            continue
        if in_components:
            m = COMPONENT_LINE_RE.match(line)
            if m:
                ids.append(int(m.group(1)))
            elif line and not line.startswith("  - "):
                break
    return ids


def get_transform_children(block):
    ids = []
    in_children = False
    for line in block_lines(block):
        if line.strip() == "m_Children:":
            in_children = True
            continue
        if in_children:
            m = CHILD_LINE_RE.match(line)
            if m:
                ids.append(int(m.group(1)))
            elif line and not line.startswith("  - "):
                break
    return ids


def get_gameobject_ref(block):
    for line in block_lines(block):
        m = GO_FILEID_RE.match(line)
        if m:
            return int(m.group(1))
    return None


def collect_subtree(root_id, by_id):
    remove_ids = set()
    stack = [root_id]

    while stack:
        current = stack.pop()
        if current in remove_ids:
            continue
        block = by_id.get(current)
        if not block:
            continue

        remove_ids.add(current)

        if block["type"] == "1":
            for comp_id in get_component_ids(block):
                if comp_id not in remove_ids:
                    stack.append(comp_id)
        elif block["type"] == "4":
            go_id = get_gameobject_ref(block)
            if go_id is not None and go_id not in remove_ids:
                stack.append(go_id)
            for child_id in get_transform_children(block):
                if child_id not in remove_ids:
                    stack.append(child_id)

    return remove_ids


def strip_fileid_refs(text, remove_ids):
    lines = []
    for line in text.splitlines():
        refs = [int(m.group(1)) for m in FILEID_REF_RE.finditer(line)]
        if any(ref in remove_ids for ref in refs):
            if CHILD_LINE_RE.match(line) or COMPONENT_LINE_RE.match(line):
                continue
        lines.append(line)
    return "\n".join(lines)


def clean_kept_blocks(blocks, remove_ids):
    cleaned = []
    for block in blocks:
        if block["id"] in remove_ids:
            continue
        copied = copy.copy(block)
        copied["text"] = strip_fileid_refs(block["text"], remove_ids)
        cleaned.append(copied)
    return cleaned


def rebuild(header, blocks):
    out = [header.rstrip("\n")]
    for block in blocks:
        out.append("--- !u!" + block["text"])
    return "\n".join(out) + "\n"


def main():
    assert_valid_guid(NEW_CATEGORY_GUID, "NEW_CATEGORY_GUID")
    assert_valid_guid(OLD_CATEGORY_GUID, "OLD_CATEGORY_GUID")
    assert_valid_guid(OLD_SIGN_MAT_GUID, "OLD_SIGN_MAT_GUID")
    assert_valid_guid(NEW_SIGN_MAT_GUID, "NEW_SIGN_MAT_GUID")

    text = SRC.read_text(encoding="utf-8")
    header, blocks = parse_blocks(text)
    by_id = {b["id"]: b for b in blocks}

    remove_ids = set()
    for block in blocks:
        if block["type"] != "1":
            continue
        name = get_gameobject_name(block)
        if not name:
            continue
        m = SLOT_NAME_RE.match(name)
        if not m:
            continue
        if int(m.group(1)) >= REMOVE_COLUMN_MIN:
            remove_ids |= collect_subtree(block["id"], by_id)

    kept_blocks = clean_kept_blocks(blocks, remove_ids)
    out_text = rebuild(header, kept_blocks)

    out_text = out_text.replace("Cabinets_NormalUncommon", "Cabinets_NormalRare")
    out_text = out_text.replace("categoryId: normal_uncommon", "categoryId: normal_rare")
    out_text = out_text.replace(
        f"guid: {OLD_CATEGORY_GUID}",
        f"guid: {NEW_CATEGORY_GUID}",
    )
    out_text = out_text.replace(
        f"guid: {OLD_SIGN_MAT_GUID}",
        f"guid: {NEW_SIGN_MAT_GUID}",
    )

    DST.write_text(out_text, encoding="utf-8")
    print(f"Wrote {DST} (removed {len(remove_ids)} objects, kept {len(kept_blocks)} blocks)")


if __name__ == "__main__":
    main()
