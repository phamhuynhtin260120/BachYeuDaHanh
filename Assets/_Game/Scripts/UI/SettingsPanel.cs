using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BachYeu {
  /// <summary>
  /// Cài đặt: music and sound volume, graphics, frame rate, phone vibration, screen shake.
  /// Built from code on its own overlay canvas; open from the title menu or the pause panel.
  /// </summary>
  public class SettingsPanel : MonoBehaviour {
    public static SettingsPanel Current { get; private set; }

    Action _onClose;
    Button[] _quality, _fps, _vibrate, _shake;
    Text _musicPct, _sfxPct, _inkName;
    Button[] _inkPresets, _skin;
    UnityEngine.UI.Slider _ink;

    public static void Open(Action onClose = null) {
      if (Current) return;
      EnsureEventSystem();
      var canvas = UiKit.Canvas("Settings", 90);
      var p = canvas.gameObject.AddComponent<SettingsPanel>();
      p._onClose = onClose;
      p.Build((RectTransform)canvas.transform);
      Current = p;
      Sfx.Play(SoundId.UiOpen);
    }

    public static void EnsureEventSystem() {
      if (EventSystem.current) return;
      var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
      DontDestroyOnLoad(es);
    }

    void OnDestroy() { if (Current == this) Current = null; }

    public void Close() {
      GameSettings.Save();
      Sfx.Play(SoundId.UiBack);
      var cb = _onClose;
      Destroy(gameObject);
      cb?.Invoke();
    }

    void Build(RectTransform root) {
      var C = new Vector2(.5f, .5f); var TL = new Vector2(0, 1); var TC = new Vector2(.5f, 1); var BC = new Vector2(.5f, 0);

      // Dim the screen and block clicks to whatever is behind.
      var dim = UiKit.Rect(root, "Dim", C, C, Vector2.zero, Vector2.zero);
      UiKit.Stretch(dim);
      var dimImg = dim.gameObject.AddComponent<Image>();
      dimImg.color = new Color(.15f, .15f, .14f, .45f);

      var card = UiKit.Rect(root, "Card", C, C, Vector2.zero, new Vector2(860, 880));

      card.gameObject.AddComponent<GlassModal>(); // dark frosted glass, light text
      var bg = card.gameObject.AddComponent<Image>();
      bg.color = UiKit.InkPaper;
      UiKit.InkImage(card, "Wash", "Wash", C, C, Vector2.zero, new Vector2(1000, 1020), new Color(1, 1, 1, .9f));
      UiKit.InkImage(card, "TopRule", "BrushBar", TC, TC, new Vector2(0, -98), new Vector2(760, 8), UiKit.InkBlack);

      UiKit.LabelAt(card, "Cài đặt", 50, UiKit.InkBlack, TL, TL, new Vector2(52, -26), new Vector2(400, 64), TextAnchor.MiddleLeft, FontStyle.Bold);

      float y = -126, row = 70;
      Label(card, "Nhạc nền", y);
      Slider(card, y, GameSettings.MusicVolume, v => { GameSettings.SetMusic(v); _musicPct.text = Pct(v); }, out _musicPct);
      y -= row;
      Label(card, "Âm thanh", y);
      Slider(card, y, GameSettings.SfxVolume, v => { GameSettings.SetSfx(v); _sfxPct.text = Pct(v); }, out _sfxPct, true);
      y -= row;
      Label(card, "Đồ hoạ", y);
      _quality = Segment(card, y, GameSettings.QualityNames, GameSettings.Quality, i => { GameSettings.SetQuality(i); UiKit.SelectSegment(_quality, i); });
      y -= row;
      Label(card, "Khung hình", y);
      _fps = Segment(card, y, new[] { "30 hình/giây", "60 hình/giây" }, GameSettings.FpsIndex, i => { GameSettings.SetFps(i); UiKit.SelectSegment(_fps, i); });
      y -= row;
      Label(card, "Rung máy", y);
      _vibrate = Segment(card, y, new[] { "Bật", "Tắt" }, GameSettings.Vibrate ? 0 : 1, i => { GameSettings.SetVibrate(i == 0); UiKit.SelectSegment(_vibrate, i); if (i == 0) GameSettings.Buzz(); });
      y -= row;
      Label(card, "Rung màn hình", y);
      _shake = Segment(card, y, new[] { "Bật", "Tắt" }, GameSettings.ScreenShake ? 0 : 1, i => { GameSettings.SetScreenShake(i == 0); UiKit.SelectSegment(_shake, i); });
      y -= row;
      // Nét quái: drag freely, or tap one of the three presets under the slider. Applies live (the match shows behind the panel).
      Label(card, "Nét quái", y);
      _ink = Slider(card, y, GameSettings.InkStyle, v => { GameSettings.SetInkStyle(v); _inkName.text = GameSettings.InkStyleName(v); MarkInkPreset(v); }, out _inkName);
      _inkName.text = GameSettings.InkStyleName(GameSettings.InkStyle);
      _inkPresets = new Button[3];
      for (int i = 0; i < 3; i++) {
        int k = i;
        _inkPresets[i] = UiKit.InkButton(card, GameSettings.InkStyleNames[i], TL, TL, new Vector2(300 + i * 130, y - 58), new Vector2(118, 46), false, () => {
          Sfx.Play(SoundId.UiClick);
          _ink.value = GameSettings.InkStylePresets[k]; // the slider's callback saves and applies it
        }, 20);
        _inkPresets[i].name = "Ink_" + GameSettings.InkStyleNames[i];
      }
      MarkInkPreset(GameSettings.InkStyle);
      y -= 130;
      Label(card, "Giao diện", y);
      _skin = Segment(card, y, GameSettings.SkinNames, GameSettings.Skin, i => {
        if (i == GameSettings.Skin) return;
        GameSettings.SetSkin(i);
        var cb = _onClose; _onClose = null;
        Destroy(gameObject);        // rebuild this panel and the menu behind it in the new skin
        MenuFlow.Rebuild();
        Open(cb);
      });

      UiKit.LabelAt(card, "Đồ hoạ Thấp giúp máy yếu chạy mượt và đỡ nóng máy.", 18, UiKit.InkGrey, BC, BC, new Vector2(-150, 44), new Vector2(520, 30), TextAnchor.MiddleLeft);
      var done = UiKit.InkButton(card, "Xong", BC, BC, new Vector2(270, 24), new Vector2(240, 80), true, Close, 28);
      done.name = "Done";
    }

    static string Pct(float v) => Mathf.RoundToInt(v * 100) + "%";

    void MarkInkPreset(float v) {
      int on = System.Array.FindIndex(GameSettings.InkStylePresets, p => Mathf.Abs(p - v) < .01f);
      UiKit.SelectSegment(_inkPresets, on); // -1 (between presets): none lit
    }

    static void Label(RectTransform card, string text, float y) =>
      UiKit.LabelAt(card, text, 26, UiKit.InkBlack, new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, y), new Vector2(220, 60), TextAnchor.MiddleLeft, FontStyle.Bold);

    Button[] Segment(RectTransform card, float y, string[] names, int selected, Action<int> pick) {
      var row = new Button[names.Length];
      float w = names.Length == 3 ? 160 : 230, gap = 12;
      for (int i = 0; i < names.Length; i++) {
        int k = i;
        row[i] = UiKit.InkButton(card, names[i], new Vector2(0, 1), new Vector2(0, 1), new Vector2(290 + i * (w + gap), y), new Vector2(w, 60), false, () => pick(k), 22);
        row[i].name = "Opt_" + names[i];
      }
      UiKit.SelectSegment(row, selected);
      return row;
    }

    /// <summary>Ink-brush slider (0..1) with a percent label. Sound sliders play a short tick on release so you hear the level.</summary>
    UnityEngine.UI.Slider Slider(RectTransform card, float y, float value, Action<float> changed, out Text pct, bool tickOnRelease = false) {
      var TL = new Vector2(0, 1);
      var rt = UiKit.Rect(card, "Slider", TL, TL, new Vector2(300, y - 10), new Vector2(380, 40));
      var hit = rt.gameObject.AddComponent<Image>();
      hit.color = UiKit.Clear;
      var bar = UiKit.InkImage(rt, "Bar", "BrushBar", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(380, 22), new Color(.15f, .16f, .16f, .25f));
      var fillArea = UiKit.Rect(rt, "FillArea", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
      UiKit.Stretch(fillArea);
      fillArea.offsetMin = new Vector2(0, 9); fillArea.offsetMax = new Vector2(0, -9);
      var fill = UiKit.InkImage(fillArea, "Fill", "BrushBar", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, UiKit.Pine);
      fill.rectTransform.sizeDelta = Vector2.zero;
      var handleArea = UiKit.Rect(rt, "HandleArea", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
      UiKit.Stretch(handleArea);
      handleArea.offsetMin = new Vector2(16, 0); handleArea.offsetMax = new Vector2(-16, 0);
      var handle = UiKit.InkImage(handleArea, "Handle", "InkDot", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, UiKit.InkBlack);
      handle.rectTransform.sizeDelta = new Vector2(40, 8);
      handle.raycastTarget = true;
      var s = rt.gameObject.AddComponent<Slider>();
      s.fillRect = fill.rectTransform;
      s.handleRect = handle.rectTransform;
      s.targetGraphic = handle;
      s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
      s.minValue = 0; s.maxValue = 1;
      s.SetValueWithoutNotify(value);
      pct = UiKit.LabelAt(card, Pct(value), 22, UiKit.InkGrey, TL, TL, new Vector2(700, y), new Vector2(100, 60), TextAnchor.MiddleRight, FontStyle.Bold);
      s.onValueChanged.AddListener(v => changed(v));
      if (tickOnRelease) {
        var trig = rt.gameObject.AddComponent<EventTrigger>();
        var e = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        e.callback.AddListener(_ => Sfx.Play(SoundId.Hit));
        trig.triggers.Add(e);
      }
      _ = bar;
      return s;
    }

    void Update() {
      var k = UnityEngine.InputSystem.Keyboard.current;
      if (k != null && k.escapeKey.wasPressedThisFrame) Close();
    }
  }
}
