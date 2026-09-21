using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class FruitNinjaGameController : MonoBehaviour
{
    public static FruitNinjaGameController Instance { get; private set; }

    public enum FruitType
    {
        Watermelon,
        Apple,
        Orange,
        Banana,
        Pineapple,
        Bomb
    }

    [Header("Game Stats")]
    public int score = 0;
    public int highScore = 0;
    public int maxLives = 3;
    public int currentLives = 3;
    public int combo = 0;
    public int fruitsSliced = 0;
    public bool isGameOver = false;

    [Header("Blade & Camera Settings")]
    public float sliceRadius = 1.8f;
    public bool adjustCamera = true;

    [Header("Spawn Settings")]
    public float minSpawnDelay = 1.1f;
    public float maxSpawnDelay = 2.0f;

    private Transform handTransform;
    private Vector3 lastHandPos;
    private TrailRenderer bladeTrail;
    private AudioSource audioSource;
    private Canvas hudCanvas;
    private TMP_Text scoreText;
    private TMP_Text highScoreText;
    private TMP_Text livesText;
    private TMP_Text comboBanner;
    private GameObject gameOverPanel;
    private TMP_Text gameOverText;

    private readonly List<GameObject> activeFruits = new List<GameObject>();

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        highScore = PlayerPrefs.GetInt("FruitNinja_HighScore", 0);
        currentLives = maxLives;

        FindHand();
        SetupCameraAngle();
        SetupBladeTrail();
        SetupAudio();
        CreateHUD();

        StartCoroutine(SpawnLoop());
    }

    void Update()
    {
        if (handTransform == null)
        {
            FindHand();
            if (handTransform != null)
            {
                SetupBladeTrail();
                SetupCameraAngle();
            }
            return;
        }

        Vector3 handPos = handTransform.position;
        lastHandPos = handPos;

        if (isGameOver)
        {
            if (Input.GetKeyDown(KeyCode.R) || Input.GetMouseButtonDown(0))
            {
                RestartGame();
            }
            return;
        }

        // Check slicing collisions
        for (int i = activeFruits.Count - 1; i >= 0; i--)
        {
            GameObject fruit = activeFruits[i];
            if (fruit == null)
            {
                activeFruits.RemoveAt(i);
                continue;
            }

            // Fall below screen check
            if (fruit.transform.position.y < (handPos.y - 3.2f))
            {
                activeFruits.RemoveAt(i);
                Destroy(fruit);
                continue;
            }

            // Check distance to hand (Generous, juicy slice radius)
            if (Vector3.Distance(handPos, fruit.transform.position) < sliceRadius)
            {
                SliceFruit(fruit, i);
            }
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
            lastHandPos = hand.transform.position;
        }
    }

    private void SetupCameraAngle()
    {
        if (!adjustCamera) return;

        Camera cam = Camera.main;
        if (cam == null || handTransform == null) return;

        // Position camera directly facing the hand and fruit slicing zone with great depth
        Vector3 targetHandPos = handTransform.position;
        cam.transform.position = new Vector3(targetHandPos.x, targetHandPos.y + 0.3f, targetHandPos.z - 4.8f);
        cam.transform.LookAt(targetHandPos + Vector3.up * 0.2f);
        cam.fieldOfView = 54f;
    }

    private void SetupBladeTrail()
    {
        if (handTransform == null || bladeTrail != null) return;

        bladeTrail = handTransform.GetComponent<TrailRenderer>();
        if (bladeTrail == null)
        {
            bladeTrail = handTransform.gameObject.AddComponent<TrailRenderer>();
        }

        bladeTrail.time = 0.32f;
        bladeTrail.startWidth = 0.65f;
        bladeTrail.endWidth = 0.04f;

        Material trailMat = new Material(Shader.Find("Sprites/Default"));
        trailMat.color = new Color(0f, 0.95f, 1f, 0.95f);
        bladeTrail.material = trailMat;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0f, 0.95f, 1f), 0f), new GradientColorKey(new Color(1f, 0.25f, 0.85f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        bladeTrail.colorGradient = gradient;
    }

    private void SetupAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.volume = 0.65f;
    }

    private void PlaySound(string soundType)
    {
        if (audioSource == null) return;

        int sampleRate = 44100;
        float duration = 0.18f;
        float freq = 600f;

        if (soundType == "slash") { freq = 760f; duration = 0.12f; }
        else if (soundType == "bonus") { freq = 980f; duration = 0.25f; }
        else if (soundType == "bomb") { freq = 120f; duration = 0.45f; }

        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = 1f - (float)i / sampleCount;

            if (soundType == "bomb")
            {
                samples[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) + Random.Range(-0.5f, 0.5f)) * envelope * 0.5f;
            }
            else
            {
                float pitchMod = Mathf.Lerp(freq * 1.3f, freq * 0.8f, (float)i / sampleCount);
                samples[i] = Mathf.Sin(2f * Mathf.PI * pitchMod * t) * envelope * 0.45f;
            }
        }

        AudioClip clip = AudioClip.Create("Sound_" + soundType, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        audioSource.PlayOneShot(clip);
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            if (!isGameOver)
            {
                int count = Random.Range(1, 3); // 1 to 2 fruits for clear visibility
                for (int i = 0; i < count; i++)
                {
                    SpawnFruit();
                    yield return new WaitForSeconds(0.2f);
                }
            }

            yield return new WaitForSeconds(Random.Range(minSpawnDelay, maxSpawnDelay));
        }
    }

    private void SpawnFruit()
    {
        Vector3 basePos = handTransform != null ? handTransform.position : Vector3.zero;

        // Launch right in the prime center of the player's view
        float startX = basePos.x + Random.Range(-1.6f, 1.6f);
        float startY = basePos.y - 1.8f;
        float startZ = basePos.z + Random.Range(-0.1f, 0.5f);

        Vector3 spawnPos = new Vector3(startX, startY, startZ);

        // Decide type: 85% Fruit, 15% Bomb
        FruitType type = (FruitType)Random.Range(0, 5);
        if (Random.value < 0.15f)
        {
            type = FruitType.Bomb;
        }

        GameObject fruitObj = CreateFruitMesh(type);
        fruitObj.name = "Fruit_" + type;
        fruitObj.transform.position = spawnPos;

        Rigidbody rb = fruitObj.AddComponent<Rigidbody>();
        rb.mass = 0.8f;
        rb.useGravity = true;

        // Upward parabolic arc right across hand height
        float upForce = Random.Range(5.8f, 7.4f);
        float sideForce = (basePos.x - startX) * 0.9f + Random.Range(-0.4f, 0.4f);
        float forwardForce = Random.Range(-0.2f, 0.2f);

        rb.linearVelocity = new Vector3(sideForce, upForce, forwardForce);
        rb.angularVelocity = Random.insideUnitSphere * 4f;

        FruitData data = fruitObj.AddComponent<FruitData>();
        data.type = type;

        activeFruits.Add(fruitObj);
    }

    private GameObject CreateFruitMesh(FruitType type)
    {
        GameObject obj;

        switch (type)
        {
            case FruitType.Watermelon:
                obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                obj.transform.localScale = new Vector3(2.1f, 2.4f, 2.1f);
                SetMaterial(obj, new Color(0.12f, 0.72f, 0.25f), new Color(0.12f, 0.72f, 0.25f) * 0.6f);
                break;

            case FruitType.Apple:
                obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                obj.transform.localScale = Vector3.one * 1.8f;
                SetMaterial(obj, new Color(0.95f, 0.12f, 0.18f), new Color(0.95f, 0.12f, 0.18f) * 0.6f);
                break;

            case FruitType.Orange:
                obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                obj.transform.localScale = Vector3.one * 1.9f;
                SetMaterial(obj, new Color(1f, 0.58f, 0.05f), new Color(1f, 0.58f, 0.05f) * 0.6f);
                break;

            case FruitType.Banana:
                obj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                obj.transform.localScale = new Vector3(0.9f, 2.3f, 0.9f);
                SetMaterial(obj, new Color(1f, 0.9f, 0.1f), new Color(1f, 0.9f, 0.1f) * 0.6f);
                break;

            case FruitType.Pineapple:
                obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                obj.transform.localScale = new Vector3(1.5f, 2.0f, 1.5f);
                SetMaterial(obj, new Color(1f, 0.82f, 0.0f), new Color(1f, 0.82f, 0.0f) * 1.8f);
                break;

            case FruitType.Bomb:
            default:
                obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                obj.transform.localScale = Vector3.one * 1.7f;
                SetMaterial(obj, new Color(0.12f, 0.12f, 0.15f), new Color(1f, 0.1f, 0.1f) * 1.6f);
                break;
        }

        Collider col = obj.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        return obj;
    }

    private void SetMaterial(GameObject obj, Color color, Color emission)
    {
        Renderer r = obj.GetComponent<Renderer>();
        if (r != null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            r.material = mat;
        }
    }

    private void SliceFruit(GameObject fruit, int index)
    {
        FruitData data = fruit.GetComponent<FruitData>();
        FruitType type = data != null ? data.type : FruitType.Apple;
        Vector3 pos = fruit.transform.position;

        activeFruits.RemoveAt(index);
        Destroy(fruit);

        if (type == FruitType.Bomb)
        {
            currentLives--;
            combo = 0;
            PlaySound("bomb");
            StartCoroutine(BombFlashEffect(pos));
            StartCoroutine(SpawnFloatingText(pos, "BOOM! -1 LIFE", Color.red));

            if (currentLives <= 0)
            {
                TriggerGameOver();
            }
        }
        else
        {
            combo++;
            fruitsSliced++;
            int earned = 100 * Mathf.Min(combo, 8);
            if (type == FruitType.Pineapple) earned += 200;

            score += earned;
            PlaySound(type == FruitType.Pineapple ? "bonus" : "slash");

            Color sliceColor = GetFruitSliceColor(type);
            SpawnSlices(pos, sliceColor);
            StartCoroutine(SpawnFloatingText(pos, "+" + earned, sliceColor));

            if (combo >= 3 && comboBanner != null)
            {
                StartCoroutine(ShowComboBanner("COMBO x" + combo + "! +" + earned));
            }
        }

        if (score > highScore)
        {
            highScore = score;
            PlayerPrefs.SetInt("FruitNinja_HighScore", highScore);
        }
    }

    private Color GetFruitSliceColor(FruitType type)
    {
        switch (type)
        {
            case FruitType.Watermelon: return new Color(1f, 0.2f, 0.25f);
            case FruitType.Apple: return new Color(0.95f, 0.15f, 0.15f);
            case FruitType.Orange: return new Color(1f, 0.58f, 0.1f);
            case FruitType.Banana: return new Color(1f, 0.9f, 0.2f);
            case FruitType.Pineapple: return new Color(1f, 0.85f, 0.05f);
            default: return Color.white;
        }
    }

    private void SpawnSlices(Vector3 pos, Color color)
    {
        // Half 1 (Flies left)
        SpawnHalfSlice(pos, color, Vector3.left * 2.8f + Vector3.up * 1.5f);
        // Half 2 (Flies right)
        SpawnHalfSlice(pos, color, Vector3.right * 2.8f + Vector3.up * 1.5f);

        // Splat Burst
        StartCoroutine(SliceJuiceBurst(pos, color));
    }

    private void SpawnHalfSlice(Vector3 pos, Color color, Vector3 force)
    {
        GameObject half = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        half.name = "FruitHalf";
        half.transform.position = pos;
        half.transform.localScale = new Vector3(1.1f, 1.4f, 1.1f);

        SetMaterial(half, color, color * 0.4f);

        Rigidbody rb = half.AddComponent<Rigidbody>();
        rb.mass = 0.4f;
        rb.linearVelocity = force + Random.insideUnitSphere * 1.2f;
        rb.angularVelocity = Random.insideUnitSphere * 8f;

        Destroy(half, 2.0f);
    }

    private IEnumerator SliceJuiceBurst(Vector3 pos, Color color)
    {
        GameObject burst = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        burst.name = "JuiceBurst";
        burst.transform.position = pos;
        Destroy(burst.GetComponent<Collider>());

        SetMaterial(burst, color, color * 2.2f);

        float t = 0f;
        Vector3 startScale = Vector3.one * 0.8f;
        Vector3 endScale = Vector3.one * 2.8f;

        while (t < 0.16f)
        {
            t += Time.deltaTime;
            burst.transform.localScale = Vector3.Lerp(startScale, endScale, t / 0.16f);
            yield return null;
        }

        Destroy(burst);
    }

    private IEnumerator BombFlashEffect(Vector3 pos)
    {
        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.name = "BombFlash";
        flash.transform.position = pos;
        Destroy(flash.GetComponent<Collider>());

        SetMaterial(flash, Color.red, Color.red * 3.0f);

        float t = 0f;
        Vector3 startScale = Vector3.one * 1.2f;
        Vector3 endScale = Vector3.one * 3.8f;

        while (t < 0.25f)
        {
            t += Time.deltaTime;
            flash.transform.localScale = Vector3.Lerp(startScale, endScale, t / 0.25f);
            yield return null;
        }

        Destroy(flash);
    }

    private IEnumerator SpawnFloatingText(Vector3 worldPos, string text, Color color)
    {
        GameObject textObj = new GameObject("FloatingScore");
        textObj.transform.position = worldPos;
        textObj.transform.localScale = Vector3.one * 0.9f;

        TextMeshPro tm = textObj.AddComponent<TextMeshPro>();
        tm.text = text;
        tm.fontSize = 5.2f;
        tm.alignment = TextAlignmentOptions.Center;
        tm.color = color;
        tm.fontStyle = FontStyles.Bold;

        Camera cam = Camera.main;
        if (cam != null)
        {
            textObj.transform.forward = cam.transform.forward;
        }

        float timer = 0f;
        float duration = 0.65f;
        Vector3 startPos = worldPos;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = timer / duration;
            textObj.transform.position = startPos + Vector3.up * (progress * 1.1f);
            tm.color = new Color(color.r, color.g, color.b, 1f - progress);
            yield return null;
        }

        Destroy(textObj);
    }

    private IEnumerator ShowComboBanner(string msg)
    {
        if (comboBanner == null) yield break;
        comboBanner.text = msg;
        comboBanner.gameObject.SetActive(true);
        yield return new WaitForSeconds(0.6f);
        if (comboBanner != null) comboBanner.gameObject.SetActive(false);
    }

    private void TriggerGameOver()
    {
        isGameOver = true;
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            if (gameOverText != null)
            {
                gameOverText.text = "GAME OVER\n\nFINAL SCORE: " + score + "\nSLICED: " + fruitsSliced + "\n\n[ CLICK OR PRESS R TO PLAY AGAIN ]";
            }
        }
    }

    public void RestartGame()
    {
        score = 0;
        combo = 0;
        fruitsSliced = 0;
        currentLives = maxLives;
        isGameOver = false;

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        foreach (var f in activeFruits)
        {
            if (f != null) Destroy(f);
        }
        activeFruits.Clear();
    }

    private void CreateHUD()
    {
        if (hudCanvas != null) return;

        GameObject canvasObj = new GameObject("FruitNinjaHUD");
        hudCanvas = canvasObj.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        scoreText = MakeText(canvasObj.transform, "SCORE: 0", new Vector2(45f, -45f), TextAlignmentOptions.TopLeft, 40, new Color(0.1f, 0.95f, 1f));
        highScoreText = MakeText(canvasObj.transform, "BEST: " + highScore, new Vector2(45f, -95f), TextAlignmentOptions.TopLeft, 26, new Color(0.7f, 0.85f, 0.95f));
        livesText = MakeText(canvasObj.transform, "LIVES: [X] [X] [X]", new Vector2(-45f, -45f), TextAlignmentOptions.TopRight, 38, new Color(1f, 0.25f, 0.35f));

        comboBanner = MakeText(canvasObj.transform, "COMBO!", new Vector2(0f, 140f), TextAlignmentOptions.Center, 50, new Color(1f, 0.85f, 0.1f));
        comboBanner.gameObject.SetActive(false);

        // Game Over Panel
        CreateGameOverPanel(canvasObj.transform);
    }

    private void CreateGameOverPanel(Transform parent)
    {
        gameOverPanel = new GameObject("GameOverPanel");
        gameOverPanel.transform.SetParent(parent, false);

        RectTransform rect = gameOverPanel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;

        Image bg = gameOverPanel.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.05f, 0.08f, 0.88f);

        gameOverText = MakeText(gameOverPanel.transform, "GAME OVER", Vector2.zero, TextAlignmentOptions.Center, 44, Color.white);
        gameOverPanel.SetActive(false);
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
        else if (align == TextAlignmentOptions.Center)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        }
        else
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        }

        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(850f, 320f);

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

        if (livesText != null)
        {
            string hearts = "";
            for (int i = 0; i < currentLives; i++) hearts += " [X] ";
            livesText.text = "LIVES: " + (currentLives > 0 ? hearts : "NONE");
        }
    }
}
