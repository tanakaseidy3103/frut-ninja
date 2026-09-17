@echo off
cd /d "%~dp0\.."
python python\gesture_sender.py --host 127.0.0.1 --port 5052 --player-id player-1 --camera 0 --show
pause
