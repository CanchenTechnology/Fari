/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/17/2026 2:08:08 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using System.Collections;
using GamerFrameWork.UIFrameWork;

public class LoadingTextUI : WindowBase
{
	public LoadingTextUIComponent uiComponent;
	[SerializeField] private float typewriterInterval = 0.06f;
	[SerializeField] private float loopPauseSeconds = 0.45f;

	private Coroutine _typewriterCoroutine;
	private string _currentText;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<LoadingTextUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		if (!string.IsNullOrEmpty(_currentText))
			StartTypewriter(_currentText);
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		StopTypewriter();
		base.OnHide();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		StopTypewriter();
		base.OnDestroy();
	}
	#endregion

	#region API Function
	public void SetLoadingText(string loadingStr)
	{
		_currentText = loadingStr;
		StartTypewriter(_currentText);
	}

	public void SetReadingCardText(TarotCard card)
	{
		var cardName = card != null && !string.IsNullOrEmpty(card.nameZh)
			? card.nameZh
			: "今日牌";
		SetLoadingText($"正在解读{cardName}。。。");
	}

	private void StartTypewriter(string text)
	{
		StopTypewriter();
		if (uiComponent?.loadingText == null) return;

		if (string.IsNullOrEmpty(text))
		{
			uiComponent.loadingText.text = "";
			return;
		}

		_typewriterCoroutine = uiComponent.StartCoroutine(TypewriterLoop(text));
	}

	private void StopTypewriter()
	{
		if (_typewriterCoroutine != null)
		{
			uiComponent.StopCoroutine(_typewriterCoroutine);
			_typewriterCoroutine = null;
		}
	}

	private IEnumerator TypewriterLoop(string text)
	{
		while (true)
		{
			uiComponent.loadingText.text = "";
			for (int i = 0; i < text.Length; i++)
			{
				uiComponent.loadingText.text = text.Substring(0, i + 1);
				yield return new WaitForSeconds(typewriterInterval);
			}
			yield return new WaitForSeconds(loopPauseSeconds);
		}
	}
	#endregion

	#region UI组件事件
	#endregion
}
