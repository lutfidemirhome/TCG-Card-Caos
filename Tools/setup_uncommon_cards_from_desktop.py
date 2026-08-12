#!/usr/bin/env python3
"""Copy Normal Uncommon card art into the project and generate CardDefinition assets."""
import os
import re
import shutil
import uuid

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC_ROOT = os.path.expanduser("~/Desktop/normal kare")
ART_ROOT = os.path.join(ROOT, "Assets/Art/Cards/Normal_Uncommon_Cards")
OUT_DIR = os.path.join(ROOT, "Assets/Resources/Cards/Definitions")
SCRIPT_GUID = "7d0dd49612e9649128599303e4c70cba"
MAX_SLOT = 5
FILE_PATTERN = re.compile(r"^Normal_(?P<character>[a-z]+)_(?P<slot>\d+)\.png$", re.IGNORECASE)


def read_guid(meta_path):
    with open(meta_path, encoding="utf-8") as handle:
        for line in handle:
            if line.startswith("guid:"):
                return line.split(":", 1)[1].strip()
    raise ValueError(f"No guid in {meta_path}")


def ensure_texture_meta(png_path):
    meta_path = png_path + ".meta"
    if os.path.exists(meta_path):
        return read_guid(meta_path)

    meta_yaml = f"""fileFormatVersion: 2
guid: {uuid.uuid4().hex}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 11
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: -1
    aniso: -1
    mipBias: -100
    wrapU: -1
    wrapV: -1
    wrapW: -1
  nPOTScale: 1
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: 0
  textureShape: 1
  singleChannelComponent: 0
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  applyGammaDecoding: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: 
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
  spritePackingTag: 
  pSDRemoveMatte: 0
  pSDShowRemoveMatteOption: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
    with open(meta_path, "w", encoding="utf-8") as handle:
        handle.write(meta_yaml)
    return read_guid(meta_path)


def display_name(character, slot):
    return f"{character.capitalize()} {slot}"


def write_asset(asset_path, meta_path, definition_id, character, slot, texture_guid):
    os.makedirs(OUT_DIR, exist_ok=True)
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


def clean_art_root():
    if not os.path.isdir(ART_ROOT):
        os.makedirs(ART_ROOT, exist_ok=True)
        return

    for entry in os.listdir(ART_ROOT):
        path = os.path.join(ART_ROOT, entry)
        if os.path.isdir(path):
            shutil.rmtree(path)
        elif entry.endswith(".png") or entry.endswith(".meta"):
            os.remove(path)


def remove_legacy_definitions():
    if not os.path.isdir(OUT_DIR):
        return 0

    removed = 0
    for filename in os.listdir(OUT_DIR):
        if filename.startswith("normal_uncommon_") and (
            filename.endswith(".asset") or filename.endswith(".asset.meta")
        ):
            os.remove(os.path.join(OUT_DIR, filename))
            removed += 1
    return removed


def normalize_target_name(filename):
    match = FILE_PATTERN.match(filename)
    if not match:
        return None

    character = match.group("character").lower()
    slot = int(match.group("slot"))
    return f"Normal_{character}_{slot}.png"


def main():
    if not os.path.isdir(SRC_ROOT):
        raise SystemExit(f"Missing source folder: {SRC_ROOT}")

    clean_art_root()
    removed = remove_legacy_definitions()

    copied = 0
    for dirpath, _, filenames in os.walk(SRC_ROOT):
        for filename in sorted(filenames):
            if not filename.lower().endswith(".png"):
                continue

            target_name = normalize_target_name(filename)
            if target_name is None:
                print(f"Skip (name): {filename}")
                continue

            slot = int(target_name.rsplit("_", 1)[-1].split(".")[0])
            if slot < 1 or slot > MAX_SLOT:
                print(f"Skip (slot): {filename}")
                continue

            character = target_name[len("Normal_") : target_name.rfind("_")]
            src_path = os.path.join(dirpath, filename)
            family_dir = os.path.join(ART_ROOT, character)
            os.makedirs(family_dir, exist_ok=True)
            dst_path = os.path.join(family_dir, target_name)
            shutil.copy2(src_path, dst_path)
            copied += 1

            definition_id = os.path.splitext(target_name)[0]
            texture_guid = ensure_texture_meta(dst_path)
            asset_path = os.path.join(OUT_DIR, definition_id + ".asset")
            meta_path = asset_path + ".meta"
            write_asset(asset_path, meta_path, definition_id, character, slot, texture_guid)

    print(
        f"Copied {copied} PNGs to {ART_ROOT}, "
        f"removed {removed} legacy definition files, "
        f"wrote {copied} CardDefinition assets to {OUT_DIR}"
    )


if __name__ == "__main__":
    main()
