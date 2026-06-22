/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/20/2026 10:47:05 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class FriendMoveUI : WindowBase
{
    public FriendMoveUIComponent uiComponent;
    private static FriendDataManager.FriendData sPendingFriend;

    private FriendDataManager.FriendData currentFriend;
    private bool isProcessing;

    public static void Show(FriendDataManager.FriendData friend)
    {
        if (friend == null)
        {
            ToastManager.ShowToast("好友资料不完整");
            return;
        }

        sPendingFriend = friend;
        UIModule.Instance.PopUpWindow<FriendMoveUI>();
    }

    #region 生命周期函数
    // 调用机制与 Mono Awake 一致
    public override void OnAwake()
    {
        uiComponent = gameObject.GetComponent<FriendMoveUIComponent>();
        if (uiComponent == null)
        {
            Debug.LogError("FriendMoveUI 缺少 UI 组件绑定脚本：FriendMoveUIComponent");
            return;
        }

        EnsureBlockFriendButton();
        uiComponent.InitComponent(this);
        this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
        base.OnAwake();
    }

    // 物体显示时执行
    public override void OnShow()
    {
        base.OnShow();
        currentFriend = sPendingFriend;
        isProcessing = false;
        RefreshView();
    }

    // 物体隐藏时执行
    public override void OnHide()
    {
        currentFriend = null;
        sPendingFriend = null;
        base.OnHide();
    }

    // 物体销毁时执行
    public override void OnDestroy()
    {
        base.OnDestroy();
    }
    #endregion

    #region API Function

    #endregion

    #region UI组件事件
    public void OnViewFriendButtonClick()
    {
        if (currentFriend == null)
        {
            ToastManager.ShowToast("好友资料不完整");
            return;
        }

        if (currentFriend.isVirtual)
        {
            HideWindow();
            CreateFriendInfoUI.Show(currentFriend);
            return;
        }

        if (CanOpenRealFriendProfile(currentFriend))
        {
            HideWindow();
            FriendProfileUI.Show(currentFriend);
            return;
        }

        ToastManager.ShowToast("好友关系确认后才能查看资料");
    }

	public void OnSendOracleRelatonButtonClick()
	{
		if (currentFriend == null || isProcessing)
		{
			return;
		}

		if (CreatedFriendRelationshipDivinationLocalFlow.CanHandle(currentFriend))
		{
			FriendDataManager.FriendData capturedLocal = currentFriend;
			HideWindow();
			CreatedFriendRelationshipDivinationLocalFlow.TryStart(capturedLocal);
			return;
		}

		if (!RelationshipDivinationFlow.CanUseTwoPersonDivination(currentFriend, true))
		{
			RefreshView();
            return;
        }

        if (!currentFriend.isVirtual && !CanOpenRealFriendProfile(currentFriend))
        {
            ToastManager.ShowToast("好友关系确认后才能发起双人占卜");
            RefreshView();
            return;
        }

        FriendDataManager.FriendData captured = currentFriend;
        HideWindow();
        RelationshipDivinationOverlay.StartForFriend(transform, captured);
    }

    public void OnDeleteFriendButtonClick()
    {
        if (currentFriend == null || isProcessing)
        {
            return;
        }

        ShowDeleteConfirm(currentFriend);
    }

    public void OnBlockFriendButtonClick()
    {
        if (currentFriend == null || isProcessing)
        {
            return;
        }

        ShowBlockConfirm(currentFriend);
    }

    public void OnCancelButtonClick()
    {
        HideWindow();
    }
    #endregion

    private void RefreshView()
    {
        if (uiComponent == null) return;

        bool hasFriend = currentFriend != null;
        bool isRealFriend = hasFriend && !currentFriend.isVirtual;
		bool canOpenProfile = hasFriend && (currentFriend.isVirtual || CanOpenRealFriendProfile(currentFriend));
		bool canUseRelationshipDivination = hasFriend
			&& (CreatedFriendRelationshipDivinationLocalFlow.CanHandle(currentFriend)
				|| (CanOpenRealFriendProfile(currentFriend)
					&& RelationshipDivinationFlow.CanUseTwoPersonDivination(currentFriend, false)));

        SetButtonVisible(uiComponent.ViewFriendButton, canOpenProfile);
        SetButtonVisible(uiComponent.SendOracleRelatonButton, canUseRelationshipDivination);
        SetButtonVisible(uiComponent.DeleteFriendButton, hasFriend);
        SetButtonVisible(uiComponent.BlockFriendButton, isRealFriend);
        SetButtonVisible(uiComponent.CancelButton, true);

        SetButtonInteractable(uiComponent.ViewFriendButton, !isProcessing && canOpenProfile);
        SetButtonInteractable(uiComponent.SendOracleRelatonButton, !isProcessing && canUseRelationshipDivination);
        SetButtonInteractable(uiComponent.DeleteFriendButton, !isProcessing && hasFriend);
        SetButtonInteractable(uiComponent.BlockFriendButton, !isProcessing && isRealFriend);
        SetButtonInteractable(uiComponent.CancelButton, !isProcessing);

        SetButtonText(uiComponent.ViewFriendButton, currentFriend != null && currentFriend.isVirtual ? "编辑好友资料" : "查看好友资料");
        SetButtonText(uiComponent.SendOracleRelatonButton, "发起关系占卜");
        SetButtonText(uiComponent.DeleteFriendButton, "删除好友");
        SetButtonText(uiComponent.BlockFriendButton, "屏蔽好友");
        SetButtonText(uiComponent.CancelButton, "取消");
    }

    private void ShowDeleteConfirm(FriendDataManager.FriendData friend)
    {
        if (friend == null) return;

        DeleteAccountUI.ShowDeleteFriend(friend, () => BeginDeleteFriend(friend));
    }

    private void ShowBlockConfirm(FriendDataManager.FriendData friend)
    {
        if (friend == null || friend.isVirtual) return;

        string name = FormatFriendName(friend);
        SelectWindow selectWindow = UIModule.Instance.PopUpWindow<SelectWindow>();
        if (selectWindow == null)
        {
            ToastManager.ShowToast("确认窗口打开失败");
            return;
        }

        selectWindow.InitViewState(
            SelectType.Normal,
            $"确定屏蔽「{name}」吗？屏蔽后会解除好友关系，并且搜索结果会显示为已屏蔽。",
            () => BeginBlockFriend(friend),
            null,
            "屏蔽",
            "取消",
            "屏蔽好友",
            TextAnchor.MiddleCenter);
    }

    private void BeginDeleteFriend(FriendDataManager.FriendData friend)
    {
        if (friend == null || isProcessing)
        {
            return;
        }

        isProcessing = true;
        RefreshView();

        if (friend.isVirtual)
        {
            DeleteVirtualFriend(friend);
            return;
        }

        DeleteRealFriend(friend);
    }

    private void BeginBlockFriend(FriendDataManager.FriendData friend)
    {
        if (friend == null || friend.isVirtual || isProcessing)
        {
            return;
        }

        isProcessing = true;
        RefreshView();
        BlockRealFriend(friend);
    }

    private void DeleteVirtualFriend(FriendDataManager.FriendData friend)
    {
        if (FirestoreManager.Instance != null && FirestoreManager.Instance.IsInitialized)
        {
            ToastManager.ShowToast($"正在删除 {FormatFriendName(friend)}");
            FirestoreManager.Instance.DeleteVirtualFriend(friend, success =>
            {
                isProcessing = false;
                RefreshView();
                if (!success)
                {
                    ToastManager.ShowToast("云端删除失败，已保留本地好友");
                    return;
                }

                ToastManager.ShowToast("已删除创建的好友");
                HideWindow();
            });
            return;
        }

        bool removed = FriendDataManager.Instance != null && FriendDataManager.Instance.RemoveVirtualFriend(friend.id);
        isProcessing = false;
        RefreshView();
        ToastManager.ShowToast(removed ? "已删除本地创建好友" : "删除失败");
        if (removed)
            HideWindow();
    }

    private void DeleteRealFriend(FriendDataManager.FriendData friend)
    {
        if (FirestoreManager.Instance == null)
        {
            string friendUid = friend.firebaseUid;
            bool queued = !string.IsNullOrWhiteSpace(friendUid);
            FirestoreManager.QueueRealFriendDeleteLocal(friendUid);
            bool removed = RemoveRealFriendLocal(friend);
            isProcessing = false;
            RefreshView();
            ToastManager.ShowToast(BuildLocalDeleteToast(removed, queued));
            if (removed || queued)
                CloseAfterFriendRelationEnded();
            return;
        }

        if (!FirestoreManager.Instance.IsInitialized)
        {
            ToastManager.ShowToast($"正在删除 {FormatFriendName(friend)}");
            FirestoreManager.Instance.RemoveRealFriend(friend, success =>
            {
                isProcessing = false;
                RefreshView();
                ToastManager.ShowToast(success ? BuildLocalDeleteToast(true, !string.IsNullOrWhiteSpace(friend.firebaseUid)) : "删除好友失败，请稍后再试");
                if (success)
                    CloseAfterFriendRelationEnded();
            });
            return;
        }

        ToastManager.ShowToast($"正在删除 {FormatFriendName(friend)}");
        FirestoreManager.Instance.RemoveRealFriend(friend, success =>
        {
            isProcessing = false;
            RefreshView();
            ToastManager.ShowToast(success ? "已删除好友" : "删除好友失败，请稍后再试");
            if (success)
                CloseAfterFriendRelationEnded();
        });
    }

    private void BlockRealFriend(FriendDataManager.FriendData friend)
    {
        if (string.IsNullOrWhiteSpace(friend.firebaseUid))
        {
            bool removed = RemoveRealFriendLocal(friend);
            isProcessing = false;
            RefreshView();
            ToastManager.ShowToast(removed ? "好友缺少云端 ID，已从本地列表移除" : "屏蔽失败，请稍后再试");
            if (removed)
                CloseAfterFriendRelationEnded();
            return;
        }

        if (FirestoreManager.Instance == null)
        {
            string friendUid = friend.firebaseUid;
            bool queued = !string.IsNullOrWhiteSpace(friendUid);
            FirestoreManager.QueueRealFriendBlockLocal(friendUid);
            bool blocked = FriendDataManager.Instance != null && FriendDataManager.Instance.AddBlockedUser(friendUid);
            isProcessing = false;
            RefreshView();
            ToastManager.ShowToast(blocked ? "已屏蔽好友，云端稍后同步" : "已加入云端屏蔽队列");
            if (blocked || queued)
                CloseAfterFriendRelationEnded();
            return;
        }

        ToastManager.ShowToast($"正在屏蔽 {FormatFriendName(friend)}");
        FirestoreManager.Instance.BlockRealFriend(friend, success =>
        {
            isProcessing = false;
            RefreshView();
            string successText = FirestoreManager.Instance != null && FirestoreManager.Instance.IsInitialized ? "已屏蔽好友" : "已屏蔽好友，云端稍后同步";
            ToastManager.ShowToast(success ? successText : "屏蔽失败，请稍后再试");
            if (success)
                CloseAfterFriendRelationEnded();
        });
    }

    private bool RemoveRealFriendLocal(FriendDataManager.FriendData friend)
    {
        if (friend == null || FriendDataManager.Instance == null)
            return false;

        if (!string.IsNullOrWhiteSpace(friend.firebaseUid)
            && FriendDataManager.Instance.RemoveRealFriendByFirebaseUid(friend.firebaseUid))
            return true;

        return FriendDataManager.Instance.RemoveRealFriend(friend.id);
    }

    private string BuildLocalDeleteToast(bool removed, bool queued)
    {
        if (removed && queued) return "已删除好友，云端稍后同步";
        if (removed) return "已从本地好友列表移除";
        if (queued) return "已加入云端删除队列";
        return "删除好友失败，请稍后再试";
    }

    private bool CanOpenRealFriendProfile(FriendDataManager.FriendData friend)
    {
        return FriendProfileUI.CanShowForFriend(friend);
    }

    private void EnsureBlockFriendButton()
    {
        if (uiComponent == null || uiComponent.BlockFriendButton != null || uiComponent.DeleteFriendButton == null)
            return;

        Button clone = UnityEngine.Object.Instantiate(uiComponent.DeleteFriendButton, uiComponent.DeleteFriendButton.transform.parent);
        clone.name = "[Button]BlockFriend";
        clone.onClick.RemoveAllListeners();
        int deleteIndex = uiComponent.DeleteFriendButton.transform.GetSiblingIndex();
        clone.transform.SetSiblingIndex(deleteIndex + 1);
        uiComponent.BlockFriendButton = clone;
    }

    private void CloseAfterFriendRelationEnded()
    {
        HideWindow();
        UIModule.Instance.HideWindow<FriendProfileUI>();
    }

    private string FormatFriendName(FriendDataManager.FriendData friend)
    {
        return string.IsNullOrWhiteSpace(friend?.name) ? "好友" : friend.name.Trim();
    }

    private void SetButtonVisible(Button button, bool visible)
    {
        if (button != null)
            button.gameObject.SetActive(visible);
    }

    private void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    private void SetButtonText(Button button, string text)
    {
        if (button == null) return;
        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
            label.text = text ?? string.Empty;
    }
}
