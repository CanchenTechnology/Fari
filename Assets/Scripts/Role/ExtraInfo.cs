using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ExtraInfo : MonoBehaviour
{
    public TMP_Text roleName;
    public TMP_Text roleDes;
    public TMP_Text roleContent;

    public TMP_Text tag1Text, tag2Text, tag3Text;

    public void SetData(
        string roleNameStr,
        string roleDesStr,
        string roleContentStr,
        string tag1,
        string tag2,
        string tag3)
    {
        if (roleName != null)
            roleName.text = roleNameStr ?? "";

        if (roleDes != null)
            roleDes.text = roleDesStr ?? "";

        if (roleContent != null)
            roleContent.text = roleContentStr ?? "";

        if (tag1Text != null)
            tag1Text.text = tag1 ?? "";

        if (tag2Text != null)
            tag2Text.text = tag2 ?? "";

        if (tag3Text != null)
            tag3Text.text = tag3 ?? "";
    }
}