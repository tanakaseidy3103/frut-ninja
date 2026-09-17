using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DrawingCanvasController : MonoBehaviour
{
    [SerializeField] private RawImage drawingTarget;
    [SerializeField] private int textureSize = 512;
    [SerializeField] private Color brushColor = Color.black;
    [SerializeField] private Color clearColor = new Color(0f, 0f, 0f, 0f);
    [SerializeField] private int brushSize = 8;
    [SerializeField] private string characterSceneName = "Character";
    [SerializeField] private string gameSceneName = "Game";

    private Texture2D drawingTexture;

    private void Start()
    {
        drawingTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        drawingTexture.filterMode = FilterMode.Point;
        ClearCanvas();

        if (drawingTarget != null)
        {
            drawingTarget.texture = drawingTexture;
        }
    }

    private void Update()
    {
        if (drawingTarget == null || drawingTexture == null)
        {
            return;
        }

        if (!Input.GetMouseButton(0))
        {
            return;
        }

        RectTransform rectTransform = drawingTarget.rectTransform;
        Vector2 localPoint;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, Input.mousePosition, null, out localPoint))
        {
            return;
        }

        Rect rect = rectTransform.rect;
        float normalizedX = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
        float normalizedY = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);

        int pixelX = Mathf.RoundToInt(normalizedX * (textureSize - 1));
        int pixelY = Mathf.RoundToInt(normalizedY * (textureSize - 1));

        DrawBrush(pixelX, pixelY);
        drawingTexture.Apply();
    }

    public void ClearCanvas()
    {
        if (drawingTexture == null)
        {
            return;
        }

        Color[] pixels = new Color[textureSize * textureSize];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = clearColor;
        }

        drawingTexture.SetPixels(pixels);
        drawingTexture.Apply();
    }

    public void SaveAndReturnToCharacterScene()
    {
        string path = SaveDrawingToDisk();
        if (!string.IsNullOrWhiteSpace(path))
        {
            PlayerProfileData profile = PlayerProfileStore.Get();
            profile.characterSource = CharacterSourceType.Drawing;
            profile.drawingImagePath = path;
            PlayerProfileStore.Save(profile);
        }

        SceneManager.LoadScene(characterSceneName);
    }

    public void SaveAndStartGame()
    {
        string path = SaveDrawingToDisk();
        if (!string.IsNullOrWhiteSpace(path))
        {
            PlayerProfileData profile = PlayerProfileStore.Get();
            profile.characterSource = CharacterSourceType.Drawing;
            profile.drawingImagePath = path;
            PlayerProfileStore.Save(profile);
        }

        SceneManager.LoadScene(gameSceneName);
    }

    private void DrawBrush(int centerX, int centerY)
    {
        for (int y = -brushSize; y <= brushSize; y++)
        {
            for (int x = -brushSize; x <= brushSize; x++)
            {
                int px = centerX + x;
                int py = centerY + y;

                if (px < 0 || px >= textureSize || py < 0 || py >= textureSize)
                {
                    continue;
                }

                if ((x * x) + (y * y) <= brushSize * brushSize)
                {
                    drawingTexture.SetPixel(px, py, brushColor);
                }
            }
        }
    }

    private string SaveDrawingToDisk()
    {
        if (drawingTexture == null)
        {
            return string.Empty;
        }

        byte[] pngData = drawingTexture.EncodeToPNG();
        if (pngData == null || pngData.Length == 0)
        {
            return string.Empty;
        }

        string filePath = Path.Combine(Application.persistentDataPath, "player_drawing.png");
        File.WriteAllBytes(filePath, pngData);
        return filePath;
    }
}
