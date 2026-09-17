using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LogisticsGameController : MonoBehaviour
{
    public static LogisticsGameController Instance { get; private set; }

    [Header("Game Configuration")]
    public int totalTime = 60;
    public int maxLives = 3;

    [Header("UI References (Optional - will auto-create if null)")]
    public Canvas hudCanvas;
    public TMP_Text scoreText;
    public TMP_Text timerText;
    public TMP_Text livesText;
    public TMP_Text instructionText;

    private int score = 0;
    private int lives = 0;
    private float timeLeft = 0;
    private bool isGameOver = false;

    // References to our package system
    private PackageGrabber packageGrabber;
    private GameObject binRed;
    private GameObject binBlue;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        lives = maxLives;
        timeLeft = totalTime;

        // Auto-configure PackageGrabber
        packageGrabber = FindFirstObjectByType<PackageGrabber>();
        if (packageGrabber == null)
        {
            GameObject grabberObj = new GameObject("PackageSystem");
            packageGrabber = grabberObj.AddComponent<PackageGrabber>();
        }

        // Auto-assign hand center if not assigned
        if (packageGrabber.handCenter == null)
        {
            GameObject hand = GameObject.Find("Manager_Hand") ?? GameObject.Find("Points") ?? GameObject.Find("RightHand_CV");
            if (hand != null)
            {
                packageGrabber.handCenter = hand.transform;
            }
        }

        // Spawn Bins
        CreateTargetBins();

        // Build HUD
        CreateHUD();

        StartCoroutine(GameTimerLoop());
    }

    void Update()
    {
        if (isGameOver)
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                RestartGame();
            }
            return;
        }

        UpdateHUDText();
    }

    private void CreateTargetBins()
    {
        Vector3 referencePos = Vector3.zero;
        if (packageGrabber != null && packageGrabber.handCenter != null)
        {
            referencePos = packageGrabber.handCenter.position;
        }
        else
        {
            GameObject hand = GameObject.Find("Manager_Hand") ?? GameObject.Find("Points") ?? GameObject.Find("RightHand_CV");
            if (hand != null) referencePos = hand.transform.position;
        }

        // Red Delivery Bin (Target Left and further forward)
        binRed = GameObject.CreatePrimitive(PrimitiveType.Cube);
        binRed.name = "RedBin";
        binRed.transform.position = referencePos + Vector3.forward * 3.5f + Vector3.left * 2.2f + Vector3.down * 0.5f;
        binRed.transform.localScale = new Vector3(1.5f, 1f, 1.5f);
        binRed.GetComponent<Renderer>().material.color = new Color(0.9f, 0.2f, 0.2f);
        AddTrigger(binRed, "Red");

        // Label for Red Bin
        CreateWorldLabel("TOKYO (RED)", binRed.transform.position + Vector3.up * 0.7f, Color.red);

        // Blue Delivery Bin (Target Right and further forward)
        binBlue = GameObject.CreatePrimitive(PrimitiveType.Cube);
        binBlue.name = "BlueBin";
        binBlue.transform.position = referencePos + Vector3.forward * 3.5f + Vector3.right * 2.2f + Vector3.down * 0.5f;
        binBlue.transform.localScale = new Vector3(1.5f, 1f, 1.5f);
        binBlue.GetComponent<Renderer>().material.color = new Color(0.2f, 0.2f, 0.9f);
        AddTrigger(binBlue, "Blue");

        // Label for Blue Bin
        CreateWorldLabel("LONDON (BLUE)", binBlue.transform.position + Vector3.up * 0.7f, Color.blue);
    }

    private void AddTrigger(GameObject bin, string colorTag)
    {
        // Add a child trigger collider to detect packages
        GameObject triggerObj = new GameObject("Trigger");
        triggerObj.transform.SetParent(bin.transform, false);
        triggerObj.transform.localScale = new Vector3(0.9f, 1.2f, 0.9f);
        
        BoxCollider col = triggerObj.AddComponent<BoxCollider>();
        col.isTrigger = true;

        BinTriggerHandler handler = triggerObj.AddComponent<BinTriggerHandler>();
        handler.targetTag = colorTag;
    }

    private void CreateWorldLabel(string text, Vector3 position, Color color)
    {
        GameObject labelObj = new GameObject("BinLabel");
        labelObj.transform.position = position;
        labelObj.transform.localScale = Vector3.one * 0.8f;
        
        TextMeshPro tm = labelObj.AddComponent<TextMeshPro>();
        tm.text = text;
        tm.fontSize = 4;
        tm.alignment = TextAlignmentOptions.Center;
        tm.color = color;
    }

    private void CreateHUD()
    {
        if (hudCanvas != null) return;

        GameObject canvasObj = new GameObject("LogisticsHUD");
        hudCanvas = canvasObj.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<GraphicRaycaster>();
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // Text elements
        scoreText = CreateTextElement(canvasObj.transform, "ScoreText", new Vector2(30f, -30f), TextAlignmentOptions.TopLeft, 32);
        timerText = CreateTextElement(canvasObj.transform, "TimerText", new Vector2(0f, -30f), TextAlignmentOptions.Top, 36);
        livesText = CreateTextElement(canvasObj.transform, "LivesText", new Vector2(-30f, -30f), TextAlignmentOptions.TopRight, 32);
        instructionText = CreateTextElement(canvasObj.transform, "InstructionText", new Vector2(0f, 100f), TextAlignmentOptions.Bottom, 32);
    }

    private TMP_Text CreateTextElement(Transform parent, string name, Vector2 pos, TextAlignmentOptions align, int fontSize)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);

        RectTransform rect = textObj.AddComponent<RectTransform>();
        rect.anchorMin = AnchorForAlignment(align);
        rect.anchorMax = AnchorForAlignment(align);
        rect.pivot = AnchorForAlignment(align);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(600f, 60f);

        TMP_Text tm = textObj.AddComponent<TextMeshProUGUI>();
        tm.fontSize = fontSize;
        tm.color = Color.white;
        tm.alignment = align;
        tm.fontStyle = FontStyles.Bold;
        
        return tm;
    }

    private Vector2 AnchorForAlignment(TextAlignmentOptions alignment)
    {
        switch (alignment)
        {
            case TextAlignmentOptions.TopLeft: return new Vector2(0f, 1f);
            case TextAlignmentOptions.TopRight: return new Vector2(1f, 1f);
            case TextAlignmentOptions.Top: return new Vector2(0.5f, 1f);
            case TextAlignmentOptions.Bottom: return new Vector2(0.5f, 0f);
            default: return new Vector2(0.5f, 0.5f);
        }
    }

    private void UpdateHUDText()
    {
        if (scoreText != null) scoreText.text = "Score: " + score;
        if (timerText != null) timerText.text = "Time: " + Mathf.CeilToInt(timeLeft) + "s";
        if (livesText != null) livesText.text = "Mode: Infinite";
        if (instructionText != null)
        {
            instructionText.text = "Throw the Boxes into the Destination Bins!";
            instructionText.color = Color.yellow;
        }
    }

    private IEnumerator GameTimerLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            timeLeft++;
        }
    }

    public void AddScore(int points)
    {
        score += points;
    }

    public void LoseLife()
    {
        // Infinite mode has no lives limit
    }

    private void EndGame()
    {
        // No end game in infinite mode
    }

    private void RestartGame()
    {
        score = 0;
        lives = maxLives;
        timeLeft = totalTime;
        isGameOver = false;

        // Clean up existing packages
        GameObject[] pkgs = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var pkg in pkgs)
        {
            if (pkg.name.Contains("DeliveryPackage") || pkg.name.Contains("Package") || pkg.name.Contains("Box"))
            {
                Destroy(pkg);
            }
        }

        if (packageGrabber != null) packageGrabber.enabled = true;
        StartCoroutine(GameTimerLoop());
    }
}

public class BinTriggerHandler : MonoBehaviour
{
    public string targetTag;

    private void OnTriggerEnter(Collider other)
    {
        if (other.name.Contains("DeliveryPackage") || other.name.Contains("Package") || other.name.Contains("Box"))
        {
            // Verify if it hit the correct bin (procedural logic could be color based, currently giving score for any bin landing)
            LogisticsGameController.Instance.AddScore(100);
            
            // Play visual effect (change color of package briefly)
            Renderer r = other.GetComponent<Renderer>();
            if (r != null)
            {
                r.material.color = targetTag == "Red" ? Color.red : Color.blue;
            }

            // Destroy package after landing successfully
            Destroy(other.gameObject, 0.5f);
        }
    }
}
