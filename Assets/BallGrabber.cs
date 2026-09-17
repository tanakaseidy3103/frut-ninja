using UnityEngine;

public class BallGrabber : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The GameObject representing the hand wrist or palm (e.g., HandPoints[0] or HandPoints[9])")]
    public Transform handCenter;
    
    [Tooltip("Prefab of the ball to spawn. If null, a default sphere will be created.")]
    public GameObject ballPrefab;
    
    [Tooltip("Where to spawn new balls")]
    public Transform spawnPoint;

    [Header("Grabbing Settings")]
    public float grabRadius = 1.5f;
    public LayerMask ballLayer;
    
    [Header("Throwing Settings")]
    public float throwForceMultiplier = 15f;
    public float minThrowSpeed = 5f;

    private GameObject currentBall;
    private bool isGrabbing = false;
    private Vector3 lastHandPosition;
    private Vector3 handVelocity;

    private UDPReceive legacyReceiver;
    private GestureUdpReceiver receiver;
    
    void Start()
    {
        legacyReceiver = FindFirstObjectByType<UDPReceive>();
        receiver = FindFirstObjectByType<GestureUdpReceiver>();

        if (spawnPoint == null)
        {
            GameObject sp = new GameObject("Ball Spawn Point");
            sp.transform.position = new Vector3(0f, 1.5f, 2f);
            spawnPoint = sp.transform;
        }

        SpawnNewBall();
    }

    void Update()
    {
        // Track hand velocity for throwing
        if (handCenter != null)
        {
            Vector3 currentHandPos = handCenter.position;
            handVelocity = (currentHandPos - lastHandPosition) / Time.deltaTime;
            lastHandPosition = currentHandPos;
        }

        string gesture = GetCurrentGesture();

        if (gesture == "fist")
        {
            if (!isGrabbing)
            {
                TryGrabBall();
            }
        }
        else if (gesture == "open")
        {
            if (isGrabbing)
            {
                ThrowBall();
            }
        }

        // Keep grabbed ball attached to the hand
        if (isGrabbing && currentBall != null && handCenter != null)
        {
            currentBall.transform.position = handCenter.position;
        }
    }

    private string GetCurrentGesture()
    {
        GesturePacket packet;
        if (receiver != null && receiver.TryGetLatestPacket("player-1", out packet) && packet != null && !string.IsNullOrWhiteSpace(packet.gesture))
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
            catch { }
        }

        return "none";
    }

    private void TryGrabBall()
    {
        if (handCenter == null) return;

        // Find nearby colliders
        Collider[] colliders = Physics.OverlapSphere(handCenter.position, grabRadius);
        foreach (var col in colliders)
        {
            // Check if it's a ball
            if (col.CompareTag("Ball") || col.name.Contains("Ball") || col.name.Contains("Sphere"))
            {
                currentBall = col.gameObject;
                isGrabbing = true;
                
                // Disable physics while holding
                Rigidbody rb = currentBall.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                }
                break;
            }
        }
    }

    private void ThrowBall()
    {
        if (currentBall == null)
        {
            isGrabbing = false;
            return;
        }

        // Enable physics and apply force
        Rigidbody rb = currentBall.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            
            // Calculate throw direction: forward from camera or along hand velocity
            Vector3 throwDir = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
            if (handVelocity.magnitude > 1f)
            {
                throwDir = handVelocity.normalized;
            }

            float speed = Mathf.Max(handVelocity.magnitude * throwForceMultiplier, minThrowSpeed);
            rb.linearVelocity = throwDir * speed;
        }

        isGrabbing = false;
        currentBall = null;

        // Spawn a new ball after a short delay so the user can play again
        Invoke("SpawnNewBall", 2f);
    }

    private void SpawnNewBall()
    {
        // Don't spawn if there's already a ball waiting to be grabbed
        GameObject existingBall = GameObject.FindWithTag("Ball");
        if (existingBall != null && !isGrabbing) return;

        if (ballPrefab != null)
        {
            currentBall = Instantiate(ballPrefab, spawnPoint.position, Quaternion.identity);
        }
        else
        {
            // Create a default sphere with rigidbody and blue color
            currentBall = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            currentBall.name = "GameBall";
            currentBall.tag = "Ball";
            currentBall.transform.position = spawnPoint.position;
            currentBall.transform.localScale = Vector3.one * 0.5f;

            Rigidbody rb = currentBall.AddComponent<Rigidbody>();
            rb.mass = 0.5f;

            Renderer renderer = currentBall.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0f, 0.5f, 1f); // Vibrant blue
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (handCenter != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(handCenter.position, grabRadius);
        }
    }
}
