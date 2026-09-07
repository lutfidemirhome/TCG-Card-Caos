#!/usr/bin/env python3
"""Clone five Japanese cabinets (separate assets) and drop them in MainScene center."""
import re
import shutil
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SHELF_CATS = ROOT / "Assets/Data/ShelfCategories/Japanese"
CABINETS = ROOT / "Assets/Prefabs/Cabinets/Cabinets_Japanese"
SIGN_TEX = ROOT / "Assets/Art/ShelfSigns/Textures/Japanese"
SIGN_MAT = ROOT / "Assets/Art/ShelfSigns/Materials/Japanese"
SCENE = ROOT / "Assets/Scenes/MainScene.unity"

GUID_RE = re.compile(r"^guid: ([0-9a-f]{32})$", re.M)

CABINETS_SPEC = [
    {
        "src_prefab": ROOT / "Assets/Prefabs/Cabinets/Cabinets_Dragon/Cabinets_DragonEpic.prefab",
        "src_cat": "dragon_epic",
        "src_sign": ROOT / "Assets/Art/ShelfSigns/Textures/Dragon/dragon_epic_sign.png",
        "src_mat": ROOT / "Assets/Art/ShelfSigns/Materials/Dragon/dragon_epic_sign.mat",
        "dst_id": "dragon_epic_japan",
        "dst_prefab_name": "Cabinets_DragonEpicJapan",
        "slots": 5,
        "x": -12.0,
    },
    {
        "src_prefab": ROOT / "Assets/Prefabs/Cabinets/Cabinets_Fire/Cabinets_FireMythicGold.prefab",
        "src_cat": "fire_mythic_gold",
        "src_sign": ROOT / "Assets/Art/ShelfSigns/Textures/Fire/fire_mythic_gold_sign.png",
        "src_mat": ROOT / "Assets/Art/ShelfSigns/Materials/Fire/fire_mythic_gold_sign.mat",
        "dst_id": "fire_mythic_gold_japan",
        "dst_prefab_name": "Cabinets_FireMythicGoldJapan",
        "slots": 3,
        "x": -6.0,
    },
    {
        "src_prefab": ROOT / "Assets/Prefabs/Cabinets/Cabinets_Ground/Cabinets_GroundMasterArt.prefab",
        "src_cat": "ground_master_art",
        "src_sign": ROOT / "Assets/Art/ShelfSigns/Textures/Ground/ground_master_art_sign.png",
        "src_mat": ROOT / "Assets/Art/ShelfSigns/Materials/Ground/ground_master_art_sign.mat",
        "dst_id": "ground_master_art_japan",
        "dst_prefab_name": "Cabinets_GroundMasterArtJapan",
        "slots": 3,
        "x": 0.0,
    },
    {
        "src_prefab": ROOT / "Assets/Prefabs/Cabinets/Cabinets_Ice/Cabinets_IcePrismElite.prefab",
        "src_cat": "ice_prism_elite",
        "src_sign": ROOT / "Assets/Art/ShelfSigns/Textures/Ice/ice_prism_elite_sign.png",
        "src_mat": ROOT / "Assets/Art/ShelfSigns/Materials/Ice/ice_prism_elite_sign.mat",
        "dst_id": "ice_prism_elite_japan",
        "dst_prefab_name": "Cabinets_IcePrismEliteJapan",
        "slots": 5,
        "x": 6.0,
    },
    {
        "src_prefab": ROOT / "Assets/Prefabs/Cabinets/Cabinets_Psychic/Cabinets_PsychicElite.prefab",
        "src_cat": "psychic_elite",
        "src_sign": ROOT / "Assets/Art/ShelfSigns/Textures/Psychic/psychic_elite_sign.png",
        "src_mat": ROOT / "Assets/Art/ShelfSigns/Materials/Psychic/psychic_elite_sign.mat",
        "dst_id": "psychic_elite_japan",
        "dst_prefab_name": "Cabinets_PsychicEliteJapan",
        "slots": 10,
        "x": 12.0,
    },
]

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

ROOT_GO = "5982988856245560815"
ROOT_TF = "6362469198736883541"


def new_guid() -> str:
    return uuid.uuid4().hex


def write_folder_meta(folder: Path):
    folder.mkdir(parents=True, exist_ok=True)
    meta = folder.with_suffix(folder.suffix + ".meta") if folder.suffix else Path(str(folder) + ".meta")
    if not meta.exists():
        meta.write_text(FOLDER_META.format(guid=new_guid()), encoding="utf-8")


