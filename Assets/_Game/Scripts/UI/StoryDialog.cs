using System;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

namespace BachYeu {
  /// <summary>
  /// Story talk on an ink scroll: speaker seal, text typed out, tap to go on.
  /// Pause: alone, the match waits. Modal: blocks taps (after a match). Subtitle: online, small and auto-advancing.
  /// </summary>
  public class StoryDialog : MonoBehaviour {
    public enum Style { Pause, Modal, Subtitle }
    public static StoryDialog Current { get; private set; }

    const float CharsPerSec = 48f;
    // Subtitles stay up long enough to read: a base pause plus time per character.
    static float SubtitleHold(string s) => 1.6f + s.Length / 26f;

    Line[] _lines;
    Style _style;
    Action _done;
    int _i = -1;
    float _t, _shownAt;
    string _full;
    Text _who, _text, _hint;
    Image _seal;
    NetworkRunner _runner;
    bool _paused;

    public static void Show(Line[] lines, Style style, string header = null, Action done = null) {
      if (lines == null || lines.Length == 0) { done?.Invoke(); return; }
      if (Current) Destroy(Current.gameObject);
      SettingsPanel.EnsureEventSystem();
      var canvas = UiKit.Canvas("Story", style == Style.Subtitle ? 30 : 86);
      var d = canvas.gameObject.AddComponent<StoryDialog>();
      d._lines = lines; d._style = style; d._done = done;
      d.Build((RectTransform)canvas.transform, header);
      Current = d;
      if (style == Style.Pause) d.Pause(true);
      d.Next();
    }

    void Pause(bool on) {
      _runner ??= FindAnyObjectByType<NetworkRunner>();
      if (!_runner || !_runner.IsRunning || _runner.GameMode != GameMode.Single || _paused == on) return;
      _paused = on;
      if (on) _runner.SinglePlayerPause(); else _runner.SinglePlayerContinue();
      Time.timeScale = on ? 0 : 1;
      GameInput.ClearAll();
    }

    void OnDestroy() {
      Pause(false);
      if (Current == this) Current = null;
    }

    void Build(RectTransform root, string header) {
      var C = new Vector2(.5f, .5f); var BC = new Vector2(.5f, 0); var TC = new Vector2(.5f, 1); var TL = new Vector2(0, 1); var TR = new Vector2(1, 1);
      bool sub = _style == Style.Subtitle;

      if (!sub) {
        var dim = UiKit.Rect(root, "Dim", C, C, Vector2.zero, Vector2.zero);
        UiKit.Stretch(dim);
        var img = dim.gameObject.AddComponent<Image>();
        img.color = new Color(.12f, .12f, .11f, .5f);
        var tap = dim.gameObject.AddComponent<Button>();
        tap.transition = Selectable.Transition.None;
        tap.onClick.AddListener(Tap);
        if (!string.IsNullOrEmpty(header)) {
          var h = UiKit.LabelAt(root, header, 36, UiKit.InkPaper, BC, BC, new Vector2(0, 334), new Vector2(1300, 52), TextAnchor.MiddleLeft, FontStyle.Bold);
          h.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, .5f);
          h.raycastTarget = false;
        }
        var skip = UiKit.InkButton(root, "Bỏ qua", TR, TR, new Vector2(-28, -24), new Vector2(170, 60), false, Finish, 22);
        skip.name = "Skip";
      }

      // Subtitle sits above the health bar and stays clear of the skill buttons on the right.
      Vector2 size = sub ? new Vector2(800, 112) : new Vector2(1320, 250);
      var box = UiKit.Rect(root, "Scroll", BC, BC, new Vector2(0, sub ? 224 : 36), size);
      var bg = box.gameObject.AddComponent<Image>();
      bg.color = sub ? new Color(.97f, .95f, .9f, .9f) : UiKit.InkPaper;
      bg.raycastTarget = !sub;
      if (!sub) { var b = box.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.onClick.AddListener(Tap); }
      UiKit.InkImage(box, "Wash", "Wash", C, C, Vector2.zero, size + (sub ? new Vector2(50, 30) : new Vector2(160, 120)), new Color(1, 1, 1, sub ? .45f : .9f)).raycastTarget = false;
      UiKit.InkImage(box, "TopRule", "BrushBar", TC, TC, new Vector2(0, 6), new Vector2(size.x - 40, 8), UiKit.InkBlack).raycastTarget = false;

      _seal = UiKit.SealTag(box, "Who", "", TL, TL, new Vector2(sub ? 18 : 30, sub ? 12 : 22), new Vector2(sub ? 150 : 190, sub ? 40 : 54), UiKit.Seal, true, sub ? 18 : 24);
      _who = _seal.GetComponentInChildren<Text>();
      _text = UiKit.LabelAt(box, "", sub ? 24 : 31, UiKit.InkBlack, TL, TL, new Vector2(sub ? 186 : 30, sub ? -12 : -92), new Vector2(size.x - (sub ? 210 : 60), sub ? 96 : 140), TextAnchor.UpperLeft);
      _text.raycastTarget = false;
      _text.lineSpacing = 1.1f;
      if (!sub) {
        _hint = UiKit.LabelAt(box, "Chạm để tiếp", 18, UiKit.InkGrey, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-28, 16), new Vector2(300, 26), TextAnchor.MiddleRight);
        _hint.raycastTarget = false;
      }
    }

    void Next() {
      _i++;
      if (_i >= _lines.Length) { Finish(); return; }
      var line = _lines[_i];
      var (name, color) = Story.Speaker(line.Who);
      _who.text = name;
      _seal.color = color;
      _full = Story.Fill(line.Text);
      _t = 0;
      _shownAt = Time.unscaledTime;
      _text.text = "";
      _text.fontStyle = line.Who == Story.Narrator ? FontStyle.Italic : FontStyle.Normal;
    }

    bool Typing => _t * CharsPerSec < _full.Length;

    void Tap() {
      if (Time.unscaledTime - _shownAt < .15f) return; // ignore the tap that opened us
      if (Typing) { _t = 999; _text.text = _full; return; }
      Sfx.Play(SoundId.UiClick);
      Next();
    }

    void Finish() {
      var cb = _done;
      Destroy(gameObject);
      cb?.Invoke();
    }

    void Update() {
      if (_full == null) return;
      _t += Time.unscaledDeltaTime;
      int n = Mathf.Min(_full.Length, Mathf.FloorToInt(_t * CharsPerSec));
      if (_text.text.Length != n) _text.text = _full.Substring(0, n);
      if (_hint) _hint.text = _i < _lines.Length - 1 ? "Chạm để tiếp" : "Chạm để đóng";
      if (_style == Style.Subtitle && !Typing && Time.unscaledTime - _shownAt > _full.Length / CharsPerSec + SubtitleHold(_full)) Next();
      var k = UnityEngine.InputSystem.Keyboard.current;
      if (_style != Style.Subtitle && k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame)) Tap();
      if (_style != Style.Subtitle && k != null && k.escapeKey.wasPressedThisFrame) Finish();
    }
  }
}
