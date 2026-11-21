using System;
using System.Collections.Generic;
using UnityEngine;

namespace BachYeu {
  /// <summary>One pháp khí (charm) the player can buy, upgrade (3 levels) and carry into a match.</summary>
  public class GearDef {
    public string Id, Name, Short, Effect;
    public Color Color;
    public int Price;
    public float[] Values;                       // effect value per level 1..3
    public Func<float, string> Describe;          // value -> text
    public Action<PlayerAvatar, float> Apply;     // run start (after stats reset)
  }

  /// <summary>Pháp khí catalogue. Ids are saved: never rename or remove one.</summary>
  public static class Gear {
    public const int Slots = 3, MaxLevel = 3;
    public static readonly int[] UpgradeCost = { 0, 200, 350 }; // cost to reach level 2, 3

    public static readonly GearDef[] All = {
      new() { Id = "bua_ho_menh", Name = "Bùa hộ mệnh", Short = "Bùa", Color = Gfx.Hex("#a8321f"), Price = 120, Values = new[] { 12f, 24f, 36f },
        Describe = v => $"Máu tối đa +{v:0}", Apply = (p, v) => p.AddMaxHp(Mathf.RoundToInt(v)) },
      new() { Id = "ngoc_ho_ly", Name = "Ngọc Hồ Ly", Short = "Ngọc", Color = Gfx.Hex("#c8502a"), Price = 160, Values = new[] { .05f, .10f, .15f },
        Describe = v => $"Sát thương +{v * 100:0}%", Apply = (p, v) => p.B.DmgMul *= 1 + v },
      new() { Id = "chuong_dong", Name = "Chuông đồng", Short = "Chuông", Color = Gfx.Hex("#b5862e"), Price = 150, Values = new[] { .06f, .12f, .18f },
        Describe = v => $"Hồi chiêu kỹ năng −{v * 100:0}%", Apply = (p, v) => p.B.CdMul *= 1 - v },
      new() { Id = "quat_long_hac", Name = "Quạt lông hạc", Short = "Quạt", Color = Gfx.Hex("#2f6d9c"), Price = 120, Values = new[] { .12f, .24f, .36f },
        Describe = v => $"Hồi chiêu lướt né −{v * 100:0}%", Apply = (p, v) => p.B.DashMul *= 1 - v },
      new() { Id = "guong_bat_quai", Name = "Gương bát quái", Short = "Gương", Color = Gfx.Hex("#3e6b62"), Price = 150, Values = new[] { 15f, 25f, 35f },
        Describe = v => $"Đầu mỗi hồi có khiên chặn {v:0} sát thương", Apply = (p, v) => p.B.PhaseShield += v },
      new() { Id = "ho_lo", Name = "Hồ lô linh tửu", Short = "Hồ lô", Color = Gfx.Hex("#5f8f55"), Price = 130, Values = new[] { 2f, 3f, 4f },
        Describe = v => $"Hạ một yêu quái hồi {v:0} máu", Apply = (p, v) => p.B.Leech += Mathf.RoundToInt(v) },
      new() { Id = "trong_dong", Name = "Trống đồng nhỏ", Short = "Trống", Color = Gfx.Hex("#8a6a4e"), Price = 160, Values = new[] { .15f, .30f, .45f },
        Describe = v => $"Tuyệt kỹ tích nhanh hơn {v * 100:0}%", Apply = (p, v) => p.B.UltMul *= 1 + v },
      new() { Id = "den_keo_quan", Name = "Đèn kéo quân", Short = "Đèn", Color = Gfx.Hex("#d0552c"), Price = 140, Values = new[] { 20f, 35f, 50f },
        Describe = v => $"Vào mỗi hồi với {v:0}% tuyệt kỹ", Apply = (p, v) => p.B.PhaseUlt = Mathf.Max(p.B.PhaseUlt, v) },
    };

    public static GearDef Find(string id) { foreach (var g in All) if (g.Id == id) return g; return null; }

    /// <summary>Applies the carried pháp khí to the local player. Call right after the run's stats are reset.</summary>
    public static void ApplyTo(PlayerAvatar p) {
      var prof = Profile.Data;
      foreach (var id in prof.Equipped) {
        var g = Find(id);
        int lv = Profile.Level(id);
        if (g == null || lv <= 0) continue;
        g.Apply(p, g.Values[Mathf.Clamp(lv, 1, MaxLevel) - 1]);
      }
    }
  }

  /// <summary>Saved player progress: Hồn đăng (currency), owned pháp khí, what is carried, lifetime stats.</summary>
  public static class Profile {
    [Serializable] public class Owned { public string Id; public int Level; }

    [Serializable]
    public class SaveData {
      public int Version = 1;
      public int Coins = 200;                     // enough for a first pháp khí
      public List<Owned> Items = new();
      public List<string> Equipped = new();
      public int Games, Wins, Kills, Earned;
      public int StoryCleared;                    // highest Hành trình chapter cleared
      public List<int> StorySeen = new();          // chapters whose opening talk was shown
      public List<int> YokaiSeen = new();          // Bách yêu lục: times met, by EnemyType
      public List<int> YokaiSlain = new();         // Bách yêu lục: times fallen in your matches, by EnemyType
      public bool TutorialDone;                    // first-match guide finished or skipped
      // Nhiệm vụ hằng ngày and the login gift (dates as yyyyMMdd, local time).
      public string QuestDay = "", LastLoginDay = "";
      public List<Quests.State> Quests = new();
      public int Streak;
      public bool StreakClaimed;
      // Lifetime counters for Thành tựu, and the unlocked ones.
      public int Dawns, CleanDawns, BossKills, Revives, Damage, ContestWins, FullLightWins, HoldSeconds, QuestsDone, MaxStreak, MaxParty;
      public List<int> MasksUsed = new();
      public List<string> AchDone = new();
      public string Title = "";                    // danh hiệu worn beside the name
    }

