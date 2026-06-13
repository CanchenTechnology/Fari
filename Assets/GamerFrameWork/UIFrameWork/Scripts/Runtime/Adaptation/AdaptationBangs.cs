using UnityEngine;

namespace GamerFrameWork.UIFrameWork
{
    [RequireComponent(typeof(RectTransform))]
    public class AdaptationBangs : MonoBehaviour
    {
        private RectTransform m_RectTransform;
        // 记录上一次的屏幕尺寸和安全区，避免不必要的重复计算
        private Rect m_LastSafeArea = new Rect(0, 0, 0, 0);

        private void Awake()
        {
            m_RectTransform = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        // 也可以放在 Update 或 OnRectTransformDimensionsChange 中，以便支持游戏运行时的横竖屏旋转切换
        private void Update()
        {
            if (m_LastSafeArea != Screen.safeArea)
            {
                ApplySafeArea();
            }
        }

        public void ApplySafeArea()
        {
            Rect safeArea = Screen.safeArea;
            m_LastSafeArea = safeArea;

            // 获取屏幕的实际分辨率
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            // 将 SafeArea 的像素坐标转换为 0~1 的归一化锚点坐标
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= screenWidth;
            anchorMin.y /= screenHeight;
            anchorMax.x /= screenWidth;
            anchorMax.y /= screenHeight;

            // 应用到 RectTransform
            m_RectTransform.anchorMin = anchorMin;
            m_RectTransform.anchorMax = anchorMax;
        }
    }
}