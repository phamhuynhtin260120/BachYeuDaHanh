using System.IO;
using UnityEditor;
using UnityEngine;

namespace BachYeu.EditorTools {
  /// <summary>
  /// Paints the "Tiên cảnh" UI skin (jade fairyland: layered misty mountains, celadon paper, jade and pale gold) into
  /// Assets/_Game/Resources/UI/TienCanh. Same file names as UI/Ink, so UiKit swaps them; anything missing falls back.
  /// Procedural placeholder art: replace a PNG with a hand-painted one of the same size and slicing keeps working.
  /// </summary>
  public static class TienCanhArtGen {
    const string Dir = "Assets/_Game/Resources/UI/TienCanh";

    static readonly Color JadeDeep = Hex("#0f4e47"), Jade = Hex("#2f8a76"), JadeLight = Hex("#8fd0b5"), Celadon = Hex("#eef3e6");
    static readonly Color CeladonB = Hex("#dbe9d6"), GoldPale = Hex("#e8d79a"), Gold = Hex("#c9ab5a"), SkyTop = Hex("#eef6e9"), SkyLow = Hex("#bfe2d0");

    [MenuItem("Bách Yêu/Giao diện/Vẽ lại bộ hình Tiên cảnh")]
    public static void BuildAll() {
      Directory.CreateDirectory(Dir);
      Save("Wash", Panel(256), new Vector4(110, 110, 110, 110));
      Save("Brush", JadeButton(520, 120), new Vector4(90, 0, 90, 0));
      Save("Lines", LightButton(400, 90), new Vector4(40, 20, 40, 20));
      Save("BrushBar", Bar(400, 40), new Vector4(40, 0, 40, 0));
      Save("Scroll", HangingScroll(340, 520), Vector4.zero);
      Save("WashLight", Card(128, false), new Vector4(32, 32, 32, 32));
      Save("CardLacquer", Card(128, true), new Vector4(32, 32, 32, 32));
      Save("Orb", Orb(256), Vector4.zero);
      Save("MenuBg", Landscape(1688, 780, 0), Vector4.zero);
      Save("BlessBg", Landscape(1688, 780, 1), Vector4.zero);
      Save("ResultBg", Landscape(1688, 780, 2), Vector4.zero);
      AssetDatabase.Refresh();
      Debug.Log("[Bách Yêu] Đã vẽ bộ hình Tiên cảnh vào " + Dir);
    }

    // ---------------- landscape ----------------

