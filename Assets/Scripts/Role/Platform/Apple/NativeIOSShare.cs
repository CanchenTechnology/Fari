using System.Runtime.InteropServices;
using UnityEngine;

public static class NativeIOSShare
{
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void _fariShareText(string text);
#endif

    public static void ShareText(string text)
    {
#if UNITY_IOS && !UNITY_EDITOR
        _fariShareText(text ?? string.Empty);
#else
        Debug.Log($"[NativeIOSShare] ShareText: {text}");
#endif
    }
}
