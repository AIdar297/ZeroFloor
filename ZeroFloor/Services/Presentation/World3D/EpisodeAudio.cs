using System.IO;
using System.Windows.Media;
using ZeroFloor.Services;

namespace ZeroFloor.Services.Presentation.World3D;

/// <summary>
/// Звук 3D-эпизодов: все звуки синтезируются процедурно (шум, осцилляторы, огибающие) и
/// записываются в кэш %LOCALAPPDATA%\ZeroFloor\cache\episode_sfx как WAV — это собственные
/// звуки эпизодов, а не подмена авторских треков новеллы. Воспроизведение — пулы MediaPlayer
/// (одноразовые звуки) и зацикленные плееры (гул дрона, сирена, атмосфера, пар). Громкость =
/// Master × SFX (или Ambience для атмосферы) из настроек игры, в реальном времени.
/// </summary>
public sealed class EpisodeAudio : IDisposable
{
    private const int Rate = 22050;
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZeroFloor", "cache", "episode_sfx_v1");

    private readonly ISettingsStore _settings;
    private readonly Dictionary<string, List<MediaPlayer>> _pools = new();
    private readonly Dictionary<string, int> _poolCursor = new();
    private readonly Dictionary<string, (MediaPlayer player, double volume, bool ambience)> _loops = new();
    private bool _disposed;
    private bool _paused;

    public EpisodeAudio(ISettingsStore settings)
    {
        _settings = settings;
        EnsureGenerated();
    }

    private double SfxGain => Math.Clamp(_settings.Current.MasterVolume * _settings.Current.SfxVolume, 0, 1);
    private double AmbGain => Math.Clamp(_settings.Current.MasterVolume * _settings.Current.AmbienceVolume, 0, 1);

    public void Play(string id, double volume = 1.0)
    {
        if (_disposed || _paused) return;
        var path = PathOf(id);
        if (!File.Exists(path)) return;

        if (!_pools.TryGetValue(id, out var pool))
        {
            pool = new List<MediaPlayer>();
            for (var i = 0; i < 3; i++)
            {
                var p = new MediaPlayer();
                p.Open(new Uri(path));
                pool.Add(p);
            }
            _pools[id] = pool;
            _poolCursor[id] = 0;
        }

        var idx = _poolCursor[id];
        _poolCursor[id] = (idx + 1) % pool.Count;
        var player = pool[idx];
        player.Volume = Math.Clamp(volume * SfxGain, 0, 1);
        player.Position = TimeSpan.Zero;
        player.Play();
    }

    public void SetLoop(string id, double volume, bool ambience = false)
    {
        if (_disposed) return;
        if (!_loops.TryGetValue(id, out var loop))
        {
            if (volume <= 0.001) return;
            var path = PathOf(id);
            if (!File.Exists(path)) return;
            var player = new MediaPlayer();
            player.Open(new Uri(path));
            player.MediaEnded += (_, _) => { player.Position = TimeSpan.Zero; player.Play(); };
            if (!_paused) player.Play();
            loop = (player, volume, ambience);
        }
        loop.volume = volume;
        loop.ambience = ambience;
        _loops[id] = loop;
        loop.player.Volume = Math.Clamp(volume * (ambience ? AmbGain : SfxGain), 0, 1);
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
        foreach (var (player, _, _) in _loops.Values)
        {
            if (paused) player.Pause();
            else player.Play();
        }
    }

    public void StopAll()
    {
        foreach (var (player, _, _) in _loops.Values)
        {
            player.Stop();
            player.Close();
        }
        _loops.Clear();
        foreach (var pool in _pools.Values)
            foreach (var p in pool)
            {
                p.Stop();
                p.Close();
            }
        _pools.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        StopAll();
        _disposed = true;
    }

    private static string PathOf(string id) => Path.Combine(CacheDir, id + ".wav");

    // ------------------------------------------------------------------ генерация

    private static readonly object GenLock = new();

