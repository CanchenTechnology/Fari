using System;
using System.Collections;
using System.Collections.Generic;
using GamerFrameWork.UIFrameWork;
using UnityEngine;
using UnityEngine.UI;

public class FriendItem : MonoBehaviour
{
    public Sprite defaultSprite;
    public Image headImage;
    public Text nameText;
    public Text infoText;
    public Button viewBtn; //查看真实好友资料，或者编辑创建好友资料

    public Button oracleBtn; // 真实好友才有占卜按钮，虚拟好友没有占卜按钮
    public Button atBtn;
    public Button deleteBtn;

    private FriendDataManager.FriendData data;
    private bool isDeleting;
    private int avatarRequestVersion;
    private void OnEnable()
    {
        ResolveButtons();
        EnsureDeleteButton();
        if (atBtn != null) atBtn.onClick.AddListener(ContactFriendToUI);
        if (viewBtn != null) viewBtn.onClick.AddListener(ViewFriendProfile);
        if (oracleBtn != null) oracleBtn.onClick.AddListener(StartRelationshipDivination);
        if (deleteBtn != null) deleteBtn.onClick.AddListener(DeleteCurrentFriend);
    }
    private void OnDisable()
    {
        avatarRequestVersion++;
        if (atBtn != null) atBtn.onClick.RemoveListener(ContactFriendToUI);
        if (viewBtn != null) viewBtn.onClick.RemoveListener(ViewFriendProfile);
        if (oracleBtn != null) oracleBtn.onClick.RemoveListener(StartRelationshipDivination);
        if (deleteBtn != null) deleteBtn.onClick.RemoveListener(DeleteCurrentFriend);
    }

    private void ResolveButtons()
    {
        if (viewBtn == null) viewBtn = FindButtonByName(transform, "viewBtn");
        if (oracleBtn == null) oracleBtn = FindButtonByName(transform, "oracleBtn");
        if (atBtn == null) atBtn = FindButtonByName(transform, "artBtn", "atBtn");
        if (deleteBtn == null) deleteBtn = FindButtonByName(transform, "deleteBtn", "[Button]DeleteFriend");
    }

    private Button FindButtonByName(Transform root, params string[] names)
    {
        if (root == null || names == null) return null;

        foreach (string targetName in names)
        {
            if (root.name == targetName)
                return root.GetComponent<Button>();
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Button result = FindButtonByName(root.GetChild(i), names);
            if (result != null) return result;
        }

        return null;
    }

    private void ContactFriendToUI()
    {
        if (data == null)
        {
            return;
        }

        JumpToDialogUI.Show(data);
    }

    private void StartRelationshipDivination()
    {
        if (data == null)
        {
            return;
        }

        RelationshipDivinationOverlay.StartForFriend(transform, data);
    }

    private void ViewFriendProfile()
    {
        if (data == null)
        {
            return;
        }

        if (data.isVirtual)
        {
            CreateFriendInfoUI.Show(data);
            return;
        }

        if (CanOpenRealFriendProfile(data))
        {
            FriendProfileUI.Show(data);
            return;
        }

        ToastManager.ShowToast("好友关系确认后才能查看资料");
    }

    private bool CanOpenRealFriendProfile(FriendDataManager.FriendData friendData)
    {
        return FriendProfileUI.CanShowForFriend(friendData);
    }

    private void DeleteCurrentFriend()
    {
        if (data == null || isDeleting)
        {
            return;
        }

        FriendDataManager.FriendData captured = data;
        string title = captured.isVirtual ? "删除创建的好友" : "删除真实好友";
        string message = $"确定删除「{captured.name}」吗？删除后会从当前好友列表移除。";
        FriendRuntimeDialog.ShowConfirm(transform, title, message, "删除", () => BeginDeleteCurrentFriend(captured));
    }

    private void BeginDeleteCurrentFriend(FriendDataManager.FriendData friend)
    {
        if (friend == null || isDeleting)
        {
            return;
        }

        isDeleting = true;
        SetDeleteInteractable(false);

        if (friend.isVirtual)
        {
            DeleteVirtualFriend(friend);
            return;
        }

        DeleteRealFriend(friend);
    }

    private void DeleteVirtualFriend(FriendDataManager.FriendData friend)
    {
        if (FirestoreManager.Instance != null && FirestoreManager.Instance.IsInitialized)
        {
            ToastManager.ShowToast($"正在删除 {friend.name}");
            FirestoreManager.Instance.DeleteVirtualFriend(friend, success =>
            {
                isDeleting = false;
                SetDeleteInteractable(true);
                if (!success)
                {
                    ToastManager.ShowToast("云端删除失败，已保留本地好友");
                    return;
                }

                ToastManager.ShowToast("已删除创建的好友");
            });
            return;
        }

        bool removed = FriendDataManager.Instance.RemoveVirtualFriend(friend.id);
        isDeleting = false;
        SetDeleteInteractable(true);
        ToastManager.ShowToast(removed ? "已删除本地创建好友" : "删除失败");
    }

