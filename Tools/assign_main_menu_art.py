#!/usr/bin/env python3
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, "Assets/UI/MainMenu/Art")
SCENE = os.path.join(ROOT, "Assets/Scenes/MenuScene.unity")
IMAGE_GUID = "fe87c0e1cc204ed48ad3b37840f39efc"

ASSIGNMENTS = {
    "Logo": "tcg_demo_logo.png",
    "Button_NewGame": "new_game_button.png",
    "Button_LoadGame": "load_game_button.png",
    "Button_Settings": "settings_button.png",
    "Button_Quit": "quit_button.png",
    "Button_Feedback": "feedback_button.png",
    "Panel_Roadmap": "full_release_text_bg.png",
    "Header": "planned_for_full_release_header.png",
    "Button_Discord": "discord_icon.png",
    "Button_TikTok": "tik_tok_icon.png",
    "Button_Instagram": "instagram_icon.png",
    "Button_YouTube": "youtube_icon.png",
}

FOLLOW_US_BG = "follow_us_bg.png"
WHITE = "{r: 1, g: 1, b: 1, a: 1}"


def read_guid(png_name):
    meta_path = os.path.join(ART, png_name + ".meta")
    meta = open(meta_path).read()
    match = re.search(r"^guid: (\w+)", meta, re.M)
    if not match:
        raise RuntimeError("No guid in " + meta_path)
    return match.group(1)


def sprite_ref(guid):
    return "{fileID: 21300000, guid: " + guid + ", type: 3}"


def fix_meta_files():
    for fname in os.listdir(ART):
        if not fname.endswith(".png.meta"):
            continue
        path = os.path.join(ART, fname)
        text = open(path).read()
        text = re.sub(r"spriteMode: \d+", "spriteMode: 1", text, count=1)
        if "spriteID: 5e97eb03825dee720800000000000000" not in text:
            text = re.sub(
                r"spriteID:.*",
                "spriteID: 5e97eb03825dee720800000000000000",
                text,
                count=1,
            )
        open(path, "w").write(text)


def patch_image_block(block, sprite, preserve_aspect=False):
    block = re.sub(
        r"m_Sprite: (\{[^\n]+\}|{fileID: 0})",
        "m_Sprite: " + sprite,
        block,
        count=1,
    )
    block = re.sub(r"m_Color: \{[^\n]+\}", "m_Color: " + WHITE, block, count=1)
    if preserve_aspect:
        if "m_PreserveAspect:" in block:
            block = re.sub(r"m_PreserveAspect: \d", "m_PreserveAspect: 1", block, count=1)
        else:
            block = block.replace(
                "m_RaycastTarget:",
                "m_PreserveAspect: 1\n  m_RaycastTarget:",
            )
    return block


def find_image_block(text, go_id):
    pattern = (
        r"(--- !u!114 &\d+\nMonoBehaviour:\n"
        r"(?:  .*\n)*?"
        r"  m_GameObject: {fileID: "
        + go_id
        + r"}\n"
        r"(?:  .*\n)*?"
        r"  m_Script: {fileID: 11500000, guid: "
        + IMAGE_GUID
        + r", type: 3}\n"
        r"(?:  .*\n)*?"
        r"  m_PixelsPerUnitMultiplier: \d+\n)"
    )
    return re.search(pattern, text)


def assign_sprites(text):
    for obj_name, png in ASSIGNMENTS.items():
        guid = read_guid(png)
        sprite = sprite_ref(guid)

        go_match = re.search(
            r"--- !u!1 &(\d+)\nGameObject:\n(?:  .*\n)*?  m_Name: "
            + re.escape(obj_name)
            + r"\n",
            text,
        )
        if not go_match:
            print("MISSING GO:", obj_name)
            continue

        go_id = go_match.group(1)
        img_match = find_image_block(text, go_id)
        if not img_match:
            print("MISSING IMAGE:", obj_name)
            continue

        new_img = patch_image_block(
            img_match.group(1),
            sprite,
            preserve_aspect=(obj_name == "Logo"),
        )
        text = text[: img_match.start(1)] + new_img + text[img_match.end(1) :]
        print("OK", obj_name)

    return text


def add_follow_us_background(text):
    if "m_Name: FollowUsBackground" in text:
        print("FollowUsBackground already exists")
        return text

    follow_match = re.search(
        r"--- !u!1 &(\d+)\nGameObject:[\s\S]*?  m_Name: Text_FollowUs\n",
        text,
    )
    if not follow_match:
        print("MISSING Text_FollowUs")
        return text

    follow_go = follow_match.group(1)
    rect_pattern = (
        r"--- !u!224 &(\d+)\nRectTransform:[\s\S]*?"
        r"m_GameObject: {fileID: "
        + follow_go
        + r"}\n[\s\S]*?"
        r"m_AnchoredPosition: {x: ([^,]+), y: ([^}]+)}\n"
        r"  m_SizeDelta: {x: ([^,]+), y: ([^}]+)}\n"
        r"  m_Pivot:"
    )
    rect_match = re.search(rect_pattern, text)
    if not rect_match:
        print("MISSING Text_FollowUs rect")
        return text

    follow_rect = rect_match.group(1)
    ay = rect_match.group(3)
    sx = rect_match.group(4)
    sy = rect_match.group(5)

    panel_match = re.search(
        r"--- !u!1 &(\d+)\nGameObject:[\s\S]*?  m_Name: Panel_Roadmap\n",
        text,
    )
    panel_go = panel_match.group(1)
    panel_pattern = (
        r"--- !u!224 &(\d+)\nRectTransform:[\s\S]*?"
        r"m_GameObject: {fileID: "
        + panel_go
        + r"}\n[\s\S]*?"
        r"m_Children:\n((?:  - {fileID: \d+}\n)+)"
    )
    panel_match2 = re.search(panel_pattern, text)
    panel_rect = panel_match2.group(1)
    children_block = panel_match2.group(2)

    follow_sprite = sprite_ref(read_guid(FOLLOW_US_BG))
    ids = [910000001, 910000002, 910000003, 910000004]

    insert = f"""--- !u!1 &{ids[0]}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {ids[1]}}}
  - component: {{fileID: {ids[2]}}}
  - component: {{fileID: {ids[3]}}}
  m_Layer: 0
  m_Name: FollowUsBackground
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{ids[1]}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {ids[0]}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {panel_rect}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0.5, y: 1}}
  m_AnchorMax: {{x: 0.5, y: 1}}
  m_AnchoredPosition: {{x: 0, y: {ay}}}
  m_SizeDelta: {{x: {sx}, y: {sy}}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{ids[2]}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {ids[0]}}}
  m_CullTransparentMesh: 1
--- !u!114 &{ids[3]}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {ids[0]}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {IMAGE_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {WHITE}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {follow_sprite}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
"""

    needle = "  - {fileID: " + follow_rect + "}\n"
    replacement = "  - {fileID: " + str(ids[1]) + "}\n" + needle
    text = text.replace(children_block, children_block.replace(needle, replacement, 1), 1)
    text = text.rstrip() + "\n" + insert
    print("OK FollowUsBackground")
    return text


def main():
    fix_meta_files()
    text = open(SCENE).read()
    text = assign_sprites(text)
    text = add_follow_us_background(text)
    open(SCENE, "w").write(text)
    print("Done:", SCENE)


if __name__ == "__main__":
    main()