    private static void EnsureGenerated()
    {
        lock (GenLock)
        {
            try
            {
                Directory.CreateDirectory(CacheDir);
                foreach (var (id, gen) in Generators())
                {
                    var path = PathOf(id);
                    if (File.Exists(path)) continue;
                    WriteWav(path, gen());
                }
            }
            catch (IOException)
            {
                // нет доступа к кэшу — эпизод просто пройдёт без звука, это не ошибка игры
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static IEnumerable<(string, Func<float[]>)> Generators()
    {
        yield return ("step_metal", () => Step(metal: true, 1));
        yield return ("step_metal2", () => Step(metal: true, 2));
        yield return ("step_concrete", () => Step(metal: false, 3));
        yield return ("land", Land);
        yield return ("jump", Jump);
        yield return ("drone_hum", DroneHum);
        yield return ("drone_scan", DroneScan);
        yield return ("alarm", Alarm);
        yield return ("detect_tick", DetectTick);
        yield return ("market_amb", MarketAmbience);
        yield return ("shaft_amb", ShaftAmbience);
        yield return ("spark", Spark);
        yield return ("steam", Steam);
        yield return ("collapse", Collapse);
        yield return ("creak", Creak);
        yield return ("impact", Impact);
        yield return ("clank", Clank);
        yield return ("door", Door);
        yield return ("switch", Switch);
        yield return ("breath", Breath);
        yield return ("radio", Radio);
        yield return ("wrench", WrenchDrop);
        yield return ("objective", Objective);
        yield return ("tarp", Tarp);
        yield return ("guard_steps", GuardSteps);
    }

    private static float[] Buffer(double seconds) => new float[(int)(Rate * seconds)];

    private static double Env(double t, double attack, double decay) =>
        t < attack ? t / attack : Math.Exp(-(t - attack) / decay);

    private static float[] Step(bool metal, int seed)
    {
        var rng = new Random(seed);
        var buf = Buffer(0.22);
        double lp = 0;
        for (var i = 0; i < buf.Length; i++)
        {
            var t = i / (double)Rate;
            var noise = rng.NextDouble() * 2 - 1;
            lp += (noise - lp) * (metal ? 0.35 : 0.12);
            var thud = Math.Sin(2 * Math.PI * (metal ? 95 : 70) * t) * Env(t, 0.003, 0.035);
            var ring = metal ? Math.Sin(2 * Math.PI * (1350 + seed * 140) * t) * Env(t, 0.001, 0.05) * 0.25 : 0;
            buf[i] = (float)((lp * Env(t, 0.002, 0.03) * 0.8 + thud * 0.7 + ring) * 0.7);
        }
        return buf;
    }

    private static float[] Land()
    {
        var rng = new Random(5);
        var buf = Buffer(0.4);
        double lp = 0;
        for (var i = 0; i < buf.Length; i++)
        {
            var t = i / (double)Rate;
            lp += ((rng.NextDouble() * 2 - 1) - lp) * 0.2;
            buf[i] = (float)((Math.Sin(2 * Math.PI * 60 * t) * 0.9 + lp * 0.7) * Env(t, 0.004, 0.08) + Math.Sin(2 * Math.PI * 820 * t) * Env(t, 0.002, 0.12) * 0.15);
        }
        return buf;
    }

    private static float[] Jump()
    {
        var rng = new Random(6);
        var buf = Buffer(0.25);
        double lp = 0;
        for (var i = 0; i < buf.Length; i++)
        {
            var t = i / (double)Rate;
            lp += ((rng.NextDouble() * 2 - 1) - lp) * (0.05 + t * 0.6);
            buf[i] = (float)(lp * Math.Sin(Math.PI * t / 0.25) * 0.6);
        }
        return buf;
    }

    private static float[] DroneHum()
    {
        var rng = new Random(7);
        var buf = Buffer(2.0);
        double lp = 0;
        for (var i = 0; i < buf.Length; i++)
        {
            var t = i / (double)Rate;
            lp += ((rng.NextDouble() * 2 - 1) - lp) * 0.08;
            var am = 0.75 + 0.25 * Math.Sin(2 * Math.PI * 22 * t);
            var tone = Math.Sin(2 * Math.PI * 110 * t) * 0.5 + Math.Sin(2 * Math.PI * 220 * t) * 0.3 + Math.Sin(2 * Math.PI * 331 * t) * 0.15;
            buf[i] = (float)((tone * am + lp * 0.5) * 0.5);
        }
        return buf;
    }

    private static float[] DroneScan()
    {
        var buf = Buffer(0.5);
        for (var i = 0; i < buf.Length; i++)
        {
            var t = i / (double)Rate;
            var f = t < 0.15 ? 1400 : t < 0.3 ? 1750 : 2100;
            var local = t % 0.15;
            buf[i] = (float)(Math.Sin(2 * Math.PI * f * t) * Env(local, 0.005, 0.05) * 0.35);
        }
        return buf;
    }

    private static float[] Alarm()
    {
        var buf = Buffer(1.6);
        double phase = 0;
        for (var i = 0; i < buf.Length; i++)
        {
            var t = i / (double)Rate;
            var sweep = t < 0.8 ? t / 0.8 : (1.6 - t) / 0.8;
            var f = 620 + sweep * 560;
            phase += 2 * Math.PI * f / Rate;
            var square = Math.Sign(Math.Sin(phase)) * 0.3 + Math.Sin(phase) * 0.5;
            buf[i] = (float)(square * 0.45);
        }
        return buf;
    }

    private static float[] DetectTick()
    {
        var buf = Buffer(0.12);
        for (var i = 0; i < buf.Length; i++)
        {
            var t = i / (double)Rate;
            buf[i] = (float)(Math.Sin(2 * Math.PI * 980 * t) * Env(t, 0.002, 0.03) * 0.4);
        }
        return buf;
    }

    private static float[] MarketAmbience()
    {
        var rng = new Random(9);
        var buf = Buffer(8.0);
        double lp = 0, lp2 = 0;
        for (var i = 0; i < buf.Length; i++)
        {
            var t = i / (double)Rate;
            var n = rng.NextDouble() * 2 - 1;
            lp += (n - lp) * 0.04;
            lp2 += (n - lp2) * 0.15;
            var murmur = lp2 * (0.5 + 0.5 * Math.Sin(2 * Math.PI * 0.7 * t + Math.Sin(t * 3.1))) * 0.35;
            var hum = Math.Sin(2 * Math.PI * 50 * t) * 0.06 + Math.Sin(2 * Math.PI * 100 * t) * 0.03;
            buf[i] = (float)(lp * 0.6 + murmur + hum);
        }
        // «голоса»: короткие форманты в случайные моменты
        for (var v = 0; v < 40; v++)
        {
            var start = rng.Next(buf.Length - Rate / 2);
            var f0 = 120 + rng.Next(160);
            var formant = 500 + rng.Next(900);
            var len = Rate / 6 + rng.Next(Rate / 4);
            for (var j = 0; j < len && start + j < buf.Length; j++)
            {
                var t = j / (double)Rate;
                var env = Math.Sin(Math.PI * j / len);
                var s = Math.Sin(2 * Math.PI * f0 * t) * Math.Sin(2 * Math.PI * formant * t) * env * 0.06;
                buf[start + j] += (float)s;
            }
        }
        Crossfade(buf);
        return buf;
    }

    private static float[] ShaftAmbience()
    {
        var rng = new Random(10);
        var buf = Buffer(8.0);
        double lp = 0, air = 0;
        for (var i = 0; i < buf.Length; i++)
        {
            var t = i / (double)Rate;
            var n = rng.NextDouble() * 2 - 1;
            lp += (n - lp) * 0.01;
            air += (n - air) * 0.3;
            var rumble = lp * 2.5 + Math.Sin(2 * Math.PI * 38 * t) * 0.12;
            buf[i] = (float)(rumble * 0.6 + air * 0.05 * (0.6 + 0.4 * Math.Sin(t * 0.9)));
        }
        for (var c = 0; c < 9; c++)
        {
            var start = rng.Next(buf.Length - Rate);
            var f = 300 + rng.Next(700);
            for (var j = 0; j < Rate; j++)
            {
                var t = j / (double)Rate;
                buf[start + j] += (float)(Math.Sin(2 * Math.PI * f * t) * Env(t, 0.002, 0.25) * 0.08);
            }
        }
        Crossfade(buf);
        return buf;
    }

    private static float[] Spark()
    {
        var rng = new Random(11);
        var buf = Buffer(0.45);
        for (var i = 0; i < buf.Length; i++)
        {
            var t = i / (double)Rate;
            var crackle = rng.NextDouble() < 0.06 ? (rng.NextDouble() * 2 - 1) : 0;
            buf[i] = (float)((crackle * 0.9 + (rng.NextDouble() * 2 - 1) * 0.1) * Env(t, 0.002, 0.12));
        }
        return buf;
    }

    private static float[] Steam()
    {
        var rng = new Random(12);
        var buf = Buffer(3.0);
        double hp = 0, prev = 0;
        for (var i = 0; i < buf.Length; i++)
        {
            var t = i / (double)Rate;
            var n = rng.NextDouble() * 2 - 1;
            hp = 0.9 * (hp + n - prev);
            prev = n;
            buf[i] = (float)(hp * 0.25 * (0.8 + 0.2 * Math.Sin(t * 5)));
        }
        Crossfade(buf);
        return buf;
    }

    private static float[] Collapse()
    {
        var rng = new Random(13);
        var buf = Buffer(2.2);
        double lp = 0;
        for (var i = 0; i < buf.Length; i++)
        {
            var t = i / (double)Rate;
            lp += ((rng.NextDouble() * 2 - 1) - lp) * 0.06;
            var boom = Math.Sin(2 * Math.PI * (45 - t * 10) * t) * Env(t, 0.01, 0.5);
            var screech = Math.Sin(2 * Math.PI * (380 + Math.Sin(t * 30) * 60) * t) * Env(t, 0.05, 0.6) * 0.25;
            var ring = Math.Sin(2 * Math.PI * 1170 * t) * Env(t, 0.003, 0.7) * 0.12 + Math.Sin(2 * Math.PI * 760 * t) * Env(t, 0.003, 0.9) * 0.12;
            buf[i] = (float)((boom * 0.8 + lp * 1.6 * Env(t, 0.01, 0.8) + screech + ring) * 0.6);
        }
        return buf;
    }

    private static float[] Creak()
    {
        var buf = Buffer(1.0);
        double phase = 0;
        for (var i = 0; i < buf.Length; i++)
        {
            var t = i / (double)Rate;
            var f = 180 + Math.Sin(t * 9) * 40 + t * 60;
            phase += 2 * Math.PI * f / Rate;
            var saw = (phase / Math.PI % 2) - 1;
            buf[i] = (float)(saw * Math.Sin(Math.PI * t) * 0.25 * (0.6 + 0.4 * Math.Sin(t * 47)));
        }
        return buf;
    }

    private static float[] Impact()
    {
        var rng = new Random(14);
        var buf = Buffer(0.5);
        double lp = 0;
        for (var i = 0; i < buf.Length; i++)
        {
            var t = i / (double)Rate;
            lp += ((rng.NextDouble() * 2 - 1) - lp) * 0.25;
            buf[i] = (float)((Math.Sin(2 * Math.PI * 80 * t) + lp * 0.8) * Env(t, 0.002, 0.07) * 0.8
                + Math.Sin(2 * Math.PI * 540 * t) * Env(t, 0.002, 0.2) * 0.2);
        }
        return buf;
    }

    private static float[] Clank()
    {
        var buf = Buffer(0.8);
        for (var i = 0; i < buf.Length; i++)
        {
            var t = i / (double)Rate;
            buf[i] = (float)((Math.Sin(2 * Math.PI * 610 * t) * 0.5 + Math.Sin(2 * Math.PI * 1540 * t) * 0.3 + Math.Sin(2 * Math.PI * 2310 * t) * 0.15)
                * Env(t, 0.001, 0.2) * 0.5);
        }
        return buf;
    }

    private static float[] Door()
    {
        var rng = new Random(15);
        var buf = Buffer(1.3);
        for (var i = 0; i < buf.Length; i++)
        {
            var t = i / (double)Rate;
            var whir = Math.Sin(2 * Math.PI * (160 + t * 120) * t) * 0.3 * Math.Min(1, t * 8) * Math.Min(1, (1.3 - t) * 6);
            var hiss = (rng.NextDouble() * 2 - 1) * Env(t, 0.01, 0.15) * 0.4;
            var thunk = t > 1.1 ? Math.Sin(2 * Math.PI * 70 * (t - 1.1)) * Env(t - 1.1, 0.002, 0.05) : 0;
            buf[i] = (float)(whir + hiss + thunk * 0.8);
        }
        return buf;
    }

    private static float[] Switch()
    {
        var buf = Buffer(0.15);
        for (var i = 0; i < buf.Length; i++)
        {
            var t = i / (double)Rate;
            buf[i] = (float)((Math.Sin(2 * Math.PI * 2400 * t) * Env(t, 0.0005, 0.008) + Math.Sin(2 * Math.PI * 300 * t) * Env(t, 0.001, 0.02)) * 0.6);
        }
        return buf;
    }

    private static float[] Breath()
    {
        var rng = new Random(16);
        var buf = Buffer(2.4);
        double lp = 0;
        for (var i = 0; i < buf.Length; i++)
        {
            var t = i / (double)Rate;
            lp += ((rng.NextDouble() * 2 - 1) - lp) * 0.18;
            var cycle = t % 0.8;
            var env = cycle < 0.35 ? Math.Sin(Math.PI * cycle / 0.35) : Math.Sin(Math.PI * (cycle - 0.35) / 0.45) * 0.8;
            buf[i] = (float)(lp * env * 0.45);
        }
        Crossfade(buf);
        return buf;
    }

    private static float[] Radio()
    {
        var rng = new Random(17);
        var buf = Buffer(0.9);
        for (var i = 0; i < buf.Length; i++)
        {
            var t = i / (double)Rate;
            var gate = t < 0.05 || t > 0.85 ? 1.0 : 0.35;
            var noise = (rng.NextDouble() * 2 - 1) * gate * 0.3;
            var voice = t is > 0.08 and < 0.8 ? Math.Sin(2 * Math.PI * (180 + Math.Sin(t * 23) * 40) * t) * Math.Sin(2 * Math.PI * 900 * t) * 0.2 * (0.5 + 0.5 * Math.Sin(t * 31)) : 0;
            buf[i] = (float)((noise + voice) * 0.7);
        }
        return buf;
    }

    private static float[] WrenchDrop()
    {
        var buf = Buffer(1.2);
        foreach (var (start, amp) in new[] { (0.0, 1.0), (0.32, 0.55), (0.55, 0.3), (0.7, 0.15) })
        {
            for (var i = (int)(start * Rate); i < buf.Length; i++)
            {
                var t = i / (double)Rate - start;
                buf[i] += (float)((Math.Sin(2 * Math.PI * 1830 * t) * 0.4 + Math.Sin(2 * Math.PI * 2950 * t) * 0.25 + Math.Sin(2 * Math.PI * 920 * t) * 0.2)
                    * Env(t, 0.0005, 0.12) * amp * 0.5);
            }
        }
        return buf;
    }

    private static float[] Objective()
    {
        var buf = Buffer(0.7);
        for (var i = 0; i < buf.Length; i++)
        {
            var t = i / (double)Rate;
            var a = Math.Sin(2 * Math.PI * 660 * t) * Env(t, 0.005, 0.25);
            var b = t > 0.12 ? Math.Sin(2 * Math.PI * 990 * (t - 0.12)) * Env(t - 0.12, 0.005, 0.3) : 0;
            buf[i] = (float)((a + b) * 0.22);
        }
        return buf;
    }

    private static float[] Tarp()
    {
        var rng = new Random(18);
        var buf = Buffer(0.8);
        double lp = 0;
        for (var i = 0; i < buf.Length; i++)
        {
            var t = i / (double)Rate;
            lp += ((rng.NextDouble() * 2 - 1) - lp) * 0.3;
            buf[i] = (float)(lp * Math.Sin(Math.PI * t / 0.8) * (0.6 + 0.4 * Math.Sin(t * 70)) * 0.6);
        }
        return buf;
    }

    private static float[] GuardSteps()
    {
        var buf = Buffer(1.2);
        var rng = new Random(19);
        for (var s = 0; s < 6; s++)
        {
            var start = (int)(s * 0.2 * Rate);
            double lp = 0;
            for (var j = 0; j < Rate / 8 && start + j < buf.Length; j++)
            {
                var t = j / (double)Rate;
                lp += ((rng.NextDouble() * 2 - 1) - lp) * 0.3;
                buf[start + j] += (float)((Math.Sin(2 * Math.PI * 85 * t) * 0.7 + lp * 0.6) * Env(t, 0.002, 0.03) * 0.6);
            }
        }
        return buf;
    }

    private static void Crossfade(float[] buf)
    {
        var n = Rate / 5;
        for (var i = 0; i < n; i++)
        {
            var w = i / (double)n;
            var end = buf.Length - n + i;
            buf[i] = (float)(buf[i] * w + buf[end] * (1 - w));
        }
        Array.Resize(ref buf, buf.Length);
    }

    private static void WriteWav(string path, float[] samples)
    {
        var peak = samples.Select(Math.Abs).DefaultIfEmpty(1).Max();
        var gain = peak > 0.98f ? 0.98 / peak : 1.0;
        var tmp = path + ".tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
        using (var w = new BinaryWriter(fs))
        {
            var dataBytes = samples.Length * 2;
            w.Write("RIFF"u8.ToArray());
            w.Write(36 + dataBytes);
            w.Write("WAVE"u8.ToArray());
            w.Write("fmt "u8.ToArray());
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(Rate);
            w.Write(Rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write("data"u8.ToArray());
            w.Write(dataBytes);
            foreach (var s in samples)
                w.Write((short)Math.Clamp(s * gain * 32767, -32768, 32767));
        }
        File.Move(tmp, path, overwrite: true);
    }
}
