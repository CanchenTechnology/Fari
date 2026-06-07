using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI; // 引入 UI 命名空间

namespace Hamster.Menus
{
    // 强制依赖 Button 和 AudioSource 组件
    [RequireComponent(typeof(Button))]
    public class GUIButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        [Header("动画参数")]
        [Tooltip("悬停时呼吸缩放的幅度")]
        public float ButtonScaleRange = 0.15f;
        [Tooltip("悬停时呼吸的频率")]
        public float ButtonScaleFrequency = 6.0f;
        [Tooltip("按下时放大的比例 (正数放大，负数缩小)")]
        public float ButtonScalePressed = 0.2f; // 原代码是 0.5 (放大1.5倍)，对普通UI来说稍微有点夸张，这里微调了下
        [Tooltip("动画过渡速度")]
        public float transitionSpeed = 0.15f;

        // 内部状态
        private bool hover = false;
        private bool press = false;
        private float currentScale = 1.0f;
        private float hoverStartTime;
        private Vector3 startingScale;

        // 自动获取的组件引用
        private Button targetButton;

        private void Awake()
        {
            startingScale = transform.localScale;
            targetButton = GetComponent<Button>();
        }

        private void Update()
        {
            // 如果按钮被禁用（比如置灰），强制恢复原状并停止动画
            if (!targetButton.interactable)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, startingScale, transitionSpeed);
                hover = false;
                press = false;
                return;
            }

            // 计算目标缩放值
            float targetScale = 1.0f;
            if (press)
            {
                // 按下时的缩放大小
                targetScale = 1.0f + ButtonScalePressed;
            }
            else if (hover)
            {
                // 悬停时的呼吸动画（使用余弦波计算）
                targetScale = 1.0f + ButtonScaleRange + Mathf.Cos(
                    (hoverStartTime - Time.realtimeSinceStartup) * ButtonScaleFrequency) * ButtonScaleRange;
            }

            // 平滑插值当前缩放大小
            currentScale = currentScale * (1.0f - transitionSpeed) + targetScale * transitionSpeed;
            transform.localScale = startingScale * currentScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!targetButton.interactable) return;
            press = true;

            //// 播放音效最佳时机是在按下瞬间，而不是点击完成（因为更灵敏）
            //if (OnClicked != null)
            //{
            //    audioSource.PlayOneShot(OnClicked);
            //}
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // 修复原代码的Bug：抬起鼠标时不应该退出 Hover 状态
            press = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!targetButton.interactable) return;
            hoverStartTime = Time.realtimeSinceStartup;
            hover = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hover = false;
            press = false; // 如果鼠标移出了按钮区域，同时解除按下状态
        }

        // 当物体被隐藏时，防止缩放卡在奇怪的数值
        private void OnDisable()
        {
            hover = false;
            press = false;
            currentScale = 1.0f;
            transform.localScale = startingScale;
        }
    }
}