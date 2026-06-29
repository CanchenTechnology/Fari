using System;
using System.Collections;
using System.Collections.Generic;
using GamerFrameWork.UIFrameWork;
using GamerFrameWork.OracleRuntime;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DailyCardBox : MonoBehaviour
{
    private const string CardNameColor = "#FE8E54";

    public TMP_Text cardTitleText;
    public Image cardImage;
    public TMP_Text cardDesTitleText;
    public TMP_Text cardDesText;

    public Button detailBtn;

    private TarotCard _currentCard;
    private bool _currentUpright;

    private void OnEnable()
    {
        if (detailBtn != null)
            detailBtn.onClick.AddListener(OpenDailyCard);
    }
    
    private void OnDisable()
    {
        if (detailBtn != null)
            detailBtn.onClick.RemoveListener(OpenDailyCard);
    }

    private void OpenDailyCard()
    {
        var completeUI = UIModule.Instance.PopUpWindow<CompleteInterpretationUI>();
        if (completeUI != null && _currentCard != null)
            completeUI.SetCard(_currentCard, _currentUpright);
    }

    public void SetCardData(TarotCard card, bool upright, TodayOraclePayload oraclePayload = null,
        Sprite cardSprite = null)
    {
        if (card == null) return;

        _currentCard = card;
        _currentUpright = upright;

        if (cardTitleText != null)
            cardTitleText.text = $"今日牌：{card.DisplayName(upright)}";

        if (cardDesTitleText != null)
            cardDesTitleText.text = BuildDescriptionTitle(card, upright, oraclePayload);

        if (cardDesText != null)
            cardDesText.text = BuildDescriptionText(card, upright, oraclePayload);

        if (cardImage != null)
        {
            var sprite = cardSprite != null ? cardSprite : TarotSpriteLoader.Load(card.cardId);
            if (sprite != null)
            {
                cardImage.sprite = sprite;
                cardImage.preserveAspect = true;
                cardImage.rectTransform.localRotation = upright
                    ? Quaternion.identity
                    : Quaternion.Euler(0, 0, 180);
            }
            else
            {
                Debug.LogWarning($"[DailyCardBox] 卡牌图片加载失败: {card.cardId}");
            }
        }
    }

    private static string BuildDescriptionTitle(TarotCard card, bool upright, TodayOraclePayload oraclePayload)
    {
        if (!string.IsNullOrEmpty(oraclePayload?.title))
            return CleanDescriptionTitle(oraclePayload.title);

        if (upright)
            return "微光照亮前路";
        return "慢下来，看清内心";
    }

    private static string BuildDescriptionText(TarotCard card, bool upright, TodayOraclePayload oraclePayload)
    {
        string cardName = EscapeRichText(card?.DisplayName(upright) ?? "这张牌");
        return $"你今天抽到的塔罗牌是 <color={CardNameColor}>{cardName}</color>。你对这张牌有什么疑问吗？";
    }

    private static string CleanDescriptionTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";

        var cleaned = title.Trim();
        if (cleaned.StartsWith("今日神谕", StringComparison.Ordinal)
            || cleaned.StartsWith("今日标题", StringComparison.Ordinal))
        {
            var separators = new[] { "·", "：" };
            foreach (var separator in separators)
            {
                var index = cleaned.IndexOf(separator, StringComparison.Ordinal);
                if (index >= 0 && index + separator.Length < cleaned.Length)
                    return cleaned.Substring(index + separator.Length).Trim();
            }

            cleaned = cleaned.Replace("今日神谕", "").Replace("今日标题", "").Trim();
        }

        return string.IsNullOrEmpty(cleaned) ? title.Trim() : cleaned;
    }

    private static string EscapeRichText(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";

        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}
