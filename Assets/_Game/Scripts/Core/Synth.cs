using System;
using UnityEngine;

namespace BachYeu {
  /// <summary>
  /// Placeholder sounds made from code: plucked đàn tranh, trống, mõ, chuông, gió.
  /// Used for any sound or music that has no clip in Resources/SoundSet.asset. Pure math (thread-safe) except Clip().
  /// </summary>
  public static class Synth {
    public const int Rate = 22050;

    // Vietnamese pentatonic (điệu Bắc: Hò Xự Xang Xê Cống ~ C D F G A), as semitones from the root.
    static readonly int[] Bac = { 0, 2, 5, 7, 9 };
    // Darker set for the boss (điệu Nam flavour: C Eb F G Bb).
    static readonly int[] Nam = { 0, 3, 5, 7, 10 };

    public static AudioClip Clip(string name, float[] data) {
      var c = AudioClip.Create(name, data.Length, 1, Rate, false);
      c.SetData(data, 0);
      return c;
    }

    static float[] Buf(float seconds) => new float[Mathf.Max(1, (int)(seconds * Rate))];
    static float Hz(float midi) => 440f * Mathf.Pow(2f, (midi - 69f) / 12f);
    static float Midi(int root, int[] scale, int degree) {
      int oct = Mathf.FloorToInt(degree / (float)scale.Length);
      int i = degree - oct * scale.Length;
      return root + oct * 12 + scale[i];
    }

    // ---------- building blocks (all add into buf at offset `at` seconds) ----------

    /// <summary>Plucked string: decaying harmonics, optional bend up from a step below (nhấn) and late vibrato (rung).</summary>
    static void Pluck(float[] b, float at, float hz, float amp, float dur = 1.4f, float bend = 0, float vib = 0) {
      int s0 = (int)(at * Rate), n = (int)(dur * Rate);
      double ph1 = 0, ph2 = 0, ph3 = 0, ph4 = 0;
      for (int i = 0; i < n && s0 + i < b.Length; i++) {
        if (s0 + i < 0) continue;
        float t = i / (float)Rate;
        float f = hz;
        if (bend > 0) f *= Mathf.Pow(2f, -bend / 12f * Mathf.Exp(-t * 14f));
        if (vib > 0 && t > .18f) f *= 1 + vib * Mathf.Sin(t * 2 * Mathf.PI * 5.5f) * Mathf.Min(1, (t - .18f) * 3);
        double w = 2 * Math.PI * f / Rate;
        ph1 += w; ph2 += w * 2; ph3 += w * 3; ph4 += w * 4.02;
        float atk = Mathf.Min(1, t * 400);
        float v = (float)(Math.Sin(ph1) * Math.Exp(-t * 2.6) + .5 * Math.Sin(ph2) * Math.Exp(-t * 5) +
                          .28 * Math.Sin(ph3) * Math.Exp(-t * 8) + .14 * Math.Sin(ph4) * Math.Exp(-t * 13));
        b[s0 + i] += v * amp * atk;
      }
    }

    /// <summary>Bell / chuông: inharmonic partials with long, staggered decays.</summary>
    static void Bell(float[] b, float at, float hz, float amp, float dur = 1.6f) {
      float[] r = { 1f, 2.01f, 2.76f, 4.07f, 5.43f };
      float[] a = { 1f, .55f, .4f, .22f, .12f };
      float[] d = { 1.6f, 2.4f, 3.6f, 5f, 7f };
      int s0 = (int)(at * Rate), n = (int)(dur * Rate);
      for (int i = 0; i < n && s0 + i < b.Length; i++) {
        if (s0 + i < 0) continue;
        float t = i / (float)Rate, v = 0;
        for (int k = 0; k < r.Length; k++) v += a[k] * Mathf.Sin(2 * Mathf.PI * hz * r[k] * t) * Mathf.Exp(-t * d[k]);
        b[s0 + i] += v * amp * Mathf.Min(1, t * 600) * .5f;
      }
    }

