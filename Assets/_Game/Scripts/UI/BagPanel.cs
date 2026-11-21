using System;
using UnityEngine;
using UnityEngine.UI;

namespace BachYeu {
  /// <summary>
  /// Túi pháp khí: spend Hồn đăng to buy and upgrade charms, and pick up to 3 to carry into matches.
  /// Built from code on its own overlay canvas; opened from the title menu.
  /// </summary>
  public class BagPanel : MonoBehaviour {
    public static BagPanel Current { get; private set; }

    Action _onClose;
    int _sel;
    Text _coins, _carryTitle, _name, _lvl, _now, _next, _msg, _buyLabel, _equipLabel;
    Button _buy, _equip;
    Image _bigSeal; Text _bigSealText;
    readonly Image[] _slotImg = new Image[Gear.Slots];
    readonly Text[] _slotText = new Text[Gear.Slots];
    readonly Image[] _cardBg = new Image[8];
    readonly Image[] _cardSeal = new Image[8];
    readonly Text[] _cardSub = new Text[8];
    readonly GameObject[] _cardMark = new GameObject[8];

    public static void Open(Action onClose = null) {
      if (Current) return;
      SettingsPanel.EnsureEventSystem();
      var canvas = UiKit.Canvas("Bag", 88);
      var p = canvas.gameObject.AddComponent<BagPanel>();
      p._onClose = onClose;
      p.Build((RectTransform)canvas.transform);
      Current = p;
      p.Select(0);
      Sfx.Play(SoundId.UiOpen);
    }

    void OnDestroy() { if (Current == this) Current = null; }

    public void Close() {
      Sfx.Play(SoundId.UiBack);
      var cb = _onClose;
      Destroy(gameObject);
      cb?.Invoke();
    }

    static readonly Vector2 TL = new(0, 1), C = new(.5f, .5f), TR = new(1, 1), BR = new(1, 0), BL = new(0, 0);

