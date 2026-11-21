using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BachYeu {
  /// <summary>
  /// In-game HUD, touch controls, blessing picker and night result.
  /// Lives in the prefab Assets/_Game/Prefabs/UI/HUD.prefab: edit layout, sprites and fonts there.
  /// Every reference below is wired in that prefab; code only fills in numbers and text.
  /// If no prefab is assigned, BuildDefault() creates the same layout from code.
  /// </summary>
  public class Hud : MonoBehaviour {
    public static Hud I { get; private set; }

    [Serializable]
    public class ActionButton {
      [Tooltip("atk, s1, s2, dash, swap, ult")] public string Id;
      public Image Ring;
      public Text Label;
      public GameObject Cooldown;
      public Text CooldownText;
      public TouchButton Touch;
      [Tooltip("Optional: icons per mask id (0 Hồ Ly, 1 Tengu, 2 Hà Bá, 3 Quỷ Đá). Shown instead of the text label when set.")]
      public Image Icon;
      public Sprite[] MaskIcons = new Sprite[4];
    }

    [Header("Player")]
    public Text NameText;
    public Image MaskTagBg;
    public Text MaskTagText;
    public GameObject ResonanceTag;
    public RectTransform HpFill;
    public Text HpText;
    public RectTransform UltFill;
    public Text UltText;
    public Text PartyText;

    [Header("Phase and boss")]
    public Text PhaseTitle;
    public Text Objective;
    public GameObject BossBox;
    public Text BossName;
    public RectTransform BossFill;

    [Header("Tranh Đăng (2 teams)")]
    public GameObject ScoreBox;
    public Text ScoreLam, ScoreChu, MatchClock;
    [Tooltip("Lantern dots, west to east: Đèn Chợ, Đèn Cầu, Đèn Bến.")]
    public Image[] LanternPips = new Image[0];
    public Text ToastText;
    public float ToastDuration = 2.2f;

    [Header("Room info")]
    public Text NightText;
    public Text RoomText;
    public Text KillsText;
    public Button LeaveButton;

    [Header("Banner")]
    public CanvasGroup BannerGroup;
    public Text BannerBig, BannerTitle, BannerSub;
    public float BannerDuration = 2.4f;

    [Header("Feedback")]
    public Image HurtOverlay;
    public RectTransform NumbersLayer;
    [Tooltip("Inactive Text used as the template for floating damage numbers.")]
    public Text NumberTemplate;
    public int NumberSize = 28, BigNumberSize = 46;

    [Header("Controls")]
    public GameObject Pad;
    public List<ActionButton> Buttons = new();

    [Header("Downed")]
    public GameObject DownedPanel;
    public RectTransform ReviveFill;

    [Header("Blessing")]
    public GameObject BlessPanel;
    public Text BlessTitle, BlessWait;
    public RectTransform BlessCards;
    [Tooltip("Inactive card template. Needs child Texts named 'Title' and 'Desc'.")]
    public Button BlessCardTemplate;
    public float BlessCardSpacing = 300;

    [Header("Result")]
    public GameObject ResultPanel;
    public Text ResultBig, ResultTitle, ResultStats, ResultFragment, ResultButtonText;
    [Tooltip("Host only: play the next night right away.")]
    public Button ResultButton;
    [Tooltip("Host only, online only: go back to the lobby so friends can join.")]
    public Button ResultLobbyButton;
    [Tooltip("Shown to guests instead of the host buttons.")]
    public Text ResultWaitText;

    [Header("Pings (online only)")]
    public GameObject PingBar;
    [Tooltip("Order: Lại đây, Đứng đèn, Cẩn thận. Keys 1, 2, 3 do the same.")]
    public Button[] PingButtons = new Button[0];
    [Tooltip("Inactive Text used for ping bubbles. Empty = reuse NumberTemplate.")]
    public Text PingTemplate;
    public int PingSize = 24;

    [Header("Colors")]
    public Color FoxColor = UiKit.Fox;
    public Color WindColor = UiKit.Wind;
    public Color GoldColor = UiKit.Gold;
    public Color DashColor = UiKit.Ink2;
    public Color UltReadyColor = UiKit.Seal;
    public Color NumberColor = UiKit.Ink, NumberOtherColor = UiKit.Ink2, BigNumberColor = UiKit.Seal;

    float _bannerT, _hurtT, _toastT;
    float _bossBarY = float.NaN;
    readonly Dictionary<string, ActionButton> _btn = new();
    readonly List<(Text t, Vector3 world, float life)> _numbers = new();
    readonly Stack<Text> _numberPool = new();
    int _blessRolledFor = -1, _blessPickedFor = -1;
    bool _resultShown;
    float _resultButtonX = float.NaN;
    string _roomCode;

    // ---------------- creation ----------------
    public static Hud Create(Hud prefab, string roomCode, Action onLeave) {
      Hud hud;
      if (prefab) hud = Instantiate(prefab);
      else {
        var c = UiKit.Canvas("HUD", 10);
        hud = c.gameObject.AddComponent<Hud>();
        hud.BuildDefault((RectTransform)c.transform);
      }
      hud.name = "HUD";
      hud.Init(roomCode, onLeave);
      return hud;
    }

    void Awake() { I = this; }
    void OnDestroy() { if (I == this) I = null; }

    Action _onLeave;

    void Init(string roomCode, Action onLeave) {
      _roomCode = roomCode;
      foreach (var b in Buttons) if (b != null && !string.IsNullOrEmpty(b.Id)) _btn[b.Id] = b;
      Wire("atk", () => GameInput.AttackButtonHeld = true, () => GameInput.AttackButtonHeld = false);
      Wire("s1", () => GameInput.Press(GameAction.Skill1));
      Wire("s2", () => GameInput.Press(GameAction.Skill2));
      Wire("dash", () => GameInput.Press(GameAction.Dash));
      Wire("swap", () => GameInput.Press(GameAction.Swap));
      Wire("ult", () => GameInput.Press(GameAction.Ult));
      // The top-right button opens the in-match menu (Tiếp tục / Cài đặt / Rời trận) instead of leaving at once.
      _onLeave = onLeave;
      if (LeaveButton) {
        LeaveButton.onClick.AddListener(() => PausePanel.Toggle(_onLeave));
        var t = LeaveButton.GetComponentInChildren<Text>();
        if (t) t.text = string.IsNullOrEmpty(roomCode) ? "Tạm dừng" : "Tuỳ chọn";
      }
      if (ResultButton) ResultButton.onClick.AddListener(() => { if (GameDirector.Instance) GameDirector.Instance.RPC_RequestRestart(); });
      if (ResultLobbyButton) ResultLobbyButton.onClick.AddListener(() => { if (GameDirector.Instance) GameDirector.Instance.RPC_RequestLobby(); });
      for (int i = 0; i < PingButtons.Length; i++) {
        int k = i;
        if (PingButtons[i]) PingButtons[i].onClick.AddListener(() => GameInput.PressPing(k));
      }
      if (PingTemplate) PingTemplate.gameObject.SetActive(false);
      if (PingBar) PingBar.SetActive(!string.IsNullOrEmpty(roomCode));
      if (NumberTemplate) NumberTemplate.gameObject.SetActive(false);
      if (BlessCardTemplate) BlessCardTemplate.gameObject.SetActive(false);
      if (BlessPanel) BlessPanel.SetActive(false);
      if (ResultPanel) ResultPanel.SetActive(false);
      if (DownedPanel) DownedPanel.SetActive(false);
      if (BossBox) BossBox.SetActive(false);
      if (BannerGroup) BannerGroup.alpha = 0;
    }

    void Wire(string id, Action down, Action up = null) {
      if (!_btn.TryGetValue(id, out var b) || b.Touch == null) return;
      b.Touch.OnDown = down;
      b.Touch.OnUp = up;
    }

    // ---------------- public api ----------------
    public void Banner(string big, string title, string sub) {
      if (BannerBig) BannerBig.text = big;
      if (BannerTitle) BannerTitle.text = title;
      if (BannerSub) BannerSub.text = sub ?? "";
      _bannerT = BannerDuration;
    }

    public void HurtFlash() => _hurtT = .35f;

    /// <summary>Short line in the upper middle, e.g. "Đội Chu giữ Đèn Cầu".</summary>
    public void Toast(string text, Color color) {
      if (!ToastText) return;
      ToastText.text = text;
      ToastText.color = color;
      _toastT = ToastDuration;
    }

    static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    static string Clock(float secs) { int s = Mathf.CeilToInt(Mathf.Max(0, secs)); return $"{s / 60:00}:{s % 60:00}"; }

    public void DamageNumber(Vector3 world, int value, bool big, bool mine) {
      if (value <= 0 || _numbers.Count > 40 || NumberTemplate == null) return;
      var t = _numberPool.Count > 0 ? _numberPool.Pop() : Instantiate(NumberTemplate, NumbersLayer);
      t.gameObject.SetActive(true);
      t.text = value.ToString();
      t.fontSize = big ? BigNumberSize : NumberSize;
      t.color = big ? BigNumberColor : mine ? NumberColor : NumberOtherColor;
      _numbers.Add((t, world + new Vector3(UnityEngine.Random.Range(-.5f, .5f), 0, 0), .8f));
    }

    readonly List<(Text t, Vector3 world, float life)> _pings = new();
    readonly Stack<Text> _pingPool = new();
    const float PingLife = 2.5f;

    /// <summary>Floating callout above a player, e.g. "Lan: Lại đây!".</summary>
    public void ShowPing(Vector3 world, string text, Color color) {
      var template = PingTemplate ? PingTemplate : NumberTemplate;
      if (!template || _pings.Count > 8) return;
      var t = _pingPool.Count > 0 ? _pingPool.Pop() : Instantiate(template, NumbersLayer);
      t.gameObject.SetActive(true);
      t.text = text;
      t.fontSize = PingSize;
      t.color = color;
      var rt = (RectTransform)t.transform;
      rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, 420), rt.sizeDelta.y);
      _pings.Add((t, world, PingLife));
    }

    void UpdatePings(float dt) {
      var cam = Camera.main;
      for (int i = _pings.Count - 1; i >= 0; i--) {
        var p = _pings[i];
        p.life -= dt;
        if (p.life <= 0 || cam == null) { p.t.gameObject.SetActive(false); _pingPool.Push(p.t); _pings.RemoveAt(i); continue; }
        _pings[i] = p;
        var sp = cam.WorldToScreenPoint(p.world);
        p.t.enabled = sp.z > 0;
        if (sp.z <= 0) continue;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(NumbersLayer, sp, null, out var lp);
        ((RectTransform)p.t.transform).anchoredPosition = lp + new Vector2(0, (PingLife - p.life) * 12f);
        var c = p.t.color; c.a = Mathf.Clamp01(p.life / .5f); p.t.color = c;
      }
    }

    // ---------------- update ----------------
    void Update() {
      var kb = UnityEngine.InputSystem.Keyboard.current;
      if (kb != null && kb.escapeKey.wasPressedThisFrame && !PausePanel.Current && !SettingsPanel.Current && !HelpPanel.Current && !StoryDialog.Current && !Cinematic.Playing) PausePanel.Open(_onLeave);
      float dt = Time.deltaTime;
      var me = PlayerAvatar.Local;
      var dir = GameDirector.Instance;
      if (dir && dir.Object && dir.Object.IsValid) { Story.Tick(dir); Tutorial.Tick(dir); }

      if (BannerGroup) {
        if (_bannerT > 0) {
          _bannerT -= dt;
          float age = BannerDuration - _bannerT;
          BannerGroup.alpha = age < .35f ? age / .35f : _bannerT < .6f ? _bannerT / .6f : 1f;
        } else BannerGroup.alpha = 0;
      }
      if (HurtOverlay && _hurtT > 0) {
        _hurtT -= dt;
        var c = HurtOverlay.color; c.a = Mathf.Clamp01(_hurtT / .35f) * .28f; HurtOverlay.color = c;
      }
      if (ToastText) {
        _toastT -= dt;
        var c = ToastText.color; c.a = Mathf.Clamp01(_toastT / .4f); ToastText.color = c;
      }
      UpdateNumbers(dt);
      UpdatePings(dt);
      if (me == null || dir == null) return;
      bool contest = dir.IsContest;
      UpdateScore(me, dir, contest);

      var mask = me.MaskDef;
      var maskColor = mask.Color;
      Set(NameText, me.DisplayName);
      Set(MaskTagText, mask.Name);
      if (MaskTagBg) MaskTagBg.color = maskColor;
      if (ResonanceTag) ResonanceTag.SetActive(me.ResonanceTime > 0);
      Fill(HpFill, me.MaxHp > 0 ? me.Hp / (float)me.MaxHp : 0);
      Set(HpText, $"{Mathf.Max(0, me.Hp)} / {me.MaxHp}" + (me.Shield > 0 ? $"  · khiên {Mathf.CeilToInt(me.Shield)}" : "") + (me.Guarding ? "  · gồng" : ""));
      Fill(UltFill, me.Ult / 100f);
      Set(UltText, $"Tuyệt kỹ {Mathf.FloorToInt(me.Ult)}%");
      Set(PartyText, string.Join("\n", PlayerAvatar.All.Where(p => p != me)
        .Select(p => (contest ? $"<color={Hex(p.TeamColor)}>●</color> " : "") + $"{p.DisplayName}  {(p.Downed ? "· ĐÃ GỤC" : $"{p.Hp}/{p.MaxHp}")}")));

      if (contest) {
        Set(PhaseTitle, dir.State == RunState.Lobby ? "Phòng chờ · Tranh Đăng" : Contest.ActTitle[Mathf.Clamp(dir.Phase, 0, 2)]);
        int mine = 0;
        for (int i = 0; i < 3; i++) if (dir.LanternOwner[i] - 1 == me.Team) mine++;
        Set(Objective, dir.State switch {
          RunState.Lobby => dir.LocalIsHost ? "Chia hai đội rồi bấm Bắt đầu" : "Chọn đội, bấm Sẵn sàng",
          RunState.Won or RunState.Lost => "Hết trận",
          _ => dir.FullHoldTeam >= 0
            ? $"{Contest.TeamName[dir.FullHoldTeam]} giữ cả 3 đèn · còn {Mathf.CeilToInt(Contest.FullHoldSeconds - dir.FullHold)} giây"
            : $"{Contest.TeamName[Mathf.Clamp(me.Team, 0, 1)]} đang giữ {mine}/3 đèn" + (dir.CursedTeam >= 0 ? $" · {Contest.TeamName[dir.CursedTeam]} bị nguyền" : ""),
        });
      } else {
      Set(PhaseTitle, dir.State == RunState.Lobby ? "Phòng chờ" : Phases.Title[Mathf.Clamp(dir.Phase, 0, 3)]);
      Set(Objective, dir.State switch {
        RunState.Lobby => dir.LocalIsHost ? "Bấm Bắt đầu khi mọi người đã sẵn sàng" : "Bấm Sẵn sàng rồi chờ chủ phòng",
        RunState.Cleared => "Canh giờ đã qua",
        RunState.Blessing => "Nghỉ chân, chọn phúc lành",
        RunState.Won => "Bình minh đã đến",
        RunState.Lost => "Cả đội đã gục",
        _ => dir.Phase switch {
          0 => $"Thắp lồng đèn {dir.LitCount}/3 · đứng cạnh đèn để thắp",
          1 => $"Đánh tan yêu quái {Mathf.Min(dir.PhaseKills, Phases.Phase2Kills)}/{Phases.Phase2Kills}",
          2 => "Hạ Miêu yêu song vĩ",
          _ => "Hạ Hồng Đăng Quỷ Vương",
        }
      });
      }
      var boss = dir.Boss;
      bool showBoss = boss != null && boss.Hp > 0 && dir.State == RunState.Playing;
      if (BossBox) BossBox.SetActive(showBoss);
      if (showBoss) {
        Set(BossName, EnemyStats.Name(boss.Type) + (boss.Rage ? " · nổi giận" : ""));
        Fill(BossFill, boss.Hp / (float)Mathf.Max(1, boss.MaxHp));
      }
      Set(NightText, contest ? "Tranh Đăng" : $"Đêm thứ {dir.Night}");
      Set(RoomText, string.IsNullOrEmpty(_roomCode) ? "Chơi một mình" : $"Phòng {_roomCode} · {dir.PlayerCount} người");
      Set(KillsText, contest ? $"Bạn diệt {me.KillCount} · đẩy {me.Pushes}" : $"Đã diệt {dir.Kills}");

      SetButton("s1", mask.S1, me.SkillCooldown(1), maskColor, me.MaskId);
      SetButton("s2", mask.S2, me.SkillCooldown(2), maskColor, me.MaskId);
      SetButton("swap", "Đổi", me.SwapCooldown, GoldColor, me.MaskId);
      SetButton("dash", "Né", me.DashCooldown, DashColor, me.MaskId);
      if (_btn.TryGetValue("ult", out var ult)) {
        Set(ult.Label, mask.Ult);
        if (ult.Ring) ult.Ring.color = me.Ult >= 100 ? UltReadyColor : GoldColor;
        if (ult.Cooldown) ult.Cooldown.SetActive(me.Ult < 100);
        Set(ult.CooldownText, $"{Mathf.FloorToInt(me.Ult)}%");
        ApplyIcon(ult, me.MaskId);
      }

      if (DownedPanel) DownedPanel.SetActive(me.Downed && dir.State == RunState.Playing);
      if (me.Downed) Fill(ReviveFill, me.ReviveProgress);

      UpdateBlessing(me, dir);
      UpdateResult(dir);
    }

    static void Set(Text t, string s) { if (t && t.text != s) t.text = s; }

    static readonly int[] PipOrder = { 0, 2, 1 }; // west to east: Đèn Chợ, Đèn Cầu, Đèn Bến

    void UpdateScore(PlayerAvatar me, GameDirector dir, bool contest) {
      bool show = contest && dir.State != RunState.Lobby;
      if (ScoreBox && ScoreBox.activeSelf != show) ScoreBox.SetActive(show);
      if (BossBox) {
        // The score sits where the boss bar normally is; push the boss bar under it.
        var r = (RectTransform)BossBox.transform;
        if (float.IsNaN(_bossBarY)) _bossBarY = r.anchoredPosition.y;
        float y = show ? _bossBarY - 112 : _bossBarY;
        if (!Mathf.Approximately(r.anchoredPosition.y, y)) r.anchoredPosition = new Vector2(r.anchoredPosition.x, y);
      }
      if (!show) return;
      Set(ScoreLam, dir.Score(0).ToString());
      Set(ScoreChu, dir.Score(1).ToString());
      Set(MatchClock, Clock(dir.MatchLeft));
      for (int k = 0; k < LanternPips.Length && k < 3; k++) {
        var pip = LanternPips[k];
        if (!pip) continue;
        int i = PipOrder[k];
        int owner = dir.LanternOwner[i] - 1;
        float cap = dir.LanternProgress[i];
        Color c = owner >= 0 ? Contest.OwnerColor(owner)
          : Mathf.Abs(cap) > .02f ? Color.Lerp(UiKit.InkPaper, Contest.TeamColor[cap < 0 ? 0 : 1], Mathf.Abs(cap))
          : UiKit.InkPaper;
        pip.color = c;
        float pulse = owner < 0 && Mathf.Abs(cap) > .02f ? 1f + Mathf.Sin(Time.time * 8) * .08f : 1f;
        pip.rectTransform.localScale = Vector3.one * pulse;
      }
    }

    static void Fill(RectTransform fill, float k) {
      if (!fill) return;
      var a = fill.anchorMax; a.x = Mathf.Clamp01(k); fill.anchorMax = a;
    }

    void SetButton(string id, string text, float cd, Color ring, int mask) {
      if (!_btn.TryGetValue(id, out var b)) return;
      Set(b.Label, text);
      if (b.Label) b.Label.color = ring;
      if (b.Ring) b.Ring.color = ring;
      bool on = cd > 0;
      if (b.Cooldown && b.Cooldown.activeSelf != on) b.Cooldown.SetActive(on);
      if (on) Set(b.CooldownText, cd >= 1 ? Mathf.CeilToInt(cd).ToString() : cd.ToString("0.0"));
      ApplyIcon(b, mask);
    }

    static void ApplyIcon(ActionButton b, int mask) {
      if (!b.Icon) return;
      var s = b.MaskIcons != null && b.MaskIcons.Length > mask ? b.MaskIcons[mask] : null;
      if (s == null && b.MaskIcons != null && b.MaskIcons.Length > 0) s = b.MaskIcons[0];
      b.Icon.gameObject.SetActive(s != null);
      if (s != null) b.Icon.sprite = s;
      if (b.Label) b.Label.enabled = s == null;
    }

    void UpdateNumbers(float dt) {
      var cam = Camera.main;
      for (int i = _numbers.Count - 1; i >= 0; i--) {
        var n = _numbers[i];
        n.life -= dt;
        if (n.life <= 0 || cam == null) {
          n.t.gameObject.SetActive(false);
          _numberPool.Push(n.t);
          _numbers.RemoveAt(i);
          continue;
        }
        _numbers[i] = n;
        var sp = cam.WorldToScreenPoint(n.world);
        n.t.enabled = sp.z > 0;
        if (sp.z <= 0) continue;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(NumbersLayer, sp, null, out var lp);
        ((RectTransform)n.t.transform).anchoredPosition = lp + new Vector2(0, (.8f - n.life) * 50f);
        var c = n.t.color; c.a = Mathf.Clamp01(n.life / .5f); n.t.color = c;
      }
    }

    void UpdateBlessing(PlayerAvatar me, GameDirector dir) {
      if (!BlessPanel) return;
      bool blessing = dir.State == RunState.Blessing;
      if (BlessPanel.activeSelf != blessing) BlessPanel.SetActive(blessing);
      if (!blessing) return;
      int phase = dir.Phase;
      if (_blessPickedFor == phase) {
        foreach (Transform c in BlessCards) if (c != BlessCardTemplate.transform) c.gameObject.SetActive(false);
        Set(BlessWait, $"Đang chờ đồng đội chọn ({dir.ReadyCount}/{dir.PlayerCount})");
        return;
      }
      if (_blessRolledFor == phase) return;
      _blessRolledFor = phase;
      foreach (Transform c in BlessCards) if (c != BlessCardTemplate.transform) Destroy(c.gameObject);
      const float heal = .2f;
      Set(BlessTitle, $"{Phases.Short[phase]} đã qua · tiếp theo: {Phases.Short[Mathf.Min(phase + 1, 3)]}");
      Set(BlessWait, $"Nghỉ chân bên quầy trà: hồi {Mathf.RoundToInt(heal * 100)}% máu khi chọn.");
      var picks = BlessingPool.Roll(me.B, 3);
      for (int i = 0; i < picks.Count; i++) {
        var d = picks[i];
        var card = Instantiate(BlessCardTemplate, BlessCards);
        card.gameObject.SetActive(true);
        var rt = (RectTransform)card.transform;
        rt.anchoredPosition = new Vector2((i - (picks.Count - 1) / 2f) * BlessCardSpacing, rt.anchoredPosition.y);
        var title = card.transform.Find("Title")?.GetComponentInChildren<Text>();
        var desc = card.transform.Find("Desc")?.GetComponentInChildren<Text>();
        if (title) title.text = d.Name;
        if (desc) desc.text = d.Desc;
        card.onClick.AddListener(() => {
          me.ApplyBlessing(d, heal);
          _blessPickedFor = phase;
          dir.RPC_Ready();
        });
      }
    }

    void UpdateResult(GameDirector dir) {
      bool end = dir.State == RunState.Won || dir.State == RunState.Lost;
      if (Pad) Pad.SetActive(!end && dir.State != RunState.Blessing);
      if (!ResultPanel) return;
      if (!end) { if (ResultPanel.activeSelf) ResultPanel.SetActive(false); _resultShown = false; return; }
      if (_resultShown) return;
      _resultShown = true;
      _blessPickedFor = _blessRolledFor = -1;
      // A short film (Timeline: Win / Lose) around the local player, then an ink splash into the result page.
      void ToResult() => InkTransition.Play(() => { if (this && dir) ShowResultNow(dir); }, .3f, .55f);
      var me = PlayerAvatar.Local;
      bool good; string title, sub;
      if (dir.IsContest) {
        good = me && (dir.Winner < 0 || dir.Winner == me.Team);
        title = dir.Winner < 0 ? "Hai đội hoà" : Contest.TeamName[dir.Winner] + " thắng";
        sub = dir.WonByFullLight ? "Toàn Đăng · giữ đủ ba ngọn đèn" : good ? "Đèn của đội bạn sáng nhất phố" : "Trận sau gỡ lại nhé";
      } else {
        good = dir.State == RunState.Won;
        var ch = Story.Get(dir.Night);
        title = good ? "Bình minh đã đến" : "Đêm nuốt trọn ánh đèn";
        sub = good ? (ch != null ? $"Chương {dir.Night} · {ch.Title}" : $"Đêm thứ {dir.Night}") : "Thắp lại từ canh giờ đầu nhé";
      }
      if (me && good) me.Face(FilmYaw(me) + 180f); // turn toward the film camera, which stands on the outer side
      if (me) Cinematic.Play(good ? "Win" : "Lose", me.transform.position, FilmYaw(me), ToResult, ("tieu_de", title), ("phu_de", sub)); // calls ToResult at once if the film is missing
      else ToResult();
    }

    // Film anchor yaw: local +Z points at the arena center, so the films' "-Z" cameras stand on the open, outer
    // side looking in, with the pond and lanterns behind the player instead of in front of the lens.
    static float FilmYaw(PlayerAvatar p) {
      var to = -p.transform.position; to.y = 0;
      return to.sqrMagnitude < .25f ? p.transform.eulerAngles.y + 180f : Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
    }

    void ShowResultNow(GameDirector dir) {
      ResultPanel.SetActive(true);
      if (dir.IsContest) ShowContestResult(dir);
      else ShowNightResult(dir);
      ResultBoard.Build(ResultPanel.transform, dir, dir.IsContest ? 232 : 352);
      ShowReward(dir);
      bool host = dir.LocalIsHost;
      if (ResultButton) ResultButton.gameObject.SetActive(host);
      if (ResultLobbyButton) ResultLobbyButton.gameObject.SetActive(host && !dir.IsSingle);
      if (ResultButton) {
        // Center the main button when it is alone (solo, or lobby button hidden).
        var r = (RectTransform)ResultButton.transform;
        if (float.IsNaN(_resultButtonX)) _resultButtonX = r.anchoredPosition.x;
        bool alone = !ResultLobbyButton || !ResultLobbyButton.gameObject.activeSelf;
        r.anchoredPosition = new Vector2(alone ? 0 : _resultButtonX, r.anchoredPosition.y);
      }
      if (ResultWaitText) ResultWaitText.gameObject.SetActive(!host);
    }

    Text _rewardText;

    /// <summary>Pays this match's Hồn đăng (once) and shows it above the result buttons.</summary>
    void ShowReward(GameDirector dir) {
      int got = Profile.GrantMatchReward(dir, PlayerAvatar.Local);
      if (!_rewardText) {
        var bc = new Vector2(.5f, 0);
        _rewardText = UiKit.LabelAt(ResultPanel.transform, "", 28, UiKit.Seal, bc, bc, new Vector2(0, 176), new Vector2(900, 44), TextAnchor.MiddleCenter, FontStyle.Bold);
        _rewardText.gameObject.AddComponent<Outline>().effectColor = new Color(.97f, .96f, .93f, .9f);
        _rewardText.transform.parent.name = "Reward";
      }
      string extra = "";
      if (got >= 0 && Achievements.JustUnlocked.Count > 0) { extra += "\n<size=22>Thành tựu mới: " + string.Join(", ", Achievements.JustUnlocked) + "</size>"; Achievements.JustUnlocked.Clear(); }
      if (got >= 0 && Quests.JustFinished.Count > 0) extra += "\n<size=22>Xong nhiệm vụ: " + string.Join(", ", Quests.JustFinished) + " · nhận thưởng ở menu</size>";
      if (got > 0) {
        int before = Profile.Coins - got;
        RewardFx.CountUp(_rewardText, 0, got, n => $"+{n} Hồn đăng   ·   Đang có {before + n}" + extra, 1.1f);
        RewardFx.Burst((RectTransform)_rewardText.transform, 18);
      } else _rewardText.text = (got == 0 ? "+0 Hồn đăng   ·   " : "") + $"Đang có {Profile.Coins} Hồn đăng" + extra;
      _rewardText.verticalOverflow = VerticalWrapMode.Overflow;
    }

    void ShowContestResult(GameDirector dir) {
      int secs = Mathf.FloorToInt(dir.RunSeconds);
      Set(ResultBig, $"Tranh Đăng · {secs / 60}:{secs % 60:00}");
      string title = dir.Winner < 0 ? "Hai đội hòa" : Contest.TeamName[dir.Winner] + " thắng";
      if (dir.WonByFullLight) title += " · Toàn Đăng";
      Set(ResultTitle, title);
      if (ResultTitle) ResultTitle.color = dir.Winner < 0 ? UiKit.InkBlack : Contest.TeamColor[dir.Winner];
      string L(int v) => $"<color={Hex(Contest.TeamColor[0])}>{v}</color>";
      string C(int v) => $"<color={Hex(Contest.TeamColor[1])}>{v}</color>";
      Set(ResultStats, $"{L(dir.Score(0))} – {C(dir.Score(1))}     Đèn {L(dir.ScorePart(0, 0))}·{C(dir.ScorePart(1, 0))}   Yêu quái {L(dir.ScorePart(0, 1))}·{C(dir.ScorePart(1, 1))}   Boss {L(dir.ScorePart(0, 2))}·{C(dir.ScorePart(1, 2))}");
      Set(ResultFragment, ""); // titles live in the result board now
      Set(ResultButtonText, "Đấu lại");
    }

    void ShowNightResult(GameDirector dir) {
      if (ResultTitle) ResultTitle.color = UiKit.InkBlack;
      if (ResultFragment) { ResultFragment.fontStyle = FontStyle.Italic; ResultFragment.fontSize = 26; }
      bool won = dir.State == RunState.Won;
      Set(ResultBig, $"Đêm thứ {dir.Night} · {ArenaBuilder.Seasons[Mathf.Clamp(dir.Season, 0, 3)].Name}");
      Set(ResultTitle, won ? "Bình minh đã đến" : "Đêm lại bắt đầu từ đầu");
      int cleared = won ? 4 : dir.Phase;
      int secs = Mathf.FloorToInt(dir.RunSeconds);
      Set(ResultStats, $"Canh giờ đã qua {cleared}/4   ·   Yêu quái {dir.Kills}   ·   Thời gian {secs / 60}:{secs % 60:00}");
      Set(ResultFragment, Story.Get(dir.Night)?.Fragment ?? "Thêm một đêm nữa ở phố Hồng Đăng. Đèn vẫn sáng, và bạn vẫn đứng vững.");
      Set(ResultButtonText, won ? "Chơi đêm tiếp" : "Thử lại đêm này");
    }

    // ---------------- default layout (used by the Editor prefab builder) ----------------
    /// <summary>Creates the default layout under root and fills every reference. Also used to generate HUD.prefab.</summary>
    public void BuildDefault(RectTransform root) {
      var TL = new Vector2(0, 1); var TC = new Vector2(.5f, 1); var TR = new Vector2(1, 1);
      var BL = new Vector2(0, 0); var BR = new Vector2(1, 0); var C = new Vector2(.5f, .5f); var BC = new Vector2(.5f, 0);
      var ink = UiKit.InkBlack; var grey = UiKit.InkGrey; var paper = UiKit.InkPaper;
      FoxColor = UiKit.Fox; WindColor = UiKit.Wind; GoldColor = UiKit.Gold; DashColor = grey; UltReadyColor = UiKit.Seal;
      NumberColor = ink; NumberOtherColor = grey; BigNumberColor = UiKit.Seal;

      HurtOverlay = UiKit.Panel(root, "HurtOverlay", C, C, Vector2.zero, Vector2.zero, new Color(.66f, .2f, .12f, 0), false);
      UiKit.Stretch(HurtOverlay.rectTransform);
      HurtOverlay.raycastTarget = false;

      // Joystick zone (left side). Touch anywhere in it. Brush ring with an ink-wash knob.
      var zone = UiKit.Panel(root, "JoystickZone", BL, BL, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0), false);
      zone.rectTransform.anchorMin = Vector2.zero; zone.rectTransform.anchorMax = new Vector2(.45f, .7f);
      zone.rectTransform.offsetMin = zone.rectTransform.offsetMax = Vector2.zero;
      var jBase = UiKit.InkImage(zone.transform, "JoystickBase", "InkDot", BL, C, new Vector2(160, 160), new Vector2(190, 190), new Color(.97f, .96f, .93f, .45f));
      UiKit.InkImage(jBase.transform, "Ring", "Enso", C, C, Vector2.zero, new Vector2(190, 190), new Color(grey.r, grey.g, grey.b, .8f));
      var jKnob = UiKit.InkImage(jBase.transform, "JoystickKnob", "InkDot", C, C, Vector2.zero, new Vector2(80, 80), new Color(ink.r, ink.g, ink.b, .7f));
      var joy = zone.gameObject.AddComponent<VirtualJoystick>();
      joy.Base = jBase.rectTransform; joy.Knob = jKnob.rectTransform;

      // Top-left: phase, objective, room, party. No box, a soft paper wash behind.
      UiKit.InkImage(root, "InfoWash", "Wash", TL, TL, new Vector2(-40, 30), new Vector2(620, 300), new Color(1, 1, 1, .55f));
      var phaseSeal = UiKit.SealTag(root, "PhaseSeal", "", TL, TL, new Vector2(24, -20), new Vector2(330, 48), UiKit.Seal, true, 22);
      PhaseTitle = phaseSeal.GetComponentInChildren<Text>();
      PhaseTitle.text = "Giờ Tuất · Phố chợ";
      Objective = UiKit.LabelAt(root, "Thắp lồng đèn 0/3", 22, ink, TL, TL, new Vector2(26, -78), new Vector2(560, 32), TextAnchor.MiddleLeft, FontStyle.Bold);
      NightText = UiKit.LabelAt(root, "Đêm thứ 1", 18, grey, TL, TL, new Vector2(26, -114), new Vector2(150, 26), TextAnchor.MiddleLeft);
      RoomText = UiKit.LabelAt(root, "Phòng 1234", 18, grey, TL, TL, new Vector2(170, -114), new Vector2(300, 26), TextAnchor.MiddleLeft);
      KillsText = UiKit.LabelAt(root, "Đã diệt 0", 18, grey, TL, TL, new Vector2(26, -142), new Vector2(200, 26), TextAnchor.MiddleLeft);
      PartyText = UiKit.LabelAt(root, "", 19, ink, TL, TL, new Vector2(26, -180), new Vector2(380, 120), TextAnchor.UpperLeft, FontStyle.Bold);

      // Top-center: boss name and a red brush bar.
      var bb = UiKit.Rect(root, "BossBar", TC, TC, new Vector2(0, -16), new Vector2(600, 80));
      BossBox = bb.gameObject;
      BossName = UiKit.LabelAt(bb, "Hồng Đăng Quỷ Vương", 28, ink, TC, TC, Vector2.zero, new Vector2(600, 36), TextAnchor.MiddleCenter, FontStyle.Bold);
      BossName.gameObject.AddComponent<Outline>().effectColor = new Color(.97f, .96f, .93f, .8f);
      BossFill = UiKit.InkBar(bb, TC, TC, new Vector2(0, -42), new Vector2(600, 30), UiKit.Seal);

      // Top-center (Tranh Đăng only): team scores, match clock and three lantern dots.
      var sb = UiKit.Rect(root, "ScoreBox", TC, TC, new Vector2(0, -6), new Vector2(560, 104));
      ScoreBox = sb.gameObject;
      UiKit.InkImage(sb, "Wash", "Wash", C, C, Vector2.zero, new Vector2(700, 210), new Color(1, 1, 1, .75f));
      ScoreLam = UiKit.LabelAt(sb, "0", 60, Contest.TeamColor[0], new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(20, 0), new Vector2(160, 80), TextAnchor.MiddleLeft, FontStyle.Bold);
      ScoreLam.transform.parent.name = "ScoreLam";
      ScoreChu = UiKit.LabelAt(sb, "0", 60, Contest.TeamColor[1], new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-20, 0), new Vector2(160, 80), TextAnchor.MiddleRight, FontStyle.Bold);
      ScoreChu.transform.parent.name = "ScoreChu";
      MatchClock = UiKit.LabelAt(sb, "06:00", 26, ink, TC, TC, new Vector2(0, -6), new Vector2(200, 34), TextAnchor.MiddleCenter, FontStyle.Bold);
      MatchClock.transform.parent.name = "MatchClock";
      LanternPips = new Image[3];
      for (int i = 0; i < 3; i++) {
        var ring = UiKit.InkImage(sb, "LanternPip_" + i, "Enso", TC, TC, new Vector2((i - 1) * 54, -48), new Vector2(42, 42), ink);
        LanternPips[i] = UiKit.InkImage(ring.transform, "Fill", "InkDot", C, C, Vector2.zero, new Vector2(30, 30), paper);
      }
      ScoreBox.SetActive(false);

      ToastText = UiKit.LabelAt(root, "", 30, ink, new Vector2(.5f, .76f), C, Vector2.zero, new Vector2(900, 44), TextAnchor.MiddleCenter, FontStyle.Bold);
      ToastText.gameObject.AddComponent<Outline>().effectColor = new Color(.97f, .96f, .93f, .9f);
      ToastText.raycastTarget = false;
      ToastText.transform.parent.name = "Toast";

      // Top-right: leave, then pings under it.
      LeaveButton = UiKit.InkButton(root, "Rời phòng", TR, TR, new Vector2(-16, -14), new Vector2(170, 60), false, null, 20);
      LeaveButton.name = "LeaveButton";

      // Bottom-center: you. Name, mask seal, resonance seal, hp and ult brush bars.
      var me = UiKit.Rect(root, "PlayerPanel", BC, BC, new Vector2(0, 14), new Vector2(480, 140));
      UiKit.InkImage(me, "Wash", "Wash", C, C, Vector2.zero, new Vector2(620, 260), new Color(1, 1, 1, .7f));
      NameText = UiKit.LabelAt(me, "Tín", 24, ink, TL, TL, Vector2.zero, new Vector2(150, 34), TextAnchor.MiddleLeft, FontStyle.Bold);
      MaskTagBg = UiKit.SealTag(me, "MaskTag", "Hồ Ly", TL, TL, new Vector2(156, -1), new Vector2(104, 34), UiKit.Fox, true, 18);
      MaskTagText = MaskTagBg.GetComponentInChildren<Text>();
      var res = UiKit.SealTag(me, "ResonanceTag", "Cộng hưởng", TL, TL, new Vector2(268, -1), new Vector2(140, 34), UiKit.Gold, true, 17);
      ResonanceTag = res.gameObject;
      HpFill = UiKit.InkBar(me, TL, TL, new Vector2(-4, -40), new Vector2(488, 30), UiKit.Pine);
      HpText = UiKit.LabelAt(me, "100 / 100", 17, grey, TL, TL, new Vector2(0, -70), new Vector2(200, 24), TextAnchor.MiddleLeft);
      UltFill = UiKit.InkBar(me, TL, TL, new Vector2(-4, -94), new Vector2(488, 18), UiKit.Gold);
      UltText = UiKit.LabelAt(me, "Tuyệt kỹ 0%", 17, grey, TL, TL, new Vector2(0, -112), new Vector2(200, 24), TextAnchor.MiddleLeft);

      // Banner.
      var bn = UiKit.Rect(root, "Banner", new Vector2(.5f, .66f), C, Vector2.zero, new Vector2(900, 240));
      BannerGroup = bn.gameObject.AddComponent<CanvasGroup>();
      BannerGroup.alpha = 0; BannerGroup.blocksRaycasts = false;
      UiKit.InkImage(bn, "Wash", "Wash", C, C, new Vector2(0, -10), new Vector2(1100, 360), new Color(1, 1, 1, .85f));
      BannerBig = UiKit.LabelAt(bn, "1", 96, UiKit.Seal, TC, TC, Vector2.zero, new Vector2(900, 110), TextAnchor.MiddleCenter, FontStyle.Bold);
      BannerTitle = UiKit.LabelAt(bn, "Giờ Tuất · Phố chợ", 48, ink, TC, TC, new Vector2(0, -110), new Vector2(900, 60), TextAnchor.MiddleCenter, FontStyle.Bold);
      BannerSub = UiKit.LabelAt(bn, "Thắp 3 lồng đèn để mở đường", 22, grey, TC, TC, new Vector2(0, -172), new Vector2(900, 32), TextAnchor.MiddleCenter);

      NumbersLayer = UiKit.Rect(root, "DamageNumbers", C, C, Vector2.zero, Vector2.zero);
      UiKit.Stretch(NumbersLayer);
      var numRt = UiKit.Rect(NumbersLayer, "NumberTemplate", C, C, Vector2.zero, new Vector2(200, 50));
      NumberTemplate = numRt.gameObject.AddComponent<Text>();
      NumberTemplate.font = UiKit.Font; NumberTemplate.fontSize = 28; NumberTemplate.fontStyle = FontStyle.Bold;
      NumberTemplate.alignment = TextAnchor.MiddleCenter; NumberTemplate.color = ink; NumberTemplate.text = "123";
      NumberTemplate.raycastTarget = false;
      NumberTemplate.gameObject.AddComponent<Outline>().effectColor = new Color(.97f, .96f, .93f, .9f);
      NumberTemplate.gameObject.SetActive(false);

      // Action pad (bottom-right): brush rings, colored by mask.
      var pad = UiKit.Rect(root, "ActionPad", BR, BR, new Vector2(-24, 24), new Vector2(420, 330));
      Pad = pad.gameObject;
      Buttons.Clear();
      AddButton(pad, "atk", "Chém", new Vector2(0, 0), 156, ink);
      AddButton(pad, "s1", "Hỏa", new Vector2(-172, 0), 108, UiKit.Fox);
      AddButton(pad, "dash", "Né", new Vector2(-294, 0), 90, grey);
      AddButton(pad, "s2", "Huyễn", new Vector2(-156, 122), 108, UiKit.Fox);
      AddButton(pad, "swap", "Đổi", new Vector2(-282, 112), 96, UiKit.Gold);
      AddButton(pad, "ult", "Cửu vĩ", new Vector2(-24, 174), 124, UiKit.Gold);

      // Downed.
      var dn = UiKit.InkImage(root, "DownedPanel", "Wash", new Vector2(.5f, .3f), C, Vector2.zero, new Vector2(760, 220), new Color(1, 1, 1, .92f));
      DownedPanel = dn.gameObject;
      UiKit.LabelAt(dn.transform, "Bạn đã gục. Đồng đội đứng cạnh bạn 3 giây để cứu.", 22, ink, C, C, new Vector2(0, 22), new Vector2(620, 40), TextAnchor.MiddleCenter, FontStyle.Bold);
      ReviveFill = UiKit.InkBar(dn.transform, C, C, new Vector2(0, -26), new Vector2(480, 24), UiKit.Pine);

      // Blessing: misty mountains backdrop with three hanging scrolls.
      var bp = UiKit.Backdrop(root, "BlessingPanel", "BlessBg");
      BlessPanel = bp.gameObject;
      UiKit.LabelAt(bp.transform, "Chọn một phúc lành", 46, ink, TC, TC, new Vector2(0, -26), new Vector2(900, 56), TextAnchor.MiddleCenter, FontStyle.Bold);
      BlessTitle = UiKit.LabelAt(bp.transform, "Giờ Tuất đã qua · tiếp theo: Giờ Hợi", 22, grey, TC, TC, new Vector2(0, -86), new Vector2(900, 30), TextAnchor.MiddleCenter);
      BlessCards = UiKit.Rect(bp.transform, "Cards", C, C, new Vector2(0, -20), new Vector2(1100, 560));
      BlessCardSpacing = 350;
      var card = UiKit.InkImage(BlessCards, "CardTemplate", "Scroll", C, C, Vector2.zero, new Vector2(300, 470), Color.white);
      card.type = Image.Type.Simple;
      card.raycastTarget = true;
      BlessCardTemplate = card.gameObject.AddComponent<Button>();
      var art = UiKit.InkImage(card.transform, "Art", "InkDot", TC, TC, new Vector2(0, -62), new Vector2(120, 120), new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, .18f));
      UiKit.InkImage(art.transform, "Ring", "Enso", C, C, Vector2.zero, new Vector2(120, 120), ink);
      UiKit.LabelAt(card.transform, "Tên phúc lành", 27, ink, TC, TC, new Vector2(0, -196), new Vector2(206, 70), TextAnchor.UpperLeft, FontStyle.Bold).transform.parent.name = "Title";
      UiKit.LabelAt(card.transform, "Mô tả phúc lành", 19, grey, TC, TC, new Vector2(0, -272), new Vector2(206, 150), TextAnchor.UpperLeft).transform.parent.name = "Desc";
      card.gameObject.SetActive(false);
      BlessWait = UiKit.LabelAt(bp.transform, "", 20, grey, BC, BC, new Vector2(0, 22), new Vector2(900, 30), TextAnchor.MiddleCenter);
      BlessPanel.SetActive(false);

      // Result: dawn landscape.
      var rp = UiKit.Backdrop(root, "ResultPanel", "ResultBg");
      ResultPanel = rp.gameObject;
      var rt = rp.transform;
      ResultBig = UiKit.LabelAt(rt, "Đêm thứ 2", 22, grey, TC, TC, new Vector2(0, -34), new Vector2(680, 30), TextAnchor.MiddleCenter, FontStyle.Bold);
      ResultTitle = UiKit.LabelAt(rt, "Bình minh đã đến", 72, ink, TC, TC, new Vector2(0, -70), new Vector2(1000, 90), TextAnchor.MiddleCenter, FontStyle.Bold);
      ResultStats = UiKit.LabelAt(rt, "", 24, ink, TC, TC, new Vector2(0, -176), new Vector2(1000, 40), TextAnchor.MiddleCenter, FontStyle.Bold);
      ResultFragment = UiKit.LabelAt(rt, "", 26, ink, TC, TC, new Vector2(0, -236), new Vector2(820, 110), TextAnchor.UpperCenter, FontStyle.Italic);
      ResultButton = UiKit.InkButton(rt, "Chơi đêm tiếp", BC, BC, new Vector2(170, 34), new Vector2(400, 108), true, null, 28);
      ResultButton.name = "ResultButton";
      ResultButtonText = ResultButton.GetComponentInChildren<Text>();
      ResultLobbyButton = UiKit.InkButton(rt, "Về phòng chờ", BC, BC, new Vector2(-190, 44), new Vector2(340, 84), false, null, 26);
      ResultLobbyButton.name = "ResultLobbyButton";
      ResultWaitText = UiKit.LabelAt(rt, "Chờ chủ phòng chọn chơi tiếp hoặc về phòng chờ...", 22, grey, BC, BC, new Vector2(0, 60), new Vector2(900, 40), TextAnchor.MiddleCenter);
      ResultWaitText.transform.parent.name = "ResultWaitText";
      ResultPanel.SetActive(false);

      // Ping bar (top-right under Rời phòng, online only).
      var pb = UiKit.Rect(root, "PingBar", TR, TR, new Vector2(-16, -86), new Vector2(480, 56));
      PingBar = pb.gameObject;
      PingButtons = new Button[Pings.Text.Length];
      for (int i = 0; i < Pings.Text.Length; i++) {
        PingButtons[i] = UiKit.InkButton(pb, $"{i + 1} · {Pings.Text[i]}", TR, TR, new Vector2(-(Pings.Text.Length - 1 - i) * 160, 0), new Vector2(152, 56), false, null, 17);
        PingButtons[i].GetComponentInChildren<Text>().color = Pings.Color[i];
        PingButtons[i].name = "Ping_" + i;
      }
      var pt = UiKit.Rect(NumbersLayer, "PingTemplate", C, C, Vector2.zero, new Vector2(460, 40));
      PingTemplate = pt.gameObject.AddComponent<Text>();
      PingTemplate.font = UiKit.Font; PingTemplate.fontSize = PingSize; PingTemplate.fontStyle = FontStyle.Bold;
      PingTemplate.alignment = TextAnchor.MiddleCenter; PingTemplate.color = ink; PingTemplate.text = "Lan: Lại đây!";
      PingTemplate.raycastTarget = false;
      PingTemplate.gameObject.AddComponent<Outline>().effectColor = new Color(.97f, .96f, .93f, .95f);
      PingTemplate.gameObject.SetActive(false);
    }

    void AddButton(RectTransform pad, string id, string text, Vector2 pos, float size, Color ring) {
      // Ring = brush ensō tinted by mask color; face = paper ink dot; cooldown = dark ink dot.
      var outer = UiKit.InkImage(pad, "Button_" + id, "Enso", new Vector2(1, 0), new Vector2(1, 0), pos, new Vector2(size, size), ring);
      outer.raycastTarget = true;
      var inner = UiKit.InkImage(outer.transform, "Face", "InkDot", C5, C5, Vector2.zero, new Vector2(size - 16, size - 16), new Color(.97f, .96f, .93f, .82f));
      inner.transform.SetAsFirstSibling();
      var icon = UiKit.Panel(outer.transform, "Icon", C5, C5, Vector2.zero, new Vector2(size * .62f, size * .62f), Color.white, false);
      icon.raycastTarget = false;
      icon.preserveAspect = true;
      icon.gameObject.SetActive(false);
      var label = UiKit.Label(inner.transform, text, Mathf.RoundToInt(size * .22f), ring, TextAnchor.MiddleCenter, FontStyle.Bold);
      var cd = UiKit.InkImage(outer.transform, "Cooldown", "InkDot", C5, C5, Vector2.zero, new Vector2(size - 10, size - 10), new Color(.15f, .16f, .16f, .85f));
      var cdText = UiKit.Label(cd.transform, "", 26, UiKit.InkPaper, TextAnchor.MiddleCenter, FontStyle.Bold);
      cd.gameObject.SetActive(false);
      var tb = outer.gameObject.AddComponent<TouchButton>();
      Buttons.Add(new ActionButton { Id = id, Ring = outer, Label = label, Cooldown = cd.gameObject, CooldownText = cdText, Touch = tb, Icon = icon, MaskIcons = new Sprite[4] });
    }

    static readonly Vector2 C5 = new(.5f, .5f);
  }
}