    /// <summary>Gong: low inharmonic swell with a slow shimmer.</summary>
    static void Gong(float[] b, float at, float hz, float amp, float dur = 2.5f) {
      float[] r = { 1f, 1.48f, 2.15f, 2.93f, 3.7f };
      int s0 = (int)(at * Rate), n = (int)(dur * Rate);
      for (int i = 0; i < n && s0 + i < b.Length; i++) {
        if (s0 + i < 0) continue;
        float t = i / (float)Rate, v = 0;
        for (int k = 0; k < r.Length; k++)
          v += Mathf.Sin(2 * Mathf.PI * hz * r[k] * t * (1 + .004f * Mathf.Sin(t * 3 + k))) * Mathf.Exp(-t * (1.1f + k * .5f)) / (1 + k * .6f);
        float env = Mathf.Min(1, t * 60) * (1 - Mathf.Exp(-t * 30));
        b[s0 + i] += v * amp * env * .45f;
      }
    }

    /// <summary>Drum / trống: sine with a fast pitch drop plus a soft noise slap.</summary>
    static void Drum(float[] b, float at, float hz0, float hz1, float amp, float dur, System.Random rng) {
      int s0 = (int)(at * Rate), n = (int)(dur * Rate);
      double ph = 0; float lp = 0;
      for (int i = 0; i < n && s0 + i < b.Length; i++) {
        if (s0 + i < 0) continue;
        float t = i / (float)Rate;
        float f = hz1 + (hz0 - hz1) * Mathf.Exp(-t * 28);
        ph += 2 * Math.PI * f / Rate;
        lp += ((float)rng.NextDouble() * 2 - 1 - lp) * .25f;
        float v = (float)Math.Sin(ph) * Mathf.Exp(-t * (5f / dur)) + lp * .6f * Mathf.Exp(-t * 60);
        b[s0 + i] += v * amp;
      }
    }

    /// <summary>Wood block / mõ: short knock.</summary>
    static void Wood(float[] b, float at, float hz, float amp) {
      int s0 = (int)(at * Rate), n = (int)(.09f * Rate);
      for (int i = 0; i < n && s0 + i < b.Length; i++) {
        if (s0 + i < 0) continue;
        float t = i / (float)Rate;
        b[s0 + i] += (Mathf.Sin(2 * Mathf.PI * hz * t) + .4f * Mathf.Sin(2 * Mathf.PI * hz * 2.7f * t)) * Mathf.Exp(-t * 55) * amp;
      }
    }

    /// <summary>Filtered noise with a band sweep from f0 to f1: whoosh, wind, water, gravel.</summary>
    static void Noise(float[] b, float at, float dur, float f0, float f1, float amp, float attack, System.Random rng, float q = .5f, float grain = 0) {
      int s0 = (int)(at * Rate), n = (int)(dur * Rate);
      float low = 0, band = 0, g = 1;
      for (int i = 0; i < n && s0 + i < b.Length; i++) {
        if (s0 + i < 0) continue;
        float t = i / (float)Rate, k = i / (float)n;
        float fc = Mathf.Lerp(f0, f1, k);
        float f = 2 * Mathf.Sin(Mathf.PI * Mathf.Min(fc, Rate * .2f) / Rate);
        float x = (float)rng.NextDouble() * 2 - 1;
        low += f * band; float high = x - low - q * band; band += f * high;
        if (grain > 0 && rng.NextDouble() < grain) g = .2f + (float)rng.NextDouble();
        float env = Mathf.Min(1, t / Mathf.Max(.001f, attack)) * Mathf.Pow(1 - k, 1.6f);
        b[s0 + i] += band * amp * env * (grain > 0 ? g : 1);
      }
    }

    /// <summary>Sine sweep (chirp).</summary>
    static void Chirp(float[] b, float at, float dur, float f0, float f1, float amp) {
      int s0 = (int)(at * Rate), n = (int)(dur * Rate);
      double ph = 0;
      for (int i = 0; i < n && s0 + i < b.Length; i++) {
        if (s0 + i < 0) continue;
        float k = i / (float)n;
        ph += 2 * Math.PI * Mathf.Lerp(f0, f1, k) / Rate;
        b[s0 + i] += (float)Math.Sin(ph) * amp * Mathf.Sin(Mathf.PI * k);
      }
    }

    static float[] Finish(float[] b, float gain = 1f, bool fadeEnd = true) {
      float peak = 0;
      foreach (var v in b) peak = Mathf.Max(peak, Mathf.Abs(v));
      float k = peak > 0 ? gain * .9f / peak : 1;
      for (int i = 0; i < b.Length; i++) b[i] = (float)Math.Tanh(b[i] * k * 1.2f) / 1.05f;
      int fade = fadeEnd ? Mathf.Min(b.Length, 200) : 0;
      for (int i = 0; i < fade; i++) b[b.Length - 1 - i] *= i / (float)fade;
      return b;
    }

