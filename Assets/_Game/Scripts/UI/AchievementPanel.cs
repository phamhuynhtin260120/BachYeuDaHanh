using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BachYeu {
  /// <summary>
  /// Thành tựu: a scrolling list of every achievement with its progress, and the danh hiệu row on top where the
  /// player picks which unlocked title to wear. Opened from the title menu.
  /// </summary>
  public class AchievementPanel : MonoBehaviour {
    public static AchievementPanel Current { get; private set; }

    const float RowH = 92f;
    Action _onClose;
    Text _count, _wearing;
    RectTransform _content;
    Button _prev, _next;
    readonly List<string> _titles = new();

    public static void Open(Action onClose = null) {
      if (Current) return;
      Achievements.Check();
      SettingsPanel.EnsureEventSystem();
      var canvas = UiKit.Canvas("Achievements", 88);
      var p = canvas.gameObject.AddComponent<AchievementPanel>();
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

    static readonly Vector2 TL = new(0, 1), TR = new(1, 1), C = new(.5f, .5f), BR = new(1, 0), BL = new(0, 0);

    void Build(RectTransform root) {
      var dim = UiKit.Rect(root, "Dim", C, C, Vector2.zero, Vector2.zero);
      UiKit.Stretch(dim);
      dim.gameObject.AddComponent<Image>().color = new Color(.15f, .15f, .14f, .45f);

      var card = UiKit.Rect(root, "Card", C, C, Vector2.zero, new Vector2(1240, 800));
      card.gameObject.AddComponent<Image>().color = UiKit.InkPaper;
      UiKit.InkImage(card, "Wash", "Wash", C, C, Vector2.zero, new Vector2(1420, 940), new Color(1, 1, 1, .9f));
      UiKit.LabelAt(card, "Thành tựu", 48, UiKit.InkBlack, TL, TL, new Vector2(48, -24), new Vector2(500, 64), TextAnchor.MiddleLeft, FontStyle.Bold);
      _count = UiKit.LabelAt(card, "", 30, UiKit.InkBlack, TR, TR, new Vector2(-48, -32), new Vector2(400, 50), TextAnchor.MiddleRight, FontStyle.Bold);

      // Danh hiệu: ‹ name › to cycle through the unlocked ones.
      UiKit.LabelAt(card, "Danh hiệu đang đeo", 20, UiKit.InkGrey, TL, TL, new Vector2(50, -96), new Vector2(220, 40), TextAnchor.MiddleLeft, FontStyle.Bold);
      _prev = UiKit.InkButton(card, "‹", TL, TL, new Vector2(270, -92), new Vector2(60, 48), false, () => Cycle(-1), 28);
      _wearing = UiKit.LabelAt(card, "", 24, Gfx.Hex("#8a6a2e"), TL, TL, new Vector2(336, -96), new Vector2(330, 40), TextAnchor.MiddleCenter, FontStyle.Bold);
      _next = UiKit.InkButton(card, "›", TL, TL, new Vector2(672, -92), new Vector2(60, 48), false, () => Cycle(1), 28);
      _prev.name = "PrevTitle"; _next.name = "NextTitle";
      UiKit.LabelAt(card, "Danh hiệu hiện cạnh tên bạn trong phòng chờ và màn kết quả.", 16, UiKit.InkGrey, TL, TL, new Vector2(750, -96), new Vector2(440, 40), TextAnchor.MiddleLeft);
      UiKit.InkImage(card, "Rule", "BrushBar", TL, TL, new Vector2(40, -150), new Vector2(1160, 8), UiKit.InkBlack);

      // Scrolling list.
      var view = UiKit.Rect(card, "View", TL, TL, new Vector2(40, -166), new Vector2(1160, 520));
      view.gameObject.AddComponent<RectMask2D>();
      var hit = view.gameObject.AddComponent<Image>(); hit.color = new Color(1, 1, 1, 0); // catches drags between rows
      _content = UiKit.Rect(view, "Content", TL, TL, Vector2.zero, new Vector2(1160, RowH * Achievements.All.Length));
      var scroll = view.gameObject.AddComponent<ScrollRect>();
      scroll.content = _content; scroll.viewport = view;
      scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
      scroll.scrollSensitivity = 40; scroll.inertia = true;

      UiKit.LabelAt(card, "Kéo hoặc lăn chuột để xem thêm", 16, UiKit.InkGrey, BL, BL, new Vector2(48, 44), new Vector2(500, 30), TextAnchor.MiddleLeft);
      UiKit.InkButton(card, "Xong", BR, BR, new Vector2(-44, 22), new Vector2(220, 74), false, Close, 26).name = "Done";
    }

    void Cycle(int dir) {
      if (_titles.Count == 0) return;
      // index -1 = "Không đeo"
      int i = _titles.IndexOf(Profile.Data.Title);
      int n = _titles.Count + 1;
      int next = ((i + 1 + dir) % n + n) % n - 1;
      Achievements.Wear(next < 0 ? "" : _titles[next]);
      Sfx.Play(SoundId.UiClick);
      Refresh();
    }

    void Refresh() {
      _titles.Clear();
      _titles.AddRange(Achievements.Titles());
      _count.text = $"Đã đạt {Achievements.DoneCount}/{Achievements.All.Length}";
      var t = Profile.Data.Title;
      _wearing.text = string.IsNullOrEmpty(t) ? (_titles.Count == 0 ? "Chưa có danh hiệu" : "Không đeo") : t;
      _prev.interactable = _next.interactable = _titles.Count > 0;

      foreach (Transform c in _content) Destroy(c.gameObject);
      // Unlocked first? No: keep a fixed order, so the list reads like a checklist. Done rows look stamped.
      for (int i = 0; i < Achievements.All.Length; i++) Row(i, Achievements.All[i]);
    }

    void Row(int i, AchievementDef a) {
      bool done = Achievements.Done(a);
      int prog = Achievements.Progress(a);
      var row = UiKit.Panel(_content, "Ach_" + a.Id, TL, TL, new Vector2(0, -i * RowH), new Vector2(1150, RowH - 8), done ? new Color(1, 1, 1, .85f) : new Color(1, 1, 1, .45f));
      row.raycastTarget = false;
      var seal = UiKit.SealTag(row.transform, "Seal", done ? "Đạt" : $"{Mathf.FloorToInt(100f * prog / a.Goal)}%", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(12, 0), new Vector2(66, 66), done ? UiKit.Seal : new Color(.6f, .62f, .6f), true, done ? 20 : 16);
      seal.raycastTarget = false;
      var name = UiKit.LabelAt(row.transform, a.Name, 24, done ? UiKit.InkBlack : UiKit.InkGrey, TL, TL, new Vector2(96, -10), new Vector2(520, 34), TextAnchor.MiddleLeft, FontStyle.Bold);
      var desc = UiKit.LabelAt(row.transform, a.Desc, 18, UiKit.InkGrey, TL, TL, new Vector2(96, -46), new Vector2(520, 28), TextAnchor.MiddleLeft);
      var bar = UiKit.InkBar(row.transform, TL, TL, new Vector2(630, -34), new Vector2(240, 16), done ? UiKit.Seal : UiKit.Pine);
      UiKit.SetFill(bar, (float)prog / a.Goal);
      var count = UiKit.LabelAt(row.transform, $"{Fmt(prog)}/{Fmt(a.Goal)}", 17, UiKit.InkGrey, TL, TL, new Vector2(630, -54), new Vector2(240, 26), TextAnchor.MiddleCenter);
      var reward = UiKit.LabelAt(row.transform, $"+{a.Reward}", 22, UiKit.Gold, TL, TL, new Vector2(886, -12), new Vector2(90, 34), TextAnchor.MiddleCenter, FontStyle.Bold);
      var title = UiKit.LabelAt(row.transform, a.Title != null ? "Danh hiệu\n" + a.Title : "", 16, Gfx.Hex("#8a6a2e"), TL, TL, new Vector2(980, -12), new Vector2(166, 60), TextAnchor.MiddleCenter, FontStyle.Bold);
      foreach (var tx in new[] { name, desc, count, reward, title }) tx.raycastTarget = false;
    }

    static string Fmt(int v) => v >= 1000 ? v.ToString("#,0").Replace(',', '.') : v.ToString();

    void Update() {
      var k = UnityEngine.InputSystem.Keyboard.current;
      if (k != null && k.escapeKey.wasPressedThisFrame) Close();
    }
  }
}
