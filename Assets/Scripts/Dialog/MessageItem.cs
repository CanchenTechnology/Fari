using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// AI消息项
/// 显示AI回复的消息，包含文本和选项按钮
/// 支持塔罗师（3个选项）和占星师（4个选项）两种布局
/// </summary>
public class MessageItem : MonoBehaviour
{
    [Header("UI组件")]
    public Text contentText;
    public Button regenerateVoiceButton;
    public Image headImage;
    public Text speakerName;

    [Header("选项按钮")]
    public Button option1Button;
    public Button option2Button;
    public Button option3Button;
    public Button option4Button;

    [Header("选项按钮文本")]
    public Text option1Text;
    public Text option2Text;
    public Text option3Text;
    public Text option4Text;

    // 选项按钮点击回调
    private Action<int> mOnOptionClick;
    private Action mOnRegenerateVoiceClick;

    void Start()
    {
        // 绑定按钮事件
        if (regenerateVoiceButton != null)
        {
            regenerateVoiceButton.onClick.AddListener(OnRegenerateVoiceButtonClick);
        }

        if (option1Button != null)
        {
            option1Button.onClick.AddListener(() => OnOptionButtonClick(0));
        }

        if (option2Button != null)
        {
            option2Button.onClick.AddListener(() => OnOptionButtonClick(1));
        }

        if (option3Button != null)
        {
            option3Button.onClick.AddListener(() => OnOptionButtonClick(2));
        }

        if (option4Button != null)
        {
            option4Button.onClick.AddListener(() => OnOptionButtonClick(3));
        }
    }

    void OnDestroy()
    {
        // 移除按钮事件监听
        if (regenerateVoiceButton != null)
        {
            regenerateVoiceButton.onClick.RemoveAllListeners();
        }

        if (option1Button != null)
        {
            option1Button.onClick.RemoveAllListeners();
        }

        if (option2Button != null)
        {
            option2Button.onClick.RemoveAllListeners();
        }

        if (option3Button != null)
        {
            option3Button.onClick.RemoveAllListeners();
        }

        if (option4Button != null)
        {
            option4Button.onClick.RemoveAllListeners();
        }
    }

    /// <summary>
    /// 设置消息数据
    /// </summary>
    public void SetData(ChatMessageData data, Action<int> onOptionClick = null, Action onRegenerateVoiceClick = null)
    {
        if (data == null) return;

        mOnOptionClick = onOptionClick;
        mOnRegenerateVoiceClick = onRegenerateVoiceClick;



        // 设置选项按钮
        SetOptions(data.options, data.divinerType);
    }

    /// <summary>
    /// 设置消息内容
    /// </summary>
    public void SetContent(string content)
    {
        if (contentText != null)
        {
            contentText.text = content;
        }
    }

    /// <summary>
    /// 设置选项按钮
    /// </summary>
    public void SetOptions(List<string> options, DivinerType divinerType)
    {
        // 先隐藏所有选项按钮
        HideAllOptionButtons();

        if (options == null || options.Count == 0)
        {
            // 使用默认选项
            if (DialogSystem.Instance != null)
            {
                options = DialogSystem.Instance.GetCurrentDivinerOptions();
            }
        }

        if (options == null) return;

        // 根据占卜师类型显示不同数量的选项
        if (divinerType == DivinerType.Tarot)
        {
            // 塔罗师：3个选项
            SetOptionButton(option1Button, option1Text, options.Count > 0 ? options[0] : "为这个问题选牌阵");
            SetOptionButton(option2Button, option2Text, options.Count > 1 ? options[1] : "继续追问");
            SetOptionButton(option3Button, option3Text, options.Count > 2 ? options[2] : "明天再看这条线索");

            // 隐藏第4个按钮
            if (option4Button != null)
            {
                option4Button.gameObject.SetActive(false);
            }
        }
        else if (divinerType == DivinerType.Astrology)
        {
            // 占星师：4个选项
            SetOptionButton(option1Button, option1Text, options.Count > 0 ? options[0] : "看这段关系的周期");
            SetOptionButton(option2Button, option2Text, options.Count > 1 ? options[1] : "分析今日星象");
            SetOptionButton(option3Button, option3Text, options.Count > 2 ? options[2] : "看下一周趋势");
            SetOptionButton(option4Button, option4Text, options.Count > 3 ? options[3] : "保存明日回看");
        }
    }

    /// <summary>
    /// 设置单个选项按钮
    /// </summary>
    private void SetOptionButton(Button button, Text text, string label)
    {
        if (button != null)
        {
            button.gameObject.SetActive(true);
            if (text != null)
            {
                text.text = label;
            }
        }
    }

    /// <summary>
    /// 隐藏所有选项按钮
    /// </summary>
    private void HideAllOptionButtons()
    {
        if (option1Button != null) option1Button.gameObject.SetActive(false);
        if (option2Button != null) option2Button.gameObject.SetActive(false);
        if (option3Button != null) option3Button.gameObject.SetActive(false);
        if (option4Button != null) option4Button.gameObject.SetActive(false);
    }

    /// <summary>
    /// 设置头像精灵
    /// </summary>
    public void SetHeadSprite(Sprite sprite)
    {
        if (headImage != null)
        {
            headImage.sprite = sprite;
        }
    }

    /// <summary>
    /// 加载头像资源
    /// </summary>
    private void LoadHeadIcon(string iconName)
    {
        // 如果项目中有资源管理器，可以在这里加载
        // 例如使用 ResManager.Get.GetSpriteByName(iconName)
        // 这里留空，根据实际项目资源管理方式实现
    }

    /// <summary>
    /// 选项按钮点击事件
    /// </summary>
    private void OnOptionButtonClick(int optionIndex)
    {
        Debug.Log("选项按钮点击: " + optionIndex);
        mOnOptionClick?.Invoke(optionIndex);
    }

    /// <summary>
    /// 重新生成声音按钮点击事件
    /// </summary>
    private void OnRegenerateVoiceButtonClick()
    {
        Debug.Log("重新生成声音按钮点击");
        mOnRegenerateVoiceClick?.Invoke();
    }
}
