using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TargetSphereGameController : MonoBehaviour
{
    public static TargetSphereGameController Instance { get; private set; }

    public enum OrbType
    {
        Normal,
        GoldBonus,
        DangerBomb
    }

    [Header("Game Stats")]
    public int score = 0;
    public int highScore = 0;
    public int combo = 0;
    public int targetsHit = 0;
    public float gameTime = 0f;

    [Header("Game Settings")]
    public float touchRadius = 1.35f;
    public int maxTargets = 4;

    private Transform handTransform;
    private readonly List<OrbInstance> activeOrbs = new List<OrbInstance>();
    private Canvas hudCanvas;
    private TMP_Text scoreText;
    private TMP_Text highScoreText;
    private TMP_Text timeText;
    private TMP_Text hitsText;
    private TMP_Text comboBanner;
    private TrailRenderer handTrail;
    private AudioSource audioSource;

    private readonly Color[] normalColors = new Color[]
    {
        new Color(0f, 0.95f, 1f),    // Cyber Cyan
        new Color(1f, 0.2f, 0.8f),   // Neon Pink
        new Color(0.2f, 1f, 0.5f),   // Emerald Green
        new Color(0.7f, 0.35f, 1f)   // Ultra Violet
    };

    private class OrbInstance
    {
        public GameObject gameObject;
        public OrbType type;
        public float baseY;
        public float phase;
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        highScore = PlayerPrefs.GetInt("HandTarget_HighScore", 0);
        FindHand();
        SetupTrail();
        SetupAudio();
        CreateHUD();

        for (int i = 0; i < maxTargets; i++)
        {
            SpawnOrb();
        }

        StartCoroutine(GameTimer());
    }

    void Update()
    {
        if (handTransform == null)
        {
            FindHand();
            if (handTransform != null) SetupTrail();
            return;
        }

        Vector3 handPos = handTransform.position;

        // Animate and check collisions
        for (int i = activeOrbs.Count - 1; i >= 0; i--)
        {
            OrbInstance orb = activeOrbs[i];
            if (orb == null || orb.gameObject == null)
            {
                activeOrbs.RemoveAt(i);
                continue;
            }

            // Animate floating
            float newY = orb.baseY + Mathf.Sin(Time.time * 2.5f + orb.phase) * 0.12f;
            Vector3 pos = orb.gameObject.transform.position;
            orb.gameObject.transform.position = new Vector3(pos.x, newY, pos.z);

            // Check touch distance
            if (Vector3.Distance(handPos, orb.gameObject.transform.position) < touchRadius)
            {
                HitOrb(orb, i);
            }
        }

        // Keep population filled
        if (activeOrbs.Count < maxTargets)
        {
            SpawnOrb();
        }

        UpdateHUD();
    }

    private void FindHand()
    {
        GameObject hand = GameObject.Find("Manager_Hand") ?? 
                          GameObject.Find("Points") ?? 
                          GameObject.Find("RightHand_CV");
        if (hand != null)
        {
            handTransform = hand.transform;
        }
    }

    private void SetupTrail()
    {
        if (handTransform == null || handTrail != null) return;

        handTrail = handTransform.GetComponent<TrailRenderer>();
        if (handTrail == null)
        {
            handTrail = handTransform.gameObject.AddComponent<TrailRenderer>();
        }

        handTrail.time = 0.28f;
        handTrail.startWidth = 0.45f;
        handTrail.endWidth = 0.02f;
        Material trailMat = new Material(Shader.Find("Sprites/Default"));
        trailMat.color = new Color(0f, 0.95f, 1f, 0.85f);
        handTrail.material = trailMat;
    }

    private void SetupAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.volume = 0.6f;
    }

    private void PlayBeep(float freq, float duration)
    {
        if (audioSource == null) return;
        int sampleRate = 44100;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = 1f - (float)i / sampleCount; // Fade out
            samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.4f;
        }

        AudioClip clip = AudioClip.Create("Tone", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        audioSource.PlayOneShot(clip);
    }

    private void SpawnOrb()
    {
        Vector3 basePos = handTransform != null ? handTransform.position : Vector3.zero;

        // Position spread
        float x = Random.Range(-1.6f, 1.6f);
        float y = Random.Range(-0.5f, 1.1f);
        float z = Random.Range(0.2f, 1.3f);
        Vector3 spawnPos = basePos + new Vector3(x, y, z);

        // Decide orb type: 70% Normal, 20% Gold Bonus, 10% Danger Bomb
        float roll = Random.value;
        OrbType type = OrbType.Normal;
        Color orbColor;
        float scale = 0.7f;

        if (roll < 0.20f)
        {
            type = OrbType.GoldBonus;
            orbColor = new Color(1f, 0.85f, 0.05f); // Glowing Gold
            scale = 0.65f;
        }
        else if (roll < 0.30f)
        {
            type = OrbType.DangerBomb;
            orbColor = new Color(1f, 0.15f, 0.15f); // Crimson Red Danger
            scale = 0.75f;
        }
        else
        {
            type = OrbType.Normal;
            orbColor = normalColors[Random.Range(0, normalColors.Length)];
            scale = 0.7f;
        }

        GameObject orbObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orbObj.name = "Orb_" + type;
        orbObj.transform.position = spawnPos;
        orbObj.transform.localScale = Vector3.one * scale;

        Renderer rend = orbObj.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = orbColor;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", orbColor * (type == OrbType.GoldBonus ? 2.4f : 1.8f));
            rend.material = mat;
        }

        OrbInstance instance = new OrbInstance
        {
            gameObject = orbObj,
            type = type,
            baseY = spawnPos.y,
            phase = Random.Range(0f, 6.28f)
        };

        activeOrbs.Add(instance);
    }

    private void HitOrb(OrbInstance orb, int index)
    {
        Vector3 hitPos = orb.gameObject.transform.position;
        OrbType type = orb.type;

        activeOrbs.RemoveAt(index);
        Destroy(orb.gameObject);

        if (type == OrbType.DangerBomb)
        {
            combo = 0;
            score = Mathf.Max(0, score - 150);
            PlayBeep(180f, 0.2f); // Low buzz
            StartCoroutine(SpawnFloatingText(hitPos, "DANGER! -150", Color.red));
            StartCoroutine(BurstEffect(hitPos, Color.red));
            if (comboBanner != null) StartCoroutine(ShowComboBanner("BOMB HIT! COMBO LOST!"));
        }
        else if (type == OrbType.GoldBonus)
        {
            combo += 2;
            targetsHit++;
            int pts = 300 + (combo * 50);
            score += pts;
            PlayBeep(880f, 0.2f); // High chime
            StartCoroutine(SpawnFloatingText(hitPos, "+" + pts + " GOLD!", new Color(1f, 0.85f, 0.1f)));
            StartCoroutine(BurstEffect(hitPos, new Color(1f, 0.85f, 0.1f)));
            if (comboBanner != null) StartCoroutine(ShowComboBanner("GOLDEN BONUS! +" + pts));
        }
        else
        {
            combo++;
            targetsHit++;
            int pts = 100 * Mathf.Min(combo, 5);
            score += pts;
            PlayBeep(523.25f, 0.12f); // C5 tone
            StartCoroutine(SpawnFloatingText(hitPos, "+" + pts, Color.cyan));
            StartCoroutine(BurstEffect(hitPos, Color.white));
            if (combo >= 3 && comboBanner != null)
            {
                StartCoroutine(ShowComboBanner("COMBO x" + combo + "!"));
            }
        }

        if (score > highScore)
        {
            highScore = score;
            PlayerPrefs.SetInt("HandTarget_HighScore", highScore);
        }
    }

    private IEnumerator SpawnFloatingText(Vector3 worldPos, string text, Color color)
    {
        GameObject textObj = new GameObject("FloatingScore");
        textObj.transform.position = worldPos;
        textObj.transform.localScale = Vector3.one * 0.8f;

        TextMeshPro tm = textObj.AddComponent<TextMeshPro>();
        tm.text = text;
        tm.fontSize = 4f;
        tm.alignment = TextAlignmentOptions.Center;
        tm.color = color;
        tm.fontStyle = FontStyles.Bold;

        Camera cam = Camera.main;
        if (cam != null)
        {
            textObj.transform.forward = cam.transform.forward;
        }

        float timer = 0f;
        float duration = 0.7f;
        Vector3 startPos = worldPos;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = timer / duration;
            textObj.transform.position = startPos + Vector3.up * (progress * 0.8f);
            tm.color = new Color(color.r, color.g, color.b, 1f - progress);
            yield return null;
        }

        Destroy(textObj);
    }

    private IEnumerator BurstEffect(Vector3 pos, Color burstColor)
    {
        GameObject burst = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        burst.name = "HitBurst";
        burst.transform.position = pos;
        Destroy(burst.GetComponent<Collider>());

        Renderer r = burst.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = burstColor;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", burstColor * 2.2f);
        r.material = mat;

        float t = 0f;
        Vector3 startScale = Vector3.one * 0.6f;
        Vector3 endScale = Vector3.one * 1.9f;

        while (t < 0.16f)
        {
            t += Time.deltaTime;
            burst.transform.localScale = Vector3.Lerp(startScale, endScale, t / 0.16f);
            yield return null;
        }

        Destroy(burst);
    }

    private IEnumerator ShowComboBanner(string msg)
    {
        comboBanner.text = msg;
        comboBanner.gameObject.SetActive(true);
        yield return new WaitForSeconds(0.6f);
        if (comboBanner != null) comboBanner.gameObject.SetActive(false);
    }

    private void CreateHUD()
    {
        if (hudCanvas != null) return;

        GameObject canvasObj = new GameObject("TargetHUD");
        hudCanvas = canvasObj.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        scoreText = MakeText(canvasObj.transform, "SCORE: 0", new Vector2(40f, -40f), TextAlignmentOptions.TopLeft, 36, new Color(0.1f, 0.95f, 1f));
        highScoreText = MakeText(canvasObj.transform, "HIGH: " + highScore, new Vector2(40f, -85f), TextAlignmentOptions.TopLeft, 24, new Color(0.6f, 0.75f, 0.85f));
        timeText = MakeText(canvasObj.transform, "TIME: 0s", new Vector2(0f, -40f), TextAlignmentOptions.Top, 36, new Color(1f, 0.85f, 0.1f));
        hitsText = MakeText(canvasObj.transform, "HITS: 0", new Vector2(-40f, -40f), TextAlignmentOptions.TopRight, 34, new Color(0.3f, 1f, 0.5f));

        comboBanner = MakeText(canvasObj.transform, "COMBO!", new Vector2(0f, 120f), TextAlignmentOptions.Center, 46, new Color(1f, 0.3f, 0.8f));
        comboBanner.gameObject.SetActive(false);
    }

    private TMP_Text MakeText(Transform parent, string txt, Vector2 pos, TextAlignmentOptions align, int size, Color col)
    {
        GameObject obj = new GameObject(txt);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        if (align == TextAlignmentOptions.TopLeft)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        }
        else if (align == TextAlignmentOptions.TopRight)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        }
        else if (align == TextAlignmentOptions.Top)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        }
        else
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        }

        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(650f, 60f);

        TMP_Text tm = obj.AddComponent<TextMeshProUGUI>();
        tm.text = txt;
        tm.fontSize = size;
        tm.color = col;
        tm.alignment = align;
        tm.fontStyle = FontStyles.Bold;

        return tm;
    }

    private void UpdateHUD()
    {
        if (scoreText != null) scoreText.text = "SCORE: " + score;
        if (highScoreText != null) highScoreText.text = "BEST: " + highScore;
        if (timeText != null) timeText.text = "TIME: " + Mathf.CeilToInt(gameTime) + "s";
        if (hitsText != null) hitsText.text = "HITS: " + targetsHit;
    }

    private IEnumerator GameTimer()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            gameTime++;
        }
    }
}
