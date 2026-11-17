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

    public static void ApplyMask(Chibi c, int mask) {
      bool fox = mask == 0;
      c.Robe.sharedMaterial.SetColor("_BaseColor", fox ? Gfx.Hex("#c23a3a") : Gfx.Hex("#2f5f8a"));
      c.Face.sharedMaterial.SetColor("_BaseColor", fox ? Color.white : Gfx.Hex("#c0392b"));
      foreach (var e in c.Ears) e.SetActive(fox);
      c.Nose.SetActive(!fox);
      c.Stripe.SetActive(fox);
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
      }
      return root;
    }
  }
}
