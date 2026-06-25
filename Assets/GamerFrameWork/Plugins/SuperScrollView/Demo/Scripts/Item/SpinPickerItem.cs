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
        private float mPresetFontSize;
        private bool mHasPresetFontSize;

        public void Init()
        {
            CachePresetFontSize();
        }

        public void ApplyWheelVisual(float normalizedDistance, bool selected, Color reservedColor, Color selectedColor, float reservedFontScale, float minAlpha)
        {
            if (mText == null) return;

            float presetFontSize = GetPresetFontSize();
            float reservedFontSize = presetFontSize * Mathf.Clamp01(reservedFontScale);
            float weight = 1f - Mathf.Clamp01(normalizedDistance);
            Color textColor = selected ? selectedColor : reservedColor;
            textColor.a = selected ? 1f : Mathf.Lerp(minAlpha, 1f, weight);
            mText.color = textColor;
            mText.fontSize = Mathf.Lerp(reservedFontSize, presetFontSize, weight);
            mText.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
            mText.enableWordWrapping = false;
            mText.overflowMode = TextOverflowModes.Overflow;
        }

        private float GetPresetFontSize()
        {
            if (!mHasPresetFontSize)
            {
                CachePresetFontSize();
            }
            return mHasPresetFontSize ? mPresetFontSize : mText.fontSize;
        }

        private void CachePresetFontSize()
        {
            if (mText == null) return;
            mPresetFontSize = mText.fontSize;
            mHasPresetFontSize = true;
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
