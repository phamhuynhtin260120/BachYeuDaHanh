using System;
using UnityEngine;
using UnityEngine.UI;

namespace BachYeu {
  /// <summary>
  /// Nhiệm vụ hằng ngày: the 7-day login gift on top, today's three quests below, each with its own "Nhận" button.
  /// Opened from the title menu.
  /// </summary>
  public class DailyQuestPanel : MonoBehaviour {
    public static DailyQuestPanel Current { get; private set; }

    Action _onClose;
    Text _coins, _reset, _streakTitle, _giftLabel, _msg;
    Button _gift;
    readonly Image[] _days = new Image[Quests.StreakGifts.Length];
    readonly Text[] _dayText = new Text[Quests.StreakGifts.Length];
    readonly Text[] _qName = new Text[Quests.PerDay], _qCount = new Text[Quests.PerDay], _qReward = new Text[Quests.PerDay], _qBtnText = new Text[Quests.PerDay];
    readonly RectTransform[] _qFill = new RectTransform[Quests.PerDay];
    readonly Button[] _qBtn = new Button[Quests.PerDay];
    float _tick;

    public static void Open(Action onClose = null) {
      if (Current) return;
      Quests.Refresh();
      SettingsPanel.EnsureEventSystem();
      var canvas = UiKit.Canvas("Quests", 88);
      var p = canvas.gameObject.AddComponent<DailyQuestPanel>();
      p._onClose = onClose;
      p.Build((RectTransform)canvas.transform);
      Current = p;
      p.Refresh();
      Sfx.Play(SoundId.UiOpen);
    }

    void OnDestroy() { if (Current == this) Current = null; }

    public void Close() {
      Sfx.Play(SoundId.UiBack);
      var cb = _onClose;
      Destroy(gameObject);
      cb?.Invoke();
    }

    static readonly Vector2 TL = new(0, 1), TR = new(1, 1), C = new(.5f, .5f), BR = new(1, 0), BL = new(0, 0), TC = new(.5f, 1);

    void Build(RectTransform root) {
      var dim = UiKit.Rect(root, "Dim", C, C, Vector2.zero, Vector2.zero);
      UiKit.Stretch(dim);
      dim.gameObject.AddComponent<Image>().color = new Color(.15f, .15f, .14f, .45f);

      var card = UiKit.Rect(root, "Card", C, C, Vector2.zero, new Vector2(1100, 780));
      card.gameObject.AddComponent<Image>().color = UiKit.InkPaper;
      UiKit.InkImage(card, "Wash", "Wash", C, C, Vector2.zero, new Vector2(1280, 920), new Color(1, 1, 1, .9f));

      UiKit.LabelAt(card, "Nhiệm vụ hằng ngày", 46, UiKit.InkBlack, TL, TL, new Vector2(48, -24), new Vector2(640, 64), TextAnchor.MiddleLeft, FontStyle.Bold);
      _reset = UiKit.LabelAt(card, "", 19, UiKit.InkGrey, TL, TL, new Vector2(50, -86), new Vector2(640, 28), TextAnchor.MiddleLeft);
      UiKit.SealTag(card, "CoinSeal", "Hồn", TR, TR, new Vector2(-300, -34), new Vector2(54, 54), UiKit.Gold, true, 17);
      _coins = UiKit.LabelAt(card, "0", 38, UiKit.InkBlack, TR, TR, new Vector2(-48, -28), new Vector2(240, 60), TextAnchor.MiddleRight, FontStyle.Bold);
      UiKit.InkImage(card, "Rule", "BrushBar", TL, TL, new Vector2(40, -122), new Vector2(1020, 8), UiKit.InkBlack);

      // Login gift: 7 day seals and a claim button.
      _streakTitle = UiKit.LabelAt(card, "", 22, UiKit.InkBlack, TL, TL, new Vector2(48, -140), new Vector2(700, 32), TextAnchor.MiddleLeft, FontStyle.Bold);
      for (int i = 0; i < _days.Length; i++) {
        var seal = UiKit.SealTag(card, "Day" + i, "", TL, TL, new Vector2(48 + i * 106, -184), new Vector2(94, 94), UiKit.InkGrey, true, 18);
        _days[i] = seal;
        _dayText[i] = seal.GetComponentInChildren<Text>();
        _dayText[i].lineSpacing = .9f;
      }
      _gift = UiKit.InkButton(card, "Nhận", TR, TR, new Vector2(-48, -196), new Vector2(240, 76), true, ClaimGift, 24);
      _gift.name = "ClaimGift";
      _giftLabel = _gift.GetComponentInChildren<Text>();

      UiKit.LabelAt(card, "Nhiệm vụ hôm nay", 22, UiKit.InkBlack, TL, TL, new Vector2(48, -304), new Vector2(500, 32), TextAnchor.MiddleLeft, FontStyle.Bold);
      for (int i = 0; i < Quests.PerDay; i++) {
        int k = i;
        var row = UiKit.Panel(card, "Quest" + i, TL, TL, new Vector2(44, -344 - i * 108), new Vector2(1012, 98), new Color(1, 1, 1, .55f));
        _qName[i] = UiKit.LabelAt(row.transform, "", 25, UiKit.InkBlack, TL, TL, new Vector2(24, -12), new Vector2(560, 36), TextAnchor.MiddleLeft, FontStyle.Bold);
        _qFill[i] = UiKit.InkBar(row.transform, TL, TL, new Vector2(24, -62), new Vector2(520, 20), UiKit.Pine);
        _qCount[i] = UiKit.LabelAt(row.transform, "", 19, UiKit.InkGrey, TL, TL, new Vector2(560, -54), new Vector2(110, 34), TextAnchor.MiddleLeft, FontStyle.Bold);
        _qReward[i] = UiKit.LabelAt(row.transform, "", 22, UiKit.Gold, TL, TL, new Vector2(660, -32), new Vector2(110, 40), TextAnchor.MiddleCenter, FontStyle.Bold);
        _qBtn[i] = UiKit.InkButton(row.transform, "Nhận", TR, TR, new Vector2(-16, -13), new Vector2(200, 72), false, () => ClaimQuest(k), 22);
        _qBtn[i].name = "ClaimQuest" + i;
        _qBtnText[i] = _qBtn[i].GetComponentInChildren<Text>();
      }

      _msg = UiKit.LabelAt(card, "", 20, UiKit.Seal, BL, BL, new Vector2(48, 44), new Vector2(700, 30), TextAnchor.MiddleLeft, FontStyle.Bold);
      UiKit.InkButton(card, "Xong", BR, BR, new Vector2(-44, 24), new Vector2(220, 74), false, Close, 26).name = "Done";
    }