    // ---------- sound effects ----------

    public static float[] Make(SoundId id, int seed = 1) {
      var r = new System.Random(seed * 7919 + (int)id);
      float[] b;
      switch (id) {
        case SoundId.UiClick: b = Buf(.1f); Wood(b, 0, 1350, 1); return Finish(b, .6f);
        case SoundId.UiBack: b = Buf(.1f); Wood(b, 0, 900, 1); return Finish(b, .55f);
        case SoundId.UiOpen: b = Buf(.8f); Pluck(b, 0, Hz(76), .7f, .8f); Pluck(b, .07f, Hz(81), .6f, .7f); return Finish(b, .55f);
        case SoundId.Swing: b = Buf(.24f); Noise(b, 0, .24f, 700, 2600, 1, .05f, r, .35f); return Finish(b, .7f);
        case SoundId.SwingBig: b = Buf(.38f); Noise(b, 0, .38f, 450, 2200, 1, .07f, r, .3f); Drum(b, .02f, 140, 60, .4f, .2f, r); return Finish(b, .8f);
        case SoundId.Hit: b = Buf(.16f); Drum(b, 0, 190, 70, 1, .14f, r); Noise(b, 0, .06f, 2500, 1200, .5f, .002f, r); return Finish(b, .75f);
        case SoundId.HitBig: b = Buf(.32f); Drum(b, 0, 130, 45, 1, .3f, r); Noise(b, 0, .12f, 1800, 600, .7f, .002f, r); return Finish(b, .9f);
        case SoundId.EnemySpawn: b = Buf(.7f); Noise(b, 0, .7f, 200, 900, .6f, .45f, r, .25f); Chirp(b, .1f, .55f, 330, 200, .35f); return Finish(b, .5f);
        case SoundId.EnemyDeath: b = Buf(.45f); Noise(b, 0, .4f, 1400, 300, .8f, .003f, r, .4f); Chirp(b, 0, .35f, 520, 140, .45f); return Finish(b, .7f);
        case SoundId.BossSpawn: b = Buf(3f); Drum(b, 0, 90, 38, 1, .9f, r); Gong(b, .05f, 62, 1.1f, 2.9f); Drum(b, .45f, 90, 38, .7f, .7f, r); return Finish(b, 1f);
        case SoundId.BossDeath: b = Buf(3.2f); Gong(b, 0, 82, 1, 3.1f); Bell(b, .35f, Hz(84), .5f, 2.6f); Bell(b, .7f, Hz(88), .4f, 2.4f); return Finish(b, 1f);
        case SoundId.Fire:
          b = Buf(.6f); Noise(b, 0, .55f, 300, 1500, .9f, .04f, r, .5f);
          for (int i = 0; i < 9; i++) Noise(b, (float)r.NextDouble() * .45f, .03f, 3000, 2000, .5f, .002f, r);
          return Finish(b, .75f);
        case SoundId.Wind: b = Buf(.9f); Noise(b, 0, .9f, 380, 1300, 1, .3f, r, .18f); Noise(b, .1f, .7f, 900, 500, .5f, .2f, r, .2f); return Finish(b, .7f);
        case SoundId.Water:
          b = Buf(.6f); Noise(b, 0, .45f, 600, 1800, .4f, .02f, r, .6f);
          for (int i = 0; i < 6; i++) { float f = 280 + (float)r.NextDouble() * 320; Chirp(b, (float)r.NextDouble() * .4f, .07f, f, f * 2.4f, .45f); }
          return Finish(b, .7f);
        case SoundId.Stone: b = Buf(.55f); Drum(b, 0, 100, 40, 1, .35f, r); Noise(b, .01f, .5f, 900, 300, .7f, .005f, r, .6f, .02f); return Finish(b, .9f);
        case SoundId.Swap: b = Buf(.9f); Bell(b, 0, Hz(79), .7f, .9f); Bell(b, .06f, Hz(86), .5f, .8f); Noise(b, 0, .25f, 1200, 2600, .3f, .05f, r, .3f); return Finish(b, .6f);
        case SoundId.Dash: b = Buf(.17f); Noise(b, 0, .17f, 900, 3000, 1, .02f, r, .35f); return Finish(b, .55f);
        case SoundId.Blink: b = Buf(.5f); Noise(b, 0, .2f, 3000, 900, .6f, .01f, r, .3f); Bell(b, .04f, Hz(93), .5f, .45f); return Finish(b, .6f);
        case SoundId.Ult: b = Buf(2.2f); Drum(b, 0, 95, 40, 1, .6f, r); Gong(b, .02f, 98, 1, 2.1f); Bell(b, .1f, Hz(81), .35f, 1.6f); return Finish(b, 1f);
        case SoundId.Shield: b = Buf(1.1f); Bell(b, 0, Hz(72), .6f, 1f); Bell(b, .03f, Hz(79), .5f, 1f); Bell(b, .06f, Hz(84), .4f, 1f); return Finish(b, .65f);
        case SoundId.Heal: b = Buf(1f); for (int i = 0; i < 4; i++) Pluck(b, i * .08f, Hz(Midi(72, Bac, i * 1 + 1)), .6f, .8f); return Finish(b, .6f);
        case SoundId.Leap: b = Buf(.4f); Drum(b, 0, 120, 45, 1, .3f, r); Noise(b, 0, .25f, 600, 200, .5f, .004f, r, .5f); return Finish(b, .85f);
        case SoundId.Hurt: b = Buf(.2f); Drum(b, 0, 150, 60, .9f, .18f, r); Noise(b, 0, .08f, 1500, 500, .6f, .002f, r); return Finish(b, .7f);
        case SoundId.Downed:
          b = Buf(1.6f); int[] dn = { 4, 3, 2, 0 };
          for (int i = 0; i < 4; i++) Pluck(b, i * .22f, Hz(Midi(60, Bac, dn[i])), .7f, 1.2f, i == 3 ? 1 : 0, .006f);
          return Finish(b, .7f);
        case SoundId.Revived: b = Buf(1.1f); for (int i = 0; i < 4; i++) Pluck(b, i * .1f, Hz(Midi(67, Bac, i + 1)), .6f, .9f); Bell(b, .4f, Hz(88), .3f, .7f); return Finish(b, .7f);
        case SoundId.Ping: b = Buf(.5f); Bell(b, 0, Hz(93), .7f, .5f); return Finish(b, .45f);
        case SoundId.Warning: b = Buf(.7f); Drum(b, 0, 85, 42, 1, .3f, r); Drum(b, .22f, 85, 42, .8f, .4f, r); return Finish(b, .8f);
        case SoundId.LanternTaken: b = Buf(1.2f); Bell(b, 0, Hz(81), .7f, 1.1f); Bell(b, .1f, Hz(88), .5f, 1f); return Finish(b, .7f);
        case SoundId.LanternLit: b = Buf(1.6f); Bell(b, 0, Hz(72), .6f, 1.5f); Bell(b, .08f, Hz(79), .5f, 1.4f); Bell(b, .16f, Hz(84), .5f, 1.3f); Bell(b, .24f, Hz(88), .4f, 1.2f); return Finish(b, .8f);
        case SoundId.Pickup: b = Buf(.5f); Pluck(b, 0, Hz(79), .6f, .4f); Pluck(b, .06f, Hz(84), .6f, .4f); return Finish(b, .55f);
        case SoundId.Victory: {
          b = Buf(3f); int[] m = { 0, 1, 2, 3, 4, 5, 7 }; float[] tt = { 0, .16f, .32f, .48f, .64f, .8f, 1.1f };
          for (int i = 0; i < m.Length; i++) Pluck(b, tt[i], Hz(Midi(67, Bac, m[i])), .7f, i == m.Length - 1 ? 1.9f : .9f, i == m.Length - 1 ? 2 : 0, i == m.Length - 1 ? .008f : 0);
          Gong(b, 1.1f, 131, .5f, 1.9f); Bell(b, 1.1f, Hz(91), .35f, 1.8f);
          return Finish(b, .9f);
        }
        case SoundId.Defeat: {
          b = Buf(3f); int[] m = { 4, 3, 2, 1, 0 };
          for (int i = 0; i < m.Length; i++) Pluck(b, i * .34f, Hz(Midi(57, Nam, m[i])), .7f, i == m.Length - 1 ? 1.6f : 1.1f, i % 2 == 1 ? 1.5f : 0, .006f);
          Gong(b, 1.36f, 55, .6f, 1.6f);
          return Finish(b, .85f);
        }
        case SoundId.PhaseClear: b = Buf(1.8f); Gong(b, 0, 110, .6f, 1.7f); Bell(b, .1f, Hz(84), .5f, 1.5f); Bell(b, .3f, Hz(91), .4f, 1.3f); return Finish(b, .8f);
        default: b = Buf(.1f); Wood(b, 0, 1000, 1); return Finish(b, .5f);
      }
    }

