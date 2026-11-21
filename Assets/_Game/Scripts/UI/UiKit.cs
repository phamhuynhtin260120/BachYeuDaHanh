using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BachYeu {
  /// <summary>Tiny helpers to build uGUI from code with the scroll-and-seal palette.</summary>
  public static class UiKit {
    public static readonly Color Paper = Gfx.Hex("#f7f1e3");
    public static readonly Color PaperGlass = new(0.97f, 0.945f, 0.89f, 0.88f);
    public static readonly Color Ink = Gfx.Hex("#2a221d");
    public static readonly Color Ink2 = Gfx.Hex("#5f5246");
    public static readonly Color Line = Gfx.Hex("#b9a888");
    public static readonly Color Seal = Gfx.Hex("#a8321f");
    public static readonly Color Gold = Gfx.Hex("#b5862e");
    public static readonly Color Fox = Gfx.Hex("#c8502a");
    public static readonly Color Wind = Gfx.Hex("#2f6d9c");
    public static readonly Color Jade = Gfx.Hex("#5f8f55");

    static Font _font;
    public static Font Font => _font ? _font : _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    public static void ResetCaches() { _round = null; _ink.Clear(); }

    /// <summary>When set (by the Editor prefab builder), Round returns this saved sprite asset instead of a runtime texture.</summary>
    public static Sprite RoundOverride;

    static Sprite _round;
    /// <summary>The generated circle texture, so the Editor builder can save it as a PNG.</summary>
    public static Texture2D RoundTexture { get; private set; }
    /// <summary>White circle that also works as a 9-sliced rounded rectangle.</summary>
    public static Sprite Round {
      get {
        if (RoundOverride) return RoundOverride;
        if (_round) return _round;
        const int s = 128;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[s * s];
        float r = s / 2f;
        for (int y = 0; y < s; y++)
          for (int x = 0; x < s; x++) {
            float d = Mathf.Sqrt((x + .5f - r) * (x + .5f - r) + (y + .5f - r) * (y + .5f - r));
            byte a = (byte)(Mathf.Clamp01(r - d) * 255);
            px[y * s + x] = new Color32(255, 255, 255, a);
          }
        tex.SetPixels32(px);
        tex.Apply();
        RoundTexture = tex;
        _round = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(60, 60, 60, 60));
        return _round;
      }
    }

    public static Canvas Canvas(string name, int order) {
      var go = new GameObject(name, typeof(RectTransform));
      var c = go.AddComponent<Canvas>();
      c.renderMode = RenderMode.ScreenSpaceOverlay;
      c.sortingOrder = order;
      var sc = go.AddComponent<CanvasScaler>();
      sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      sc.referenceResolution = new Vector2(1600, 900);
      sc.matchWidthOrHeight = .6f;
      go.AddComponent<GraphicRaycaster>();
      return c;
    }

    public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size) {
      var go = new GameObject(name, typeof(RectTransform));
      var rt = (RectTransform)go.transform;
      rt.SetParent(parent, false);
      rt.anchorMin = rt.anchorMax = anchor;
      rt.pivot = pivot;
      rt.anchoredPosition = pos;
      rt.sizeDelta = size;
      return rt;
    }

    public static void Stretch(RectTransform rt, float inset = 0) {
      rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
      rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
    }

    public static Image Panel(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color, bool rounded = true) {
      var rt = Rect(parent, name, anchor, pivot, pos, size);
      var img = rt.gameObject.AddComponent<Image>();
      img.color = color;
      if (rounded) { img.sprite = Round; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 6f; }
      return img;
    }

    public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal) {
      var go = new GameObject("Label", typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var t = go.AddComponent<Text>();
      t.font = Font;
      t.text = text;
      t.fontSize = size;
      t.color = color;
      t.alignment = align;
      t.fontStyle = style;
      t.horizontalOverflow = HorizontalWrapMode.Wrap;
      t.verticalOverflow = VerticalWrapMode.Overflow;
      t.raycastTarget = false;
      Stretch((RectTransform)go.transform);
      return t;
    }

    public static Text LabelAt(Transform parent, string text, int size, Color color, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 boxSize, TextAnchor align, FontStyle style = FontStyle.Normal) {
      var rt = Rect(parent, "Label", anchor, pivot, pos, boxSize);
      var t = Label(rt, text, size, color, align, style);
      return t;
    }

    public static Button Button(Transform parent, string text, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color bg, Color fg, UnityAction onClick, int fontSize = 24) {
      var img = Panel(parent, "Button", anchor, pivot, pos, size, bg);
      var b = img.gameObject.AddComponent<Button>();
      var colors = b.colors;
      colors.highlightedColor = new Color(1, 1, 1, .92f);
      colors.pressedColor = new Color(.85f, .85f, .85f, 1);
      b.colors = colors;
      if (onClick != null) b.onClick.AddListener(onClick);
      Label(img.transform, text, fontSize, fg, TextAnchor.MiddleCenter, FontStyle.Bold);
      return b;
    }

    /// <summary>Horizontal bar; returns the fill RectTransform (set anchorMax.x for 0..1).</summary>
    public static RectTransform Bar(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color fill, Color? back = null) {
      var bg = Panel(parent, "Bar", anchor, pivot, pos, size, back ?? new Color(.16f, .13f, .11f, .16f));
      var f = Panel(bg.transform, "Fill", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, fill);
      var rt = f.rectTransform;
      rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
      rt.offsetMin = rt.offsetMax = Vector2.zero;
      return rt;
    }

    public static void SetFill(RectTransform fill, float k) {
      var a = fill.anchorMax; a.x = Mathf.Clamp01(k); fill.anchorMax = a;
    }

    public static InputField Input(Transform parent, string value, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size) {
      var img = Panel(parent, "Input", anchor, pivot, pos, size, Color.white);
      var field = img.gameObject.AddComponent<InputField>();
      var txt = Label(img.transform, value, 30, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
      txt.supportRichText = false;
      txt.raycastTarget = true;
      field.textComponent = txt;
      field.text = value;
      field.characterLimit = 12;
      return field;
    }

    public static void Each(Action<int> f, int n) { for (int i = 0; i < n; i++) f(i); }

    // ---------------- ink-wash (thủy mặc) skin ----------------
    public static readonly Color InkBlack = Gfx.Hex("#26292a");
    public static readonly Color InkGrey = Gfx.Hex("#4b5352");
    public static readonly Color InkPaper = Gfx.Hex("#f2efe7");
    public static readonly Color Pine = Gfx.Hex("#3e6b62");
    public static readonly Color Clear = new(1, 1, 1, 0);

    /// <summary>Folder under Resources holding the ink sprites (Brush, BrushBar, Enso, InkDot, Seal, SealLine, Lines, Wash, Scroll, MenuBg, BlessBg, ResultBg, Lantern).</summary>
    public const string InkDir = "UI/Ink/";
    static readonly System.Collections.Generic.Dictionary<string, Sprite> _ink = new();

    public static Sprite InkSprite(string name) {
      if (_ink.TryGetValue(name, out var s) && s) return s;
      s = Resources.Load<Sprite>(InkDir + name);
      if (!s) Debug.LogWarning($"[UiKit] Thiếu sprite thủy mặc Resources/{InkDir}{name}.png");
      _ink[name] = s;
      return s;
    }

    public static void ResetInk() => _ink.Clear();

    /// <summary>Image with an ink sprite. Sliced when the sprite has borders.</summary>
    public static Image InkImage(Transform parent, string name, string sprite, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color) {
      var rt = Rect(parent, name, anchor, pivot, pos, size);
      var img = rt.gameObject.AddComponent<Image>();
      img.sprite = InkSprite(sprite);
      img.color = color;
      img.raycastTarget = false;
      if (img.sprite && img.sprite.border != Vector4.zero) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 2f; }
      return img;
    }

    /// <summary>Full-screen picture that covers the screen without stretching.</summary>
    public static Image Backdrop(Transform parent, string name, string sprite) {
      var holder = Rect(parent, name, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
      Stretch(holder);
      var mask = holder.gameObject.AddComponent<RectMask2D>();
      mask.enabled = true;
      var blocker = holder.gameObject.AddComponent<Image>();
      blocker.color = InkPaper; // also blocks clicks to the game behind
      var img = InkImage(holder, "Picture", sprite, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1688, 780), Color.white);
      var fit = img.gameObject.AddComponent<AspectRatioFitter>();
      fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
      fit.aspectRatio = 1688f / 780f;
      return blocker;
    }

    /// <summary>Primary = dry brush stroke with light text; secondary = two ink lines with dark text.</summary>
    public static Button InkButton(Transform parent, string text, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, bool primary, UnityAction onClick = null, int fontSize = 26) {
      var img = InkImage(parent, "Button", primary ? "Brush" : "Lines", anchor, pivot, pos, size, Color.white);
      img.raycastTarget = true;
      var b = img.gameObject.AddComponent<Button>();
      var colors = b.colors;
      colors.highlightedColor = new Color(.92f, .92f, .92f, 1);
      colors.pressedColor = new Color(.75f, .75f, .75f, 1);
      colors.disabledColor = new Color(.7f, .7f, .7f, .6f);
      b.colors = colors;
      if (onClick != null) b.onClick.AddListener(onClick);
      var label = Label(img.transform, text, fontSize, primary ? InkPaper : InkBlack, TextAnchor.MiddleCenter, FontStyle.Bold);
      if (primary) ((RectTransform)label.transform).offsetMax = new Vector2(-size.x * .06f, 0);
      return b;
    }

    /// <summary>Square stamp tag: solid (colored, light text) or outline (colored text).</summary>
    public static Image SealTag(Transform parent, string name, string text, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color, bool solid = true, int fontSize = 18) {
      var img = InkImage(parent, name, solid ? "Seal" : "SealLine", anchor, pivot, pos, size, color);
      Label(img.transform, text, fontSize, solid ? InkPaper : color, TextAnchor.MiddleCenter, FontStyle.Bold);
      return img;
    }

    /// <summary>Brush-stroke bar; returns the fill RectTransform (set anchorMax.x for 0..1).</summary>
    public static RectTransform InkBar(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color fill, Color? back = null) {
      var bg = InkImage(parent, "Bar", "BrushBar", anchor, pivot, pos, size, back ?? new Color(.15f, .16f, .16f, .28f));
      var f = InkImage(bg.transform, "Fill", "BrushBar", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, fill);
      var rt = f.rectTransform;
      rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
      rt.offsetMin = rt.offsetMax = Vector2.zero;
      return rt;
    }

    /// <summary>Text field drawn as a single ink underline.</summary>
    public static InputField InkInput(Transform parent, string value, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, int fontSize = 32) {
      var rt = Rect(parent, "Input", anchor, pivot, pos, size);
      var hit = rt.gameObject.AddComponent<Image>();
      hit.color = Clear;
      InkImage(rt, "Underline", "BrushBar", new Vector2(.5f, 0), new Vector2(.5f, 0), Vector2.zero, new Vector2(size.x, 10), InkBlack);
      var field = rt.gameObject.AddComponent<InputField>();
      var txt = Label(rt, value, fontSize, InkBlack, TextAnchor.MiddleLeft, FontStyle.Bold);
      ((RectTransform)txt.transform).offsetMin = new Vector2(6, 8);
      txt.supportRichText = false;
      txt.raycastTarget = true;
      field.textComponent = txt;
      field.targetGraphic = hit;
      field.text = value;
      field.characterLimit = 12;
      return field;
    }

    /// <summary>Round season / toggle button: brush ring, ink dot fill when selected (dot is the targetGraphic).</summary>
    public static Button InkToggle(Transform parent, string text, Vector2 anchor, Vector2 pivot, Vector2 pos, float size, int fontSize = 20) {
      var dot = InkImage(parent, "Toggle", "InkDot", anchor, pivot, pos, new Vector2(size, size), Clear);
      dot.raycastTarget = true;
      InkImage(dot.transform, "Ring", "Enso", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(size, size), InkGrey);
      var b = dot.gameObject.AddComponent<Button>();
      b.transition = Selectable.Transition.None;
      Label(dot.transform, text, fontSize, InkBlack, TextAnchor.MiddleCenter, FontStyle.Bold);
      return b;
    }

    /// <summary>Row of InkButtons used as a segmented choice: the selected one becomes a dry brush stroke with light text.</summary>
    public static void SelectSegment(Button[] row, int selected) {
      for (int i = 0; i < row.Length; i++) {
        var b = row[i];
        if (!b) continue;
        bool on = i == selected;
        if (b.targetGraphic is Image img) {
          img.sprite = InkSprite(on ? "Brush" : "Lines");
          img.color = on ? Color.white : new Color(1, 1, 1, .7f);
        }
        var label = b.GetComponentInChildren<Text>();
        if (label) label.color = on ? InkPaper : InkGrey;
      }
    }

    /// <summary>Highlights one InkToggle in a row (selected = ink dot with light text).</summary>
    public static void SelectToggle(Button[] row, int selected) {
      for (int i = 0; i < row.Length; i++) {
        var b = row[i];
        if (!b) continue;
        bool on = i == selected;
        if (b.targetGraphic) b.targetGraphic.color = on ? InkBlack : Clear;
        var label = b.GetComponentInChildren<Text>();
        if (label) label.color = on ? InkPaper : InkBlack;
      }
    }
  }
}
