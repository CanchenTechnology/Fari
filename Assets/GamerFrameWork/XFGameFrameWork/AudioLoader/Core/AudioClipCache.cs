using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

//**********************************************
//创建人：玖一
//功能说明：获取音频缓存
//**********************************************
namespace XFGameFrameWork.AudioLoader
{
    public class AudioClipCache
    {
        private Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        public void GetClipAsync(string path, LoadMode loadMode, Action<AudioClip> onLoaded)
        {
            ResMgr.Init(loadMode);
            if (cache.TryGetValue(path, out var clip))
            {
                onLoaded?.Invoke(clip); // 缓存命中直接回调
                return;
            }

            // 异步加载
            ResMgr.LoadAsync<AudioClip>(path).OnCompleted(loadedClip =>
            {
                if (loadedClip != null)
                {
                    cache[path] = loadedClip;
                }
                onLoaded?.Invoke(loadedClip); // 加载完成后回调
            });
        }
    }

}


    