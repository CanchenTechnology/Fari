using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using YooAsset;

/// <summary>
/// 统一的塔罗牌 Sprite 加载器
/// 使用 YooAsset 加载 SpriteAtlas 图集，从图集中按名称获取卡牌 Sprite
/// </summary>
public static class TarotSpriteLoader
{
    /// <summary>
    /// YooAsset 中 SpriteAtlas 的加载地址
    /// 使用 AddressByFileName 规则 + SupportExtensionless，地址即文件名（不含扩展名）
    /// Collector 中配置 CollectPath: Assets/GameData/Arts/SpriteAtlas, AddressRule: AddressByFileName
    /// </summary>
    public const string ATLAS_PATH = "TarotSpriteAtlas";

    private static SpriteAtlas _atlas;
    private static AssetHandle _atlasHandle;
    private static readonly Dictionary<string, AssetHandle> SpriteHandles = new Dictionary<string, AssetHandle>();
    private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

#if UNITY_EDITOR
    private static readonly string[] EditorSpriteFolders =
    {
        "Assets/GameData/Arts/Sprites/MajorArcana",
        "Assets/GameData/Arts/Sprites/Cups",
        "Assets/GameData/Arts/Sprites/Wands",
        "Assets/GameData/Arts/Sprites/Swords",
        "Assets/GameData/Arts/Sprites/Pentacles",
        "Assets/GameData/Arts/Sprites/Tarot/Cups_Pack",
        "Assets/GameData/Arts/Sprites/Tarot/Swords_Pack",
        "Assets/GameData/Arts/Sprites/Tarot/Pentacles_Selected_and_Pending_Collection/approved_selected",
        "Assets/GameData/Arts/Sprites/Tarot/Pentacles_Selected_and_Pending_Collection/pending_review"
    };

    private static readonly string[] EditorSpriteExtensions =
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".psd"
    };
