using UnityEngine;

[DisallowMultipleComponent]
public class LogisticsGameController : MonoBehaviour
{
    void Start()
    {
        // Replace with clean TargetSphereGameController
        if (FindFirstObjectByType<TargetSphereGameController>() == null)
        {
            GameObject targetSystem = new GameObject("TargetSphereSystem");
            targetSystem.AddComponent<TargetSphereGameController>();
        }

        // Disable PackageGrabber if attached
        PackageGrabber grabber = FindFirstObjectByType<PackageGrabber>();
        if (grabber != null)
        {
            grabber.enabled = false;
        }
    }
}
