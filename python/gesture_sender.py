import argparse
import json
import socket
import time
from collections import deque

import cv2
import mediapipe as mp


class LedController:
    def __init__(self, pin=None, threshold=0.12, debounce_frames=3):
        self.pin = pin
        self.threshold = threshold
        self.debounce_frames = debounce_frames
        self.led_state = False
        self.near_count = 0
        self.far_count = 0
        self.gpio = None

        if pin is not None:
            try:
                import RPi.GPIO as GPIO  # type: ignore

                GPIO.setmode(GPIO.BCM)
                GPIO.setup(pin, GPIO.OUT)
                GPIO.output(pin, GPIO.LOW)
                self.gpio = GPIO
                print(f"[INFO] LED GPIO inicializado no pino BCM {pin}")
            except Exception as exc:
                print(f"[AVISO] Falha ao inicializar GPIO: {exc}")
                print("[AVISO] Continuando sem controle fisico de LED.")

    @staticmethod
    def _hand_area_ratio(landmarks):
        if not landmarks:
            return 0.0
        xs = [point[0] for point in landmarks]
        ys = [point[1] for point in landmarks]
        return max(max(xs) - min(xs), 0.0) * max(max(ys) - min(ys), 0.0)

    def _set_led(self, state):
        if state == self.led_state:
            return
        self.led_state = state
        if self.gpio is not None and self.pin is not None:
            self.gpio.output(self.pin, self.gpio.HIGH if state else self.gpio.LOW)

    def update(self, landmarks):
        area_ratio = self._hand_area_ratio(landmarks)
        is_near = bool(landmarks) and (area_ratio >= self.threshold)

        if is_near:
            self.near_count += 1
            self.far_count = 0
        else:
            self.far_count += 1
            self.near_count = 0

        if self.near_count >= self.debounce_frames and self.pin is not None:
            self._set_led(True)
        elif self.far_count >= self.debounce_frames and self.pin is not None:
            self._set_led(False)

        return is_near, area_ratio

    def cleanup(self):
        if self.gpio is not None and self.pin is not None:
            self.gpio.output(self.pin, self.gpio.LOW)
            self.gpio.cleanup()


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


def build_packet(player_id, source_name, gesture, command, landmarks):
    return {
        "playerId": player_id,
        "source": source_name,
        "gesture": gesture,
        "timestampMs": int(time.time() * 1000),
        "command": command,
        "landmarks": [{"x": x, "y": y, "z": z} for x, y, z in landmarks],
    }


def build_led_packet(gesture, is_near, area_ratio):
    return {
        "type": "led_relay",
        "gesture": gesture,
        "isNear": bool(is_near),
        "areaRatio": float(area_ratio),
        "timestampMs": int(time.time() * 1000),
    }


def draw_debug(frame, landmarks, gesture, command, fps):
    height, width = frame.shape[:2]
    for x, y, _ in landmarks:
        cv2.circle(frame, (int(x * width), int(y * height)), 4, (0, 255, 0), -1)

    cv2.putText(frame, f"gesture: {gesture}", (16, 28), cv2.FONT_HERSHEY_SIMPLEX, 0.75, (0, 255, 255), 2)
    cv2.putText(frame, f"moveX: {command['moveX']:.2f}", (16, 58), cv2.FONT_HERSHEY_SIMPLEX, 0.65, (255, 255, 255), 2)
    cv2.putText(frame, f"moveZ: {command['moveZ']:.2f}", (16, 86), cv2.FONT_HERSHEY_SIMPLEX, 0.65, (255, 255, 255), 2)
    cv2.putText(frame, f"jump: {command['jump']} attack: {command['attack']}", (16, 114), cv2.FONT_HERSHEY_SIMPLEX, 0.65, (255, 255, 255), 2)
    cv2.putText(frame, f"fps: {fps:.1f}", (16, 142), cv2.FONT_HERSHEY_SIMPLEX, 0.65, (255, 255, 255), 2)


def draw_led_debug(frame, is_near, area_ratio, threshold, led_on):
    cv2.putText(
        frame,
        f"near: {is_near} area: {area_ratio:.3f} thr: {threshold:.3f}",
        (16, 170),
        cv2.FONT_HERSHEY_SIMPLEX,
        0.6,
        (0, 255, 0) if is_near else (0, 0, 255),
        2,
    )
    cv2.putText(
        frame,
        f"LED: {'ON' if led_on else 'OFF'}",
        (16, 198),
        cv2.FONT_HERSHEY_SIMPLEX,
        0.6,
        (0, 255, 255),
        2,
    )


def main():
    parser = argparse.ArgumentParser(description="MediaPipe hand tracking sender for Unity over UDP.")
    parser.add_argument("--host", default="127.0.0.1", help="Unity host or IP address")
    parser.add_argument("--port", type=int, default=5052, help="UDP port used by Unity")
    parser.add_argument("--player-id", default="player-1", help="Unique player id, e.g. player-1")
    parser.add_argument("--camera", default="0", help="OpenCV camera index or network camera URL")
    parser.add_argument("--camera-name", default="webcam", help="Source label stored in the packet")
    parser.add_argument("--show", action="store_true", help="Show the OpenCV debug window")
    parser.add_argument("--led-pin", type=int, default=None, help="Pino BCM do LED no Raspberry (opcional)")
    parser.add_argument(
        "--near-threshold",
        type=float,
        default=0.12,
        help="Area minima da mao para considerar 'perto' e ligar LED",
    )
    parser.add_argument("--relay-host", default=None, help="IP opcional para relay de LED por UDP")
    parser.add_argument("--relay-port", type=int, default=5053, help="Porta UDP do relay de LED")
    args = parser.parse_args()

    capture = cv2.VideoCapture(parse_camera_source(args.camera))
    if not capture.isOpened():
        raise RuntimeError(f"Could not open camera source: {args.camera}")

    tracker = GestureTracker()
    led_controller = LedController(pin=args.led_pin, threshold=args.near_threshold)
    socket_client = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

    mp_hands = mp.solutions.hands
    mp_draw = mp.solutions.drawing_utils

    prev_frame_time = time.time()

    with mp_hands.Hands(
        static_image_mode=False,
        model_complexity=1,
        max_num_hands=1,
        min_detection_confidence=0.6,
        min_tracking_confidence=0.5,
    ) as hands:
        try:
            while True:
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

                is_near, area_ratio = led_controller.update(landmarks)

                packet = build_packet(args.player_id, args.camera_name, gesture, command, landmarks)
                socket_client.sendto(json.dumps(packet).encode("utf-8"), (args.host, args.port))

                if args.relay_host:
                    relay_packet = build_led_packet(gesture, is_near, area_ratio)
                    socket_client.sendto(
                        json.dumps(relay_packet).encode("utf-8"),
                        (args.relay_host, args.relay_port),
                    )

                current_time = time.time()
                fps = 1.0 / max(current_time - prev_frame_time, 1e-6)
                prev_frame_time = current_time

                if args.show:
                    draw_debug(frame, landmarks, gesture, command, fps)
                    if args.led_pin is not None:
                        draw_led_debug(frame, is_near, area_ratio, args.near_threshold, led_controller.led_state)
                    cv2.imshow("Gesture Sender", frame)
                    key = cv2.waitKey(1) & 0xFF
                    if key in (27, ord("q")):
                        break
        finally:
            led_controller.cleanup()

    capture.release()
    socket_client.close()
    cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
