using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BachYeu {
  /// <summary>
  /// Turns an info panel (Cài đặt, Nhiệm vụ, Bách yêu lục…) into dark frosted glass with light text, so the living 3D
  /// backdrop shows through and the words stay easy to read. Put it on the panel's card; it restyles what the panel
  /// built and keeps doing so a few times a second, since panels recolour their rows and tabs as you use them.
  /// Rules: dark text becomes cream, grey text light grey, strong colours (red, jade…) are lifted; light row
  /// backgrounds become faint glass; ink lines and secondary buttons are tinted so they show on the dark glass.
  /// </summary>
  public class GlassModal : MonoBehaviour {
    public static readonly Color Glass = new(.05f, .08f, .08f, .9f); // a hint of the scene shows, the menu behind does not compete
    static readonly Color Cream = new(.97f, .94f, .87f), Soft = new(.80f, .82f, .79f), Row = new(1, 1, 1, .08f);

    readonly Dictionary<Graphic, Color> _set = new(); // what we last wrote: anything else is a new colour to convert
    float _next;

    void Start() {
      var card = GetComponent<Image>();
      if (card) { card.sprite = null; card.type = Image.Type.Simple; card.color = Glass; }
      // Thin gold rim: four hairline edges (an Outline effect would tint the whole card).
      foreach (var (a, sz) in new[] { (new Vector2(.5f, 1), new Vector2(0, 2)), (new Vector2(.5f, 0), new Vector2(0, 2)), (new Vector2(0, .5f), new Vector2(2, 0)), (new Vector2(1, .5f), new Vector2(2, 0)) }) {
        var e = UiKit.Rect(transform, "Rim", a, a, Vector2.zero, Vector2.zero);
        e.anchorMin = new Vector2(sz.x > 0 ? a.x : 0, sz.y > 0 ? a.y : 0); e.anchorMax = new Vector2(sz.x > 0 ? a.x : 1, sz.y > 0 ? a.y : 1);
        e.sizeDelta = sz;
        var img = e.gameObject.AddComponent<Image>(); img.color = new Color(.86f, .72f, .42f, .6f); img.raycastTarget = false;
        _set[img] = img.color;
      }
      foreach (Transform c in transform) if (c.name == "Wash") c.gameObject.SetActive(false); // the paper/frame wash
      var dim = transform.parent ? transform.parent.Find("Dim") : null;
      if (dim && dim.TryGetComponent<Image>(out var d)) d.color = new Color(.02f, .04f, .04f, .6f);
      Pass();
    }

    void Update() {
      if (Time.unscaledTime < _next) return;
      _next = Time.unscaledTime + .2f;
      Pass();
    }

    void Pass() {
      foreach (var t in GetComponentsInChildren<Text>(true)) Fix(t, Text(t.color, t.GetComponentInParent<Button>()));
      foreach (var img in GetComponentsInChildren<Image>(true)) {
        if (img.gameObject == gameObject) continue;
        var c = img.color;
        string s = img.sprite ? img.sprite.name : "";
        Color want = c;
        if (s == "Lines") want = new Color(.32f, .34f, .33f, Mathf.Max(.85f, c.a)); // secondary button: a dark chip under light text
        else if (s == "BrushBar" && Lum(c) < .4f) want = new Color(.9f, .84f, .7f, Mathf.Max(.45f, c.a)); // ink rules and bar tracks
        else if (s == "" && c.a > .15f && Lum(c) > .7f) want = Row;                                           // light rows / cells
        else if (s == "WashLight" || s == "Wash") want = new Color(1, 1, 1, .1f);
        Fix(img, want);
      }
    }

    void Fix(Graphic g, Color want) {
      if (_set.TryGetValue(g, out var last) && last == g.color) return; // already ours
      g.color = want;
      _set[g] = want;
    }

    static Color Text(Color c, Button inButton) {
      // Text on a primary (Brush) button is already light on a dark stroke: leave it.
      if (inButton && inButton.targetGraphic is Image b && b.sprite && b.sprite.name == "Brush") return c;
      Color.RGBToHSV(c, out _, out float sat, out float val);
      float lum = Lum(c);
      if (lum > .62f) return c;                                   // light already
      if (sat > .35f && val > .3f) { var l = Color.Lerp(c, Color.white, .42f); l.a = c.a; return l; } // accent: lift it
      return lum < .3f ? new Color(Cream.r, Cream.g, Cream.b, c.a) : new Color(Soft.r, Soft.g, Soft.b, c.a);
    }

    static float Lum(Color c) => c.r * .3f + c.g * .59f + c.b * .11f;
  }
}