def copy_with_new_meta(src: Path, dst: Path) -> str:
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dst)
    guid = new_guid()
    src_meta = src.with_name(src.name + ".meta")
    if src.suffix:
        src_meta = Path(str(src) + ".meta")
    dst_meta = Path(str(dst) + ".meta")
    text = src_meta.read_text(encoding="utf-8")
    text = GUID_RE.sub(f"guid: {guid}", text, count=1)
    dst_meta.write_text(text, encoding="utf-8")
    return guid


def prefab_instance_yaml(prefab_guid: str, name: str, instance_id: int, stripped_id: int, x: float) -> str:
    return f"""--- !u!1001 &{instance_id}
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {{fileID: 0}}
    m_Modifications:
    - target: {{fileID: {ROOT_GO}, guid: {prefab_guid}, type: 3}}
      propertyPath: m_Name
      value: {name}
      objectReference: {{fileID: 0}}
    - target: {{fileID: {ROOT_TF}, guid: {prefab_guid}, type: 3}}
      propertyPath: m_LocalPosition.x
      value: {x}
      objectReference: {{fileID: 0}}
    - target: {{fileID: {ROOT_TF}, guid: {prefab_guid}, type: 3}}
      propertyPath: m_LocalPosition.y
      value: 0
      objectReference: {{fileID: 0}}
    - target: {{fileID: {ROOT_TF}, guid: {prefab_guid}, type: 3}}
      propertyPath: m_LocalPosition.z
      value: 0
      objectReference: {{fileID: 0}}
    - target: {{fileID: {ROOT_TF}, guid: {prefab_guid}, type: 3}}
      propertyPath: m_LocalRotation.w
      value: 1
      objectReference: {{fileID: 0}}
    - target: {{fileID: {ROOT_TF}, guid: {prefab_guid}, type: 3}}
      propertyPath: m_LocalRotation.x
      value: 0
      objectReference: {{fileID: 0}}
    - target: {{fileID: {ROOT_TF}, guid: {prefab_guid}, type: 3}}
      propertyPath: m_LocalRotation.y
      value: 0
      objectReference: {{fileID: 0}}
    - target: {{fileID: {ROOT_TF}, guid: {prefab_guid}, type: 3}}
      propertyPath: m_LocalRotation.z
      value: 0
      objectReference: {{fileID: 0}}
    - target: {{fileID: {ROOT_TF}, guid: {prefab_guid}, type: 3}}
      propertyPath: m_LocalEulerAnglesHint.x
      value: 0
      objectReference: {{fileID: 0}}
    - target: {{fileID: {ROOT_TF}, guid: {prefab_guid}, type: 3}}
      propertyPath: m_LocalEulerAnglesHint.y
      value: 0
      objectReference: {{fileID: 0}}
    - target: {{fileID: {ROOT_TF}, guid: {prefab_guid}, type: 3}}
      propertyPath: m_LocalEulerAnglesHint.z
      value: 0
      objectReference: {{fileID: 0}}
    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents: []
  m_SourcePrefab: {{fileID: 100100000, guid: {prefab_guid}, type: 3}}
--- !u!4 &{stripped_id} stripped
Transform:
  m_CorrespondingSourceObject: {{fileID: {ROOT_TF}, guid: {prefab_guid}, type: 3}}
  m_PrefabInstance: {{fileID: {instance_id}}}
  m_PrefabAsset: {{fileID: 0}}
"""


