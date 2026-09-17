"""
Controle de LED por gesto de mao (proximidade na camera).

Como funciona:
- Usa OpenCV para abrir a camera.
- Usa MediaPipe para detectar a mao.
- Se a area da caixa da mao ficar acima de um limite, entende que a mao esta perto.
- Mao perto: LED ligado.
- Mao longe/sem mao: LED desligado.

Dependencias:
    pip install opencv-python mediapipe RPi.GPIO

Execucao:
    python controle_led.py --pin 17 --threshold 0.12
"""

import argparse
import sys
import time

import cv2
import mediapipe as mp


def setup_gpio(pin: int):
    """Inicializa GPIO no Raspberry Pi; retorna modulo GPIO ou None se indisponivel."""
    try:
        import RPi.GPIO as GPIO  # type: ignore

        GPIO.setmode(GPIO.BCM)
        GPIO.setup(pin, GPIO.OUT)
        GPIO.output(pin, GPIO.LOW)
        return GPIO
    except Exception as exc:
        print(f"[AVISO] GPIO nao disponivel: {exc}")
        print("[AVISO] Rodando em modo simulacao (sem controle fisico do LED).")
        return None


def set_led(gpio_module, pin: int, state: bool):
    """Atualiza estado do LED (real ou simulacao)."""
    if gpio_module is not None:
        gpio_module.output(pin, gpio_module.HIGH if state else gpio_module.LOW)
    else:
        print("LED ON" if state else "LED OFF")


def hand_area_ratio(landmarks, frame_width: int, frame_height: int) -> float:
    """Calcula area normalizada da mao na imagem com base na bounding box dos landmarks."""
    xs = [lm.x * frame_width for lm in landmarks]
    ys = [lm.y * frame_height for lm in landmarks]

    min_x, max_x = max(min(xs), 0), min(max(xs), frame_width)
    min_y, max_y = max(min(ys), 0), min(max(ys), frame_height)

    box_w = max_x - min_x
    box_h = max_y - min_y
    box_area = max(box_w * box_h, 0)
    frame_area = frame_width * frame_height
    if frame_area <= 0:
        return 0.0
    return float(box_area) / float(frame_area)


def main():
    parser = argparse.ArgumentParser(description="Liga LED quando a mao chega perto da camera.")
    parser.add_argument("--pin", type=int, default=17, help="Pino BCM do LED (padrao: 17)")
    parser.add_argument(
        "--threshold",
        type=float,
        default=0.12,
        help="Limiar da area da mao para considerar perto (padrao: 0.12)",
    )
    parser.add_argument("--camera", type=int, default=0, help="Indice da camera (padrao: 0)")
    parser.add_argument(
        "--min-detect",
        type=float,
        default=0.6,
        help="Confianca minima de deteccao (padrao: 0.6)",
    )
    parser.add_argument(
        "--min-track",
        type=float,
        default=0.5,
        help="Confianca minima de rastreio (padrao: 0.5)",
    )
    args = parser.parse_args()

    gpio = setup_gpio(args.pin)
    led_state = False

    cap = cv2.VideoCapture(args.camera)
    if not cap.isOpened():
        print("[ERRO] Nao foi possivel abrir a camera.")
        if gpio is not None:
            gpio.cleanup()
        sys.exit(1)

    mp_hands = mp.solutions.hands
    mp_draw = mp.solutions.drawing_utils

    # Pequeno debounce para evitar piscar com variacao de frame.
    near_count = 0
    far_count = 0
    debounce_frames = 3

    with mp_hands.Hands(
        static_image_mode=False,
        max_num_hands=1,
        min_detection_confidence=args.min_detect,
        min_tracking_confidence=args.min_track,
    ) as hands:
        print("[INFO] Pressione 'q' para sair.")
        while True:
            ok, frame = cap.read()
            if not ok:
                print("[ERRO] Falha ao ler frame da camera.")
                break

            frame = cv2.flip(frame, 1)
            h, w = frame.shape[:2]

            rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            result = hands.process(rgb)

            hand_ratio = 0.0
            hand_found = False

            if result.multi_hand_landmarks:
                hand_found = True
                landmarks = result.multi_hand_landmarks[0]
                mp_draw.draw_landmarks(frame, landmarks, mp_hands.HAND_CONNECTIONS)
                hand_ratio = hand_area_ratio(landmarks.landmark, w, h)

            is_near = hand_found and (hand_ratio >= args.threshold)

            if is_near:
                near_count += 1
                far_count = 0
            else:
                far_count += 1
                near_count = 0

            if near_count >= debounce_frames and not led_state:
                led_state = True
                set_led(gpio, args.pin, True)

            if far_count >= debounce_frames and led_state:
                led_state = False
                set_led(gpio, args.pin, False)

            status = "PERTO" if is_near else "LONGE"
            cv2.putText(
                frame,
                f"Estado: {status}",
                (10, 30),
                cv2.FONT_HERSHEY_SIMPLEX,
                0.8,
                (0, 255, 0) if is_near else (0, 0, 255),
                2,
            )
            cv2.putText(
                frame,
                f"Area mao: {hand_ratio:.3f}  Limite: {args.threshold:.3f}",
                (10, 60),
                cv2.FONT_HERSHEY_SIMPLEX,
                0.65,
                (255, 255, 255),
                2,
            )
            cv2.putText(
                frame,
                f"LED: {'ON' if led_state else 'OFF'}",
                (10, 90),
                cv2.FONT_HERSHEY_SIMPLEX,
                0.75,
                (0, 255, 255),
                2,
            )

            cv2.imshow("Controle LED por Mao", frame)
            key = cv2.waitKey(1) & 0xFF
            if key == ord("q"):
                break

            time.sleep(0.005)

    cap.release()
    cv2.destroyAllWindows()
    if gpio is not None:
        gpio.output(args.pin, gpio.LOW)
        gpio.cleanup()


if __name__ == "__main__":
    main()
