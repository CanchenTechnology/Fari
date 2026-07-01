using System;
using System.Collections.Generic;
using UnityEngine;

public static class DialogVoiceAudioUtility
{
    public static byte[] AudioClipToPcm16(AudioClip clip, int samplePosition, out float rms)
    {
        rms = 0f;
        if (clip == null || samplePosition <= 0) return null;

        int channels = Mathf.Max(1, clip.channels);
        int frameCount = Mathf.Min(samplePosition, clip.samples);
        float[] interleaved = new float[frameCount * channels];
        clip.GetData(interleaved, 0);

        if (channels == 1)
            return FloatSamplesToPcm16(interleaved, frameCount, out rms);

        float[] mono = new float[frameCount];
        for (int frame = 0; frame < frameCount; frame++)
        {
            float sum = 0f;
            int baseIndex = frame * channels;
            for (int channel = 0; channel < channels; channel++)
                sum += interleaved[baseIndex + channel];
            mono[frame] = sum / channels;
        }

        return FloatSamplesToPcm16(mono, frameCount, out rms);
    }

    public static byte[] FloatSamplesToPcm16(IReadOnlyList<float> samples, out float rms)
    {
        return FloatSamplesToPcm16(samples, samples != null ? samples.Count : 0, out rms);
    }

    public static byte[] FloatSamplesToPcm16(IReadOnlyList<float> samples, int sampleCount, out float rms)
    {
        rms = 0f;
        if (samples == null || sampleCount <= 0) return null;

        sampleCount = Mathf.Min(sampleCount, samples.Count);
        byte[] pcm = new byte[sampleCount * 2];
        double sumSquares = 0d;

        for (int i = 0; i < sampleCount; i++)
        {
            float sample = Mathf.Clamp(samples[i], -1f, 1f);
            sumSquares += sample * sample;

            int intSample = Mathf.RoundToInt(sample * short.MaxValue);
            if (intSample > short.MaxValue) intSample = short.MaxValue;
            if (intSample < short.MinValue) intSample = short.MinValue;

            pcm[i * 2] = (byte)(intSample & 0xff);
            pcm[i * 2 + 1] = (byte)((intSample >> 8) & 0xff);
        }

        rms = Mathf.Sqrt((float)(sumSquares / Math.Max(1, sampleCount)));
        return pcm;
    }

    public static float ComputeRms(IReadOnlyList<float> samples)
    {
        if (samples == null || samples.Count == 0) return 0f;

        double sumSquares = 0d;
        for (int i = 0; i < samples.Count; i++)
        {
            float sample = Mathf.Clamp(samples[i], -1f, 1f);
            sumSquares += sample * sample;
        }

        return Mathf.Sqrt((float)(sumSquares / Math.Max(1, samples.Count)));
    }

    public static string CleanTranscript(string text)
    {
        return string.IsNullOrWhiteSpace(text) ? "" : text.Trim();
    }
}