    // ---------- music ----------

    /// <summary>Seamless loop: plucked đàn tranh melody on the pentatonic scale over a drone; battle adds trống and mõ.</summary>
    public static float[] Music(MusicId id) {
      var rng = new System.Random(id switch { MusicId.Menu => 11, MusicId.Battle => 23, _ => 37 });
      bool menu = id == MusicId.Menu, boss = id == MusicId.Boss;
      float bpm = menu ? 72 : boss ? 112 : 96;
      int bars = menu ? 16 : 16;
      int root = boss ? 50 : 55; // D3 for the boss, G3 otherwise
      int[] scale = boss ? Nam : Bac;
      float beat = 60f / bpm, len = bars * 4 * beat;
      var b = Buf(len + 3f);

      // Drone: root and fifth, slow breathing.
      int n = (int)(len * Rate);
      float f1 = Hz(root - 12), f2 = Hz(root - 5);
      for (int i = 0; i < b.Length; i++) {
        float t = i / (float)Rate;
        float breathe = .6f + .4f * Mathf.Sin(2 * Mathf.PI * t / (beat * 8));
        b[i] += (Mathf.Sin(2 * Mathf.PI * f1 * t) * .09f + Mathf.Sin(2 * Mathf.PI * f2 * t) * .05f) * breathe * (menu ? .9f : .7f);
      }

      // Melody: 4-bar phrases built from a motif, varied on repeat; long notes get nhấn (bend) and rung (vibrato).
      int deg = 5;
      var motif = new int[16]; var rests = new bool[16];
      for (int s = 0; s < 16; s++) { motif[s] = rng.Next(-2, 3); rests[s] = rng.NextDouble() < (menu ? .45 : .3); }
      float step = beat / 2;
      for (int bar = 0; bar < bars; bar++) {
        bool vary = (bar / 4) % 2 == 1;
        for (int s = 0; s < 8; s++) {
          int idx = (bar % 2) * 8 + s;
          if (rests[idx] && !(s == 0 && bar % 4 == 0)) continue;
          int mv = motif[idx] + (vary && rng.NextDouble() < .35 ? rng.Next(-1, 2) : 0);
          deg = Mathf.Clamp(deg + mv, 2, 10);
          if (bar % 4 == 3 && s >= 6) deg = s == 7 ? 5 : deg; // phrase ends home
          float at = (bar * 8 + s) * step;
          bool longNote = s == 0 || (s == 4 && rng.NextDouble() < .5);
          float hz = Hz(Midi(root + 12, scale, deg));
          Pluck(b, at, hz, longNote ? .5f : .38f, longNote ? 2.2f : 1.2f, longNote && rng.NextDouble() < .5 ? 2 : 0, longNote ? .007f : 0);
          if (menu && longNote && rng.NextDouble() < .4) Pluck(b, at + .02f, hz / 2, .2f, 2f);
        }
        // Bass pluck on each bar.
        Pluck(b, bar * 4 * beat, Hz(Midi(root - 12, scale, bar % 4 == 2 ? 3 : 0)), .35f, 2.5f);
      }

      // Percussion for battle / boss: trống on 1 and 3 (every beat for the boss), mõ on the off-beats.
      if (!menu) {
        for (int k = 0; k < bars * 4; k++) {
          float at = k * beat;
          if (boss || k % 2 == 0) Drum(b, at, boss ? 80 : 95, 42, boss ? .55f : .45f, .45f, rng);
          if (k % 4 == 3 && !boss) Drum(b, at + beat / 2, 95, 42, .3f, .3f, rng);
          Wood(b, at + beat / 2, 1150, .16f);
          if (boss && k % 8 == 0) Gong(b, at, 73, .35f, 2.5f);
        }
      } else {
        for (int k = 0; k < bars; k += 4) Bell(b, k * 4 * beat, Hz(Midi(root + 24, scale, 0)), .14f, 2.5f);
      }

      // Wrap the tail into the start so the loop is seamless, then trim to the loop length.
      var outp = new float[n];
      for (int i = 0; i < b.Length; i++) outp[i % n] += b[i];
      return Finish(outp, .8f, false);
    }
  }
}
