using System;
using System.Collections.Generic;
using UnityEngine;

namespace BachYeu {
  public class QuestDef {
    public string Id, Text;   // Text uses {n}
    public int Goal, Reward;
    public bool NeedsFriends; // only one of these per day, so solo players can still finish the others
  }

  /// <summary>
  /// Nhiệm vụ hằng ngày: three small tasks a day (same for everyone on that date), counted at the end of each match,
  /// plus a 7-day login gift that grows with the streak. Everything resets at local midnight. Saved in the Profile.
  /// </summary>
  public static class Quests {
    public static readonly QuestDef[] Pool = {
      new() { Id = "kills", Text = "Hạ {n} yêu quái", Goal = 40, Reward = 50 },
      new() { Id = "games", Text = "Chơi {n} trận bất kỳ", Goal = 3, Reward = 40 },
      new() { Id = "dawn", Text = "Thấy bình minh {n} lần", Goal = 1, Reward = 70 },
      new() { Id = "watches", Text = "Qua {n} canh giờ", Goal = 6, Reward = 50 },
      new() { Id = "boss", Text = "Đánh bại {n} boss", Goal = 2, Reward = 60 },
      new() { Id = "contest", Text = "Thắng {n} trận Tranh Đăng", Goal = 1, Reward = 70, NeedsFriends = true },
      new() { Id = "friends", Text = "Chơi {n} trận cùng bạn", Goal = 2, Reward = 60, NeedsFriends = true },
    };

    public static readonly int[] StreakGifts = { 30, 40, 50, 60, 70, 80, 150 };
    public const int PerDay = 3;

    [Serializable] public class State { public string Id; public int Progress; public bool Claimed; }

    public static QuestDef Def(string id) { foreach (var q in Pool) if (q.Id == id) return q; return null; }
    public static string Describe(QuestDef q) => q.Text.Replace("{n}", q.Goal.ToString());

    static string DayKey(DateTime d) => d.ToString("yyyyMMdd");
    public static string Today => DayKey(DateTime.Now);

    /// <summary>Time left until the quests change (local midnight).</summary>
    public static TimeSpan UntilReset => DateTime.Today.AddDays(1) - DateTime.Now;

    /// <summary>Roll today's quests and move the login streak forward if the date changed. Cheap: call any time.</summary>
    public static void Refresh() {
      var d = Profile.Data;
      string today = Today;
      if (d.LastLoginDay != today) {
        d.Streak = d.LastLoginDay == DayKey(DateTime.Today.AddDays(-1)) ? d.Streak + 1 : 1;
        d.MaxStreak = Mathf.Max(d.MaxStreak, d.Streak);
        d.LastLoginDay = today;
        d.StreakClaimed = false;
      }
      if (d.QuestDay != today || d.Quests.Count != PerDay) {
        d.QuestDay = today;
        d.Quests.Clear();
        foreach (var q in Roll(today)) d.Quests.Add(new State { Id = q.Id });
      }
      Profile.Save();
      Achievements.Check();
    }

    static List<QuestDef> Roll(string day) {
      var rng = new System.Random(int.Parse(day));
      var pool = new List<QuestDef>(Pool);
      var pick = new List<QuestDef>();
      bool friends = false;
      while (pick.Count < PerDay && pool.Count > 0) {
        var q = pool[rng.Next(pool.Count)];
        pool.Remove(q);
        if (q.NeedsFriends && friends) continue;
        friends |= q.NeedsFriends;
        pick.Add(q);
      }
      return pick;
    }

    public static int TodayGift => StreakGifts[(Mathf.Max(1, Profile.Data.Streak) - 1) % StreakGifts.Length];
    public static int StreakDayIndex => (Mathf.Max(1, Profile.Data.Streak) - 1) % StreakGifts.Length;

    public static bool CanClaimGift => !Profile.Data.StreakClaimed;

    public static bool ClaimGift() {
      if (!CanClaimGift) return false;
      int g = TodayGift;
      Profile.Data.StreakClaimed = true;
      Profile.Data.Coins += g;
      Profile.Data.Earned += g;
      Profile.Save();
      return true;
    }

    public static bool IsDone(State s) { var q = Def(s.Id); return q != null && s.Progress >= q.Goal; }

    public static bool Claim(State s) {
      var q = Def(s.Id);
      if (q == null || s.Claimed || s.Progress < q.Goal) return false;
      s.Claimed = true;
      Profile.Data.Coins += q.Reward;
      Profile.Data.Earned += q.Reward;
      Profile.Data.QuestsDone++;
      Profile.Save();
      Achievements.Check();
      return true;
    }

    /// <summary>Something waits to be collected (for the red dot on the menu button).</summary>
    public static bool AnythingToClaim {
      get {
        if (CanClaimGift) return true;
        foreach (var s in Profile.Data.Quests) if (!s.Claimed && IsDone(s)) return true;
        return false;
      }
    }

    /// <summary>Quests finished by the last match, for the result screen.</summary>
    public static readonly List<string> JustFinished = new();

    public static void ResetSession() => JustFinished.Clear();

    /// <summary>Count one finished match. Called once per match from the reward payout; does not save (the caller does).</summary>
    public static void OnMatchEnd(GameDirector dir, PlayerAvatar me, bool won, int cleared) {
      JustFinished.Clear();
      var d = Profile.Data;
      if (d.QuestDay != Today) { Refresh(); }
      bool withFriends = PlayerAvatar.All.Count >= 2;
      foreach (var s in d.Quests) {
        var q = Def(s.Id);
        if (q == null) continue;
        bool wasDone = s.Progress >= q.Goal;
        int add = s.Id switch {
          "kills" => me.KillCount,
          "games" => 1,
          "dawn" => !dir.IsContest && won ? 1 : 0,
          "watches" => dir.IsContest ? 0 : cleared,
          "boss" => dir.IsContest ? (dir.BossFinisher != Fusion.PlayerRef.None ? 1 : 0) : (cleared >= 4 ? 2 : cleared >= 3 ? 1 : 0),
          "contest" => dir.IsContest && won ? 1 : 0,
          "friends" => withFriends ? 1 : 0,
          _ => 0,
        };
        s.Progress = Mathf.Min(q.Goal, s.Progress + add);
        if (!wasDone && s.Progress >= q.Goal) JustFinished.Add(Describe(q));
      }
    }
  }
}
