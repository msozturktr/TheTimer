#!/usr/bin/env python3
"""Generate the TheTimer completion chime (two soft bell tones) as 16-bit mono WAV, stdlib only."""
import math, struct, sys, wave

RATE = 44100
OUT = sys.argv[1] if len(sys.argv) > 1 else "complete.wav"

def bell(freq, start, length, gain):
    for i in range(int(length * RATE)):
        t = i / RATE
        env = math.exp(-4.0 * t) * min(1.0, t * 200)  # fast attack, smooth decay
        s = math.sin(2 * math.pi * freq * t) + 0.3 * math.sin(2 * math.pi * freq * 2.01 * t)
        yield int((start * RATE) + i), gain * env * s

samples = [0.0] * int(1.6 * RATE)
for freq, start, gain in ((880.0, 0.0, 0.35), (1318.5, 0.18, 0.30)):
    for idx, value in bell(freq, start, 1.4, gain):
        if idx < len(samples):
            samples[idx] += value

with wave.open(OUT, "wb") as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE)
    w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s)) * 32000)) for s in samples))
