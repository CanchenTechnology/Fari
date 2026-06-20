using System.Collections.Generic;
using UnityEngine;
namespace GamerFrameWork.UIFrameWork
{
    public class AnalysisComponentDataTool
    {
        /// <summary>
        /// 解析窗口数据
        /// </summary>
        /// <param name="objDataList"></param>
        /// <param name="trans"></param>
        /// <param name="winName"></param>
        public static void AnalysisWindowNodeData(ref List<EditorObjectData> objDataList, Transform trans, string winName)
        {
            if (objDataList == null)
            {
                objDataList = new List<EditorObjectData>();
            }

            if (trans == null)
            {
                return;
            }

            for (int i = 0; i < trans.childCount; i++)
            {
                GameObject obj = trans.GetChild(i).gameObject;
                string name = obj.name;
                if (name.Contains("#")) continue;
                // 判断是否是合法的标记节点（形如 [Type]FieldName）
                if (TryParseMarkedName(name, out string fieldType, out string fieldName))
                {
                    var objectData = new EditorObjectData { fieldName = fieldName, fieldType = fieldType, insID = obj.GetInstanceID() };
                    objDataList.Add(objectData);
                    //处理列表元素绑定
                    if (fieldType.Contains(","))
                    {
                        objectData.dataList = new List<EditorObjectData>();
                        objectData.fieldType = objectData.fieldType.Replace(",", "");
                        for (int j = 0; j < obj.transform.childCount; j++)
                        {
                            GameObject listObjItme = obj.transform.GetChild(j).gameObject;
                            objectData.dataList.Add(new EditorObjectData { fieldName = listObjItme.name.Replace("#", ""), insID = listObjItme.GetInstanceID() });
                        }
                    }
                }

                // 递归子节点
                AnalysisWindowNodeData(ref objDataList,trans.GetChild(i), winName);
            }
        }

        private static bool TryParseMarkedName(string name, out string fieldType, out string fieldName)
        {
            fieldType = null;
            fieldName = null;

            if (string.IsNullOrEmpty(name) || name[0] != '[')
            {
                return false;
            }

            int endIndex = name.IndexOf(']');
            if (endIndex <= 1 || endIndex >= name.Length - 1)
            {
                Debug.LogWarning($"UI字段标记格式无效，已跳过：{name}。正确格式为 [Type]FieldName。");
                return false;
            }

            fieldType = name.Substring(1, endIndex - 1).Trim();
            fieldName = name.Substring(endIndex + 1).Trim();

            if (string.IsNullOrEmpty(fieldType) || string.IsNullOrEmpty(fieldName))
            {
                Debug.LogWarning($"UI字段标记缺少类型或字段名，已跳过：{name}。");
                return false;
            }

            return true;
        }
    }
}
