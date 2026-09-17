using UnityEngine;

[DisallowMultipleComponent]
public class GesturePlayerRig : MonoBehaviour
{
    [SerializeField] private string playerId = "player-1";
    [SerializeField] private GestureHandAvatar handAvatar;
    [SerializeField] private GestureCharacterMotor characterMotor;

    private void Reset()
    {
        handAvatar = GetComponentInChildren<GestureHandAvatar>();
        characterMotor = GetComponentInChildren<GestureCharacterMotor>();
    }

    private void Awake()
    {
        ApplyPlayerId();
    }

    private void OnValidate()
    {
        ApplyPlayerId();
    }

    private void ApplyPlayerId()
    {
        if (handAvatar != null)
        {
            handAvatar.PlayerId = playerId;
        }

        if (characterMotor != null)
        {
            characterMotor.PlayerId = playerId;
        }
    }
}