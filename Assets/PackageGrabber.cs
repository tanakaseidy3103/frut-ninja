using UnityEngine;

public class PackageGrabber : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Transform of the hand center to hold the package (e.g., Manager_Hand or wrist Point)")]
    public Transform handCenter;

    [Header("Spawn Settings")]
    [Tooltip("Spawn position for the packages (e.g. on top of the coffee table)")]
    public Vector3 spawnPosition = new Vector3(0f, 1.2f, 1.5f);
    
    [Header("Gameplay Settings")]
    public float grabRadius = 1.8f;
    public float throwForce = 12f;

    private GameObject currentPackage;
    private bool isHolding = false;
    private Vector3 lastHandPos;
    private Vector3 handVelocity;

    private UDPReceive legacyReceiver;
    private GestureUdpReceiver receiver;

    void Start()
    {
        legacyReceiver = FindFirstObjectByType<UDPReceive>();
        receiver = FindFirstObjectByType<GestureUdpReceiver>();

        // Try to automatically find the table in the room to place the spawn point
        GameObject table = GameObject.Find("table") ?? GameObject.Find("Table");
        if (table != null)
        {
            spawnPosition = table.transform.position + Vector3.up * 0.4f;
        }
        else
        {
            // Default position visible in front of the camera/couch
            spawnPosition = new Vector3(0f, 1.5f, 0f);
        }

        SetupHandCollider();
        SpawnNewPackage();
    }

    private void SetupHandCollider()
    {
        if (handCenter != null)
        {
            Rigidbody handRb = handCenter.GetComponent<Rigidbody>();
            if (handRb == null)
            {
                handRb = handCenter.gameObject.AddComponent<Rigidbody>();
            }
            handRb.isKinematic = true;
            handRb.useGravity = false;

            SphereCollider handCol = handCenter.GetComponent<SphereCollider>();
            if (handCol == null)
            {
                handCol = handCenter.gameObject.AddComponent<SphereCollider>();
            }
            handCol.isTrigger = false;
            handCol.radius = 1.0f; // Perfect physical size to touch the boxes
        }
    }

    void Update()
    {
        // Setup collider if handCenter was assigned late
        if (handCenter != null && handCenter.GetComponent<SphereCollider>() == null)
        {
            SetupHandCollider();
        }

        // Track hand velocity for the throw
        if (handCenter != null)
        {
            Vector3 currentHandPos = handCenter.position;
            handVelocity = (currentHandPos - lastHandPos) / Time.deltaTime;
            lastHandPos = currentHandPos;
        }

        string gesture = GetCurrentGesture();

        if (gesture == "fist")
        {
            if (!isHolding)
            {
                TryGrabPackage();
            }
        }
        else if (gesture == "open")
        {
            if (isHolding)
            {
                ThrowPackage();
            }
        }

        // Keep package locked to the hand while holding
        if (isHolding && currentPackage != null && handCenter != null)
        {
            currentPackage.transform.position = handCenter.position;
        }
    }

    private string GetCurrentGesture()
    {
        // Keyboard and Mouse fallbacks for instant response
        if (Input.GetKey(KeyCode.Alpha2) || Input.GetKey(KeyCode.E) || Input.GetMouseButton(0))
        {
            return "fist"; // Grab!
        }
        if (Input.GetKey(KeyCode.Alpha1) || Input.GetKey(KeyCode.Space) || Input.GetMouseButton(1))
        {
            return "open"; // Throw!
        }

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

    private void TryGrabPackage()
    {
        if (handCenter == null) return;

        Collider[] colliders = Physics.OverlapSphere(handCenter.position, grabRadius);
        foreach (var col in colliders)
        {
            if (col.name.Contains("DeliveryPackage") || col.name.Contains("Package") || col.name.Contains("Box"))
            {
                currentPackage = col.gameObject;
                isHolding = true;

                Rigidbody rb = currentPackage.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                }
                break;
            }
        }
    }

    private void ThrowPackage()
    {
        if (currentPackage == null)
        {
            isHolding = false;
            return;
        }

        // Rename it so that GameObject.Find("DeliveryPackage") won't block future spawns
        currentPackage.name = "ThrownPackage";

        Rigidbody rb = currentPackage.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;

            // Direct throw forward/upwards relative to the hand movement
            Vector3 throwDir = Camera.main != null ? (Camera.main.transform.forward + Vector3.up * 0.2f).normalized : Vector3.forward;
            if (handVelocity.magnitude > 1f)
            {
                throwDir = (handVelocity.normalized + Vector3.up * 0.3f).normalized;
            }

            float force = Mathf.Max(handVelocity.magnitude * throwForce, 8f);
            rb.linearVelocity = throwDir * force;
        }

        isHolding = false;
        currentPackage = null;

        // Spawn next package on the table after a delay
        Invoke("SpawnNewPackage", 1.5f);
    }

    private void SpawnNewPackage()
    {
        // Check if there are any active packages on the table
        GameObject existing = GameObject.Find("DeliveryPackage_Center") ?? 
                              GameObject.Find("DeliveryPackage_Left") ?? 
                              GameObject.Find("DeliveryPackage_Right");
        if (existing != null && !isHolding) return;

        // Dynamically place the packages right in front of the hand (very close)
        if (handCenter != null)
        {
            // Positioned extremely close and slightly lower
            spawnPosition = handCenter.position + Vector3.down * 0.1f + Vector3.forward * 0.1f;
        }

        Vector3 boxScale = new Vector3(2.4f, 1.8f, 2.4f); // Giant boxes!

        // Spawn Center Box
        SpawnBox("DeliveryPackage_Center", spawnPosition, boxScale);

        // Spawn Left Box
        SpawnBox("DeliveryPackage_Left", spawnPosition + Vector3.left * 2.8f, boxScale);

        // Spawn Right Box
        SpawnBox("DeliveryPackage_Right", spawnPosition + Vector3.right * 2.8f, boxScale);
    }

    private void SpawnBox(string boxName, Vector3 pos, Vector3 scale)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = boxName;
        box.transform.position = pos;
        box.transform.localScale = scale;

        Rigidbody rb = box.AddComponent<Rigidbody>();
        rb.mass = 1.0f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.isKinematic = true; // Keep it still! It will not fall or roll away until thrown

        // Color it cardboard brown
        Renderer renderer = box.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = new Color(0.6f, 0.45f, 0.3f); // Brown
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (handCenter != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(handCenter.position, grabRadius);
        }
    }
}
