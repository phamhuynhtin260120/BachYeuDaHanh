using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering.Universal;
using UnityEngine.Timeline;
using UnityEngine.UI;

namespace BachYeu {
  /// <summary>
  /// Plays one of the game's short films (Timeline assets in Resources/Cinematics: Trailer, Win, Lose).
  /// It brings its own camera, cinema bars, captions, ink fade and a "Bỏ qua" button, hides the game UI while it
  /// runs, and puts everything back when the film ends or is skipped. The tracks (CineShot, CineCaption, CineFx,
  /// CineProp, CineFade) read the camera and anchor from here, so the same film works anywhere: the anchor is the
  /// arena center for the trailer and the local player for Win / Lose.
  /// Edit the films in the Timeline window; rebuild the defaults from the menu "Bách Yêu > Tạo lại phim Timeline".
  /// </summary>
  public class Cinematic : MonoBehaviour {
    public const string Folder = "Cinematics/";
    static Cinematic _i;
    public static bool Playing => _i;
    public static Camera Cam => _i ? _i._cam : null;

    PlayableDirector _director;
    Camera _cam, _mainCam;
    Vector3 _anchor;
    Quaternion _yaw;
    Action _done;
    readonly Dictionary<string, string> _tokens = new();
    readonly List<Canvas> _hidden = new();
    bool _finished;

    // overlay
    Image _fade;
    RectTransform _barTop, _barBottom;
    readonly Dictionary<CaptionStyle, (CanvasGroup g, Text title, Text sub, RectTransform root)> _caps = new();

    /// <summary>
    /// Plays Resources/Cinematics/<paramref name="name"/>. Returns false (and calls <paramref name="done"/> at once) if the
    /// film is missing, so callers can always chain on it.
    /// </summary>
    public static bool Play(string name, Vector3 anchor, float yaw, Action done, params (string key, string value)[] tokens) {
      var asset = Resources.Load<TimelineAsset>(Folder + name);
      if (!asset || _i) { done?.Invoke(); return false; }
      var go = new GameObject("Cinematic_" + name);
      _i = go.AddComponent<Cinematic>();
      _i._anchor = anchor;
      _i._yaw = Quaternion.Euler(0, yaw, 0);
      _i._done = done;
      foreach (var (k, v) in tokens) _i._tokens["{" + k + "}"] = v;
      _i.Begin(asset);
      return true;
    }

    public static void Skip() { if (_i) _i.Finish(); }

    public static Vector3 World(Vector3 offset) => _i ? _i._anchor + _i._yaw * offset : offset;

    public static string Fill(string s) {
      if (string.IsNullOrEmpty(s) || !_i) return s ?? "";
      foreach (var kv in _i._tokens) s = s.Replace(kv.Key, kv.Value);
      return s;
    }

    // ---------------- lifecycle ----------------
    void Begin(TimelineAsset asset) {
      _mainCam = Camera.main;
      var camGo = new GameObject("CineCamera");
      camGo.transform.SetParent(transform, false);
      _cam = camGo.AddComponent<Camera>();
      if (_mainCam) {
        _cam.CopyFrom(_mainCam);
        _cam.depth = _mainCam.depth + 5;
        _mainCam.enabled = false;
      }
      _cam.nearClipPlane = .1f; _cam.farClipPlane = 200f;
      var data = camGo.AddComponent<UniversalAdditionalCameraData>();
      data.renderPostProcessing = true;
      camGo.tag = "MainCamera";
      camGo.transform.position = World(new Vector3(0, 6, -10));
      camGo.transform.LookAt(World(Vector3.zero));

      foreach (var c in FindObjectsByType<Canvas>())
        if (c.isRootCanvas && c.enabled && c.name != "InkTransition" && c.name != "Loading") { c.enabled = false; _hidden.Add(c); }
      BuildOverlay();
      GameInput.ClearAll();

      _director = gameObject.AddComponent<PlayableDirector>();
      _director.playOnAwake = false;
      _director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
      _director.extrapolationMode = DirectorWrapMode.None;
      _director.playableAsset = asset;
      _director.stopped += _ => Finish();
      _director.Play();
    }

