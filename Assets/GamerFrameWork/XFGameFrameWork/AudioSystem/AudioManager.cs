using UnityEngine;
using UnityEngine.Audio;
using System;
using System.Collections;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Audio Mixer")]
    public AudioMixer mixer;

    const string SAVE_KEY = "AudioSettings";

    //================================================
    #region 数据结构

    public enum AudioType
    {
        BGM,
        SFX,
        Voice
    }

    [Serializable]
    class AudioSettingsData
    {
        public bool BgmOn = true;
        public bool SfxOn = true;
        public bool VoiceOn = true;

        public float BgmVolume = 1f;
        public float SfxVolume = 1f;
        public float VoiceVolume = 1f;
    }

    AudioSettingsData settings = new AudioSettingsData();

    #endregion

    //================================================
    #region 事件（UI监听）

    public static event Action<float> OnBgmVolumeChanged;
    public static event Action<float> OnSfxVolumeChanged;
    public static event Action<float> OnVoiceVolumeChanged;

    public static event Action<bool> OnBgmSwitchChanged;
    public static event Action<bool> OnSfxSwitchChanged;
    public static event Action<bool> OnVoiceSwitchChanged;

    #endregion

    //================================================
    #region 播放器

    AudioSource bgmSource;
    AudioSource voiceSource;
    List<AudioSource> sfxSources = new();

    Queue<AudioClip> voiceQueue = new();

    const int SFX_POOL_SIZE = 10;

    #endregion

    //================================================
    #region 初始化

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSettings();
        CreateSources();
        ApplyAllSettings();
    }

    void CreateSources()
    {
        bgmSource = CreateSource("BGM_Source");
        bgmSource.loop = true;

        voiceSource = CreateSource("Voice_Source");

        for (int i = 0; i < SFX_POOL_SIZE; i++)
            sfxSources.Add(CreateSource("SFX_" + i));
    }

    AudioSource CreateSource(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform);

        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;

        return source;
    }

    #endregion

    //================================================
    #region BGM

    public void PlayBGM(AudioClip clip, float fadeTime = 1f)
    {
        if (!settings.BgmOn || clip == null) return;

        StopAllCoroutines();
        StartCoroutine(FadeBGM(clip, fadeTime));
    }

    IEnumerator FadeBGM(AudioClip newClip, float time)
    {
        if (bgmSource.isPlaying)
        {
            for (float t = 0; t < time; t += Time.deltaTime)
            {
                bgmSource.volume = 1 - t / time;
                yield return null;
            }
        }

        bgmSource.clip = newClip;
        bgmSource.Play();

        for (float t = 0; t < time; t += Time.deltaTime)
        {
            bgmSource.volume = t / time;
            yield return null;
        }
    }

    public void StopBGM()
    {
        bgmSource.Stop();
    }

    #endregion

    //================================================
    #region SFX

    public void PlaySFX(AudioClip clip)
    {
        if (!settings.SfxOn || clip == null) return;

        AudioSource source = GetFreeSfxSource();
        source.PlayOneShot(clip);
    }

    AudioSource GetFreeSfxSource()
    {
        foreach (var s in sfxSources)
            if (!s.isPlaying)
                return s;

        return sfxSources[0];
    }

    #endregion

    //================================================
    #region Voice

    public void PlayVoice(AudioClip clip, bool interrupt = true)
    {
        if (!settings.VoiceOn || clip == null) return;

        if (interrupt)
        {
            voiceSource.Stop();
            voiceQueue.Clear();
        }

        voiceSource.clip = clip;
        voiceSource.Play();
    }

    public void QueueVoice(AudioClip clip)
    {
        if (!settings.VoiceOn || clip == null) return;

        voiceQueue.Enqueue(clip);

        if (!voiceSource.isPlaying)
            StartCoroutine(PlayVoiceQueue());
    }

    IEnumerator PlayVoiceQueue()
    {
        while (voiceQueue.Count > 0)
        {
            var clip = voiceQueue.Dequeue();

            voiceSource.clip = clip;
            voiceSource.Play();

            yield return new WaitForSeconds(clip.length);
        }
    }

    public void StopVoice()
    {
        voiceSource.Stop();
        voiceQueue.Clear();
    }

    /// <summary>
    /// 检查 VoiceSource 是否正在播放
    /// </summary>
    public bool IsVoicePlaying()
    {
        return voiceSource != null && voiceSource.isPlaying;
    }

    #endregion

    //================================================
    #region 音量控制（Mixer）

    public void SetVolume(AudioType type, float value)
    {
        float db = Mathf.Log10(Mathf.Clamp(value, 0.001f, 1)) * 20;

        switch (type)
        {
            case AudioType.BGM:
                settings.BgmVolume = value;
                mixer.SetFloat("BGMVolume", db);
                OnBgmVolumeChanged?.Invoke(value);
                break;

            case AudioType.SFX:
                settings.SfxVolume = value;
                mixer.SetFloat("SFXVolume", db);
                OnSfxVolumeChanged?.Invoke(value);
                break;

            case AudioType.Voice:
                settings.VoiceVolume = value;
                mixer.SetFloat("VoiceVolume", db);
                OnVoiceVolumeChanged?.Invoke(value);
                break;
        }

        SaveSettings();
    }

    #endregion

    //================================================
    #region 开关控制

    public void SetSwitch(AudioType type, bool on)
    {
        switch (type)
        {
            case AudioType.BGM:
                settings.BgmOn = on;
                if (!on) StopBGM();
                OnBgmSwitchChanged?.Invoke(on);
                break;

            case AudioType.SFX:
                settings.SfxOn = on;
                OnSfxSwitchChanged?.Invoke(on);
                break;

            case AudioType.Voice:
                settings.VoiceOn = on;
                if (!on) StopVoice();
                OnVoiceSwitchChanged?.Invoke(on);
                break;
        }

        SaveSettings();
    }

    #endregion

    //================================================
    #region 保存

    void SaveSettings()
    {
        PlayerPrefs.SetString(SAVE_KEY,
            JsonUtility.ToJson(settings));
        PlayerPrefs.Save();
    }

    void LoadSettings()
    {
        if (!PlayerPrefs.HasKey(SAVE_KEY)) return;

        settings = JsonUtility.FromJson<AudioSettingsData>(
            PlayerPrefs.GetString(SAVE_KEY));
    }

    void ApplyAllSettings()
    {
        SetVolume(AudioType.BGM, settings.BgmVolume);
        SetVolume(AudioType.SFX, settings.SfxVolume);
        SetVolume(AudioType.Voice, settings.VoiceVolume);
    }

    #endregion
}