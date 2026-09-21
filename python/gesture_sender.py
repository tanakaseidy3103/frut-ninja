import argparse
import json
import socket
import time
from collections import deque

import cv2
import mediapipe as mp


class GestureTracker:
    def __init__(self, swipe_threshold=0.12, history_size=6):
        self.swipe_threshold = swipe_threshold
        self.wrist_history = deque(maxlen=history_size)
        self.last_swipe_name = "none"
        self.last_swipe_time = 0.0

    def classify(self, landmarks, handedness):
        if not landmarks:
            return "none", self._empty_command()

        finger_states = self._finger_states(landmarks, handedness)
        extended_count = sum(1 for is_extended in finger_states if is_extended)
        wrist = landmarks[0]
        palm_center_x = sum(point[0] for point in landmarks[:9]) / min(len(landmarks), 9)
        palm_center_y = sum(point[1] for point in landmarks[:9]) / min(len(landmarks), 9)

        gesture = "none"
        attack = False
        jump = False

        swipe = self._detect_swipe(wrist)
        if swipe != "none":
            gesture = swipe
            jump = swipe == "swipe_up"
        elif extended_count >= 4:
            gesture = "open"
        elif extended_count <= 1:
            gesture = "fist"
            attack = True

        move_x = self._apply_dead_zone((palm_center_x - 0.5) * 2.2)
        move_z = self._apply_dead_zone((0.62 - palm_center_y) * 2.8)

        if gesture == "swipe_left":
            move_x = -1.0
        elif gesture == "swipe_right":
            move_x = 1.0

        if gesture == "fist":
            move_x = 0.0
            move_z = 0.0

        command = {
            "moveX": max(-1.0, min(1.0, move_x)),
            "moveY": 0.0,
            "moveZ": max(-1.0, min(1.0, move_z)),
            "jump": jump,
            "attack": attack,
            "confidence": min(1.0, extended_count / 5.0 if gesture == "open" else 1.0),
        }
        return gesture, command

    def _finger_states(self, landmarks, handedness):
        thumb_tip = landmarks[4]
        thumb_ip = landmarks[3]
        if handedness == "Right":
            thumb_extended = thumb_tip[0] < thumb_ip[0]
        else:
            thumb_extended = thumb_tip[0] > thumb_ip[0]

        finger_pairs = ((8, 6), (12, 10), (16, 14), (20, 18))
        states = [thumb_extended]
        for tip_idx, pip_idx in finger_pairs:
            states.append(landmarks[tip_idx][1] < landmarks[pip_idx][1])
        return states

    def _detect_swipe(self, wrist):
        self.wrist_history.append(wrist)
        if len(self.wrist_history) < self.wrist_history.maxlen:
            return "none"

        start_x, start_y, _ = self.wrist_history[0]
        end_x, end_y, _ = self.wrist_history[-1]
        delta_x = end_x - start_x
        delta_y = end_y - start_y

        now = time.time()
        if now - self.last_swipe_time < 0.35:
            return "none"

        swipe_name = "none"
        if abs(delta_x) > self.swipe_threshold and abs(delta_x) > abs(delta_y) * 1.2:
            swipe_name = "swipe_right" if delta_x > 0 else "swipe_left"
        elif -delta_y > self.swipe_threshold:
            swipe_name = "swipe_up"

        if swipe_name != "none":
            self.last_swipe_name = swipe_name
            self.last_swipe_time = now

        return swipe_name

    @staticmethod
    def _apply_dead_zone(value, dead_zone=0.18):
        if abs(value) < dead_zone:
            return 0.0
        return value

    @staticmethod
    def _empty_command():
        return {
            "moveX": 0.0,
            "moveY": 0.0,
            "moveZ": 0.0,
            "jump": False,
            "attack": False,
            "confidence": 0.0,
        }


def parse_camera_source(value):
    return int(value) if value.isdigit() else value


def open_camera(source):
    if isinstance(source, int):
        capture = cv2.VideoCapture(source, cv2.CAP_DSHOW)
        if capture.isOpened():
            return capture
        capture.release()

    return cv2.VideoCapture(source)


def build_packet(player_id, source_name, gesture, command, landmarks):
    return {
        "playerId": player_id,
        "source": source_name,
        "gesture": gesture,
        "timestampMs": int(time.time() * 1000),
        "command": command,
        "landmarks": [{"x": x, "y": y, "z": z} for x, y, z in landmarks],
    }


