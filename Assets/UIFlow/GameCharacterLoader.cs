using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameCharacterLoader : MonoBehaviour
{
    [SerializeField] private TMP_Text nicknameText;
    [SerializeField] private Image uiCharacterImage;
    [SerializeField] private SpriteRenderer worldCharacterSprite;
    [SerializeField] private Sprite[] presetSprites;

    private void Start()
    {
        PlayerProfileData profile = PlayerProfileStore.Get();

        if (nicknameText != null)
        {
            nicknameText.text = string.IsNullOrWhiteSpace(profile.nickname) ? "Player" : profile.nickname;
        }

        Sprite loadedSprite = LoadSpriteFromProfile(profile);
        if (loadedSprite == null)
        {
            return;
        }

        if (uiCharacterImage != null)
        {
            uiCharacterImage.sprite = loadedSprite;
            uiCharacterImage.preserveAspect = true;
        }

        if (worldCharacterSprite != null)
        {
            worldCharacterSprite.sprite = loadedSprite;
        }
    }

    private Sprite LoadSpriteFromProfile(PlayerProfileData profile)
    {
        if (profile.characterSource == CharacterSourceType.Drawing && !string.IsNullOrWhiteSpace(profile.drawingImagePath))
        {
            if (File.Exists(profile.drawingImagePath))
            {
                byte[] pngData = File.ReadAllBytes(profile.drawingImagePath);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (texture.LoadImage(pngData))
                {
                    return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }
        }

        if (presetSprites == null || presetSprites.Length == 0)
        {
            return null;
        }

        int index = Mathf.Clamp(profile.presetCharacterIndex, 0, presetSprites.Length - 1);
        return presetSprites[index];
    }
}
