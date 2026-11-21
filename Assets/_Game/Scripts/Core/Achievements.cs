using System;
using System.Collections.Generic;
using UnityEngine;

namespace BachYeu {
  public class AchievementDef {
    public string Id, Name, Desc;
    public int Goal = 1, Reward;
    public string Title;                        // danh hiệu it unlocks (optional)
    public Func<Profile.SaveData, int> Value;   // current progress, from lifetime counters
  }

  /// <summary>
  /// Thành tựu: long-term goals read from the lifetime counters in the Profile. Unlocking pays Hồn đăng at once
  /// and may unlock a danh hiệu the player can wear (shown beside the name in the room and on the result board).
  /// Check() is cheap and safe to call after anything that moves a counter.
  /// </summary>
  public static class Achievements {
    static int Owned(Profile.SaveData d) => d.Items.Count;
    static int Maxed(Profile.SaveData d) { int n = 0; foreach (var o in d.Items) if (o.Level >= Gear.MaxLevel) n++; return n; }
    static int Met(Profile.SaveData d) { int n = 0; foreach (var v in d.YokaiSeen) if (v > 0) n++; return n; }

    public static readonly AchievementDef[] All = {
      new() { Id = "first_game", Name = "Lần đầu ra phố", Desc = "Chơi trận đầu tiên", Reward = 20, Value = d => d.Games },
      new() { Id = "kills_100", Name = "Thợ săn yêu", Desc = "Hạ 100 yêu quái", Goal = 100, Reward = 60, Title = "Thợ săn yêu", Value = d => d.Kills },
      new() { Id = "kills_1000", Name = "Diệt yêu sư", Desc = "Hạ 1000 yêu quái", Goal = 1000, Reward = 250, Title = "Diệt yêu sư", Value = d => d.Kills },
      new() { Id = "dawn_1", Name = "Bình minh đầu tiên", Desc = "Thấy bình minh 1 lần", Reward = 50, Value = d => d.Dawns },
      new() { Id = "dawn_20", Name = "Người giữ đêm", Desc = "Thấy bình minh 20 lần", Goal = 20, Reward = 200, Title = "Người giữ đêm", Value = d => d.Dawns },
      new() { Id = "no_down", Name = "Bất khuất", Desc = "Thấy bình minh mà không gục lần nào", Reward = 120, Title = "Bất khuất", Value = d => d.CleanDawns },
      new() { Id = "story_1", Name = "Hết phần một", Desc = "Qua cả 6 chương Đêm Hồng Đăng", Goal = 6, Reward = 300, Title = "Người mở cổng", Value = d => Mathf.Min(d.StoryCleared, 6) },
      new() { Id = "story_2", Name = "Hết phần hai", Desc = "Qua cả 6 chương phần hai", Goal = 12, Reward = 400, Title = "Người tiễn đèn", Value = d => Mathf.Min(d.StoryCleared, 12) },
      new() { Id = "boss_10", Name = "Phá trận", Desc = "Đánh bại 10 boss", Goal = 10, Reward = 150, Title = "Phá trận", Value = d => d.BossKills },
      new() { Id = "revive_25", Name = "Cứu tinh", Desc = "Kéo đồng đội dậy 25 lần", Goal = 25, Reward = 150, Title = "Cứu tinh", Value = d => d.Revives },
      new() { Id = "damage_100k", Name = "Mũi nhọn", Desc = "Gây tổng 100.000 sát thương", Goal = 100000, Reward = 150, Title = "Mũi nhọn", Value = d => d.Damage },
      new() { Id = "contest_1", Name = "Ra mắt Tranh Đăng", Desc = "Thắng 1 trận Tranh Đăng", Reward = 50, Value = d => d.ContestWins },
      new() { Id = "contest_10", Name = "Vua đèn", Desc = "Thắng 10 trận Tranh Đăng", Goal = 10, Reward = 200, Title = "Vua đèn", Value = d => d.ContestWins },
      new() { Id = "full_light", Name = "Toàn Đăng", Desc = "Thắng Tranh Đăng nhờ giữ đủ 3 đèn", Reward = 150, Title = "Toàn Đăng", Value = d => d.FullLightWins },
      new() { Id = "hold_600", Name = "Thủ đèn", Desc = "Đứng giữ đèn tổng 10 phút", Goal = 600, Reward = 100, Value = d => d.HoldSeconds },
      new() { Id = "book_full", Name = "Người chép sổ", Desc = "Ghi đủ mọi loài vào Bách yêu lục", Goal = Bestiary.All.Length, Reward = 150, Title = "Người chép sổ", Value = Met },
      new() { Id = "gear_1", Name = "Pháp khí đầu tiên", Desc = "Mua 1 pháp khí", Reward = 30, Value = Owned },
      new() { Id = "gear_max", Name = "Luyện khí", Desc = "Nâng 1 pháp khí lên cấp tối đa", Reward = 100, Value = Maxed },
      new() { Id = "quests_20", Name = "Siêng năng", Desc = "Nhận thưởng 20 nhiệm vụ hằng ngày", Goal = 20, Reward = 150, Title = "Siêng năng", Value = d => d.QuestsDone },
      new() { Id = "streak_7", Name = "Bảy ngày liền", Desc = "Vào game 7 ngày liên tiếp", Goal = 7, Reward = 100, Value = d => d.MaxStreak },
      new() { Id = "masks_4", Name = "Đủ bốn mặt", Desc = "Mang mỗi mặt nạ vào trận ít nhất một lần", Goal = Masks.All.Length, Reward = 80, Value = d => d.MasksUsed.Count },
      new() { Id = "party_4", Name = "Đông đủ", Desc = "Chơi một trận đủ 4 người", Goal = 4, Reward = 80, Title = "Bạn bè đông đủ", Value = d => d.MaxParty },
      new() { Id = "earn_5000", Name = "Phú hộ phố đèn", Desc = "Kiếm tổng 5.000 Hồn đăng", Goal = 5000, Reward = 150, Title = "Phú hộ", Value = d => d.Earned },
    };

