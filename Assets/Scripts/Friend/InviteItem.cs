using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using GamerFrameWork.UIFrameWork;
using TMPro;

public class InviteItem : MonoBehaviour
{
    public Image headImage;
    public TMP_Text nameText;
    public TMP_Text infoText;
    public Button infoBtn;
    public Button rejectBtn;

    private FriendDataManager.InviteData data;
    private bool isProcessing;

    private void OnEnable()
    {
        EnsureRejectButton();
        ApplyCardVisualStyle();
        if (infoBtn != null) infoBtn.onClick.AddListener(OnActionButtonClick);
        if (rejectBtn != null) rejectBtn.onClick.AddListener(OnRejectButtonClick);
    }

    private void OnDisable()
    {
        if (infoBtn != null) infoBtn.onClick.RemoveListener(OnActionButtonClick);
        if (rejectBtn != null) rejectBtn.onClick.RemoveListener(OnRejectButtonClick);
    }

    /// <summary>
    /// 当前绑定的邀请数据
    /// </summary>
    public FriendDataManager.InviteData Data => data;

    /// <summary>
    /// 设置邀请数据显示
    /// </summary>
    public void SetData(Sprite sprite, string info)
    {
        if (headImage != null) headImage.sprite = sprite;

        if (infoText != null) infoText.text = info;
    }

    /// <summary>
    /// 通过 InviteData 设置邀请数据显示
    /// </summary>
    public void SetData(FriendDataManager.InviteData inviteData)
    {
        data = inviteData;
        EnsureRejectButton();
        ApplyCardVisualStyle();
        isProcessing = false;
        SetButtonsInteractable(true);

        string displayName = string.IsNullOrWhiteSpace(inviteData.name) ? "新的好友请求" : inviteData.name.Trim();
        string info = string.IsNullOrWhiteSpace(inviteData.info)
            ? "想添加你为好友"
            : inviteData.info.Trim();

        if (nameText != null) nameText.text = displayName;
        SetData(inviteData.headSprite, $"好友请求 · {info}");
        SetButtonText(infoBtn, "同意");
        SetButtonText(rejectBtn, "拒绝");
    }

    /// <summary>
    /// 重置为池化状态（回收前调用）
    /// </summary>
    public void ResetForPool()
    {
        data = null;
        isProcessing = false;
        if (nameText != null) nameText.text = string.Empty;
        if (infoText != null) infoText.text = string.Empty;
        SetButtonsInteractable(true);
    }

    private void OnActionButtonClick()
    {
        if (data == null || isProcessing)
        {
            return;
        }

        if (string.IsNullOrEmpty(data.firebaseUid))
        {
            Debug.LogWarning("[InviteItem] 邀请数据不完整");
            return;
        }

        isProcessing = true;
        SetButtonsInteractable(false);
        ToastManager.ShowToast($"正在接受 {data.name} 的好友请求");
        Debug.Log($"[InviteItem] 正在接受好友请求：{data.name}");
        FirestoreManager.Instance.AcceptFriendRequest(data, success =>
        {
            isProcessing = false;
            SetButtonsInteractable(true);
            Debug.Log(success ? $"[InviteItem] 已添加好友：{data.name}" : "[InviteItem] 接受好友请求失败");
            ToastManager.ShowToast(success ? "已添加好友" : "接受好友请求失败");
        });
    }

    private void OnRejectButtonClick()
    {
        if (data == null || isProcessing || string.IsNullOrEmpty(data.firebaseUid))
        {
            Debug.LogWarning("[InviteItem] 邀请数据不完整");
            return;
        }

        isProcessing = true;
        SetButtonsInteractable(false);
        ToastManager.ShowToast($"正在拒绝 {data.name} 的好友请求");
        Debug.Log($"[InviteItem] 正在拒绝好友请求：{data.name}");
        FirestoreManager.Instance.RejectFriendRequest(data, success =>
        {
            isProcessing = false;
            SetButtonsInteractable(true);
            Debug.Log(success ? $"[InviteItem] 已拒绝好友请求：{data.name}" : "[InviteItem] 拒绝好友请求失败");
            ToastManager.ShowToast(success ? "已拒绝好友请求" : "拒绝失败，请稍后再试");
        });
    }

    private void EnsureRejectButton()
    {
        if (rejectBtn != null || infoBtn == null) return;

        GameObject rejectGO = Instantiate(infoBtn.gameObject, infoBtn.transform.parent);
        rejectGO.name = "[Button]RejectFriendRequest";
        rejectBtn = rejectGO.GetComponent<Button>();
        if (rejectBtn != null)
            rejectBtn.onClick.RemoveAllListeners();

        RectTransform source = infoBtn.GetComponent<RectTransform>();
        RectTransform target = rejectGO.GetComponent<RectTransform>();
        if (source != null && target != null)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.sizeDelta = source.sizeDelta;
            target.anchoredPosition = source.anchoredPosition + new Vector2(-(source.rect.width + 8f), 0f);
        }

        SetButtonText(rejectBtn, "拒绝");
        ApplyButtonVisual(rejectBtn, new Color(0.18f, 0.12f, 0.18f, 1f), new Color(0.96f, 0.70f, 0.74f, 1f));
    }

    private void SetButtonText(Button button, string text)
    {
        if (button == null) return;
        TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>(true);
        if (buttonText != null) buttonText.text = text;
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (infoBtn != null) infoBtn.interactable = interactable;
        if (rejectBtn != null) rejectBtn.interactable = interactable;
    }

    private void ApplyCardVisualStyle()
    {
        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null && rect.sizeDelta.y < 104f)
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, 104f);

        Image bg = GetComponent<Image>();
        if (bg != null)
            bg.color = new Color(0.13f, 0.10f, 0.16f, 0.96f);

        if (nameText != null)
        {
            nameText.color = new Color(1f, 0.82f, 0.50f, 1f);
            nameText.fontStyle = FontStyles.Bold;
        }

        if (infoText != null)
            infoText.color = new Color(0.84f, 0.80f, 0.92f, 1f);

        ApplyButtonVisual(infoBtn, new Color(0.30f, 0.10f, 0.48f, 1f), Color.white);
        ApplyButtonVisual(rejectBtn, new Color(0.18f, 0.12f, 0.18f, 1f), new Color(0.96f, 0.70f, 0.74f, 1f));
    }

    private void ApplyButtonVisual(Button button, Color background, Color textColor)
    {
        if (button == null) return;

        Image image = button.GetComponent<Image>();
        if (image != null)
            image.color = background;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.color = textColor;
            text.fontStyle = FontStyles.Bold;
        }
    }
}
