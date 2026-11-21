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
        Type = EnemyType.Ghost, Name = "Lồng đèn ma", Kind = "Yêu quái thường", Color = Gfx.Hex("#c0392b"),
        Where = "Mọi canh giờ · Tranh Đăng",
        Lore = "Lồng đèn giấy bị bỏ quên sau hội, lâu ngày hoá yêu. Chúng bay lửng lơ và kéo nhau cả đàn tới chỗ có hơi người.",
        Tip = "Yếu nhưng đông. Gom chúng lại một chỗ rồi dùng chiêu đánh vòng để hạ cả đám.",
      },
      new() {
        Type = EnemyType.Umbrella, Name = "Ô giấy một mắt", Kind = "Yêu quái thường", Color = Gfx.Hex("#6b4a8a"),
        Where = "Mọi canh giờ · Tranh Đăng",
        Lore = "Chiếc ô giấy rách bị vứt bên bến sông. Nó nhảy lò cò từng bước, thè cái lưỡi đỏ doạ người qua đường.",
        Tip = "Nó lao tới theo từng cú nhảy. Lùi lại lúc nó bật lên, đánh trả khi nó vừa tiếp đất.",
      },
      new() {
        Type = EnemyType.Heavy, Name = "Yêu to khỏe", Kind = "Yêu quái lớn", Color = Gfx.Hex("#6a6f7a"),
        Where = "Từ giờ Hợi · Quỷ Vương gọi tới · Tranh Đăng",
        Lore = "Khối đá dưới chân cầu thấm mực lâu năm mà thành tinh. Chậm chạp, nhưng mỗi cú dậm làm rung cả mặt đất.",
        Tip = "Khi vòng đỏ hiện quanh nó, lướt ra ngoài ngay. Đánh vào lúc nó đang thở dốc sau cú dậm.",
      },
      new() {
        Type = EnemyType.Elite, Name = "Ô yêu đầu đàn", Kind = "Tinh anh", Color = Gfx.Hex("#4e3566"),
        Where = "Giờ Hợi · Tranh Đăng",
        Lore = "Chiếc ô lớn nhất bầy, đã nuốt nhiều chiếc ô khác nên to gấp đôi. Đám ô giấy nhỏ hay lẽo đẽo theo sau nó.",
        Tip = "Máu dày, hay rơi đồ hồi máu. Ở Tranh Đăng, hạ nó sẽ gửi một Yêu to khỏe sang bờ đội bạn.",
      },
      new() {
        Type = EnemyType.Cat, Name = "Miêu yêu song vĩ", Kind = "Boss", Color = Gfx.Hex("#3a3040"),
        Where = "Giờ Tý · Sân đền",
        Lore = "Con mèo đen sống quá trăm năm trong sân đền nên mọc thêm một cái đuôi. Nó biết nói, nhưng chỉ nói nửa sự thật.",
        Tip = "Nó ngắm một đường thẳng rồi lao tới: né sang ngang khi vệt đỏ hiện ra. Còn nửa máu thì nó nổi giận và gọi thêm lồng đèn ma.",
      },
      new() {
        Type = EnemyType.King, Name = "Hồng Đăng Quỷ Vương", Kind = "Boss", Color = Gfx.Hex("#7a1a10"),
        Where = "Giờ Sửu · Cổng Hồng Đăng · Tranh Đăng",
        Lore = "Kẻ ngồi sau cổng Hồng Đăng. Người trong phố tưởng nó canh cổng, nhưng thật ra nó cũng bị nhốt ở đó.",
        Tip = "Ba kiểu đòn: rải mực quanh bạn, dậm một vòng lớn quanh mình, và gọi yêu quái. Khi nổi giận, nó gọi thêm hai Yêu to khỏe.",
      },
      new() {
        Type = EnemyType.Wisp, Name = "Ma Trơi", Kind = "Yêu quái hiếm", Color = Gfx.Hex("#2a9d8f"),
        Where = "Chỉ có ở Tranh Đăng · từ phút thứ hai",
        Lore = "Đốm lửa xanh lạc giữa hai bờ sông. Nó không đánh ai, chỉ lặng lẽ trôi về ngọn đèn đang sáng nhất.",
        Tip = "Nó trôi tới cướp lồng đèn. Hạ nó trước để giữ đèn và ghi thêm điểm cho đội.",
      },
      new() {
        Type = EnemyType.Inkstone, Name = "Nghiên mực yêu", Kind = "Yêu quái bắn xa", Color = Gfx.Hex("#3c3f44"),
        Where = "Từ chương 3 · Giờ Tuất, Giờ Hợi",
        Lore = "Phiến nghiên của một ông đồ không ai nhớ tên. Mực trong lòng nó chưa bao giờ khô, và nó thích hắt mực vào những ai đi ngang.",
        Tip = "Nó đứng xa rồi hắt vòng mực vào chỗ bạn đứng. Đừng đứng yên: lướt tới áp sát, nó chạy chậm và máu mỏng.",
      },
      new() {
        Type = EnemyType.Shade, Name = "Bóng không đèn", Kind = "Yêu quái tàng hình", Color = Gfx.Hex("#22232a"),
        Where = "Từ chương 5 · Giờ Hợi",
        Lore = "Cái bóng của một người cầm đèn đi lạc. Đèn tắt từ lâu, chỉ còn cái bóng đi tìm đường về, lặng lẽ và rất nhanh.",
        Tip = "Ở xa chỉ thấy một vệt tối dưới đất. Thấy vệt tối lướt về phía mình thì quay lại chém, nó hiện hình khi đến gần.",
      },
      new() {
        Type = EnemyType.Firecracker, Name = "Pháo yêu", Kind = "Yêu quái tự nổ", Color = Gfx.Hex("#c62f22"),
        Where = "Từ chương 7 · mọi canh giờ",
        Lore = "Bánh pháo đốt dở từ đêm giao thừa năm nào. Ngòi vẫn âm ỉ cháy, và nó chỉ muốn nổ một lần cho thật to.",
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
