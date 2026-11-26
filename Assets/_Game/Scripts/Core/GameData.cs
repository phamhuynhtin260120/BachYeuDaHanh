using System;
using System.Collections.Generic;
using UnityEngine;

namespace BachYeu {
  public enum EnemyType : byte { Ghost, Umbrella, Heavy, Elite, Cat, King, Wisp, Inkstone, Shade, Firecracker } // append only: the value goes over the network

  public enum RunState : byte { Playing, Cleared, Blessing, Won, Lost, Lobby }

  /// <summary>Quick pings players send to each other (keys 1, 2, 3 or the ping buttons).</summary>
  public static class Pings {
    public static readonly string[] Text = { "Lại đây!", "Đứng đèn!", "Cẩn thận!" };
    public static readonly Color[] Color = { Gfx.Hex("#2f6d9c"), Gfx.Hex("#b5862e"), Gfx.Hex("#a8321f") };
  }

  /// <summary>Choices made on the title menu, read when the session starts.</summary>
  public static class LocalProfile {
    public static string Name = "";
    public static int Season;
    /// <summary>0 = Hành trình (co-op), 1 = Tranh Đăng (2 teams).</summary>
    public static int Mode;
    /// <summary>The two masks this player takes into a run (ids into Masks.All).</summary>
    public static int MaskA = Masks.Fox, MaskB = Masks.Tengu;

    /// <summary>Hành trình: the night (chapter) to start from. 1..6 = story chapters, 7 = Đêm tự do.</summary>
    public static int Chapter {
      get { if (_chapter <= 0) _chapter = Mathf.Max(1, PlayerPrefs.GetInt(ChapterKey, 1)); return _chapter; }
      private set => _chapter = value;
    }
    static int _chapter;
    public static void ResetSession() => _chapter = 0;
    const string ChapterKey = "byd.chapter";
    public static void SetChapter(int night) { Chapter = Mathf.Max(1, night); PlayerPrefs.SetInt(ChapterKey, Chapter); PlayerPrefs.Save(); }

    const string MaskKeyA = "byd.maskA", MaskKeyB = "byd.maskB";
    public static void LoadMasks() {
      int a = PlayerPrefs.GetInt(MaskKeyA, Masks.Fox), b = PlayerPrefs.GetInt(MaskKeyB, Masks.Tengu);
      if (Masks.Valid(a, b)) { MaskA = a; MaskB = b; }
    }
    public static void SaveMasks(int a, int b) {
      if (!Masks.Valid(a, b)) return;
      MaskA = a; MaskB = b;
      PlayerPrefs.SetInt(MaskKeyA, a); PlayerPrefs.SetInt(MaskKeyB, b); PlayerPrefs.Save();
    }
  }

  /// <summary>Play modes. Hành trình is the co-op night; Tranh Đăng is two teams racing for lanterns and the boss.</summary>
  public static class Modes {
    public const int Journey = 0, Contest = 1;
    public static readonly string[] Name = { "Hành trình", "Tranh Đăng" };
  }

