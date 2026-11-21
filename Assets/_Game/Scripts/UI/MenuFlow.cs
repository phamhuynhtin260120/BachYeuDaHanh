using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BachYeu {
  /// <summary>
  /// Everything before a match, as one flow of screens on a single canvas:
  ///   Màn khởi động (logo, "Chạm để bắt đầu", name on first run) → Sảnh chính (hub) →
  ///   Hành trình map / Tranh Đăng / Chơi cùng bạn → Phòng (mode, season, room code) → chọn tướng → loading → match.
  /// The start screen shows once per app session; coming back from a match lands on the hub.
  /// The secondary screens (Nhiệm vụ, Túi pháp khí, Bách yêu lục, Thành tựu, Cách chơi, Cài đặt) open on top of the hub.
  /// </summary>
  public class MenuFlow : MonoBehaviour {
    public static MenuFlow Current { get; private set; }
    static bool _startSeen;
    public static void ResetSession() => _startSeen = false;

    GameLauncher _launcher;
    RectTransform _root, _start, _hub, _room;

    public static void Show(GameLauncher launcher) {
      if (Current) Destroy(Current.gameObject);
      SettingsPanel.EnsureEventSystem();
      var canvas = UiKit.Canvas("MenuFlow", 20);
      var f = canvas.gameObject.AddComponent<MenuFlow>();
      f._launcher = launcher;
      f._root = (RectTransform)canvas.transform;
      Current = f;
      UiKit.Backdrop(f._root, "Backdrop", "MenuBg");
      if (_startSeen) f.GoHub(); else f.GoStart();
      LoadingScreen.Hide(.15f);
    }

    public static void Hide() { if (Current) Destroy(Current.gameObject); }

    void OnDestroy() {
      if (Current == this) Current = null;
      if (_launcher) _launcher.StatusSink = null;
    }

    static readonly Vector2 C = new(.5f, .5f), TL = new(0, 1), TR = new(1, 1), BL = new(0, 0), BR = new(1, 0), L = new(0, .5f), BC = new(.5f, 0), TC = new(.5f, 1);

    RectTransform Screen(string name) {
      var r = UiKit.Rect(_root, name, C, C, Vector2.zero, Vector2.zero);
      UiKit.Stretch(r);
      return r;
    }

    void Clear() {
      foreach (var r in new[] { _start, _hub, _room }) if (r) Destroy(r.gameObject);
      _start = _hub = _room = null;
    }

    // ================= Màn khởi động =================
    Text _tap;
    InputField _nameInput;
    GameObject _namePanel;

    void GoStart() {
      Clear();
      _start = Screen("StartScreen");
      var title = UiKit.LabelAt(_start, "Bách Yêu\nDạ Hành", 150, UiKit.InkBlack, C, C, new Vector2(-40, 90), new Vector2(1100, 360), TextAnchor.MiddleCenter, FontStyle.Bold);
      title.lineSpacing = .85f;
      title.gameObject.AddComponent<Outline>().effectColor = new Color(.97f, .96f, .94f, .85f);
      UiKit.SealTag(_start, "Seal", "Dạ", C, C, new Vector2(360, 190), new Vector2(84, 84), UiKit.Seal, true, 38);
      UiKit.LabelAt(_start, "Đêm hội Hồng Đăng · 1 đến 4 người", 28, UiKit.InkGrey, C, C, new Vector2(0, -120), new Vector2(900, 40), TextAnchor.MiddleCenter, FontStyle.Bold);
      _tap = UiKit.LabelAt(_start, "Chạm để bắt đầu", 34, UiKit.Seal, BC, BC, new Vector2(0, 150), new Vector2(800, 50), TextAnchor.MiddleCenter, FontStyle.Bold);
      UiKit.LabelAt(_start, "Phiên bản " + Application.version + " · Thuỷ mặc phố đèn", 18, UiKit.InkGrey, BR, BR, new Vector2(-30, 20), new Vector2(600, 28), TextAnchor.MiddleRight);
      UiKit.InkButton(_start, "Xem giới thiệu", BL, BL, new Vector2(30, 24), new Vector2(240, 60), false, PlayTrailer, 20).name = "Trailer";
      _idle = 0;
      var hit = _start.gameObject.AddComponent<Image>(); hit.color = new Color(1, 1, 1, 0);
      var b = _start.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None;
      b.onClick.AddListener(OnStartTap);
      Sfx.Play(SoundId.UiOpen);
    }

    float _idle;
    const float TrailerAfterIdle = 25f;

    // The trailer runs over the arena behind the menu; the start screen comes back when it ends.
    void PlayTrailer() {
      if (Cinematic.Playing) return;
      _idle = 0;
      Cinematic.Play("Trailer", Vector3.zero, 0, () => { _idle = 0; });
    }

    void OnStartTap() {
      if (Cinematic.Playing) return;
      if (_namePanel) return;
      Sfx.Play(SoundId.UiClick);
      if (string.IsNullOrWhiteSpace(_launcher.PlayerName)) { AskName(); return; }
      _startSeen = true;
      InkTransition.Play(GoHub);
    }

    void AskName() {
      _tap.enabled = false;
      var p = UiKit.Rect(_start, "NamePanel", BC, BC, new Vector2(0, 110), new Vector2(760, 210));
      _namePanel = p.gameObject;
      p.gameObject.AddComponent<Image>().color = new Color(.98f, .97f, .94f, .95f);
      UiKit.LabelAt(p, "Lần đầu đến phố Hồng Đăng. Bạn tên là gì?", 24, UiKit.InkBlack, TL, TL, new Vector2(30, -16), new Vector2(700, 36), TextAnchor.MiddleLeft, FontStyle.Bold);
      _nameInput = UiKit.InkInput(p, "", TL, TL, new Vector2(30, -62), new Vector2(430, 70), 34);
      _nameInput.characterLimit = 16;
      _nameInput.name = "NameInput";
      UiKit.InkButton(p, "Vào phố", TR, TR, new Vector2(-30, -60), new Vector2(220, 76), true, ConfirmName, 26).name = "ConfirmName";
      UiKit.LabelAt(p, "Tên hiện trong phòng chờ và trong truyện. Đổi được ở sảnh.", 17, UiKit.InkGrey, BL, BL, new Vector2(30, 18), new Vector2(700, 26), TextAnchor.MiddleLeft);
      _nameInput.Select();
    }

    void ConfirmName() {
      var n = _nameInput ? _nameInput.text.Trim() : "";
      if (n.Length == 0) { Sfx.Play(SoundId.UiBack); return; }
      _launcher.PlayerName = n;
      Sfx.Play(SoundId.Pickup);
      _startSeen = true;
      InkTransition.Play(GoHub);
    }

    // ================= Sảnh chính =================
    Text _hubName, _hubTitle, _hubCoins, _journeySub, _hubStatus;
    GameObject _questDot;

    void GoHub() {
      Clear();
      _hub = Screen("Hub");
      _launcher.StatusSink = s => { if (_hubStatus) _hubStatus.text = s; };

      // Logo, top-left.
      var logo = UiKit.LabelAt(_hub, "Bách Yêu Dạ Hành", 60, UiKit.InkBlack, TL, TL, new Vector2(230, -36), new Vector2(640, 80), TextAnchor.MiddleLeft, FontStyle.Bold);
      logo.gameObject.AddComponent<Outline>().effectColor = new Color(.97f, .96f, .94f, .8f);
      UiKit.LabelAt(_hub, "Đêm hội Hồng Đăng", 22, UiKit.InkGrey, TL, TL, new Vector2(234, -112), new Vector2(500, 30), TextAnchor.MiddleLeft, FontStyle.Bold);

      // Profile card, top-right: name (tap to rename), title, Hồn đăng.
      var card = UiKit.Rect(_hub, "Profile", TR, TR, new Vector2(-40, -32), new Vector2(460, 126));
      card.gameObject.AddComponent<Image>().color = new Color(.98f, .97f, .94f, .9f);
      UiKit.InkImage(card, "Rule", "BrushBar", TL, TL, new Vector2(16, -4), new Vector2(428, 6), UiKit.Seal);
      _hubName = UiKit.LabelAt(card, "", 30, UiKit.InkBlack, TL, TL, new Vector2(22, -16), new Vector2(300, 40), TextAnchor.MiddleLeft, FontStyle.Bold);
      _hubTitle = UiKit.LabelAt(card, "", 18, Gfx.Hex("#8a6a2e"), TL, TL, new Vector2(22, -56), new Vector2(300, 26), TextAnchor.MiddleLeft, FontStyle.Bold);
      UiKit.SealTag(card, "Coin", "Hồn", BL, BL, new Vector2(22, 12), new Vector2(46, 36), UiKit.Gold, true, 15);
      _hubCoins = UiKit.LabelAt(card, "", 24, UiKit.InkBlack, BL, BL, new Vector2(76, 10), new Vector2(200, 40), TextAnchor.MiddleLeft, FontStyle.Bold);
      UiKit.InkButton(card, "Đổi tên", TR, TR, new Vector2(-14, -20), new Vector2(120, 48), false, Rename, 18).name = "Rename";

      // Three big mode cards, left column.
      _journeySub = ModeCard(0, "Hành trình", "", "Một mình hoặc cùng bạn · cốt truyện theo chương", true, OpenJourney).Item2;
      ModeCard(1, "Tranh Đăng", "Hai đội giành lồng đèn · 2 đến 4 người", "Tạo phòng rồi gửi mã cho bạn bè", false, () => OpenRoom(Modes.Contest));
      ModeCard(2, "Vào phòng bạn", "Nhập mã phòng bạn bè gửi cho bạn", "Chế độ do chủ phòng chọn", false, () => OpenRoom(-1));
      _hubStatus = UiKit.LabelAt(_hub, "", 20, UiKit.Seal, L, L, new Vector2(76, -330), new Vector2(760, 30), TextAnchor.MiddleLeft, FontStyle.Bold);

      // Bottom bar.
      string[] names = { "Nhiệm vụ", "Túi pháp khí", "Bách yêu lục", "Thành tựu", "Cách chơi", "Cài đặt" };
      System.Action[] acts = {
        () => DailyQuestPanel.Open(RefreshHub), () => BagPanel.Open(RefreshHub), () => BestiaryPanel.Open(RefreshHub),
        () => AchievementPanel.Open(RefreshHub), () => HelpPanel.Open(), () => SettingsPanel.Open(),
      };
      var bar = UiKit.Rect(_hub, "NavBar", BC, BC, new Vector2(0, 22), new Vector2(1340, 80));
      bar.gameObject.AddComponent<Image>().color = new Color(.98f, .97f, .94f, .75f);
      for (int i = 0; i < names.Length; i++) {
        int k = i;
        var b = UiKit.InkButton(bar, names[i], L, L, new Vector2(10 + i * 221, 0), new Vector2(210, 64), false, () => acts[k](), 21);
        b.name = "Nav_" + i;
        if (i == 0) _questDot = UiKit.SealTag(b.transform, "Dot", "!", TR, C, new Vector2(-12, -8), new Vector2(32, 32), UiKit.Seal, true, 19).gameObject;
      }
      RefreshHub();
    }

    (Button, Text) ModeCard(int i, string title, string sub, string hint, bool primary, UnityEngine.Events.UnityAction onClick) {
      var pos = new Vector2(72, 170 - i * 176);
      var card = UiKit.Rect(_hub, "Mode_" + title, L, L, pos, new Vector2(620, 158));
      var bg = card.gameObject.AddComponent<Image>();
      bg.color = primary ? new Color(.15f, .16f, .16f, .97f) : new Color(.98f, .97f, .94f, .9f);
      var btn = card.gameObject.AddComponent<Button>();
      btn.onClick.AddListener(() => { Sfx.Play(SoundId.UiClick); onClick(); });
      var cb = btn.colors; cb.highlightedColor = new Color(.92f, .92f, .92f); cb.pressedColor = new Color(.8f, .8f, .8f); btn.colors = cb;
      Color ink = primary ? UiKit.InkPaper : UiKit.InkBlack, soft = primary ? new Color(.85f, .83f, .78f) : UiKit.InkGrey;
      UiKit.InkImage(card, "Bar", "BrushBar", TL, TL, new Vector2(0, -4), new Vector2(10, 150), primary ? UiKit.Seal : UiKit.Pine).rectTransform.localRotation = Quaternion.identity;
      UiKit.LabelAt(card, title, 44, ink, TL, TL, new Vector2(34, -14), new Vector2(560, 60), TextAnchor.MiddleLeft, FontStyle.Bold).raycastTarget = false;
      var s = UiKit.LabelAt(card, sub, 22, primary ? Gfx.Hex("#e9c46a") : UiKit.Seal, TL, TL, new Vector2(36, -78), new Vector2(560, 32), TextAnchor.MiddleLeft, FontStyle.Bold);
      s.raycastTarget = false;
      UiKit.LabelAt(card, hint, 18, soft, TL, TL, new Vector2(36, -112), new Vector2(560, 28), TextAnchor.MiddleLeft).raycastTarget = false;
      UiKit.LabelAt(card, "›", 64, ink, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-26, 4), new Vector2(50, 90), TextAnchor.MiddleCenter, FontStyle.Bold).raycastTarget = false;
      btn.name = "Mode_" + i;
      return (btn, s);
    }

    void RefreshHub() {
      if (!_hub) return;
      _hubName.text = string.IsNullOrEmpty(_launcher.PlayerName) ? "Khách" : _launcher.PlayerName;
      var t = Profile.Data.Title;
      _hubTitle.text = string.IsNullOrEmpty(t) ? $"Thành tựu {Achievements.DoneCount}/{Achievements.All.Length}" : "« " + t + " »";
      _hubCoins.text = $"{Profile.Coins} Hồn đăng";
      int n = Mathf.Clamp(LocalProfile.Chapter, 1, JourneyMap.FreeNight);
      var ch = Story.Get(n);
      _journeySub.text = ch != null ? $"Chương {n} · {ch.Title}" : "Đêm tự do";
      if (_questDot) _questDot.SetActive(Quests.AnythingToClaim);
    }

    void Rename() {
      var p = UiKit.Rect(_hub, "RenamePanel", C, C, Vector2.zero, new Vector2(700, 230));
      p.gameObject.AddComponent<Image>().color = new Color(.98f, .97f, .94f, .98f);
      UiKit.LabelAt(p, "Đổi tên", 30, UiKit.InkBlack, TL, TL, new Vector2(30, -16), new Vector2(600, 40), TextAnchor.MiddleLeft, FontStyle.Bold);
      var input = UiKit.InkInput(p, _launcher.PlayerName, TL, TL, new Vector2(30, -70), new Vector2(400, 70), 32);
      input.characterLimit = 16;
      UiKit.InkButton(p, "Lưu", TR, TR, new Vector2(-30, -70), new Vector2(200, 72), true, () => {
        if (input.text.Trim().Length == 0) return;
        _launcher.PlayerName = input.text;
        Destroy(p.gameObject);
        RefreshHub();
      }, 24);
      UiKit.InkButton(p, "Huỷ", BR, BR, new Vector2(-30, 16), new Vector2(140, 52), false, () => Destroy(p.gameObject), 20);
    }

    void OpenJourney() => InkTransition.Play(OpenJourneyNow, .28f, .4f, new Vector2(.25f, .6f));

    void OpenJourneyNow() {
      JourneyMap.Open((night, alone) => {
        _launcher.SelectMode(Modes.Journey);
        var ch = Story.Get(night);
        if (ch != null) _launcher.SelectSeason(ch.Season);
        RefreshHub();
        if (alone) _launcher.PlaySolo();
        else OpenRoom(Modes.Journey);
      });
    }

    // ================= Phòng =================
    int _roomMode;   // -1: join a friend's room (their mode)
    Button[] _modeBtns, _seasonBtns;
    GameObject _seasonRow;
    Text _roomStatus, _roomHint;
    InputField _code;

    void OpenRoom(int mode) {
      if (_room) Destroy(_room.gameObject);
      _roomMode = mode;
      if (mode >= 0) _launcher.SelectMode(mode);
      _room = UiKit.Rect(_root, "Room", C, C, Vector2.zero, Vector2.zero);
      UiKit.Stretch(_room);
      _room.gameObject.AddComponent<Image>().color = new Color(.15f, .15f, .14f, .45f);
      var card = UiKit.Rect(_room, "Card", C, C, Vector2.zero, new Vector2(960, 700));
      card.gameObject.AddComponent<Image>().color = UiKit.InkPaper;
      UiKit.InkImage(card, "Wash", "Wash", C, C, Vector2.zero, new Vector2(1120, 840), new Color(1, 1, 1, .9f));
      UiKit.LabelAt(card, mode < 0 ? "Vào phòng bạn" : "Chơi cùng bạn", 46, UiKit.InkBlack, TL, TL, new Vector2(48, -26), new Vector2(700, 60), TextAnchor.MiddleLeft, FontStyle.Bold);
      UiKit.InkImage(card, "Rule", "BrushBar", TL, TL, new Vector2(40, -96), new Vector2(880, 8), UiKit.InkBlack);

      float y = -122;
      if (mode >= 0) {
        UiKit.LabelAt(card, "Chế độ", 20, UiKit.InkGrey, TL, TL, new Vector2(50, y), new Vector2(300, 28), TextAnchor.MiddleLeft, FontStyle.Bold);
        _modeBtns = new Button[2];
        int night = Mathf.Clamp(LocalProfile.Chapter, 1, JourneyMap.FreeNight);
        string jn = Story.Get(night) != null ? $"Hành trình · Chương {night}" : "Hành trình · Đêm tự do";
        _modeBtns[0] = UiKit.InkButton(card, jn, TL, TL, new Vector2(48, y - 34), new Vector2(420, 66), false, () => { _launcher.SelectMode(Modes.Journey); PaintRoom(); }, 22);
        _modeBtns[1] = UiKit.InkButton(card, "Tranh Đăng", TL, TL, new Vector2(488, y - 34), new Vector2(420, 66), false, () => { _launcher.SelectMode(Modes.Contest); PaintRoom(); }, 22);
        y -= 124;
        var row = UiKit.Rect(card, "Seasons", TL, TL, new Vector2(48, y), new Vector2(860, 110));
        _seasonRow = row.gameObject;
        UiKit.LabelAt(row, "Mùa", 20, UiKit.InkGrey, TL, TL, new Vector2(2, 0), new Vector2(300, 28), TextAnchor.MiddleLeft, FontStyle.Bold);
        _seasonBtns = new Button[4];
        for (int i = 0; i < 4; i++) {
          int k = i;
          _seasonBtns[i] = UiKit.InkToggle(row, ArenaBuilder.Seasons[i].Name, TL, TL, new Vector2(i * 150, -34), 76, 18);
          _seasonBtns[i].onClick.AddListener(() => { _launcher.SelectSeason(k); PaintRoom(); });
        }
        y -= 118;
      }
      UiKit.LabelAt(card, "Mã phòng", 20, UiKit.InkGrey, TL, TL, new Vector2(50, y), new Vector2(300, 28), TextAnchor.MiddleLeft, FontStyle.Bold);
      _code = UiKit.InkInput(card, _launcher.RoomCode, TL, TL, new Vector2(48, y - 32), new Vector2(460, 74), 40);
      _code.characterLimit = 8;
      _code.contentType = InputField.ContentType.IntegerNumber;
      _code.name = "RoomCodeInput";
      if (mode >= 0) UiKit.InkButton(card, "Mã mới", TL, TL, new Vector2(530, y - 34), new Vector2(180, 66), false, () => { _code.text = Random.Range(1000, 9999).ToString(); }, 20).name = "NewCode";
      _roomHint = UiKit.LabelAt(card, "", 18, UiKit.InkGrey, TL, TL, new Vector2(50, y - 116), new Vector2(860, 50), TextAnchor.UpperLeft);

      _roomStatus = UiKit.LabelAt(card, "", 20, UiKit.Seal, BL, BL, new Vector2(50, 116), new Vector2(860, 30), TextAnchor.MiddleLeft, FontStyle.Bold);
      _launcher.StatusSink = s => { if (_roomStatus) _roomStatus.text = s; };
      UiKit.InkButton(card, mode < 0 ? "Vào phòng" : "Tạo / vào phòng", BR, BR, new Vector2(-44, 26), new Vector2(380, 92), true, Go, 28).name = "RoomGo";
      UiKit.InkButton(card, "Quay lại", BL, BL, new Vector2(44, 32), new Vector2(200, 72), false, CloseRoom, 22).name = "RoomBack";
      PaintRoom();
      RewardFx.PopIn(card);
      Sfx.Play(SoundId.UiOpen);
    }

    void PaintRoom() {
      if (!_room) return;
      bool contest = LocalProfile.Mode == Modes.Contest;
      if (_modeBtns != null) UiKit.SelectSegment(_modeBtns, LocalProfile.Mode);
      if (_seasonRow) _seasonRow.SetActive(contest);
      if (_seasonBtns != null) UiKit.SelectToggle(_seasonBtns, LocalProfile.Season);
      _roomHint.text = _roomMode < 0
        ? "Nhập mã bạn bè gửi. Chế độ và mùa do chủ phòng chọn. Bạn sẽ chọn tướng trong phòng chờ."
        : contest
          ? "Tranh Đăng cần ít nhất 2 người, chia hai đội Lam và Chu. Gửi mã này cho bạn bè."
          : "Gửi mã này cho bạn bè (tối đa 4 người). Mùa đi theo chương. Chọn tướng trong phòng chờ.";
    }

    void Go() {
      _launcher.RoomCode = _code.text;
      _launcher.PlayOnline();
    }

    void CloseRoom() {
      Sfx.Play(SoundId.UiBack);
      if (_room) Destroy(_room.gameObject);
      _room = null;
      _launcher.StatusSink = s => { if (_hubStatus) _hubStatus.text = s; };
    }

    void Update() {
      if (_tap && _tap.enabled) { var c = _tap.color; c.a = .55f + .45f * Mathf.Sin(Time.unscaledTime * 3.2f); _tap.color = c; }
      if (_start && !_namePanel && !Cinematic.Playing && (_idle += Time.unscaledDeltaTime) > TrailerAfterIdle) PlayTrailer(); // attract mode
      if (Cinematic.Playing) return;
      var k = UnityEngine.InputSystem.Keyboard.current;
      if (k == null) return;
      if (_start && !_namePanel && (k.anyKey.wasPressedThisFrame)) OnStartTap();
      else if (_namePanel && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame)) ConfirmName();
      else if (_room && k.escapeKey.wasPressedThisFrame) CloseRoom();
    }
  }
}
