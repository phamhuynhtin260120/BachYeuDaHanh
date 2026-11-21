using UnityEngine;
using UnityEngine.UI;

namespace BachYeu {
  /// <summary>
  /// Title menu UI. Lives in Assets/_Game/Prefabs/UI/Menu.prefab: edit layout, sprites and fonts there.
  /// GameLauncher wires the buttons; this class only holds references.
  /// </summary>
  public class MenuView : MonoBehaviour {
    public InputField RoomCode;
    public InputField PlayerName;
    public Text Status;
    public Button SoloButton;
    public Button OnlineButton;
    [Tooltip("Optional season buttons in order: Xuân, Hạ, Thu, Đông.")]
    public Button[] SeasonButtons = new Button[0];
    [Tooltip("Optional mode buttons in order: Hành trình, Tranh Đăng.")]
    public Button[] ModeButtons = new Button[0];
    [Tooltip("Opens the mask picker. Its label shows the two masks you carry.")]
    public Button MaskButton;
    public Text MaskButtonText;

    public static MenuView Create(MenuView prefab) {
      if (prefab) { var m = Instantiate(prefab); m.name = "Menu"; return m; }
      var canvas = UiKit.Canvas("Menu", 20);
      var view = canvas.gameObject.AddComponent<MenuView>();
      view.BuildDefault((RectTransform)canvas.transform);
      return view;
    }

    /// <summary>Default layout. Also used by the Editor builder to generate Menu.prefab.</summary>
    public void BuildDefault(RectTransform root) {
      // Thủy mặc layout: landscape backdrop, title on the left, an underline form on the right.
      var L = new Vector2(0, .5f); var R = new Vector2(1, .5f); var TL = new Vector2(0, 1); var BL = Vector2.zero;
      UiKit.Backdrop(root, "Backdrop", "MenuBg");

      var title = UiKit.LabelAt(root, "Bách Yêu\nDạ Hành", 118, UiKit.InkBlack, L, L, new Vector2(84, 50), new Vector2(640, 280), TextAnchor.MiddleLeft, FontStyle.Bold);
      title.lineSpacing = .85f;
      title.transform.parent.name = "Title";
      title.gameObject.AddComponent<Outline>().effectColor = new Color(.97f, .96f, .94f, .8f);
      UiKit.SealTag(root, "TitleSeal", "Dạ", L, L, new Vector2(640, 118), new Vector2(64, 64), UiKit.Seal, true, 30);
      UiKit.InkImage(root, "TaglineRule", "BrushBar", L, L, new Vector2(88, -112), new Vector2(46, 8), UiKit.Seal);
      UiKit.LabelAt(root, "Đêm hội Hồng Đăng · 1 đến 4 người", 24, UiKit.InkGrey, L, L, new Vector2(146, -112), new Vector2(560, 34), TextAnchor.MiddleLeft).transform.parent.name = "Tagline";
      MaskButton = UiKit.InkButton(root, "Mặt nạ: Hồ Ly · Tengu", L, L, new Vector2(80, -196), new Vector2(470, 76), false, null, 24);
      MaskButton.name = "MaskButton";
      MaskButtonText = MaskButton.GetComponentInChildren<Text>();

      var form = UiKit.Rect(root, "Form", R, R, new Vector2(-96, 20), new Vector2(500, 700));
      UiKit.InkImage(form, "Wash", "Wash", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(620, 780), new Color(1, 1, 1, .85f));
      UiKit.LabelAt(form, "Tên của bạn", 22, UiKit.InkGrey, TL, TL, new Vector2(0, 0), new Vector2(500, 30), TextAnchor.MiddleLeft, FontStyle.Bold);
      PlayerName = UiKit.InkInput(form, "", TL, TL, new Vector2(0, -32), new Vector2(500, 70), 34);
      PlayerName.characterLimit = 16;
      PlayerName.name = "PlayerName";
      UiKit.LabelAt(form, "Mã phòng", 22, UiKit.InkGrey, TL, TL, new Vector2(0, -126), new Vector2(500, 30), TextAnchor.MiddleLeft, FontStyle.Bold);
      RoomCode = UiKit.InkInput(form, "1234", TL, TL, new Vector2(0, -158), new Vector2(500, 70), 38);
      RoomCode.name = "RoomCode";

      SeasonButtons = new Button[4];
      for (int i = 0; i < 4; i++) {
        SeasonButtons[i] = UiKit.InkToggle(form, ArenaBuilder.Seasons[i].Name, TL, TL, new Vector2(i * 136, -262), 92, 20);
        SeasonButtons[i].name = "Season_" + ArenaBuilder.Seasons[i].Name;
      }
      UiKit.SelectToggle(SeasonButtons, 0);

      UiKit.LabelAt(form, "Chế độ", 22, UiKit.InkGrey, TL, TL, new Vector2(0, -366), new Vector2(500, 30), TextAnchor.MiddleLeft, FontStyle.Bold);
      ModeButtons = new Button[2];
      for (int i = 0; i < 2; i++) {
        ModeButtons[i] = UiKit.InkButton(form, Modes.Name[i], TL, TL, new Vector2(i * 255, -398), new Vector2(245, 58), false, null, 22);
        ModeButtons[i].name = "Mode_" + i;
      }

      OnlineButton = UiKit.InkButton(form, "Tạo / vào phòng", TL, TL, new Vector2(-10, -476), new Vector2(520, 112), true, null, 30);
      OnlineButton.name = "OnlineButton";
      SoloButton = UiKit.InkButton(form, "Chơi một mình", TL, TL, new Vector2(0, -600), new Vector2(500, 80), false, null, 26);
      SoloButton.name = "SoloButton";

      Status = UiKit.LabelAt(root, "Bạn bè nhập cùng mã phòng để chơi chung (tối đa 4 người).\nBàn phím: WASD di chuyển · J đánh · K, L skill · Q đổi mặt nạ · U tuyệt kỹ · Space lướt · 1, 2, 3 ping", 17, UiKit.InkGrey, BL, BL, new Vector2(84, 22), new Vector2(900, 56), TextAnchor.LowerLeft);
      Status.transform.parent.name = "Status";
    }
  }
}
