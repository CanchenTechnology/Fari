using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 塔罗牌数据 —— 78 张标准塔罗牌
/// </summary>
[Serializable]
public class TarotCard
{
    public string cardId;        // eg. "major_00", "cups_01"
    public string nameEn;        // eg. "The Fool"
    public string nameZh;        // eg. "愚者"
    public string arcana;        // "major" | "minor"
    public string suit;          // "cups" | "wands" | "swords" | "pentacles" | ""
    public int number;           // 牌序号
    public List<string> keywords = new List<string>();
    public string element;       // "fire" | "water" | "air" | "earth" | "spirit"

    /// <summary>格式化的牌名（含正逆位标记）</summary>
    public string DisplayName(bool upright)
        => TarotDeck.FormatDisplayName(nameZh, upright);

    /// <summary>用于 API 传输的卡片摘要</summary>
    public string ToSummary(bool upright)
        => $"{cardId}:{nameZh}({(upright ? "upright" : "reversed")})";
}

/// <summary>
/// 塔罗牌组 —— 78 张标准韦特塔罗
/// </summary>
public static class TarotDeck
{
    private static List<TarotCard> _fullDeck;
    private static System.Random _rng = new System.Random();

    public static string FormatDisplayName(string name, bool upright)
    {
        string safeName = StripOrientationSuffix(name);
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = "未知牌";

        return $"{safeName}·{(upright ? "正位" : "逆位")}";
    }

    public static string FormatDisplayName(string name, string orientation)
        => FormatDisplayName(name, IsUprightOrientation(orientation));

    public static bool IsUprightOrientation(string orientation)
    {
        if (string.IsNullOrWhiteSpace(orientation))
            return true;

        string normalized = orientation.Trim();
        return !string.Equals(normalized, "reversed", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalized, "reverse", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalized, "逆", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalized, "逆位", StringComparison.OrdinalIgnoreCase);
    }

    public static string StripOrientationSuffix(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";

        string result = name.Trim();
        string[] suffixes =
        {
            "·正位", "·逆位",
            "（正位）", "（逆位）",
            "(正位)", "(逆位)",
            "(upright)", "(reversed)"
        };

        foreach (string suffix in suffixes)
        {
            if (result.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return result.Substring(0, result.Length - suffix.Length).Trim();
        }

        return result;
    }

    public static List<TarotCard> FullDeck
    {
        get
        {
            if (_fullDeck == null) _fullDeck = BuildDeck();
            return _fullDeck;
        }
    }

    /// <summary>从完整牌组中随机抽一张（不重复）</summary>
    public static (TarotCard card, bool upright) DrawOne(List<TarotCard> excludeFrom = null)
    {
        var pool = new List<TarotCard>(FullDeck);
        if (excludeFrom != null)
            pool.RemoveAll(c => excludeFrom.Exists(e => e.cardId == c.cardId));

        if (pool.Count == 0)
            pool = new List<TarotCard>(FullDeck);

        var card = pool[_rng.Next(pool.Count)];
        bool upright = _rng.Next(2) == 0;
        return (card, upright);
    }

    /// <summary>随机抽 N 张（不重复）</summary>
    public static List<(TarotCard card, bool upright)> DrawMultiple(int count)
    {
        var pool = new List<TarotCard>(FullDeck);
        var result = new List<(TarotCard, bool)>();
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int idx = _rng.Next(pool.Count);
            result.Add((pool[idx], _rng.Next(2) == 0));
            pool.RemoveAt(idx);
        }
        return result;
    }

    public static TarotCard GetById(string cardId)
        => FullDeck.Find(c => c.cardId == cardId);

    // ========== 22 张大阿卡纳 ==========

    private static TarotCard Major(int n, string en, string zh, string element, params string[] kw)
        => new TarotCard
        {
            cardId = $"major_{n:D2}",
            nameEn = en, nameZh = zh, arcana = "major", suit = "",
            number = n, element = element,
            keywords = new List<string>(kw)
        };

    // ========== 56 张小阿卡纳 ==========

    private static TarotCard Minor(string suit, int n, string zh, params string[] kw)
        => new TarotCard
        {
            cardId = $"{suit}_{n:D2}",
            nameEn = $"{n} of {suit}", nameZh = zh, arcana = "minor", suit = suit,
            number = n, element = SuitElement(suit),
            keywords = new List<string>(kw)
        };

    private static string SuitElement(string suit) => suit switch
    {
        "cups" => "water",
        "wands" => "fire",
        "swords" => "air",
        "pentacles" => "earth",
        _ => ""
    };

