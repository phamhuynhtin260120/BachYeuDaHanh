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

    // Baloo 2 (OFL, Resources/UI/Fonts): Medium for running text, ExtraBold for anything asked to be bold,
    // so bold text uses real heavy letters instead of Unity's smeared fake bold. Falls back to the built-in font.
    static Font _font, _fontBold;
    public static Font Font => _font ? _font : _font = Resources.Load<Font>("UI/Fonts/Baloo2-Medium") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    public static Font FontBold => _fontBold ? _fontBold : _fontBold = Resources.Load<Font>("UI/Fonts/Baloo2-ExtraBold") ?? Font;

    /// <summary>Give a Text the game font: bold styles move to the ExtraBold face.</summary>
    public static void ApplyFont(Text t) {
      if (!t) return;
      bool bold = t.fontStyle == FontStyle.Bold || t.fontStyle == FontStyle.BoldAndItalic;
      t.font = bold ? FontBold : Font;
      if (bold && FontBold != Font) t.fontStyle = t.fontStyle == FontStyle.BoldAndItalic ? FontStyle.Italic : FontStyle.Normal;
    }

    public static void ResetCaches() { _round = null; _ink.Clear(); _font = null; _fontBold = null; }

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
      ApplyFont(t);
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
    static readonly Color InkPaperWash = Gfx.Hex("#f2efe7"), InkPaperAged = Gfx.Hex("#efe2c4");
    /// <summary>Card / paper color: cool rice paper for the ink-wash skin, aged warm paper for Cổ phong.</summary>
    public static Color InkPaper => GameSettings.Skin switch { GameSettings.SkinCoPhong => InkPaperAged, GameSettings.SkinTienCanh => InkPaperJade, _ => InkPaperWash };
    static readonly Color InkPaperJade = Gfx.Hex("#eef3e6");
    public static readonly Color Pine = Gfx.Hex("#3e6b62");
    public static readonly Color Clear = new(1, 1, 1, 0);

    /// <summary>Folder under Resources holding the ink sprites (Brush, BrushBar, Enso, InkDot, Seal, SealLine, Lines, Wash, Scroll, MenuBg, BlessBg, ResultBg, Lantern).</summary>
    public const string InkDir = "UI/Ink/";
    /// <summary>Cổ phong skin: same file names as UI/Ink; a sprite missing here falls back to the ink one.</summary>
    public const string CoPhongDir = "UI/CoPhong/";
    /// <summary>Tiên cảnh skin (jade fairyland): same file names again.</summary>
    public const string TienCanhDir = "UI/TienCanh/";
    static string SkinDir => GameSettings.Skin switch { GameSettings.SkinCoPhong => CoPhongDir, GameSettings.SkinTienCanh => TienCanhDir, _ => null };
    static readonly System.Collections.Generic.Dictionary<string, Sprite> _ink = new();

    public static Sprite InkSprite(string name) {
      if (_ink.TryGetValue(name, out var s) && s) return s;
      if (SkinDir != null) s = Resources.Load<Sprite>(SkinDir + name);
      if (!s) s = Resources.Load<Sprite>(InkDir + name);
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
      if (CoPhong && sprite == "Wash") {
        // The ink wash is drawn larger than its card for a soft edge; the Cổ phong frame must sit on the card itself.
        if (name == "Wash" && parent.GetComponent<Image>()) {
          rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
          rt.offsetMin = new Vector2(-10, -10); rt.offsetMax = new Vector2(10, 10);
          img.color = Color.white;
        } else if (color.a < .8f) { // light washes behind HUD text: a small paper card instead of the heavy frame
          img.sprite = InkSprite("WashLight");
          rt.sizeDelta = size * .86f;
          img.color = new Color(1, 1, 1, Mathf.Max(color.a, .82f));
        }
      }
      if (img.sprite && img.sprite.border != Vector4.zero) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 2f; }
      return img;
    }

    /// <summary>True for the framed skins (Cổ phong, Tiên cảnh): panels get real frames instead of soft ink washes.</summary>
    public static bool CoPhong => GameSettings.Skin != GameSettings.SkinInk;
    public static bool TienCanh => GameSettings.Skin == GameSettings.SkinTienCanh;

    /// <summary>Colors the hub and stage text use under the framed skins.</summary>
    public struct ThemeColors { public Color Gold, Rim, Deep, TileIcon, Cream, Glow, StageText, Edge, Pill; }
    static readonly ThemeColors RedTheme = new() {
      Gold = Gfx.Hex("#f0c96a"), Rim = Gfx.Hex("#d4a84f"), Deep = Gfx.Hex("#5a120c"), TileIcon = Gfx.Hex("#8c2418"),
      Cream = Gfx.Hex("#fbe9c2"), Glow = Gfx.Hex("#f6d58c"), StageText = Gfx.Hex("#f6ead0"), Edge = new Color(.16f, .08f, .04f, .95f), Pill = new Color(.17f, .09f, .05f, .9f),
    };
    static readonly ThemeColors JadeTheme = new() {
      Gold = Gfx.Hex("#f6e7ae"), Rim = Gfx.Hex("#e4d08c"), Deep = Gfx.Hex("#0f4e47"), TileIcon = Gfx.Hex("#f6e7ae"),
      Cream = Gfx.Hex("#effaf4"), Glow = Gfx.Hex("#fff3c4"), StageText = Gfx.Hex("#f7fcf8"), Edge = new Color(.02f, .16f, .15f, .9f), Pill = new Color(.03f, .2f, .19f, .7f),
    };
    public static ThemeColors Theme => TienCanh ? JadeTheme : RedTheme;

    /// <summary>Game icon from Resources/UI/Icons (white silhouettes from game-icons.net, CC BY 3.0: tint with the Image color).</summary>
    public static Sprite Icon(string name) {
      if (_ink.TryGetValue("icon:" + name, out var s) && s) return s;
      s = Resources.Load<Sprite>("UI/Icons/" + name);
      _ink["icon:" + name] = s;
      return s;
    }

    public static Image IconImage(Transform parent, string icon, Vector2 anchor, Vector2 pivot, Vector2 pos, float size, Color color) {
      var rt = Rect(parent, "Icon_" + icon, anchor, pivot, pos, new Vector2(size, size));
      var img = rt.gameObject.AddComponent<Image>();
      img.sprite = Icon(icon);
      img.color = color;
      img.preserveAspect = true;
      img.raycastTarget = false;
      return img;
    }

    /// <summary>Text drawn on the hub stage: light letters with a dark edge under Cổ phong, ink on paper otherwise.</summary>
    public static Text StageLabel(Transform parent, string text, int size, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 box, TextAnchor align, bool bold = true) {
      var t = LabelAt(parent, text, size, CoPhong ? Theme.StageText : InkBlack, anchor, pivot, pos, box, align, bold ? FontStyle.Bold : FontStyle.Normal);
      var o = t.gameObject.AddComponent<Outline>();
      o.effectColor = CoPhong ? Theme.Edge : new Color(.97f, .96f, .94f, .8f);
      o.effectDistance = new Vector2(2, -2);
      return t;
    }

    /// <summary>
    /// UI that was built ahead of time and saved in a scene (the match HUD, character select) keeps the sprites it was
    /// saved with. Under Cổ phong, swap every ink sprite that has a Cổ phong twin, with the same rules as InkImage.
    /// </summary>
    public static void Reskin(Transform root) {
      GameSettings.Load(); // scene UI can wake before the launcher has loaded the options
      if (!root) return;
      foreach (var t in root.GetComponentsInChildren<Text>(true)) ApplyFont(t);
      if (!CoPhong) return;
      foreach (var img in root.GetComponentsInChildren<Image>(true)) {
        if (!img.sprite) continue;
        string n = img.sprite.name;
        if (img.sprite.texture && img.sprite.texture.name != n) continue; // atlas pieces: not ours
        var twin = Resources.Load<Sprite>(SkinDir + n);
        if (!twin || twin == img.sprite) continue;
        var rt = img.rectTransform;
        if (n == "Wash") {
          if (img.name == "Wash" && rt.parent && rt.parent.GetComponent<Image>()) {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-10, -10); rt.offsetMax = new Vector2(10, 10);
            img.color = Color.white;
          } else if (img.color.a < .8f) {
            twin = InkSprite("WashLight");
            rt.sizeDelta *= .86f;
            img.color = new Color(1, 1, 1, Mathf.Max(img.color.a, .82f));
          }
        }
        img.sprite = twin;
        if (twin.border != Vector4.zero) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 2f; }
        else if (img.type == Image.Type.Sliced) img.type = Image.Type.Simple;
      }
    }

    /// <summary>Cổ phong only: dress a plain colored panel as a paper card (or a red lacquer card when primary).</summary>
    public static void SkinPanel(Image img, bool primary = false) {
      if (!CoPhong || !img) return;
      img.sprite = InkSprite(primary ? "CardLacquer" : "WashLight");
      img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 2f;
      img.color = Color.white;
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
