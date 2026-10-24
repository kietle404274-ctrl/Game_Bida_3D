import sys
import numpy as np
import soundfile as sf

for path in sys.argv[1:]:
    y, sr = sf.read(path)
    if y.ndim > 1:
        y = y.mean(axis=1)
    dur = len(y) / sr
    peak = np.max(np.abs(y))
    env = np.abs(y)
    win = int(sr * 0.005)
    rms = np.sqrt(np.convolve(y ** 2, np.ones(win) / win, mode="same"))
    # onset: các điểm RMS vượt 35% đỉnh sau khi đã xuống dưới 10%
    thr_hi, thr_lo = 0.35 * rms.max(), 0.10 * rms.max()
    onsets, armed = [], True
    for i, v in enumerate(rms):
        if armed and v > thr_hi:
            onsets.append(i / sr); armed = False
        elif not armed and v < thr_lo:
            armed = True
    # thời gian tắt: từ đỉnh đến khi RMS < 10% đỉnh
    pk = int(np.argmax(rms))
    after = np.where(rms[pk:] < thr_lo)[0]
    decay = (after[0] / sr) if len(after) else (len(y) - pk) / sr
    spec = np.abs(np.fft.rfft(y * np.hanning(len(y))))
    freqs = np.fft.rfftfreq(len(y), 1 / sr)
    centroid = (spec * freqs).sum() / spec.sum()
    cum = np.cumsum(spec) / spec.sum()
    f85 = freqs[np.searchsorted(cum, 0.85)]
    bands = [(0, 200), (200, 800), (800, 2500), (2500, 6000), (6000, 12000)]
    tot = (spec ** 2).sum()
    be = [100 * (spec[(freqs >= a) & (freqs < b)] ** 2).sum() / tot for a, b in bands]
    top = freqs[np.argsort(spec)[-5:][::-1]]
    print(f"== {path}\n sr={sr} dur={dur:.2f}s peak={peak:.2f} onsets={len(onsets)} at {[round(o,2) for o in onsets[:12]]}")
    print(f" decay_to_10%={decay*1000:.0f}ms centroid={centroid:.0f}Hz f85={f85:.0f}Hz")
    print(" band% " + " ".join(f"{a}-{b}Hz:{e:.0f}" for (a, b), e in zip(bands, be)))
    print(" top peaks Hz:", [int(f) for f in top])
