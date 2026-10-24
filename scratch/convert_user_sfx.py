"""Chuyển 3 file mp3 do người dùng cung cấp thành wav sạch cho game.

- tieng_bi_va_cham.mp3 -> sfx_ball_hit.wav   (chỉ giữ tiếng tách đầu tiên, bỏ chuỗi nảy phía sau)
- Tieng_bi_roi.mp3     -> sfx_pocket_drop.wav (giữ nguyên, chuẩn hóa)
- Tieng_pha_bi.mp3     -> sfx_break_shot.wav  (cắt khoảng lặng đầu, chuẩn hóa chống rè)

Chạy: python scratch/convert_user_sfx.py
"""
import os
import numpy as np
import soundfile as sf

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
OUT = os.path.join(ROOT, "Assets", "Game", "Audio")


def load_mono(name):
    y, sr = sf.read(os.path.join(ROOT, name))
    if y.ndim > 1:
        y = y.mean(axis=1)
    return y, sr


def save(y, sr, name, fade_out_ms=20, peak=0.9):
    n_out = int(sr * fade_out_ms / 1000)
    y = y.copy()
    y[-n_out:] *= np.linspace(1, 0, n_out)
    y = y / (np.max(np.abs(y)) + 1e-9) * peak
    sf.write(os.path.join(OUT, name), y, sr, subtype="PCM_16")
    print("wrote", name, f"{len(y)/sr:.2f}s")


# 1. Va chạm bi: file gốc gồm nhiều đoạn nối nhau (0.04s, ~0.25s, ~0.37s).
#    Người dùng xác nhận tiếng tách phía sau mới đúng -> lấy đoạn cuối (onset ~0.37s) cùng phần đuôi ngân
y, sr = load_mono("tieng_bi_va_cham.mp3")
save(y[int(sr * 0.365): int(sr * 0.56)], sr, "sfx_ball_hit.wav")

# 2. Bi rơi lỗ: dùng cả file
y, sr = load_mono("Tieng_bi_roi.mp3")
save(y, sr, "sfx_pocket_drop.wav", fade_out_ms=30)

# 3. Phá bi: bỏ ~0.58s lặng ở đầu
y, sr = load_mono("Tieng_pha_bi.mp3")
save(y[int(sr * 0.58):], sr, "sfx_break_shot.wav", fade_out_ms=60)