    private void DeleteRealFriend(FriendDataManager.FriendData friend)
    {
        if (FirestoreManager.Instance == null || !FirestoreManager.Instance.IsInitialized)
        {
            ToastManager.ShowToast("好友服务未初始化，无法删除真实好友");
            isDeleting = false;
            SetDeleteInteractable(true);
            return;
        }

        ToastManager.ShowToast($"正在删除 {friend.name}");
        FirestoreManager.Instance.RemoveRealFriend(friend, success =>
        {
            isDeleting = false;
            SetDeleteInteractable(true);
            ToastManager.ShowToast(success ? "已删除好友" : "删除好友失败，请稍后再试");
        });
    }

    private void SetDeleteInteractable(bool interactable)
    {
        if (deleteBtn != null)
            deleteBtn.interactable = interactable;
    }
    /// <summary>
    /// 当前绑定的好友数据
    /// </summary>
    public FriendDataManager.FriendData Data => data;

    /// <summary>
    /// 设置好友数据显示
    /// </summary>
    public void SetData(Sprite sprite, string name, string info)
    {
        headImage.sprite = sprite ? sprite : defaultSprite;
        nameText.text = name;
        infoText.text = info;
    }

    /// <summary>
    /// 通过 FriendData 设置好友数据显示
    /// </summary>
    public void SetData(FriendDataManager.FriendData friendData)
    {
        ResolveButtons();
        EnsureDeleteButton();
        data = friendData;
        SetData(friendData.headSprite, friendData.name, friendData.info);
        LoadRemoteAvatarIfNeeded(friendData);

        bool canOpenProfile = friendData != null && (friendData.isVirtual || CanOpenRealFriendProfile(friendData));
        if (viewBtn != null)
            viewBtn.gameObject.SetActive(canOpenProfile);
        if (oracleBtn != null)
            oracleBtn.gameObject.SetActive(friendData != null && (friendData.isVirtual || CanOpenRealFriendProfile(friendData)));
        if (deleteBtn != null)
            deleteBtn.gameObject.SetActive(friendData != null);
    }

    /// <summary>
    /// 重置为池化状态（回收前调用）
    /// </summary>
    public void ResetForPool()
    {
        data = null;
        headImage.sprite = defaultSprite;
        nameText.text = string.Empty;
        infoText.text = string.Empty;
        if (viewBtn != null) viewBtn.gameObject.SetActive(false);
        if (oracleBtn != null) oracleBtn.gameObject.SetActive(false);
        if (deleteBtn != null) deleteBtn.gameObject.SetActive(false);
        isDeleting = false;
        avatarRequestVersion++;
    }

    private void LoadRemoteAvatarIfNeeded(FriendDataManager.FriendData friendData)
    {
        if (friendData == null || friendData.headSprite != null || string.IsNullOrWhiteSpace(friendData.photoUrl))
        {
            return;
        }

        int requestId = ++avatarRequestVersion;
        StartCoroutine(FriendAvatarImageUtility.LoadSpriteFromUrlCoroutine(friendData.photoUrl, sprite =>
        {
            if (requestId != avatarRequestVersion || data != friendData || sprite == null) return;
            friendData.headSprite = sprite;
            if (headImage != null) headImage.sprite = sprite;
        }));
    }

    private void EnsureDeleteButton()
    {
        if (deleteBtn != null) return;

        Button sourceButton = viewBtn != null ? viewBtn : atBtn;
        if (sourceButton == null) return;

        GameObject deleteGO = Instantiate(sourceButton.gameObject, sourceButton.transform.parent);
        deleteGO.name = "[Button]DeleteFriend";
        deleteBtn = deleteGO.GetComponent<Button>();
        if (deleteBtn != null)
        {
            deleteBtn.onClick.RemoveAllListeners();
            deleteBtn.interactable = true;
        }

        RectTransform source = sourceButton.GetComponent<RectTransform>();
        RectTransform target = deleteGO.GetComponent<RectTransform>();
        if (source != null && target != null)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.sizeDelta = source.sizeDelta;
            float offset = Mathf.Max(source.rect.width, source.sizeDelta.x, 44f) + 8f;
            target.anchoredPosition = source.anchoredPosition + new Vector2(offset, 0f);
        }

        Image buttonImage = deleteGO.GetComponent<Image>();
        if (buttonImage != null)
            buttonImage.color = new Color(0.28f, 0.07f, 0.12f, buttonImage.color.a);

        SetButtonText(deleteBtn, "删");
        deleteGO.SetActive(data != null);
    }

    private void SetButtonText(Button button, string text)
    {
        if (button == null) return;
        Text label = button.GetComponentInChildren<Text>(true);
        if (label == null)
        {
            GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(button.transform, false);
            RectTransform rect = textGO.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            label = textGO.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.alignment = TextAnchor.MiddleCenter;
        }

        label.text = text;
        label.fontSize = Mathf.Max(label.fontSize, 14);
        label.color = Color.white;
    }
}
