using System;
using UnityEngine;
using UnityEngine.UI;

namespace BachYeu {
  /// <summary>
  /// Hành trình map: two pages (Phần một, Phần hai), six chapters each as lanterns along the river; page one ends in
  /// a gateway lantern to page two, page two ends in Đêm tự do.
  /// Picking one sets the night to play; "Lên đường" starts alone, "Chơi cùng bạn" goes back to the menu to open a room.
  /// </summary>
  public class JourneyMap : MonoBehaviour {
    public static JourneyMap Current { get; private set; }

    /// <summary>(night, startAloneNow)</summary>
    Action<int, bool> _choose;
    int _sel;
    readonly Image[] _node = new Image[7];
    readonly Text[] _nodeText = new Text[7];
    readonly Image[] _ring = new Image[7];
    readonly Text[] _cap = new Text[7];
    readonly Image[] _seg = new Image[6];
    Text _title, _meta, _teaser, _reward, _twist, _part, _progress;
    Button _go, _friends;
    Button[] _tabs;
    int _page;
    const int Slots = 7;

    public static int FreeNight => Story.Chapters.Length + 1;
    public static bool Unlocked(int night) => night <= Mathf.Min(Profile.Data.StoryCleared + 1, FreeNight);

    public static void Open(Action<int, bool> choose) {
      if (Current) return;
      SettingsPanel.EnsureEventSystem();
      var canvas = UiKit.Canvas("Journey", 87);
      var m = canvas.gameObject.AddComponent<JourneyMap>();
      m._choose = choose;
      m.Build((RectTransform)canvas.transform);
      Current = m;
      int start = Mathf.Clamp(LocalProfile.Chapter, 1, FreeNight);
      if (!Unlocked(start)) start = Mathf.Min(Profile.Data.StoryCleared + 1, FreeNight);
      m.ShowPage(start >= FreeNight ? 1 : Story.PartOf(start));
      m.Select(start);
      Sfx.Play(SoundId.UiOpen);
    }

    void OnDestroy() { if (Current == this) Current = null; }

    void Close() { Sfx.Play(SoundId.UiBack); Destroy(gameObject); }

    void Build(RectTransform root) {
      var C = new Vector2(.5f, .5f); var TL = new Vector2(0, 1); var BL = Vector2.zero; var BR = new Vector2(1, 0); var TR = new Vector2(1, 1);
      var bd = UiKit.Backdrop(root, "Backdrop", "ResultBg");
      bd.color = UiKit.InkPaper;

      UiKit.LabelAt(root, "Hành trình", 56, UiKit.InkBlack, TL, TL, new Vector2(330, -40), new Vector2(420, 72), TextAnchor.MiddleLeft, FontStyle.Bold);
      _part = UiKit.LabelAt(root, "", 24, UiKit.InkGrey, TL, TL, new Vector2(334, -112), new Vector2(700, 32), TextAnchor.MiddleLeft, FontStyle.Bold);
      _tabs = new Button[Story.PartName.Length];
      for (int p = 0; p < _tabs.Length; p++) {
        int k = p;
        _tabs[p] = UiKit.InkButton(root, p == 0 ? "Phần một" : "Phần hai", new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-120 + p * 240, -44), new Vector2(220, 60), false, () => { Sfx.Play(SoundId.UiClick); TryPage(k); }, 22);
        _tabs[p].name = "Part" + p;
      }
      UiKit.InkButton(root, "Đóng", TR, TR, new Vector2(-32, -28), new Vector2(170, 64), false, Close, 24).name = "Close";
      int cleared = Profile.Data.StoryCleared;
      _progress = UiKit.LabelAt(root, $"Đã qua {Mathf.Min(cleared, Story.Chapters.Length)}/{Story.Chapters.Length} chương", 22, UiKit.Seal, TR, TR, new Vector2(-230, -40), new Vector2(300, 40), TextAnchor.MiddleRight, FontStyle.Bold);

