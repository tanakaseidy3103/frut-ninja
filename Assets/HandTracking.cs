using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;


public class HandTracking : MonoBehaviour
{
    // Start is called before the first frame update
    public UDPReceive udpReceive;
    public GameObject[] handPoints;
    public bool useJsonLandmarks = true;
    public Vector3 jsonDeltaScale = new Vector3(6f, 6f, 6f);
    public Vector3 jsonPositionOffset = Vector3.zero;
    public bool mirrorX = true;
    public bool invertY = true;
    public bool invertZ = true;
    public bool calibrateFromFirstPacket = true;
    public bool printParseErrors = false;

    private Vector3[] initialLocalPositions;
    private GestureLandmark[] jsonReferenceLandmarks;
    private bool hasJsonReference;
    void Start()
    {
        timeLeft = updateInterval;
        CacheInitialPointPositions();
        
    }

    // Update is called once per frame
    
    public float updateInterval = 0.5f; // The interval at which to update the FPS display
    private float accum = 0f; // FPS accumulated over the interval
    private int frames = 0; // Frames drawn over the interval
    private float timeLeft; // Time left for current interval
    public float shift = 7 ;

    void Update()
    {
        if (udpReceive == null)
        {
            return;
        }

        string data = udpReceive.data;
        if (string.IsNullOrWhiteSpace(data))
        {
            return;
        }

        try
        {
            if (useJsonLandmarks && TryApplyJsonPacket(data))
            {
                return;
            }

            TryApplyLegacyPacket(data);
        }
        catch (Exception err)
        {
            if (printParseErrors)
            {
                Debug.LogWarning($"HandTracking parse error: {err.Message}");
            }
        }


    }

    private bool TryApplyJsonPacket(string data)
    {
        if (!data.StartsWith("{"))
        {
            return false;
        }

        GesturePacket packet = JsonUtility.FromJson<GesturePacket>(data);
        if (packet == null)
        {
            return true;
        }

        if (packet.landmarks == null || packet.landmarks.Length == 0)
        {
            return true;
        }

        if (initialLocalPositions == null || initialLocalPositions.Length != handPoints.Length)
        {
            CacheInitialPointPositions();
        }

        if (calibrateFromFirstPacket && !hasJsonReference)
        {
            CaptureJsonReference(packet.landmarks);
            return true;
        }

        int pointCount = Mathf.Min(21, Mathf.Min(handPoints.Length, packet.landmarks.Length));
        for (int i = 0; i < pointCount; i++)
        {
            if (handPoints[i] == null)
            {
                continue;
            }

            GestureLandmark landmark = packet.landmarks[i];
            float dx = landmark.x;
            float dy = landmark.y;
            float dz = landmark.z;

            if (hasJsonReference && i < jsonReferenceLandmarks.Length && jsonReferenceLandmarks[i] != null)
            {
                dx -= jsonReferenceLandmarks[i].x;
                dy -= jsonReferenceLandmarks[i].y;
                dz -= jsonReferenceLandmarks[i].z;
            }

            if (mirrorX)
            {
                dx = -dx;
            }

            if (invertY)
            {
                dy = -dy;
            }

            if (invertZ)
            {
                dz = -dz;
            }

            float x = initialLocalPositions[i].x + (dx * jsonDeltaScale.x) + jsonPositionOffset.x;
            float y = initialLocalPositions[i].y + (dy * jsonDeltaScale.y) + jsonPositionOffset.y;
            float z = initialLocalPositions[i].z + (dz * jsonDeltaScale.z) + jsonPositionOffset.z;

            handPoints[i].transform.localPosition = new Vector3(x, y, z);
        }

        return true;
    }

    private void TryApplyLegacyPacket(string data)
    {
        if (data.Length < 2)
        {
            return;
        }

        data = data.Remove(0, 1);
        data = data.Remove(data.Length - 1, 1);
        string[] points = data.Split(',');

        int pointCount = Mathf.Min(21, handPoints.Length);
        for (int i = 0; i < pointCount; i++)
        {
            if (handPoints[i] == null)
            {
                continue;
            }

            float x = 32.83f - float.Parse(points[i * 3]) / 100;
            float y = float.Parse(points[i * 3 + 1]) / 100;
            float z = float.Parse(points[i * 3 + 2]) / 100;

            handPoints[i].transform.localPosition = new Vector3(x, y, z);
        }
    }

    private void CacheInitialPointPositions()
    {
        if (handPoints == null)
        {
            initialLocalPositions = new Vector3[0];
            return;
        }

        initialLocalPositions = new Vector3[handPoints.Length];
        for (int i = 0; i < handPoints.Length; i++)
        {
            if (handPoints[i] != null)
            {
                initialLocalPositions[i] = handPoints[i].transform.localPosition;
            }
        }
    }

    private void CaptureJsonReference(GestureLandmark[] landmarks)
    {
        int pointCount = Mathf.Min(21, landmarks.Length);
        jsonReferenceLandmarks = new GestureLandmark[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            GestureLandmark source = landmarks[i];
            jsonReferenceLandmarks[i] = new GestureLandmark
            {
                x = source.x,
                y = source.y,
                z = source.z
            };
        }

        hasJsonReference = true;
    }
}