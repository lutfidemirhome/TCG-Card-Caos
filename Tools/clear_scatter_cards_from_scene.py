#!/usr/bin/env python3
"""Remove all children under ScatteredCards from a Unity scene YAML file."""

from __future__ import annotations

import re
import sys
from pathlib import Path

SCATTER_ROOT_NAME = "ScatteredCards"
SCATTER_TRANSFORM_ID = 929101152


def parse_blocks(text: str) -> list[tuple[str, int | None, str]]:
    parts = text.split("--- !u!")
    blocks: list[tuple[str, int | None, str]] = []
    for part in parts:
        if not part.strip():
            continue
        header, _, body = part.partition("\n")
        match = re.match(r"(\d+) &(\d+)", header.strip())
        if not match:
            blocks.append((header, None, body))
            continue
        blocks.append((header, int(match.group(2)), body))
    return blocks


def find_scatter_transform_id(blocks: list[tuple[str, int | None, str]]) -> int | None:
    for header, block_id, body in blocks:
        if block_id is None:
            continue
        if header.startswith("1 ") and f"\n  m_Name: {SCATTER_ROOT_NAME}\n" in body:
            transform_match = re.search(r"- component: \{fileID: (\d+)\}", body)
            if transform_match:
                return int(transform_match.group(1))
    return None


def collect_child_transform_ids(blocks: list[tuple[str, int | None, str]], parent_transform_id: int) -> set[int]:
    child_ids: set[int] = set()
    for header, block_id, body in blocks:
        if block_id is None or not header.startswith("4 "):
            continue
        if f"m_Father: {{fileID: {parent_transform_id}}}" in body:
            child_ids.add(block_id)
    return child_ids


def collect_game_object_ids(blocks: list[tuple[str, int | None, str]], transform_ids: set[int]) -> set[int]:
    game_object_ids: set[int] = set()
    for header, block_id, body in blocks:
        if block_id is None or not header.startswith("1 "):
            continue
        for transform_id in transform_ids:
            if f"- component: {{fileID: {transform_id}}}" in body:
                game_object_ids.add(block_id)
                break
    return game_object_ids


def collect_component_ids(blocks: list[tuple[str, int | None, str]], game_object_ids: set[int]) -> set[int]:
    component_ids: set[int] = set()
    for header, block_id, body in blocks:
        if block_id is None or not header.startswith("1 "):
            continue
        if block_id not in game_object_ids:
            continue
        for match in re.finditer(r"- component: \{fileID: (\d+)\}", body):
            component_id = int(match.group(1))
            if component_id != block_id:
                component_ids.add(component_id)
    return component_ids


def clear_scatter_children(body: str, scatter_transform_id: int) -> str:
    pattern = (
        rf"(--- !u!4 &{scatter_transform_id}\nTransform:[\s\S]*?  m_Children:\n)"
        rf"([\s\S]*?)(  m_Father:)"
    )
    match = re.search(pattern, body)
    if not match:
        return body
    return body[: match.start(2)] + body[match.end(2) :]


def main() -> int:
    scene_path = Path(sys.argv[1] if len(sys.argv) > 1 else "Assets/Scenes/MainScene.unity")
    if not scene_path.exists():
        print(f"Missing scene: {scene_path}")
        return 1

    text = scene_path.read_text(encoding="utf-8")
    blocks = parse_blocks(text)

    scatter_transform_id = find_scatter_transform_id(blocks) or SCATTER_TRANSFORM_ID
    child_transform_ids = collect_child_transform_ids(blocks, scatter_transform_id)
    game_object_ids = collect_game_object_ids(blocks, child_transform_ids)
    component_ids = collect_component_ids(blocks, game_object_ids)

    remove_ids = set(child_transform_ids)
    remove_ids.update(game_object_ids)
    remove_ids.update(component_ids)

    kept_blocks: list[str] = []
    removed = 0
    for header, block_id, body in blocks:
        if block_id is not None and block_id in remove_ids:
            removed += 1
            continue

        if block_id == scatter_transform_id and header.startswith("4 "):
            body = re.sub(r"  m_Children:\n(?:  - \{fileID: \d+\}\n)+", "  m_Children: []\n", body)

        if block_id is None:
            kept_blocks.append(body if not header.strip() else f"--- !u!{header}\n{body}")
        else:
            kept_blocks.append(f"--- !u!{header}\n{body}")

    output = kept_blocks[0] if kept_blocks else ""
    if len(kept_blocks) > 1:
        output = kept_blocks[0] + "".join(kept_blocks[1:])

    scene_path.write_text(output, encoding="utf-8")
    print(f"Cleared {removed} YAML blocks ({len(child_transform_ids)} scatter cards) from {scene_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
