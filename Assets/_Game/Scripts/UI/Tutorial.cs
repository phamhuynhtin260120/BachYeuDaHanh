using UnityEngine;
using UnityEngine.UI;

namespace BachYeu {
  /// <summary>
  /// First-match guide: Bà Lồng teaches one control at a time on a small card above the joystick, and a gold ring
  /// pulses around the button to press. Each step finishes when the player actually does it. It never pauses the
  /// match. Runs in night matches (not Tranh Đăng) until finished or skipped; "Học lại" in Cách chơi starts it again.
  /// </summary>
  public class Tutorial : MonoBehaviour {
    class Step {
      public string Title, Text, Keys;
      public string[] Targets;        // HUD element names to ring
    }

    static readonly Step[] Steps = {
      new() { Title = "Đi lại", Text = "Kéo vòng tròn bên trái để đi lại.", Keys = "W A S D hoặc phím mũi tên", Targets = new[] { "JoystickBase" } },
      new() { Title = "Chém", Text = "Giữ nút Chém để đánh yêu quái ở gần.", Keys = "Giữ J", Targets = new[] { "Button_atk" } },
      new() { Title = "Kỹ năng", Text = "Bấm một trong hai nút kỹ năng. Mỗi mặt nạ có hai chiêu riêng, dùng xong phải chờ hồi.", Keys = "K hoặc L", Targets = new[] { "Button_s1", "Button_s2" } },
      new() { Title = "Lướt né", Text = "Bấm Né để lướt nhanh, thoát khỏi vòng đỏ và đòn của yêu quái.", Keys = "Space", Targets = new[] { "Button_dash" } },
      new() { Title = "Đổi mặt nạ", Text = "Bạn mang hai mặt nạ. Bấm Đổi để thay mặt nạ, lúc đổi sẽ tung thêm một đòn đặc biệt.", Keys = "Q", Targets = new[] { "Button_swap" } },
      new() { Title = "Thắp lồng đèn", Text = "Đứng trong vòng vàng quanh lồng đèn để thắp. Thắp đủ 3 ngọn là qua canh giờ đầu tiên.", Keys = null, Targets = new string[0] },
      new() { Title = "Tuyệt kỹ", Text = "Đánh trúng yêu quái để nạp Tuyệt kỹ. Khi đầy, nút lớn sáng lên: bấm để tung chiêu mạnh nhất.", Keys = "U", Targets = new[] { "Button_ult" } },
    };

    // Session progress: a guide cut short by the end of a match picks up at the same step next match.
    static int _step;
    static Tutorial _card;
    public static void ResetSession() { _step = 0; _card = null; }

    /// <summary>Start over from the first step next match (from Cách chơi).</summary>
    public static void Restart() {
      Profile.Data.TutorialDone = false;
      Profile.Save();
      _step = 0;
    }

    public static void Tick(GameDirector dir) {
      if (Profile.Data.TutorialDone || !dir || dir.IsContest) { Hide(); return; }
      bool show = dir.State == RunState.Playing && PlayerAvatar.Local && Hud.I && !StoryDialog.Current;
      if (!show) { Hide(); return; }
      if (!_card) {
        var canvas = UiKit.Canvas("Tutorial", 40);
        _card = canvas.gameObject.AddComponent<Tutorial>();
        _card.Build((RectTransform)canvas.transform);
      }
    }

    static void Hide() { if (_card) Destroy(_card.gameObject); _card = null; }

    // ---------------- card ----------------
    Text _title, _text, _keys, _count;
    Image _bg;
    readonly System.Collections.Generic.List<RectTransform> _rings = new();
    float _moveT, _atkT, _doneT = -1, _finishT = -1;
    bool _ultReady;
    int _shown = -1;