  /// <summary>Tranh Đăng rules and layout. Team 0 = Đội Lam (west bank), team 1 = Đội Chu (east bank).</summary>
  public static class Contest {
    public const float MatchSeconds = 360f, ActSeconds = 120f, FullHoldSeconds = 15f, AutoReviveSeconds = 8f;
    public const float LanternRadius = 2.2f, CaptureSeconds = 3f;
    public const int KillPoints = 2, ElitePoints = 10, BossDamagePoints = 30, BossFinishPoints = 20;
    public static readonly string[] TeamName = { "Đội Lam", "Đội Chu" };
    public static readonly Color[] TeamColor = { Gfx.Hex("#3e6b62"), Gfx.Hex("#a8321f") };
    public static readonly Vector3[] Base = { new(-9, 0, -4.5f), new(9, 0, -4.5f) };
    /// <summary>Đèn Chợ (west), Đèn Bến (east), Đèn Cầu (center, worth double).</summary>
    public static readonly Vector3[] Lanterns = { new(-5.5f, 0, 4), new(5.5f, 0, 4), new(0, 0, -6.5f) }; // Đèn Cầu sits in front of the lotus pond at the center
    public static readonly string[] LanternName = { "Đèn Chợ", "Đèn Bến", "Đèn Cầu" };
    public static readonly int[] LanternPoints = { 1, 1, 2 };
    public static readonly Vector3 BossSpawn = new(0, 0, 8);
    public static readonly string[] ActTitle = { "Hồi 1 · Giành đèn", "Hồi 2 · Yêu quái tràn", "Hồi 3 · Tranh boss" };
    public static readonly string[] ActSub = {
      "Đứng trong vòng đèn để đổi màu đèn sang đội mình",
      "Hạ tinh anh bờ mình để gửi Gấu tinh sang đội kia",
      "Hồng Đăng Quỷ Vương thức giấc. Đội đánh nhiều hơn và đội kết liễu đều được thưởng",
    };
    // ---- Hazards that make life hard for both teams ----
    /// <summary>Mưa mực: ink circles rain onto lanterns people are standing on.</summary>
    public const float InkRainFirst = 20f, InkRainEvery = 25f, InkRainRadius = 1.9f, InkRainWarn = 1.4f;
    public const int InkRainDamage = 14;
    /// <summary>Mắt Trơi: from 2:00, one at a time, a wisp steals the most valuable lantern; last hit is worth points.</summary>
    public const float WispFirst = 120f, WispEvery = 60f, WispStealSeconds = 2.5f;
    public const int WispPoints = 15;
    public static readonly Vector3 WispSpawn = new(0, 0, 11);
    public static readonly Color WispColor = Gfx.Hex("#3fb8a8");
    /// <summary>Lời nguyền Hồng Đăng: the team ahead by this much is hunted harder by yokai and the boss.</summary>
    public const int CurseOn = 30, CurseOff = 15;
    public const float CurseTargetWeight = .55f; // distances to cursed players count this much
    /// <summary>Lantern owner color: 0 Đội Lam, 1 Đội Chu, 2 Mắt Trơi.</summary>
    public static Color OwnerColor(int owner) => owner == 2 ? WispColor : TeamColor[Mathf.Clamp(owner, 0, 1)];

    public static Vector3 SpawnPoint(int team, int slot) => Base[Mathf.Clamp(team, 0, 1)] + new Vector3(0, 0, (slot % 2) * 1.4f - .7f);
  }

  public static class EnemyStats {
    public static int Hp(EnemyType t) => t switch {
      EnemyType.Ghost => 60, EnemyType.Umbrella => 95, EnemyType.Heavy => 240,
      EnemyType.Elite => 520, EnemyType.Cat => 2000, EnemyType.Wisp => 650,
      EnemyType.Inkstone => 80, EnemyType.Shade => 110, EnemyType.Firecracker => 70, _ => 4200,
    };
    public static float Speed(EnemyType t) => t switch {
      EnemyType.Ghost => 2.6f, EnemyType.Heavy => 1.5f, EnemyType.Cat => 2.4f, EnemyType.King => 1.1f, EnemyType.Wisp => 2.3f,
      EnemyType.Inkstone => 1.6f, EnemyType.Shade => 3.0f, EnemyType.Firecracker => 3.4f, _ => 0f,
    };
    public static float Radius(EnemyType t) => t switch {
      EnemyType.Ghost => .5f, EnemyType.Umbrella => .6f, EnemyType.Heavy => .9f,
      EnemyType.Elite => 1f, EnemyType.Cat => 1.5f, EnemyType.Wisp => .7f,
      EnemyType.Inkstone => .6f, EnemyType.Shade => .5f, EnemyType.Firecracker => .5f, _ => 1.8f,
    };
    public static int Contact(EnemyType t) => t switch {
      EnemyType.Ghost => 6, EnemyType.Umbrella => 8, EnemyType.Heavy => 18, EnemyType.Elite => 16, EnemyType.Wisp => 0,
      EnemyType.Inkstone => 4, EnemyType.Shade => 9, EnemyType.Firecracker => 0, _ => 10,
    };
    public static bool IsBoss(EnemyType t) => t == EnemyType.Cat || t == EnemyType.King;
    public static string Name(EnemyType t) => t switch {
      EnemyType.Cat => "Hỏa Khuyển", EnemyType.King => "Hồng Đăng Quỷ Vương", EnemyType.Wisp => "Mắt Trơi", _ => "",
    };
  }