    const string Key = "byd.profile";
    static SaveData _data;

    public static SaveData Data {
      get {
        if (_data != null) return _data;
        var json = PlayerPrefs.GetString(Key, "");
        try { _data = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json); } catch { _data = null; }
        _data ??= new SaveData();
        _data.Items ??= new(); _data.Equipped ??= new(); _data.StorySeen ??= new(); _data.YokaiSeen ??= new(); _data.YokaiSlain ??= new(); _data.Quests ??= new(); _data.MasksUsed ??= new(); _data.AchDone ??= new(); _data.Title ??= ""; _data.QuestDay ??= ""; _data.LastLoginDay ??= "";
        _data.Equipped.RemoveAll(id => Level(id) <= 0);
        return _data;
      }
    }

    public static event Action Changed;

    public static void ResetSession() { _data = null; _lastRewardKey = null; Changed = null; }

    public static void Save() {
      PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
      PlayerPrefs.Save();
      Changed?.Invoke();
    }

    public static int Coins => Data.Coins;
    public static int Level(string id) { foreach (var o in Data.Items) if (o.Id == id) return o.Level; return 0; }
    public static bool IsEquipped(string id) => Data.Equipped.Contains(id);

    /// <summary>Price of the next step for this item: buy, or upgrade; 0 when maxed.</summary>
    public static int NextCost(GearDef g) {
      int lv = Level(g.Id);
      if (lv <= 0) return g.Price;
      return lv >= Gear.MaxLevel ? 0 : Gear.UpgradeCost[lv];
    }

    /// <summary>Buys or upgrades. Returns false if not enough Hồn đăng or already max.</summary>
    public static bool BuyOrUpgrade(GearDef g) {
      int cost = NextCost(g);
      if (cost <= 0 || Data.Coins < cost) return false;
      Data.Coins -= cost;
      var o = Data.Items.Find(x => x.Id == g.Id);
      if (o == null) { Data.Items.Add(new Owned { Id = g.Id, Level = 1 }); if (Data.Equipped.Count < Gear.Slots) Data.Equipped.Add(g.Id); }
      else o.Level++;
      Save();
      Achievements.Check();
      return true;
    }

    /// <summary>Carry or put back. When all slots are full, carrying fails (put one back first).</summary>
    public static bool ToggleEquip(string id) {
      if (Level(id) <= 0) return false;
      if (Data.Equipped.Remove(id)) { Save(); return true; }
      if (Data.Equipped.Count >= Gear.Slots) return false;
      Data.Equipped.Add(id);
      Save();
      return true;
    }

    // ---------- match rewards ----------
    static string _lastRewardKey;

    /// <summary>
    /// Pays out once per finished match (local player). Night: base + kills + watches cleared + dawn bonus.
    /// Tranh Đăng: base + kills + win/draw bonus. Returns the amount, or -1 if this match was already paid.
    /// </summary>
    public static int GrantMatchReward(GameDirector dir, PlayerAvatar me) {
      if (!dir || !me) return -1;
      string key = dir.RunStartTick + "/" + dir.State;
      if (key == _lastRewardKey) return -1;
      _lastRewardKey = key;
      int kills = Mathf.Min(me.KillCount, 60);
      int coins; bool won;
      if (dir.IsContest) {
        won = dir.Winner == me.Team;
        coins = 30 + kills + (won ? 40 : dir.Winner < 0 ? 20 : 0);
      } else {
        won = dir.State == RunState.Won;
        int cleared = won ? 4 : dir.Phase;
        coins = 15 + kills + cleared * 20 + (won ? 60 : 0);
      }
      Data.Coins += coins;
      Data.Earned += coins;
      Data.Games++;
      if (won) Data.Wins++;
      Data.Kills += me.KillCount;
      Achievements.JustUnlocked.Clear(); // the result screen shows only what this match unlocked
      int clearedPhases = dir.IsContest ? 0 : (won ? 4 : dir.Phase);
      BachYeu.Quests.OnMatchEnd(dir, me, won, clearedPhases);
      // Lifetime counters for Thành tựu.
      if (!dir.IsContest && won) { Data.Dawns++; if (me.Downs == 0) Data.CleanDawns++; }
      Data.BossKills += dir.IsContest ? (dir.BossFinisher == me.Object.StateAuthority ? 1 : 0) : (clearedPhases >= 4 ? 2 : clearedPhases >= 3 ? 1 : 0);
      Data.Revives += me.Revives;
      Data.Damage += me.DamageDealt;
      if (dir.IsContest && won) { Data.ContestWins++; if (dir.WonByFullLight) Data.FullLightWins++; }
      Data.HoldSeconds += Mathf.RoundToInt(me.HoldTime);
      foreach (var m in new[] { me.MaskA, me.MaskB }) if (!Data.MasksUsed.Contains(m)) Data.MasksUsed.Add(m);
      Data.MaxParty = Mathf.Max(Data.MaxParty, PlayerAvatar.All.Count);
      Save();
      Achievements.Check();
      return coins;
    }
  }
}