    void Finish() {
      if (_finished) return;
      _finished = true;
      if (_director) _director.Stop();
      foreach (var c in _hidden) if (c) c.enabled = true;
      if (_mainCam) _mainCam.enabled = true;
      var done = _done;
      _i = null;
      Destroy(gameObject);
      done?.Invoke();
    }

    void OnDestroy() {
      if (_i == this) { _i = null; foreach (var c in _hidden) if (c) c.enabled = true; if (_mainCam) _mainCam.enabled = true; }
      if (_overlay) Destroy(_overlay);
    }

    void Update() {
      // Cinema bars slide in.
      float k = Mathf.Clamp01((float)(_director ? _director.time : 0) / .4f);
      if (_barTop) { _barTop.sizeDelta = new Vector2(0, 74 * k); _barBottom.sizeDelta = new Vector2(0, 74 * k); }
      var kb = UnityEngine.InputSystem.Keyboard.current;
      if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) Finish();
    }

    // ---------------- overlay ----------------
    GameObject _overlay;

    void BuildOverlay() {
      var canvas = UiKit.Canvas("CinematicOverlay", 110);
      _overlay = canvas.gameObject;
      var root = (RectTransform)canvas.transform;
      var C = new Vector2(.5f, .5f);

      _fade = UiKit.Rect(root, "Fade", C, C, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
      UiKit.Stretch(_fade.rectTransform);
      _fade.color = new Color(0, 0, 0, 0);
      _fade.raycastTarget = false;

      _barTop = Bar(root, "BarTop", new Vector2(0, 1), new Vector2(1, 1));
      _barBottom = Bar(root, "BarBottom", new Vector2(0, 0), new Vector2(1, 0));

      // Lower third: brush bar + title + sub, bottom-left above the bar.
      var lower = UiKit.Rect(root, "Lower", Vector2.zero, Vector2.zero, new Vector2(90, 110), new Vector2(1100, 130));
      UiKit.InkImage(lower, "Brush", "BrushBar", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -4), new Vector2(160, 10), UiKit.Seal).raycastTarget = false;
      var lt = UiKit.LabelAt(lower, "", 40, UiKit.InkPaper, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -20), new Vector2(1100, 56), TextAnchor.MiddleLeft, FontStyle.Bold);
      var ls = UiKit.LabelAt(lower, "", 24, new Color(.9f, .88f, .82f), new Vector2(0, 1), new Vector2(0, 1), new Vector2(2, -86), new Vector2(1100, 34), TextAnchor.MiddleLeft, FontStyle.Italic);
      Shadow(lt); Shadow(ls);
      AddCap(CaptionStyle.Lower, lower, lt, ls);

      // Big: centered title with a sub line.
      var big = UiKit.Rect(root, "Big", C, C, new Vector2(0, 220), new Vector2(1400, 220)); // upper third, clear of faces
      var bt = UiKit.LabelAt(big, "", 80, UiKit.InkPaper, C, C, new Vector2(0, 30), new Vector2(1400, 120), TextAnchor.MiddleCenter, FontStyle.Bold);
      bt.horizontalOverflow = HorizontalWrapMode.Wrap; bt.resizeTextForBestFit = true; bt.resizeTextMinSize = 40; bt.resizeTextMaxSize = 80; // one line, shrink to fit
      var bs = UiKit.LabelAt(big, "", 30, new Color(.95f, .9f, .78f), C, C, new Vector2(0, -60), new Vector2(1400, 44), TextAnchor.MiddleCenter, FontStyle.Bold);
      Shadow(bt); Shadow(bs);
      AddCap(CaptionStyle.Big, big, bt, bs);

      // Card: paper title card with the seal (end of the trailer).
      var card = UiKit.Rect(root, "Card", C, C, Vector2.zero, new Vector2(1000, 420));
      var bg = card.gameObject.AddComponent<Image>(); bg.color = new Color(.95f, .93f, .88f, .96f); bg.raycastTarget = false;
      UiKit.InkImage(card, "Wash", "Wash", C, C, Vector2.zero, new Vector2(1160, 560), new Color(1, 1, 1, .9f)).raycastTarget = false;
      var ct = UiKit.LabelAt(card, "", 96, UiKit.InkBlack, C, C, new Vector2(-20, 40), new Vector2(980, 230), TextAnchor.MiddleCenter, FontStyle.Bold);
      ct.lineSpacing = .85f;
      UiKit.SealTag(card, "Seal", "Dạ", C, C, new Vector2(330, 120), new Vector2(70, 70), UiKit.Seal, true, 32).raycastTarget = false;
      var cs = UiKit.LabelAt(card, "", 26, UiKit.InkGrey, C, C, new Vector2(0, -140), new Vector2(980, 40), TextAnchor.MiddleCenter, FontStyle.Bold);
      AddCap(CaptionStyle.Card, card, ct, cs);