    /// <summary>Misty layered jade mountains under a pale sun, gold-lit ridges, cloud bands. 1, 2: a celadon sheet hangs in the middle for text.</summary>
    static Texture2D Landscape(int w, int h, int variant) {
      var t = New(w, h);
      var px = new Color[w * h];
      const int layers = 5;
      var ridge = new float[layers, w];
      for (int l = 0; l < layers; l++) {
        float baseH = .6f - l * .085f, amp = .2f + l * .02f, f = .0016f + l * .0007f;
        for (int x = 0; x < w; x++) {
          // Ridged noise: sharp karst peaks like the misty mountains of Chinese landscape painting.
          float r1 = 1 - Mathf.Abs(Mathf.PerlinNoise(x * f + l * 13.1f, l * 7.3f) * 2 - 1);
          float r2 = 1 - Mathf.Abs(Mathf.PerlinNoise(x * f * 2.7f + l * 3.7f, 2 + l) * 2 - 1);
          float smooth = Mathf.PerlinNoise(x * f * .8f + l * 5.3f, l * 1.9f);
          float peak = smooth * .6f + Mathf.Pow(r1, 2.2f) * .3f + Mathf.Pow(r2, 3f) * .1f;
          ridge[l, x] = (baseH + (peak - .45f) * amp * 1.5f) * h;
        }
      }
      Vector2 sun = new(w * .64f, h * .76f);
      float sunR = h * .085f;
      for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++) {
          float v = y / (float)h;
          var c = Color.Lerp(SkyLow, SkyTop, Mathf.SmoothStep(.25f, 1f, v));
          float d = Vector2.Distance(new Vector2(x, y), sun);
          c = Color.Lerp(c, new Color(1, .99f, .93f), Mathf.Clamp01(1 - (d - sunR) / (sunR * 2.2f)) * .55f);
          if (d < sunR) c = Color.Lerp(c, new Color(1, .99f, .94f), .9f);
          // Mountain layers, far (pale) to near (deep), each fading into mist at its foot.
          for (int l = 0; l < layers; l++) {
            float top = ridge[l, x];
            if (y > top) continue;
            float k = l / (float)(layers - 1);
            var mc = Color.Lerp(Color.Lerp(SkyLow, JadeLight, .7f), JadeDeep, Mathf.Pow(k, .85f));
            float depth = (top - y) / h;
            mc = Color.Lerp(mc, Color.Lerp(SkyLow, Color.white, .3f), Mathf.Clamp01(depth * (2.6f - k * 1.2f)) * (.75f - k * .35f)); // mist at the foot
            mc = Grain(mc, x, y, .008f);
            if (top - y < 2.2f) mc = Color.Lerp(mc, GoldPale, .75f - k * .25f); // gold-lit ridge line
            c = mc;
          }
          // Cloud bands drifting across.
          for (int b = 0; b < 3; b++) {
            float cy = h * (.18f + b * .17f) + Mathf.Sin(x * .006f + b * 2.1f) * h * .03f;
            float band = Mathf.Clamp01(1 - Mathf.Abs(y - cy) / (h * (.035f + b * .01f)));
            float puff = Mathf.PerlinNoise(x * .004f + b * 9, y * .02f + b) ;
            c = Color.Lerp(c, new Color(.97f, .99f, .97f), band * band * Mathf.SmoothStep(.35f, .75f, puff) * .85f);
          }
          // Hanging sheet (blessing / result).
          if (variant > 0) {
            int x0 = (int)(w * .13f), x1 = (int)(w * .87f), y0 = (int)(h * .025f); // low: the footer hint sits on the sheet
            if (x >= x0 && x < x1 && y >= y0) {
              int e = Mathf.Min(Mathf.Min(x - x0, x1 - 1 - x), y - y0);
              var paper = Color.Lerp(Celadon, CeladonB, Mathf.PerlinNoise(x * .03f, y * .03f) * .6f);
              c = e < 2 ? Gold : e < 4 ? GoldPale : Color.Lerp(c, paper, Mathf.Clamp01(.86f + e / 400f));
            }
            int rodH = 12;
            if (y < y0 && y >= y0 - rodH && x >= x0 - 22 && x < x1 + 22) {
              float cyl = Mathf.Sin((y0 - 1 - y) / (float)rodH * Mathf.PI);
              bool cap = x < x0 + 4 || x >= x1 - 4;
              c = cap ? Color.Lerp(Gold, GoldPale, cyl) : Color.Lerp(JadeDeep, Jade, cyl);
            }
          }
          float u = x / (float)w;
          float vig = 1 - Mathf.Pow(Mathf.Abs(u - .5f) * 2, 4) * .18f - Mathf.Pow(Mathf.Abs(v - .5f) * 2, 4) * .12f;
          c = Color.Lerp(JadeDeep, c, vig); c.a = 1;
          px[y * w + x] = c;
        }
      t.SetPixels(px);
      t.Apply();
      return t;
    }

    // ---------------- pieces ----------------

