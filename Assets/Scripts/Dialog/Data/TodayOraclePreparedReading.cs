using System;
using GamerFrameWork.OracleRuntime;
using UnityEngine;

/// <summary>
/// 今日神谕翻牌后的完整预生成结果。
/// 点击翻牌后先生成这一份数据，再交给各个 UI 同步读取。
/// </summary>
[Serializable]
public class TodayOraclePreparedReading
{
    public TarotCard card;
    public bool upright;

    public string cardId;
    public string cardDisplayName;
    public string cardDescription;
    public string cardMeaning;
    public Sprite cardIcon;

    public TodayCardPayload cardPayload;
    public TodayOraclePayload oraclePayload;
    public CompleteInterpretationPayload interpretationPayload;

    public string preparedAt;

    public bool IsFor(TarotCard targetCard, bool targetUpright)
    {
        return targetCard != null
            && cardId == targetCard.cardId
            && upright == targetUpright;
    }
}
