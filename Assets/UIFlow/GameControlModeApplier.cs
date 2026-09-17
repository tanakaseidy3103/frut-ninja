using UnityEngine;

public class GameControlModeApplier : MonoBehaviour
{
    [SerializeField] private GameObject handControlRoot;
    [SerializeField] private GameObject characterControlRoot;

    private void Start()
    {
        PlayerProfileData profile = PlayerProfileStore.Get();
        bool handMode = profile.controlTarget == ControlTargetType.Hand;

        if (handControlRoot != null)
        {
            handControlRoot.SetActive(handMode);
        }

        if (characterControlRoot != null)
        {
            characterControlRoot.SetActive(!handMode);
        }
    }
}
