using UnityEngine;

namespace BachYeu {
  public class YokaiEntry {
    public EnemyType Type;
    public string Name, Kind, Where, Lore, Tip;
    public Color Color;
  }

  /// <summary>
  /// Bách yêu lục: the yokai codex. Every kind you meet is written in (first sighting pays a little Hồn đăng),
  /// and the book keeps how many of each have fallen while you were there. Saved in the Profile.
  /// </summary>
  public static class Bestiary {
    public const int FirstSightReward = 20;

    // Indexed by EnemyType (append only, like the enum).
    public static readonly YokaiEntry[] All = {
      new() {
        Type = EnemyType.Ghost, Name = "Dơi đèn", Kind = "Yêu quái thường", Color = Gfx.Hex("#9e2a2a"),
        Where = "Mọi canh giờ · Tranh Đăng",
        Lore = "Lũ dơi đỏ ngủ dưới mái đình, cứ đến mùa hội là thức dậy. Chúng mê ánh lồng đèn, bay thành đàn tới chỗ nào sáng nhất và có hơi người.",
        Tip = "Yếu nhưng đông. Gom chúng lại một chỗ rồi dùng chiêu đánh vòng để hạ cả đàn.",
      },
      new() {
        Type = EnemyType.Umbrella, Name = "Thạch yêu", Kind = "Yêu quái thường", Color = Gfx.Hex("#4f9a6a"),
        Where = "Mọi canh giờ · Tranh Đăng",
        Lore = "Mẻ thạch lá dứa đổ hỏng sau đêm hội, để lâu bên bến sông mà thành tinh. Nó nhảy lò cò từng cú, dính vào ai thì người ấy cũng thấy nặng chân.",
        Tip = "Nó lao tới theo từng cú nhảy. Lùi lại lúc nó bật lên, đánh trả khi nó vừa tiếp đất.",
      },
      new() {
        Type = EnemyType.Heavy, Name = "Gấu tinh", Kind = "Yêu quái lớn", Color = Gfx.Hex("#6b4a32"),
        Where = "Từ giờ Hợi · Quỷ Vương gọi tới · Tranh Đăng",
        Lore = "Con gấu già trên núi sau đền, ngủ đông quá trăm năm nên thành tinh. Chậm chạp, nhưng mỗi cú dậm làm rung cả mặt đất.",
        Tip = "Khi vòng đỏ hiện quanh nó, lướt ra ngoài ngay. Đánh vào lúc nó đang thở dốc sau cú dậm.",
      },
      new() {
        Type = EnemyType.Elite, Name = "Rương tham", Kind = "Tinh anh", Color = Gfx.Hex("#8a5a2b"),
        Where = "Giờ Hợi · Tranh Đăng",
        Lore = "Chiếc rương tiền công đức của đền, ăn mãi không no nên mọc ra răng. Nó dẫn đầu cả bầy, há miệng đớp mọi thứ lấp lánh trên đường đi.",
        Tip = "Máu dày, hay rơi đồ hồi máu. Ở Tranh Đăng, hạ nó sẽ gửi một Gấu tinh sang bờ đội bạn.",
      },
      new() {
        Type = EnemyType.Cat, Name = "Hỏa Khuyển", Kind = "Boss", Color = Gfx.Hex("#9e2f22"),
        Where = "Giờ Tý · Sân đền",
        Lore = "Con chó đỏ đeo vòng gai, canh sân đền từ thời chưa ai nhớ. Nó biết nói, nhưng chỉ nói nửa sự thật.",
        Tip = "Nó ngắm một đường thẳng rồi lao tới: né sang ngang khi vệt đỏ hiện ra. Còn nửa máu thì nó nổi giận và gọi thêm dơi đèn.",
      },
      new() {
        Type = EnemyType.King, Name = "Hồng Đăng Quỷ Vương", Kind = "Boss", Color = Gfx.Hex("#7a1a10"),
        Where = "Giờ Sửu · Cổng Hồng Đăng · Tranh Đăng",
        Lore = "Bóng áo choàng đỏ đội mũ cao, tay cầm hai lưỡi hái, ngồi sau cổng Hồng Đăng. Người trong phố tưởng nó canh cổng, nhưng thật ra nó cũng bị nhốt ở đó.",
        Tip = "Ba kiểu đòn: rải mực quanh bạn, dậm một vòng lớn quanh mình, và gọi yêu quái. Khi nổi giận, nó gọi thêm hai Gấu tinh.",
      },
      new() {
        Type = EnemyType.Wisp, Name = "Mắt Trơi", Kind = "Yêu quái hiếm", Color = Gfx.Hex("#6b4a8a"),
        Where = "Chỉ có ở Tranh Đăng · từ phút thứ hai",
        Lore = "Con mắt tím có cánh, lạc giữa hai bờ sông. Nó không đánh ai, chỉ nhìn chằm chằm rồi lặng lẽ trôi về ngọn đèn đang sáng nhất.",
        Tip = "Nó trôi tới cướp lồng đèn. Hạ nó trước để giữ đèn và ghi thêm điểm cho đội.",
      },
      new() {
        Type = EnemyType.Inkstone, Name = "Cây phun mực", Kind = "Yêu quái bắn xa", Color = Gfx.Hex("#4e7a3a"),
        Where = "Từ chương 3 · Giờ Tuất, Giờ Hợi",
        Lore = "Cây hoa mọc cạnh bàn viết của một ông đồ, uống nước mực lâu năm nên thành tinh. Nụ hoa của nó ngậm đầy mực, thấy ai đi ngang là phun.",
        Tip = "Nó đứng xa rồi phun vòng mực vào chỗ bạn đứng. Đừng đứng yên: lướt tới áp sát, nó chậm và máu mỏng.",
      },
      new() {
        Type = EnemyType.Shade, Name = "Bóng không đèn", Kind = "Yêu quái tàng hình", Color = Gfx.Hex("#22232a"),
        Where = "Từ chương 5 · Giờ Hợi",
        Lore = "Hồn ma của một người cầm đèn đi lạc. Đèn tắt từ lâu, chỉ còn cái bóng đi tìm đường về, lặng lẽ và rất nhanh.",
        Tip = "Ở xa chỉ thấy một vệt tối dưới đất. Thấy vệt tối lướt về phía mình thì quay lại chém, nó hiện hình khi đến gần.",
      },
      new() {
        Type = EnemyType.Firecracker, Name = "Tiểu quỷ pháo", Kind = "Yêu quái tự nổ", Color = Gfx.Hex("#c62f22"),
        Where = "Từ chương 7 · mọi canh giờ",
        Lore = "Con tiểu quỷ đỏ trốn trong bánh pháo đêm giao thừa. Trong bụng nó vẫn âm ỉ một cái ngòi, và nó chỉ muốn nổ một lần cho thật to.",
        Tip = "Nó lao tới rồi châm ngòi: vòng đỏ hiện quanh nó là sắp nổ. Lướt ra xa ngay, hoặc hạ nó trước khi nó tới gần.",
      },
    };

