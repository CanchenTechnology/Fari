using GamerFrameWork.UIFrameWork;
using UnityEngine;

public class TempUI : WindowBase
{
	public TempUIComponent uiComponent;

	private static readonly Vector2 DesignSize = new Vector2(390f, 844f);

	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<TempUIComponent>();
		if (uiComponent != null)
		{
			uiComponent.InitComponent(this);
			if (Canvas != null)
			{
				Canvas.sortingOrder = (int)uiComponent.windowLayer;
			}
		}

		base.OnAwake();
		ApplyDesignFit();
	}

	public override void OnShow()
	{
		base.OnShow();
		ApplyDesignFit();
	}

	public void ApplyDesignFit()
	{
		if (uiComponent == null || uiComponent.designRoot == null)
		{
			return;
		}

		RectTransform designRoot = uiComponent.designRoot;
		RectTransform parent = designRoot.parent as RectTransform;
		float parentWidth = parent != null && parent.rect.width > 1f ? parent.rect.width : 1080f;
		float parentHeight = parent != null && parent.rect.height > 1f ? parent.rect.height : 1920f;
		float scale = Mathf.Min(parentWidth / DesignSize.x, parentHeight / DesignSize.y);

		designRoot.anchorMin = new Vector2(0.5f, 0.5f);
		designRoot.anchorMax = new Vector2(0.5f, 0.5f);
		designRoot.pivot = new Vector2(0.5f, 0.5f);
		designRoot.anchoredPosition = Vector2.zero;
		designRoot.sizeDelta = DesignSize;
		designRoot.localScale = Vector3.one * scale;
	}

	public void OnBackButtonClick()
	{
	}

	public void OnSettingButtonClick()
	{
	}

	public void OnAddButtonClick()
	{
	}

	public void OnDivinationHistoryButtonClick()
	{
	}
}
