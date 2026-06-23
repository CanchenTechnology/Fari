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
    private Button itemButton;
    private int avatarRequestVersion;
    private void OnEnable()
    {
        ResolveButtons();
        BindButtons();
    }
    private void OnDisable()
    {
        avatarRequestVersion++;
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
        if (moreBtn != null) moreBtn.onClick.AddListener(OpenDialogWithAtFriend);
        if (itemButton != null && itemButton != moreBtn) itemButton.onClick.AddListener(OpenFriendProfile);
    }

    private void UnbindButtons()
    {
        if (moreBtn != null) moreBtn.onClick.RemoveListener(OpenDialogWithAtFriend);
        if (itemButton != null && itemButton != moreBtn) itemButton.onClick.RemoveListener(OpenFriendProfile);
    }

    private void OpenDialogWithAtFriend()
    {
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
        if (data == null)
        {
            ToastManager.ShowToast("好友资料不完整");
            return;
        }

        if (data.isVirtual)
        {
            CreateFriendInfoUI.Show(data);
            return;
        }

        if (FriendProfileUI.CanShowForFriend(data))
        {
            FriendProfileUI.Show(data);
            return;
        }

        ToastManager.ShowToast("好友关系确认后才能查看资料");
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
        data = friendData;
        if (friendData == null)
        {
            ResetForPool();
            return;
        }

        SetData(friendData.headSprite, friendData.name, friendData.info);
        LoadRemoteAvatarIfNeeded(friendData);

        if (moreBtn != null)
        {
            moreBtn.gameObject.SetActive(friendData != null);
            SetButtonText(moreBtn, "@");
        }
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
        if (moreBtn != null) moreBtn.gameObject.SetActive(false);
        avatarRequestVersion++;
    }

    private void SetButtonText(Button button, string text)
    {
        if (button == null) return;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = text;
        TMPro.TMP_Text tmpLabel = button.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (tmpLabel != null) tmpLabel.text = text;
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
}
