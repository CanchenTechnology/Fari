using System.Collections;
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
            for (int i = 0; i < trans.childCount; i++)
            {
                GameObject obj = trans.GetChild(i).gameObject;
                string name = obj.name;
                if (name.Contains("#")) continue;
                // 判断是否是合法的标记节点（形如 [Type]FieldName）
                if (name.Contains("[") && name.Contains("]"))
                {
                    int index = name.IndexOf("]") + 1;
                    string fieldName = name.Substring(index, name.Length - index); // 字段名
                    string fieldType = name.Substring(1, index - 2);              // 字段类型

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
    }
}

