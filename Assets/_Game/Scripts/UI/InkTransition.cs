using System;
using UnityEngine;
using UnityEngine.UI;

namespace BachYeu {
  /// <summary>
  /// Ink-splash screen transition: a blot of ink spreads from a point until it covers the screen, a paper wash
  /// blooms over it, the change happens underneath (the midpoint callback), then the cover fades away.
  /// One overlay at a time, above every other canvas, survives scene loads.
  /// </summary>
  public class InkTransition : MonoBehaviour {
    static InkTransition _i;
    public static bool Busy => _i;

    RectTransform _wash;
    readonly RectTransform[] _blots = new RectTransform[3];
    static readonly float[] Lag = { 1f, .82f, .9f };
    Image _washImg;
    CanvasGroup _group;
    Action _mid;
    float _t, _in, _out;
    bool _midDone;

    /// <summary>Cover, run <paramref name="midpoint"/>, uncover. <paramref name="from"/>: viewport point (0..1) the ink spreads from.</summary>
    public static void Play(Action midpoint, float inTime = .32f, float outTime = .45f, Vector2? from = null) {
      if (_i) { midpoint?.Invoke(); return; } // already covering: just do it
      var canvas = UiKit.Canvas("InkTransition", 120);
      DontDestroyOnLoad(canvas.gameObject);
      _i = canvas.gameObject.AddComponent<InkTransition>();
      _i.Build((RectTransform)canvas.transform, from ?? new Vector2(.5f, .5f));
      _i._mid = midpoint; _i._in = inTime; _i._out = outTime;
    }

    void Build(RectTransform root, Vector2 from) {
      _group = gameObject.AddComponent<CanvasGroup>();
      _group.blocksRaycasts = true;
      // Three overlapping blots, a little apart and turned, read as one organic splash and build up a dense black.
      for (int i = 0; i < _blots.Length; i++) {
        var off = i == 0 ? Vector2.zero : UnityEngine.Random.insideUnitCircle * 45f;
        _blots[i] = UiKit.InkImage(root, "Ink" + i, "InkDot", from, new Vector2(.5f, .5f), off, new Vector2(200, 200), new Color(.1f, .11f, .11f, 1)).rectTransform;
        _blots[i].localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0, 360f));
        _blots[i].localScale = Vector3.zero;
      }
      _wash = UiKit.InkImage(root, "Wash", "InkDot", from, new Vector2(.5f, .5f), Vector2.zero, new Vector2(200, 200), new Color(.95f, .93f, .88f, 0)).rectTransform; // a paper-colored bloom inside the ink
      _wash.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0, 360f));
      _washImg = _wash.GetComponent<Image>();
      _wash.localScale = Vector3.zero;
    }

    void Update() {
      _t += Time.unscaledDeltaTime;
      if (_t < _in) {
        float x = Mathf.Clamp01(_t / _in), k = x * x * (3f - 2f * x); // slow start so the splash edge is seen
        for (int i = 0; i < _blots.Length; i++) _blots[i].localScale = Vector3.one * Mathf.Clamp01(k / Lag[i]) * 26f; // 200 px x26 covers the screen from any corner
        _wash.localScale = Vector3.one * Mathf.Max(0, k - .55f) / .45f * 24f;
        _washImg.color = new Color(.95f, .93f, .88f, Mathf.Clamp01((k - .55f) * 3f));
        return;
      }
      if (!_midDone) {
        _midDone = true;
        foreach (var b in _blots) b.localScale = Vector3.one * 26f;
        _wash.localScale = Vector3.one * 26f;
        _washImg.color = new Color(.95f, .93f, .88f, 1);
        try { _mid?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
        return;
      }
      float o = (_t - _in) / Mathf.Max(.01f, _out);
      _group.alpha = 1f - Ease(o);
      if (o >= .6f) _group.blocksRaycasts = false;
      if (o >= 1f) { Destroy(gameObject); _i = null; }
    }

    static float Ease(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x) * (1f - x); }

    void OnDestroy() { if (_i == this) _i = null; }
  }
}
