/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/20/2026 10:30:43 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class TwoPersonDivinationResultFlowUI : WindowBase
{
	public TwoPersonDivinationResultFlowUIComponent uiComponent;
	private RelationshipDivinationRecord currentRecord;
	private FriendDataManager.FriendData currentFriend;
	private DivinationRecordData currentRecordData;
	private bool hasLoggedSaveButtonAutoEnable;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<TwoPersonDivinationResultFlowUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("TwoPersonDivinationResultFlowUI 缺少 UI 组件绑定脚本：TwoPersonDivinationResultFlowUIComponent");
			return;
		}
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		EnsureSaveButtonInteractable();
		RefreshFromFlow();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		base.OnHide();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		base.OnDestroy();
	}
	#endregion

	#region API Function
	public void RefreshFromFlow()
	{
		currentRecord = RelationshipDivinationFlow.CurrentRecord;
		currentFriend = RelationshipDivinationFlow.CurrentFriend;
		currentRecordData = currentRecord != null ? RelationshipDivinationFlow.BuildDivinationRecord(currentRecord) : null;

		if (currentRecordData != null)
			DialogSystem.Instance?.ActivateReadingFromRecord(currentRecordData, DivinationPhase.Completed);

		Render();
	}

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		HideWindow();
	}
	public void OnContinueChatButtonClick()
	{
		if (currentRecordData == null)
		{
			ToastManager.ShowToast("暂无可继续解读的双人占卜结果");
			return;
		}

		DialogSystem.Instance?.ActivateReadingFromRecord(currentRecordData, DivinationPhase.FollowUp);
		HideWindow();
		UIModule.Instance.PopUpWindow<DialogUI>();
	}
	public void OnSaveHistoryButtonClick()
	{
		if (currentRecord == null)
		{
			SetSaveButtonState(true, "保存到历史");
			ToastManager.ShowToast("暂无可保存的双人占卜结果");
			return;
		}

		currentRecordData ??= RelationshipDivinationFlow.BuildDivinationRecord(currentRecord);

		DivinationRecordFirestore store = RelationshipDivinationFlow.GetOrCreateHistoryService();
		if (store == null)
		{
			SetSaveButtonState(true, "保存到历史");
			ToastManager.ShowToast("历史服务暂不可用");
			return;
		}

		store.SaveRecord(currentRecordData, success =>
		{
			SetSaveButtonState(true, "保存到历史");
			ToastManager.ShowToast(success ? "已保存到历史" : "已保存到本地，云端稍后同步");
			if (success)
				Debug.Log("[TwoPersonDivinationResultFlowUI] 双人占卜历史已保存");
			else
				Debug.LogWarning("[TwoPersonDivinationResultFlowUI] 云端历史保存失败");
		});
	}
		#endregion

	private void LateUpdate()
	{
		EnsureSaveButtonInteractable();
	}

	private void Render()
	{
		if (uiComponent == null)
			return;

		if (currentRecord == null)
		{
			SetSaveButtonState(true, "保存到历史");
			ToastManager.ShowToast("占卜结果不存在");
			return;
		}

		RelationshipDivinationCard myCard = RelationshipDivinationFlow.GetMyPrivateCard(currentRecord);
		RelationshipDivinationCard friendCard = RelationshipDivinationFlow.GetFriendPrivateCard(currentRecord);
		RelationshipDivinationCard sharedCard = currentRecord.SharedCard;

		ApplyCardSprite(uiComponent.MyCardImage, myCard);
		ApplyCardSprite(uiComponent.FriendCardImage, friendCard);
		ApplyCardSprite(uiComponent.relationCardImage, sharedCard);

		if (uiComponent.Card1DescText != null)
			uiComponent.Card1DescText.text = BuildCardDescription(myCard, "你的感受");
		if (uiComponent.Card2DescText != null)
			uiComponent.Card2DescText.text = BuildCardDescription(friendCard, "对方回应");
		if (uiComponent.Card3DescText != null)
			uiComponent.Card3DescText.text = BuildCardDescription(sharedCard, "关系走向");
		if (uiComponent.SummaryDescText != null)
			uiComponent.SummaryDescText.text = BuildSummaryText(myCard, friendCard, sharedCard);

		SetSaveButtonState(true, "保存到历史");
	}

	private void SetSaveButtonState(bool interactable, string label)
	{
		if (uiComponent?.SaveHistoryButton == null)
			return;

		uiComponent.SaveHistoryButton.interactable = interactable;
		RelationshipDivinationFlow.SetButtonText(uiComponent.SaveHistoryButton, label);
	}

	private void EnsureSaveButtonInteractable()
	{
		if (uiComponent?.SaveHistoryButton == null)
			return;

		if (!uiComponent.SaveHistoryButton.interactable)
		{
			uiComponent.SaveHistoryButton.interactable = true;
			RelationshipDivinationFlow.SetButtonText(uiComponent.SaveHistoryButton, "保存到历史");
			if (!hasLoggedSaveButtonAutoEnable)
			{
				hasLoggedSaveButtonAutoEnable = true;
				Debug.LogWarning("[TwoPersonDivinationResultFlowUI] SaveHistoryButton 被置为不可交互，已自动恢复。");
			}
		}
	}

	private void ApplyCardSprite(Image target, RelationshipDivinationCard card)
	{
		if (target == null) return;

		Sprite sprite = RelationshipDivinationFlow.LoadCardSprite(card);
		if (sprite != null)
			target.sprite = sprite;
	}

	private string BuildCardDescription(RelationshipDivinationCard card, string role)
	{
		if (card == null)
			return $"{role}暂未揭示。";

		string cardName = string.IsNullOrWhiteSpace(card.cardName) ? "这张牌" : card.DisplayName;
		if (role == "你的感受")
			return $"「{cardName}」提示你看见自己的真实期待，以更柔和的方式靠近这段关系。";
		if (role == "对方回应")
			return $"「{cardName}」显示对方也在整理心意，正在寻找更合适的回应方式。";

		return $"「{cardName}」作为共同揭示，提醒你们把真诚沟通落到下一步行动里。";
	}

	private string BuildSummaryText(RelationshipDivinationCard myCard, RelationshipDivinationCard friendCard, RelationshipDivinationCard sharedCard)
	{
		string otherName = RelationshipDivinationFlow.GetFriendName(currentFriend);
		string sharedName = sharedCard != null ? sharedCard.DisplayName : "共同牌";
		string myName = myCard != null ? myCard.DisplayName : "你的牌";
		string friendName = friendCard != null ? friendCard.DisplayName : "对方的牌";

		return $"你与 {otherName} 的牌面由「{myName}」「{friendName}」和共同揭示「{sharedName}」组成。保持开放沟通，坦诚表达感受，一起创造更稳定的连接。";
	}
}
