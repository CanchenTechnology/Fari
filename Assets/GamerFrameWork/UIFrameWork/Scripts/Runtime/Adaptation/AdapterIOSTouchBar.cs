using UnityEngine;
/*----------------------------------------------------------------------------
* Title: iOS触摸条适配组件
* Description: 直接挂载到窗口的UIContent节点上即可(iPhonex以上所有iOS设备均有触摸条，故直接生效所有iOS设备即可)
* GitHub：https://github.com/ZMteacher?tab=repositories
----------------------------------------------------------------------------*/
namespace GamerFrameWork.UIFrameWork
{
    public class AdapterIOSTouchBar : MonoBehaviour
    {
        /// <summary>
        /// 偏移量
        /// </summary>
        public float offsetValue = 0.01f;

        private void Start()
        {
#if UNITY_IOS
            GeneratorAdaptation();
#endif
        }


        public void GeneratorAdaptation()
        {
            RectTransform rectTrans = transform.GetComponent<RectTransform>();
            rectTrans.anchorMin = new Vector2(rectTrans.anchorMin.x,offsetValue);
        }
    }
}