    void Build(RectTransform root) {
      var BL = new Vector2(0, 0); var TL = new Vector2(0, 1); var TR = new Vector2(1, 1);
      var card = UiKit.Rect(root, "Card", BL, BL, new Vector2(24, 286), new Vector2(520, 166));
      _bg = card.gameObject.AddComponent<Image>();
      _bg.color = new Color(.97f, .95f, .9f, .93f);
      _bg.raycastTarget = false;
      UiKit.InkImage(card, "TopRule", "BrushBar", new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, 4), new Vector2(490, 7), UiKit.InkBlack).raycastTarget = false;
      UiKit.SealTag(card, "Who", "Bà Lồng", TL, TL, new Vector2(16, -16), new Vector2(118, 36), UiKit.Seal, true, 17).raycastTarget = false;
      _title = UiKit.LabelAt(card, "", 24, UiKit.InkBlack, TL, TL, new Vector2(146, -14), new Vector2(240, 40), TextAnchor.MiddleLeft, FontStyle.Bold);
      _count = UiKit.LabelAt(card, "", 16, UiKit.InkGrey, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 12), new Vector2(170, 24), TextAnchor.MiddleRight);
      _text = UiKit.LabelAt(card, "", 20, UiKit.InkBlack, TL, TL, new Vector2(18, -60), new Vector2(486, 64), TextAnchor.UpperLeft);
      _keys = UiKit.LabelAt(card, "", 16, UiKit.InkGrey, BL, BL, new Vector2(18, 12), new Vector2(320, 24), TextAnchor.MiddleLeft);
      foreach (var t in new[] { _title, _text, _keys, _count }) t.raycastTarget = false;
      var skip = UiKit.InkButton(card, "Bỏ hướng dẫn", TR, TR, new Vector2(-10, -10), new Vector2(150, 40), false, Skip, 15);
      skip.name = "SkipTutorial";
      GameInput.Used = 0;
      Show();
    }

    void Show() {
      ClearRings();
      if (_step >= Steps.Length) return;
      var s = Steps[_step];
      _shown = _step;
      _title.text = s.Title;
      _title.color = UiKit.InkBlack;
      _text.text = s.Text;
      _keys.text = s.Keys != null && !Application.isMobilePlatform ? "Bàn phím: " + s.Keys : "";
      _count.text = $"Bước {_step + 1}/{Steps.Length}";
      foreach (var name in s.Targets) Ring(name);
      _moveT = _atkT = 0;
      GameInput.Used = 0;
    }

    void Ring(string name) {
      var target = Find(Hud.I.transform, name) as RectTransform;
      if (!target || !target.gameObject.activeInHierarchy) return;
      var size = target.rect.size + new Vector2(48, 48);
      var ring = UiKit.InkImage(target, "TutorialRing", "Enso", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, size, RingColor);
      ring.raycastTarget = false;
      _rings.Add(ring.rectTransform);
    }

    static readonly Color RingColor = Gfx.Hex("#ffcf33");

    static Transform Find(Transform t, string name) {
      if (t.name == name) return t;
      foreach (Transform c in t) { var r = Find(c, name); if (r) return r; }
      return null;
    }

    void ClearRings() {
      foreach (var r in _rings) if (r) Destroy(r.gameObject);
      _rings.Clear();
    }

    void OnDestroy() { ClearRings(); if (_card == this) _card = null; }

    void Skip() {
      Sfx.Play(SoundId.UiBack);
      Finish();
    }

    void Finish() {
      Profile.Data.TutorialDone = true;
      Profile.Save();
      Destroy(gameObject);
    }

    bool StepDone(PlayerAvatar me, GameDirector dir) {
      float dt = Time.deltaTime;
      switch (_step) {
        case 0: if (GameInput.Move.sqrMagnitude > .1f) _moveT += dt; return _moveT > 1.2f;
        case 1: if (GameInput.AttackHeld) _atkT += dt; return _atkT > .6f;
        case 2: return (GameInput.Used & (1 << (int)GameAction.Skill1 | 1 << (int)GameAction.Skill2)) != 0;
        case 3: return (GameInput.Used & 1 << (int)GameAction.Dash) != 0;
        case 4: return (GameInput.Used & 1 << (int)GameAction.Swap) != 0;
        case 5: return dir.LitCount >= 1 || dir.Phase > 0;
        case 6:
          if (me.Ult >= 100) { if (!_ultReady) GameInput.Used = 0; _ultReady = true; }
          _count.text = _ultReady ? "Tuyệt kỹ đã đầy!" : $"Đang nạp {Mathf.FloorToInt(me.Ult)}%";
          return _ultReady && (GameInput.Used & 1 << (int)GameAction.Ult) != 0;
      }
      return false;
    }

    void Update() {
      var me = PlayerAvatar.Local; var dir = GameDirector.Instance;
      if (!me || !dir) return;

      // Pulse the rings.
      float w = .5f + .5f * Mathf.Sin(Time.unscaledTime * 6f);
      foreach (var r in _rings) if (r) {
        r.localScale = Vector3.one * (1f + .1f * w);
        r.GetComponent<Image>().color = Color.Lerp(RingColor, Color.white, w * .5f);
      }

      if (_finishT >= 0) { if (Time.unscaledTime > _finishT) Finish(); return; }
      if (_doneT >= 0) {
        if (Time.unscaledTime < _doneT) return;
        _doneT = -1;
        _step++;
        if (_step >= Steps.Length) {
          ClearRings();
          _title.text = "Xong rồi!";
          _text.text = "Cháu đã biết cách chơi. Xem lại bất cứ lúc nào ở Tạm dừng, mục Cách chơi.";
          _keys.text = ""; _count.text = "";
          _finishT = Time.unscaledTime + 5f;
          return;
        }
        Show();
        return;
      }
      if (_shown != _step) Show();
      if (Time.timeScale == 0) return;
      if (StepDone(me, dir)) {
        Sfx.Play(SoundId.Pickup);
        ClearRings();
        _title.text = "Tốt lắm!";
        _title.color = UiKit.Pine;
        _doneT = Time.unscaledTime + 1f;
      }
    }
  }
}
