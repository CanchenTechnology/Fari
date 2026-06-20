using UnityEngine;
using UnityEngine.UI;

public class FriendItem : MonoBehaviour
{
    public Sprite defaultSprite;
    public Image headImage;
    public Text nameText;
    public Text infoText;
    public Button moreBtn;

    private FriendDataManager.FriendData data;
    private int avatarRequestVersion;
    private void OnEnable()
    {
        ResolveButtons();
        if (moreBtn != null) moreBtn.onClick.AddListener(OpenFriendMoveUI);
    }
    private void OnDisable()
    {
        avatarRequestVersion++;
        if (moreBtn != null) moreBtn.onClick.RemoveListener(OpenFriendMoveUI);
    }

    private void ResolveButtons()
    {
        if (moreBtn == null) moreBtn = FindButtonByName(transform, "moreBtn", "[Button]More", "[Button]FriendMove");
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

    private void OpenFriendMoveUI()
    {
        if (data == null)
        {
            return;
        }

        FriendMoveUI.Show(data);
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
            moreBtn.gameObject.SetActive(friendData != null);
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
