using System;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

namespace BachYeu {
  /// <summary>
  /// In-match menu (Tạm dừng). Playing alone it really pauses the match; online the match keeps running.
  /// Buttons: Tiếp tục, Cài đặt, Rời trận (tap twice to confirm).
  /// </summary>
  public class PausePanel : MonoBehaviour {
    public static PausePanel Current { get; private set; }

    Action _leave;
    NetworkRunner _runner;
    bool _paused;
    GameObject _card;
    Text _leaveLabel;
    float _confirmUntil;

    public static void Toggle(Action leave) { if (Current) Current.Resume(); else Open(leave); }

    public static void Open(Action leave) {
      if (Current || SettingsPanel.Current) return;
      SettingsPanel.EnsureEventSystem();
      var canvas = UiKit.Canvas("Pause", 85);
      var p = canvas.gameObject.AddComponent<PausePanel>();
      p._leave = leave;
      p._runner = FindAnyObjectByType<NetworkRunner>();
      p.Build((RectTransform)canvas.transform);
      Current = p;
      p.SetPaused(true);
      Sfx.Play(SoundId.UiOpen);
    }

    bool Solo => _runner && _runner.IsRunning && _runner.GameMode == GameMode.Single;

    void SetPaused(bool on) {
      if (!Solo || _paused == on) return;
      _paused = on;
      if (on) _runner.SinglePlayerPause(); else _runner.SinglePlayerContinue();
      Time.timeScale = on ? 0 : 1; // particles, animators and camera too
      GameInput.ClearAll();
    }

    void OnDestroy() {
      SetPaused(false);
      if (Current == this) Current = null;
    }

    public void Resume() { Sfx.Play(SoundId.UiBack); Destroy(gameObject); }

    void Build(RectTransform root) {
      var C = new Vector2(.5f, .5f); var TC = new Vector2(.5f, 1);
      var dim = UiKit.Rect(root, "Dim", C, C, Vector2.zero, Vector2.zero);
      UiKit.Stretch(dim);
      dim.gameObject.AddComponent<Image>().color = new Color(.15f, .15f, .14f, .45f);

      var card = UiKit.Rect(root, "Card", C, C, Vector2.zero, new Vector2(560, 650));

      card.gameObject.AddComponent<GlassModal>(); // dark frosted glass, light text
      _card = card.gameObject;
      card.gameObject.AddComponent<Image>().color = UiKit.InkPaper;
      UiKit.InkImage(card, "Wash", "Wash", C, C, Vector2.zero, new Vector2(700, 790), new Color(1, 1, 1, .9f));
      UiKit.LabelAt(card, Solo ? "Tạm dừng" : "Tuỳ chọn", 48, UiKit.InkBlack, TC, TC, new Vector2(0, -30), new Vector2(500, 64), TextAnchor.MiddleCenter, FontStyle.Bold);
      UiKit.LabelAt(card, Solo ? "Trận đấu đang đứng yên" : "Chơi online: trận vẫn tiếp diễn", 20, UiKit.InkGrey, TC, TC, new Vector2(0, -96), new Vector2(500, 30), TextAnchor.MiddleCenter);
      UiKit.InkImage(card, "Rule", "BrushBar", TC, TC, new Vector2(0, -134), new Vector2(420, 8), UiKit.Seal);

      UiKit.InkButton(card, "Tiếp tục", TC, TC, new Vector2(0, -166), new Vector2(420, 96), true, Resume, 30).name = "Resume";
      UiKit.InkButton(card, "Cài đặt", TC, TC, new Vector2(0, -280), new Vector2(380, 76), false, OpenSettings, 26).name = "Settings";
      UiKit.InkButton(card, "Cách chơi", TC, TC, new Vector2(0, -372), new Vector2(380, 76), false, OpenHelp, 26).name = "Help";
      var leave = UiKit.InkButton(card, "Rời trận", TC, TC, new Vector2(0, -464), new Vector2(380, 76), false, Leave, 26);
      leave.name = "Leave";
      _leaveLabel = leave.GetComponentInChildren<Text>();
      UiKit.LabelAt(card, "Esc: đóng bảng này", 17, UiKit.InkGrey, TC, TC, new Vector2(0, -566), new Vector2(500, 26), TextAnchor.MiddleCenter);
    }

    void OpenSettings() {
      _card.SetActive(false);
      SettingsPanel.Open(() => { if (this) _card.SetActive(true); });
    }

    void OpenHelp() {
      _card.SetActive(false);
      HelpPanel.Open(() => { if (this) _card.SetActive(true); });
    }

    void Leave() {
      if (Time.unscaledTime > _confirmUntil) {
        _confirmUntil = Time.unscaledTime + 3f;
        _leaveLabel.text = "Chạm lần nữa để rời";
        _leaveLabel.color = UiKit.Seal;
        return;
      }
      SetPaused(false);
      var cb = _leave;
      Destroy(gameObject);
      cb?.Invoke();
    }

    void Update() {
      if (_leaveLabel && _confirmUntil > 0 && Time.unscaledTime > _confirmUntil) {
        _confirmUntil = 0;
        _leaveLabel.text = "Rời trận";
        _leaveLabel.color = UiKit.InkBlack;
      }
      var k = UnityEngine.InputSystem.Keyboard.current;
      if (k != null && k.escapeKey.wasPressedThisFrame && !SettingsPanel.Current && !HelpPanel.Current) Resume();
    }
  }
}
