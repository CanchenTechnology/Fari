using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//**********************************************
//创建人：玖一
//功能说明：
//**********************************************
namespace XFGameFrameWork.AudioLoader
{
    [CreateAssetMenu(fileName = "AudioSetting", menuName = "Audio/AudioSetting")]
    public class AudioSetting : ScriptableObject
    {
        [Range(0f, 1f)] public float bgmVolume = 1.0f;
        [Range(0f, 1f)] public float sfxVolume = 1.0f;
        public LoadMode loadMode = LoadMode.Resources;

    }
}


