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

    [Header("Blade Settings")]
    public float sliceRadius = 3.2f;

    [Header("Spawn Settings")]
    public float minSpawnDelay = 0.9f;
    public float maxSpawnDelay = 1.7f;
    public float minUpForce = 11.0f;
    public float maxUpForce = 15.5f;

    private Transform handTransform;
    private Transform fingerTipTransform;
    private HandTracking handTracking;
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
    private List<Vector3> previousSlicePoints = new List<Vector3>();

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        highScore = PlayerPrefs.GetInt("FruitNinja_HighScore", 0);
        currentLives = maxLives;
        if (minUpForce < 14f) minUpForce = 15.0f;
        if (maxUpForce < 18f) maxUpForce = 21.0f;
        if (sliceRadius < 3.2f) sliceRadius = 3.6f;

        FindHandReferences();
        SetupBladeTrail();
        SetupAudio();
        CreateHUD();

        StartCoroutine(SpawnLoop());
    }

    void Update()
    {
        if (handTransform == null)
        {
            FindHandReferences();
            if (handTransform != null) SetupBladeTrail();
            return;
        }

        if (isGameOver)
        {
            if (Input.GetKeyDown(KeyCode.R) || Input.GetMouseButtonDown(0))
            {
                RestartGame();
            }
            return;
        }

        // Check slicing collisions against all hand points
        List<Vector3> slicePoints = GetHandSlicePoints();
        float sqrSliceRadius = sliceRadius * sliceRadius;

        for (int i = activeFruits.Count - 1; i >= 0; i--)
        {
            GameObject fruit = activeFruits[i];
            if (fruit == null)
            {
                activeFruits.RemoveAt(i);
                continue;
            }

            // If fruit fell below view
            if (fruit.transform.position.y < -1.5f)
            {
                activeFruits.RemoveAt(i);
                Destroy(fruit);
                continue;
            }

            // Check distance from any part of the hand or continuous sweep trajectory
            Vector3 fruitPos = fruit.transform.position;
            bool hit = false;

            for (int pIdx = 0; pIdx < slicePoints.Count; pIdx++)
            {
                Vector3 currentP = slicePoints[pIdx];

                // Direct proximity check
                if ((currentP - fruitPos).sqrMagnitude < sqrSliceRadius)
                {
                    hit = true;
                    break;
                }

                // Swept movement check from previous frame to catch fast slashes
                if (previousSlicePoints != null && pIdx < previousSlicePoints.Count)
                {
                    Vector3 prevP = previousSlicePoints[pIdx];
                    if (SqrDistancePointToSegment(prevP, currentP, fruitPos) < sqrSliceRadius)
                    {
                        hit = true;
                        break;
                    }
                }
            }

            if (hit)
            {
                SliceFruit(fruit, i);
            }
        }

        previousSlicePoints = new List<Vector3>(slicePoints);
        UpdateHUD();
    }

    private float SqrDistancePointToSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        Vector3 ap = p - a;
        float sqrLen = ab.sqrMagnitude;
        if (sqrLen < 0.0001f) return (p - a).sqrMagnitude;
        float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / sqrLen);
        Vector3 closest = a + t * ab;
        return (p - closest).sqrMagnitude;
    }


    // Show slice radius as a gizmo in the Scene view for debugging
    void OnDrawGizmosSelected()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = new Color(0f, 1f, 0.3f, 0.35f);
            List<Vector3> pts = GetHandSlicePoints();
            foreach (Vector3 p in pts)
            {
                Gizmos.DrawSphere(p, sliceRadius);
            }
            Gizmos.color = Color.yellow;
            foreach (GameObject f in activeFruits)
            {
                if (f != null) Gizmos.DrawWireSphere(f.transform.position, 0.5f);
            }
        }
    }

    private void FindHandReferences()
    {
        handTracking = FindFirstObjectByType<HandTracking>();

        // 1. Try HandCon (most common in this project)
        HandCon handCon = FindFirstObjectByType<HandCon>();
        if (handCon != null && handCon.bone != null)
        {
            handTransform = handCon.bone.transform;
            Debug.Log("[FruitNinja] Found hand via HandCon.bone: " + handCon.bone.name);
            return;
        }

        // 2. Try HandCon2
        HandCon2 handCon2 = FindFirstObjectByType<HandCon2>();
        if (handCon2 != null && handCon2.bone != null)
        {
            handTransform = handCon2.bone.transform;
            Debug.Log("[FruitNinja] Found hand via HandCon2.bone: " + handCon2.bone.name);
            return;
        }

        // 3. Try HandController
        HandController handCtrl = FindFirstObjectByType<HandController>();
        if (handCtrl != null)
        {
            if (handCtrl.hand != null) { handTransform = handCtrl.hand.transform; Debug.Log("[FruitNinja] Found hand via HandController.hand"); return; }
            if (handCtrl.armature != null) { handTransform = handCtrl.armature.transform; Debug.Log("[FruitNinja] Found hand via HandController.armature"); return; }
        }

        // 4. Try named GameObjects
        string[] names = new string[] { "Manager_Hand", "Points", "RightHand_CV", "Bone", "Armature", "Hand", "RightHand", "palm" };
        foreach (string n in names)
        {
            GameObject go = GameObject.Find(n);
            if (go != null)
            {
                handTransform = go.transform;
                Debug.Log("[FruitNinja] Found hand by name: " + n);
                return;
            }
        }

        // 5. Use HandTracking palm point (index 0) as the hand anchor
        if (handTracking != null && handTracking.handPoints != null && handTracking.handPoints.Length > 0 && handTracking.handPoints[0] != null)
        {
            handTransform = handTracking.handPoints[0].transform;
            Debug.Log("[FruitNinja] Found hand via HandTracking.handPoints[0]");
            return;
        }

        Debug.LogWarning("[FruitNinja] Could not find any hand reference!");
    }

    // MediaPipe landmark 8 is the index fingertip; use it so the blade trail hugs the fingertip
    private void FindFingerTipReference()
    {
        if (handTracking != null && handTracking.handPoints != null && handTracking.handPoints.Length > 8 && handTracking.handPoints[8] != null)
        {
            fingerTipTransform = handTracking.handPoints[8].transform;
        }
    }

    // MediaPipe fingertip landmarks: 4=thumb, 8=index, 12=middle, 16=ring, 20=pinky
    private static readonly int[] FingertipLandmarkIndices = { 4, 8, 12, 16, 20 };

    private List<Vector3> GetHandSlicePoints()
    {
        List<Vector3> points = new List<Vector3>();

        // handTransform (HandCon/HandCon2 bone) is the transform that actually drives the
        // visible hand model, so it must always be included or the blade never lines up with fruit.
        if (handTransform != null)
        {
            points.Add(handTransform.position);
        }

        // Fingertip landmarks add finer per-finger hits when they share the hand's coordinate space.
        if (handTracking != null && handTracking.handPoints != null)
        {
            foreach (int idx in FingertipLandmarkIndices)
            {
                if (idx < handTracking.handPoints.Length && handTracking.handPoints[idx] != null)
                    points.Add(handTracking.handPoints[idx].transform.position);
            }
        }

        return points;
    }

    private void SetupBladeTrail()
    {
        FindFingerTipReference();
        Transform trailAnchor = fingerTipTransform != null ? fingerTipTransform : handTransform;
        if (trailAnchor == null || bladeTrail != null) return;

        bladeTrail = trailAnchor.GetComponent<TrailRenderer>();
        if (bladeTrail == null)
        {
            bladeTrail = trailAnchor.gameObject.AddComponent<TrailRenderer>();
        }

        bladeTrail.time = 0.35f;
        bladeTrail.startWidth = 0.85f;
        bladeTrail.endWidth = 0.05f;

        Material trailMat = new Material(Shader.Find("Sprites/Default"));
        trailMat.color = new Color(0f, 0.95f, 1f, 0.95f);
        bladeTrail.material = trailMat;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0f, 0.95f, 1f), 0f), new GradientColorKey(new Color(1f, 0.2f, 0.85f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        bladeTrail.colorGradient = gradient;
    }

    private void SetupAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.volume = 0.7f;
    }

    private void PlaySound(string soundType)
    {
        if (audioSource == null) return;

        int sampleRate = 44100;
        float duration = 0.18f;
        float freq = 600f;

        if (soundType == "slash") { freq = 760f; duration = 0.12f; }
        else if (soundType == "bonus") { freq = 980f; duration = 0.25f; }
        else if (soundType == "bomb") { freq = 110f; duration = 0.45f; }

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
                int count = Random.Range(1, 4); // 1 to 3 fruits simultaneously
                for (int i = 0; i < count; i++)
                {
                    SpawnFruit();
                    yield return new WaitForSeconds(0.18f);
                }
            }

            yield return new WaitForSeconds(Random.Range(minSpawnDelay, maxSpawnDelay));
        }
    }

    private void SpawnFruit()
    {
        // 3 Zones across the whole room: 0 = Left, 1 = Center, 2 = Right
        int zone = Random.Range(0, 3);
        float startX = 0f;
        float sideForce = 0f;

        if (zone == 0)
        {
            // Left Zone
            startX = Random.Range(-3.5f, -1.5f);
            sideForce = Random.Range(0.8f, 2.2f); // Arc towards center-right
        }
        else if (zone == 1)
        {
            // Center Zone (over the coffee table)
            startX = Random.Range(-1.2f, 1.4f);
            sideForce = Random.Range(-0.8f, 0.8f);
        }
        else
        {
            // Right Zone
            startX = Random.Range(1.6f, 4.2f);
            sideForce = Random.Range(-2.2f, -0.8f); // Arc towards center-left
        }

        float handY = handTransform != null ? handTransform.position.y : 5.0f;
        float handZ = handTransform != null ? handTransform.position.z : 1.5f;

        // Ensure apex reaches the hand height and higher (eye & hand level)
        float targetApexY = Mathf.Max(handY + Random.Range(0.6f, 2.5f), 6.0f);
        float startY = Random.Range(-0.4f, 1.2f);
        float startZ = handZ + Random.Range(-0.2f, 0.4f);

        Vector3 spawnPos = new Vector3(startX, startY, startZ);

        FruitType type = (FruitType)Random.Range(0, 5);
        if (Random.value < 0.16f)
        {
            type = FruitType.Bomb;
        }

        GameObject fruitObj = CreateFruitMesh(type);
        fruitObj.name = "Fruit_" + type;
        fruitObj.transform.position = spawnPos;

        Rigidbody rb = fruitObj.AddComponent<Rigidbody>();
        rb.mass = 0.5f;
        rb.useGravity = true;

        // Physics apex velocity: v = sqrt(2 * g * deltaY)
        float gravity = Mathf.Abs(Physics.gravity.y);
        if (gravity < 0.1f) gravity = 9.81f;
        float deltaY = Mathf.Max(targetApexY - startY, 4.5f);
        float calculatedUpForce = Mathf.Sqrt(2f * gravity * deltaY) * Random.Range(1.05f, 1.25f);
        float upForce = Mathf.Max(calculatedUpForce, Random.Range(minUpForce, maxUpForce));

        rb.linearVelocity = new Vector3(sideForce, upForce, 0f);
        rb.angularVelocity = Random.insideUnitSphere * 4.5f;

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
                obj.transform.localScale = new Vector3(3.2f, 3.8f, 3.2f); // Huge Watermelon!
                SetMaterial(obj, new Color(0.12f, 0.78f, 0.25f), new Color(0.12f, 0.78f, 0.25f) * 0.7f);
                break;

            case FruitType.Apple:
                obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                obj.transform.localScale = Vector3.one * 2.8f; // Huge Apple!
                SetMaterial(obj, new Color(0.95f, 0.12f, 0.18f), new Color(0.95f, 0.12f, 0.18f) * 0.7f);
                break;

            case FruitType.Orange:
                obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                obj.transform.localScale = Vector3.one * 2.9f; // Huge Orange!
                SetMaterial(obj, new Color(1f, 0.58f, 0.05f), new Color(1f, 0.58f, 0.05f) * 0.7f);
                break;

            case FruitType.Banana:
                obj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                obj.transform.localScale = new Vector3(1.2f, 3.6f, 1.2f); // Huge Banana!
                SetMaterial(obj, new Color(1f, 0.92f, 0.1f), new Color(1f, 0.92f, 0.1f) * 0.7f);
                break;

            case FruitType.Pineapple:
                obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                obj.transform.localScale = new Vector3(2.3f, 3.2f, 2.3f); // Huge Golden Pineapple!
                SetMaterial(obj, new Color(1f, 0.82f, 0.0f), new Color(1f, 0.82f, 0.0f) * 2.0f);
                break;

            case FruitType.Bomb:
            default:
                obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                obj.transform.localScale = Vector3.one * 2.6f; // Huge Bomb!
                SetMaterial(obj, new Color(0.12f, 0.12f, 0.15f), new Color(1f, 0.1f, 0.1f) * 2.0f);
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
        SpawnHalfSlice(pos, color, Vector3.left * 3.2f + Vector3.up * 1.8f);
        SpawnHalfSlice(pos, color, Vector3.right * 3.2f + Vector3.up * 1.8f);
        StartCoroutine(SliceJuiceBurst(pos, color));
    }

    private void SpawnHalfSlice(Vector3 pos, Color color, Vector3 force)
    {
        GameObject half = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        half.name = "FruitHalf";
        half.transform.position = pos;
        half.transform.localScale = new Vector3(1.7f, 2.3f, 1.7f); // Big visible halves!

        SetMaterial(half, color, color * 0.4f);

        Rigidbody rb = half.AddComponent<Rigidbody>();
        rb.mass = 0.4f;
        rb.linearVelocity = force + Random.insideUnitSphere * 1.5f;
        rb.angularVelocity = Random.insideUnitSphere * 8f;

        Destroy(half, 2.0f);
    }

    private IEnumerator SliceJuiceBurst(Vector3 pos, Color color)
    {
        GameObject burst = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        burst.name = "JuiceBurst";
        burst.transform.position = pos;
        Destroy(burst.GetComponent<Collider>());

        SetMaterial(burst, color, color * 2.5f);

        float t = 0f;
        Vector3 startScale = Vector3.one * 1.2f;
        Vector3 endScale = Vector3.one * 3.8f;

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

        SetMaterial(flash, Color.red, Color.red * 3.5f);

        float t = 0f;
        Vector3 startScale = Vector3.one * 1.5f;
        Vector3 endScale = Vector3.one * 4.8f;

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
        textObj.transform.localScale = Vector3.one * 1.1f;

        TextMeshPro tm = textObj.AddComponent<TextMeshPro>();
        tm.text = text;
        tm.fontSize = 6.5f;
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
            textObj.transform.position = startPos + Vector3.up * (progress * 1.2f);
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

public class FruitData : MonoBehaviour
{
    public FruitNinjaGameController.FruitType type;
}
