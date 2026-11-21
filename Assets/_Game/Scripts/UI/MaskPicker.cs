using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BachYeu {
  /// <summary>
  /// Full-screen picker: one hanging scroll per mask, tap two of them, then Xong.
  /// The first one picked is worn at the start. Saved to PlayerPrefs and, in the lobby, sent to your avatar.
  /// Built from code (no prefab) so new masks appear automatically.
  /// </summary>
  public class MaskPicker : MonoBehaviour {
    public static MaskPicker Open(Action onDone) {
      var canvas = UiKit.Canvas("MaskPicker", 40);
      var p = canvas.gameObject.AddComponent<MaskPicker>();
      p._onDone = onDone;
      p.Build((RectTransform)canvas.transform);
      return p;
    }

    /// <summary>"Hồ Ly · Tengu" for the given pair.</summary>
    public static string PairName(int a, int b) => $"{Masks.All[a].Name} · {Masks.All[b].Name}";

    Action _onDone;
    readonly List<int> _picked = new();
    readonly List<(Image card, Image seal, Text sealText)> _cards = new();
    Text _status;
    Button _ok;

    void Build(RectTransform root) {
      var C = new Vector2(.5f, .5f); var TC = new Vector2(.5f, 1); var BC = new Vector2(.5f, 0);
      var ink = UiKit.InkBlack; var grey = UiKit.InkGrey;
      UiKit.Backdrop(root, "Backdrop", "BlessBg");
      UiKit.LabelAt(root, "Chọn 2 mặt nạ", 46, ink, TC, TC, new Vector2(0, -24), new Vector2(900, 56), TextAnchor.MiddleCenter, FontStyle.Bold);
      UiKit.LabelAt(root, "Mặt nạ chọn trước được đeo khi vào trận. Bấm Đổi để chuyển sang mặt nạ còn lại.", 20, grey, TC, TC, new Vector2(0, -80), new Vector2(1100, 30), TextAnchor.MiddleCenter);

      LocalProfile.LoadMasks();
      _picked.Add(LocalProfile.MaskA);
      _picked.Add(LocalProfile.MaskB);

      int n = Masks.All.Length;
      const float spacing = 336;
      for (int i = 0; i < n; i++) {
        int id = i;
        var def = Masks.All[i];
        var card = UiKit.InkImage(root, "Mask_" + def.Name, "Scroll", C, C, new Vector2((i - (n - 1) / 2f) * spacing, -30), new Vector2(316, 520), Color.white);
        card.type = Image.Type.Simple;
        card.raycastTarget = true;
        card.gameObject.AddComponent<Button>().onClick.AddListener(() => Toggle(id));
        var t = card.transform;
        // Header: role seal, name, element.
        var role = UiKit.SealTag(t, "Role", def.Role, TC, TC, new Vector2(0, -70), new Vector2(132, 34), def.Color, true, 17);
        UiKit.LabelAt(t, def.Name, 34, ink, TC, TC, new Vector2(0, -112), new Vector2(240, 44), TextAnchor.MiddleCenter, FontStyle.Bold);
        UiKit.LabelAt(t, "Hệ " + def.Element.ToLower(), 17, def.Color, TC, TC, new Vector2(0, -152), new Vector2(240, 24), TextAnchor.MiddleCenter, FontStyle.Bold);
        var body = UiKit.LabelAt(t,
          $"<b>Đánh:</b> {def.Attack}\n<b>{def.S1}:</b> {def.S1Desc}\n<b>{def.S2}:</b> {def.S2Desc}\n<b>{def.Ult}:</b> {def.UltDesc}\n<b>Khi đổi vào:</b> {def.SwapDesc}",
          15, grey, TC, TC, new Vector2(0, -186), new Vector2(232, 290), TextAnchor.UpperLeft);
        body.lineSpacing = 1.05f;
        body.supportRichText = true;
        // Pick order stamp (1 or 2) in the corner.
        var seal = UiKit.SealTag(t, "Pick", "1", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-34, -40), new Vector2(46, 46), UiKit.Seal, true, 24);
        _cards.Add((card, seal, seal.GetComponentInChildren<Text>()));
      }

      _status = UiKit.LabelAt(root, "", 22, ink, BC, BC, new Vector2(0, 126), new Vector2(900, 32), TextAnchor.MiddleCenter, FontStyle.Bold);
      _ok = UiKit.InkButton(root, "Xong", BC, BC, new Vector2(150, 26), new Vector2(320, 96), true, Confirm, 28);
      UiKit.InkButton(root, "Huỷ", BC, BC, new Vector2(-170, 34), new Vector2(240, 76), false, Close, 24);
      Refresh();
    }

    void Toggle(int id) {
      if (_picked.Contains(id)) _picked.Remove(id);
      else if (_picked.Count < 2) _picked.Add(id);
      else { _picked.RemoveAt(1); _picked.Add(id); } // already two: replace the second
      Refresh();
    }

    void Refresh() {
      for (int i = 0; i < _cards.Count; i++) {
        int order = _picked.IndexOf(i);
        var (card, seal, text) = _cards[i];
        seal.gameObject.SetActive(order >= 0);
        if (order >= 0) text.text = (order + 1).ToString();
        card.color = order >= 0 ? Color.white : new Color(1, 1, 1, .55f);
        card.transform.localScale = Vector3.one * (order >= 0 ? 1.03f : .97f);
      }
      bool ready = _picked.Count == 2;
      _status.text = ready ? $"Mang theo: {PairName(_picked[0], _picked[1])}" : $"Chọn thêm {2 - _picked.Count} mặt nạ";
      _ok.interactable = ready;
    }

    void Confirm() {
      if (_picked.Count != 2) return;
      LocalProfile.SaveMasks(_picked[0], _picked[1]);
      if (PlayerAvatar.Local) PlayerAvatar.Local.SetMasks(_picked[0], _picked[1]);
      Close();
    }

    void Close() {
      _onDone?.Invoke();
      Destroy(gameObject);
    }
  }
}