      // River path: brush strokes between lantern nodes.
      var path = UiKit.Rect(root, "Path", C, C, new Vector2(0, 90), new Vector2(1400, 260));
      Vector2 P(int i) => new(-600 + i * 200, (i % 2 == 0 ? -40 : 50));
      for (int i = 0; i < Slots - 1; i++) {
        Vector2 a = P(i), b = P(i + 1), mid = (a + b) / 2, d = b - a;
        _seg[i] = UiKit.InkImage(path, "River" + i, "BrushBar", C, C, mid, new Vector2(d.magnitude, 14), UiKit.Pine);
        _seg[i].rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
      }
      for (int i = 0; i < Slots; i++) {
        int k = i;
        _ring[i] = UiKit.InkImage(path, "Ring" + i, "Enso", C, C, P(i), new Vector2(128, 128), UiKit.Gold);
        var node = UiKit.InkImage(path, "Node" + i, "InkDot", C, C, P(i), new Vector2(96, 96), UiKit.InkGrey);
        node.raycastTarget = true;
        var btn = node.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => Tap(k));
        _node[i] = node;
        _nodeText[i] = UiKit.Label(node.transform, "", 36, UiKit.InkPaper, TextAnchor.MiddleCenter, FontStyle.Bold);
        _cap[i] = UiKit.LabelAt(path, "", 18, UiKit.InkGrey, C, C, P(i) + new Vector2(0, i % 2 == 0 ? -82 : 82), new Vector2(190, 26), TextAnchor.MiddleCenter, FontStyle.Bold);
      }

