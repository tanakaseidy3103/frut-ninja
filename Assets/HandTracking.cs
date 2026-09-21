using System.Collections.Generic;
using UnityEngine;
using System;

public class HandTracking : MonoBehaviour
{
    public UDPReceive udpReceive;
    public GameObject[] handPoints;
    public bool useJsonLandmarks = true;
    public Vector3 jsonDeltaScale = new Vector3(6f, 6f, 6f);
    public Vector3 jsonPositionOffset = Vector3.zero;
    public bool mirrorX = true;
    public bool invertY = true;
    public bool invertZ = true;
    public bool calibrateFromFirstPacket = true;
    [Range(0.01f, 1f)]
    public float smoothingFactor = 0.35f;
    public bool printParseErrors = false;

    private Vector3[] initialLocalPositions;
    private Vector3[] targetPositions;
    private GestureLandmark[] jsonReferenceLandmarks;
    private bool hasJsonReference;
    private string lastProcessedData = "";

    void Start()
    {
        if (udpReceive == null)
        {
            udpReceive = FindFirstObjectByType<UDPReceive>();
        }
        CacheInitialPointPositions();
    }

    void Update()
    {
        if (udpReceive == null) return;

        string data = udpReceive.data;
        if (!string.IsNullOrWhiteSpace(data) && data != lastProcessedData)
        {
            lastProcessedData = data;
            try
            {
                if (data.StartsWith("{"))
                {
                    TryApplyJsonPacket(data);
                }
                else
                {
                    TryApplyLegacyPacket(data);
                }
            }
            catch (Exception err)
            {
                if (printParseErrors)
                {
                    Debug.LogWarning("HandTracking parse error: " + err.Message);
                }
            }
        }

        // Smoothly interpolate towards target positions
        if (targetPositions != null && handPoints != null)
        {
            int count = Mathf.Min(handPoints.Length, targetPositions.Length);
            for (int i = 0; i < count; i++)
            {
                if (handPoints[i] != null)
                {
                    handPoints[i].transform.localPosition = Vector3.Lerp(handPoints[i].transform.localPosition, targetPositions[i], smoothingFactor);
                }
            }
        }
    }

    private bool TryApplyJsonPacket(string data)
    {
        GesturePacket packet = JsonUtility.FromJson<GesturePacket>(data);
        if (packet == null || packet.landmarks == null || packet.landmarks.Length == 0)
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
            if (handPoints[i] == null) continue;

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

            if (mirrorX) dx = -dx;
            if (invertY) dy = -dy;
            if (invertZ) dz = -dz;

            float x = initialLocalPositions[i].x + (dx * jsonDeltaScale.x) + jsonPositionOffset.x;
            float y = initialLocalPositions[i].y + (dy * jsonDeltaScale.y) + jsonPositionOffset.y;
            float z = initialLocalPositions[i].z + (dz * jsonDeltaScale.z) + jsonPositionOffset.z;

            targetPositions[i] = new Vector3(x, y, z);
        }

        return true;
    }

    private void TryApplyLegacyPacket(string data)
    {
        if (data.Length < 2) return;

        data = data.Substring(1, data.Length - 2);
        string[] points = data.Split(',');

        int pointCount = Mathf.Min(21, handPoints.Length);
        for (int i = 0; i < pointCount; i++)
        {
            if (handPoints[i] == null || (i * 3 + 2) >= points.Length) continue;

            float x = 32.83f - float.Parse(points[i * 3]) / 100f;
            float y = float.Parse(points[i * 3 + 1]) / 100f;
            float z = float.Parse(points[i * 3 + 2]) / 100f;

            targetPositions[i] = new Vector3(x, y, z);
        }
    }

    private void CacheInitialPointPositions()
    {
        if (handPoints == null || handPoints.Length == 0)
        {
            List<GameObject> children = new List<GameObject>();
            foreach (Transform child in transform)
            {
                children.Add(child.gameObject);
            }
            handPoints = children.ToArray();
        }

        if (handPoints == null)
        {
            initialLocalPositions = new Vector3[0];
            targetPositions = new Vector3[0];
            return;
        }

        initialLocalPositions = new Vector3[handPoints.Length];
        targetPositions = new Vector3[handPoints.Length];
        for (int i = 0; i < handPoints.Length; i++)
        {
            if (handPoints[i] != null)
            {
                initialLocalPositions[i] = handPoints[i].transform.localPosition;
                targetPositions[i] = initialLocalPositions[i];
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