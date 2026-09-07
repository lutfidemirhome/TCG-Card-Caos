#!/usr/bin/env python3
"""Copy Desktop Japan_PsaKartlar into Resources/Cards/PsaCard/Japanese/. Does not spawn."""
import os
import shutil
import uuid

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SOURCE_ROOT = os.path.expanduser("~/Desktop/Japan_PsaKartlar")
DEST_ROOT = os.path.join(ROOT, "Assets/Resources/Cards/PsaCard/Japanese")
GRADES = (
    ("psa_7_japan", "psa_7"),
    ("psa_8_japan", "psa_8"),
    ("psa_9_japan", "psa_9"),
    ("psa_10_japan", "psa_10"),
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


def new_guid():
    return uuid.uuid4().hex


def ensure_folder_meta(folder_path):
    os.makedirs(folder_path, exist_ok=True)
    meta_path = folder_path + ".meta"
    if not os.path.exists(meta_path):
        with open(meta_path, "w", encoding="utf-8") as handle:
            handle.write(FOLDER_META_TEMPLATE.format(guid=new_guid()))


def ensure_texture_meta(png_path):
    meta_path = png_path + ".meta"
    if os.path.exists(meta_path):
        return
    with open(meta_path, "w", encoding="utf-8") as handle:
        handle.write(TEXTURE_META_TEMPLATE.format(guid=new_guid()))


def variant_index(folder_name, grade):
    prefix = grade + "_"
    if not folder_name.startswith(prefix):
        return None
    try:
        return int(folder_name[len(prefix):])
    except ValueError:
        return None


def main():
    if not os.path.isdir(SOURCE_ROOT):
        raise SystemExit(f"Source folder missing: {SOURCE_ROOT}")

    ensure_folder_meta(DEST_ROOT)
    copied = 0
    skipped = []
    per_grade = {}

    for src_grade_name, grade in GRADES:
        src_grade = os.path.join(SOURCE_ROOT, src_grade_name)
        dest_grade = os.path.join(DEST_ROOT, grade)
        if not os.path.isdir(src_grade):
            skipped.append(f"missing {src_grade_name}")
            continue

        ensure_folder_meta(dest_grade)
        per_grade[grade] = 0

        for name in sorted(os.listdir(src_grade)):
            src_variant = os.path.join(src_grade, name)
            if not os.path.isdir(src_variant):
                continue

            index = variant_index(name, grade)
            if index is None:
                skipped.append(name)
                continue

            dest_variant = os.path.join(dest_grade, name)
            ensure_folder_meta(dest_variant)
            expected = ("card_diffuseMAT.png", f"{name}_Preview.png")
            for filename in expected:
                src_png = os.path.join(src_variant, filename)
                if not os.path.isfile(src_png):
                    skipped.append(f"missing file {src_grade_name}/{name}/{filename}")
                    continue
                dest_png = os.path.join(dest_variant, filename)
                shutil.copy2(src_png, dest_png)
                copied += 1
                ensure_texture_meta(dest_png)

            per_grade[grade] += 1

    print(f"Copied {copied} PNGs")
    total = 0
    for _, grade in GRADES:
        count = per_grade.get(grade, 0)
        total += count
        print(f"  {grade}: {count} variants")
    print(f"Total variants: {total}")
    if skipped:
        print(f"Skipped {len(skipped)}")
        for item in skipped:
            print(f"  skip: {item}")


if __name__ == "__main__":
    main()