    void ClaimGift() {
      int g = Quests.TodayGift;
      if (!Quests.ClaimGift()) return;
      RewardFx.Burst((RectTransform)_gift.transform);
      _msg.text = $"Nhận quà ngày {Quests.StreakDayIndex + 1}: +{g} Hồn đăng";
      Refresh();
      RewardFx.CountUp(_coins, Profile.Coins - g, Profile.Coins, n => n.ToString(), .7f);
    }

    void ClaimQuest(int i) {
      var d = Profile.Data;
      if (i >= d.Quests.Count) return;
      var s = d.Quests[i];
      var q = Quests.Def(s.Id);
      if (!Quests.Claim(s)) {
        Sfx.Play(SoundId.UiBack);
        _msg.text = s.Claimed ? "Đã nhận thưởng nhiệm vụ này rồi." : "Chưa xong nhiệm vụ này. Vào trận để làm tiếp nhé.";
        return;
      }
      RewardFx.Burst((RectTransform)_qBtn[i].transform);
      _msg.text = $"Hoàn thành \"{Quests.Describe(q)}\": +{q.Reward} Hồn đăng";
      Refresh();
      RewardFx.CountUp(_coins, Profile.Coins - q.Reward, Profile.Coins, n => n.ToString(), .7f);
    }

    void Refresh() {
      var d = Profile.Data;
      _coins.text = Profile.Coins.ToString();
      int streak = Mathf.Max(1, d.Streak), today = Quests.StreakDayIndex;
      _streakTitle.text = $"Quà đăng nhập · ngày thứ {streak} liên tiếp" + (streak > Quests.StreakGifts.Length ? " (vòng mới)" : "");
      for (int i = 0; i < _days.Length; i++) {
        bool got = i < today || (i == today && d.StreakClaimed);
        _days[i].color = i == today && !d.StreakClaimed ? UiKit.Seal : got ? UiKit.Pine : new Color(.6f, .62f, .6f);
        _dayText[i].text = got ? $"Ngày {i + 1}\nĐã nhận" : $"Ngày {i + 1}\n+{Quests.StreakGifts[i]}";
      }
      _gift.interactable = Quests.CanClaimGift;
      _giftLabel.text = Quests.CanClaimGift ? $"Nhận +{Quests.TodayGift}" : "Mai quay lại nhé";

      for (int i = 0; i < Quests.PerDay; i++) {
        var s = i < d.Quests.Count ? d.Quests[i] : null;
        var q = s != null ? Quests.Def(s.Id) : null;
        if (q == null) { _qName[i].text = ""; continue; }
        _qName[i].text = Quests.Describe(q);
        _qName[i].color = s.Claimed ? UiKit.InkGrey : UiKit.InkBlack;
        UiKit.SetFill(_qFill[i], (float)s.Progress / q.Goal);
        _qCount[i].text = $"{s.Progress}/{q.Goal}";
        _qReward[i].text = $"+{q.Reward}";
        bool done = s.Progress >= q.Goal;
        _qBtnText[i].text = s.Claimed ? "Đã nhận" : done ? "Nhận" : "Đang làm";
        _qBtnText[i].color = s.Claimed ? UiKit.InkGrey : done ? UiKit.Seal : UiKit.InkGrey;
      }
      Tick();
    }

    void Tick() {
      var t = Quests.UntilReset;
      _reset.text = $"Nhiệm vụ mới sau {(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00} · tiến độ được cộng khi hết trận";
    }

    void Update() {
      _tick -= Time.unscaledDeltaTime;
      if (_tick <= 0) {
        _tick = 1;
        if (Profile.Data.QuestDay != Quests.Today) { Quests.Refresh(); Refresh(); } // midnight passed while open
        else Tick();
      }
      var k = UnityEngine.InputSystem.Keyboard.current;
      if (k != null && k.escapeKey.wasPressedThisFrame) Close();
    }
  }
}

