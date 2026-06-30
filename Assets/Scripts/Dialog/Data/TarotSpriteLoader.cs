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
        // 圣杯 cups → CupsXX.jpg
        for (int i = 1; i <= 14; i++) map[$"cups_{i:D2}"] = $"Cups{i:D2}";

        // 权杖 wands → WandsXX.jpg
        for (int i = 1; i <= 14; i++) map[$"wands_{i:D2}"] = $"Wands{i:D2}";

        // 宝剑 swords → SwordsXX.jpg
        for (int i = 1; i <= 14; i++) map[$"swords_{i:D2}"] = $"Swords{i:D2}";

        // 星币 pentacles → PentsXX.jpg（文件前缀是 Pents 不是 Pentacles）
        for (int i = 1; i <= 14; i++) map[$"pentacles_{i:D2}"] = $"Pents{i:D2}";

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
            Debug.LogError($"[TarotSpriteLoader] 图集加载失败: {ATLAS_PATH}，" +
                           $"Status={_atlasHandle?.Status}, Error={_atlasHandle?.LastError}");
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
            Debug.LogWarning($"[TarotSpriteLoader] 加载 YooAsset Sprite 失败: {spriteName}，Status={handle?.Status}, Error={handle?.LastError}");
            handle?.Release();
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
