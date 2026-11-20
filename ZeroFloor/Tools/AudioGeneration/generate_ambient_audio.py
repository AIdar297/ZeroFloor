# -*- coding: utf-8 -*-
"""
Процедурная генерация фоновых аудиопетель (музыка + эмбиент) для «Нулевой этаж».

Все треки синтезируются (дроны + фильтрованный шум + редкие события), а не
берутся из внешних источников — это оригинальный контент, созданный под
конкретные сцены игры. Каждый трек — бесшовная петля (закольцована кроссфейдом
хвоста в начало), подходящая для IAudioService.PlayMusicAsync/PlayAmbienceAsync.

Запуск: python3 generate_ambient_audio.py   (из корня проекта ZeroFloor)
Результат: Assets/Audio/Music/<id>.wav, Assets/Audio/Ambience/<id>.wav
"""
import numpy as np
import wave
import os

SR = 44100

# Пишем во временную папку вне проекта: .wav-мастера затем конвертируются в
# .mp3 отдельным шагом (ffmpeg) прямо в Assets/Audio/{Music,Ambience}, а эта
# папка удаляется — так в репозитории остаются только компактные .mp3.
TMP_ROOT = os.environ.get("ZF_AUDIO_TMP") or os.path.join(os.path.expanduser("~"), "zf_audio_tmp")
OUT_MUSIC = os.path.join(TMP_ROOT, "Music")
OUT_AMB = os.path.join(TMP_ROOT, "Ambience")


def lp_noise(n, cutoff, rng):
    x = rng.standard_normal(n)
    spec = np.fft.rfft(x)
    freqs = np.fft.rfftfreq(n, 1.0 / SR)
    resp = 1.0 / (1.0 + (freqs / max(cutoff, 1.0)) ** 2)
    y = np.fft.irfft(spec * resp, n)
    return y / (np.std(y) + 1e-9)


def bp_noise(n, low, high, rng):
    x = rng.standard_normal(n)
    spec = np.fft.rfft(x)
    freqs = np.fft.rfftfreq(n, 1.0 / SR)
    resp = (freqs / max(low, 1.0)) / (1.0 + (freqs / max(low, 1.0)) ** 2)
    resp = resp / (1.0 + (freqs / max(high, 1.0)) ** 2)
    y = np.fft.irfft(spec * resp, n)
    return y / (np.std(y) + 1e-9)


def drone(t, base_freq, harmonics, vibrato_depth, vibrato_rate, detune, phase):
    sig = np.zeros_like(t)
    for h, amp in harmonics:
        f = base_freq * h * (1.0 + detune)
        vib = 1.0 + vibrato_depth * np.sin(2 * np.pi * vibrato_rate * t + phase * h)
        sig += amp * np.sin(2 * np.pi * f * vib * t + phase)
    return sig


def sparse_events(n, rng, count, freqs, amp, decay_range, burst_len=2.0):
    buf = np.zeros(n)
    tloc = np.linspace(0, burst_len, int(burst_len * SR), endpoint=False)
    for _ in range(count):
        start = int(rng.uniform(0.03, 0.93) * n)
        freq = rng.choice(freqs)
        decay = rng.uniform(*decay_range)
        env = np.exp(-tloc / decay)
        burst = amp * rng.uniform(0.6, 1.0) * np.sin(2 * np.pi * freq * tloc) * env
        end = min(n, start + len(burst))
        buf[start:end] += burst[: end - start]
    return buf


def build(t, rng, p):
    n = len(t)
    sig = np.zeros(n)
    if p.get("harmonics"):
        sig += p.get("drone_gain", 0.5) * drone(
            t,
            p["base_freq"],
            p["harmonics"],
            p.get("vibrato_depth", 0.002),
            p.get("vibrato_rate", 0.06),
            rng.uniform(-0.003, 0.003),
            rng.uniform(0, 2 * np.pi),
        )
    if p.get("noise_level"):
        if p.get("noise_type") == "band":
            noise = bp_noise(n, p["noise_low"], p["noise_high"], rng)
        else:
            noise = lp_noise(n, p.get("noise_cutoff", 400), rng)
        sig += p["noise_level"] * noise
    if p.get("tremolo_depth"):
        trem = 1.0 + p["tremolo_depth"] * np.sin(
            2 * np.pi * p.get("tremolo_rate", 0.07) * t + rng.uniform(0, 2 * np.pi)
        )
        sig *= trem
    if p.get("pulse_bpm"):
        beat_hz = p["pulse_bpm"] / 60.0
        env = ((np.sin(2 * np.pi * beat_hz * t - np.pi / 2) + 1) / 2) ** 3
        depth = p.get("pulse_depth", 0.4)
        sig *= 1 - depth + depth * env
    if p.get("events_count"):
        sig += sparse_events(
            n,
            rng,
            p["events_count"],
            p["events_freqs"],
            p.get("events_amp", 0.12),
            p.get("events_decay", (1.0, 2.0)),
        )
    return sig