  public static class Phases {
    public static readonly string[] Title = { "Giờ Tuất · Phố chợ", "Giờ Hợi · Bến sông", "Giờ Tý · Sân đền", "Giờ Sửu · Cổng Hồng Đăng" };
    public static readonly string[] Short = { "Giờ Tuất", "Giờ Hợi", "Giờ Tý", "Giờ Sửu" };
    public static readonly string[] Sub = { "Thắp 3 lồng đèn để mở đường", "Yêu quái kéo đến từ mặt nước", "Hỏa Khuyển chặn đường", "Hồng Đăng Quỷ Vương thức giấc" };
    public static readonly Vector3[] Lanterns = { new(-7, 0, 3), new(7, 0, 4), new(0, 0, 10) };
    public const int Phase2Kills = 16;
  }

  public class MaskDef {
    public string Name, Element, Role, S1, S2, Ult;
    public float S1Cd, S2Cd, AtkCd;
    public Color Color;
    /// <summary>One line per item for the picker: attack, skill 1, skill 2, ultimate, what swapping in does.</summary>
    public string Attack, S1Desc, S2Desc, UltDesc, SwapDesc;
  }

  /// <summary>
  /// The mask collection. Each player takes two into a run and swaps between them.
  /// Ids are indexes into All and are what goes over the network: never reorder, only append.
  /// </summary>
  public static class Masks {
    public const int Fox = 0, Tengu = 1, HaBa = 2, QuyDa = 3;
    public static readonly MaskDef[] All = {
      new() { Name = "Hồ Ly", Element = "Lửa", Role = "Sát thương", Color = Gfx.Hex("#c4452c"),
              S1 = "Hỏa", S2 = "Huyễn", Ult = "Cửu vĩ", S1Cd = 4, S2Cd = 6, AtkCd = .3f,
              Attack = "Chuỗi 3 nhát, nhát cuối hất lùi",
              S1Desc = "3 hồ hỏa tự đuổi yêu quái gần nhất",
              S2Desc = "Dịch chuyển 5 mét, gây sát thương dọc đường",
              UltDesc = "3 đợt lửa bùng quanh mình",
              SwapDesc = "Bùng lửa quanh mình" },
      new() { Name = "Tengu", Element = "Gió", Role = "Khống chế", Color = Gfx.Hex("#2f6d9c"),
              S1 = "Lốc", S2 = "Giáng", Ult = "Phong", S1Cd = 5, S2Cd = 6, AtkCd = .48f,
              Attack = "Chém gió rộng, hất lùi",
              S1Desc = "Lốc xoáy hút và đẩy yêu quái",
              S2Desc = "Nhảy tới yêu quái gần nhất, đáp xuống đập mạnh",
              UltDesc = "5 đợt gió quét quanh mình",
              SwapDesc = "Thổi bật yêu quái ra xa" },
      new() { Name = "Hà Bá", Element = "Nước", Role = "Hỗ trợ", Color = Gfx.Hex("#2f8f8a"),
              S1 = "Suối", S2 = "Thủy thuẫn", Ult = "Hồng thủy", S1Cd = 7, S2Cd = 9, AtkCd = .38f,
              Attack = "Roi nước quất trước mặt",
              S1Desc = "Suối mát: hồi 22 máu cho mình và đồng đội gần",
              S2Desc = "Khiên nước 35 điểm cho mình và đồng đội gần, 5 giây",
              UltDesc = "Sóng lớn: hồi máu và tạo khiên cả đội, cuốn trôi yêu quái",
              SwapDesc = "Vòng nước hồi 10 máu cho người gần" },
      new() { Name = "Quỷ Đá", Element = "Đất", Role = "Chống chịu", Color = Gfx.Hex("#8a6a4e"),
              S1 = "Gồng", S2 = "Tường đá", Ult = "Địa chấn", S1Cd = 8, S2Cd = 10, AtkCd = .62f,
              Attack = "Đấm nặng, hất văng. Đeo mặt nạ này nhận ít sát thương hơn 15%",
              S1Desc = "Gồng đá 4 giây: giảm 60% sát thương, không bị đẩy",
              S2Desc = "Dựng tường đá chắn đường trước mặt trong 6 giây",
              UltDesc = "3 lần dậm đất, đập mạnh mọi thứ quanh mình",
              SwapDesc = "Dậm đất hất văng yêu quái" },
    };

