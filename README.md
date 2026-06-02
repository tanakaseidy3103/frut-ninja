# Hand-Detection-3D

## English

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

## 日本語

このプロジェクトは、MediaPipe による手のトラッキング、Unity、UDP 通信を組み合わせて、手のジェスチャーで 3D キャラクターをリアルタイム操作するシステムです。現在のパイプラインでは、次のことができます。

- Webカメラまたはスマホのカメラ映像から手を取得する
- Python で 21 個の手のランドマークを検出する
- `open`、`fist`、`swipe_left`、`swipe_right`、`swipe_up` などのジェスチャーを判定する
- JSON 形式の UDP パケットで Unity にコマンドを送る
- 3D の手モデルとプレイヤーキャラクターを動かす
- `playerId` を分けることで最大 4 人まで対応する

### アーキテクチャ

処理の流れは次の通りです。

1. Python が Webカメラまたはネットワークカメラから映像を取得する。
2. MediaPipe が手の 21 個のランドマークを抽出する。
3. ジェスチャー判定層がランドマークをゲーム用コマンドに変換する。
4. `playerId`、`gesture`、`command`、`landmarks` を含む UDP パケットを Unity に送信する。
5. Unity が対応するプレイヤーの 3D 手モデルとキャラクターを更新する。

### UDP パケット形式

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

### ジェスチャー割り当て

- `open`: 手の位置から移動入力を有効にする
- `fist`: 攻撃を発動する
- `swipe_up`: ジャンプを発動する
- `swipe_left`: 左方向へ移動する
- `swipe_right`: 右方向へ移動する

### 使い方

#### 必要環境

- Unity プロジェクトが開けて、C# スクリプトをコンパイルできること
- Python 3.10 以上
- Webカメラ、またはカメラ配信 URL

#### Python のセットアップ

依存関係をインストールします。

```bash
pip install -r python/requirements.txt
```

送信プログラムを起動します。

```bash
python python/gesture_sender.py --host 127.0.0.1 --port 5052 --player-id player-1 --camera 0 --show
```

複数例:

```bash
python python/gesture_sender.py --player-id player-2 --camera 1 --show
python python/gesture_sender.py --player-id player-3 --camera http://192.168.0.15:8080/video --show
```

#### Unity のセットアップ

1. `GestureNetwork` などの空の GameObject を作成して、`GestureUdpReceiver` を追加する。
2. プレイヤーごとに手のリグを作成または複製し、`GestureHandAvatar` を追加する。
3. 21 個の手のポイントに対応する Transform を割り当て、Python 側と同じ `playerId` を設定する。
4. 各キャラクターに `GestureCharacterMotor` を追加し、`GestureUdpReceiver` を割り当てる。
5. 同じプレイヤーの手とキャラクターをまとめる場合は、`GesturePlayerRig` を使うと `playerId` を一度だけ設定できる。

### 最大 4 人までのマルチプレイ

1. Unity で 4 つのプレイヤーリグを作る。
2. `player-1`、`player-2`、`player-3`、`player-4` を使う。
3. カメラソースごとに Python 送信プログラムを 1 つずつ起動する。
4. すべて同じ UDP ホストとポートに送信する。

Unity の受信側は `playerId` ごとに最新パケットを保存するので、1 つの受信口で複数ストリームを扱えます。

### 低遅延のポイント

- UDP を使うことで接続管理のオーバーヘッドを避ける
- ジェスチャー認識は軽量なヒューリスティックで実装する
- 平滑化は Unity 側の手モデルにだけ適用する
- よい結果を得るには、十分な明るさと手を画面中央付近に保つことが重要

### 既存スクリプトについて

`Assets/` にある元のスクリプトはそのまま残しています。新しいジェスチャー制御パイプラインは追加方式なので、既存の手トラッキング構成を壊さずにシーン単位で導入できます。

### 今後の改善

- ヒューリスティックなジェスチャー認識を学習済み分類器に置き換える
- モバイルブラウザ向けに WebSocket 送信を追加する
- ロビーとプレイヤー自動登録を追加する
- リモートマルチプレイが必要な場合は Photon、Mirror、または Netcode for GameObjects でプレイヤー状態を同期する
