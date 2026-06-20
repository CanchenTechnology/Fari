using System;
using UnityEngine;
using UnityEngine.UI;

public enum OracleForegroundEffectStyle
{
    Subtle,
    Dialog,
    DailyOracle
}

public class OracleForegroundEffects : MonoBehaviour
{
    private const string OverlayName = "OracleForegroundEffects";

    private static Sprite circleSprite;
    private static Sprite diamondSprite;
    private static Sprite flameSprite;

    private RectTransform rectTransform;
    private ParticleDot[] sparkles = Array.Empty<ParticleDot>();
    private ParticleDot[] smoke = Array.Empty<ParticleDot>();
    private Image leftGlow;
    private Image rightGlow;
    private Image leftFlame;
    private Image rightFlame;
    private float timeOffset;
    private OracleForegroundEffectStyle currentStyle;

    public static void Attach(Canvas canvas, OracleForegroundEffectStyle style)
    {
        if (canvas == null) return;

        Transform existing = canvas.transform.Find(OverlayName);
        OracleForegroundEffects effects;
        if (existing == null)
        {
            GameObject overlay = new GameObject(OverlayName, typeof(RectTransform), typeof(CanvasGroup), typeof(OracleForegroundEffects));
            overlay.transform.SetParent(canvas.transform, false);

            RectTransform rect = overlay.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            CanvasGroup group = overlay.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.ignoreParentGroups = true;

            effects = overlay.GetComponent<OracleForegroundEffects>();
        }
        else
        {
            effects = existing.GetComponent<OracleForegroundEffects>();
            if (effects == null)
                effects = existing.gameObject.AddComponent<OracleForegroundEffects>();
        }

        effects.transform.SetAsLastSibling();
        effects.Configure(style);
    }

    public static void Detach(Canvas canvas)
    {
        if (canvas == null) return;
        Transform existing = canvas.transform.Find(OverlayName);
        if (existing != null)
            Destroy(existing.gameObject);
    }

    private void Configure(OracleForegroundEffectStyle style)
    {
        currentStyle = style;
        rectTransform = transform as RectTransform;
        timeOffset = UnityEngine.Random.Range(0f, 100f);

        int sparkleCount = style == OracleForegroundEffectStyle.DailyOracle ? 30 : style == OracleForegroundEffectStyle.Dialog ? 20 : 12;
        int smokeCount = style == OracleForegroundEffectStyle.DailyOracle ? 8 : style == OracleForegroundEffectStyle.Dialog ? 10 : 5;

        ClearChildren();
        EnsureSprites();
        CreateGlows();
        CreateFlames();
        sparkles = CreateParticles("Sparkle", sparkleCount, false);
        smoke = CreateParticles("Smoke", smokeCount, true);
    }

    private void Update()
    {
        if (rectTransform == null) return;

        float dt = Time.unscaledDeltaTime;
        UpdateGlow(leftGlow, 0f);
        UpdateGlow(rightGlow, 1.7f);
        UpdateFlame(leftFlame, 0.2f);
        UpdateFlame(rightFlame, 1.4f);

        UpdateParticles(sparkles, dt);
        UpdateParticles(smoke, dt);
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
    }

    private void CreateGlows()
    {
        leftGlow = CreateImage("LeftCandleGlow", circleSprite, new Color(1f, 0.66f, 0.28f, 0.18f), transform);
        rightGlow = CreateImage("RightCandleGlow", circleSprite, new Color(0.82f, 0.45f, 1f, 0.12f), transform);

        ConfigureGlow(leftGlow.rectTransform, new Vector2(0f, 0f), new Vector2(120f, 70f), new Vector2(0f, 0f));
        ConfigureGlow(rightGlow.rectTransform, new Vector2(1f, 0f), new Vector2(150f, 90f), new Vector2(0f, 0f));
    }

    private void CreateFlames()
    {
        leftFlame = CreateImage("LeftCandleFlame", flameSprite, new Color(1f, 0.82f, 0.36f, 0.72f), transform);
        rightFlame = CreateImage("RightCandleFlame", flameSprite, new Color(0.92f, 0.62f, 1f, 0.55f), transform);

        ConfigureFlame(leftFlame.rectTransform, new Vector2(0f, 0f), new Vector2(34f, 54f), new Vector2(24f, 22f));
        ConfigureFlame(rightFlame.rectTransform, new Vector2(1f, 0f), new Vector2(38f, 60f), new Vector2(-34f, 24f));

        if (currentStyle == OracleForegroundEffectStyle.Subtle)
        {
            leftFlame.color = new Color(1f, 0.78f, 0.38f, 0.42f);
            rightFlame.color = new Color(0.88f, 0.56f, 1f, 0.34f);
        }
    }

