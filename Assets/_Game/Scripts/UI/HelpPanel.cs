using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace BachYeu {
  /// <summary>
  /// Cách chơi: a short handbook in tabs (controls, a night, masks, Tranh Đăng, Hồn đăng), plus a button to run the
  /// first-match guide again. Opened from the title menu and from the pause panel.
  /// </summary>
  public class HelpPanel : MonoBehaviour {
    public static HelpPanel Current { get; private set; }

    static readonly string[] Tabs = { "Điều khiển", "Một đêm", "Mặt nạ", "Tranh Đăng", "Hồn đăng" };

    Action _onClose;
    Button[] _tabs;
    Text _page, _restartLabel;
    int _tab;

    public static void Open(Action onClose = null) {
      if (Current) return;
      SettingsPanel.EnsureEventSystem();
      var canvas = UiKit.Canvas("Help", 90);
      var p = canvas.gameObject.AddComponent<HelpPanel>();
      p._onClose = onClose;
      p.Build((RectTransform)canvas.transform);
      Current = p;
      p.Select(0);
      Sfx.Play(SoundId.UiOpen);
    }

    void OnDestroy() { if (Current == this) Current = null; }

    public void Close() {
      Sfx.Play(SoundId.UiBack);
      var cb = _onClose;
      Destroy(gameObject);
      cb?.Invoke();
    }

    void Build(RectTransform root) {
      var C = new Vector2(.5f, .5f); var TL = new Vector2(0, 1); var BR = new Vector2(1, 0); var BL = new Vector2(0, 0);
      var dim = UiKit.Rect(root, "Dim", C, C, Vector2.zero, Vector2.zero);
      UiKit.Stretch(dim);
      dim.gameObject.AddComponent<Image>().color = new Color(.15f, .15f, .14f, .45f);

      var card = UiKit.Rect(root, "Card", C, C, Vector2.zero, new Vector2(1240, 780));
      card.gameObject.AddComponent<Image>().color = UiKit.InkPaper;
      UiKit.InkImage(card, "Wash", "Wash", C, C, Vector2.zero, new Vector2(1420, 920), new Color(1, 1, 1, .9f));
      UiKit.LabelAt(card, "Cách chơi", 48, UiKit.InkBlack, TL, TL, new Vector2(48, -24), new Vector2(500, 64), TextAnchor.MiddleLeft, FontStyle.Bold);
      UiKit.InkImage(card, "Rule", "BrushBar", TL, TL, new Vector2(40, -184), new Vector2(1160, 8), UiKit.InkBlack);

      _tabs = new Button[Tabs.Length];
      for (int i = 0; i < Tabs.Length; i++) {
        int k = i;
        _tabs[i] = UiKit.InkButton(card, Tabs[i], TL, TL, new Vector2(48 + i * 232, -104), new Vector2(220, 64), false, () => { Sfx.Play(SoundId.UiClick); Select(k); }, 22);
        _tabs[i].name = "Tab_" + i;
      }

      _page = UiKit.LabelAt(card, "", 22, UiKit.InkBlack, TL, TL, new Vector2(56, -210), new Vector2(1130, 460), TextAnchor.UpperLeft);
      _page.lineSpacing = 1.15f;
      _page.supportRichText = true;

      var restart = UiKit.InkButton(card, "Học lại từ đầu", BL, BL, new Vector2(44, 24), new Vector2(260, 74), false, RestartGuide, 22);
      restart.name = "RestartGuide";
      _restartLabel = UiKit.LabelAt(card, "Bà Lồng sẽ hướng dẫn lại từng bước ở trận đêm tiếp theo.", 17, UiKit.InkGrey, BL, BL, new Vector2(320, 46), new Vector2(560, 30), TextAnchor.MiddleLeft);
      UiKit.InkButton(card, "Xong", BR, BR, new Vector2(-44, 24), new Vector2(220, 74), false, Close, 26).name = "Done";
    }

    void RestartGuide() {
      Tutorial.Restart();
      Sfx.Play(SoundId.Pickup);
      _restartLabel.text = "Đã bật lại hướng dẫn. Vào một trận đêm để bắt đầu.";
      _restartLabel.color = UiKit.Pine;
    }

    void Select(int i) {
      _tab = i;
      UiKit.SelectSegment(_tabs, i);
      _page.text = Page(i);
    }

    static string H(string s) => $"<b><color=#a8321f>{s}</color></b>";

    static string Page(int i) {
      var b = new StringBuilder();
      switch (i) {
        case 0:
          b.AppendLine(H("Trên điện thoại"));
          b.AppendLine("• Kéo vòng tròn bên trái để đi lại.");
          b.AppendLine("• Giữ nút Chém lớn để đánh liên tục. Hai nút nhỏ là hai kỹ năng của mặt nạ đang đeo.");
          b.AppendLine("• Né: lướt nhanh một đoạn. Đổi: thay sang mặt nạ thứ hai. Nút trên cùng là Tuyệt kỹ, bấm khi đã nạp đầy.");
          b.AppendLine("• Ba nút báo hiệu góc trên (Lại đây, Đứng đèn, Cẩn thận) để gọi đồng đội.");
          b.AppendLine();
          b.AppendLine(H("Trên bàn phím"));
          b.AppendLine("• W A S D hoặc mũi tên: đi lại · Giữ J: chém · K, L: kỹ năng");
          b.AppendLine("• Space: lướt né · Q: đổi mặt nạ · U: Tuyệt kỹ · 1, 2, 3: báo hiệu · Esc: tạm dừng");
          b.AppendLine();
          b.AppendLine(H("Mẹo"));
          b.Append("• Vòng đỏ dưới đất là đòn sắp giáng xuống. Lướt ra ngoài trước khi vòng đầy.");
          break;
        case 1:
          b.AppendLine("Mỗi đêm có 4 canh giờ. Qua hết là bình minh và bạn thắng.");
          b.AppendLine();
          b.AppendLine(H("Giờ Tuất · Phố chợ") + "  Đứng trong vòng vàng để thắp 3 lồng đèn.");
          b.AppendLine(H("Giờ Hợi · Bến sông") + $"  Hạ {Phases.Phase2Kills} yêu quái kéo đến từ mặt nước.");
          b.AppendLine(H("Giờ Tý · Sân đền") + "  Đánh bại Miêu yêu song vĩ.");
          b.AppendLine(H("Giờ Sửu · Cổng Hồng Đăng") + "  Đánh bại Hồng Đăng Quỷ Vương.");
          b.AppendLine();
          b.AppendLine("• Sau mỗi canh giờ, chọn một phúc lành để mạnh hơn cho tới hết đêm.");
          b.AppendLine("• Khi gục, đồng đội đứng cạnh bạn 3 giây để cứu. Cả đội cùng gục thì đêm đó thua.");
          b.AppendLine("• Thua thì chơi lại đúng đêm đó. Ở Hành trình, mỗi chương mới yêu quái lại khỏe hơn.");
          b.Append("• Yêu quái đôi khi rơi đồ hồi máu: đi qua để nhặt.");
          break;
        case 2:
          b.AppendLine("Mỗi trận, mỗi người chọn lại 2 mặt nạ (màn chọn tướng hiện trước khi vào trận). Đổi mặt nạ đúng lúc là chìa khoá:");
          b.AppendLine("lúc đổi sẽ tung một đòn riêng của mặt nạ mới.");
          b.AppendLine();
          foreach (var m in Masks.All) {
            b.AppendLine($"<b><color=#{ColorUtility.ToHtmlStringRGB(m.Color)}>{m.Name}</color></b> · {m.Element} · {m.Role}");
            b.AppendLine($"   {m.S1}: {m.S1Desc}. {m.S2}: {m.S2Desc}. Tuyệt kỹ {m.Ult}: {m.UltDesc}.");
          }
          break;
        case 3:
          b.AppendLine("Hai đội Lam và Chu thi xem đội nào giữ đèn giỏi hơn trong 6 phút, cần ít nhất 2 người.");
          b.AppendLine();
          b.AppendLine(H(Contest.ActTitle[0]) + "  " + Contest.ActSub[0] + ". Đèn Cầu ở giữa được gấp đôi điểm.");
          b.AppendLine(H(Contest.ActTitle[1]) + "  " + Contest.ActSub[1] + ".");
          b.AppendLine(H(Contest.ActTitle[2]) + "  " + Contest.ActSub[2] + ".");
          b.AppendLine();
          b.AppendLine("• Mưa mực trút xuống những lồng đèn có người đứng: nhìn vòng đen mà tránh.");
          b.AppendLine("• Từ phút thứ hai, Ma Trơi trôi tới cướp đèn. Hạ nó để được thêm điểm.");
          b.AppendLine("• Đội dẫn quá xa sẽ bị Lời nguyền Hồng Đăng: yêu quái và boss săn đội đó trước.");
          b.Append("• Giữ đủ cả 3 đèn trong 15 giây là thắng ngay.");
          break;
        case 4:
          b.AppendLine("Hồn đăng là tiền trong game. Kiếm được sau mỗi trận, kể cả khi thua.");
          b.AppendLine();
          b.AppendLine(H("Kiếm Hồn đăng"));
          b.AppendLine("• Một đêm: thưởng gốc, cộng theo số yêu quái hạ, số canh giờ qua, và thêm nhiều nếu thấy bình minh.");
          b.AppendLine("• Tranh Đăng: thưởng gốc, cộng theo số yêu quái hạ, thêm khi thắng hoặc hoà.");
          b.AppendLine("• Qua một chương Hành trình lần đầu: thưởng lớn.");
          b.AppendLine($"• Gặp một loài yêu quái mới, được ghi vào Bách yêu lục: +{Bestiary.FirstSightReward}.");
          b.AppendLine();
          b.AppendLine(H("Tiêu Hồn đăng"));
          b.Append("• Mở Túi pháp khí ở menu để mua và nâng cấp pháp khí. Mang tối đa 3 món vào trận.");
          break;
      }
      return b.ToString();
    }

    void Update() {
      var k = UnityEngine.InputSystem.Keyboard.current;
      if (k == null) return;
      if (k.escapeKey.wasPressedThisFrame) Close();
      else if (k.rightArrowKey.wasPressedThisFrame) Select((_tab + 1) % Tabs.Length);
      else if (k.leftArrowKey.wasPressedThisFrame) Select((_tab + Tabs.Length - 1) % Tabs.Length);
    }
  }
}
