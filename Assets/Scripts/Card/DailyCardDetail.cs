using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 今日牌详情展示组件
/// 挂在 TodayCardUI 的 DailyCardScrollScrollRect.Content 下
/// 由 TodayCardUI.SetDailyCardDetail() 填充数据
/// </summary>
public class DailyCardDetail : MonoBehaviour
{

    [Header("牌面基础")]
    public Image cardImage;
    public TMP_Text cardNameText;       // 格式："今日牌:圣杯六(正位)"
    public TMP_Text descriptionText;    // 牌面描述文本

    [Header("牌义解读")]
    public TMP_Text uprightMeaningText;  // 正位牌义（TodayCardUI 填充）
    public TMP_Text reversedMeaningText; // 逆位牌义（TodayCardUI 填充）

    [Header("今日适宜")]
    public TMP_Text uprightMeaningText1; // 建议项 1
    public TMP_Text uprightMeaningText2; // 建议项 2
    public TMP_Text uprightMeaningText3; // 建议项 3

    [Header("今日不宜")]
    public TMP_Text reversedMeaningText1; // 避免项 1（原名 reversedMeaningText，保持兼容）
    public TMP_Text reversedMeaningText2; // 避免项 2
    public TMP_Text reversedMeaningText3; // 避免项 3

    [Header("今日状态映射")]
    public TMP_Text todayStateText;       // 如：今天你不需要同时解决所有问题…

    [Header("情绪提醒")]
    public TMP_Text emotionText;

    [Header("行动建议")]
    public TMP_Text actionSuggestionText; // 如：聆听直觉

}
