#!/usr/bin/env python3
"""Copy Desktop Japan Packler art into Resources/Cards/BoosterPack/Japanese/.

Does not spawn. English Pack01–05 folders stay untouched.
"""
import os
import re
import shutil
import uuid

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SOURCE_ROOT = os.path.expanduser("~/Desktop/Japan Packler")
DEST_ROOT = os.path.join(ROOT, "Assets/Resources/Cards/BoosterPack/Japanese")
DEF_PATH = os.path.join(ROOT, "Assets/Resources/Cards/JapaneseBoosterPackDefinition.asset")
SCRIPT_GUID = "2ac6481953bf1498bb6a1f4a184f5540"
GUID_RE = re.compile(r"^[0-9a-f]{32}$")

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
  maxTextureSize: 2048
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
    maxTextureSize: 2048
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
    maxTextureSize: 2048
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
  m_Name: JapaneseBoosterPackDefinition
  m_EditorClassIdentifier: 
  packSet: 1
  shelfCategoryId: 
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


def copy_png(src_png, dest_png):
    if not os.path.exists(dest_png) or os.path.getsize(dest_png) != os.path.getsize(src_png):
        shutil.copy2(src_png, dest_png)
        copied = True
    else:
        copied = False
    meta_path = dest_png + ".meta"
    if not os.path.exists(meta_path):
        with open(meta_path, "w", encoding="utf-8") as handle:
            handle.write(TEXTURE_META_TEMPLATE.format(guid=new_guid()))
    return copied


def find_png(folder, *needles):
    for name in os.listdir(folder):
        lower = name.lower()
        if not lower.endswith(".png"):
            continue
        if any(needle in lower for needle in needles):
            return os.path.join(folder, name)
    return None


def write_definition():
    meta_path = DEF_PATH + ".meta"
    asset_guid = read_guid(meta_path) if os.path.exists(meta_path) else new_guid()
    with open(DEF_PATH, "w", encoding="utf-8") as handle:
        handle.write(ASSET_TEMPLATE.format(script_guid=SCRIPT_GUID))
    with open(meta_path, "w", encoding="utf-8") as handle:
        handle.write(ASSET_META_TEMPLATE.format(guid=asset_guid))


def main():
    if not os.path.isdir(SOURCE_ROOT):
        raise SystemExit(f"Source folder missing: {SOURCE_ROOT}")

    ensure_folder_meta(DEST_ROOT)
    copied = 0
    skipped = []

    for index in range(1, 6):
        src = os.path.join(SOURCE_ROOT, f"Pack-{index}")
        if not os.path.isdir(src):
            skipped.append(f"missing Pack-{index}")
            continue

        dest = os.path.join(DEST_ROOT, f"Pack{index:02d}")
        ensure_folder_meta(dest)

        base = find_png(src, f"pack-{index}.png", f"pack{index}.png")
        preview = find_png(src, "nizleme", "preview", "önizleme")
        if base is None:
            skipped.append(f"Pack-{index} base png")
            continue
        if copy_png(base, os.path.join(dest, f"Pack{index:02d}_BaseColor.png")):
            copied += 1
        if preview is None:
            skipped.append(f"Pack-{index} preview png")
        elif copy_png(preview, os.path.join(dest, f"Pack{index:02d}_Preview.png")):
            copied += 1

    write_definition()
    print(f"Copied {copied} Japanese pack PNGs")
    print("Wrote JapaneseBoosterPackDefinition.asset")
    if skipped:
        print(f"Skipped {len(skipped)}")
        for item in skipped:
            print(f"  skip: {item}")


if __name__ == "__main__":
    main()
