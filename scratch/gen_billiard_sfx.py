"""Sinh âm thanh bida thực tế bằng tổng hợp vật lý (modal synthesis).

- sfx_ball_hit.wav     : va chạm bi-bi (nhựa phenolic: tiếng "tách" sắc, ngân ngắn)
- sfx_cue_hit.wav      : đầu cơ (da + phấn) chạm bi cái: tiếng "tốc" đục + tách nhẹ
- sfx_cushion_hit.wav  : bi đập băng cao su: tiếng "thụp" trầm, tắt nhanh

Chạy: python scratch/gen_billiard_sfx.py
"""
import os
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, lfilter

SR = 44100
rng = np.random.default_rng(7)
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Game", "Audio")


def tvec(dur):
    return np.arange(int(SR * dur)) / SR


def modes(t, spec):
    """spec: list (freq_hz, amp, tau_s) -> tổng các sin tắt dần."""
    y = np.zeros_like(t)
    for f, a, tau in spec:
        ph = rng.uniform(0, 2 * np.pi)
        y += a * np.sin(2 * np.pi * f * t + ph) * np.exp(-t / tau)
    return y


def noise(t, tau, kind, cutoff):
    n = rng.standard_normal(len(t)) * np.exp(-t / tau)
    b, a = butter(2, cutoff / (SR / 2), btype=kind)
    return lfilter(b, a, n)


def finish(y, name, peak=0.9):
    # vào gắt (0.1ms) và ra mượt (15ms) để không bị "bụp" đầu/cuối
    n_in = int(SR * 0.0001)
    y[:n_in] *= np.linspace(0, 1, n_in)
    n_out = int(SR * 0.015)
    y[-n_out:] *= np.linspace(1, 0, n_out)
    y = y / (np.max(np.abs(y)) + 1e-9) * peak
    wavfile.write(os.path.join(OUT, name), SR, (y * 32767).astype(np.int16))
    print("wrote", name, f"{len(y)/SR:.2f}s")


def ball_click(t, scale=1.0):
    y = modes(t, [
        (1250, 0.30, 0.014),
        (2650, 1.00, 0.010),
        (4350, 0.70, 0.007),
        (6900, 0.50, 0.004),
        (9800, 0.30, 0.0025),
    ])
    y += 0.8 * noise(t, 0.0008, "high", 3000)       # tách va chạm
    y += 0.30 * modes(t, [(480, 1.0, 0.006)])       # thân bi
    return y * scale


def gen_ball_hit():
    t = tvec(0.28)
    finish(ball_click(t), "sfx_ball_hit.wav")


def gen_cue_hit():
    t = tvec(0.25)
    y = modes(t, [
        (180, 0.80, 0.018),
        (420, 0.60, 0.012),
        (900, 0.40, 0.008),
        (2600, 0.35, 0.004),
    ])
    y += 0.9 * noise(t, 0.003, "low", 3000)         # va chạm da cơ
    y += 0.45 * ball_click(t)                       # bi cái kêu theo
    finish(y, "sfx_cue_hit.wav")


def gen_cushion_hit():
    t = tvec(0.32)
    y = modes(t, [
        (95, 1.00, 0.050),
        (170, 0.70, 0.035),
        (320, 0.40, 0.020),
    ])
    y += 0.6 * noise(t, 0.012, "low", 700)
    y += 0.2 * modes(t, [(2000, 1.0, 0.002)])
    b, a = butter(2, 3000 / (SR / 2), btype="low")
    finish(lfilter(b, a, y), "sfx_cushion_hit.wav")


if __name__ == "__main__":
    gen_ball_hit()
    gen_cue_hit()
    gen_cushion_hit()
