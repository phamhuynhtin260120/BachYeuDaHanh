using System;
using System.Collections.Generic;
using UnityEngine;

namespace BachYeu {
  public enum EnemyType : byte { Ghost, Umbrella, Heavy, Elite, Cat, King }

  public enum RunState : byte { Playing, Cleared, Blessing, Won, Lost }

  public static class EnemyStats {
    public static int Hp(EnemyType t) => t switch {
      EnemyType.Ghost => 60, EnemyType.Umbrella => 95, EnemyType.Heavy => 240,
      EnemyType.Elite => 520, EnemyType.Cat => 2000, _ => 4200,
    };
    public static float Speed(EnemyType t) => t switch {
      EnemyType.Ghost => 2.6f, EnemyType.Heavy => 1.5f, EnemyType.Cat => 2.4f, EnemyType.King => 1.1f, _ => 0f,
    };
    public static float Radius(EnemyType t) => t switch {
      EnemyType.Ghost => .5f, EnemyType.Umbrella => .6f, EnemyType.Heavy => .9f,
      EnemyType.Elite => 1f, EnemyType.Cat => 1.5f, _ => 1.8f,
    };
    public static int Contact(EnemyType t) => t switch {
      EnemyType.Ghost => 6, EnemyType.Umbrella => 8, EnemyType.Heavy => 18, EnemyType.Elite => 16, _ => 10,
    };
    public static bool IsBoss(EnemyType t) => t == EnemyType.Cat || t == EnemyType.King;
    public static string Name(EnemyType t) => t switch {
      EnemyType.Cat => "Miêu yêu song vĩ", EnemyType.King => "Hồng Đăng Quỷ Vương", _ => "",
    };
  }

  public static class Phases {
    public static readonly string[] Title = { "Giờ Tuất · Phố chợ", "Giờ Hợi · Bến sông", "Giờ Tý · Sân đền", "Giờ Sửu · Cổng Hồng Đăng" };
    public static readonly string[] Short = { "Giờ Tuất", "Giờ Hợi", "Giờ Tý", "Giờ Sửu" };
    public static readonly string[] Sub = { "Thắp 3 lồng đèn để mở đường", "Yêu quái kéo đến từ mặt nước", "Miêu yêu song vĩ chặn đường", "Hồng Đăng Quỷ Vương thức giấc" };
    public static readonly Vector3[] Lanterns = { new(-7, 0, 3), new(7, 0, 4), new(0, 0, 10) };
    public const int Phase2Kills = 16;
  }

  public class MaskDef {
    public string Name, S1, S2, Ult;
    public float S1Cd, S2Cd, AtkCd;
  }

  public static class Masks {
    public static readonly MaskDef[] All = {
      new() { Name = "Hồ Ly", S1 = "Hỏa", S2 = "Huyễn", Ult = "Cửu vĩ", S1Cd = 4, S2Cd = 6, AtkCd = .3f },
      new() { Name = "Tengu", S1 = "Lốc", S2 = "Giáng", Ult = "Phong", S1Cd = 5, S2Cd = 6, AtkCd = .48f },
    };
  }

  /// <summary>Per-player run modifiers picked between phases. Local only.</summary>
  public class Blessings {
    public float FoxAuto, Res = .3f, ResDur = 3, SwapMul = 1, CdMul = 1, UltMul = 1;
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
