using UnityEngine;
using UnityEngine.UI;

namespace BachYeu {
  /// <summary>
  /// Title menu UI. Lives in Assets/_Game/Prefabs/UI/Menu.prefab: edit layout, sprites and fonts there.
  /// GameLauncher wires the buttons; this class only holds references.
  /// </summary>
  public class MenuView : MonoBehaviour {
    public InputField RoomCode;
    public Text Status;
    public Button SoloButton;
    public Button OnlineButton;
    [Tooltip("Optional season buttons in order: Xuân, Hạ, Thu, Đông.")]
    public Button[] SeasonButtons = new Button[0];

    public static MenuView Create(MenuView prefab) {
      if (prefab) { var m = Instantiate(prefab); m.name = "Menu"; return m; }
      var canvas = UiKit.Canvas("Menu", 20);
      var view = canvas.gameObject.AddComponent<MenuView>();
      view.BuildDefault((RectTransform)canvas.transform);
      return view;
    }

    /// <summary>Default layout. Also used by the Editor builder to generate Menu.prefab.</summary>
    public void BuildDefault(RectTransform root) {
      var C = new Vector2(.5f, .5f); var TC = new Vector2(.5f, 1);
      var dim = UiKit.Panel(root, "Dim", C, C, Vector2.zero, Vector2.zero, new Color(.93f, .89f, .83f, .78f), false);
      UiKit.Stretch(dim.rectTransform);
      var card = UiKit.Panel(root, "Card", C, C, Vector2.zero, new Vector2(760, 600), UiKit.Paper);
      var t = card.transform;

      var seal = UiKit.Panel(t, "Seal", TC, TC, new Vector2(0, -28), new Vector2(110, 70), UiKit.Seal);
      UiKit.Label(seal.transform, "Đêm Hội", 22, UiKit.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
      UiKit.LabelAt(t, "Bách Yêu Dạ Hành", 52, UiKit.Ink, TC, TC, new Vector2(0, -108), new Vector2(700, 64), TextAnchor.MiddleCenter, FontStyle.Bold).transform.parent.name = "Title";
      UiKit.LabelAt(t, "Đêm lễ hội Hồng Đăng không chịu kết thúc. Đeo mặt nạ linh thú, cùng đồng đội săn yêu quái qua bốn canh giờ và đưa bình minh trở về.", 19, UiKit.Ink2, TC, TC, new Vector2(0, -178), new Vector2(640, 60), TextAnchor.MiddleCenter).transform.parent.name = "Tagline";

      UiKit.LabelAt(t, "MÙA", 15, UiKit.Ink2, TC, TC, new Vector2(-250, -262), new Vector2(120, 24), TextAnchor.MiddleLeft, FontStyle.Bold);
      SeasonButtons = new Button[4];
      for (int i = 0; i < 4; i++) {
        SeasonButtons[i] = UiKit.Button(t, ArenaBuilder.Seasons[i].Name, TC, TC, new Vector2(-150 + i * 110, -262), new Vector2(100, 40), UiKit.PaperGlass, UiKit.Ink, null, 18);
        SeasonButtons[i].name = "Season_" + ArenaBuilder.Seasons[i].Name;
      }

      UiKit.LabelAt(t, "MÃ PHÒNG", 15, UiKit.Ink2, TC, TC, new Vector2(-250, -330), new Vector2(160, 24), TextAnchor.MiddleLeft, FontStyle.Bold);
      RoomCode = UiKit.Input(t, "1234", TC, TC, new Vector2(40, -330), new Vector2(300, 56));
      RoomCode.name = "RoomCode";

      SoloButton = UiKit.Button(t, "Chơi một mình", TC, TC, new Vector2(-150, -430), new Vector2(270, 66), UiKit.PaperGlass, UiKit.Ink, null, 24);
      SoloButton.name = "SoloButton";
      OnlineButton = UiKit.Button(t, "Tạo / vào phòng", TC, TC, new Vector2(150, -430), new Vector2(270, 66), UiKit.Seal, UiKit.Paper, null, 24);
      OnlineButton.name = "OnlineButton";
      Status = UiKit.LabelAt(t, "Bạn bè nhập cùng mã phòng để chơi chung (tối đa 4 người).\nBàn phím: WASD di chuyển · J đánh · K, L skill · Q đổi mặt nạ · U tuyệt kỹ · Space lướt", 16, UiKit.Ink2, TC, TC, new Vector2(0, -520), new Vector2(700, 60), TextAnchor.MiddleCenter);
      Status.transform.parent.name = "Status";
    }
  }
}
