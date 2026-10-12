#!/usr/bin/env python3
"""Generate TheTimer completion sounds as 16-bit mono WAV, stdlib only.
Usage: make_sound.py <chime|bell|gong> <out.wav>"""
import math, struct, sys, wave

RATE = 44100
PRESETS = {
    # (frequency Hz, start s, gain, decay rate) per tone, total length s
    "chime": ([(880.0, 0.0, 0.35, 4.0), (1318.5, 0.18, 0.30, 4.0)], 1.6),
    "bell": ([(1568.0, 0.0, 0.28, 9.0), (1568.0, 0.22, 0.28, 9.0), (2093.0, 0.44, 0.30, 6.0)], 1.4),
    "gong": ([(196.0, 0.0, 0.45, 1.6), (293.7, 0.0, 0.20, 2.2)], 2.8),
}

preset, out = sys.argv[1], sys.argv[2]
tones, total = PRESETS[preset]
samples = [0.0] * int(total * RATE)
for freq, start, gain, decay in tones:
    first = int(start * RATE)
    for i in range(len(samples) - first):
        t = i / RATE
        env = math.exp(-decay * t) * min(1.0, t * 200)  # fast attack, smooth decay
        samples[first + i] += gain * env * (math.sin(2 * math.pi * freq * t) + 0.3 * math.sin(2 * math.pi * freq * 2.01 * t))

with wave.open(out, "wb") as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE)
    w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s)) * 32000)) for s in samples))
