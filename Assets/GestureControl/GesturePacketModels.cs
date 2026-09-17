using System;

[Serializable]
public class GestureLandmark
{
    public float x;
    public float y;
    public float z;
}

[Serializable]
public class GestureCommand
{
    public float moveX;
    public float moveY;
    public float moveZ;
    public bool jump;
    public bool attack;
    public float confidence;
}

[Serializable]
public class GesturePacket
{
    public string playerId = "player-1";
    public string source = "webcam";
    public string gesture = "none";
    public long timestampMs;
    public GestureCommand command = new GestureCommand();
    public GestureLandmark[] landmarks = new GestureLandmark[0];
}