using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//**********************************************
//创建人：玖一
//功能说明：挂在到开始场景
//**********************************************
namespace XFGameFrameWork.AudioLoader
{
    public static class AudioAPI
    {
        public static void PlaySFX(string path) => AudioMgr.Instance.PlaySFX(path);
        public static void PlayBGM(string path) => AudioMgr.Instance.PlayBGM(path);
        public static void PlayLoopSFX(string path)=>AudioMgr.Instance.PlayLoopSFX(path);
        public static void StopLoopSFX(string path)=>AudioMgr.Instance.StopLoopSFX(path);
        public static void SetVolume(AudioType type, float v) => AudioMgr.Instance.SetVolume(type, v);
    }
    public class AudioMgr : MonoBehaviour
    {
        public static AudioMgr Instance { get; private set; }
        public AudioSetting audioSetting;
        private AudioClipCache clipCache;
        private AudioSource bgmSource;
        private AudioSourcePool sfxPool;


        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            DontDestroyOnLoad(gameObject);
            Instance = this;

            sfxPool = new AudioSourcePool(transform, 3);
            clipCache = new AudioClipCache();

            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.volume = audioSetting.bgmVolume;
        }
        public void PlayBGM(string path)
        {
            clipCache.GetClipAsync(path, audioSetting.loadMode, clip => {
                if (clip == null) return;
                bgmSource.clip = clip;
                bgmSource.Play();
            });  
        }
        public void PlaySFX(string path)
        {
            clipCache.GetClipAsync(path, audioSetting.loadMode, clip => {
                if (clip == null) return;

                var src = sfxPool.Get();
                src.clip = clip;
                src.volume = audioSetting.bgmVolume;
                src.Play();
                StartCoroutine(RecycleAfter(src, clip.length));
            });   
        }
        private IEnumerator RecycleAfter(AudioSource source, float delay)
        {
            yield return new WaitForSeconds(delay);
            sfxPool.Recycle(source);
        }
        public void SetVolume(AudioType type, float volume)
        {
            if (type == AudioType.BGM)
            {
                bgmSource.volume = volume;
                audioSetting.bgmVolume = volume;
            }
            else
            {
                audioSetting.sfxVolume = volume;
            }

        }
        private Dictionary<string, AudioSource> loopSFXDict =new Dictionary<string, AudioSource>();

        public void PlayLoopSFX(string path)
        {
            if (loopSFXDict.ContainsKey(path)) return;
            clipCache.GetClipAsync(path, audioSetting.loadMode, clip =>
            {
                if (clip == null) return;

                if (!loopSFXDict.ContainsKey(path))
                {
                    var src = sfxPool.Get();
                    src.clip = clip;
                    src.loop = true;
                    src.volume = audioSetting.sfxVolume;
                    src.Play();
                    loopSFXDict[path] = src;
                }
            });
       
        }

        public void StopLoopSFX(string path)
        {
            if (!loopSFXDict.TryGetValue(path, out var src)) return;
            src.Stop();
            src.loop = false;
            Debug.Log(src.name);
            sfxPool.Recycle(src);
            loopSFXDict.Remove(path);
        }
    }
    public enum AudioType
    {
        BGM,
        SFX,
    }


}