def make_loop(duration, p, seed):
    cross = 3.0
    n = int((duration + cross) * SR)
    main_n = int(duration * SR)
    cs = int(cross * SR)
    t = np.arange(n) / SR

    def one_channel(ch_seed):
        rng = np.random.default_rng(ch_seed)
        x = build(t, rng, p)
        head = x[:main_n].copy()
        tail = x[main_n : main_n + cs]
        if len(tail) < cs:
            tail = np.pad(tail, (0, cs - len(tail)))
        fo = np.linspace(1, 0, cs)
        fi = np.linspace(0, 1, cs)
        head[:cs] = head[:cs] * fi + tail * fo
        return head

    left = one_channel(seed)
    right = one_channel(seed + 10007)
    return left, right


def normalize(x, peak):
    m = np.max(np.abs(x)) + 1e-9
    return x / m * peak


def write_wav(path, left, right):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    n = len(left)
    inter = np.empty(n * 2, dtype=np.int16)
    inter[0::2] = (np.clip(left, -1, 1) * 32767).astype(np.int16)
    inter[1::2] = (np.clip(right, -1, 1) * 32767).astype(np.int16)
    with wave.open(path, "wb") as wf:
        wf.setnchannels(2)
        wf.setsampwidth(2)
        wf.setframerate(SR)
        wf.writeframes(inter.tobytes())


# ---------------------------------------------------------------------------
# Таблица треков: id -> (category, duration, params)
# ---------------------------------------------------------------------------

AMBIENCE = {
    "amb_dispatch_hum": dict(
        duration=36, base_freq=55, harmonics=[(1, 1.0), (2, 0.4), (3, 0.15)],
        drone_gain=0.35, noise_level=0.5, noise_cutoff=300,
        tremolo_depth=0.08, tremolo_rate=0.05,
        events_count=4, events_freqs=[220, 330, 440], events_amp=0.06, events_decay=(0.15, 0.35),
    ),
    "amb_market_crowd": dict(
        duration=36, noise_level=0.6, noise_type="band", noise_low=150, noise_high=2200,
        tremolo_depth=0.15, tremolo_rate=0.3,
        events_count=14, events_freqs=[500, 650, 800, 1000, 1300], events_amp=0.12, events_decay=(0.1, 0.3),
    ),
    "amb_tech_shafts": dict(
        duration=38, base_freq=40, harmonics=[(1, 0.6), (2, 0.3)], drone_gain=0.3,
        noise_level=0.55, noise_type="band", noise_low=80, noise_high=1200,
        tremolo_depth=0.25, tremolo_rate=0.08,
        events_count=6, events_freqs=[90, 120, 160], events_amp=0.2, events_decay=(0.3, 0.6),
    ),
    "amb_archive_drone": dict(
        duration=36, base_freq=80, harmonics=[(1, 0.8), (2, 0.3), (4, 0.15)], drone_gain=0.4,
        noise_level=0.25, noise_cutoff=200,
        tremolo_depth=0.05, tremolo_rate=0.03,
        events_count=8, events_freqs=[1200, 1500, 1800], events_amp=0.05, events_decay=(0.1, 0.2),
    ),
    "amb_ventilation": dict(
        duration=36, base_freq=48, harmonics=[(1, 0.5)], drone_gain=0.3,
        noise_level=0.65, noise_type="band", noise_low=200, noise_high=3000,
        tremolo_depth=0.3, tremolo_rate=0.12,
    ),
    "amb_admin_sterile": dict(
        duration=36, base_freq=120, harmonics=[(1, 0.3), (3, 0.1)], drone_gain=0.35,
        noise_level=0.2, noise_cutoff=1500,
        tremolo_depth=0.03, tremolo_rate=0.5,
    ),
    "amb_apartment_quiet": dict(
        duration=36, base_freq=65, harmonics=[(1, 0.25)], drone_gain=0.3,
        noise_level=0.15, noise_cutoff=250,
        tremolo_depth=0.05, tremolo_rate=0.05,
        events_count=3, events_freqs=[300, 400], events_amp=0.04, events_decay=(0.3, 0.6),
    ),
    "amb_office_tension": dict(
        duration=36, base_freq=90, harmonics=[(1, 0.4), (2, 0.2)], drone_gain=0.3,
        noise_level=0.2, noise_cutoff=350,
        tremolo_depth=0.04, tremolo_rate=0.07,
        events_count=12, events_freqs=[1000], events_amp=0.03, events_decay=(0.05, 0.1),
    ),
    "amb_crown_airy": dict(
        duration=36, base_freq=220, harmonics=[(1, 0.3), (2, 0.15), (3, 0.08)], drone_gain=0.3,
        noise_level=0.3, noise_type="band", noise_low=500, noise_high=4000,
        tremolo_depth=0.1, tremolo_rate=0.04,
        events_count=5, events_freqs=[1046.5, 1318.5, 1568], events_amp=0.05, events_decay=(0.8, 1.5),
    ),
    "amb_core_cathedral": dict(
        duration=42, base_freq=36, harmonics=[(1, 1.0), (1.5, 0.5), (2, 0.4), (3, 0.2)], drone_gain=0.4,
        noise_level=0.3, noise_cutoff=150,
        tremolo_depth=0.15, tremolo_rate=0.03,
        events_count=4, events_freqs=[72, 108], events_amp=0.15, events_decay=(1.5, 3.0),
    ),
}

