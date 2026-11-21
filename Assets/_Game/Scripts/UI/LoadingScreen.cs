using UnityEngine;
using UnityEngine.UI;

namespace BachYeu {
  /// <summary>
  /// Loading curtain between the menu and a match: ink landscape, a turning brush circle, a status line with
  /// animated dots, and one tip. Shown while connecting or starting; lifts with a short fade.
  /// </summary>
  public class LoadingScreen : MonoBehaviour {
    static LoadingScreen _i;

    static readonly string[] Tips = {
      "Vòng đỏ dưới đất là đòn sắp giáng xuống. Lướt ra ngoài trước khi vòng đầy.",
      "Đổi mặt nạ đúng lúc sẽ tung thêm một đòn riêng của mặt nạ mới.",
      "Đứng cạnh đồng đội đã gục 3 giây để kéo họ dậy.",
      "Thắp lồng đèn nhanh hơn khi cả đội cùng đứng trong vòng vàng.",
      "Ở Tranh Đăng, Đèn Cầu giữa sân được gấp đôi điểm.",
      "Hà Bá hồi máu và tạo khiên cho cả đội. Đi cùng Quỷ Đá để giữ trận.",
      "Ma Trơi không đánh ai, nhưng nó cướp đèn. Hạ nó trước.",
      "Gặp loài yêu quái mới, Bách yêu lục thưởng thêm Hồn đăng.",
      "Pháp khí mang vào trận mạnh hơn khi nâng cấp ở Túi pháp khí.",
      "Nhiệm vụ hằng ngày đổi lúc nửa đêm. Nhớ nhận thưởng trước khi đi ngủ.",
      "Đêm sương dày: đi sát đồng đội, yêu quái hiện ra rất gần mới thấy.",
    };

    Text _status;
    RectTransform _spinner;
    CanvasGroup _group;
    string _base;
    float _fadeAt = -1, _appear = 1;

    public static void Show(string status) {
      if (!_i) {
        var canvas = UiKit.Canvas("Loading", 95);
        _i = canvas.gameObject.AddComponent<LoadingScreen>();
        _i.Build((RectTransform)canvas.transform);
        DontDestroyOnLoad(canvas.gameObject);
        _i._group.alpha = 0; _i._appear = 0; // fades in over the menu
      }
      _i._base = status;
      _i._fadeAt = -1;
      _i._appear = Mathf.Min(_i._appear, _i._group.alpha); // shown again while fading out: fade back in
      _i._group.blocksRaycasts = true;
    }

    /// <summary>Lift the curtain after <paramref name="delay"/> seconds.</summary>
    public static void Hide(float delay = 0) {
      if (!_i) return;
      _i._fadeAt = Time.unscaledTime + delay;
    }

    void Build(RectTransform root) {
      var C = new Vector2(.5f, .5f); var BC = new Vector2(.5f, 0);
      _group = gameObject.AddComponent<CanvasGroup>();
      UiKit.Backdrop(root, "Backdrop", "MenuBg");
      var wash = UiKit.Rect(root, "Wash", C, C, Vector2.zero, Vector2.zero);
      UiKit.Stretch(wash);
      wash.gameObject.AddComponent<Image>().color = new Color(.96f, .95f, .92f, .55f);
      var title = UiKit.LabelAt(root, "Bách Yêu Dạ Hành", 64, UiKit.InkBlack, C, C, new Vector2(0, 120), new Vector2(1000, 90), TextAnchor.MiddleCenter, FontStyle.Bold);
      title.gameObject.AddComponent<Outline>().effectColor = new Color(.97f, .96f, .94f, .8f);
      _spinner = UiKit.InkImage(root, "Spinner", "Enso", C, C, new Vector2(0, -10), new Vector2(120, 120), UiKit.Seal).rectTransform;
      _status = UiKit.LabelAt(root, "", 28, UiKit.InkBlack, C, C, new Vector2(0, -110), new Vector2(1000, 44), TextAnchor.MiddleCenter, FontStyle.Bold);
      var tipBox = UiKit.Rect(root, "Tip", BC, BC, new Vector2(0, 70), new Vector2(1100, 80));
      tipBox.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, .7f);
      UiKit.SealTag(tipBox, "Seal", "Mẹo", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(16, 0), new Vector2(80, 44), UiKit.Seal, true, 20);
      UiKit.LabelAt(tipBox, Tips[Random.Range(0, Tips.Length)], 22, UiKit.InkBlack, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(112, 0), new Vector2(970, 70), TextAnchor.MiddleLeft, FontStyle.Italic);
    }

    void Update() {
      _spinner.localRotation = Quaternion.Euler(0, 0, -Time.unscaledTime * 160f);
      int dots = (int)(Time.unscaledTime * 3) % 4;
      _status.text = _base + new string('.', dots);
      if (_appear < 1 && _fadeAt < 0) { _appear = Mathf.Min(1, _appear + Time.unscaledDeltaTime / .25f); _group.alpha = _appear; }
      if (_fadeAt >= 0 && Time.unscaledTime >= _fadeAt) {
        _group.alpha -= Time.unscaledDeltaTime * 3f;
        _group.blocksRaycasts = false;
        if (_group.alpha <= 0) { Destroy(gameObject); _i = null; }
      }
    }
  }
}