    /// <summary>Panel: celadon paper with a jade border, a pale gold hairline and cloud-curl corners. 9-sliced at 110.</summary>
    static Texture2D Panel(int n) {
      var t = New(n, n);
      for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++) {
          float d = RoundRect(x + .5f, y + .5f, n, n, 30, 2);
          if (d > 1) { t.SetPixel(x, y, Color.clear); continue; }
          Color c;
          if (d > -18) c = Color.Lerp(JadeDeep, Jade, Mathf.Sin(Mathf.Clamp01(-d / 18f) * Mathf.PI) * .8f);
          else if (d > -21) c = GoldPale;
          else if (d > -23) c = Gold;
          else c = Paper(x, y);
          c.a = Mathf.Clamp01(1 - d);
          t.SetPixel(x, y, c);
        }
      foreach (var (cx, cy) in new[] { (20, 20), (n - 21, 20), (20, n - 21), (n - 21, n - 21) }) Curl(t, cx, cy, 8);
      t.Apply();
      return t;
    }

    static Texture2D JadeButton(int w, int h) {
      var t = New(w, h);
      float r = h * .44f;
      for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++) {
          float d = RoundRect(x + .5f, y + .5f, w, h, r, 6);
          if (d > 1) { t.SetPixel(x, y, Color.clear); continue; }
          Color c;
          if (d > -4) c = GoldPale;
          else if (d > -6) c = Gold;
          else {
            float v = y / (float)h;
            c = Color.Lerp(JadeDeep, Jade, Mathf.SmoothStep(0, 1, v * 1.1f));
            if (v > .6f && v < .82f) c = Color.Lerp(c, Color.white, .14f * Mathf.Sin((v - .6f) / .22f * Mathf.PI));
          }
          c.a = Mathf.Clamp01(1 - d);
          t.SetPixel(x, y, c);
        }
      t.Apply();
      return t;
    }

    static Texture2D LightButton(int w, int h) {
      var t = New(w, h);
      for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++) {
          float d = RoundRect(x + .5f, y + .5f, w, h, h * .4f, 4);
          if (d > 1) { t.SetPixel(x, y, Color.clear); continue; }
          Color c = d > -3 ? Jade : d > -5 ? GoldPale : Color.Lerp(Celadon, Color.white, .3f);
          c.a = Mathf.Clamp01(1 - d) * (d > -5 ? 1 : .92f);
          t.SetPixel(x, y, c);
        }
      t.Apply();
      return t;
    }

    static Texture2D Bar(int w, int h) {
      var t = New(w, h);
      for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++) {
          float d = RoundRect(x + .5f, y + .5f, w, h, h * .45f, 8);
          if (d > 1) { t.SetPixel(x, y, Color.clear); continue; }
          var c = Color.Lerp(new Color(.8f, .84f, .8f), Color.white, Mathf.SmoothStep(.1f, .7f, y / (float)h));
          c.a = Mathf.Clamp01(1 - d);
          t.SetPixel(x, y, c);
        }
      t.Apply();
      return t;
    }

    /// <summary>Hanging scroll: jade brocade margins, celadon paper, jade rods with gold caps and two tassels.</summary>
    static Texture2D HangingScroll(int w, int h) {
      var t = New(w, h);
      int rod = 24, margin = 18;
      for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++) {
          Color c = Color.clear;
          if (y < rod || y >= h - rod) {
            int ry = y < rod ? y : h - 1 - y;
            float cyl = Mathf.Sin(ry / (float)rod * Mathf.PI);
            bool cap = x < 18 || x >= w - 18;
            c = cap ? Color.Lerp(Gold, GoldPale, cyl) : Color.Lerp(JadeDeep, Jade, cyl);
          } else if (x >= 10 && x < w - 10) {
            int ex = Mathf.Min(x - 10, w - 11 - x), ey = Mathf.Min(y - rod, h - rod - 1 - y);
            if (ex < margin || ey < margin + 6) {
              c = Color.Lerp(JadeDeep, Jade, .45f);
              if (((x / 10) + (y / 10)) % 3 == 0 && x % 10 > 3 && x % 10 < 6 && y % 10 > 3 && y % 10 < 6) c = Color.Lerp(c, GoldPale, .7f);
              if (ex == margin - 1 || ey == margin + 5) c = GoldPale;
            } else c = Paper(x, y);
          }
          t.SetPixel(x, y, c);
        }
      t.Apply();
      return t;
    }

    /// <summary>Small card, 9-sliced at 32: translucent celadon (light) or translucent deep jade with a gold hairline (dark).</summary>
    static Texture2D Card(int n, bool dark) {
      var t = New(n, n);
      for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++) {
          float d = RoundRect(x + .5f, y + .5f, n, n, 16, 2);
          if (d > 1) { t.SetPixel(x, y, Color.clear); continue; }
          Color c;
          float a;
          if (dark) {
            if (d > -3) { c = GoldPale; a = 1; }
            else { c = Color.Lerp(JadeDeep, Jade, Mathf.Clamp01(y / (float)n) * .5f); a = .86f; }
          } else {
            if (d > -3) { c = Jade; a = .9f; }
            else { c = Paper(x, y); a = .93f; }
          }
          c.a = Mathf.Clamp01(1 - d) * a;
          t.SetPixel(x, y, c);
        }
      t.Apply();
      return t;
    }

    /// <summary>Round jade orb with a pale gold ring and a soft glow around it (icon tiles).</summary>
    static Texture2D Orb(int n) {
      var t = New(n, n);
      float r = n * .4f, cx = n / 2f;
      for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++) {
          float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(cx, cx));
          Color c;
          if (d > r + 4) { float g = Mathf.Clamp01(1 - (d - r - 4) / (n * .1f)); c = new Color(1, .97f, .8f, g * g * .5f); }
          else if (d > r) c = GoldPale;
          else if (d > r - 3) c = Gold;
          else {
            float k = d / r;
            c = Color.Lerp(Jade, JadeDeep, k * k);
            float hl = Mathf.Clamp01(1 - Vector2.Distance(new Vector2(x, y), new Vector2(cx - r * .3f, cx + r * .35f)) / (r * .6f));
            c = Color.Lerp(c, JadeLight, hl * .45f);
          }
          if (d > r + 4 - 1 && d <= r + 4) c.a = Mathf.Max(c.a, .6f);
          t.SetPixel(x, y, c);
        }
      t.Apply();
      return t;
    }

    // ---------------- helpers ----------------

    static void Curl(Texture2D t, int cx, int cy, int r) { // a small spiral cloud curl in pale gold
      for (float a = 0; a < Mathf.PI * 3.2f; a += .05f) {
        float rr = r * (1 - a / (Mathf.PI * 3.6f));
        int x = cx + Mathf.RoundToInt(Mathf.Cos(a) * rr), y = cy + Mathf.RoundToInt(Mathf.Sin(a) * rr);
        t.SetPixel(x, y, GoldPale); t.SetPixel(x + 1, y, GoldPale);
      }
    }

    static Color Paper(int x, int y) {
      float n = Mathf.PerlinNoise(x * .05f, y * .05f) * .6f + Mathf.PerlinNoise(x * .3f, y * .02f) * .4f;
      return Color.Lerp(Celadon, CeladonB, n * .6f);
    }

    static Color Grain(Color c, int x, int y, float amt) {
      float n = Mathf.PerlinNoise(x * .02f, y * .3f) - .5f;
      return new Color(Mathf.Clamp01(c.r + n * amt), Mathf.Clamp01(c.g + n * amt), Mathf.Clamp01(c.b + n * amt), c.a);
    }

    static float RoundRect(float x, float y, int w, int h, float r, float pad) {
      float hx = w / 2f - pad, hy = h / 2f - pad;
      float qx = Mathf.Abs(x - w / 2f) - (hx - r), qy = Mathf.Abs(y - h / 2f) - (hy - r);
      float ox = Mathf.Max(qx, 0), oy = Mathf.Max(qy, 0);
      return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
    }

    static Texture2D New(int w, int h) => new(w, h, TextureFormat.RGBA32, false);

    static void Save(string name, Texture2D t, Vector4 border) {
      var path = $"{Dir}/{name}.png";
      File.WriteAllBytes(path, t.EncodeToPNG());
      Object.DestroyImmediate(t);
      AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
      var imp = (TextureImporter)AssetImporter.GetAtPath(path);
      imp.textureType = TextureImporterType.Sprite;
      imp.spriteImportMode = SpriteImportMode.Single;
      imp.alphaIsTransparency = true;
      imp.mipmapEnabled = false;
      imp.spriteBorder = border;
      imp.SaveAndReimport();
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
  }
}
