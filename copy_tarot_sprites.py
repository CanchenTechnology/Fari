#!/usr/bin/env python3
"""将塔罗牌 Sprite 复制到 Resources/TarotCards/ 并生成 .meta 文件"""
import os
import uuid
import shutil

BASE = "/Users/kittenhao/Unity/UnityDemo/MoonlyApp/Assets"
SRC = f"{BASE}/GameData/Arts/Sprites"
DST = f"{BASE}/Resources/TarotCards"

# cardId -> (source_subfolder, source_filename)
MAPPING = {}

# Major Arcana
major_map = {
    0: "Fool", 1: "Magician", 2: "High_Priestess", 3: "Empress",
    4: "Emperor", 5: "Hierophant", 6: "Lovers", 7: "Chariot",
    8: "Strength", 9: "Hermit", 10: "Wheel_of_Fortune", 11: "Justice",
    12: "Hanged_Man", 13: "Death", 14: "Temperance", 15: "Devil",
    16: "Tower", 17: "Star", 18: "Moon", 19: "Sun",
    20: "Judgement", 21: "World",
}
for num, name in major_map.items():
    card_id = f"major_{num:02d}"
    filename = f"RWS_Tarot_{num:02d}_{name}.jpg"
    MAPPING[card_id] = ("MajorArcana", filename)

# Minor Arcana
suits = {
    "cups": ("Cups", "Cups"),
    "wands": ("Wands", "Wands"),
    "swords": ("Swords", "Swords"),
    "pentacles": ("Pentacles", "Pents"),  # 文件夹 Pentacles，文件前缀 Pents
}
for suit_key, (suit_dir, file_prefix) in suits.items():
    for num in range(1, 15):
        card_id = f"{suit_key}_{num:02d}"
        filename = f"{file_prefix}{num:02d}.jpg"
        MAPPING[card_id] = (suit_dir, filename)

SPRITE_META_TEMPLATE = '''fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
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
  textureFormat: -3
  maxTextureSize: 1024
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 16
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 210
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 1
  swizzle: 50462976
  cookieLightType: 1
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 1024
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
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
    physicsShape: []
    bones: []
    spriteID: {sprite_id}
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
'''


def gen_sprite_id():
    """生成随机的 spriteID（仿 Unity 格式）"""
    import random
    parts = [random.getrandbits(32) for _ in range(4)]
    return f"{parts[0] & 0xFFFFFFFF:08x}{parts[1] & 0xFFFFFFFF:08x}{parts[2] & 0xFFFFFFFF:08x}{parts[3] & 0xFFFFFFFF:08x}"


def main():
    os.makedirs(DST, exist_ok=True)
    count = 0
    for card_id, (subfolder, filename) in sorted(MAPPING.items()):
        src_jpg = os.path.join(SRC, subfolder, filename)
        dst_jpg = os.path.join(DST, f"{card_id}.jpg")

        if not os.path.exists(src_jpg):
            print(f"⚠️  源文件不存在，跳过: {src_jpg}")
            continue

        shutil.copy2(src_jpg, dst_jpg)

        # 生成 .meta
        guid_val = str(uuid.uuid4()).replace("-", "")
        sprite_id = gen_sprite_id()
        meta_content = SPRITE_META_TEMPLATE.format(guid=guid_val, sprite_id=sprite_id)
        meta_path = dst_jpg + ".meta"
        with open(meta_path, "w") as f:
            f.write(meta_content)

        count += 1
        print(f"  ✅ {card_id}.jpg")

    print(f"\n完成！共复制 {count}/78 张牌到 Resources/TarotCards/")
    print(f"请在 Unity 中等待资源重新导入（约1-2分钟）")


if __name__ == "__main__":
    main()