    public static bool Valid(int a, int b) => a >= 0 && b >= 0 && a < All.Length && b < All.Length && a != b;
  }

  /// <summary>Per-player run modifiers picked between phases. Local only.</summary>
  public class Blessings {
    public float FoxAuto, Res = .3f, ResDur = 3, SwapMul = 1, CdMul = 1, UltMul = 1;
    // From pháp khí (Gear.ApplyTo): damage and dash cooldown multipliers, shield and ult at each phase start.
    public float DmgMul = 1, DashMul = 1, PhaseShield, PhaseUlt;
    public bool WindBlade, InkDash;
    public int Leech;
    public readonly List<string> Picked = new();
  }

  public class BlessingDef {
    public string Name, Desc;
    public bool Repeatable;
    public Action<PlayerAvatar> Apply;
  }

  public static class BlessingPool {
    public static readonly BlessingDef[] All = {
      new() { Name = "Đèn lồng hộ thân", Desc = "Máu tối đa +25 và hồi đầy máu.", Repeatable = true, Apply = p => p.AddMaxHp(25) },
      new() { Name = "Hồ hỏa tự phát", Desc = "Đòn đánh thường của Hồ Ly có 35% bắn thêm một hồ hỏa.", Repeatable = true, Apply = p => p.B.FoxAuto += .35f },
      new() { Name = "Lưỡi gió", Desc = "Đòn đánh thường của Tengu phóng thêm một lưỡi gió xuyên địch.", Apply = p => p.B.WindBlade = true },
      new() { Name = "Cộng hưởng sâu", Desc = "Sau khi đổi mặt nạ: sát thương +60% trong 5 giây.", Apply = p => { p.B.Res = .6f; p.B.ResDur = 5; } },
      new() { Name = "Thay mặt chớp nhoáng", Desc = "Hồi chiêu đổi mặt nạ giảm 40%.", Apply = p => p.B.SwapMul *= .6f },
      new() { Name = "Mực loang", Desc = "Lướt né để lại vòng mực gây 50 sát thương.", Apply = p => p.B.InkDash = true },
      new() { Name = "Hút linh hỏa", Desc = "Mỗi yêu quái bạn hạ hồi 3 máu.", Repeatable = true, Apply = p => p.B.Leech += 3 },
      new() { Name = "Tâm tĩnh như nước", Desc = "Hồi chiêu mọi skill giảm 20%.", Repeatable = true, Apply = p => p.B.CdMul *= .8f },
      new() { Name = "Nộ hỏa", Desc = "Tuyệt kỹ tích đầy nhanh gấp 1,5 lần.", Repeatable = true, Apply = p => p.B.UltMul *= 1.5f },
    };

    public static List<BlessingDef> Roll(Blessings b, int n) {
      var pool = new List<BlessingDef>();
      foreach (var d in All) if (d.Repeatable || !b.Picked.Contains(d.Name)) pool.Add(d);
      var pick = new List<BlessingDef>();
      while (pick.Count < n && pool.Count > 0) {
        int i = UnityEngine.Random.Range(0, pool.Count);
        pick.Add(pool[i]);
        pool.RemoveAt(i);
      }
      return pick;
    }
  }
}
