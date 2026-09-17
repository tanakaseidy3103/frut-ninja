"""
Receptor UDP para controle de LED no Raspberry Pi.

Uso no Raspberry:
    python3 led_receiver.py --port 5053 --pin 17

Ele recebe pacotes JSON com chave `isNear` (true/false) e atualiza o LED.
"""

import argparse
import json
import socket


class GpioLed:
    def __init__(self, pin: int):
        self.pin = pin
        self.gpio = None
        self.state = False
        self._setup()

    def _setup(self):
        try:
            import RPi.GPIO as GPIO  # type: ignore

            GPIO.setmode(GPIO.BCM)
            GPIO.setup(self.pin, GPIO.OUT)
            GPIO.output(self.pin, GPIO.LOW)
            self.gpio = GPIO
            print(f"[INFO] GPIO pronto no pino BCM {self.pin}")
        except Exception as exc:
            raise RuntimeError(f"Falha ao inicializar GPIO: {exc}")

    def set(self, on: bool):
        if self.gpio is None:
            return
        if on == self.state:
            return
        self.state = on
        self.gpio.output(self.pin, self.gpio.HIGH if on else self.gpio.LOW)
        print("[LED] ON" if on else "[LED] OFF")

    def cleanup(self):
        if self.gpio is not None:
            self.gpio.output(self.pin, self.gpio.LOW)
            self.gpio.cleanup()


def main():
    parser = argparse.ArgumentParser(description="Recebe estado de mao perto/longe por UDP e controla LED.")
    parser.add_argument("--host", default="0.0.0.0", help="Host local para escutar UDP")
    parser.add_argument("--port", type=int, default=5053, help="Porta UDP para escutar")
    parser.add_argument("--pin", type=int, default=17, help="Pino BCM do LED")
    args = parser.parse_args()

    led = GpioLed(args.pin)
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind((args.host, args.port))
    print(f"[INFO] Escutando UDP em {args.host}:{args.port}")

    try:
        while True:
            raw, addr = sock.recvfrom(4096)
            try:
                data = json.loads(raw.decode("utf-8"))
            except Exception:
                continue

            is_near = bool(data.get("isNear", False))
            led.set(is_near)
            print(f"[INFO] from={addr[0]} gesture={data.get('gesture', 'none')} near={is_near}")
    except KeyboardInterrupt:
        print("\n[INFO] Encerrando receptor.")
    finally:
        sock.close()
        led.cleanup()


if __name__ == "__main__":
    main()
