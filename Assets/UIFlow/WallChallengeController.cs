using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class WallChallengeController : MonoBehaviour
{
    private static WallChallengeController instance;

    [SerializeField] private string playerId = "player-1";
    [SerializeField] private float spawnZ = 12f;
    [SerializeField] private float passZ = 0f;
    [SerializeField] private float despawnZ = -3f;
    [SerializeField] private float wallSpeed = 3.4f;
    [SerializeField] private float roundDelay = 1.2f;
    [SerializeField] private int maxLives = 3;
    [SerializeField] private bool allowKeyboardFallback = true;
    [SerializeField] private float keyboardGestureHoldSeconds = 0.85f;

    private readonly string[] targetGestures = { "open", "fist", "swipe_left", "swipe_right", "swipe_up" };

    private GestureUdpReceiver receiver;
    private UDPReceive legacyReceiver;
    private Canvas hudCanvas;
    private TMP_Text scoreText;
    private TMP_Text livesText;
    private TMP_Text targetText;
    private TMP_Text statusText;
    private TMP_Text currentGestureText;
    private Transform wallRoot;
    private string currentTarget = "open";
    private string keyboardGesture = "none";
    private float keyboardGestureUntil;
    private bool roundActive;
    private bool challengeStarted;
    private int score;
    private int lives;

    // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    // private static void CreateBootstrap()
    // {
    //     if (instance != null)
    //     {
    //         return;
    //     }
    // 
    //     GameObject bootstrap = new GameObject("Wall Challenge Controller");
    //     instance = bootstrap.AddComponent<WallChallengeController>();
    //     DontDestroyOnLoad(bootstrap);
    // }

    private void OnEnable()
    {
        RuntimeStartScreen.GameStarted += StartChallenge;
    }

    private void OnDisable()
    {
        RuntimeStartScreen.GameStarted -= StartChallenge;
    }

    private void StartChallenge()
    {
        if (challengeStarted)
        {
            return;
        }

        challengeStarted = true;
        lives = maxLives;
        score = 0;

        EnsureReceiver();
        EnsureView();
        BuildHud();
        CreateFloorGuide();
        RefreshHud("Prepare a pose");
        StartCoroutine(RunChallengeLoop());
    }

    private void EnsureReceiver()
    {
        receiver = FindFirstObjectByType<GestureUdpReceiver>();
        legacyReceiver = FindFirstObjectByType<UDPReceive>();

        if (receiver != null || legacyReceiver != null)
        {
            return;
        }

        GameObject receiverObject = new GameObject("UDPReceive");
        legacyReceiver = receiverObject.AddComponent<UDPReceive>();
    }

    private void EnsureView()
    {
        if (Camera.main == null)
        {
            GameObject cameraObject = new GameObject("Wall Challenge Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 2.2f, -8f);
            camera.transform.LookAt(new Vector3(0f, 2.1f, 4f));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.04f, 0.055f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;
        }

        if (FindFirstObjectByType<Light>() == null)
        {
            GameObject lightObject = new GameObject("Wall Challenge Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }
    }

    private IEnumerator RunChallengeLoop()
    {
        while (lives > 0)
        {
            currentTarget = targetGestures[Random.Range(0, targetGestures.Length)];
            RefreshHud("Faca o gesto certo");

            yield return new WaitForSeconds(roundDelay);
            yield return SpawnAndResolveWall(currentTarget);
        }

        RefreshHud("Fim de jogo - pontuacao: " + score);
    }

    private IEnumerator SpawnAndResolveWall(string targetGesture)
    {
        roundActive = true;
        wallRoot = CreateWall(targetGesture);
        bool resolved = false;

        while (wallRoot != null && wallRoot.position.z > despawnZ)
        {
            wallRoot.position += Vector3.back * wallSpeed * Time.deltaTime;
            UpdateCurrentGestureText();

            if (!resolved && wallRoot.position.z <= passZ)
            {
                resolved = true;
                ResolveWall(targetGesture);
            }

            yield return null;
        }

        if (wallRoot != null)
        {
            Destroy(wallRoot.gameObject);
            wallRoot = null;
        }

        roundActive = false;
    }

    private Transform CreateWall(string targetGesture)
    {
        GameObject root = new GameObject("Gesture Wall - " + targetGesture);
        root.transform.position = new Vector3(0f, 2.2f, spawnZ);

        Vector2 holeCenter = GetHoleCenter(targetGesture);
        Vector2 holeSize = GetHoleSize(targetGesture);
        Color wallColor = new Color(0.0f, 0.7f, 0.82f, 0.92f);
        Color trimColor = new Color(1f, 0.9f, 0.2f, 1f);

        const float wallWidth = 7.5f;
        const float wallHeight = 4.8f;
        const float thickness = 0.25f;

        float leftWidth = Mathf.Max(0.1f, holeCenter.x + wallWidth * 0.5f - holeSize.x * 0.5f);
        float rightWidth = Mathf.Max(0.1f, wallWidth * 0.5f - holeCenter.x - holeSize.x * 0.5f);
        float topHeight = Mathf.Max(0.1f, wallHeight * 0.5f - holeCenter.y - holeSize.y * 0.5f);
        float bottomHeight = Mathf.Max(0.1f, holeCenter.y + wallHeight * 0.5f - holeSize.y * 0.5f);

        CreateCube(root.transform, "Left Wall", new Vector3(-wallWidth * 0.5f + leftWidth * 0.5f, 0f, 0f), new Vector3(leftWidth, wallHeight, thickness), wallColor);
        CreateCube(root.transform, "Right Wall", new Vector3(wallWidth * 0.5f - rightWidth * 0.5f, 0f, 0f), new Vector3(rightWidth, wallHeight, thickness), wallColor);
        CreateCube(root.transform, "Top Wall", new Vector3(holeCenter.x, wallHeight * 0.5f - topHeight * 0.5f, 0f), new Vector3(holeSize.x, topHeight, thickness), wallColor);
        CreateCube(root.transform, "Bottom Wall", new Vector3(holeCenter.x, -wallHeight * 0.5f + bottomHeight * 0.5f, 0f), new Vector3(holeSize.x, bottomHeight, thickness), wallColor);
        CreateCube(root.transform, "Hole Glow", new Vector3(holeCenter.x, holeCenter.y, -0.04f), new Vector3(holeSize.x, holeSize.y, 0.05f), new Color(0.02f, 0.04f, 0.06f, 0.9f));

        GameObject labelObject = new GameObject("Gesture Label");
        labelObject.transform.SetParent(root.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, -3.05f, -0.1f);
        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.text = GestureDisplayName(targetGesture);
        label.fontSize = 1.05f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = trimColor;

        return root.transform;
    }

    private void CreateCube(Transform parent, string objectName, Vector3 localPosition, Vector3 scale, Color color)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = objectName;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localScale = scale;

        Renderer renderer = cube.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = CreateMaterial(color);
        }

        Collider collider = cube.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
    }

    private Vector2 GetHoleCenter(string gesture)
    {
        switch (gesture)
        {
            case "swipe_left":
                return new Vector2(-1.8f, 0f);
            case "swipe_right":
                return new Vector2(1.8f, 0f);
            case "swipe_up":
                return new Vector2(0f, 1.1f);
            default:
                return Vector2.zero;
        }
    }

    private Vector2 GetHoleSize(string gesture)
    {
        switch (gesture)
        {
            case "fist":
                return new Vector2(1.35f, 1.35f);
            case "swipe_left":
            case "swipe_right":
                return new Vector2(1.7f, 2.3f);
            case "swipe_up":
                return new Vector2(2.1f, 1.65f);
            default:
                return new Vector2(2.4f, 2.2f);
        }
    }

    private void ResolveWall(string targetGesture)
    {
        string currentGesture = GetCurrentGesture();
        bool success = currentGesture == targetGesture;

        if (success)
        {
            score += 100;
            wallSpeed += 0.15f;
            RefreshHud("Passou!");
            return;
        }

        lives = Mathf.Max(0, lives - 1);
        RefreshHud("Errou: precisava " + GestureDisplayName(targetGesture));
    }

    private string GetCurrentGesture()
    {
        UpdateKeyboardGesture();

        GesturePacket packet;
        if (receiver != null && receiver.TryGetLatestPacket(playerId, out packet) && packet != null && !string.IsNullOrWhiteSpace(packet.gesture))
        {
            if (packet.gesture != "none")
            {
                return packet.gesture;
            }
        }

        if (legacyReceiver != null && !string.IsNullOrWhiteSpace(legacyReceiver.data) && legacyReceiver.data.StartsWith("{"))
        {
            try
            {
                packet = JsonUtility.FromJson<GesturePacket>(legacyReceiver.data);
                if (packet != null && !string.IsNullOrWhiteSpace(packet.gesture))
                {
                    if (packet.gesture != "none")
                    {
                        return packet.gesture;
                    }
                }
            }
            catch
            {
            }
        }

        if (allowKeyboardFallback && Time.time <= keyboardGestureUntil)
        {
            return keyboardGesture;
        }

        return "none";
    }

    private void UpdateKeyboardGesture()
    {
        if (!allowKeyboardFallback)
        {
            return;
        }

        string pressedGesture = "none";

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            pressedGesture = "open";
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            pressedGesture = "fist";
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            pressedGesture = "swipe_left";
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            pressedGesture = "swipe_right";
        }
        else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space))
        {
            pressedGesture = "swipe_up";
        }

        if (pressedGesture == "none")
        {
            return;
        }

        keyboardGesture = pressedGesture;
        keyboardGestureUntil = Time.time + keyboardGestureHoldSeconds;
    }

    private void BuildHud()
    {
        if (hudCanvas != null)
        {
            Destroy(hudCanvas.gameObject);
        }

        GameObject canvasObject = new GameObject("Wall Challenge HUD");
        DontDestroyOnLoad(canvasObject);

        hudCanvas = canvasObject.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 20;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        scoreText = CreateHudText(canvasObject.transform, "Score", new Vector2(28f, -24f), TextAlignmentOptions.TopLeft, 32);
        livesText = CreateHudText(canvasObject.transform, "Lives", new Vector2(28f, -66f), TextAlignmentOptions.TopLeft, 32);
        targetText = CreateHudText(canvasObject.transform, "Target", new Vector2(0f, -28f), TextAlignmentOptions.Top, 44);
        statusText = CreateHudText(canvasObject.transform, "Status", new Vector2(0f, -84f), TextAlignmentOptions.Top, 28);
        currentGestureText = CreateHudText(canvasObject.transform, "CurrentGesture", new Vector2(-28f, -24f), TextAlignmentOptions.TopRight, 28);
    }

    private TMP_Text CreateHudText(Transform parent, string objectName, Vector2 anchoredPosition, TextAlignmentOptions alignment, int fontSize)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = AnchorForAlignment(alignment);
        rect.anchorMax = AnchorForAlignment(alignment);
        rect.pivot = AnchorForAlignment(alignment);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(720f, 64f);

        TMP_Text text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = alignment;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private Vector2 AnchorForAlignment(TextAlignmentOptions alignment)
    {
        if (alignment == TextAlignmentOptions.TopLeft)
        {
            return new Vector2(0f, 1f);
        }

        if (alignment == TextAlignmentOptions.TopRight)
        {
            return new Vector2(1f, 1f);
        }

        return new Vector2(0.5f, 1f);
    }

    private void RefreshHud(string status)
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + score;
        }

        if (livesText != null)
        {
            livesText.text = "Vidas: " + lives;
        }

        if (targetText != null)
        {
            targetText.text = "Gesto: " + GestureDisplayName(currentTarget);
        }

        if (statusText != null)
        {
            statusText.text = status;
        }

        UpdateCurrentGestureText();
    }

    private void UpdateCurrentGestureText()
    {
        if (currentGestureText == null)
        {
            return;
        }

        string gesture = GetCurrentGesture();
        currentGestureText.text = "Atual: " + GestureDisplayName(gesture);
        currentGestureText.color = roundActive && gesture == currentTarget ? new Color(0.2f, 1f, 0.55f) : Color.white;
    }

    private string GestureDisplayName(string gesture)
    {
        switch (gesture)
        {
            case "open":
                return "MAO ABERTA";
            case "fist":
                return "PUNHO";
            case "swipe_left":
                return "ESQUERDA";
            case "swipe_right":
                return "DIREITA";
            case "swipe_up":
                return "CIMA";
            default:
                return "NENHUM";
        }
    }

    private void CreateFloorGuide()
    {
        GameObject guide = GameObject.CreatePrimitive(PrimitiveType.Cube);
        guide.name = "Wall Challenge Pass Line";
        guide.transform.position = new Vector3(0f, 0.02f, passZ);
        guide.transform.localScale = new Vector3(8f, 0.04f, 0.08f);

        Renderer renderer = guide.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = CreateMaterial(new Color(1f, 0.85f, 0.1f, 1f));
        }

        Collider collider = guide.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
    }

    private Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader);
        material.color = color;
        return material;
    }
}