      // Chapter card.
      var card = UiKit.Rect(root, "Chapter", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 40), new Vector2(1320, 270));
      card.gameObject.AddComponent<Image>().color = new Color(.97f, .95f, .9f, .92f);
      UiKit.InkImage(card, "Rule", "BrushBar", TL, TL, new Vector2(30, -6), new Vector2(1260, 8), UiKit.InkBlack);
      _title = UiKit.LabelAt(card, "", 38, UiKit.InkBlack, TL, TL, new Vector2(36, -24), new Vector2(820, 52), TextAnchor.MiddleLeft, FontStyle.Bold);
      _meta = UiKit.LabelAt(card, "", 20, UiKit.InkGrey, TL, TL, new Vector2(38, -78), new Vector2(820, 28), TextAnchor.MiddleLeft, FontStyle.Bold);
      _teaser = UiKit.LabelAt(card, "", 24, UiKit.InkBlack, TL, TL, new Vector2(38, -116), new Vector2(820, 80), TextAnchor.UpperLeft, FontStyle.Italic);
      _twist = UiKit.LabelAt(card, "", 20, UiKit.Pine, BL, BL, new Vector2(38, 56), new Vector2(820, 30), TextAnchor.MiddleLeft, FontStyle.Bold);
      _reward = UiKit.LabelAt(card, "", 20, UiKit.Seal, BL, BL, new Vector2(38, 22), new Vector2(820, 30), TextAnchor.MiddleLeft, FontStyle.Bold);
      _go = UiKit.InkButton(card, "Lên đường", BR, BR, new Vector2(-30, 112), new Vector2(380, 104), true, () => Choose(true), 30);
      _go.name = "Go";
      _friends = UiKit.InkButton(card, "Chơi cùng bạn", BR, BR, new Vector2(-50, 28), new Vector2(340, 70), false, () => Choose(false), 22);
      _friends.name = "Friends";
    }

    // Page 0: chapters 1..6 and a gateway to page 1. Page 1: chapters 7..12 and Đêm tự do.
    int NightAt(int slot) => slot < Slots - 1 ? _page * Story.PerPart + slot + 1 : _page == 0 ? -1 : FreeNight;

    bool PageOpen(int page) => page == 0 || Profile.Data.StoryCleared >= page * Story.PerPart;

    void TryPage(int page) {
      if (!PageOpen(page)) { _part.text = $"Qua hết {Story.PartName[page - 1]} để mở {Story.PartName[page]}"; _part.color = UiKit.Seal; return; }
      ShowPage(page);
      int first = page * Story.PerPart + 1;
      Select(Unlocked(_sel) && Story.PartOf(_sel) == page && _sel < FreeNight ? _sel : Mathf.Min(Mathf.Max(first, Mathf.Min(Profile.Data.StoryCleared + 1, FreeNight)), page == 0 ? Story.PerPart : FreeNight));
    }

    void ShowPage(int page) {
      _page = page;
      _part.text = Story.PartName[page];
      _part.color = UiKit.InkGrey;
      for (int p = 0; p < _tabs.Length; p++) {
        UiKit.SelectSegment(_tabs, page);
        _tabs[p].GetComponentInChildren<Text>().color = PageOpen(p) ? (p == page ? UiKit.InkPaper : UiKit.InkBlack) : new Color(.55f, .57f, .56f);
      }
      for (int i = 0; i < Slots; i++) {
        int n = NightAt(i);
        _nodeText[i].text = n < 0 ? "II" : n == FreeNight ? "∞" : n.ToString();
        _nodeText[i].fontSize = n < 0 || n == FreeNight ? 40 : 36;
        _cap[i].text = n < 0 ? "Phần hai" : n == FreeNight ? "Đêm tự do" : Story.Get(n).Place;
      }
      for (int i = 0; i < Slots - 1; i++) {
        int n = NightAt(i + 1);
        bool open = n < 0 ? PageOpen(1) : Unlocked(n);
        _seg[i].color = new Color(.24f, .42f, .38f, open ? .8f : .25f);
      }
      Paint();
    }

    void Tap(int slot) {
      int n = NightAt(slot);
      if (n < 0) { Sfx.Play(SoundId.UiClick); TryPage(1); return; }
      Select(n);
    }

    void Paint() {
      int cleared = Profile.Data.StoryCleared;
      for (int i = 0; i < Slots; i++) {
        int n = NightAt(i);
        bool open = n < 0 ? PageOpen(1) : Unlocked(n), done = n > 0 && n <= cleared && n < FreeNight, sel = n == _sel;
        _node[i].color = done ? UiKit.Seal : open ? UiKit.InkBlack : new Color(.6f, .62f, .6f, .6f);
        _nodeText[i].color = open ? UiKit.InkPaper : new Color(1, 1, 1, .6f);
        _ring[i].color = sel ? UiKit.Gold : UiKit.Clear;
        _node[i].rectTransform.localScale = Vector3.one * (sel ? 1.12f : 1f);
      }
    }

    void Select(int night) {
      if (!Unlocked(night)) { Sfx.Play(SoundId.UiBack); return; }
      _sel = night;
      int cleared = Profile.Data.StoryCleared;
      Paint();
      var ch = Story.Get(night);
      if (ch != null) {
        _title.text = $"Chương {night} · {ch.Title}";
        _meta.text = $"{ch.Place} · Mùa {ArenaBuilder.Seasons[ch.Season].Name} · Yêu quái mạnh hơn {Mathf.RoundToInt((Story.Difficulty(night) - 1) * 100)}%";
        _teaser.text = ch.Teaser;
        _twist.text = ch.Twist != Twist.None ? "Luật đêm: " + Story.TwistText(ch.Twist, true) : "";
        _reward.text = night <= cleared ? "Đã hoàn thành · chơi lại để kiếm Hồn đăng" : $"Lần đầu hoàn thành: +{Story.FirstClearReward(night)} Hồn đăng";
      } else {
        _title.text = "Đêm tự do";
        _meta.text = "Không có hội thoại · mỗi đêm thắng, yêu quái mạnh thêm";
        _teaser.text = "Phố Hồng Đăng vẫn mở hội. Cứ thắp đèn đến khi trời sáng, để rèn tay và kiếm Hồn đăng.";
        _twist.text = "";
        _reward.text = "";
      }
    }

    void Choose(bool alone) {
      LocalProfile.SetChapter(_sel);
      var cb = _choose;
      Destroy(gameObject);
      cb?.Invoke(_sel, alone);
    }

    void Update() {
      var k = UnityEngine.InputSystem.Keyboard.current;
      if (k != null && k.escapeKey.wasPressedThisFrame) Close();
      // Soft pulse on the selected lantern.
      for (int i = 0; i < Slots; i++) if (NightAt(i) == _sel) _ring[i].rectTransform.localRotation = Quaternion.Euler(0, 0, -Time.unscaledTime * 20f);
    }
  }
}
