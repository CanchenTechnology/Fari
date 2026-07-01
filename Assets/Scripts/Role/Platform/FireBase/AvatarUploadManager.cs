using System;
using System.Collections;
using System.IO;
using System.Text;
using Firebase.Auth;
using Firebase.Extensions;
using UnityEngine;
using UnityEngine.Networking;
using XFGameFrameWork;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 用户头像上传管理器。
/// 不依赖 Firebase.Storage.dll，直接用 Firebase Storage REST API 上传到 avatars/{uid}/avatar_512.jpg。
/// </summary>
public class AvatarUploadManager : MonoSingleton<AvatarUploadManager>
{
    public class AvatarUploadResult
    {
        public string photoUrl;
        public string storagePath;
        public Sprite previewSprite;
    }

    private const string StorageBucket = "fari-app-b2fd2.firebasestorage.app";
    private const int AvatarSize = 512;
    private const int MaxBytes = 2 * 1024 * 1024;

    public bool IsUploading { get; private set; }

    public void PickAndUploadAvatar(Action<AvatarUploadResult> onSuccess, Action<string> onError)
    {
#if UNITY_EDITOR
        string path = EditorUtility.OpenFilePanel("选择头像图片", "", "png,jpg,jpeg");
        if (string.IsNullOrEmpty(path))
        {
            onError?.Invoke("已取消选择头像");
            return;
        }

        UploadAvatarFromFile(path, onSuccess, onError);
#elif UNITY_ANDROID || UNITY_IOS
        NativeGallery.GetImageFromGallery(path =>
        {
            if (string.IsNullOrEmpty(path))
            {
                onError?.Invoke("已取消选择头像");
                return;
            }

            UploadAvatarFromFile(path, onSuccess, onError);
        }, "选择头像图片", "image/*");
#else
        onError?.Invoke("当前平台不支持系统相册选择");
#endif
    }

    public void UploadAvatarFromFile(string filePath, Action<AvatarUploadResult> onSuccess, Action<string> onError)
    {
        Texture2D source = LoadTextureFromFile(filePath, onError);
        if (source == null) return;

        UploadAvatarTexture(source, onSuccess, onError);
    }

    public void UploadAvatarTexture(Texture2D source, Action<AvatarUploadResult> onSuccess, Action<string> onError)
    {
        if (source == null)
        {
            onError?.Invoke("头像图片为空");
            return;
        }

        if (IsUploading)
        {
            onError?.Invoke("头像正在上传中");
            return;
        }

        StartCoroutine(UploadAvatarCoroutine(source, null, true, onSuccess, onError));
    }

    public void UploadVirtualFriendAvatarFromFile(string virtualFriendId, string filePath, Action<AvatarUploadResult> onSuccess, Action<string> onError)
    {
        Texture2D source = LoadTextureFromFile(filePath, onError);
        if (source == null) return;

        UploadVirtualFriendAvatarTexture(virtualFriendId, source, onSuccess, onError);
    }

    public void UploadVirtualFriendAvatarTexture(string virtualFriendId, Texture2D source, Action<AvatarUploadResult> onSuccess, Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(virtualFriendId))
        {
            onError?.Invoke("虚拟好友 ID 为空，无法上传头像");
            return;
        }

        if (source == null)
        {
            onError?.Invoke("头像图片为空");
            return;
        }

        if (IsUploading)
        {
            onError?.Invoke("头像正在上传中");
            return;
        }

        FirebaseUser user = FirebaseAuth.DefaultInstance?.CurrentUser;
        string uid = !string.IsNullOrWhiteSpace(user?.UserId) ? user.UserId : UserDataManager.Instance?.FirebaseUid;
        if (string.IsNullOrWhiteSpace(uid))
        {
            onError?.Invoke("用户未登录，无法上传好友头像");
            return;
        }

