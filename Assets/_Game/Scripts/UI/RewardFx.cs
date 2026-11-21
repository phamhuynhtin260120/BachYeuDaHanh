using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BachYeu {
  /// <summary>
  /// Small UI juice for rewards: a number that counts up, a burst of gold seals flying out of a point,
  /// and a pop-in for panels. Runs on unscaled time so it also works while a match is paused.
  /// </summary>
  public class RewardFx : MonoBehaviour {
    static RewardFx _host;
    static RewardFx Host {
      get {
        if (_host) return _host;
        var go = new GameObject("RewardFx");
        DontDestroyOnLoad(go);
        return _host = go.AddComponent<RewardFx>();
      }
    }

    class Count { public Text T; public int From, To; public float Dur, Time; public Func<int, string> Format; }
    class Coin { public RectTransform R; public Image I; public Vector2 V; public float Life, Max, Spin; }
    class Pop { public RectTransform R; public CanvasGroup G; public float Time, Dur; }

    readonly List<Count> _counts = new();
    readonly List<Coin> _coins = new();
    readonly List<Pop> _pops = new();

    /// <summary>Counts <paramref name="label"/> from <paramref name="from"/> to <paramref name="to"/>, writing it with <paramref name="format"/>.</summary>
    public static void CountUp(Text label, int from, int to, Func<int, string> format, float dur = .9f) {
      if (!label) return;
      Host._counts.RemoveAll(c => c.T == label);
      Host._counts.Add(new Count { T = label, From = from, To = to, Dur = dur, Format = format });
      label.text = format(from);
    }

    /// <summary>Gold seals burst out of <paramref name="at"/> (a UI element) and fall away.</summary>
    public static void Burst(RectTransform at, int count = 14) {
      if (!at) return;
      var canvas = at.GetComponentInParent<Canvas>();
      if (!canvas) return;
      var root = (RectTransform)canvas.rootCanvas.transform;
      Vector2 local;
      RectTransformUtility.ScreenPointToLocalPointInRectangle(root, RectTransformUtility.WorldToScreenPoint(null, at.position), null, out local);
      for (int i = 0; i < count; i++) {
        var img = UiKit.InkImage(root, "Coin", i % 3 == 0 ? "Seal" : "InkDot", new Vector2(.5f, .5f), new Vector2(.5f, .5f), local, Vector2.one * UnityEngine.Random.Range(16f, 28f), i % 3 == 0 ? UiKit.Seal : UiKit.Gold);
        img.raycastTarget = false;
        float a = UnityEngine.Random.Range(0f, Mathf.PI * 2);
        float sp = UnityEngine.Random.Range(260f, 520f);
        Host._coins.Add(new Coin { R = img.rectTransform, I = img, V = new Vector2(Mathf.Cos(a), Mathf.Abs(Mathf.Sin(a)) + .4f) * sp, Life = 0, Max = UnityEngine.Random.Range(.7f, 1.1f), Spin = UnityEngine.Random.Range(-360f, 360f) });
      }
      Sfx.Play(SoundId.Pickup);
    }

    /// <summary>Scales a panel up from 92% and fades it in.</summary>
    public static void PopIn(RectTransform panel, float dur = .22f) {
      if (!panel) return;
      var g = panel.GetComponent<CanvasGroup>();
      if (!g) g = panel.gameObject.AddComponent<CanvasGroup>();
      g.alpha = 0;
      panel.localScale = Vector3.one * .92f;
      Host._pops.Add(new Pop { R = panel, G = g, Dur = dur });
    }

    void Update() {
      float dt = Time.unscaledDeltaTime;
      for (int i = _counts.Count - 1; i >= 0; i--) {
        var c = _counts[i];
        if (!c.T) { _counts.RemoveAt(i); continue; }
        c.Time += dt;
        float k = Mathf.Clamp01(c.Time / c.Dur);
        k = 1f - (1f - k) * (1f - k);
        c.T.text = c.Format(Mathf.RoundToInt(Mathf.Lerp(c.From, c.To, k)));
        if (c.Time >= c.Dur) _counts.RemoveAt(i);
      }
      for (int i = _coins.Count - 1; i >= 0; i--) {
        var c = _coins[i];
        c.Life += dt;
        if (!c.R || c.Life >= c.Max) { if (c.R) Destroy(c.R.gameObject); _coins.RemoveAt(i); continue; }
        c.V += new Vector2(0, -1100f) * dt;
        c.R.anchoredPosition += c.V * dt;
        c.R.Rotate(0, 0, c.Spin * dt);
        var col = c.I.color; col.a = 1f - Mathf.Pow(c.Life / c.Max, 2); c.I.color = col;
      }
      for (int i = _pops.Count - 1; i >= 0; i--) {
        var p = _pops[i];
        if (!p.R) { _pops.RemoveAt(i); continue; }
        p.Time += dt;
        float k = Mathf.Clamp01(p.Time / p.Dur);
        float e = 1f - (1f - k) * (1f - k);
        p.G.alpha = e;
        p.R.localScale = Vector3.one * Mathf.Lerp(.92f, 1f, e);
        if (k >= 1f) _pops.RemoveAt(i);
      }
    }
  }
}
