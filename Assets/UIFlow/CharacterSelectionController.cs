using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CharacterSelectionController : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private string drawingSceneName = "CharacterDraw";
    [SerializeField] private TMP_Text selectedText;

    private int selectedPreset;

    private void Start()
    {
        PlayerProfileData profile = PlayerProfileStore.Get();
        selectedPreset = profile.presetCharacterIndex;
        RefreshSelectedText();
    }

    public void SelectPresetCharacter(int presetIndex)
    {
        selectedPreset = Mathf.Max(0, presetIndex);

        PlayerProfileData profile = PlayerProfileStore.Get();
        profile.characterSource = CharacterSourceType.Preset;
        profile.presetCharacterIndex = selectedPreset;
        PlayerProfileStore.Save(profile);

        RefreshSelectedText();
    }

    public void OpenDrawingScene()
    {
        SceneManager.LoadScene(drawingSceneName);
    }

    public void SetHandControlMode()
    {
        PlayerProfileData profile = PlayerProfileStore.Get();
        profile.controlTarget = ControlTargetType.Hand;
        PlayerProfileStore.Save(profile);
        RefreshSelectedText();
    }

    public void SetCharacterControlMode()
    {
        PlayerProfileData profile = PlayerProfileStore.Get();
        profile.controlTarget = ControlTargetType.Character;
        PlayerProfileStore.Save(profile);
        RefreshSelectedText();
    }

    public void StartGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    private void RefreshSelectedText()
    {
        if (selectedText == null)
        {
            return;
        }

        PlayerProfileData profile = PlayerProfileStore.Get();
        string modeName = profile.controlTarget == ControlTargetType.Hand ? "Hand" : "Character";
        string sourceName = profile.characterSource == CharacterSourceType.Drawing ? "Drawing" : ("Preset " + selectedPreset);
        selectedText.text = "Mode: " + modeName + " | Avatar: " + sourceName;
    }
}
