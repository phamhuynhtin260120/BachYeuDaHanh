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
    public static void ResetSession() { _startSeen = false; _hubPlace = -1; }

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
      f._backdrop = UiKit.Backdrop(f._root, "Backdrop", "MenuBg").gameObject;
      if (_startSeen) f.GoHub(); else f.GoStart();
      LoadingScreen.Hide(.15f);
    }

    public static void Hide() { if (Current) Destroy(Current.gameObject); }

    /// <summary>Build the menu again (after the UI skin changes).</summary>
    public static void Rebuild() { if (Current) Show(Current._launcher); }

    GameObject _backdrop;

    // Places the hub backdrop can show; each time the hub opens it moves on to another one.
    static readonly (string film, string name)[] HubPlaces = {
      ("MenuHub", "Hồ sen giữa phố"), ("MenuHub_DinhTra", "Đình trà bên cầu gỗ"),
      ("MenuHub_DinhTung", "Đình cổ dưới gốc tùng"), ("MenuHub_ThacNuoc", "Thác nước đảo bay"),
    };
    static int _hubPlace = -1;

    static (string film, string name) NextHubPlace() {
      int n = HubPlaces.Length;
      int pick = _hubPlace < 0 ? UnityEngine.Random.Range(0, n) : (_hubPlace + 1 + UnityEngine.Random.Range(0, n - 1)) % n; // never the same twice in a row
      for (int i = 0; i < n; i++, pick = (pick + 1) % n)
        if (Resources.Load<UnityEngine.Timeline.TimelineAsset>(Cinematic.Folder + HubPlaces[pick].film)) break;
      _hubPlace = pick;
      return HubPlaces[pick];
    }

    /// <summary>
    /// Living background: loop a Timeline film over the real arena behind the menu (Resources/Cinematics/<paramref name="film"/>).
    /// The painted backdrop only shows if the film is missing.
    /// </summary>
    void LiveBackdrop(string film) {
      bool live = Cinematic.PlayBackdrop(film);
      if (_backdrop) _backdrop.SetActive(!live);
    }

    void OnDestroy() {
      Cinematic.StopBackdrop();
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
      LiveBackdrop("MenuStart");
      var title = UiKit.LabelAt(_start, "Bách Yêu\nDạ Hành", 150, UiKit.InkBlack, C, C, new Vector2(-40, 90), new Vector2(1100, 360), TextAnchor.MiddleCenter, FontStyle.Bold);
      title.lineSpacing = .85f;
      title.gameObject.AddComponent<Outline>().effectColor = new Color(.97f, .96f, .94f, .85f);
      UiKit.SealTag(_start, "Seal", "Dạ", C, C, new Vector2(360, 190), new Vector2(84, 84), UiKit.Seal, true, 38);
      var tagline = UiKit.LabelAt(_start, "Đêm hội Hồng Đăng · 1 đến 4 người", 30, UiKit.InkBlack, C, C, new Vector2(0, -120), new Vector2(900, 44), TextAnchor.MiddleCenter, FontStyle.Bold);
      _tap = UiKit.LabelAt(_start, "Chạm để bắt đầu", 38, UiKit.Seal, BC, BC, new Vector2(0, 150), new Vector2(800, 54), TextAnchor.MiddleCenter, FontStyle.Bold);
      // Over the living 3D backdrop: a paper-coloured edge keeps the small lines readable.
      foreach (var t in new[] { tagline, _tap }) { var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(.97f, .95f, .9f, .9f); o.effectDistance = new Vector2(2, -2); }
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
    // Game-style hub: the hero in the middle of the stage, icon tiles down the left, profile and Hồn đăng on top,
    // and on the right the chosen mode's banner, three mode chips and one big "Bắt đầu" button.
    Text _hubName, _hubTitle, _hubCoins, _journeySub, _hubStatus, _bannerTitle, _bannerHint;
    GameObject _questDot;
    Button[] _modeChips;
    int _hubMode;
    Image _bannerIcon;

    static readonly string[] ModeNames = { "Hành trình", "Tranh Đăng", "Vào phòng" };
    static readonly string[] ModeIcons = { "journey", "contest", "room" };

    void GoHub() {
      Clear();
      _hub = Screen("Hub");
      var place = NextHubPlace();
      LiveBackdrop(place.film);
      _launcher.StatusSink = s => { if (_hubStatus) _hubStatus.text = s; };
      bool cp = UiKit.CoPhong;
      var th = UiKit.Theme; var gold = th.Gold;

      // ---- Profile, top-left: round portrait (tap to rename), name, title.
      var portrait = UiKit.InkImage(_hub, "Portrait", "InkDot", TL, TL, new Vector2(28, -18), new Vector2(104, 104), cp ? UiKit.Theme.Rim : UiKit.InkBlack);
      portrait.raycastTarget = true;
      var pb = portrait.gameObject.AddComponent<Button>(); pb.onClick.AddListener(Rename);
      UiKit.InkImage(portrait.transform, "Inner", "InkDot", C, C, Vector2.zero, new Vector2(88, 88), cp ? UiKit.Theme.Deep : UiKit.InkPaper);
      UiKit.IconImage(portrait.transform, "mask", C, C, Vector2.zero, 62, cp ? gold : UiKit.Seal);
      _hubName = UiKit.StageLabel(_hub, "", 36, TL, TL, new Vector2(146, -20), new Vector2(420, 50), TextAnchor.MiddleLeft);
      _hubTitle = UiKit.LabelAt(_hub, "", 20, cp ? gold : Gfx.Hex("#8a6a2e"), TL, TL, new Vector2(148, -70), new Vector2(420, 30), TextAnchor.MiddleLeft, FontStyle.Bold);
      _hubTitle.gameObject.AddComponent<Outline>().effectColor = cp ? th.Edge : new Color(1, 1, 1, .6f);

      // ---- Top-right: Hồn đăng pill, help and settings.
      var pill = UiKit.Rect(_hub, "Coins", TR, TR, new Vector2(-196, -30), new Vector2(270, 62));
      var pillBg = pill.gameObject.AddComponent<Image>();
      pillBg.color = cp ? th.Pill : new Color(.98f, .97f, .94f, .92f);
      UiKit.SkinPanel(pillBg, true);
      UiKit.IconImage(pill, "lantern", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(10, 0), 48, cp ? gold : UiKit.Seal);
      _hubCoins = UiKit.LabelAt(pill, "", 28, cp ? th.Cream : UiKit.InkBlack, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(64, 0), new Vector2(196, 50), TextAnchor.MiddleLeft, FontStyle.Bold);
      RoundIcon(_hub, "help", TR, new Vector2(-112, -28), () => HelpPanel.Open());
      RoundIcon(_hub, "settings", TR, new Vector2(-30, -28), () => SettingsPanel.Open());

      // ---- Left column: icon tiles.
      string[] names = { "Nhiệm vụ", "Túi pháp khí", "Bách yêu lục", "Thành tựu" };
      string[] icons = { "quest", "bag", "codex", "trophy" };
      System.Action[] acts = { () => DailyQuestPanel.Open(RefreshHub), () => BagPanel.Open(RefreshHub), () => BestiaryPanel.Open(RefreshHub), () => AchievementPanel.Open(RefreshHub) };
      for (int i = 0; i < names.Length; i++) {
        int k = i;
        var tile = IconTile(_hub, icons[i], names[i], TL, new Vector2(46, -170 - i * 158), () => acts[k]());
        tile.name = "Nav_" + i;
        if (i == 0) _questDot = UiKit.SealTag(tile.transform, "Dot", "!", TR, C, new Vector2(-6, -6), new Vector2(34, 34), UiKit.Seal, true, 20).gameObject;
      }

      // ---- The hero, centre stage, with a pool of shadow at its feet.
      UiKit.InkImage(_hub, "HeroShadow", "InkDot", C, C, new Vector2(-90, -318), new Vector2(340, 62), new Color(0, 0, 0, cp ? .45f : .2f));
      HubHero.Create(_hub, C, new Vector2(-90, -24), new Vector2(520, 720));

      // ---- Right: mode banner, chips, big start button.
      var banner = UiKit.Rect(_hub, "Banner", TR, TR, new Vector2(-34, -124), new Vector2(520, 220));
      var bannerBg = banner.gameObject.AddComponent<Image>();
      bannerBg.color = new Color(.15f, .16f, .16f, .95f);
      UiKit.SkinPanel(bannerBg, true);
      _bannerIcon = UiKit.IconImage(banner, "journey", new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-18, -6), 150, new Color(1, .85f, .5f, .22f));
      _bannerTitle = UiKit.LabelAt(banner, "", 46, gold, TL, TL, new Vector2(30, -18), new Vector2(460, 60), TextAnchor.MiddleLeft, FontStyle.Bold);
      _bannerTitle.gameObject.AddComponent<Outline>().effectColor = th.Edge;
      _journeySub = UiKit.LabelAt(banner, "", 24, th.Cream, TL, TL, new Vector2(32, -86), new Vector2(460, 36), TextAnchor.MiddleLeft, FontStyle.Bold);
      _bannerHint = UiKit.LabelAt(banner, "", 19, new Color(.95f, .88f, .76f, .85f), TL, TL, new Vector2(32, -128), new Vector2(440, 60), TextAnchor.UpperLeft);

      _modeChips = new Button[3];
      for (int i = 0; i < 3; i++) {
        int k = i;
        _modeChips[i] = UiKit.InkButton(_hub, ModeNames[i], BR, BR, new Vector2(-34 - (2 - i) * 176, 196), new Vector2(166, 62), false, () => { Sfx.Play(SoundId.UiClick); SetHubMode(k); }, 22);
        _modeChips[i].name = "Mode_" + i;
      }
      var start = UiKit.InkButton(_hub, "Bắt đầu", BR, BR, new Vector2(-34, 46), new Vector2(520, 132), true, StartSelected, 52);
      start.name = "Start";
      UiKit.IconImage(start.transform, "play", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(70, 0), 70, cp ? th.Glow : UiKit.InkPaper);
      _hubStatus = UiKit.StageLabel(_hub, "", 20, BR, BR, new Vector2(-34, 266), new Vector2(520, 30), TextAnchor.MiddleRight);
      _hubStatus.color = cp ? th.Glow : UiKit.Seal;

      // Where the hero stands tonight (the backdrop film), bottom-left.
      var where = UiKit.StageLabel(_hub, "Đang ở · " + place.name, 20, BC, BC, new Vector2(-90, 22), new Vector2(560, 30), TextAnchor.MiddleCenter); // under the hero, clear of the icon column
      where.color = UiKit.CoPhong ? UiKit.Theme.Gold : UiKit.InkBlack;

      SetHubMode(_hubMode);
      RefreshHub();
    }

    void SetHubMode(int m) {
      _hubMode = Mathf.Clamp(m, 0, 2);
      UiKit.SelectSegment(_modeChips, _hubMode);
      _bannerTitle.text = ModeNames[_hubMode];
      _bannerIcon.sprite = UiKit.Icon(ModeIcons[_hubMode]);
      _bannerHint.text = _hubMode switch {
        0 => "Một mình hoặc cùng bạn · cốt truyện theo chương",
        1 => "Tạo phòng rồi gửi mã cho bạn bè",
        _ => "Nhập mã phòng bạn bè gửi cho bạn · chế độ do chủ phòng chọn",
      };
      RefreshHub();
    }

    void StartSelected() {
      Sfx.Play(SoundId.UiClick);
      if (_hubMode == 0) OpenJourney();
      else OpenRoom(_hubMode == 1 ? Modes.Contest : -1);
    }

    /// <summary>Square icon tile with its name under it (left column).</summary>
    Button IconTile(RectTransform parent, string icon, string label, Vector2 anchor, Vector2 pos, System.Action act) {
      // Tiên cảnh: a round glowing jade orb (floating, no box); otherwise a framed square tile.
      var tile = UiKit.InkImage(parent, "Tile", UiKit.TienCanh ? "Orb" : "Lines", anchor, anchor, pos, new Vector2(108, 108), Color.white);
      tile.raycastTarget = true;
      var b = tile.gameObject.AddComponent<Button>();
      b.onClick.AddListener(() => { Sfx.Play(SoundId.UiClick); act(); });
      UiKit.IconImage(tile.transform, icon, C, C, new Vector2(0, 2), 70, UiKit.CoPhong ? UiKit.Theme.TileIcon : UiKit.InkBlack);
      UiKit.StageLabel(tile.transform, label, 20, BC, TC, new Vector2(0, -2), new Vector2(180, 30), TextAnchor.MiddleCenter);
      return b;
    }

    void RoundIcon(RectTransform parent, string icon, Vector2 anchor, Vector2 pos, System.Action act) {
      bool cp = UiKit.CoPhong;
      var ring = UiKit.InkImage(parent, "Btn_" + icon, "InkDot", anchor, anchor, pos, new Vector2(68, 68), cp ? UiKit.Theme.Rim : UiKit.InkBlack);
      ring.raycastTarget = true;
      var b = ring.gameObject.AddComponent<Button>();
      b.onClick.AddListener(() => { Sfx.Play(SoundId.UiClick); act(); });
      UiKit.InkImage(ring.transform, "Inner", "InkDot", C, C, Vector2.zero, new Vector2(58, 58), cp ? UiKit.Theme.Deep : UiKit.InkPaper);
      UiKit.IconImage(ring.transform, icon, C, C, Vector2.zero, 40, cp ? UiKit.Theme.Gold : UiKit.InkBlack);
    }

    void RefreshHub() {
      if (!_hub) return;
      _hubName.text = string.IsNullOrEmpty(_launcher.PlayerName) ? "Khách" : _launcher.PlayerName;
      var t = Profile.Data.Title;
      _hubTitle.text = string.IsNullOrEmpty(t) ? $"Thành tựu {Achievements.DoneCount}/{Achievements.All.Length}" : "« " + t + " »";
      _hubCoins.text = $"{Profile.Coins}";
      int n = Mathf.Clamp(LocalProfile.Chapter, 1, JourneyMap.FreeNight);
      var ch = Story.Get(n);
      _journeySub.text = _hubMode switch {
        0 => ch != null ? $"Chương {n} · {ch.Title}" : "Đêm tự do",
        1 => "Hai đội giành lồng đèn · 2 đến 4 người",
        _ => "Vào phòng bạn bè",
      };
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
