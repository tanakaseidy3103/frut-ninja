# Hand-Detection-3D

This project combines computer vision, MediaPipe hand tracking, Unity, and UDP networking to control a 3D character with hand gestures in real time. The repository now includes a complete pipeline for:

- capturing a hand from a webcam or mobile camera stream,
- detecting the 21 hand landmarks in Python,
- classifying gestures such as `open`, `fist`, `swipe_left`, `swipe_right`, and `swipe_up`,
- sending commands to Unity over UDP in JSON format,
- animating a 3D hand and driving a player character,
- scaling the setup to up to 4 players by assigning a unique `playerId` to each sender.

## Demo

[![Watch the video](https://github-production-user-asset-6210df.s3.amazonaws.com/75379150/270224152-4304f8eb-551a-40ce-9c06-b5696c73e1c6.PNG)](https://www.youtube.com/watch?v=Dl0FvKSwzv8)

## Architecture

The real-time loop is:

1. Python captures video from a webcam or network camera.
2. MediaPipe extracts 21 landmarks for the detected hand.
3. The gesture layer converts landmarks into gameplay commands.
4. A UDP packet is sent to Unity with `playerId`, `gesture`, `command`, and `landmarks`.
5. Unity updates the 3D hand avatar and character movement for the matching player.

### UDP Packet Format

```json
{
  "playerId": "player-1",
  "source": "webcam",
  "gesture": "open",
  "timestampMs": 1710000000000,
  "command": {
    "moveX": 0.42,
    "moveY": 0.0,
    "moveZ": 0.85,
    "jump": false,
    "attack": false,
    "confidence": 0.92
  },
  "landmarks": [
    { "x": 0.51, "y": 0.79, "z": -0.03 }
  ]
}
```

## Gesture Mapping

- `open`: enables walking input from hand position.
- `fist`: triggers attack.
- `swipe_up`: triggers jump.
- `swipe_left`: lateral move to the left.
- `swipe_right`: lateral move to the right.

The current Python implementation uses simple heuristic gesture recognition so that the system remains fast and easy to tune. This is enough for a playable prototype with low latency.

## New Files

- `python/gesture_sender.py`: OpenCV + MediaPipe sender.
- `python/requirements.txt`: Python dependencies.
- `Assets/GestureControl/GesturePacketModels.cs`: serializable packet classes.
- `Assets/GestureControl/GestureUdpReceiver.cs`: UDP listener for Unity.
- `Assets/GestureControl/GestureHandAvatar.cs`: maps landmarks to a 3D hand rig.
- `Assets/GestureControl/GestureCharacterMotor.cs`: applies movement, jump, and attack.
- `Assets/GestureControl/GesturePlayerRig.cs`: keeps a hand avatar and character motor on the same `playerId`.

## Getting Started

### Prerequisites

- Unity project opened and able to compile C# scripts.
- Python 3.10+.
- A webcam or a camera stream URL.

### Python Setup

Install the dependencies:

```bash
pip install -r python/requirements.txt
```

Run one sender:

```bash
python python/gesture_sender.py --host 127.0.0.1 --port 5052 --player-id player-1 --camera 0 --show
```

Examples:

```bash
python python/gesture_sender.py --player-id player-2 --camera 1 --show
python python/gesture_sender.py --player-id player-3 --camera http://192.168.0.15:8080/video --show
```

If each player uses a phone, run one sender per device or per video source, always with a unique `playerId`.

## Unity Setup

### 1. Add The Receiver

Create an empty GameObject such as `GestureNetwork` and add `GestureUdpReceiver`.

### 2. Add A Hand Avatar Per Player

For each player:

- create or duplicate a hand rig object,
- add `GestureHandAvatar`,
- assign the 21 transforms used as hand points,
- set the same `playerId` used in Python.

### 3. Add A Character Per Player

For each playable character:

- add `GestureCharacterMotor`,
- assign the shared `GestureUdpReceiver`,
- optionally assign `CharacterController`, `Rigidbody`, and `Animator`,
- set the same `playerId`.

### 4. Optional Rig Helper

If a hand and a character belong to the same player root object, add `GesturePlayerRig` and set the `playerId` once.

## Multiplayer Up To 4 Players

To support 4 players:

1. Create 4 player rigs in Unity.
2. Use `player-1`, `player-2`, `player-3`, and `player-4`.
3. Run one Python sender per camera source.
4. Point all senders to the same UDP host and port.

The Unity receiver stores the latest packet for each `playerId`, so multiple streams can share a single network listener.

## Low Latency Notes

- UDP is used because it avoids the overhead of a connection-oriented protocol.
- Gesture recognition uses lightweight heuristics instead of a heavier classifier.
- Smoothing is applied only on the Unity hand avatar, not on the command stream.
- For best results, use good lighting and keep the hand near the center of the frame.

## Existing Legacy Scripts

The original scripts in `Assets/` were kept intact. The new gesture-control pipeline is additive and can be integrated scene by scene without breaking the previous hand-tracking setup.

## Next Improvements

- Replace heuristic gesture recognition with a trained classifier.
- Add WebSocket transport for mobile browser controllers.
- Add a lobby and player auto-registration.
- Synchronize player state over Photon, Mirror, or Netcode for GameObjects if remote multiplayer is required.