def main():
    for folder in (SHELF_CATS, CABINETS, SIGN_TEX, SIGN_MAT):
        write_folder_meta(folder)

    created = []
    for spec in CABINETS_SPEC:
        src_cat = ROOT / "Assets/Data/ShelfCategories" / f"{spec['src_cat']}.asset"
        sign_stem = f"{spec['dst_id']}_sign"
        tex_guid = copy_with_new_meta(spec["src_sign"], SIGN_TEX / f"{sign_stem}.png")
        mat_text = spec["src_mat"].read_text(encoding="utf-8")
        src_tex_guid = GUID_RE.search((Path(str(spec["src_sign"]) + ".meta")).read_text(encoding="utf-8")).group(1)
        mat_text = mat_text.replace(f"m_Name: {spec['src_cat']}_sign", f"m_Name: {sign_stem}")
        mat_text = mat_text.replace(src_tex_guid, tex_guid)
        dst_mat = SIGN_MAT / f"{sign_stem}.mat"
        dst_mat.write_text(mat_text, encoding="utf-8")
        mat_guid = new_guid()
        src_mat_meta = Path(str(spec["src_mat"]) + ".meta").read_text(encoding="utf-8")
        Path(str(dst_mat) + ".meta").write_text(GUID_RE.sub(f"guid: {mat_guid}", src_mat_meta, count=1), encoding="utf-8")

        cat_text = src_cat.read_text(encoding="utf-8")
        cat_text = cat_text.replace(f"m_Name: {spec['src_cat']}", f"m_Name: {spec['dst_id']}")
        cat_text = re.sub(rf"categoryId: {re.escape(spec['src_cat'])}", f"categoryId: {spec['dst_id']}", cat_text)
        cat_text = re.sub(r"slotsPerRow: \d+", f"slotsPerRow: {spec['slots']}", cat_text)
        cat_text = re.sub(
            r"signMaterial: \{fileID: 2100000, guid: [0-9a-f]{32}, type: 2\}",
            f"signMaterial: {{fileID: 2100000, guid: {mat_guid}, type: 2}}",
            cat_text,
        )
        dst_cat = SHELF_CATS / f"{spec['dst_id']}.asset"
        dst_cat.write_text(cat_text, encoding="utf-8")
        cat_guid = new_guid()
        Path(str(dst_cat) + ".meta").write_text(
            GUID_RE.sub(f"guid: {cat_guid}", (src_cat.with_suffix(".asset.meta")).read_text(encoding="utf-8"), count=1),
            encoding="utf-8",
        )

        src_prefab_name = spec["src_prefab"].stem
        prefab_text = spec["src_prefab"].read_text(encoding="utf-8")
        prefab_text = prefab_text.replace(src_prefab_name, spec["dst_prefab_name"])
        prefab_text = prefab_text.replace(f"categoryId: {spec['src_cat']}", f"categoryId: {spec['dst_id']}")
        prefab_text = re.sub(
            r"categoryDefinition: \{fileID: 11400000, guid: [0-9a-f]{32}, type: 2\}",
            f"categoryDefinition: {{fileID: 11400000, guid: {cat_guid}, type: 2}}",
            prefab_text,
            count=1,
        )
        src_mat_guid = GUID_RE.search(Path(str(spec["src_mat"]) + ".meta").read_text(encoding="utf-8")).group(1)
        prefab_text = prefab_text.replace(src_mat_guid, mat_guid)

        dst_prefab = CABINETS / f"{spec['dst_prefab_name']}.prefab"
        dst_prefab.write_text(prefab_text, encoding="utf-8")
        prefab_guid = new_guid()
        Path(str(dst_prefab) + ".meta").write_text(
            GUID_RE.sub(f"guid: {prefab_guid}", Path(str(spec["src_prefab"]) + ".meta").read_text(encoding="utf-8"), count=1),
            encoding="utf-8",
        )
        created.append({**spec, "prefab_guid": prefab_guid})
        print(f"  {spec['dst_prefab_name']}  {spec['dst_id']}  slots/row={spec['slots']}  guid={prefab_guid}")

    scene = SCENE.read_text(encoding="utf-8")
    if "Cabinets_DragonEpicJapan" in scene:
        print("Scene already has Japanese cabinets; skipped scene insert.")
        return

    blocks = []
    root_ids = []
    for i, spec in enumerate(created):
        instance_id = 941000001 + i * 2
        stripped_id = instance_id + 1
        root_ids.append(str(instance_id))
        blocks.append(
            prefab_instance_yaml(spec["prefab_guid"], spec["dst_prefab_name"], instance_id, stripped_id, spec["x"])
        )

    marker = "--- !u!1660057539 &9223372036854775807\n"
    if marker not in scene:
        raise SystemExit("SceneRoots marker not found")
    scene = scene.replace(marker, "".join(blocks) + marker, 1)
    scene = scene.rstrip() + "\n" + "".join(f"  - {{fileID: {rid}}}\n" for rid in root_ids)
    SCENE.write_text(scene, encoding="utf-8")
    print(f"Placed {len(created)} Japanese cabinets at scene center (z=0).")


if __name__ == "__main__":
    main()
