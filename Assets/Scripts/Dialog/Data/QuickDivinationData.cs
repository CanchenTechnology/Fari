using System;
using System.Collections.Generic;
using UnityEngine;
using XFGameFrameWork;

/// <summary>
/// 快速占卜话题
/// </summary>
[Serializable]
public class QuickTopic
{
    public string key;              // "love", "work", "self", "social"
    public string label;            // "情感关系", "事业发展"
    public string icon;             // emoji 或符号
    public List<string> questions = new List<string>();  // 3个问题
}

/// <summary>
/// 快速占卜配置（云端拉取或本地兜底）
/// </summary>
[Serializable]
public class QuickReadingConfig
{
    public string intro;
    public string defaultTopic;     // "love"
    public List<QuickTopic> topics = new List<QuickTopic>();
}

public class QuickDivinationData : MonoSingleton<QuickDivinationData>
{
    public delegate void OnConfigUpdated();
    public event OnConfigUpdated ConfigUpdated;

    /// <summary> 当前配置 </summary>
    public QuickReadingConfig Config { get; private set; }

    /// <summary> 当前选中的话题 key </summary>
    public string CurrentTopicKey { get; private set; } = "love";

    /// <summary> 是否展开全部话题 </summary>
    public bool IsExpanded { get; set; } = false;

    [Tooltip("是否已从云端加载过配置")]
    public bool HasRemoteConfig { get; private set; } = false;

    #region 生命周期

    protected override void Awake()
    {
        base.Awake();
        InitDefaultConfig();
    }

    #endregion

    #region 默认配置（三个神谕师各不同）

    /// <summary> 初始化本地兜底配置（根据当前角色） </summary>
    public void InitDefaultConfig()
    {
        var characterType = RoleManager.Instance != null
            ? RoleManager.Instance.characterType
            : CharacterType.TarotReader;

        Config = characterType switch
        {
            CharacterType.Astrologer => GetAstrologerDefaultConfig(),
            CharacterType.Meditator   => GetMeditatorDefaultConfig(),
            _                         => GetTarotDefaultConfig(),
        };

        CurrentTopicKey = Config.defaultTopic;
    }

    private QuickReadingConfig GetTarotDefaultConfig()
    {
        return new QuickReadingConfig
        {
            intro = "若你愿意，我可以陪你寻找此刻心中最想知道的答案。",
            defaultTopic = "love",
            topics = new List<QuickTopic>
            {
                new QuickTopic
                {
                    key = "self", label = "自我探索", icon = "◈",
                    questions = new List<string> { "我当前最需要关注的是什么？", "我的内在状态正在经历什么变化？", "什么是现在应该放下的？" }
                },
                new QuickTopic
                {
                    key = "love", label = "情感关系", icon = "♥",
                    questions = new List<string> { "这段关系会走向怎样的未来？", "他/她现在对我的真实感受是什么？", "我们之间存在什么问题需要解决？" }
                },
                new QuickTopic
                {
                    key = "work", label = "事业发展", icon = "⚔",
                    questions = new List<string> { "未来的工作会走向哪里？", "我现在的努力会得到回报吗？", "接下来的机会在哪里？" }
                },
                new QuickTopic
                {
                    key = "social", label = "社交人际", icon = "☀",
                    questions = new List<string> { "我该如何与他人更好相处？", "这段友谊/关系值得投入吗？", "如何建立更健康的人际边界？" }
                },
                new QuickTopic
                {
                    key = "three_card", label = "三牌占卜", icon = "✦",
                    questions = new List<string> { "给我做个三张牌的占卜" }
                }
            }
        };
    }

    private QuickReadingConfig GetAstrologerDefaultConfig()
    {
        return new QuickReadingConfig
        {
            intro = "抬头看看星空，也许此刻的答案早已写在你的星盘里。",
            defaultTopic = "relationship_cycle",
            topics = new List<QuickTopic>
            {
                new QuickTopic
                {
                    key = "relationship_cycle", label = "关系周期", icon = "☉",
                    questions = new List<string> { "这段关系现在处在什么周期？", "现在适合主动表达吗？", "我该如何理解他的沉默？" }
                },
                new QuickTopic
                {
                    key = "near_trend", label = "近期趋势", icon = "☽",
                    questions = new List<string> { "接下来一周我该留意什么？", "今天适合做决定吗？", "这件事的时间窗口在哪里？" }
                },
                new QuickTopic
                {
                    key = "expression_window", label = "表达窗口", icon = "☿",
                    questions = new List<string> { "我该什么时候开口更合适？", "这句话该怎么说更稳？", "我现在最该避免哪种表达？" }
                },
                new QuickTopic
                {
                    key = "long_direction", label = "长期方向", icon = "♃",
                    questions = new List<string> { "我人生的长期模式是什么？", "未来一年我该聚焦什么？", "什么提醒对我最重要？" }
                }
            }
        };
    }

    private QuickReadingConfig GetMeditatorDefaultConfig()
    {
        return new QuickReadingConfig
        {
            intro = "深呼吸，让我们回到身体，答案一直在你的内在。",
            defaultTopic = "anxiety_settle",
            topics = new List<QuickTopic>
            {
                new QuickTopic
                {
                    key = "anxiety_settle", label = "平复不安", icon = "☁",
                    questions = new List<string> { "我怎么让此刻的不安平稳下来？", "焦虑的根源是什么？", "什么可以帮助我重置状态？" }
                },
                new QuickTopic
                {
                    key = "sleep_review", label = "睡前复盘", icon = "☾",
                    questions = new List<string> { "今晚我该放下什么？", "今天有什么值得肯定的？", "睡前可以做什么小仪式？" }
                },
                new QuickTopic
                {
                    key = "body_tension", label = "身体觉察", icon = "⚕",
                    questions = new List<string> { "我的身体在告诉我什么？", "哪里最紧绷？", "如何把注意力带回身体？" }
                },
                new QuickTopic
                {
                    key = "small_action", label = "最小行动", icon = "→",
                    questions = new List<string> { "此刻我能做的最小一步是什么？", "什么是我可控的？", "如何从微小的行动开始重建信心？" }
                }
            }
        };
    }

