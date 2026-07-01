using System;
using UnityEngine;
using UnityEngine.UI;
using GamerFrameWork.UIFrameWork;
using TMPro;

public class FriendItem : MonoBehaviour
{
    public Sprite defaultSprite;
    public Image headImage;
    public TMP_Text nameText;
    public TMP_Text infoText;
    public Button moreBtn;

    private FriendDataManager.FriendData data;
    private FriendDataManager.InviteData requestData;
    private Button itemButton;
    private FriendSwipeRevealItem swipeRevealItem;
    private Action<FriendDataManager.InviteData> requestAcceptAction;
    private bool isRequestMode;
    private bool requestAlreadyAdded;
    private int avatarRequestVersion;
    private void OnEnable()
    {
        ResolveButtons();
        ResolveSwipeRevealItem();
        BindButtons();
    }
    private void OnDisable()
    {
        avatarRequestVersion++;
        swipeRevealItem?.ResetReveal(true);
        UnbindButtons();
    }

    private void ResolveButtons()
    {
        if (moreBtn == null) moreBtn = FindButtonByName(transform,
            "atBtn", "AtButton", "[Button]At", "@Button", "moreBtn", "[Button]More", "[Button]FriendMove");
        if (itemButton == null) itemButton = GetComponent<Button>();
        if (itemButton == null) itemButton = gameObject.AddComponent<Button>();
        itemButton.transition = Selectable.Transition.None;
    }