    private void ConfigureFlame(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 offset)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = size;
        rect.anchoredPosition = offset;
    }

    private void ConfigureGlow(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 offset)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(anchor.x, 0f);
        rect.sizeDelta = size;
        rect.anchoredPosition = offset;
    }

    private ParticleDot[] CreateParticles(string prefix, int count, bool isSmoke)
    {
        ParticleDot[] result = new ParticleDot[count];
        for (int i = 0; i < count; i++)
        {
            Sprite sprite = isSmoke ? circleSprite : diamondSprite;
            Color color = isSmoke
                ? new Color(0.62f, 0.55f, 0.72f, 0.10f)
                : RandomSparkleColor();
            Image image = CreateImage($"{prefix}_{i:00}", sprite, color, transform);
            result[i] = new ParticleDot(image, isSmoke);
            ResetParticle(result[i], true);
        }
        return result;
    }

    private Image CreateImage(string name, Sprite sprite, Color color, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private void UpdateParticles(ParticleDot[] dots, float dt)
    {
        if (dots == null) return;
        foreach (ParticleDot dot in dots)
        {
            if (dot == null || dot.image == null) continue;

            dot.age += dt;
            if (dot.age >= dot.life)
            {
                ResetParticle(dot, false);
                continue;
            }

            float normalized = dot.age / dot.life;
            Vector2 drift = dot.velocity * dt;
            drift.x += Mathf.Sin((Time.unscaledTime + timeOffset + dot.phase) * dot.waveSpeed) * dot.waveAmount * dt;
            dot.rect.anchoredPosition += drift;

            float alpha = dot.isSmoke
                ? Mathf.Sin(normalized * Mathf.PI) * dot.maxAlpha
                : Mathf.SmoothStep(0f, dot.maxAlpha, Mathf.Sin(normalized * Mathf.PI));
            Color color = dot.baseColor;
            color.a = alpha;
            dot.image.color = color;

            float pulse = 1f + Mathf.Sin((Time.unscaledTime + dot.phase) * 4f) * 0.16f;
            dot.rect.localScale = Vector3.one * pulse;
        }
    }

    private void ResetParticle(ParticleDot dot, bool randomizeAge)
    {
        Vector2 size = GetCanvasSize();
        float sideBand = UnityEngine.Random.value < 0.5f ? 0.08f : 0.92f;
        float x = dot.isSmoke
            ? UnityEngine.Random.Range(size.x * 0.05f, size.x * 0.95f)
            : Mathf.Lerp(0f, size.x, UnityEngine.Random.value < 0.65f ? UnityEngine.Random.value : sideBand);
        float y = dot.isSmoke
            ? UnityEngine.Random.Range(-size.y * 0.08f, size.y * 0.25f)
            : UnityEngine.Random.Range(size.y * 0.05f, size.y * 0.95f);

        dot.rect.anchorMin = new Vector2(0f, 0f);
        dot.rect.anchorMax = new Vector2(0f, 0f);
        dot.rect.pivot = new Vector2(0.5f, 0.5f);
        dot.rect.anchoredPosition = new Vector2(x, y);

        float baseSize = dot.isSmoke
            ? UnityEngine.Random.Range(28f, 74f)
            : UnityEngine.Random.Range(5f, currentStyle == OracleForegroundEffectStyle.DailyOracle ? 14f : 11f);
        dot.rect.sizeDelta = new Vector2(baseSize, baseSize);

        dot.life = dot.isSmoke
            ? UnityEngine.Random.Range(7f, 14f)
            : UnityEngine.Random.Range(3.5f, 8.5f);
        dot.age = randomizeAge ? UnityEngine.Random.Range(0f, dot.life) : 0f;
        dot.velocity = dot.isSmoke
            ? new Vector2(UnityEngine.Random.Range(-7f, 8f), UnityEngine.Random.Range(8f, 24f))
            : new Vector2(UnityEngine.Random.Range(-10f, 10f), UnityEngine.Random.Range(3f, 18f));
        dot.phase = UnityEngine.Random.Range(0f, 10f);
        dot.waveSpeed = UnityEngine.Random.Range(0.6f, 1.8f);
        dot.waveAmount = dot.isSmoke ? UnityEngine.Random.Range(8f, 18f) : UnityEngine.Random.Range(3f, 9f);
        dot.maxAlpha = dot.isSmoke
            ? UnityEngine.Random.Range(0.035f, 0.09f)
            : UnityEngine.Random.Range(0.22f, 0.55f);
        dot.baseColor = dot.isSmoke ? new Color(0.62f, 0.55f, 0.72f, 1f) : RandomSparkleColor();
    }

    private void UpdateGlow(Image image, float phase)
    {
        if (image == null) return;

        float pulse = 1f + Mathf.Sin((Time.unscaledTime + phase) * 1.8f) * 0.055f;
        image.rectTransform.localScale = new Vector3(pulse, pulse, 1f);

        Color color = image.color;
        float baseAlpha = image == leftGlow ? 0.14f : 0.10f;
        color.a = baseAlpha + Mathf.Sin((Time.unscaledTime + phase) * 2.3f) * 0.035f;
        image.color = color;
    }

    private void UpdateFlame(Image image, float phase)
    {
        if (image == null) return;

        float wave = Mathf.Sin((Time.unscaledTime + timeOffset + phase) * 7.2f);
        float slow = Mathf.Sin((Time.unscaledTime + phase) * 2.1f);
        image.rectTransform.localScale = new Vector3(1f + wave * 0.08f, 1f + slow * 0.13f, 1f);
        image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, wave * 2.5f);

        Color color = image.color;
        float baseAlpha = image == leftFlame
            ? currentStyle == OracleForegroundEffectStyle.Subtle ? 0.42f : 0.72f
            : currentStyle == OracleForegroundEffectStyle.Subtle ? 0.34f : 0.55f;
        color.a = Mathf.Clamp01(baseAlpha + wave * 0.045f);
        image.color = color;
    }

    private Vector2 GetCanvasSize()
    {
        if (rectTransform == null)
            return new Vector2(Screen.width, Screen.height);

        Rect rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f)
            return new Vector2(Screen.width, Screen.height);

        return rect.size;
    }

    private Color RandomSparkleColor()
    {
        float roll = UnityEngine.Random.value;
        if (roll < 0.35f) return new Color(1f, 0.82f, 0.44f, 1f);
        if (roll < 0.70f) return new Color(0.76f, 0.55f, 1f, 1f);
        return new Color(1f, 0.96f, 0.78f, 1f);
    }

    private static void EnsureSprites()
    {
        if (circleSprite == null)
            circleSprite = CreateCircleSprite();
        if (diamondSprite == null)
            diamondSprite = CreateDiamondSprite();
        if (flameSprite == null)
            flameSprite = CreateFlameSprite();
    }

    private static Sprite CreateCircleSprite()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.48f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(1f - distance / radius);
                alpha = Mathf.SmoothStep(0f, 1f, alpha);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateFlameSprite()
    {
        const int width = 64;
        const int height = 96;
        Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((width - 1) * 0.5f, height * 0.24f);
        for (int y = 0; y < height; y++)
        {
            float normalizedY = y / (float)(height - 1);
            float taper = Mathf.Lerp(0.95f, 0.08f, normalizedY);
            float vertical = Mathf.Sin(normalizedY * Mathf.PI);
            for (int x = 0; x < width; x++)
            {
                float normalizedX = Mathf.Abs((x - center.x) / (width * 0.5f));
                float body = Mathf.Clamp01(1f - normalizedX / Mathf.Max(0.04f, taper));
                float alpha = Mathf.Pow(body, 1.8f) * Mathf.Pow(vertical, 0.7f);
                float heat = Mathf.Clamp01(1f - normalizedX * 0.8f - normalizedY * 0.2f);
                Color color = Color.Lerp(new Color(1f, 0.38f, 0.08f, alpha), new Color(1f, 0.96f, 0.58f, alpha), heat);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0f));
    }

    private static Sprite CreateDiamondSprite()
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.48f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Mathf.Abs(x - center.x) + Mathf.Abs(y - center.y);
                float alpha = Mathf.Clamp01(1f - distance / radius);
                alpha = Mathf.Pow(alpha, 0.7f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private class ParticleDot
    {
        public readonly Image image;
        public readonly RectTransform rect;
        public readonly bool isSmoke;
        public Vector2 velocity;
        public Color baseColor;
        public float age;
        public float life;
        public float maxAlpha;
        public float phase;
        public float waveSpeed;
        public float waveAmount;

        public ParticleDot(Image image, bool isSmoke)
        {
            this.image = image;
            this.rect = image.rectTransform;
            this.isSmoke = isSmoke;
        }
    }
}
