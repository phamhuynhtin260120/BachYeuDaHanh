using System.Collections.Generic;
using UnityEngine;

namespace BachYeu {
  /// <summary>Placeholder chibi and yokai models built from primitives. Replace with real art later.</summary>
  public static class Models {
    public class Chibi {
      public Transform Root, Body;
      public Renderer Robe, Face;
      public GameObject[] Ears;
      public GameObject Nose, Stripe;
    }

    public static Chibi BuildChibi(Transform parent, Color robe, Color hair) {
      var c = new Chibi();
      var root = new GameObject("Model").transform;
      root.SetParent(parent, false);
      c.Root = root;
      c.Robe = Gfx.Prim(PrimitiveType.Cylinder, root, new Vector3(0, .45f, 0), new Vector3(.75f, .36f, .75f), Gfx.NewLit(robe)).GetComponent<Renderer>();
      c.Body = c.Robe.transform;
      var dark = Gfx.Lit(Gfx.Hex("#3a2a22"));
      Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(-.14f, .08f, 0), Vector3.one * .24f, dark);
      Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(.14f, .08f, 0), Vector3.one * .24f, dark);
      Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(0, 1.25f, 0), Vector3.one * 1.12f, Gfx.Lit(Gfx.Hex("#f6dcc4")));
      var hairGo = Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(0, 1.47f, -.16f), new Vector3(1.16f, .78f, 1.06f), Gfx.Lit(hair));
      hairGo.name = "Hair";
      var eye = Gfx.Unlit(Fx.Ink);
      Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(-.18f, 1.18f, .5f), Vector3.one * .14f, eye);
      Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(.18f, 1.18f, .5f), Vector3.one * .14f, eye);

      var mask = new GameObject("Mask").transform;
      mask.SetParent(root, false);
      mask.localPosition = new Vector3(.44f, 1.5f, .12f);
      mask.localRotation = Quaternion.Euler(0, 57, 0);
      c.Face = Gfx.Prim(PrimitiveType.Sphere, mask, Vector3.zero, new Vector3(.48f, .53f, .22f), Gfx.NewLit(Color.white)).GetComponent<Renderer>();
      c.Ears = new[] {
        Gfx.MeshObj(Gfx.Cone, mask, new Vector3(-.1f, .18f, 0), new Vector3(.16f, .22f, .16f), Gfx.Lit(Color.white)),
        Gfx.MeshObj(Gfx.Cone, mask, new Vector3(.1f, .18f, 0), new Vector3(.16f, .22f, .16f), Gfx.Lit(Color.white)),
      };
      c.Nose = Gfx.MeshObj(Gfx.Cone, mask, new Vector3(0, .02f, .06f), new Vector3(.14f, .34f, .14f), Gfx.Lit(Gfx.Hex("#8f2418")));
      c.Nose.transform.localRotation = Quaternion.Euler(90, 0, 0);
      c.Stripe = Gfx.Prim(PrimitiveType.Cube, mask, new Vector3(0, 0, .11f), new Vector3(.04f, .2f, .04f), Gfx.Lit(Gfx.Hex("#c0392b")));
      return c;
    }

    /// <summary>Placeholder look per mask id: robe and mask colors, fox ears, tengu nose.</summary>
    public static void ApplyMask(Chibi c, int mask) {
      var (robe, face) = mask switch {
        Masks.Tengu => (Gfx.Hex("#2f5f8a"), Gfx.Hex("#c0392b")),
        Masks.HaBa => (Gfx.Hex("#2f8f8a"), Gfx.Hex("#d8efe9")),
        Masks.QuyDa => (Gfx.Hex("#6b5640"), Gfx.Hex("#9a8a76")),
        _ => (Gfx.Hex("#c23a3a"), Color.white),
      };
      c.Robe.sharedMaterial.SetColor("_BaseColor", robe);
      c.Face.sharedMaterial.SetColor("_BaseColor", face);
      foreach (var e in c.Ears) e.SetActive(mask == Masks.Fox || mask == Masks.QuyDa); // Quỷ Đá: stubby horns
      c.Nose.SetActive(mask == Masks.Tengu);
      c.Stripe.SetActive(mask == Masks.Fox || mask == Masks.HaBa);
    }

    /// <summary>Builds a yokai model and returns the materials that should flash on hit.</summary>
    public static Transform BuildEnemy(Transform parent, EnemyType type, List<Material> flashMats) {
      var root = new GameObject("Model").transform;
      root.SetParent(parent, false);
      Material Flash(Color c, Color? em = null) { var m = Gfx.NewLit(c, em); flashMats.Add(m); return m; }
      var ink = Gfx.Unlit(Fx.Ink);
      switch (type) {
        case EnemyType.Ghost: {
          Gfx.Prim(PrimitiveType.Cylinder, root, new Vector3(0, .9f, 0), new Vector3(.84f, .38f, .84f), Flash(Gfx.Hex("#c0392b"), Gfx.Hex("#7a1a10")));
          Gfx.Prim(PrimitiveType.Cylinder, root, new Vector3(0, 1.33f, 0), new Vector3(.6f, .05f, .6f), Gfx.Lit(Fx.Ink));
          Gfx.Prim(PrimitiveType.Cylinder, root, new Vector3(0, .47f, 0), new Vector3(.6f, .05f, .6f), Gfx.Lit(Fx.Ink));
          Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(-.14f, 1, .4f), Vector3.one * .16f, ink);
          Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(.14f, 1, .4f), Vector3.one * .16f, ink);
          Gfx.Prim(PrimitiveType.Cube, root, new Vector3(0, .78f, .41f), new Vector3(.22f, .05f, .04f), ink);
          break;
        }
        case EnemyType.Umbrella:
        case EnemyType.Elite: {
          Gfx.MeshObj(Gfx.Cone, root, new Vector3(0, .8f, 0), new Vector3(1.7f, .9f, 1.7f), Flash(Gfx.Hex("#6b4a8a")));
          Gfx.Prim(PrimitiveType.Cylinder, root, new Vector3(0, .45f, 0), new Vector3(.1f, .45f, .1f), Gfx.Lit(Gfx.Hex("#6b4a3a")));
          Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(0, 1.1f, .52f), Vector3.one * .36f, Gfx.Unlit(Gfx.Hex("#faf6ec")));
          Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(0, 1.1f, .68f), Vector3.one * .16f, ink);
          Gfx.Prim(PrimitiveType.Cube, root, new Vector3(0, .86f, .55f), new Vector3(.12f, .3f, .04f), Gfx.Unlit(Gfx.Hex("#c0392b")));
          if (type == EnemyType.Elite) root.localScale = Vector3.one * 1.7f;
          break;
        }
        case EnemyType.Heavy: {
          var b = Gfx.Prim(PrimitiveType.Cube, root, new Vector3(0, 1, 0), Vector3.one * 1.5f, Flash(Gfx.Hex("#6a6f7a")));
          b.transform.localRotation = Quaternion.Euler(20, 35, 15);
          foreach (var x in new[] { -.45f, .45f }) {
            var h = Gfx.MeshObj(Gfx.Cone, root, new Vector3(x, 1.65f, 0), new Vector3(.32f, .55f, .32f), Gfx.Lit(Gfx.Hex("#e9dcc8")));
            h.transform.localRotation = Quaternion.Euler(0, 0, -x * 40);
          }
          foreach (var x in new[] { -.3f, .3f })
            Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(x, 1.15f, .85f), Vector3.one * .22f, Gfx.Unlit(Gfx.Hex("#ffd94a")));
          break;
        }
        case EnemyType.Cat: {
          var dark = Flash(Gfx.Hex("#3a3040"));
          Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(0, 1, 0), new Vector3(2.3f, 1.7f, 3.2f), dark);
          Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(0, 1.6f, 1.3f), Vector3.one * 1.5f, dark);
          foreach (var x in new[] { -.4f, .4f }) Gfx.MeshObj(Gfx.Cone, root, new Vector3(x, 2.1f, 1.3f), new Vector3(.5f, .5f, .5f), dark);
          foreach (var x in new[] { -.28f, .28f }) Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(x, 1.7f, 1.98f), Vector3.one * .24f, Gfx.Unlit(Gfx.Hex("#c7f05a")));
          foreach (var x in new[] { -.35f, .35f }) {
            var t = Gfx.Prim(PrimitiveType.Cylinder, root, new Vector3(x, 1.9f, -1.5f), new Vector3(.2f, .85f, .2f), dark);
            t.transform.localRotation = Quaternion.Euler(-32, 0, x * 34);
            Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(x * 1.9f, 2.65f, -1.95f), Vector3.one * .34f, Gfx.Unlit(Gfx.Hex("#c88ae6")));
          }
          break;
        }
        case EnemyType.King: {
          Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(0, 1.2f, 0), Vector3.one * 2.1f, Flash(Gfx.Hex("#6b4a8a")));
          foreach (var x in new[] { -.5f, .5f }) {
            var h = Gfx.MeshObj(Gfx.Cone, root, new Vector3(x, 2.0f, 0), new Vector3(.44f, .7f, .44f), Gfx.Lit(Gfx.Hex("#e9dcc8")));
            h.transform.localRotation = Quaternion.Euler(0, 0, -x * 34);
            Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(x * .7f, 1.4f, .92f), Vector3.one * .3f, Gfx.Unlit(Gfx.Hex("#ffd94a")));
          }
          Gfx.Prim(PrimitiveType.Cylinder, root, new Vector3(0, .55f, 0), new Vector3(2.1f, .1f, 2.1f), Flash(Gfx.Hex("#b3392b"), Gfx.Hex("#7a1a10")));
          foreach (var s in new[] { -1f, 1f })
            Gfx.Prim(PrimitiveType.Cylinder, root, new Vector3(s * 1.5f, 1.6f, .2f), new Vector3(.36f, .17f, .36f), Gfx.NewLit(Gfx.Hex("#c0392b"), Gfx.Hex("#ff7a2a")));
          root.localScale = Vector3.one * 1.5f;
          break;
        }
        case EnemyType.Wisp: { // Ma Trơi: a floating blue-green will-o'-wisp with a pale core
          Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(0, 1.3f, 0), Vector3.one * 1.1f, Flash(Gfx.Hex("#3fb8a8"), Gfx.Hex("#2ad1b8")));
          Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(0, 1.3f, 0), Vector3.one * .55f, Gfx.Unlit(Gfx.Hex("#e8fff9")));
          for (int i = 0; i < 3; i++) {
            float a = i * 120f * Mathf.Deg2Rad;
            var f = Gfx.MeshObj(Gfx.Cone, root, new Vector3(Mathf.Cos(a) * .35f, 1.85f, Mathf.Sin(a) * .35f), new Vector3(.32f, .6f, .32f), Gfx.NewLit(Gfx.Hex("#7fe0cf"), Gfx.Hex("#3fd9c2")));
            f.transform.localRotation = Quaternion.Euler(Mathf.Sin(a) * 18, 0, -Mathf.Cos(a) * 18);
          }
          Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(-.17f, 1.38f, .48f), Vector3.one * .14f, ink);
          Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(.17f, 1.38f, .48f), Vector3.one * .14f, ink);
          break;
        }
        case EnemyType.Inkstone: { // Nghiên mực yêu: a squat inkstone with a pool of ink on top and a stick of ink for a horn
          var stone = Flash(Gfx.Hex("#3c3f44"));
          Gfx.Prim(PrimitiveType.Cube, root, new Vector3(0, .45f, 0), new Vector3(1.1f, .7f, .8f), stone);
          Gfx.Prim(PrimitiveType.Cylinder, root, new Vector3(0, .81f, -.05f), new Vector3(.7f, .02f, .5f), Gfx.Unlit(Fx.Ink));
          var stick = Gfx.Prim(PrimitiveType.Cube, root, new Vector3(.3f, 1.05f, -.15f), new Vector3(.16f, .5f, .16f), Gfx.Lit(Gfx.Hex("#1c1d20")));
          stick.transform.localRotation = Quaternion.Euler(0, 0, -18);
          foreach (var x in new[] { -.22f, .22f }) {
            Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(x, .55f, .41f), Vector3.one * .2f, Gfx.Unlit(Gfx.Hex("#faf6ec")));
            Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(x, .55f, .5f), Vector3.one * .09f, ink);
          }
          foreach (var x in new[] { -.4f, .4f }) Gfx.Prim(PrimitiveType.Cube, root, new Vector3(x, .06f, .2f), new Vector3(.2f, .12f, .3f), stone);
          break;
        }
        case EnemyType.Shade: { // Bóng không đèn: a tall shadow with a snuffed paper lantern for a head; a faint ground mark always shows
          var dark = Flash(Gfx.Hex("#22232a"));
          Gfx.MeshObj(Gfx.Cone, root, new Vector3(0, .75f, 0), new Vector3(1f, 1.5f, 1f), dark);
          Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(0, 1.55f, 0), new Vector3(.6f, .7f, .6f), Gfx.Lit(Gfx.Hex("#8c8577")));
          Gfx.Prim(PrimitiveType.Cylinder, root, new Vector3(0, 1.95f, 0), new Vector3(.32f, .04f, .32f), Gfx.Lit(Fx.Ink));
          foreach (var x in new[] { -.12f, .12f }) Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(x, 1.6f, .28f), Vector3.one * .1f, Gfx.Unlit(Gfx.Hex("#d9f2ff")));
          var mark = Gfx.Prim(PrimitiveType.Cylinder, root, new Vector3(0, .02f, 0), new Vector3(1.1f, .01f, 1.1f), Gfx.Unlit(Gfx.Hex("#5a5c66")));
          mark.name = "Shadow";
          break;
        }
        case EnemyType.Firecracker: { // Pháo yêu: a bundle of red firecrackers with a lit fuse and a grin
          var red = Flash(Gfx.Hex("#c62f22"));
          for (int i = 0; i < 3; i++) {
            float a = i * 120f * Mathf.Deg2Rad;
            Gfx.Prim(PrimitiveType.Cylinder, root, new Vector3(Mathf.Cos(a) * .22f, .6f, Mathf.Sin(a) * .22f), new Vector3(.36f, .55f, .36f), red);
          }
          Gfx.Prim(PrimitiveType.Cylinder, root, new Vector3(0, .62f, 0), new Vector3(.72f, .08f, .72f), Gfx.Lit(Gfx.Hex("#e0b23a")));
          var fuse = Gfx.Prim(PrimitiveType.Cylinder, root, new Vector3(0, 1.3f, 0), new Vector3(.05f, .18f, .05f), Gfx.Lit(Gfx.Hex("#4a3a2a")));
          fuse.transform.localRotation = Quaternion.Euler(0, 0, 20);
          Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(-.06f, 1.5f, 0), Vector3.one * .16f, Gfx.NewLit(Gfx.Hex("#ffb347"), Gfx.Hex("#ff7a1a")));
          foreach (var x in new[] { -.12f, .12f }) Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(x, .8f, .42f), Vector3.one * .1f, ink);
          Gfx.Prim(PrimitiveType.Cube, root, new Vector3(0, .58f, .42f), new Vector3(.26f, .05f, .04f), ink);
          break;
        }
      }
      return root;
    }
  }
}
