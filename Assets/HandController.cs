using System.Collections.Generic;
using UnityEngine;
using System;

public class HandController : MonoBehaviour
{
    public GameObject armature;
    public List<Transform> armatureList = new List<Transform>();

    public GameObject hand;
    public List<Transform> handList = new List<Transform>();

    public List<GameObject> points = new List<GameObject>();

    void Start()
    {
        if (armature != null) GetAllChilds(armature.transform, armatureList);
        if (hand != null) GetAllChilds(hand.transform, handList);

        if (armatureList.Count >= 12 && handList.Count >= 13)
        {
            for (int i = 0; i < 3; i++)
            {
                points.Add(armatureList[5 + i].gameObject);
                points.Add(handList[5 + i].gameObject);
                points.Add(handList[6 + i].gameObject);
            }

            points.Add(armatureList[11].gameObject);
            points.Add(handList[11].gameObject);
            points.Add(handList[12].gameObject);

            points.Add(armatureList[10].gameObject);
            points.Add(handList[10].gameObject);
            points.Add(handList[11].gameObject);

            points.Add(armatureList[9].gameObject);
            points.Add(handList[9].gameObject);
            points.Add(handList[10].gameObject);
        }
    }

    float getAngleX(GameObject point2, GameObject point1)
    {
        if (point1 == null || point2 == null) return 0f;
        double dx = point2.transform.position.x - point1.transform.position.x;
        double dy = point2.transform.position.y - point1.transform.position.y;
        double angleDegrees = Math.Atan2(dy, dx) * (180.0 / Math.PI) - 270.0;
        return (float)((angleDegrees + 360.0) % 360.0);
    }

    float getAngleZ(GameObject point2, GameObject point1)
    {
        if (point1 == null || point2 == null) return 0f;
        double dx = point2.transform.position.y - point1.transform.position.y;
        double dy = point2.transform.position.z - point1.transform.position.z;
        double angleDegrees = Math.Atan2(dy, dx) * (180.0 / Math.PI) - 180.0;
        return (float)-((angleDegrees + 360.0) % 360.0);
    }

    void Update()
    {
        if (points == null || points.Count < 18) return;

        Vector3 temp;

        // Finger 1 (bend forward)
        for (int i = 0; i < 3; i++)
        {
            if (points[i * 3] != null && points[i * 3 + 1] != null && points[i * 3 + 2] != null)
            {
                temp = new Vector3(
                    -getAngleX(points[i * 3 + 1], points[i * 3 + 2]),
                    points[i * 3].transform.localEulerAngles.y,
                    -getAngleZ(points[i * 3 + 1], points[i * 3 + 2])
                );
                points[i * 3].transform.localEulerAngles = temp;
            }
        }

        // Finger 2
        if (points[9] != null && points[10] != null && points[11] != null)
        {
            temp = new Vector3(
                getAngleX(points[10], points[11]),
                points[9].transform.localEulerAngles.y,
                getAngleZ(points[10], points[11])
            );
            points[9].transform.localEulerAngles = temp;
        }

        if (points[12] != null && points[13] != null && points[14] != null)
        {
            temp = new Vector3(
                getAngleX(points[13], points[14]),
                points[12].transform.localEulerAngles.y,
                getAngleZ(points[13], points[14])
            );
            points[12].transform.localEulerAngles = temp;
        }

        if (points[15] != null && points[16] != null && points[17] != null)
        {
            temp = new Vector3(
                getAngleX(points[16], points[17]),
                points[15].transform.localEulerAngles.y,
                getAngleZ(points[16], points[17])
            );
            points[15].transform.localEulerAngles = temp;
        }
    }

    void GetAllChilds(Transform parent, List<Transform> Mylist)
    {
        foreach (Transform child in parent)
        {
            Mylist.Add(child);
            GetAllChilds(child, Mylist);
        }
    }
}
