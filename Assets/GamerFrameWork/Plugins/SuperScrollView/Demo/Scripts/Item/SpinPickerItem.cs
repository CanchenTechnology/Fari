using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SuperScrollView
{
    public class SpinPickerItem : MonoBehaviour
    {
        public TMP_Text mText;
        public int mValue;

        public void Init()
        {
        }

        public int Value
        {
            get
            {
                return mValue;
            }
            set
            {
                mValue = value;
            }
        }
    }
}