    private void ResolveSwipeRevealItem()
    {
        if (swipeRevealItem == null)
            swipeRevealItem = GetComponent<FriendSwipeRevealItem>();
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

    private void BindButtons()
    {
        UnbindButtons();
        if (isRequestMode)
        {
            if (moreBtn != null) moreBtn.onClick.AddListener(OpenFriendRequestPreview);
            if (itemButton != null && itemButton != moreBtn) itemButton.onClick.AddListener(OpenFriendRequestPreview);
            return;
        }

        if (moreBtn != null) moreBtn.onClick.AddListener(OpenDialogWithAtFriend);
        if (itemButton != null && itemButton != moreBtn) itemButton.onClick.AddListener(OpenFriendProfile);
    }

    private void UnbindButtons()
    {
        if (moreBtn != null) moreBtn.onClick.RemoveListener(OnFriendRequestActionButtonClick);
        if (moreBtn != null) moreBtn.onClick.RemoveListener(OpenFriendRequestPreview);
        if (moreBtn != null) moreBtn.onClick.RemoveListener(OpenDialogWithAtFriend);
        if (itemButton != null && itemButton != moreBtn) itemButton.onClick.RemoveListener(OpenFriendRequestPreview);
        if (itemButton != null && itemButton != moreBtn) itemButton.onClick.RemoveListener(OpenFriendProfile);
    }

    private void OpenDialogWithAtFriend()
    {
        if (ShouldIgnoreClickAfterSwipe())
            return;

        if (CloseOpenSwipeReveal())
            return;

        if (data == null)
        {
            ToastManager.ShowToast("好友资料不完整");
            return;
        }

        UIModule.Instance.GetWindow<NavigationUI>()?.OpenDialogUI();
        DialogUI dialog = UIModule.Instance.GetWindow<DialogUI>();
        dialog?.SendAtFriendsMessage(data);
        ToastManager.ShowToast($"已 @ {FormatFriendName(data)}");
    }

    private void OpenFriendProfile()
    {
        if (ShouldIgnoreClickAfterSwipe())
            return;

        if (CloseOpenSwipeReveal())
            return;

        if (data == null)
        {
            ToastManager.ShowToast("好友资料不完整");
            return;
        }

        if (data.isVirtual)
        {
            FriendDataManager.Instance?.RecordVirtualFriendOperated(data);
            FriendPreviewUI.Show(data);
            return;
        }

        if (FriendProfileUI.CanShowForFriend(data))
        {
            FriendPreviewUI.Show(data);
            return;
        }

        ToastManager.ShowToast("好友关系确认后才能查看资料");
    }

    private bool CloseOpenSwipeReveal()
    {
        if (swipeRevealItem != null && swipeRevealItem.IsOpen)
        {
            swipeRevealItem.Close();
            return true;
        }

        return FriendSwipeRevealItem.CloseCurrentOpen(swipeRevealItem);
    }

    private bool ShouldIgnoreClickAfterSwipe()
    {
        return swipeRevealItem != null && swipeRevealItem.ConsumeClickBlock();
    }

    private string FormatFriendName(FriendDataManager.FriendData friend)
    {
        if (friend == null) return "好友";
        if (!string.IsNullOrWhiteSpace(friend.name)) return friend.name.Trim();
        if (!string.IsNullOrWhiteSpace(friend.handle)) return friend.handle.Trim();
        return "好友";
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
        FriendAvatarImageUtility.ApplyAvatar(headImage, sprite, GetDefaultAvatarSprite());
        if (nameText != null) nameText.text = name;
        if (infoText != null)
        {
            infoText.richText = true;
            infoText.text = info;
        }
    }

    /// <summary>
    /// 通过 FriendData 设置好友数据显示
    /// </summary>
    public void SetData(FriendDataManager.FriendData friendData)
    {
        SetData(friendData, null);
    }

    public void SetData(FriendDataManager.FriendData friendData, string displayInfo)
    {
        ResolveButtons();
        ResolveSwipeRevealItem();
        UnbindButtons();
        BeginRebind();
        isRequestMode = false;
        requestData = null;
        requestAcceptAction = null;
        requestAlreadyAdded = false;
        data = friendData;
        if (friendData == null)
        {
            ResetForPool();
            BindButtons();
            return;
        }

        string info = string.IsNullOrWhiteSpace(displayInfo) ? friendData.info : displayInfo;
        Sprite avatar = FriendAvatarImageUtility.ResolveFriendAvatar(friendData, headImage, GetDefaultAvatarSprite());
        SetData(avatar, friendData.name, info);
        LoadRemoteAvatarIfNeeded(friendData);

        if (moreBtn != null)
        {
            moreBtn.gameObject.SetActive(false);
            moreBtn.interactable = false;
            SetButtonImageVisible(moreBtn, true);
            SetButtonText(moreBtn, "@");
        }

        if (swipeRevealItem != null)
            swipeRevealItem.Configure(OpenDeleteFriendFromSwipe, true);

        BindButtons();
    }

    public void SetFriendRequestData(
        FriendDataManager.InviteData inviteData,
        bool alreadyAdded,
        bool processing,
        Action<FriendDataManager.InviteData> onAccept)
    {
        ResolveButtons();
        ResolveSwipeRevealItem();
        UnbindButtons();
        BeginRebind();

        isRequestMode = true;
        data = null;
        requestData = inviteData;
        requestAlreadyAdded = alreadyAdded;
        requestAcceptAction = onAccept;

        if (inviteData == null)
        {
            ResetForPool();
            BindButtons();
            return;
        }

        string displayName = FormatInviteName(inviteData);
        string signature = FormatInviteSignature(inviteData);
        Sprite avatar = FriendAvatarImageUtility.ResolveInviteAvatar(inviteData, headImage, GetDefaultAvatarSprite());
        SetData(avatar, displayName, signature);
        LoadRemoteAvatarIfNeeded(inviteData);

        if (moreBtn != null)
        {
            moreBtn.gameObject.SetActive(true);
            moreBtn.interactable = true;
            SetButtonImageVisible(moreBtn, true);
            SetButtonText(moreBtn, alreadyAdded ? "已添加" : "查看");
        }

        if (swipeRevealItem != null)
            swipeRevealItem.Configure(null, false);

        BindButtons();
    }

    /// <summary>
    /// 重置为池化状态（回收前调用）
    /// </summary>
    public void ResetForPool()
    {
        data = null;
        requestData = null;
        requestAcceptAction = null;
        requestAlreadyAdded = false;
        isRequestMode = false;
        FriendAvatarImageUtility.ApplyAvatar(headImage, GetDefaultAvatarSprite());
        if (nameText != null) nameText.text = string.Empty;
        if (infoText != null) infoText.text = string.Empty;
        if (moreBtn != null)
        {
            moreBtn.gameObject.SetActive(false);
            moreBtn.interactable = true;
            SetButtonImageVisible(moreBtn, true);
        }
        if (swipeRevealItem != null)
            swipeRevealItem.Configure(null, false);
        avatarRequestVersion++;
    }

    private void OpenDeleteFriendFromSwipe()
    {
        if (data == null)
        {
            ToastManager.ShowToast("好友资料不完整");
            return;
        }

        FriendUI friendUI = UIModule.Instance.GetWindow<FriendUI>();
        if (friendUI != null)
        {
            friendUI.ShowDeleteFriendConfirm(data);
            return;
        }

        FriendMoveUI.ShowDeleteConfirmFor(data);
    }

    private void SetButtonText(Button button, string text)
    {
        if (button == null) return;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = text;
        TMPro.TMP_Text tmpLabel = button.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (tmpLabel != null) tmpLabel.text = text;
    }

    private void SetButtonImageVisible(Button button, bool visible)
    {
        if (button == null) return;
        Image image = button.GetComponent<Image>();
        if (image != null) image.enabled = visible;
    }

    private void OnFriendRequestActionButtonClick()
    {
        if (!isRequestMode || requestAlreadyAdded)
            return;

        if (requestData == null)
        {
            ToastManager.ShowToast("好友请求数据不完整");
            return;
        }

        requestAcceptAction?.Invoke(requestData);
    }

    private void OpenFriendRequestPreview()
    {
        if (!isRequestMode)
            return;

        if (ShouldIgnoreClickAfterSwipe())
            return;

        if (requestData == null || string.IsNullOrWhiteSpace(requestData.firebaseUid))
        {
            ToastManager.ShowToast("好友请求数据不完整");
            return;
        }

        FriendDataManager.FriendData friend = FriendDataManager.Instance != null
            ? FriendDataManager.Instance.FindRealFriendByFirebaseUid(requestData.firebaseUid)
            : null;
        if (friend != null)
        {
            FriendPreviewUI.Show(friend);
            return;
        }

        FriendPreviewUI.Show(requestData);
    }

    private void BeginRebind()
    {
        avatarRequestVersion++;
        swipeRevealItem?.ResetReveal(true);
    }

    private string FormatInviteName(FriendDataManager.InviteData invite)
    {
        if (invite == null) return "新的好友请求";
        if (!string.IsNullOrWhiteSpace(invite.name)) return invite.name.Trim();
        if (!string.IsNullOrWhiteSpace(invite.email)) return invite.email.Trim();
        return "新的好友请求";
    }

    private string FormatInviteSignature(FriendDataManager.InviteData invite)
    {
        if (invite == null || string.IsNullOrWhiteSpace(invite.info))
            return "想添加你为好友";
        return invite.info.Trim();
    }

    private void LoadRemoteAvatarIfNeeded(FriendDataManager.FriendData friendData)
    {
        if (friendData == null || friendData.headSprite != null || string.IsNullOrWhiteSpace(friendData.photoUrl))
        {
            return;
        }

        int requestId = ++avatarRequestVersion;
        StartCoroutine(FriendAvatarImageUtility.LoadUserSpriteFromUrlCoroutine(GetFriendAvatarName(friendData), friendData.photoUrl, sprite =>
        {
            if (requestId != avatarRequestVersion || data != friendData || sprite == null) return;
            friendData.headSprite = sprite;
            FriendAvatarImageUtility.ApplyAvatar(headImage, sprite, GetDefaultAvatarSprite());
        }));
    }

    private void LoadRemoteAvatarIfNeeded(FriendDataManager.InviteData inviteData)
    {
        if (inviteData == null || inviteData.headSprite != null || string.IsNullOrWhiteSpace(inviteData.photoUrl))
        {
            return;
        }

        int requestId = ++avatarRequestVersion;
        StartCoroutine(FriendAvatarImageUtility.LoadUserSpriteFromUrlCoroutine(GetInviteAvatarName(inviteData), inviteData.photoUrl, sprite =>
        {
            if (requestId != avatarRequestVersion || requestData != inviteData || sprite == null) return;
            inviteData.headSprite = sprite;
            FriendAvatarImageUtility.ApplyAvatar(headImage, sprite, GetDefaultAvatarSprite());
        }));
    }

    private string GetFriendAvatarName(FriendDataManager.FriendData friendData)
    {
        if (!string.IsNullOrWhiteSpace(friendData?.name)) return friendData.name.Trim();
        if (!string.IsNullOrWhiteSpace(friendData?.handle)) return friendData.handle.Trim();
        return "好友";
    }

    private string GetInviteAvatarName(FriendDataManager.InviteData inviteData)
    {
        if (!string.IsNullOrWhiteSpace(inviteData?.name)) return inviteData.name.Trim();
        if (!string.IsNullOrWhiteSpace(inviteData?.email)) return inviteData.email.Trim();
        return "好友请求";
    }

    private Sprite GetDefaultAvatarSprite()
    {
        return defaultSprite != null ? defaultSprite : FriendAvatarImageUtility.DefaultAvatarSprite;
    }
}