    private static List<TarotCard> BuildDeck()
    {
        var deck = new List<TarotCard>();

        // --- 大阿卡纳 (Major Arcana) ---
        deck.Add(Major(0, "The Fool", "愚者", "air", "开始", "冒险", "天真", "自由"));
        deck.Add(Major(1, "The Magician", "魔术师", "air", "创造", "意志", "技巧", "潜能"));
        deck.Add(Major(2, "The High Priestess", "女祭司", "water", "直觉", "潜意识", "神秘", "内省"));
        deck.Add(Major(3, "The Empress", "女皇", "earth", "丰饶", "母性", "感官", "滋养"));
        deck.Add(Major(4, "The Emperor", "皇帝", "fire", "权威", "秩序", "掌控", "稳定"));
        deck.Add(Major(5, "The Hierophant", "教皇", "earth", "传统", "信仰", "教导", "规范"));
        deck.Add(Major(6, "The Lovers", "恋人", "air", "选择", "关系", "和谐", "价值观"));
        deck.Add(Major(7, "The Chariot", "战车", "water", "意志", "胜利", "掌控", "前进"));
        deck.Add(Major(8, "Strength", "力量", "fire", "内在力量", "耐心", "勇气", "驯服"));
        deck.Add(Major(9, "The Hermit", "隐士", "earth", "内省", "智慧", "孤独", "指引"));
        deck.Add(Major(10, "Wheel of Fortune", "命运之轮", "fire", "命运", "转折", "循环", "机遇"));
        deck.Add(Major(11, "Justice", "正义", "air", "公正", "因果", "平衡", "真相"));
        deck.Add(Major(12, "The Hanged Man", "倒吊人", "water", "牺牲", "放下", "新视角", "等待"));
        deck.Add(Major(13, "Death", "死神", "water", "结束", "转变", "重生", "放下"));
        deck.Add(Major(14, "Temperance", "节制", "fire", "平衡", "调和", "耐心", "中庸"));
        deck.Add(Major(15, "The Devil", "恶魔", "earth", "束缚", "欲望", "物质", "阴影"));
        deck.Add(Major(16, "The Tower", "塔", "fire", "崩塌", "剧变", "觉醒", "释放"));
        deck.Add(Major(17, "The Star", "星星", "air", "希望", "疗愈", "信任", "启示"));
        deck.Add(Major(18, "The Moon", "月亮", "water", "幻觉", "恐惧", "潜意识", "迷惑"));
        deck.Add(Major(19, "The Sun", "太阳", "fire", "喜悦", "成功", "活力", "明朗"));
        deck.Add(Major(20, "Judgement", "审判", "fire", "觉醒", "召唤", "重生", "清算"));
        deck.Add(Major(21, "The World", "世界", "earth", "完成", "圆满", "成就", "旅行"));

        // --- 小阿卡纳 (Minor Arcana) ---
        string[][] minorNames = {
            new[]{"圣杯王牌","圣杯二","圣杯三","圣杯四","圣杯五","圣杯六","圣杯七","圣杯八","圣杯九","圣杯十","圣杯侍从","圣杯骑士","圣杯王后","圣杯国王"},
            new[]{"权杖王牌","权杖二","权杖三","权杖四","权杖五","权杖六","权杖七","权杖八","权杖九","权杖十","权杖侍从","权杖骑士","权杖王后","权杖国王"},
            new[]{"宝剑王牌","宝剑二","宝剑三","宝剑四","宝剑五","宝剑六","宝剑七","宝剑八","宝剑九","宝剑十","宝剑侍从","宝剑骑士","宝剑王后","宝剑国王"},
            new[]{"星币王牌","星币二","星币三","星币四","星币五","星币六","星币七","星币八","星币九","星币十","星币侍从","星币骑士","星币王后","星币国王"},
        };
        string[] suits = { "cups", "wands", "swords", "pentacles" };
        string[][][] kw = {
            // Cups
            new[]{ new[]{"爱","情感","新开始"}, new[]{"结合","伙伴","吸引"}, new[]{"庆祝","友谊","欢聚"}, new[]{"倦怠","不满","沉思"},
                   new[]{"失落","遗憾","悲伤"}, new[]{"回忆","纯真","馈赠"}, new[]{"幻想","选择","迷惑"}, new[]{"离开","追寻","放下"},
                   new[]{"满足","愿望达成"}, new[]{"圆满","家庭","和谐"}, new[]{"创意","敏感","消息"}, new[]{"浪漫","邀请","追寻"},
                   new[]{"温柔","直觉","共情"}, new[]{"成熟","包容","情感掌控"} },
            // Wands
            new[]{ new[]{"灵感","新开始","激情"}, new[]{"计划","远见","决定"}, new[]{"展望","扩张","远航"}, new[]{"庆祝","安定","和谐"},
                   new[]{"竞争","冲突","挑战"}, new[]{"胜利","认可","进步"}, new[]{"坚守","捍卫","毅力"}, new[]{"迅速","行动","消息"},
                   new[]{"坚持","警惕","蓄力"}, new[]{"负担","责任","压力"}, new[]{"探索","冒险","热情"}, new[]{"冲锋","行动","热情"},
                   new[]{"自信","魅力","领导"}, new[]{"愿景","领导","创业"} },
            // Swords
            new[]{ new[]{"清晰","真理","突破"}, new[]{"僵局","抉择","逃避"}, new[]{"心碎","悲伤","分离"}, new[]{"休息","恢复","冥想"},
                   new[]{"冲突","输赢","敌意"}, new[]{"过渡","疗愈","前行"}, new[]{"策略","欺骗","狡猾"}, new[]{"束缚","无力","限制"},
                   new[]{"焦虑","噩梦","担忧"}, new[]{"终结","谷底","解脱"}, new[]{"好奇","警觉","信息"}, new[]{"冲刺","决断","行动"},
                   new[]{"洞察","独立","边界"}, new[]{"理性","权威","真相"} },
            // Pentacles
            new[]{ new[]{"机会","财富","新开始"}, new[]{"平衡","适应","变通"}, new[]{"合作","技能","成长"}, new[]{"守财","控制","稳定"},
                   new[]{"匮乏","寒冷","遗弃"}, new[]{"慷慨","分享","回报"}, new[]{"耐心","评估","耕耘"}, new[]{"精进","专注","技艺"},
                   new[]{"丰收","自主","优雅"}, new[]{"富足","传承","稳固"}, new[]{"务实","学习","踏实"}, new[]{"可靠","坚持","积累"},
                   new[]{"滋养","务实","富足"}, new[]{"成就","管理","财富"} },
        };
        for (int s = 0; s < 4; s++)
            for (int i = 0; i < 14; i++)
                deck.Add(Minor(suits[s], i + 1, minorNames[s][i], kw[s][i]));

        return deck;
    }
}
