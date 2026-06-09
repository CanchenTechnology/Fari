using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//**********************************************
//创建人：玖一
//功能说明：
//**********************************************
namespace XFGameFrameWork.AudioLoader
{
    public class AudioSourcePool
    {
        private readonly Queue<AudioSource> pool = new Queue<AudioSource>();
        private readonly Transform parent;

        public AudioSourcePool(Transform parent, int preload = 3)
        {
            this.parent = parent;
            for (int i = 0; i < preload; i++)
            {
                CreateNewAudioSource();
            }

        }
        private AudioSource CreateNewAudioSource()
        {
            var go = new GameObject("AudioSource");
            go.transform.parent = parent;
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            pool.Enqueue(source);
            return source;
        }
        public AudioSource Get()
        {
            if (pool.Count == 0)
            {
                CreateNewAudioSource();
            }
            return pool.Dequeue();
        }
        public void Recycle(AudioSource source)
        {
            source.clip = null;
            source.Stop();
            pool.Enqueue(source);
        }

    }
}