      var skip = UiKit.InkButton(root, "Bỏ qua  ›", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -18), new Vector2(190, 56), false, Skip, 20);
      skip.name = "SkipCinematic";
    }

    static RectTransform Bar(RectTransform root, string name, Vector2 a, Vector2 b) {
      var r = UiKit.Rect(root, name, a, a, Vector2.zero, Vector2.zero);
      r.anchorMin = new Vector2(0, a.y); r.anchorMax = new Vector2(1, a.y); r.pivot = new Vector2(.5f, a.y);
      r.sizeDelta = new Vector2(0, 0);
      var img = r.gameObject.AddComponent<Image>(); img.color = new Color(.05f, .05f, .05f, 1); img.raycastTarget = false;
      return r;
    }

    static void Shadow(Text t) { t.raycastTarget = false; var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, .75f); o.effectDistance = new Vector2(2, -2); }

    void AddCap(CaptionStyle s, RectTransform root, Text title, Text sub) {
      var g = root.gameObject.AddComponent<CanvasGroup>();
      g.alpha = 0; g.blocksRaycasts = false;
      title.raycastTarget = sub.raycastTarget = false;
      _caps[s] = (g, title, sub, root);
    }

    public static void ShowCaption(CaptionStyle s, string title, string sub) {
      if (!_i || !_i._caps.TryGetValue(s, out var c)) return;
      c.title.text = title; c.sub.text = sub;
    }

    public static void HideCaption(CaptionStyle s) { if (_i && _i._caps.TryGetValue(s, out var c)) c.g.alpha = 0; }

    public static void CaptionAlpha(CaptionStyle s, float a, float progress) {
      if (!_i || !_i._caps.TryGetValue(s, out var c)) return;
      c.g.alpha = a;
      float drift = s == CaptionStyle.Lower ? 18f * progress : 0;          // lower thirds glide a little
      float scale = s == CaptionStyle.Lower ? 1f : 1f + .04f * progress;    // titles breathe
      c.root.localScale = Vector3.one * scale;
      if (s == CaptionStyle.Lower) c.title.rectTransform.anchoredPosition = new Vector2(drift, -20);
    }

    public static void SetFade(Color c) { if (_i && _i._fade) _i._fade.color = c; }

    // ---------------- props ----------------
    public static Transform SpawnProp(PropKind kind, int id, Vector3 offset, float yaw, float scale) {
      if (!_i) return null;
      var root = new GameObject("Prop").transform;
      root.SetParent(_i.transform, false);
      root.position = World(offset);
      root.rotation = _i._yaw * Quaternion.Euler(0, yaw, 0);
      root.localScale = Vector3.zero;
      if (kind == PropKind.Yokai) {
        var type = (EnemyType)Mathf.Clamp(id, 0, 255);
        var (prefab, extra) = ArtSet.Current ? ArtSet.Current.EnemyPrefab(type) : (null, 1f);
        if (prefab) AnimUtil.SpawnVisual(prefab, root).transform.localScale *= extra;
        else Models.BuildEnemy(root, type, new List<Material>());
      } else {
        int m = Mathf.Clamp(id, 0, Masks.All.Length - 1);
        var prefab = ArtSet.Current ? ArtSet.Current.PlayerModel(m) : null;
        if (prefab) {
          var v = AnimUtil.SpawnVisual(prefab, root);
          var anim = v ? v.GetComponentInChildren<Animator>() : null;
          if (anim) { anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; anim.applyRootMotion = false; }
        } else Models.BuildChibi(root, Masks.All[m].Color, Gfx.Hex("#2b2420"));
      }
      return root;
    }
  }
}
