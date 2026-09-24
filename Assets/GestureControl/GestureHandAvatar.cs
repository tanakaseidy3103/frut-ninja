using UnityEngine;

[DisallowMultipleComponent]
public class GestureHandAvatar : MonoBehaviour
{
    [SerializeField] private GestureUdpReceiver receiver;
    [SerializeField] private string playerId = "player-1";
    [SerializeField] private Transform[] handPoints = new Transform[21];
    [SerializeField] private bool autoAssignReferences = true;
    [SerializeField] private Transform pointsRoot;
    [SerializeField] private Vector3 positionScale = new Vector3(0.35f, 0.35f, 0.6f);
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    [SerializeField] private float smoothing = 24f;
    [SerializeField] private bool moveWholeHand = true;
    [SerializeField] private float moveRangeX = 3.5f;
    [SerializeField] private float moveRangeY = 1.8f;
    [SerializeField] private float moveRangeZ = 0.7f;
    [SerializeField] private float rootSmoothing = 9f;
    [SerializeField] private bool mirrorX = true;
    [SerializeField] private bool invertY = true;
    [SerializeField] private bool invertZ = true;

    private Vector3 baseLocalPosition;

    public string PlayerId
    {
        get { return playerId; }
        set { playerId = value; }
    }

    private void Awake()
    {
        EnsureReferences();
    }

    private void Start()
    {
        EnsureReferences();
        baseLocalPosition = transform.localPosition;
    }

    private void OnValidate()
    {
        EnsureReferences();
    }

    private void Update()
    {
        if (receiver == null)
        {
            return;
        }

        GesturePacket packet;
        if (!receiver.TryGetLatestPacket(playerId, out packet))
        {
            return;
        }

        if (packet.landmarks == null)
        {
            return;
        }

        if (moveWholeHand)
        {
            ApplyRootMove(packet, lerpFactor: 1f - Mathf.Exp(-rootSmoothing * Time.deltaTime));
        }

        int pointCount = Mathf.Min(handPoints.Length, packet.landmarks.Length);
        float lerpFactor = 1f - Mathf.Exp(-smoothing * Time.deltaTime);

        for (int index = 0; index < pointCount; index++)
        {
            if (handPoints[index] == null)
            {
                continue;
            }

            GestureLandmark landmark = packet.landmarks[index];

            float localX = ((mirrorX ? 0.5f - landmark.x : landmark.x - 0.5f) * positionScale.x) + positionOffset.x;
            float localY = ((invertY ? 0.5f - landmark.y : landmark.y - 0.5f) * positionScale.y) + positionOffset.y;
            float localZ = (((invertZ ? -landmark.z : landmark.z) * positionScale.z) + positionOffset.z);

            Vector3 targetPosition = new Vector3(localX, localY, localZ);
            handPoints[index].localPosition = Vector3.Lerp(handPoints[index].localPosition, targetPosition, lerpFactor);
        }
    }

    private void ApplyRootMove(GesturePacket packet, float lerpFactor)
    {
        float moveX = 0f;
        float moveY = 0f;
        float moveZ = 0f;

        if (packet.command != null)
        {
            moveX = packet.command.moveX;
            moveY = packet.command.moveY;
            moveZ = packet.command.moveZ;
        }

        if (packet.landmarks != null && packet.landmarks.Length > 0)
        {
            GestureLandmark wrist = packet.landmarks[0];
            moveX = Mathf.Clamp((wrist.x - 0.5f) * 2f, -1f, 1f);
            moveY = Mathf.Clamp((0.5f - wrist.y) * 2f, -1f, 1f);
        }

        Vector3 target = baseLocalPosition + new Vector3(moveX * moveRangeX, moveY * moveRangeY, moveZ * moveRangeZ);
        transform.localPosition = Vector3.Lerp(transform.localPosition, target, lerpFactor);
    }

    private void EnsureReferences()
    {
        if (!autoAssignReferences)
        {
            return;
        }

        if (receiver == null)
        {
            receiver = FindFirstObjectByType<GestureUdpReceiver>();
        }

        if (pointsRoot == null)
        {
            GameObject pointsObject = GameObject.Find("Points");
            if (pointsObject != null)
            {
                pointsRoot = pointsObject.transform;
            }
        }

        if (pointsRoot == null)
        {
            return;
        }

        if (handPoints == null || handPoints.Length != 21)
        {
            handPoints = new Transform[21];
        }

        for (int i = 0; i < 21; i++)
        {
            Transform candidate = FindDeepChild(pointsRoot, i.ToString());
            if (candidate == null)
            {
                candidate = FindDeepChild(pointsRoot, "Sphere (" + i + ")");
            }

            if (candidate != null)
            {
                handPoints[i] = candidate;
            }
        }
    }

    private Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform result = FindDeepChild(child, childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}