    void Build(RectTransform root) {
      var dim = UiKit.Rect(root, "Dim", C, C, Vector2.zero, Vector2.zero);
      UiKit.Stretch(dim);
      dim.gameObject.AddComponent<Image>().color = new Color(.15f, .15f, .14f, .45f);

      var card = UiKit.Rect(root, "Card", C, C, Vector2.zero, new Vector2(1240, 780));
      card.gameObject.AddComponent<Image>().color = UiKit.InkPaper;
      UiKit.InkImage(card, "Wash", "Wash", C, C, Vector2.zero, new Vector2(1420, 920), new Color(1, 1, 1, .9f));

      UiKit.LabelAt(card, "Túi pháp khí", 48, UiKit.InkBlack, TL, TL, new Vector2(48, -24), new Vector2(500, 64), TextAnchor.MiddleLeft, FontStyle.Bold);
      UiKit.LabelAt(card, "Mang tối đa 3 món vào trận. Hồn đăng kiếm được sau mỗi trận đấu.", 19, UiKit.InkGrey, TL, TL, new Vector2(50, -86), new Vector2(760, 28), TextAnchor.MiddleLeft);
      var coin = UiKit.SealTag(card, "CoinSeal", "Hồn", TR, TR, new Vector2(-300, -34), new Vector2(54, 54), UiKit.Gold, true, 17);
      _ = coin;
      _coins = UiKit.LabelAt(card, "0", 38, UiKit.InkBlack, TR, TR, new Vector2(-48, -28), new Vector2(240, 60), TextAnchor.MiddleRight, FontStyle.Bold);
      UiKit.InkImage(card, "Rule", "BrushBar", TL, TL, new Vector2(40, -122), new Vector2(1160, 8), UiKit.InkBlack);

      // Carried slots.
      _carryTitle = UiKit.LabelAt(card, "Đang mang", 22, UiKit.InkGrey, TL, TL, new Vector2(48, -140), new Vector2(300, 30), TextAnchor.MiddleLeft, FontStyle.Bold);
      for (int i = 0; i < Gear.Slots; i++) {
        var s = UiKit.InkImage(card, "Slot" + i, "SealLine", TL, TL, new Vector2(48 + i * 250, -176), new Vector2(236, 70), UiKit.InkGrey);
        _slotImg[i] = s;
        _slotText[i] = UiKit.Label(s.transform, "Ô trống", 22, UiKit.InkGrey, TextAnchor.MiddleCenter, FontStyle.Bold);
      }

      // Catalogue grid: 4 x 2.
      for (int i = 0; i < Gear.All.Length && i < 8; i++) {
        int k = i;
        var g = Gear.All[i];
        var pos = new Vector2(48 + (i % 4) * 186, -274 - (i / 4) * 206);
        var bg = UiKit.Panel(card, "Item_" + g.Id, TL, TL, pos, new Vector2(174, 194), new Color(1, 1, 1, .55f));
        _cardBg[i] = bg;
        var btn = bg.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => Select(k));
        _cardSeal[i] = UiKit.SealTag(bg.transform, "Seal", g.Short, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -14), new Vector2(92, 92), g.Color, true, 22);
        UiKit.LabelAt(bg.transform, g.Name, 19, UiKit.InkBlack, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -112), new Vector2(170, 30), TextAnchor.MiddleCenter, FontStyle.Bold);
        _cardSub[i] = UiKit.LabelAt(bg.transform, "", 17, UiKit.InkGrey, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -144), new Vector2(170, 28), TextAnchor.MiddleCenter);
        var mark = UiKit.SealTag(bg.transform, "Carried", "Mang", TR, TR, new Vector2(-4, -4), new Vector2(58, 30), UiKit.Seal, true, 14);
        _cardMark[i] = mark.gameObject;
      }

      // Detail column.
      var d = UiKit.Rect(card, "Detail", TR, TR, new Vector2(-44, -140), new Vector2(400, 560));
      UiKit.Panel(d, "Bg", TL, TL, Vector2.zero, new Vector2(400, 560), new Color(1, 1, 1, .5f));
      _bigSeal = UiKit.SealTag(d, "BigSeal", "", new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -20), new Vector2(120, 120), UiKit.Seal, true, 28);
      _bigSealText = _bigSeal.GetComponentInChildren<Text>();
      _name = UiKit.LabelAt(d, "", 32, UiKit.InkBlack, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -150), new Vector2(380, 44), TextAnchor.MiddleCenter, FontStyle.Bold);
      _lvl = UiKit.LabelAt(d, "", 20, UiKit.Gold, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -196), new Vector2(380, 28), TextAnchor.MiddleCenter, FontStyle.Bold);
      _now = UiKit.LabelAt(d, "", 22, UiKit.InkBlack, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -238), new Vector2(360, 64), TextAnchor.UpperCenter);
      _next = UiKit.LabelAt(d, "", 19, UiKit.InkGrey, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -306), new Vector2(360, 56), TextAnchor.UpperCenter);
      _buy = UiKit.InkButton(d, "Mua", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 116), new Vector2(340, 86), true, Buy, 26);
      _buy.name = "Buy";
      _buyLabel = _buy.GetComponentInChildren<Text>();
      _equip = UiKit.InkButton(d, "Mang theo", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 40), new Vector2(300, 64), false, Equip, 22);
      _equip.name = "Equip";
      _equipLabel = _equip.GetComponentInChildren<Text>();
      _msg = UiKit.LabelAt(d, "", 18, UiKit.Seal, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 8), new Vector2(380, 28), TextAnchor.MiddleCenter, FontStyle.Bold);

      _stats = UiKit.LabelAt(card, "", 18, UiKit.InkGrey, BL, BL, new Vector2(48, 30), new Vector2(860, 28), TextAnchor.MiddleLeft);
      UiKit.InkButton(card, "Xong", BR, BR, new Vector2(-44, 24), new Vector2(220, 74), false, Close, 26).name = "Done";
    }

    Text _stats;

    void Select(int i) {
      _sel = Mathf.Clamp(i, 0, Gear.All.Length - 1);
      _msg.text = "";
      Refresh();
    }

    void Buy() {
      var g = Gear.All[_sel];
      int lv = Profile.Level(g.Id), cost = Profile.NextCost(g);
      if (cost <= 0) return;
      if (!Profile.BuyOrUpgrade(g)) { _msg.text = $"Còn thiếu {cost - Profile.Coins} Hồn đăng"; Sfx.Play(SoundId.UiBack); Refresh(); return; }
      Sfx.Play(lv == 0 ? SoundId.Pickup : SoundId.LanternLit);
      Refresh();
      _msg.text = lv == 0 ? (Profile.IsEquipped(g.Id) ? "Đã mua và mang theo" : "Đã mua") : $"Đã lên cấp {lv + 1}";
    }

    void Equip() {
      var g = Gear.All[_sel];
      if (Profile.Level(g.Id) <= 0) return;
      bool was = Profile.IsEquipped(g.Id);
      if (!Profile.ToggleEquip(g.Id)) { _msg.text = "Đã mang đủ 3 món. Bỏ bớt một món trước."; Refresh(); return; }
      Sfx.Play(was ? SoundId.UiBack : SoundId.Swap);
      Refresh();
      _msg.text = was ? "Đã bỏ ra" : "Đã mang theo";
    }

    void Refresh() {
      var prof = Profile.Data;
      _coins.text = prof.Coins.ToString();
      _carryTitle.text = $"Đang mang {prof.Equipped.Count}/{Gear.Slots}";
      for (int i = 0; i < Gear.Slots; i++) {
        var g = i < prof.Equipped.Count ? Gear.Find(prof.Equipped[i]) : null;
        _slotImg[i].sprite = UiKit.InkSprite(g != null ? "Seal" : "SealLine");
        _slotImg[i].color = g != null ? g.Color : UiKit.InkGrey;
        _slotText[i].text = g != null ? $"{g.Name} · {Profile.Level(g.Id)}" : "Ô trống";
        _slotText[i].color = g != null ? UiKit.InkPaper : UiKit.InkGrey;
        _slotText[i].fontSize = g != null ? 19 : 22;
      }
      for (int i = 0; i < _cardBg.Length; i++) {
        if (!_cardBg[i]) continue;
        var g = Gear.All[i];
        int lv = Profile.Level(g.Id);
        _cardBg[i].color = i == _sel ? new Color(1, 1, 1, .95f) : new Color(1, 1, 1, .45f);
        var o = _cardBg[i].GetComponent<Outline>();
        if (!o) o = _cardBg[i].gameObject.AddComponent<Outline>();
        o.effectColor = i == _sel ? UiKit.InkBlack : UiKit.Clear;
        o.effectDistance = new Vector2(3, -3);
        _cardSeal[i].color = lv > 0 ? g.Color : new Color(g.Color.r, g.Color.g, g.Color.b, .35f);
        _cardSub[i].text = lv > 0 ? $"Cấp {lv}/{Gear.MaxLevel}" : $"{g.Price} Hồn đăng";
        _cardSub[i].color = lv > 0 ? UiKit.Gold : UiKit.InkGrey;
        _cardMark[i].SetActive(Profile.IsEquipped(g.Id));
      }

      var s = Gear.All[_sel];
      int L = Profile.Level(s.Id), cost = Profile.NextCost(s);
      _bigSeal.color = s.Color;
      _bigSealText.text = s.Short;
      _name.text = s.Name;
      _lvl.text = L > 0 ? $"Cấp {L}/{Gear.MaxLevel}" : "Chưa có";
      _now.text = L > 0 ? s.Describe(s.Values[L - 1]) : s.Describe(s.Values[0]);
      _next.text = L <= 0 ? $"Nâng tối đa: {s.Describe(s.Values[Gear.MaxLevel - 1])}" : L < Gear.MaxLevel ? $"Cấp {L + 1}: {s.Describe(s.Values[L])}" : "Đã đạt cấp cao nhất";
      _buyLabel.text = L <= 0 ? $"Mua · {cost}" : L < Gear.MaxLevel ? $"Nâng cấp · {cost}" : "Tối đa";
      _buy.interactable = cost > 0;
      _buyLabel.color = cost > 0 && Profile.Coins < cost ? new Color(.95f, .8f, .75f) : UiKit.InkPaper;
      _equip.gameObject.SetActive(L > 0);
      _equipLabel.text = Profile.IsEquipped(s.Id) ? "Bỏ ra" : "Mang theo";
      if (_stats) _stats.text = $"Đã chơi {prof.Games} trận · Thắng {prof.Wins} · Diệt {prof.Kills} yêu quái · Tổng Hồn đăng kiếm được {prof.Earned}";
    }

    void Update() {
      var k = UnityEngine.InputSystem.Keyboard.current;
      if (k != null && k.escapeKey.wasPressedThisFrame) Close();
    }
  }
}
