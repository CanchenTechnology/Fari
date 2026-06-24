using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class FriendAvatarImageUtility
{
    public class PickedAvatar
    {
        public Sprite sprite;
        public string persistentPath;
    }

    private const int AvatarSize = 512;
    private const string AvatarFolderName = "FriendAvatars";
    private const int FallbackAvatarSize = 128;

    private static Sprite defaultAvatarSprite;
    private static Texture2D defaultAvatarTexture;
    private static readonly Dictionary<Image, string> avatarTargetTokens = new Dictionary<Image, string>();

    public static Sprite DefaultAvatarSprite
    {
        get
        {
            if (defaultAvatarSprite == null)
                defaultAvatarSprite = CreateDefaultAvatarSprite();
            return defaultAvatarSprite;
        }
    }

    public static void ApplyAvatar(Image target, Sprite sprite, Sprite localFallback = null)
    {
        if (target == null) return;

        Sprite resolved = ResolveAvatar(sprite, target, localFallback);
        target.sprite = resolved;
        target.enabled = resolved != null;
        target.preserveAspect = true;
    }

    public static Sprite ResolveAvatar(Sprite sprite, Image fallbackImage = null, Sprite localFallback = null)
    {
        if (sprite != null) return sprite;
        if (localFallback != null) return localFallback;
        if (fallbackImage != null && fallbackImage.sprite != null) return fallbackImage.sprite;
        return DefaultAvatarSprite;
    }

    public static Sprite ResolveFriendAvatar(FriendDataManager.FriendData friend, Image fallbackImage = null, Sprite localFallback = null)
    {
        if (friend == null)
            return ResolveAvatar(null, fallbackImage, localFallback);

        if (friend.headSprite != null)
            return friend.headSprite;

        if (!string.IsNullOrWhiteSpace(friend.avatarImagePath))
        {
            Sprite localSprite = LoadSpriteFromPath(friend.avatarImagePath);
            if (localSprite != null)
            {
                friend.headSprite = localSprite;
                return localSprite;
            }
        }

        return ResolveAvatar(null, fallbackImage, localFallback);
    }

    public static Sprite ResolveInviteAvatar(FriendDataManager.InviteData invite, Image fallbackImage = null, Sprite localFallback = null)
    {
        if (invite == null)
            return ResolveAvatar(null, fallbackImage, localFallback);

        if (invite.headSprite != null)
            return invite.headSprite;

        return ResolveAvatar(null, fallbackImage, localFallback);
    }

    public static void SetAvatarTargetToken(Image target, string token)
    {
        if (target == null) return;
        avatarTargetTokens[target] = token ?? string.Empty;
    }

    public static bool IsAvatarTargetTokenValid(Image target, string token)
    {
        if (target == null) return false;
        return avatarTargetTokens.TryGetValue(target, out string currentToken)
            && currentToken == (token ?? string.Empty);
    }

    public static void PickAvatar(Action<PickedAvatar> onSuccess, Action<string> onError)
    {
#if UNITY_EDITOR
        string path = EditorUtility.OpenFilePanel("选择好友头像", "", "png,jpg,jpeg");
        if (string.IsNullOrEmpty(path))
        {
            onError?.Invoke("已取消选择头像");
            return;
        }

        LoadAndPersistAvatar(path, onSuccess, onError);
#elif UNITY_ANDROID || UNITY_IOS
        NativeGallery.GetImageFromGallery(path =>
        {
            if (string.IsNullOrEmpty(path))
            {
                onError?.Invoke("已取消选择头像");
                return;
            }

            LoadAndPersistAvatar(path, onSuccess, onError);
        }, "选择好友头像", "image/*");
#else
        onError?.Invoke("当前平台不支持系统相册选择");
#endif
    }

    public static IEnumerator LoadCurrentUserAvatarCoroutine(Action<Sprite, string> onComplete)
    {
        if (TryLoadKnownAccountAvatarFromCache(out Sprite cachedSprite, out string cachedPath))
        {
            onComplete?.Invoke(cachedSprite, cachedPath);
            yield break;
        }

        UserDataManager userData = UserDataManager.Instance;
        string photoUrl = userData != null ? userData.PhotoUrl : string.Empty;
        LoginType loginType = userData != null ? userData.CurrentLoginType : LoginType.Email;
        Sprite loadedSprite = null;
        string cachePath = GetPreferredAccountCachePath(loginType);

        switch (loginType)
        {
            case LoginType.Apple:
                yield return AppleUserInfoHelper.LoadAndCacheAvatarCoroutine(photoUrl, sprite => loadedSprite = sprite);
                cachePath = AppleUserInfoHelper.LocalAvatarPath;
                break;
            case LoginType.Facebook:
                yield return FacebookUserInfoHelper.LoadAndCacheAvatarCoroutine(photoUrl, sprite => loadedSprite = sprite);
                cachePath = FacebookUserInfoHelper.LocalAvatarPath;
                break;
            default:
                yield return GoogleUserInfoHelper.LoadAndCacheAvatarCoroutine(photoUrl, sprite => loadedSprite = sprite);
                cachePath = GoogleUserInfoHelper.LocalAvatarPath;
                break;
        }

        if (loadedSprite != null)
        {
            onComplete?.Invoke(loadedSprite, File.Exists(cachePath) ? cachePath : string.Empty);
        }
        else
        {
            onComplete?.Invoke(null, string.Empty);
        }
    }

    public static Sprite LoadSpriteFromPath(string path)
    {
        if (!TryLoadSpriteFromPath(path, out Sprite sprite))
        {
            return null;
        }

        return sprite;
    }

    public static IEnumerator LoadSpriteFromUrlCoroutine(string url, Action<Sprite> onComplete)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            onComplete?.Invoke(null);
            yield break;
        }

        using UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("[FriendAvatarImageUtility] 下载好友头像失败: " + request.error);
            onComplete?.Invoke(null);
            yield break;
        }

        Texture2D texture = DownloadHandlerTexture.GetContent(request);
        onComplete?.Invoke(TextureToSprite(texture));
    }

    public static bool TryLoadSpriteFromPath(string path, out Sprite sprite)
    {
        sprite = null;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                UnityEngine.Object.Destroy(texture);
                return false;
            }

            sprite = TextureToSprite(texture);
            return sprite != null;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[FriendAvatarImageUtility] 加载好友头像失败: " + e.Message);
            return false;
        }
    }

    private static void LoadAndPersistAvatar(string sourcePath, Action<PickedAvatar> onSuccess, Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            onError?.Invoke("头像文件不存在");
            return;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(sourcePath);
            Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!source.LoadImage(bytes))
            {
                UnityEngine.Object.Destroy(source);
                onError?.Invoke("头像图片格式不支持");
                return;
            }

            Texture2D avatarTexture = CropAndResizeSquare(source, AvatarSize);
            UnityEngine.Object.Destroy(source);

            string persistentPath = SaveTextureToPersistent(avatarTexture);
            onSuccess?.Invoke(new PickedAvatar
            {
                sprite = TextureToSprite(avatarTexture),
                persistentPath = persistentPath,
            });
        }
        catch (Exception e)
        {
            onError?.Invoke("处理头像图片失败: " + e.Message);
        }
    }

    private static bool TryLoadKnownAccountAvatarFromCache(out Sprite sprite, out string path)
    {
        UserDataManager userData = UserDataManager.Instance;
        if (userData != null && TryLoadSpriteFromPath(GetPreferredAccountCachePath(userData.CurrentLoginType), out sprite))
        {
            path = GetPreferredAccountCachePath(userData.CurrentLoginType);
            return true;
        }

        string[] knownPaths =
        {
            GoogleUserInfoHelper.LocalAvatarPath,
            AppleUserInfoHelper.LocalAvatarPath,
            FacebookUserInfoHelper.LocalAvatarPath,
        };

        foreach (string knownPath in knownPaths)
        {
            if (TryLoadSpriteFromPath(knownPath, out sprite))
            {
                path = knownPath;
                return true;
            }
        }

        sprite = null;
        path = string.Empty;
        return false;
    }

    private static string GetPreferredAccountCachePath(LoginType loginType)
    {
        switch (loginType)
        {
            case LoginType.Apple:
                return AppleUserInfoHelper.LocalAvatarPath;
            case LoginType.Facebook:
                return FacebookUserInfoHelper.LocalAvatarPath;
            default:
                return GoogleUserInfoHelper.LocalAvatarPath;
        }
    }

    private static string SaveTextureToPersistent(Texture2D texture)
    {
        string folder = Path.Combine(Application.persistentDataPath, AvatarFolderName);
        Directory.CreateDirectory(folder);

        string path = Path.Combine(folder, "friend_avatar_" + Guid.NewGuid().ToString("N") + ".png");
        File.WriteAllBytes(path, texture.EncodeToPNG());
        return path;
    }

    private static Texture2D CropAndResizeSquare(Texture2D source, int size)
    {
        int cropSize = Mathf.Min(source.width, source.height);
        int offsetX = (source.width - cropSize) / 2;
        int offsetY = (source.height - cropSize) / 2;
        Texture2D output = new Texture2D(size, size, TextureFormat.RGBA32, false);

        for (int y = 0; y < size; y++)
        {
            float v = (y + 0.5f) / size;
            int sourceY = Mathf.Clamp(offsetY + Mathf.FloorToInt(v * cropSize), 0, source.height - 1);
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                int sourceX = Mathf.Clamp(offsetX + Mathf.FloorToInt(u * cropSize), 0, source.width - 1);
                output.SetPixel(x, y, source.GetPixel(sourceX, sourceY));
            }
        }

        output.Apply(false, false);
        return output;
    }

    private static Sprite TextureToSprite(Texture2D texture)
    {
        if (texture == null) return null;
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateDefaultAvatarSprite()
    {
        if (defaultAvatarTexture == null)
        {
            defaultAvatarTexture = new Texture2D(FallbackAvatarSize, FallbackAvatarSize, TextureFormat.RGBA32, false)
            {
                name = "GeneratedFriendDefaultAvatar",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color edge = new Color(0.12f, 0.09f, 0.16f, 0f);
            Color outer = new Color(0.23f, 0.13f, 0.27f, 1f);
            Color inner = new Color(0.86f, 0.52f, 0.20f, 1f);
            Color face = new Color(1f, 0.80f, 0.56f, 1f);

            for (int y = 0; y < FallbackAvatarSize; y++)
            {
                for (int x = 0; x < FallbackAvatarSize; x++)
                {
                    float u = (x + 0.5f) / FallbackAvatarSize * 2f - 1f;
                    float v = (y + 0.5f) / FallbackAvatarSize * 2f - 1f;
                    float radius = Mathf.Sqrt(u * u + v * v);

                    if (radius > 1f)
                    {
                        defaultAvatarTexture.SetPixel(x, y, edge);
                        continue;
                    }

                    Color pixel = Color.Lerp(inner, outer, Mathf.Clamp01(radius));

                    float head = Mathf.Sqrt(u * u + (v - 0.28f) * (v - 0.28f));
                    float shoulders = Mathf.Sqrt((u / 0.72f) * (u / 0.72f) + ((v + 0.48f) / 0.36f) * ((v + 0.48f) / 0.36f));
                    if (head < 0.28f || shoulders < 1f && v < -0.16f)
                        pixel = Color.Lerp(pixel, face, 0.82f);

                    defaultAvatarTexture.SetPixel(x, y, pixel);
                }
            }

            defaultAvatarTexture.Apply(false, false);
        }

        return TextureToSprite(defaultAvatarTexture);
    }
}