    public static AchievementDef Find(string id) { foreach (var a in All) if (a.Id == id) return a; return null; }
    public static bool Done(AchievementDef a) => Profile.Data.AchDone.Contains(a.Id);
    public static int Progress(AchievementDef a) => Mathf.Clamp(a.Value(Profile.Data), 0, a.Goal);
    public static int DoneCount => Profile.Data.AchDone.Count;

    /// <summary>Titles you can wear: those of unlocked achievements.</summary>
    public static IEnumerable<string> Titles() { foreach (var a in All) if (a.Title != null && Done(a)) yield return a.Title; }

    /// <summary>Unlocked since the result screen last read it.</summary>
    public static readonly List<string> JustUnlocked = new();
    public static void ResetSession() => JustUnlocked.Clear();

    /// <summary>Unlocks everything whose goal is met, pays the rewards, and tells the player. Saves when anything changed.</summary>
    public static void Check() {
      var d = Profile.Data;
      bool any = false;
      foreach (var a in All) {
        if (d.AchDone.Contains(a.Id) || a.Value(d) < a.Goal) continue;
        d.AchDone.Add(a.Id);
        d.Coins += a.Reward;
        d.Earned += a.Reward;
        any = true;
        JustUnlocked.Add(a.Name);
        if (Hud.I) Hud.I.Toast($"Thành tựu: {a.Name} · +{a.Reward} Hồn đăng", UiKit.Gold);
      }
      if (any) { Profile.Save(); Check(); } // Earned grew: "Phú hộ" may follow
    }

    /// <summary>Wear a danh hiệu (null or empty: none).</summary>
    public static void Wear(string title) {
      Profile.Data.Title = title ?? "";
      Profile.Save();
      if (PlayerAvatar.Local) PlayerAvatar.Local.SetTitle(Profile.Data.Title);
    }
  }
}