#endif

    /// <summary>图集是否已成功加载</summary>
    public static bool IsReady => _atlas != null;

    // ========== cardId → 图集内 spriteName 映射（78 张） ==========

    private static readonly Dictionary<string, string> SpriteNameMap = BuildMap();

    private static Dictionary<string, string> BuildMap()
    {
        var map = new Dictionary<string, string>();

        // --- 大阿卡纳 (0-21) ---
        string[] majorNames =
        {
            "Fool", "Magician", "High_Priestess", "Empress", "Emperor",
            "Hierophant", "Lovers", "Chariot", "Strength", "Hermit",
            "Wheel_of_Fortune", "Justice", "Hanged_Man", "Death", "Temperance",
            "Devil", "Tower", "Star", "Moon", "Sun", "Judgement", "World"
        };
        for (int i = 0; i < 22; i++)
            map[$"major_{i:D2}"] = $"RWS_Tarot_{i:D2}_{majorNames[i]}";

        // --- 小阿卡纳 (1-14) ---
        // 圣杯 cups → 新版 PNG 资源
        string[] cupsNames =
        {
            "Ace_of_Cups_圣杯一",
            "Two_of_Cups_圣杯二",
            "Three_of_Cups_圣杯三",
            "Four_of_Cups_圣杯四",
            "Five_of_Cups_圣杯五",
            "Six_of_Cups_圣杯六",
            "Seven_of_Cups_圣杯七",
            "Eight_of_Cups_圣杯八",
            "Nine_of_Cups_圣杯九",
            "Ten_of_Cups_圣杯十",
            "Page_of_Cups_圣杯侍从",
            "Knight_of_Cups_圣杯骑士",
            "Queen_of_Cups_圣杯王后",
            "King_of_Cups_圣杯国王"
        };
        for (int i = 0; i < cupsNames.Length; i++)
            map[$"cups_{i + 1:D2}"] = cupsNames[i];

        // 权杖 wands → WandsXX.jpg
        for (int i = 1; i <= 14; i++) map[$"wands_{i:D2}"] = $"Wands{i:D2}";

        // 宝剑 swords → 新版 PNG 资源
        string[] swordsNames =
        {
            "Ace_of_Swords_宝剑一",
            "Two_of_Swords_宝剑二",
            "Three_of_Swords_宝剑三",
            "Four_of_Swords_宝剑四",
            "Five_of_Swords_宝剑五",
            "Six_of_Swords_宝剑六",
            "Seven_of_Swords_宝剑七",
            "Eight_of_Swords_宝剑八",
            "Nine_of_Swords_宝剑九",
            "Ten_of_Swords_宝剑十",
            "Page_of_Swords_宝剑侍从",
            "Knight_of_Swords_宝剑骑士",
            "Queen_of_Swords_宝剑王后",
            "King_of_Swords_宝剑国王"
        };
        for (int i = 0; i < swordsNames.Length; i++)
            map[$"swords_{i + 1:D2}"] = swordsNames[i];

        // 星币 pentacles → 新版 PNG 资源
        string[] pentaclesNames =
        {
            "Ace_of_Pentacles_星币一",
            "Two_of_Pentacles_星币二",
            "Three_of_Pentacles_星币三",
            "Four_of_Pentacles_星币四",
            "Five_of_Pentacles_星币五",
            "Six_of_Pentacles_星币六",
            "Seven_of_Pentacles_星币七",
            "Eight_of_Pentacles_星币八",
            "Nine_of_Pentacles_星币九",
            "Ten_of_Pentacles_星币十",
            "Page_of_Pentacles_星币侍从",
            "Knight_of_Pentacles_星币骑士",
            "Queen_of_Pentacles_星币王后",
            "King_of_Pentacles_星币国王"
        };
        for (int i = 0; i < pentaclesNames.Length; i++)
            map[$"pentacles_{i + 1:D2}"] = pentaclesNames[i];

        return map;
    }

    // ========== 初始化 & 加载 ==========

    /// <summary>
    /// 初始化：使用 YooAsset 同步加载 SpriteAtlas 图集
    /// 可在游戏启动时预加载，也可在首次 Load 时自动调用
    /// </summary>
    public static void Initialize()
    {
        if (_atlas != null) return;

        Debug.Log($"[TarotSpriteLoader] 使用 YooAsset 加载图集: {ATLAS_PATH}");
        _atlasHandle = YooAssets.LoadAssetSync<SpriteAtlas>(ATLAS_PATH);

        if (_atlasHandle != null && _atlasHandle.Status == EOperationStatus.Succeed)
        {
            _atlas = _atlasHandle.AssetObject as SpriteAtlas;
            if (_atlas != null)
                Debug.Log($"[TarotSpriteLoader] 图集加载成功，包含 {_atlas.spriteCount} 张 Sprite");
            else
                Debug.LogError($"[TarotSpriteLoader] 图集 AssetObject 转换失败");
        }
        else
        {
            Debug.LogWarning($"[TarotSpriteLoader] 图集加载失败: {ATLAS_PATH}，将尝试单图资源兜底。" +
                             $"Status={_atlasHandle?.Status}, Error={_atlasHandle?.LastError}");
            _atlasHandle?.Release();
            _atlasHandle = null;
        }
    }

    /// <summary>
    /// 根据 cardId 加载卡牌 Sprite
    /// </summary>
    /// <param name="cardId">如 "major_00", "cups_01", "pentacles_14"</param>
    /// <returns>Sprite 或 null</returns>
    public static Sprite Load(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Debug.LogWarning("[TarotSpriteLoader] cardId 为空");
            return null;
        }

        if (!SpriteNameMap.TryGetValue(cardId, out var spriteName))
        {
            Debug.LogWarning($"[TarotSpriteLoader] cardId 不在映射表中: {cardId}");
            return null;
        }

        Sprite sprite = LoadFromAtlas(spriteName);
        if (sprite != null)
            return sprite;

        sprite = LoadSpriteAsset(spriteName);
        if (sprite == null)
        {
            int atlasSpriteCount = _atlas != null ? _atlas.spriteCount : 0;
            Debug.LogWarning($"[TarotSpriteLoader] YooAsset 中未找到 Sprite: {spriteName} (cardId={cardId})，atlasSpriteCount={atlasSpriteCount}");
        }

        return sprite;
    }

    private static Sprite LoadFromAtlas(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName))
            return null;

        // 懒加载：首次使用自动初始化图集
        if (_atlas == null)
            Initialize();

        return _atlas != null ? _atlas.GetSprite(spriteName) : null;
    }

    private static Sprite LoadSpriteAsset(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName))
            return null;

        if (SpriteCache.TryGetValue(spriteName, out Sprite cachedSprite))
            return cachedSprite;

        AssetHandle handle = YooAssets.LoadAssetSync<Sprite>(spriteName);
        if (handle == null || handle.Status != EOperationStatus.Succeed)
        {
            string status = $"Status={handle?.Status}, Error={handle?.LastError}";
            handle?.Release();

#if UNITY_EDITOR
            Sprite editorSprite = LoadEditorSpriteAsset(spriteName);
            if (editorSprite != null)
            {
                SpriteCache[spriteName] = editorSprite;
                return editorSprite;
            }
#endif

            Debug.LogWarning($"[TarotSpriteLoader] 加载 YooAsset Sprite 失败: {spriteName}，{status}");
            return null;
        }

        Sprite sprite = handle.AssetObject as Sprite;
        if (sprite == null)
        {
            Debug.LogWarning($"[TarotSpriteLoader] YooAsset Sprite 类型转换失败: {spriteName}");
            handle.Release();
            return null;
        }

        SpriteHandles[spriteName] = handle;
        SpriteCache[spriteName] = sprite;
        return sprite;
    }

#if UNITY_EDITOR
    private static Sprite LoadEditorSpriteAsset(string spriteName)
    {
        foreach (string folder in EditorSpriteFolders)
        {
            foreach (string extension in EditorSpriteExtensions)
            {
                string path = $"{folder}/{spriteName}{extension}";
                Sprite sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null)
                    return sprite;
            }
        }

        return null;
    }
#endif

    /// <summary>
    /// 释放图集资源
    /// </summary>
    public static void Release()
    {
        if (_atlasHandle != null)
        {
            _atlasHandle.Release();
            _atlasHandle = null;
        }
        _atlas = null;
        foreach (AssetHandle handle in SpriteHandles.Values)
        {
            handle?.Release();
        }
        SpriteHandles.Clear();
        SpriteCache.Clear();
        Debug.Log("[TarotSpriteLoader] 图集已释放");
    }
}