def draw_debug(frame, landmarks, gesture, command, fps):
    height, width = frame.shape[:2]
    for x, y, _ in landmarks:
        cv2.circle(frame, (int(x * width), int(y * height)), 4, (0, 255, 0), -1)

    cv2.putText(frame, f"gesture: {gesture}", (16, 28), cv2.FONT_HERSHEY_SIMPLEX, 0.75, (0, 255, 255), 2)
    cv2.putText(frame, f"moveX: {command['moveX']:.2f}", (16, 58), cv2.FONT_HERSHEY_SIMPLEX, 0.65, (255, 255, 255), 2)
    cv2.putText(frame, f"moveZ: {command['moveZ']:.2f}", (16, 86), cv2.FONT_HERSHEY_SIMPLEX, 0.65, (255, 255, 255), 2)
    cv2.putText(frame, f"fps: {fps:.1f}", (16, 114), cv2.FONT_HERSHEY_SIMPLEX, 0.65, (255, 255, 255), 2)


def main():
    parser = argparse.ArgumentParser(description="MediaPipe hand tracking sender for Unity over UDP.")
    parser.add_argument("--host", default="127.0.0.1", help="Unity host or IP address")
    parser.add_argument("--port", type=int, default=5052, help="UDP port used by Unity")
    parser.add_argument("--player-id", default="player-1", help="Unique player id, e.g. player-1")
    parser.add_argument("--camera", default="0", help="OpenCV camera index or network camera URL")
    parser.add_argument("--camera-name", default="webcam", help="Source label stored in the packet")
    parser.add_argument("--show", action="store_true", help="Show the OpenCV debug window")
    parser.add_argument("--max-fps", type=int, default=45, help="Limit max send rate to save CPU and avoid network flood")
    args = parser.parse_args()

    capture = open_camera(parse_camera_source(args.camera))
    if not capture.isOpened():
        raise RuntimeError(f"Could not open camera source: {args.camera}")

    # Set camera resolution to optimal 640x480 for ultra fast processing
    capture.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
    capture.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)

    if args.show:
        cv2.namedWindow("Gesture Sender", cv2.WINDOW_NORMAL)
        cv2.waitKey(1)

    tracker = GestureTracker()
    socket_client = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

    mp_hands = mp.solutions.hands
    mp_draw = mp.solutions.drawing_utils

    min_frame_interval = 1.0 / max(10, args.max_fps)
    last_send_time = 0.0
    prev_frame_time = time.time()

    with mp_hands.Hands(
        static_image_mode=False,
        model_complexity=0,  # Model 0 is ultra lightweight and fast
        max_num_hands=1,
        min_detection_confidence=0.6,
        min_tracking_confidence=0.5,
    ) as hands:
        try:
            while True:
                current_time = time.time()
                elapsed = current_time - last_send_time
                if elapsed < min_frame_interval:
                    time.sleep(max(0.001, min_frame_interval - elapsed))

                has_frame, frame = capture.read()
                if not has_frame:
                    break

                frame = cv2.flip(frame, 1)
                rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
                result = hands.process(rgb_frame)

                landmarks = []
                gesture = "none"
                command = tracker._empty_command()

                if result.multi_hand_landmarks:
                    hand_landmarks = result.multi_hand_landmarks[0]
                    handedness = result.multi_handedness[0].classification[0].label if result.multi_handedness else "Right"
                    landmarks = [(lm.x, lm.y, lm.z) for lm in hand_landmarks.landmark]
                    gesture, command = tracker.classify(landmarks, handedness)

                    if args.show:
                        mp_draw.draw_landmarks(frame, hand_landmarks, mp_hands.HAND_CONNECTIONS)

                # Send packet to Unity
                packet = build_packet(args.player_id, args.camera_name, gesture, command, landmarks)
                socket_client.sendto(json.dumps(packet).encode("utf-8"), (args.host, args.port))
                last_send_time = time.time()

                fps = 1.0 / max(last_send_time - prev_frame_time, 1e-6)
                prev_frame_time = last_send_time

                if args.show:
                    draw_debug(frame, landmarks, gesture, command, fps)
                    cv2.imshow("Gesture Sender", frame)
                    key = cv2.waitKey(1) & 0xFF
                    if key in (27, ord("q")):
                        break
        finally:
            pass

    capture.release()
    socket_client.close()
    cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
