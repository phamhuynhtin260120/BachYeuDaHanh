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
      [Tooltip("Optional: icons per mask (0 = Hồ Ly, 1 = Tengu). Shown instead of the text label when set.")]
      public Image Icon;
      public Sprite[] MaskIcons = new Sprite[2];
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
    public Button ResultButton;

    [Header("Colors")]
    public Color FoxColor = UiKit.Fox;
    public Color WindColor = UiKit.Wind;
    public Color GoldColor = UiKit.Gold;
    public Color DashColor = UiKit.Ink2;
    public Color UltReadyColor = UiKit.Seal;
    public Color NumberColor = UiKit.Ink, NumberOtherColor = UiKit.Ink2, BigNumberColor = UiKit.Seal;

    static readonly string[] Fragments = {
      "Đêm đầu tiên: cổng Hồng Đăng không đóng. Bà bán lồng đèn đầu phố nói đây không phải lần đầu.",
      "Dưới gầm cầu có một chiếc mặt nạ cáo nứt đôi. Nó còn ấm, như vừa có người đeo.",
      "Miêu yêu thì thầm trước khi tan: \"Người giữ đèn không muốn trời sáng.\"",
      "Trên quả chuông đền khắc tên một đứa trẻ. Cái tên ấy giống hệt tên bạn.",
      "Quỷ Vương không canh cổng. Nó cũng bị nhốt sau cổng, giống như bạn.",
      "Bạn nhớ ra ai đã thắp ngọn đèn đầu tiên. Mùa sau, bạn sẽ quay lại tìm người ấy.",
    };

    float _bannerT, _hurtT;
    readonly Dictionary<string, ActionButton> _btn = new();
    readonly List<(Text t, Vector3 world, float life)> _numbers = new();
    readonly Stack<Text> _numberPool = new();
    int _blessRolledFor = -1, _blessPickedFor = -1;
    bool _resultShown;
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

    void Init(string roomCode, Action onLeave) {
      _roomCode = roomCode;
      foreach (var b in Buttons) if (b != null && !string.IsNullOrEmpty(b.Id)) _btn[b.Id] = b;
      Wire("atk", () => GameInput.AttackButtonHeld = true, () => GameInput.AttackButtonHeld = false);
      Wire("s1", () => GameInput.Press(GameAction.Skill1));
      Wire("s2", () => GameInput.Press(GameAction.Skill2));
      Wire("dash", () => GameInput.Press(GameAction.Dash));
      Wire("swap", () => GameInput.Press(GameAction.Swap));
      Wire("ult", () => GameInput.Press(GameAction.Ult));
      if (LeaveButton) LeaveButton.onClick.AddListener(() => onLeave?.Invoke());
      if (ResultButton) ResultButton.onClick.AddListener(() => { if (GameDirector.Instance) GameDirector.Instance.RPC_RequestRestart(); });
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

    public void DamageNumber(Vector3 world, int value, bool big, bool mine) {
      if (value <= 0 || _numbers.Count > 40 || NumberTemplate == null) return;
      var t = _numberPool.Count > 0 ? _numberPool.Pop() : Instantiate(NumberTemplate, NumbersLayer);
      t.gameObject.SetActive(true);
      t.text = value.ToString();
      t.fontSize = big ? BigNumberSize : NumberSize;
      t.color = big ? BigNumberColor : mine ? NumberColor : NumberOtherColor;
      _numbers.Add((t, world + new Vector3(UnityEngine.Random.Range(-.5f, .5f), 0, 0), .8f));
    }

    // ---------------- update ----------------
    void Update() {
      float dt = Time.deltaTime;
      var me = PlayerAvatar.Local;
      var dir = GameDirector.Instance;

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
      UpdateNumbers(dt);
      if (me == null || dir == null) return;

      var mask = Masks.All[me.Mask];
      var maskColor = me.Mask == 0 ? FoxColor : WindColor;
      Set(MaskTagText, mask.Name);
      if (MaskTagBg) MaskTagBg.color = maskColor;
      if (ResonanceTag) ResonanceTag.SetActive(me.ResonanceTime > 0);
      Fill(HpFill, me.MaxHp > 0 ? me.Hp / (float)me.MaxHp : 0);
      Set(HpText, $"{Mathf.Max(0, me.Hp)} / {me.MaxHp}");
      Fill(UltFill, me.Ult / 100f);
      Set(UltText, $"Tuyệt kỹ {Mathf.FloorToInt(me.Ult)}%");
      Set(PartyText, string.Join("\n", PlayerAvatar.All.Where(p => p != me)
        .Select(p => $"{p.DisplayName}  {(p.Downed ? "· ĐÃ GỤC" : $"{p.Hp}/{p.MaxHp}")}")));

      Set(PhaseTitle, Phases.Title[Mathf.Clamp(dir.Phase, 0, 3)]);
      Set(Objective, dir.State switch {
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
      var boss = dir.Boss;
      bool showBoss = boss != null && boss.Hp > 0 && dir.State == RunState.Playing;
      if (BossBox) BossBox.SetActive(showBoss);
      if (showBoss) {
        Set(BossName, EnemyStats.Name(boss.Type) + (boss.Rage ? " · nổi giận" : ""));
        Fill(BossFill, boss.Hp / (float)Mathf.Max(1, boss.MaxHp));
      }
      Set(NightText, $"Đêm thứ {dir.Night}");
      Set(RoomText, string.IsNullOrEmpty(_roomCode) ? "Chơi một mình" : $"Phòng {_roomCode} · {dir.PlayerCount} người");
      Set(KillsText, $"Đã diệt {dir.Kills}");

      SetButton("s1", mask.S1, me.SkillCooldown(1), maskColor, me.Mask);
      SetButton("s2", mask.S2, me.SkillCooldown(2), maskColor, me.Mask);
      SetButton("swap", "Đổi", me.SwapCooldown, GoldColor, me.Mask);
      SetButton("dash", "Né", me.DashCooldown, DashColor, me.Mask);
      if (_btn.TryGetValue("ult", out var ult)) {
        Set(ult.Label, mask.Ult);
        if (ult.Ring) ult.Ring.color = me.Ult >= 100 ? UltReadyColor : GoldColor;
        if (ult.Cooldown) ult.Cooldown.SetActive(me.Ult < 100);
        Set(ult.CooldownText, $"{Mathf.FloorToInt(me.Ult)}%");
        ApplyIcon(ult, me.Mask);
      }

      if (DownedPanel) DownedPanel.SetActive(me.Downed && dir.State == RunState.Playing);
      if (me.Downed) Fill(ReviveFill, me.ReviveProgress);

      UpdateBlessing(me, dir);
      UpdateResult(dir);
    }

    static void Set(Text t, string s) { if (t && t.text != s) t.text = s; }

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
      ResultPanel.SetActive(true);
      bool won = dir.State == RunState.Won;
      Set(ResultBig, won ? "☼" : "☾");
      Set(ResultTitle, won ? "Bình minh đã đến" : "Đêm lại bắt đầu từ đầu");
      int cleared = won ? 4 : dir.Phase;
      int secs = Mathf.FloorToInt(dir.RunSeconds);
      Set(ResultStats, $"Canh giờ đã qua {cleared}/4   ·   Yêu quái {dir.Kills}   ·   Thời gian {secs / 60}:{secs % 60:00}");
      Set(ResultFragment, Fragments[Mathf.Clamp(dir.Night - 1, 0, Fragments.Length - 1)]);
      Set(ResultButtonText, won ? "Chơi đêm tiếp" : "Thử lại đêm này");
    }

    // ---------------- default layout (used by the Editor prefab builder) ----------------
    /// <summary>Creates the default layout under root and fills every reference. Also used to generate HUD.prefab.</summary>
    public void BuildDefault(RectTransform root) {
      var TL = new Vector2(0, 1); var TC = new Vector2(.5f, 1); var TR = new Vector2(1, 1);
      var BL = new Vector2(0, 0); var BR = new Vector2(1, 0); var C = new Vector2(.5f, .5f);

      HurtOverlay = UiKit.Panel(root, "HurtOverlay", C, C, Vector2.zero, Vector2.zero, new Color(.66f, .2f, .12f, 0), false);
      UiKit.Stretch(HurtOverlay.rectTransform);
      HurtOverlay.raycastTarget = false;

      // Joystick zone (left side). Touch anywhere in it.
      var zone = UiKit.Panel(root, "JoystickZone", BL, BL, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0), false);
      zone.rectTransform.anchorMin = Vector2.zero; zone.rectTransform.anchorMax = new Vector2(.45f, .7f);
      zone.rectTransform.offsetMin = zone.rectTransform.offsetMax = Vector2.zero;
      var jBase = UiKit.Panel(zone.transform, "JoystickBase", BL, C, new Vector2(150, 150), new Vector2(170, 170), new Color(.97f, .94f, .89f, .35f));
      var jKnob = UiKit.Panel(jBase.transform, "JoystickKnob", C, C, Vector2.zero, new Vector2(72, 72), new Color(.66f, .2f, .12f, .55f));
      jBase.raycastTarget = jKnob.raycastTarget = false;
      jBase.type = jKnob.type = Image.Type.Simple;
      var joy = zone.gameObject.AddComponent<VirtualJoystick>();
      joy.Base = jBase.rectTransform; joy.Knob = jKnob.rectTransform;

      // Top-left: player panel.
      var tl = UiKit.Panel(root, "PlayerPanel", TL, TL, new Vector2(16, -16), new Vector2(330, 150), UiKit.PaperGlass);
      NameText = UiKit.LabelAt(tl.transform, "Tiểu Đăng", 20, UiKit.Ink, TL, TL, new Vector2(14, -10), new Vector2(130, 28), TextAnchor.MiddleLeft, FontStyle.Bold);
      MaskTagBg = UiKit.Panel(tl.transform, "MaskTag", TL, TL, new Vector2(140, -12), new Vector2(84, 24), UiKit.Fox);
      MaskTagText = UiKit.Label(MaskTagBg.transform, "Hồ Ly", 15, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
      var res = UiKit.Panel(tl.transform, "ResonanceTag", TL, TL, new Vector2(230, -12), new Vector2(90, 24), Gfx.Hex("#f2c14e"));
      UiKit.Label(res.transform, "Cộng hưởng", 13, UiKit.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
      ResonanceTag = res.gameObject;
      HpFill = UiKit.Bar(tl.transform, TL, TL, new Vector2(14, -46), new Vector2(302, 14), UiKit.Jade);
      HpText = UiKit.LabelAt(tl.transform, "100 / 100", 15, UiKit.Ink2, TL, TL, new Vector2(14, -62), new Vector2(200, 22), TextAnchor.MiddleLeft);
      UltFill = UiKit.Bar(tl.transform, TL, TL, new Vector2(14, -90), new Vector2(302, 9), UiKit.Gold);
      UltText = UiKit.LabelAt(tl.transform, "Tuyệt kỹ 0%", 15, UiKit.Ink2, TL, TL, new Vector2(14, -102), new Vector2(200, 22), TextAnchor.MiddleLeft);
      PartyText = UiKit.LabelAt(root, "", 16, UiKit.Ink, TL, TL, new Vector2(22, -176), new Vector2(330, 120), TextAnchor.UpperLeft, FontStyle.Bold);

      // Top-center: phase and boss.
      var tc = UiKit.Panel(root, "PhasePanel", TC, TC, new Vector2(0, -16), new Vector2(460, 74), UiKit.PaperGlass);
      PhaseTitle = UiKit.LabelAt(tc.transform, "Giờ Tuất · Phố chợ", 21, UiKit.Ink, TC, TC, new Vector2(0, -8), new Vector2(440, 30), TextAnchor.MiddleCenter, FontStyle.Bold);
      Objective = UiKit.LabelAt(tc.transform, "Thắp lồng đèn 0/3", 16, UiKit.Ink2, TC, TC, new Vector2(0, -40), new Vector2(440, 26), TextAnchor.MiddleCenter);
      var bb = UiKit.Rect(root, "BossBar", TC, TC, new Vector2(0, -98), new Vector2(520, 44));
      BossBox = bb.gameObject;
      BossName = UiKit.LabelAt(bb, "Hồng Đăng Quỷ Vương", 18, Gfx.Hex("#5b2d6e"), TC, TC, Vector2.zero, new Vector2(520, 22), TextAnchor.MiddleCenter, FontStyle.Bold);
      BossFill = UiKit.Bar(bb, TC, TC, new Vector2(0, -24), new Vector2(520, 16), UiKit.Seal, new Color(.16f, .13f, .11f, .25f));

      // Top-right: night, room, leave.
      var tr = UiKit.Panel(root, "InfoPanel", TR, TR, new Vector2(-16, -16), new Vector2(250, 96), UiKit.PaperGlass);
      NightText = UiKit.LabelAt(tr.transform, "Đêm thứ 1", 19, UiKit.Ink, TR, TR, new Vector2(-14, -8), new Vector2(230, 28), TextAnchor.MiddleRight, FontStyle.Bold);
      RoomText = UiKit.LabelAt(tr.transform, "Phòng 1234", 15, UiKit.Ink2, TR, TR, new Vector2(-14, -38), new Vector2(230, 22), TextAnchor.MiddleRight);
      KillsText = UiKit.LabelAt(tr.transform, "Đã diệt 0", 15, UiKit.Ink2, TR, TR, new Vector2(-14, -62), new Vector2(230, 22), TextAnchor.MiddleRight);
      LeaveButton = UiKit.Button(root, "Rời phòng", TR, TR, new Vector2(-16, -120), new Vector2(130, 40), UiKit.PaperGlass, UiKit.Ink, null, 16);
      LeaveButton.name = "LeaveButton";

      // Banner.
      var bn = UiKit.Rect(root, "Banner", new Vector2(.5f, .66f), C, Vector2.zero, new Vector2(900, 220));
      BannerGroup = bn.gameObject.AddComponent<CanvasGroup>();
      BannerGroup.alpha = 0; BannerGroup.blocksRaycasts = false;
      BannerBig = UiKit.LabelAt(bn, "1", 96, UiKit.Seal, TC, TC, Vector2.zero, new Vector2(900, 110), TextAnchor.MiddleCenter, FontStyle.Bold);
      BannerTitle = UiKit.LabelAt(bn, "Giờ Tuất · Phố chợ", 44, UiKit.Ink, TC, TC, new Vector2(0, -110), new Vector2(900, 56), TextAnchor.MiddleCenter, FontStyle.Bold);
      BannerSub = UiKit.LabelAt(bn, "Thắp 3 lồng đèn để mở đường", 20, UiKit.Ink2, TC, TC, new Vector2(0, -166), new Vector2(900, 30), TextAnchor.MiddleCenter);

      NumbersLayer = UiKit.Rect(root, "DamageNumbers", C, C, Vector2.zero, Vector2.zero);
      UiKit.Stretch(NumbersLayer);
      var numRt = UiKit.Rect(NumbersLayer, "NumberTemplate", C, C, Vector2.zero, new Vector2(200, 50));
      NumberTemplate = numRt.gameObject.AddComponent<Text>();
      NumberTemplate.font = UiKit.Font; NumberTemplate.fontSize = 28; NumberTemplate.fontStyle = FontStyle.Bold;
      NumberTemplate.alignment = TextAnchor.MiddleCenter; NumberTemplate.color = UiKit.Ink; NumberTemplate.text = "123";
      NumberTemplate.raycastTarget = false;
      NumberTemplate.gameObject.AddComponent<Outline>().effectColor = new Color(.97f, .94f, .89f, .9f);
      NumberTemplate.gameObject.SetActive(false);

      // Action pad (bottom-right).
      var pad = UiKit.Rect(root, "ActionPad", BR, BR, new Vector2(-24, 24), new Vector2(420, 330));
      Pad = pad.gameObject;
      Buttons.Clear();
      AddButton(pad, "atk", "Chém", new Vector2(0, 0), 150, UiKit.Seal);
      AddButton(pad, "s1", "Hỏa", new Vector2(-168, 0), 104, UiKit.Fox);
      AddButton(pad, "dash", "Né", new Vector2(-288, 0), 88, UiKit.Ink2);
      AddButton(pad, "s2", "Huyễn", new Vector2(-152, 120), 104, UiKit.Fox);
      AddButton(pad, "swap", "Đổi", new Vector2(-276, 110), 94, UiKit.Gold);
      AddButton(pad, "ult", "Cửu vĩ", new Vector2(-26, 170), 116, UiKit.Gold);

      // Downed.
      var dn = UiKit.Panel(root, "DownedPanel", new Vector2(.5f, .3f), C, Vector2.zero, new Vector2(560, 96), UiKit.PaperGlass);
      DownedPanel = dn.gameObject;
      UiKit.LabelAt(dn.transform, "Bạn đã gục. Đồng đội đứng cạnh bạn 3 giây để cứu.", 19, UiKit.Ink, TC, TC, new Vector2(0, -12), new Vector2(540, 40), TextAnchor.MiddleCenter, FontStyle.Bold);
      ReviveFill = UiKit.Bar(dn.transform, TC, TC, new Vector2(0, -60), new Vector2(460, 12), UiKit.Jade);

      // Blessing.
      var bp = UiKit.Panel(root, "BlessingPanel", C, C, Vector2.zero, new Vector2(980, 420), UiKit.Paper);
      BlessPanel = bp.gameObject;
      UiKit.LabelAt(bp.transform, "CHỌN MỘT PHÚC LÀNH LỄ HỘI", 15, UiKit.Seal, TC, TC, new Vector2(0, -20), new Vector2(900, 24), TextAnchor.MiddleCenter, FontStyle.Bold);
      BlessTitle = UiKit.LabelAt(bp.transform, "Giờ Tuất đã qua · tiếp theo: Giờ Hợi", 30, UiKit.Ink, TC, TC, new Vector2(0, -48), new Vector2(900, 40), TextAnchor.MiddleCenter, FontStyle.Bold);
      BlessCards = UiKit.Rect(bp.transform, "Cards", C, C, new Vector2(0, -20), new Vector2(920, 220));
      var card = UiKit.Panel(BlessCards, "CardTemplate", C, C, Vector2.zero, new Vector2(280, 210), Gfx.Hex("#ece4d4"));
      BlessCardTemplate = card.gameObject.AddComponent<Button>();
      UiKit.LabelAt(card.transform, "Tên phúc lành", 24, UiKit.Ink, TC, TC, new Vector2(0, -16), new Vector2(250, 60), TextAnchor.UpperLeft, FontStyle.Bold).transform.parent.name = "Title";
      UiKit.LabelAt(card.transform, "Mô tả phúc lành", 18, UiKit.Ink2, TC, TC, new Vector2(0, -80), new Vector2(250, 120), TextAnchor.UpperLeft).transform.parent.name = "Desc";
      card.gameObject.SetActive(false);
      BlessWait = UiKit.LabelAt(bp.transform, "", 18, UiKit.Ink2, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 18), new Vector2(900, 30), TextAnchor.MiddleCenter);
      BlessPanel.SetActive(false);

      // Result.
      var rp = UiKit.Panel(root, "ResultPanel", C, C, Vector2.zero, new Vector2(720, 470), UiKit.Paper);
      ResultPanel = rp.gameObject;
      ResultBig = UiKit.LabelAt(rp.transform, "☼", 70, UiKit.Seal, TC, TC, new Vector2(0, -18), new Vector2(680, 80), TextAnchor.MiddleCenter, FontStyle.Bold);
      ResultTitle = UiKit.LabelAt(rp.transform, "Bình minh đã đến", 36, UiKit.Ink, TC, TC, new Vector2(0, -100), new Vector2(680, 46), TextAnchor.MiddleCenter, FontStyle.Bold);
      ResultStats = UiKit.LabelAt(rp.transform, "", 20, UiKit.Ink2, TC, TC, new Vector2(0, -156), new Vector2(680, 60), TextAnchor.MiddleCenter);
      ResultFragment = UiKit.LabelAt(rp.transform, "", 20, UiKit.Ink, TC, TC, new Vector2(0, -228), new Vector2(620, 110), TextAnchor.UpperCenter, FontStyle.Italic);
      ResultButton = UiKit.Button(rp.transform, "Chơi đêm tiếp", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 28), new Vector2(260, 60), UiKit.Seal, UiKit.Paper, null, 24);
      ResultButton.name = "ResultButton";
      ResultButtonText = ResultButton.GetComponentInChildren<Text>();
      ResultPanel.SetActive(false);
    }

    void AddButton(RectTransform pad, string id, string text, Vector2 pos, float size, Color ring) {
      var outer = UiKit.Panel(pad, "Button_" + id, new Vector2(1, 0), new Vector2(1, 0), pos, new Vector2(size, size), ring);
      var inner = UiKit.Panel(outer.transform, "Face", C5, C5, Vector2.zero, new Vector2(size - 8, size - 8), UiKit.Paper);
      inner.raycastTarget = false;
      outer.type = inner.type = Image.Type.Simple;
      var icon = UiKit.Panel(outer.transform, "Icon", C5, C5, Vector2.zero, new Vector2(size * .62f, size * .62f), Color.white, false);
      icon.raycastTarget = false;
      icon.preserveAspect = true;
      icon.gameObject.SetActive(false);
      var label = UiKit.Label(inner.transform, text, Mathf.RoundToInt(size * .22f), ring, TextAnchor.MiddleCenter, FontStyle.Bold);
      var cd = UiKit.Panel(outer.transform, "Cooldown", C5, C5, Vector2.zero, new Vector2(size, size), new Color(.16f, .13f, .11f, .62f));
      cd.raycastTarget = false;
      cd.type = Image.Type.Simple;
      var cdText = UiKit.Label(cd.transform, "", 24, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
      cd.gameObject.SetActive(false);
      var tb = outer.gameObject.AddComponent<TouchButton>();
      Buttons.Add(new ActionButton { Id = id, Ring = outer, Label = label, Cooldown = cd.gameObject, CooldownText = cdText, Touch = tb, Icon = icon, MaskIcons = new Sprite[2] });
    }

    static readonly Vector2 C5 = new(.5f, .5f);
  }
}