MUSIC = {
    "mus_low_city_theme": dict(
        duration=46, base_freq=110, harmonics=[(1, 0.6), (1.5, 0.3), (2, 0.25)], drone_gain=0.5,
        noise_level=0.2, noise_cutoff=300,
        tremolo_depth=0.1, tremolo_rate=0.06,
        events_count=5, events_freqs=[220, 277.18, 330], events_amp=0.08, events_decay=(1.0, 2.0),
    ),
    "mus_market_theme": dict(
        duration=46, base_freq=220, harmonics=[(1, 0.4), (1.25, 0.2), (1.5, 0.25), (2, 0.15)], drone_gain=0.45,
        noise_level=0.25, noise_type="band", noise_low=500, noise_high=3000,
        tremolo_depth=0.2, tremolo_rate=0.5,
        events_count=16, events_freqs=[440, 523.25, 587.33, 659.25, 784], events_amp=0.1, events_decay=(0.3, 0.6),
    ),
    "mus_stealth_tension": dict(
        duration=46, base_freq=98, harmonics=[(1, 0.5), (2, 0.2)], drone_gain=0.45,
        noise_level=0.15, noise_cutoff=250,
        pulse_bpm=70, pulse_depth=0.5,
        events_count=6, events_freqs=[196, 233.08], events_amp=0.1, events_decay=(0.2, 0.4),
    ),
    "mus_interrogation_dread": dict(
        duration=46, base_freq=65, harmonics=[(1, 0.7), (1.02, 0.4)], drone_gain=0.5,
        noise_level=0.3, noise_cutoff=180,
        tremolo_depth=0.12, tremolo_rate=0.04,
        events_count=4, events_freqs=[65, 98], events_amp=0.12, events_decay=(2.0, 3.0),
    ),
    "mus_apartment_intimate": dict(
        duration=46, base_freq=196, harmonics=[(1, 0.5), (2, 0.25), (3, 0.1)], drone_gain=0.4,
        noise_level=0.1, noise_cutoff=300,
        tremolo_depth=0.08, tremolo_rate=0.08,
        events_count=8, events_freqs=[392, 440, 493.88, 587.33], events_amp=0.09, events_decay=(1.0, 2.0),
    ),
    "mus_archive_mystery": dict(
        duration=46, base_freq=146.83, harmonics=[(1, 0.5), (1.5, 0.3), (2, 0.2)], drone_gain=0.45,
        noise_level=0.2, noise_cutoff=220,
        tremolo_depth=0.1, tremolo_rate=0.05,
        events_count=10, events_freqs=[293.66, 349.23, 440, 523.25], events_amp=0.1, events_decay=(1.5, 2.5),
    ),
    "mus_power_intrigue": dict(
        duration=46, base_freq=123.47, harmonics=[(1, 0.6), (1.5, 0.25), (2, 0.2)], drone_gain=0.45,
        noise_level=0.18, noise_cutoff=260,
        pulse_bpm=55, pulse_depth=0.3,
        events_count=5, events_freqs=[246.94, 311.13], events_amp=0.08, events_decay=(1.0, 2.0),
    ),
    "mus_crown_cold": dict(
        duration=46, base_freq=261.63, harmonics=[(1, 0.4), (2, 0.3), (3, 0.15), (4, 0.08)], drone_gain=0.4,
        noise_level=0.15, noise_type="band", noise_low=800, noise_high=4000,
        tremolo_depth=0.06, tremolo_rate=0.03,
        events_count=7, events_freqs=[523.25, 659.25, 783.99], events_amp=0.07, events_decay=(1.5, 2.5),
    ),
    "mus_core_climax": dict(
        duration=52, base_freq=55, harmonics=[(1, 0.8), (1.5, 0.5), (2, 0.4), (3, 0.25), (4, 0.15)], drone_gain=0.5,
        noise_level=0.35, noise_cutoff=220,
        tremolo_depth=0.2, tremolo_rate=0.1,
        pulse_bpm=90, pulse_depth=0.35,
        events_count=8, events_freqs=[110, 164.81, 220], events_amp=0.18, events_decay=(1.0, 2.0),
    ),
    "mus_watcher_calm": dict(
        duration=46, base_freq=87.31, harmonics=[(1, 0.4), (2, 0.2)], drone_gain=0.4,
        noise_level=0.15, noise_cutoff=200,
        tremolo_depth=0.07, tremolo_rate=0.04,
        events_count=4, events_freqs=[174.61, 261.63], events_amp=0.06, events_decay=(2.0, 3.0),
    ),
    "mus_finale_escape": dict(
        duration=46, base_freq=196, harmonics=[(1, 0.5), (1.5, 0.3), (2, 0.25), (3, 0.12)], drone_gain=0.45,
        noise_level=0.15, noise_type="band", noise_low=400, noise_high=3500,
        tremolo_depth=0.1, tremolo_rate=0.06,
        events_count=10, events_freqs=[392, 440, 493.88, 587.33, 659.25], events_amp=0.12, events_decay=(1.0, 2.0),
    ),
    "mus_finale_collapse": dict(
        duration=46, base_freq=49, harmonics=[(1, 0.9), (1.02, 0.5), (2, 0.3)], drone_gain=0.55,
        noise_level=0.4, noise_cutoff=200,
        tremolo_depth=0.25, tremolo_rate=0.08,
        events_count=5, events_freqs=[49, 73.42], events_amp=0.2, events_decay=(2.0, 3.5),
    ),
    "mus_finale_successor": dict(
        duration=46, base_freq=110, harmonics=[(1, 0.5), (2, 0.25), (3, 0.1)], drone_gain=0.4,
        noise_level=0.15, noise_cutoff=220,
        tremolo_depth=0.08, tremolo_rate=0.04,
        events_count=6, events_freqs=[220, 261.63, 329.63], events_amp=0.08, events_decay=(1.5, 2.5),
    ),
    "mus_finale_balance": dict(
        duration=46, base_freq=174.61, harmonics=[(1, 0.5), (1.5, 0.3), (2, 0.2)], drone_gain=0.4,
        noise_level=0.12, noise_cutoff=260,
        tremolo_depth=0.07, tremolo_rate=0.05,
        events_count=8, events_freqs=[349.23, 415.30, 523.25], events_amp=0.09, events_decay=(1.5, 2.5),
    ),
}


def generate_all():
    seed_base = 1000
    report = []
    for idx, (track_id, p) in enumerate(sorted(AMBIENCE.items())):
        left, right = make_loop(p["duration"], p, seed_base + idx)
        left = normalize(left, 0.7)
        right = normalize(right, 0.7)
        path = os.path.join(OUT_AMB, track_id + ".wav")
        write_wav(path, left, right)
        report.append(("ambience", track_id, p["duration"], path))

    for idx, (track_id, p) in enumerate(sorted(MUSIC.items())):
        left, right = make_loop(p["duration"], p, seed_base + 500 + idx)
        left = normalize(left, 0.8)
        right = normalize(right, 0.8)
        path = os.path.join(OUT_MUSIC, track_id + ".wav")
        write_wav(path, left, right)
        report.append(("music", track_id, p["duration"], path))

    for cat, tid, dur, path in report:
        size_kb = os.path.getsize(path) / 1024
        print(f"[{cat:9s}] {tid:28s} {dur:5.1f}s  {size_kb:8.1f} KB  -> {path}")
    print(f"\nTotal: {len(report)} tracks")


if __name__ == "__main__":
    generate_all()
