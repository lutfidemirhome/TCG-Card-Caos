#!/usr/bin/env python3
"""Copy Desktop Japanese normal cards + back art, then generate CardDefinitions.

Does not spawn cards or add Japanese categories to scatter.
English card folders and backs stay untouched.
"""
import os
import re
import shutil
import uuid

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SOURCE_ROOT = os.path.expanduser("~/Desktop/japon kartlar")
BACK_SOURCE = os.path.expanduser(
    "~/Desktop/kart arka kapak japonca/kart_arka_template-japon.png"
)
ART_ROOT = os.path.join(ROOT, "Assets/Art/Cards/Japanese")
DEF_ROOT = os.path.join(ROOT, "Assets/Resources/Cards/Definitions/Japanese")
BACK_RESOURCE_DIR = os.path.join(ROOT, "Assets/Resources/Cards/JapaneseCardBack")
BACK_ART_NAME = "kart_arka_template_japon.png"
SCRIPT_GUID = "7d0dd49612e9649128599303e4c70cba"
GUID_RE = re.compile(r"^[0-9a-f]{32}$")

CATEGORIES = {
    "dragon_epic_japan": 5,
    "fire_mythic_gold_japan": 3,
    "ground_master_art_japan": 3,
    "ice_prism_elite_japan": 5,
    "psychic_elite_japan": 10,
}

PNG_RE = re.compile(
    r"^(?P<category>"
    + "|".join(re.escape(name) for name in CATEGORIES)
    + r")_(?P<character>[a-z0-9]+)_(?P<slot>\d+)\.png$",
    re.IGNORECASE,
)

TEXTURE_META_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
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
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: {max_size}
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
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
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: {max_size}
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: Standalone
    maxTextureSize: {max_size}
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData: 
    physicsShape: []
    bones: []
    spriteID: 
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

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


def new_guid():
    return uuid.uuid4().hex


def read_guid(meta_path):
    with open(meta_path, encoding="utf-8") as handle:
        for line in handle:
            if line.startswith("guid:"):
                guid = line.split(":", 1)[1].strip()
                if GUID_RE.match(guid):
                    return guid
    raise ValueError(f"No guid in {meta_path}")


def ensure_folder_meta(folder_path):
    os.makedirs(folder_path, exist_ok=True)
    meta_path = folder_path + ".meta"
    if os.path.exists(meta_path):
        return read_guid(meta_path)
    guid = new_guid()
    with open(meta_path, "w", encoding="utf-8") as handle:
        handle.write(FOLDER_META_TEMPLATE.format(guid=guid))
    return guid


def ensure_texture_meta(png_path, max_size):
    meta_path = png_path + ".meta"
    if os.path.exists(meta_path):
        return read_guid(meta_path)
    guid = new_guid()
    with open(meta_path, "w", encoding="utf-8") as handle:
        handle.write(TEXTURE_META_TEMPLATE.format(guid=guid, max_size=max_size))
    return guid


def copy_png(src_png, dest_png, max_size):
    if not os.path.exists(dest_png) or os.path.getsize(dest_png) != os.path.getsize(src_png):
        shutil.copy2(src_png, dest_png)
        copied = True
    else:
        copied = False
    guid = ensure_texture_meta(dest_png, max_size)
    return copied, guid


def display_name(character, slot):
    return f"{character.capitalize()} {slot}"


def write_definition(def_id, character, slot, category, texture_guid, dest_dir):
    ensure_folder_meta(dest_dir)
    asset_path = os.path.join(dest_dir, def_id + ".asset")
    meta_path = asset_path + ".meta"
    asset_guid = read_guid(meta_path) if os.path.exists(meta_path) else new_guid()

    with open(asset_path, "w", encoding="utf-8") as handle:
        handle.write(
            ASSET_TEMPLATE.format(
                script_guid=SCRIPT_GUID,
                definition_id=def_id,
                display_name=display_name(character, slot),
                category=category,
                slot=slot,
                texture_guid=texture_guid,
            )
        )
    with open(meta_path, "w", encoding="utf-8") as handle:
        handle.write(ASSET_META_TEMPLATE.format(guid=asset_guid))


def import_back():
    if not os.path.isfile(BACK_SOURCE):
        raise SystemExit(f"Japanese back missing: {BACK_SOURCE}")

    ensure_folder_meta(ART_ROOT)
    ensure_folder_meta(BACK_RESOURCE_DIR)
    art_dest = os.path.join(ART_ROOT, BACK_ART_NAME)
    resource_dest = os.path.join(BACK_RESOURCE_DIR, BACK_ART_NAME)
    copied_art, _ = copy_png(BACK_SOURCE, art_dest, 1024)
    copied_res, _ = copy_png(BACK_SOURCE, resource_dest, 1024)
    return int(copied_art) + int(copied_res)


def main():
    if not os.path.isdir(SOURCE_ROOT):
        raise SystemExit(f"Source folder missing: {SOURCE_ROOT}")

    copied = import_back()
    created = 0
    skipped = []
    per_category = {name: 0 for name in CATEGORIES}

    ensure_folder_meta(ART_ROOT)
    ensure_folder_meta(DEF_ROOT)

    for category, max_slot in CATEGORIES.items():
        src_cat = os.path.join(SOURCE_ROOT, category)
        if not os.path.isdir(src_cat):
            skipped.append(f"missing category folder: {category}")
            continue

        art_cat = os.path.join(ART_ROOT, category)
        def_cat = os.path.join(DEF_ROOT, category)
        ensure_folder_meta(art_cat)
        ensure_folder_meta(def_cat)

        for dirpath, _, filenames in os.walk(src_cat):
            for filename in sorted(filenames):
                if not filename.lower().endswith(".png"):
                    continue

                match = PNG_RE.match(filename)
                if not match:
                    skipped.append(filename)
                    continue

                file_category = match.group("category").lower()
                character = match.group("character").lower()
                slot = int(match.group("slot"))
                if file_category != category:
                    skipped.append(filename)
                    continue
                if slot < 1 or slot > max_slot:
                    skipped.append(f"{filename} (slot {slot})")
                    continue

                dest_name = f"{category}_{character}_{slot}.png"
                dest_dir = os.path.join(art_cat, character)
                ensure_folder_meta(dest_dir)
                dest_png = os.path.join(dest_dir, dest_name)
                src_png = os.path.join(dirpath, filename)
                was_copied, texture_guid = copy_png(src_png, dest_png, 2048)
                if was_copied:
                    copied += 1

                def_id = f"{category}_{character}_{slot}"
                write_definition(def_id, character, slot, category, texture_guid, def_cat)
                created += 1
                per_category[category] += 1

    print(f"Copied {copied} PNGs (includes Japanese back copies)")
    print(f"Generated {created} Japanese CardDefinition assets")
    for category in CATEGORIES:
        print(f"  {category}: {per_category[category]}")
    if skipped:
        print(f"Skipped {len(skipped)}")
        for name in skipped:
            print(f"  skip: {name}")


if __name__ == "__main__":
    main()
