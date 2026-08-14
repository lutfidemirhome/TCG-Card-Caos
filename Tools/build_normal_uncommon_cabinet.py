#!/usr/bin/env python3
"""Build Cabinet_NormalUncommon.prefab from Common by removing slot columns 5-9."""

import copy
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "Assets/Prefabs/Cabinets/Cabinets_Normal/Cabinets_NormalCommon.prefab"
DST = ROOT / "Assets/Prefabs/Cabinets/Cabinets_Normal/Cabinets_NormalUncommon.prefab"

SLOT_NAME_RE = re.compile(r"^CardShelfSlot_\d+_(\d+)$")
REMOVE_COLUMN_MIN = 5
FILEID_REF_RE = re.compile(r"\{fileID: (\d+)")
GAMEOBJECT_NAME_RE = re.compile(r"^  m_Name: (.+)$")
COMPONENT_LINE_RE = re.compile(r"  - component: \{fileID: (\d+)\}")
CHILD_LINE_RE = re.compile(r"  - \{fileID: (\d+)\}")
GO_FILEID_RE = re.compile(r"^  m_GameObject: \{fileID: (\d+)\}")

NEW_CATEGORY_GUID = "f8761c6dee694106a48ac6f7b53e3540"
NEW_SIGN_MAT_GUID = "68a0d08112704ebd803d29166bd14653"


def parse_blocks(text: str):
    parts = text.split("--- !u!")
    header = parts[0]
    blocks = []
    for part in parts[1:]:
        block = part.strip("\n")
        if not block:
            continue
        m = re.match(r"(\d+) &(\d+)\n", block)
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


def rebuild(header, blocks):
    out = [header.rstrip("\n")]
    for block in blocks:
        out.append("--- !u!" + block["text"])
    return "\n".join(out) + "\n"


def main():
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

    kept_blocks = [b for b in blocks if b["id"] not in remove_ids]
    out_text = rebuild(header, kept_blocks)
    out_text = strip_fileid_refs(out_text, remove_ids)

    out_text = out_text.replace("Cabinets_NormalCommon", "Cabinets_NormalUncommon")
    out_text = out_text.replace("categoryId: normal_common", "categoryId: normal_uncommon")
    out_text = out_text.replace(
        "guid: c7d2a9e14b8f5420a91de3f4b5c6d789",
        f"guid: {NEW_CATEGORY_GUID}",
    )
    out_text = out_text.replace(
        "guid: e8f4a2b1c3d5476f9a0b1c2d3e4f5a67",
        f"guid: {NEW_SIGN_MAT_GUID}",
    )

    DST.write_text(out_text, encoding="utf-8")
    print(f"Wrote {DST} (removed {len(remove_ids)} objects, kept {len(kept_blocks)} blocks)")


if __name__ == "__main__":
    main()
