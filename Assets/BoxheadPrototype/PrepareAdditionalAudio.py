"""Prepare the four additional guns from their own ElevenLabs recordings."""
from pathlib import Path
import subprocess
import sys

if __name__ == '__main__':
    subprocess.run([
        sys.executable, str(Path(__file__).with_name('PrepareDedicatedWeaponAudio.py')),
        '--profiles', 'AK47', 'Shotgun', 'Suomi_KP', 'Grease_Gun',
    ], check=True)
