using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StartMenuController : MonoBehaviour
{
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private string characterSceneName = "Character";
    [SerializeField] private string fallbackNickname = "Player";

    private void Start()
    {
        PlayerProfileData profile = PlayerProfileStore.Get();
        if (nicknameInput != null)
        {
            nicknameInput.text = string.IsNullOrWhiteSpace(profile.nickname) ? fallbackNickname : profile.nickname;
        }
    }

    public void OnContinuePressed()
    {
        PlayerProfileData profile = PlayerProfileStore.Get();
        string nickname = nicknameInput != null ? nicknameInput.text : string.Empty;

        if (string.IsNullOrWhiteSpace(nickname))
        {
            nickname = fallbackNickname;
        }

        profile.nickname = nickname.Trim();
        PlayerProfileStore.Save(profile);
        SceneManager.LoadScene(characterSceneName);
    }
}
