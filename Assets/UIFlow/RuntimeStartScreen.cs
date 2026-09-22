using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RuntimeStartScreen : MonoBehaviour
{
    public static event System.Action GameStarted;

    private static RuntimeStartScreen instance;
    private static bool gameStarted;

    private readonly List<Behaviour> pausedBehaviours = new List<Behaviour>();
    private Canvas canvas;
    private Canvas waitingCanvas;
    private TMP_Text waitingLabel;
    private TMP_FontAsset japaneseFontAsset;
    private System.Diagnostics.Process cameraProcess;
    private float previousTimeScale = 1f;
    private bool launchCameraOnStart = true;
    private string pythonExecutable = "python";
    private string cameraIndex = "0";
    private const float CameraConnectTimeoutSeconds = 25f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateBootstrap()
    {
        if (instance != null)
        {
            return;
        }

        GameObject bootstrap = new GameObject("Runtime Start Screen");
        instance = bootstrap.AddComponent<RuntimeStartScreen>();
        DontDestroyOnLoad(bootstrap);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (gameStarted)
        {
            return;
        }

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        PauseGameplayScripts();
        BuildStartScreen();
    }

    private void PauseGameplayScripts()
    {
        pausedBehaviours.Clear();

        foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (behaviour == null || behaviour == this || !behaviour.enabled)
            {
                continue;
            }

            if (!ShouldPause(behaviour))
            {
                continue;
            }

            behaviour.enabled = false;
            pausedBehaviours.Add(behaviour);
        }
    }

    private bool ShouldPause(MonoBehaviour behaviour)
    {
        string typeName = behaviour.GetType().Name;
        return typeName == "UDPReceive"
            || typeName == "GestureUdpReceiver"
            || typeName == "HandTracking"
            || typeName == "HandController"
            || typeName == "HandCon"
            || typeName == "HandCon2"
            || typeName == "FinalScript"
            || typeName == "GestureCharacterMotor"
            || typeName == "GesturePlayerRig"
            || typeName == "GestureHandAvatar"
            || typeName == "GameControlModeApplier"
            || typeName == "GameCharacterLoader";
    }

    private void BuildStartScreen()
    {
        if (canvas != null)
        {
            Destroy(canvas.gameObject);
        }

        canvas = CreateCanvas();
        japaneseFontAsset = CreateJapaneseFontAsset();

        Image background = CreatePanel(canvas.transform, "Background", new Color(0.04f, 0.05f, 0.07f, 0.96f));
        Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        Image accent = CreatePanel(canvas.transform, "Accent", new Color(0.0f, 0.75f, 0.82f, 0.25f));
        Stretch(accent.rectTransform, new Vector2(0f, 0.55f), Vector2.one, Vector2.zero, Vector2.zero);

        RectTransform panel = CreatePanel(canvas.transform, "Menu Panel", new Color(0.09f, 0.11f, 0.14f, 0.92f)).rectTransform;
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(620f, 430f);
        panel.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(44, 44, 38, 38);
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;

        TMP_Text title = CreateText(panel, "3D HAND FRUIT NINJA", 40, FontStyles.Bold, TextAlignmentOptions.Center);
        title.color = new Color(1f, 0.85f, 0.1f);
        AddLayout(title.gameObject, 540f, 60f);

        TMP_Text subtitle = CreateText(panel, "Slice Flying Fruits With Your Hand!", 22, FontStyles.Normal, TextAlignmentOptions.Center);
        subtitle.color = new Color(0.2f, 1f, 0.4f);
        AddLayout(subtitle.gameObject, 540f, 35f);

        TMP_Text hint = CreateText(panel, "Swipe fast to slice fruits / Avoid bombs!", 18, FontStyles.Normal, TextAlignmentOptions.Center);
        hint.color = new Color(0.9f, 0.95f, 1f);
        AddLayout(hint.gameObject, 540f, 45f);

        Button playButton = CreateButton(panel, "PLAY NINJA", new Color(0.95f, 0.3f, 0.1f), new Color(1f, 1f, 1f));
        AddLayout(playButton.gameObject, 320f, 60f);
        playButton.onClick.AddListener(StartGame);

        Button quitButton = CreateButton(panel, "EXIT", new Color(0.18f, 0.21f, 0.25f), new Color(0.82f, 0.88f, 0.92f));
        AddLayout(quitButton.gameObject, 320f, 48f);
        quitButton.onClick.AddListener(QuitGame);
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Start Screen Canvas");
        DontDestroyOnLoad(canvasObject);

        Canvas createdCanvas = canvasObject.AddComponent<Canvas>();
        createdCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        createdCanvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            DontDestroyOnLoad(eventSystem);
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        return createdCanvas;
    }

    private Image CreatePanel(Transform parent, string objectName, Color color)
    {
        GameObject panelObject = new GameObject(objectName);
        panelObject.transform.SetParent(parent, false);
        Image image = panelObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private TMP_Text CreateText(Transform parent, string text, int fontSize, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(text);
        textObject.transform.SetParent(parent, false);

        TMP_Text label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        if (japaneseFontAsset != null)
        {
            label.font = japaneseFontAsset;
        }
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.Normal;

        return label;
    }

    private TMP_FontAsset CreateJapaneseFontAsset()
    {
        Font japaneseFont = Font.CreateDynamicFontFromOSFont("Noto Sans JP", 64);
        if (japaneseFont == null)
        {
            japaneseFont = Font.CreateDynamicFontFromOSFont("Meiryo", 64);
        }

        if (japaneseFont == null)
        {
            Debug.LogWarning("Japanese font was not found. The start screen may show missing glyphs.");
            return null;
        }

        return TMP_FontAsset.CreateFontAsset(japaneseFont);
    }

    private Button CreateButton(Transform parent, string text, Color backgroundColor, Color textColor)
    {
        Image image = CreatePanel(parent, text + " Button", backgroundColor);
        Button button = image.gameObject.AddComponent<Button>();

        ColorBlock colors = button.colors;
        colors.normalColor = backgroundColor;
        colors.highlightedColor = Color.Lerp(backgroundColor, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(backgroundColor, Color.black, 0.18f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        TMP_Text label = CreateText(image.transform, text, 24, FontStyles.Bold, TextAlignmentOptions.Center);
        label.color = textColor;
        Stretch(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        return button;
    }

    private void AddLayout(GameObject target, float preferredWidth, float preferredHeight)
    {
        LayoutElement element = target.AddComponent<LayoutElement>();
        element.preferredWidth = preferredWidth;
        element.preferredHeight = preferredHeight;
    }

    private void Stretch(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
    }

    private void StartGame()
    {
        gameStarted = true;
        LaunchCameraSender();

        bool hasLegacyUdpReceiver = HasPausedBehaviour("UDPReceive");

        foreach (Behaviour behaviour in pausedBehaviours)
        {
            if (behaviour != null)
            {
                if (hasLegacyUdpReceiver && behaviour.GetType().Name == "GestureUdpReceiver")
                {
                    continue;
                }

                behaviour.enabled = true;
            }
        }

        pausedBehaviours.Clear();

        if (canvas != null)
        {
            Destroy(canvas.gameObject);
            canvas = null;
        }

        // Keep Time.timeScale at 0 so fruits don't spawn/fall while the camera is still connecting.
        BuildWaitingScreen();
        StartCoroutine(WaitForCameraThenBeginGame());
    }

    private IEnumerator WaitForCameraThenBeginGame()
    {
        UDPReceive legacyReceiver = FindFirstObjectByType<UDPReceive>();
        GestureUdpReceiver gestureReceiver = FindFirstObjectByType<GestureUdpReceiver>();

        float elapsed = 0f;
        while (elapsed < CameraConnectTimeoutSeconds)
        {
            bool legacyReady = legacyReceiver != null && !string.IsNullOrEmpty(legacyReceiver.data);
            bool gestureReady = gestureReceiver != null && gestureReceiver.ActivePlayerCount > 0;

            if (legacyReady || gestureReady)
            {
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        FinishStartingGame();
    }

    private void FinishStartingGame()
    {
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;

        if (waitingCanvas != null)
        {
            Destroy(waitingCanvas.gameObject);
            waitingCanvas = null;
        }

        if (FindFirstObjectByType<FruitNinjaGameController>() == null)
        {
            GameObject ninjaObj = new GameObject("FruitNinjaSystem");
            ninjaObj.AddComponent<FruitNinjaGameController>();
        }

        GameStarted?.Invoke();
    }

    private void BuildWaitingScreen()
    {
        if (waitingCanvas != null)
        {
            Destroy(waitingCanvas.gameObject);
        }

        waitingCanvas = CreateCanvas();

        Image background = CreatePanel(waitingCanvas.transform, "Waiting Background", new Color(0.04f, 0.05f, 0.07f, 0.96f));
        Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        RectTransform panel = CreatePanel(waitingCanvas.transform, "Waiting Panel", new Color(0.09f, 0.11f, 0.14f, 0.92f)).rectTransform;
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(620f, 220f);
        panel.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(44, 44, 30, 30);
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;

        TMP_Text title = CreateText(panel, "CONNECTING CAMERA...", 32, FontStyles.Bold, TextAlignmentOptions.Center);
        title.color = new Color(0.2f, 1f, 0.9f);
        AddLayout(title.gameObject, 540f, 45f);

        waitingLabel = CreateText(panel, "Please wait while the hand tracker starts up", 20, FontStyles.Normal, TextAlignmentOptions.Center);
        waitingLabel.color = new Color(0.85f, 0.9f, 0.95f);
        AddLayout(waitingLabel.gameObject, 540f, 60f);
    }

    private bool HasPausedBehaviour(string typeName)
    {
        foreach (Behaviour behaviour in pausedBehaviours)
        {
            if (behaviour != null && behaviour.GetType().Name == typeName)
            {
                return true;
            }
        }

        return false;
    }

    private void LaunchCameraSender()
    {
        if (!launchCameraOnStart || cameraProcess != null)
        {
            return;
        }

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string senderPath = Path.Combine(projectRoot, "python", "gesture_sender.py");

        if (!File.Exists(senderPath))
        {
            Debug.LogWarning("Camera sender not found: " + senderPath);
            return;
        }

        cameraProcess = TryStartCameraProcess(pythonExecutable, senderPath, projectRoot);
        if (cameraProcess == null || cameraProcess.HasExited)
        {
            cameraProcess = TryStartCameraProcess("py", senderPath, projectRoot);
        }

        if (cameraProcess == null)
        {
            Debug.LogWarning("Camera sender could not be started. Run python/run_camera_tracking.bat manually.");
        }
    }

    private System.Diagnostics.Process TryStartCameraProcess(string executable, string senderPath, string projectRoot)
    {
        try
        {
            return StartCameraProcess(executable, senderPath, projectRoot);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("Could not start camera sender with " + executable + ": " + exception.Message);
            return null;
        }
    }

    private System.Diagnostics.Process StartCameraProcess(string executable, string senderPath, string projectRoot)
    {
        System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = executable,
            Arguments = "-u " + Quote(senderPath)
                + " --host 127.0.0.1"
                + " --port 5052"
                + " --player-id player-1"
                + " --camera " + Quote(cameraIndex)
                + " --show",
            WorkingDirectory = projectRoot,
            UseShellExecute = false,
            CreateNoWindow = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo);
        process.OutputDataReceived += OnCameraOutput;
        process.ErrorDataReceived += OnCameraError;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private void OnCameraOutput(object sender, System.Diagnostics.DataReceivedEventArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.Data))
        {
            Debug.Log("Camera sender: " + args.Data);
        }
    }

    private void OnCameraError(object sender, System.Diagnostics.DataReceivedEventArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.Data))
        {
            Debug.LogWarning("Camera sender error: " + args.Data);
        }
    }

    private string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private void QuitGame()
    {
        Application.Quit();
    }

    private void OnApplicationQuit()
    {
        StopCameraSender();
    }

    private void OnDestroy()
    {
        StopCameraSender();
    }

    private void StopCameraSender()
    {
        if (cameraProcess == null)
        {
            return;
        }

        try
        {
            if (!cameraProcess.HasExited)
            {
                cameraProcess.Kill();
            }
        }
        catch
        {
        }
        finally
        {
            cameraProcess.Dispose();
            cameraProcess = null;
        }
    }
}
