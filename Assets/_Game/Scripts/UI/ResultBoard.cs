using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BachYeu {
  /// <summary>
  /// The per-player table on the result screen: kills, damage, boss damage, rescues, falls (or lantern time in
  /// Tranh Đăng), and one title per player so everyone has something to be proud of. Rows are sorted by damage;
  /// your own row is highlighted.
  /// </summary>
  public static class ResultBoard {
    const float RowH = 44f;

    class Col { public string Head; public float W; public Func<PlayerAvatar, string> Val; }

    static string TitleTag(PlayerAvatar p) { var t = p.Title.ToString(); return string.IsNullOrEmpty(t) ? "" : $"\n<size=13><color=#8a6a2e>{t}</color></size>"; }

    static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    static string Clock(float secs) { int s = Mathf.RoundToInt(Mathf.Max(0, secs)); return $"{s / 60}:{s % 60:00}"; }

    /// <summary>Builds (or rebuilds) the table under <paramref name="parent"/>, its top edge at <paramref name="top"/> from the parent's top.</summary>
    public static void Build(Transform parent, GameDirector dir, float top) {
      var old = parent.Find("ResultBoard");
      if (old) UnityEngine.Object.Destroy(old.gameObject);
      var players = PlayerAvatar.All.Where(p => p && p.Object && p.Object.IsValid).OrderByDescending(p => p.DamageDealt).ToList();
      if (players.Count == 0) return;
      bool contest = dir.IsContest;
      var titles = Titles(dir, players);

      var cols = new List<Col> {
        new() { Head = "Người chơi", W = 230, Val = p => contest ? $"<color={Hex(p.TeamColor)}>{p.DisplayName}</color>{TitleTag(p)}" : p.DisplayName + TitleTag(p) },
      };
      if (!contest) cols.Add(new Col { Head = "Mặt nạ", W = 120, Val = p => Masks.All[Mathf.Clamp(p.MaskId, 0, Masks.All.Length - 1)].Name });
      cols.Add(new Col { Head = "Hạ", W = 70, Val = p => p.KillCount.ToString() });
      cols.Add(new Col { Head = "Sát thương", W = 120, Val = p => p.DamageDealt.ToString() });
      cols.Add(new Col { Head = "Đánh boss", W = 110, Val = p => p.BossDamage.ToString() });
      if (contest) cols.Add(new Col { Head = "Giữ đèn", W = 100, Val = p => Clock(p.HoldTime) });
      cols.Add(new Col { Head = "Cứu", W = 70, Val = p => p.Revives.ToString() });
      if (!contest) cols.Add(new Col { Head = "Gục", W = 70, Val = p => p.Downs.ToString() });
      cols.Add(new Col { Head = "Danh hiệu", W = 330, Val = p => titles.TryGetValue(p, out var t) ? t : "Đồng hành" });

      float width = cols.Sum(c => c.W);
      var TC = new Vector2(.5f, 1);
      var board = UiKit.Rect(parent, "ResultBoard", TC, TC, new Vector2(0, -top), new Vector2(width + 40, RowH * (players.Count + 1) + 16));
      var bg = board.gameObject.AddComponent<Image>();
      bg.color = new Color(.98f, .97f, .94f, .72f);
      bg.raycastTarget = false;

      void Row(int r, Func<Col, string> text, bool head, bool me) {
        float y = -8 - r * RowH;
        if (me) {
          var hl = UiKit.Panel(board, "Me", new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, y), new Vector2(width + 24, RowH - 4), new Color(UiKit.Seal.r, UiKit.Seal.g, UiKit.Seal.b, .12f));
          hl.raycastTarget = false;
        }
        float x = 20;
        foreach (var c in cols) {
          bool first = c == cols[0], last = c == cols[^1];
          var t = UiKit.LabelAt(board, text(c), head ? 18 : 21, head ? UiKit.InkGrey : UiKit.InkBlack, new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(x, y), new Vector2(c.W - 8, RowH - 4), first || last ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, head || first ? FontStyle.Bold : FontStyle.Normal);
          t.raycastTarget = false;
          t.supportRichText = true;
          t.horizontalOverflow = HorizontalWrapMode.Wrap;
          t.resizeTextForBestFit = true; t.resizeTextMinSize = 14; t.resizeTextMaxSize = head ? 18 : 21;
          if (last && !head) t.color = UiKit.Seal;
          x += c.W;
        }
      }

      Row(0, c => c.Head, true, false);
      for (int i = 0; i < players.Count; i++) {
        var p = players[i];
        Row(i + 1, c => c.Val(p), false, p == PlayerAvatar.Local);
      }
    }

    /// <summary>One title per player, best feats first; each feat goes to one player only.</summary>
    static Dictionary<PlayerAvatar, string> Titles(GameDirector dir, List<PlayerAvatar> players) {
      var given = new Dictionary<PlayerAvatar, string>();
      if (dir.IsContest && dir.BossFinisher != Fusion.PlayerRef.None) {
        var fin = players.FirstOrDefault(p => p.Owner == dir.BossFinisher);
        if (fin) given[fin] = "Kết liễu · đòn cuối boss";
      }
      // A "best at" title only goes to the real leader of that stat, and only if they have no title yet.
      void Award(Func<PlayerAvatar, float> score, string title, float min = 1) {
        var best = players.Where(p => score(p) >= min).OrderByDescending(score).FirstOrDefault();
        if (best && !given.ContainsKey(best)) given[best] = title;
      }
      if (dir.IsContest) {
        Award(p => p.HoldTime, "Thủ đèn · giữ đèn lâu nhất");
        Award(p => p.Pushes, "Cuồng phong · đẩy địch nhiều nhất");
      }
      Award(p => p.BossDamage, "Phá trận · đánh boss mạnh nhất");
      Award(p => p.Revives, "Cứu tinh · kéo đồng đội dậy nhiều nhất");
      Award(p => p.KillCount, "Diệt yêu · hạ nhiều yêu quái nhất");
      Award(p => p.DamageDealt, "Mũi nhọn · sát thương cao nhất");
      // Everyone else: a title about their own numbers, never a false "nhất".
      foreach (var p in players) {
        if (given.ContainsKey(p)) continue;
        given[p] = p.Revives > 0 ? $"Đồng đội tốt · cứu {p.Revives} lần"
          : !dir.IsContest && p.Downs == 0 && p.KillCount > 0 ? "Bất khuất · không gục lần nào"
          : p.Downs > 0 ? $"Lì đòn · gục {p.Downs} lần vẫn đứng dậy"
          : p.KillCount > 0 ? $"Chiến hữu · hạ {p.KillCount} yêu quái"
          : "Đồng hành";
      }
      return given;
    }
  }
}
