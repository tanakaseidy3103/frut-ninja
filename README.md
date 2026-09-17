# Hand-Detection-3D

| Start Menu | Live Hand Tracking Gameplay |
| :---: | :---: |
| ![Start Menu](img/start_screen.png) | ![Live Hand Tracking](img/gameplay.png) |

## English

This project combines computer vision, MediaPipe hand tracking, Unity, and UDP networking to control a 3D character and play interactive logistics games in real time. The repository includes:

- Real-time 3D hand tracking from a webcam using Python (OpenCV + MediaPipe).
- Low-latency UDP networking sending JSON packets with coordinates and gestures.
- **Logistics Infinite Game**: An interactive warehouse minigame where players grab cardboard boxes (Fist gesture / E key / Left click) and throw them (Open gesture / Space key / Right click) into target shipping containers (Tokyo/Red vs London/Blue).
- **Physical Hand Colliders**: Kinematic rigidbodies and sphere colliders prevent hand joints from clipping through cardboard boxes, allowing you to physically push objects.
- **Joint Movement Smoothing**: Uses Lerp filters on the 3D hand rig to eliminate high-frequency webcam jitter.
- Multi-player scaling supporting up to 4 players by assigning a unique `playerId`.

---

## 日本語

このプロジェクトは、MediaPipeによるリアルタイム手の3Dトラッキング（Python）、Unity、およびUDPネットワーク通信を組み合わせた、次世代の非接触型物流シミュレーション・ゲームシステムです。

- **物流無限仕分けゲーム (Logistics Infinite Game)**: カメラの前に次々と出現する配送用の「ダンボール箱」を手のジェスチャー（Fist / キーボードE / マウスクリック）で掴み、配送先（東京行/赤、ロンドン行/青）のコンテナに向けて物理アプローチ（Open / スペースキー / 右クリック）で投げ入れる仕分けゲーム。
- **物理コライダーの自動生成**: 3Dの手が段ボール箱オブジェクトを「すり抜ける」のを防ぐため、手の中央に動的物理コライダーを実装。箱を物理的に手で押す・触るアクションが可能。
- **Lerpによる動きの平滑化**: カメラ特有の微細なブレ（ジッター）を極限まで排除する位置補間フィルターを実装し、スムーズで精密な操作性を実現。
- 最大4人までのマルチプレイヤー拡張性。

---

## Demo (Video)

[![Watch the video](https://github-production-user-asset-6210df.s3.amazonaws.com/75379150/270224152-4304f8eb-551a-40ce-9c06-b5696c73e1c6.PNG)](https://www.youtube.com/watch?v=Dl0FvKSwzv8)

---

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

---

## Gesture & Controls Mapping

### Camera Gestures
- `open`: Release or throw held cardboard box / enable character movement.
- `fist`: Grab the nearest cardboard box / trigger attack.
- `swipe_up`: Trigger jump.
- `swipe_left`: Move left.
- `swipe_right`: Move right.

### Hybrid Keyboard & Mouse Fallbacks
- **Grab Box**: Press `E` or `2` key, or **Left Mouse Click**.
- **Throw Box**: Press `Space` or `1` key, or **Right Mouse Click**.

---

## Getting Started

### Prerequisites

- Unity 6 LTS (6000.3.11f1) or compatible.
- Python 3.10+.
- A standard USB webcam.

### Python Setup

Install the dependencies:

```bash
pip install -r python/requirements.txt
```

Run the gesture sender program:

```bash
python python/gesture_sender.py --host 127.0.0.1 --port 5052 --player-id player-1 --camera 0 --show
```

---

## Unity Setup (Logistics Game)

1. Open `Assets/Scenes/Final Scene.unity`.
2. Ensure the `GameController` object has the `LogisticsGameController` and `PackageGrabber` components attached.
3. Make sure the Python sender is running.
4. Press **PLAY** in the Unity Editor.
5. Control the hand avatar using your camera and sort the cardboard boxes!

---

## Low Latency & Hand Smoothing

- **UDP Transport**: Low overhead socket communication avoids game thread blocking.
- **Lerp Filter**: Configurable `smoothingFactor` (default `0.25`) on the `HandTracking` component controls responsiveness vs stabilization.
- **Physics Calibration**: Box dimensions and collision parameters have been scaled up for robust gameplay.