        string storagePath = $"avatars/{uid}/virtual_{SanitizeStorageName(virtualFriendId)}_{AvatarSize}.jpg";
        StartCoroutine(UploadAvatarCoroutine(source, storagePath, false, onSuccess, onError));
    }

    private IEnumerator UploadAvatarCoroutine(Texture2D source, string storagePathOverride, bool saveAsUserAvatar, Action<AvatarUploadResult> onSuccess, Action<string> onError)
    {
        IsUploading = true;

        Texture2D avatarTexture = null;
        try
        {
            avatarTexture = CropAndResizeSquare(source, AvatarSize);
        }
        catch (Exception e)
        {
            IsUploading = false;
            onError?.Invoke("处理头像图片失败: " + e.Message);
            yield break;
        }

        byte[] jpgBytes = EncodeJpgUnderLimit(avatarTexture);
        if (jpgBytes == null || jpgBytes.Length == 0 || jpgBytes.Length > MaxBytes)
        {
            IsUploading = false;
            onError?.Invoke("头像压缩后仍超过 2MB，请换一张图片");
            yield break;
        }

        FirebaseUser user = FirebaseAuth.DefaultInstance?.CurrentUser;
        if (user == null)
        {
            IsUploading = false;
            onError?.Invoke("用户未登录，无法上传头像");
            yield break;
        }

        string uid = user.UserId;
        string storagePath = string.IsNullOrWhiteSpace(storagePathOverride)
            ? $"avatars/{uid}/avatar_{AvatarSize}.jpg"
            : storagePathOverride;
        string downloadToken = null;

        string idToken = null;
        string tokenError = null;
        var tokenTask = user.TokenAsync(false);
        yield return new WaitUntil(() => tokenTask.IsCompleted);
        if (tokenTask.IsFaulted || tokenTask.IsCanceled)
            tokenError = tokenTask.Exception?.InnerException?.Message ?? "获取 Firebase Token 失败";
        else
            idToken = tokenTask.Result;

        if (string.IsNullOrEmpty(idToken))
        {
            IsUploading = false;
            onError?.Invoke(tokenError);
            yield break;
        }

        string uploadUrl = BuildUploadUrl(storagePath);
        using (UnityWebRequest request = new UnityWebRequest(uploadUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(jpgBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", "Firebase " + idToken);
            request.SetRequestHeader("Content-Type", "image/jpeg");
            request.SetRequestHeader("X-Firebase-Storage-Version", "unity-avatar-upload/1.0");

            yield return request.SendWebRequest();

            string response = request.downloadHandler == null ? "" : request.downloadHandler.text;
            if (request.result != UnityWebRequest.Result.Success)
            {
                IsUploading = false;
                onError?.Invoke($"头像上传失败: {request.responseCode} {request.error} {response}");
                yield break;
            }

            downloadToken = ExtractDownloadToken(response);
        }

        string photoUrl = BuildDownloadUrl(storagePath, downloadToken);
        if (saveAsUserAvatar)
        {
            UserDataManager.Instance.SetPhotoUrl(photoUrl);
            UserDataManager.Instance.SetAvatarStoragePath(storagePath);
            UserDataManager.Instance.SaveData();
            ClearAccountAvatarCaches();
            SaveAvatarCache(jpgBytes, photoUrl);

            if (FirebaseAuthManager.Instance != null)
            {
                FirebaseAuthManager.Instance.UpdateUserProfile(UserDataManager.Instance.UserName, photoUrl);
            }
        }

        bool firestoreDone = false;
        bool firestoreSuccess = !saveAsUserAvatar;
        if (saveAsUserAvatar && FirestoreManager.Instance != null && FirestoreManager.Instance.IsInitialized)
        {
            FirestoreManager.Instance.SaveUserData(success =>
            {
                firestoreSuccess = success;
                firestoreDone = true;
            });
        }
        else
        {
            firestoreDone = true;
        }

        yield return new WaitUntil(() => firestoreDone);

        IsUploading = false;
        if (!firestoreSuccess && FirestoreManager.Instance != null && FirestoreManager.Instance.IsInitialized)
        {
            onError?.Invoke("头像已上传，但用户资料同步到 Firestore 失败");
            yield break;
        }

        onSuccess?.Invoke(new AvatarUploadResult
        {
            photoUrl = photoUrl,
            storagePath = storagePath,
            previewSprite = TextureToSprite(avatarTexture),
        });
    }

    private static Texture2D LoadTextureFromFile(string filePath, Action<string> onError)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            onError?.Invoke("头像文件不存在");
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(filePath);
        }
        catch (Exception e)
        {
            onError?.Invoke("读取头像文件失败: " + e.Message);
            return null;
        }

        Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!source.LoadImage(bytes))
        {
            Destroy(source);
            onError?.Invoke("头像图片格式不支持");
            return null;
        }

        return source;
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

    private static byte[] EncodeJpgUnderLimit(Texture2D texture)
    {
        int[] qualities = { 86, 78, 70, 62, 54 };
        foreach (int quality in qualities)
        {
            byte[] bytes = texture.EncodeToJPG(quality);
            if (bytes.Length <= MaxBytes) return bytes;
        }

        return texture.EncodeToJPG(45);
    }

    private static string BuildDownloadUrl(string storagePath, string token)
    {
        string encodedPath = Uri.EscapeDataString(storagePath);
        return string.IsNullOrWhiteSpace(token)
            ? $"https://firebasestorage.googleapis.com/v0/b/{StorageBucket}/o/{encodedPath}?alt=media"
            : $"https://firebasestorage.googleapis.com/v0/b/{StorageBucket}/o/{encodedPath}?alt=media&token={token}";
    }

    private static string BuildUploadUrl(string storagePath)
    {
        string encodedPath = Uri.EscapeDataString(storagePath);
        return $"https://firebasestorage.googleapis.com/v0/b/{StorageBucket}/o/{encodedPath}";
    }

    private static string ExtractDownloadToken(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return string.Empty;

        string token = ExtractJsonString(response, "downloadTokens");
        if (!string.IsNullOrWhiteSpace(token))
            return token.Split(',')[0].Trim();

        token = ExtractJsonString(response, "firebaseStorageDownloadTokens");
        return string.IsNullOrWhiteSpace(token) ? string.Empty : token.Split(',')[0].Trim();
    }

    private static string ExtractJsonString(string json, string key)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            return string.Empty;

        string marker = "\"" + key + "\"";
        int keyIndex = json.IndexOf(marker, StringComparison.Ordinal);
        if (keyIndex < 0)
            return string.Empty;

        int colonIndex = json.IndexOf(':', keyIndex + marker.Length);
        if (colonIndex < 0)
            return string.Empty;

        int valueStart = json.IndexOf('"', colonIndex + 1);
        if (valueStart < 0)
            return string.Empty;

        StringBuilder builder = new StringBuilder();
        bool escaping = false;
        for (int i = valueStart + 1; i < json.Length; i++)
        {
            char c = json[i];
            if (escaping)
            {
                builder.Append(c);
                escaping = false;
                continue;
            }

            if (c == '\\')
            {
                escaping = true;
                continue;
            }

            if (c == '"')
                break;

            builder.Append(c);
        }

        return builder.ToString();
    }

    private static void SaveAvatarCache(byte[] jpgBytes, string photoUrl)
    {
        try
        {
            switch (UserDataManager.Instance != null ? UserDataManager.Instance.CurrentLoginType : LoginType.Email)
            {
                case LoginType.Apple:
                    AppleUserInfoHelper.SaveLocalAvatarCache(jpgBytes, photoUrl);
                    break;
                case LoginType.Facebook:
                    FacebookUserInfoHelper.SaveLocalAvatarCache(jpgBytes, photoUrl);
                    break;
                default:
                    GoogleUserInfoHelper.SaveLocalAvatarCache(jpgBytes, photoUrl);
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AvatarUploadManager] 本地头像缓存写入失败: " + e.Message);
        }
    }

    private static void ClearAccountAvatarCaches()
    {
        GoogleUserInfoHelper.ClearLocalAvatarCache();
        AppleUserInfoHelper.ClearLocalAvatarCache();
        FacebookUserInfoHelper.ClearLocalAvatarCache();
    }

    private static Sprite TextureToSprite(Texture2D texture)
    {
        if (texture == null) return null;
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }

    private static string SanitizeStorageName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "friend";
        StringBuilder builder = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-')
            {
                builder.Append(c);
            }
            else
            {
                builder.Append('_');
            }
        }

        return builder.Length == 0 ? "friend" : builder.ToString();
    }
}
