using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BachYeu.EditorTools {
  /// <summary>
  /// Paints the "Cổ phong" UI skin (aged paper, red lacquer and gold, carved wood, red stage curtains) into
  /// Assets/_Game/Resources/UI/CoPhong. Each file uses the same name as its ink-wash twin in UI/Ink, so UiKit
  /// swaps them when the Cổ phong skin is on; anything not painted here falls back to the ink sprite.
  /// Procedural placeholder art: replace any PNG with hand-painted art of the same size and the slicing still works.
  /// </summary>
  public static class CoPhongArtGen {
    const string Dir = "Assets/_Game/Resources/UI/CoPhong";

    static readonly Color Lacquer = Hex("#8c2418"), LacquerDark = Hex("#5a120c"), Gold = Hex("#d4a84f"), GoldDark = Hex("#8a6424");
    static readonly Color PaperA = Hex("#f1e4c6"), PaperB = Hex("#e2cfa5"), Wood = Hex("#5b3a24"), WoodDark = Hex("#2e1c12");

    [MenuItem("Bách Yêu/Giao diện/Vẽ lại bộ hình Cổ phong")]
    public static void BuildAll() {
      Directory.CreateDirectory(Dir);
      Save("Wash", PanelFrame(256), new Vector4(110, 110, 110, 110));
      Save("Brush", LacquerButton(520, 120), new Vector4(90, 0, 90, 0));
      Save("Lines", PaperButton(400, 90), new Vector4(40, 20, 40, 20));
      Save("BrushBar", Bar(400, 40), new Vector4(40, 0, 40, 0));
      Save("Scroll", HangingScroll(340, 520), Vector4.zero);
      Save("WashLight", Card(128, false), new Vector4(32, 32, 32, 32));
      Save("CardLacquer", Card(128, true), new Vector4(32, 32, 32, 32));
      Save("MenuBg", Stage(1688, 780, 0), Vector4.zero);
      Save("BlessBg", Stage(1688, 780, 1), Vector4.zero);
      Save("ResultBg", Stage(1688, 780, 2), Vector4.zero);
      AssetDatabase.Refresh();
      Debug.Log("[Bách Yêu] Đã vẽ bộ hình Cổ phong vào " + Dir);
    }

    // ---------------- pieces ----------------

    /// <summary>Panel: paper with fibres, a red lacquer border, gold hairlines and lattice corners. 9-sliced at 110.</summary>
    static Texture2D PanelFrame(int n) {
      var t = New(n, n);
      for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++) {
          int e = Mathf.Min(Mathf.Min(x, y), Mathf.Min(n - 1 - x, n - 1 - y)); // distance to the edge
          Color c;
          if (e < 4) c = WoodDark;
          else if (e < 40) { // lacquer band with a soft sheen
            float k = (e - 4) / 36f;
            c = Color.Lerp(LacquerDark, Lacquer, Mathf.Sin(k * Mathf.PI) * .9f);
            c = Grain(c, x, y, .05f);
          } else if (e < 44) c = Gold;
          else if (e < 48) c = GoldDark;
          else if (e < 52) c = Color.Lerp(PaperB, WoodDark, .35f); // inner shadow
          else c = Paper(x, y);
          t.SetPixel(x, y, c);
        }
      // Gold lattice corner pieces (a small square-spiral), drawn on the lacquer band.
      foreach (var (cx, cy) in new[] { (22, 22), (n - 23, 22), (22, n - 23), (n - 23, n - 23) }) Fret(t, cx, cy, 13);
      t.Apply();
      return t;
    }

    /// <summary>Small card, 9-sliced at 32: paper in a dark wood rim (light) or red lacquer in a gold rim (lacquer).</summary>
    static Texture2D Card(int n, bool lacquer) {
      var t = New(n, n);
      for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++) {
          float d = RoundRect(x + .5f, y + .5f, n, n, 12, 2);
          if (d > 1) { t.SetPixel(x, y, Color.clear); continue; }
          Color c;
          if (lacquer) {
            if (d > -3) c = WoodDark;
            else if (d > -6) c = Gold;
            else if (d > -8) c = GoldDark;
            else c = Grain(Color.Lerp(LacquerDark, Lacquer, Mathf.Clamp01(y / (float)n * 1.3f)), x, y, .04f);
          } else {
            if (d > -5) c = Grain(Color.Lerp(WoodDark, Wood, .5f), x, y, .08f);
            else if (d > -7) c = Gold;
            else c = Paper(x, y);
          }
          c.a = Mathf.Clamp01(1 - d);
          t.SetPixel(x, y, c);
        }
      t.Apply();
      return t;
    }

    /// <summary>Primary button: a red lacquer plaque with a gold rim and a top highlight, rounded ends.</summary>
    static Texture2D LacquerButton(int w, int h) {
      var t = New(w, h);
      float r = h * .42f;
      for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++) {
          float d = RoundRect(x + .5f, y + .5f, w, h, r, 6);
          if (d > 1) { t.SetPixel(x, y, Color.clear); continue; }
          Color c;
          if (d > -5) c = Gold;                                // gold rim
          else if (d > -8) c = GoldDark;
          else {
            float v = y / (float)h;
            c = Color.Lerp(LacquerDark, Lacquer, Mathf.SmoothStep(0, 1, v * 1.2f));
            if (v > .62f && v < .8f) c = Color.Lerp(c, Color.white, .12f * Mathf.Sin((v - .62f) / .18f * Mathf.PI)); // sheen
            c = Grain(c, x, y, .04f);
          }
          c.a = Mathf.Clamp01(1 - d); // anti-aliased edge
          t.SetPixel(x, y, c);
        }
      t.Apply();
      return t;
    }

    /// <summary>Secondary button: paper card framed in dark wood with a gold hairline.</summary>
    static Texture2D PaperButton(int w, int h) {
      var t = New(w, h);
      for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++) {
          float d = RoundRect(x + .5f, y + .5f, w, h, 14, 4);
          if (d > 1) { t.SetPixel(x, y, Color.clear); continue; }
          Color c;
          if (d > -6) c = Grain(Color.Lerp(WoodDark, Wood, .5f), x, y, .08f);
          else if (d > -8) c = Gold;
          else c = Paper(x, y);
          c.a = Mathf.Clamp01(1 - d);
          t.SetPixel(x, y, c);
        }
      t.Apply();
      return t;
    }

    /// <summary>Bar / rule: a light rounded bar (it is tinted per use) with a darker lower lip.</summary>
    static Texture2D Bar(int w, int h) {
      var t = New(w, h);
      for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++) {
          float d = RoundRect(x + .5f, y + .5f, w, h, h * .45f, 6);
          if (d > 1) { t.SetPixel(x, y, Color.clear); continue; }
          float v = y / (float)h;
          var c = Color.Lerp(new Color(.78f, .74f, .68f), Color.white, Mathf.SmoothStep(.1f, .7f, v));
          c.a = Mathf.Clamp01(1 - d);
          t.SetPixel(x, y, c);
        }
      t.Apply();
      return t;
    }

    /// <summary>Hanging scroll: brocade margins, paper centre, wooden rods with gold caps top and bottom.</summary>
    static Texture2D HangingScroll(int w, int h) {
      var t = New(w, h);
      int rod = 26, margin = 22;
      for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++) {
          Color c = Color.clear;
          bool rodRow = y < rod || y >= h - rod;
          if (rodRow) {
            int ry = y < rod ? y : h - 1 - y;
            float cyl = Mathf.Sin(ry / (float)rod * Mathf.PI);
            bool cap = x < 18 || x >= w - 18;
            c = cap ? Color.Lerp(GoldDark, Gold, cyl) : Color.Lerp(WoodDark, Wood, cyl);
            if (!cap) c = Grain(c, x, y, .1f);
          } else if (x >= 10 && x < w - 10) {
            int ex = Mathf.Min(x - 10, w - 11 - x), ey = Mathf.Min(y - rod, h - rod - 1 - y);
            if (ex < margin || ey < margin + 8) { // red brocade with a faint gold cloud dot pattern
              c = Grain(Lacquer, x, y, .05f);
              if (((x / 9) + (y / 9)) % 4 == 0 && (x % 9 > 3 && x % 9 < 6) && (y % 9 > 3 && y % 9 < 6)) c = Color.Lerp(c, Gold, .6f);
              if (ex == margin - 1 || ey == margin + 7) c = Gold;
            } else c = Paper(x, y);
          }
          t.SetPixel(x, y, c);
        }
      t.Apply();
      return t;
    }

    /// <summary>
    /// Shadow-puppet stage: carved wood wall, red curtains with folds on both sides, a lacquer valance with a gold
    /// lattice band and fringe, a warm glow in the middle. Variant 0 menu, 1 blessing (golden), 2 result (dusk).
    /// </summary>
    static Texture2D Stage(int w, int h, int variant) {
      var t = New(w, h);
      var glow = variant == 1 ? Hex("#f6d58c") : variant == 2 ? Hex("#e9a87a") : Hex("#f0dcb0");
      int valance = variant == 0 ? 86 : 0, curtain = variant == 0 ? 250 : 200; // titles sit at the very top there: no valance
      // Blessing and result screens print dark text over most of the stage: hang a big paper sheet there.
      int px0 = (int)(w * .12f), px1 = (int)(w * .88f), py0 = (int)(h * .05f), py1 = h + 40; // the sheet runs off the top edge
      for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++) {
          float u = x / (float)w, v = y / (float)h;
          // Back wall: dark planks with a warm glow behind the centre.
          float plank = (x % 140 < 3) ? .7f : 1f;
          float g = Mathf.Exp(-((u - .5f) * (u - .5f) * 5f + (v - .55f) * (v - .55f) * 7f));
          var c = Grain(Color.Lerp(WoodDark, Wood, .25f) * plank, x, y, .05f);
          c = Color.Lerp(c, glow, g * .78f);
          // Stage floor.
          if (y < 70) c = Grain(Color.Lerp(WoodDark, Wood, .55f + (y % 18 < 2 ? -.3f : 0)), x, y, .08f);
          // Curtains (left and right), gathered at the bottom.
          int dx = Mathf.Min(x, w - 1 - x);
          float reach = curtain * (.75f + .25f * v);
          if (dx < reach) {
            float fold = Mathf.Sin(dx / 34f * Mathf.PI * 2 + v * 1.2f) * .5f + .5f;
            var cc = Color.Lerp(LacquerDark, Lacquer, .35f + fold * .65f);
            float edge = Mathf.Clamp01((reach - dx) / 6f);
            c = Color.Lerp(c, Grain(cc, x, y, .03f), edge);
            if (reach - dx < 3) c = Color.Lerp(c, GoldDark, .8f);
          }
          // Valance across the top: lacquer with a gold lattice band, gold fringe below.
          int top = h - 1 - y;
          if (top < valance) {
            c = Grain(Color.Lerp(LacquerDark, Lacquer, .6f), x, y, .04f);
            if (top > 22 && top < 58) c = Lattice(x, top - 22) ? Gold : Color.Lerp(LacquerDark, WoodDark, .5f);
            if (top == 22 || top == 58 || top == 59) c = Gold;
            if (top >= valance - 10 && (x % 10) < 4) c = Color.Lerp(Gold, GoldDark, (top - (valance - 10)) / 10f);
            else if (top >= valance - 10) c = Color.Lerp(WoodDark, Wood, .4f);
          }
          // Hanging paper sheet (blessing / result): paper with a gold hairline, wooden rods with gold caps.
          if (variant > 0) {
            bool inX = x >= px0 && x < px1;
            if (inX && y >= py0 && y < py1) {
              int e = Mathf.Min(Mathf.Min(x - px0, px1 - 1 - x), Mathf.Min(y - py0, py1 - 1 - y));
              c = e < 3 ? GoldDark : e < 5 ? Gold : Color.Lerp(Paper(x, y), PaperB, Mathf.Clamp01(1 - e / 40f) * .5f);
            }
            int rodH = 18;
            bool rodTop = y >= py1 && y < py1 + rodH, rodBot = y < py0 && y >= py0 - rodH;
            if ((rodTop || rodBot) && x >= px0 - 22 && x < px1 + 22) {
              int ry = rodTop ? y - py1 : py0 - 1 - y;
              float cyl = Mathf.Sin(ry / (float)rodH * Mathf.PI);
              bool cap = x < px0 + 6 || x >= px1 - 6;
              c = cap ? Color.Lerp(GoldDark, Gold, cyl) : Grain(Color.Lerp(WoodDark, Wood, cyl), x, y, .1f);
            }
          }
          // Vignette.
          float vig = Mathf.Clamp01(1 - Mathf.Pow(Mathf.Abs(u - .5f) * 2, 3) * .35f - Mathf.Pow(Mathf.Abs(v - .5f) * 2, 3) * .25f);
          c *= vig; c.a = 1;
          t.SetPixel(x, y, c);
        }
      t.Apply();
      return t;
    }

    // ---------------- helpers ----------------

    static bool Lattice(int x, int y) { // a repeating Chinese window fret, 36 px tall
      int px = x % 36, py = y % 36;
      bool outer = px < 3 || py < 3;
      bool inner = (px > 10 && px < 13 && py > 10 && py < 26) || (py > 10 && py < 13 && px > 10 && px < 26) || (px > 23 && px < 26 && py > 17 && py < 26);
      return outer || inner;
    }

    static void Fret(Texture2D t, int cx, int cy, int r) {
      for (int y = -r; y <= r; y++)
        for (int x = -r; x <= r; x++) {
          int ax = Mathf.Abs(x), ay = Mathf.Abs(y);
          bool ring = (Mathf.Max(ax, ay) == r) || (Mathf.Max(ax, ay) == r - 5 && !(x > 0 && ay < 3)) || (ax < 2 && ay < 2);
          if (ring) t.SetPixel(cx + x, cy + y, Gold);
        }
    }

    static Color Paper(int x, int y) {
      float n = Mathf.PerlinNoise(x * .05f, y * .05f) * .6f + Mathf.PerlinNoise(x * .3f, y * .02f) * .4f; // blotches + fibres
      var c = Color.Lerp(PaperA, PaperB, n * .7f);
      return c;
    }

    static Color Grain(Color c, int x, int y, float amt) {
      float n = Mathf.PerlinNoise(x * .02f, y * .4f) - .5f;
      return new Color(Mathf.Clamp01(c.r + n * amt), Mathf.Clamp01(c.g + n * amt), Mathf.Clamp01(c.b + n * amt), c.a);
    }

    /// <summary>Signed distance (px) to a rounded rectangle inset by pad; negative inside.</summary>
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
      UnityEngine.Object.DestroyImmediate(t);
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