    #endregion

    #region Firebase 配置加载

    /// <summary>
    /// 从 Firestore 加载云端快速占卜配置
    /// </summary>
    public void LoadRemoteConfig(System.Action<bool> onComplete = null)
    {
        var firestore = FirestoreManager.Instance;
        if (firestore == null || !firestore.IsInitialized)
        {
            Debug.LogWarning("[QuickDivinationData] Firestore 未就绪，使用本地默认配置");
            onComplete?.Invoke(false);
            return;
        }

        // 根据当前角色构建文档路径: quick_reading/{oracleId}
        string oracleId = GetCurrentOracleId();
        string uid = UserDataManager.Instance != null ? UserDataManager.Instance.FirebaseUid : "";

        var db = typeof(FirestoreManager).GetField("_db",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (db == null)
        {
            onComplete?.Invoke(false);
            return;
        }

        // 方式：直接用 FirestoreManager 已有的 LoadUserDataByUid 模式读取
        // 这里新增一个泛用读取方法，通过 Firestore 的文档引用直接读
        StartCoroutine(FetchConfigCoroutine(oracleId, onComplete));
    }

    private System.Collections.IEnumerator FetchConfigCoroutine(string oracleId, System.Action<bool> onComplete)
    {
        // 通过 FirebaseExtensions 异步读取
        var db = Firebase.Firestore.FirebaseFirestore.DefaultInstance;
        var docRef = db.Collection("quick_reading").Document(oracleId);
        var task = docRef.GetSnapshotAsync();

        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted || task.IsCanceled || !task.Result.Exists)
        {
            Debug.Log($"[QuickDivinationData] 云端无快速占卜配置({oracleId})，使用默认");
            onComplete?.Invoke(false);
            yield break;
        }

        ApplyRemoteConfig(task.Result.ToDictionary());
        HasRemoteConfig = true;
        ConfigUpdated?.Invoke();
        onComplete?.Invoke(true);
    }

    /// <summary> 解析 Firestore 文档到本地配置 </summary>
    private void ApplyRemoteConfig(Dictionary<string, object> data)
    {
        if (data == null) return;

        var config = new QuickReadingConfig();

        if (data.TryGetValue("intro", out object intro)) config.intro = intro.ToString();
        if (data.TryGetValue("defaultTopic", out object dt)) config.defaultTopic = dt.ToString();

        if (data.TryGetValue("topics", out object topicsObj) && topicsObj is List<object> topicsList)
        {
            config.topics = new List<QuickTopic>();
            foreach (var t in topicsList)
            {
                if (t is Dictionary<string, object> topicDict)
                {
                    var topic = new QuickTopic();
                    if (topicDict.TryGetValue("key",       out object k)) topic.key       = k.ToString();
                    if (topicDict.TryGetValue("label",     out object l)) topic.label     = l.ToString();
                    if (topicDict.TryGetValue("icon",      out object i)) topic.icon      = i.ToString();

                    if (topicDict.TryGetValue("questions", out object qObj) && qObj is List<object> qList)
                    {
                        topic.questions = new List<string>();
                        foreach (var q in qList) topic.questions.Add(q.ToString());
                    }

                    config.topics.Add(topic);
                }
            }
        }

        Config = config;
        CurrentTopicKey = Config.defaultTopic;
    }

    #endregion

    #region 业务方法

    /// <summary> 获取当前选中话题的所有问题 </summary>
    public List<string> GetCurrentQuestions()
    {
        var topic = Config?.topics?.Find(t => t.key == CurrentTopicKey);
        return topic?.questions ?? new List<string>();
    }

    /// <summary> 切换话题（key: love/work/self/social） </summary>
    public void SwitchTopic(string topicKey)
    {
        if (Config == null) return;

        bool exists = Config.topics.Exists(t => t.key == topicKey);
        if (!exists) return;

        CurrentTopicKey = topicKey;
    }

    /// <summary> 获取当前选中的话题 </summary>
    public QuickTopic GetCurrentTopic()
    {
        return Config?.topics?.Find(t => t.key == CurrentTopicKey);
    }

    /// <summary> 获取所有话题（展开态用） </summary>
    public List<QuickTopic> GetAllTopics()
    {
        return Config?.topics ?? new List<QuickTopic>();
    }

    /// <summary> 获取收起态话题（前4个） </summary>
    public List<QuickTopic> GetVisibleTopics()
    {
        var all = GetAllTopics();
        return IsExpanded ? all : all.GetRange(0, Math.Min(4, all.Count));
    }

    /// <summary> 换角色时重新初始化 </summary>
    public void RefreshForCharacter(CharacterType newType)
    {
        // 优先尝试加载远端配置，失败则用本地默认
        InitDefaultConfig();
        LoadRemoteConfig(success =>
        {
            if (!success) Debug.Log("[QuickDivinationData] 使用本地默认配置");
        });
    }

    private string GetCurrentOracleId()
    {
        if (RoleManager.Instance == null) return "tarot";

        return RoleManager.Instance.characterType switch
        {
            CharacterType.Astrologer => "astrology",
            CharacterType.Meditator  => "sage",
            _                        => "tarot",
        };
    }

    #endregion
}