    public static int Count => All.Length;

    static int Get(System.Collections.Generic.List<int> list, int i) => i >= 0 && i < list.Count ? list[i] : 0;
    static void Add(System.Collections.Generic.List<int> list, int i, int n) {
      while (list.Count <= i) list.Add(0);
      list[i] += n;
    }

    public static bool Seen(EnemyType t) => Get(Profile.Data.YokaiSeen, (int)t) > 0;
    public static int Slain(EnemyType t) => Get(Profile.Data.YokaiSlain, (int)t);
    public static int SeenCount { get { int n = 0; foreach (var e in All) if (Seen(e.Type)) n++; return n; } }

    /// <summary>A yokai appeared on this machine. The first time ever, it is written in and pays a small reward.</summary>
    public static void NoteSeen(EnemyType t) {
      int i = (int)t;
      if (i < 0 || i >= All.Length) return;
      bool first = !Seen(t);
      Add(Profile.Data.YokaiSeen, i, 1);
      if (!first) return; // the counter is saved with the match reward
      Profile.Data.Coins += FirstSightReward;
      Profile.Data.Earned += FirstSightReward;
      Profile.Save();
      if (Hud.I) Hud.I.Toast($"Bách yêu lục ghi thêm: {All[i].Name} · +{FirstSightReward} Hồn đăng", UiKit.Gold);
      Achievements.Check();
    }

    /// <summary>A yokai fell while you were in the match (anyone's kill counts).</summary>
    public static void NoteSlain(EnemyType t) {
      int i = (int)t;
      if (i < 0 || i >= All.Length) return;
      Add(Profile.Data.YokaiSlain, i, 1);
    }
  }
}
