using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BachYeu.EditorTools {
  /// <summary>
  /// Builds the lantern post prefab (Assets/_Game/Art/Lantern/LanternPost.prefab) and puts it in ArtSet.LanternPost:
  /// a stone footing, a lacquered wooden post with a curved arm, and a ribbed red paper lantern with dark caps,
  /// gold bands and a tassel, hanging from the arm. Meshes are generated (lathe), saved in LanternMeshes.asset.
  /// Contract the game relies on (keep it if you swap in a modelled lantern): a child "Paper" whose renderer is the
  /// paper (tinted and lit by the game), a child "Swing" that the game sways, and a Light child "Glow".
  /// </summary>
  public static class LanternBuilder {
    const string Dir = "Assets/_Game/Art/Lantern";

    [MenuItem("Bách Yêu/Dựng lại cột lồng đèn")]
    public static void Build() {
      Directory.CreateDirectory(Dir);
      var meshPath = Dir + "/LanternMeshes.asset";
      AssetDatabase.DeleteAsset(meshPath);
      var holder = ScriptableObject.CreateInstance<LanternMeshHolder>();
      AssetDatabase.CreateAsset(holder, meshPath);
      Mesh Keep(Mesh m, string name) { m.name = name; AssetDatabase.AddObjectToAsset(m, holder); return m; }

      var wood = Mat("Wood", "#3b2619", .35f, 0);
      var stone = Mat("Stone", "#8a857a", .1f, 0);
      var gold = Mat("Gold", "#c9a24a", .55f, .6f);
      var paper = Mat("Paper", "#7a5a50", .15f, 0);
      var cap = Mat("Cap", "#1f1713", .45f, 0);
      var tassel = Mat("Tassel", "#9e2a1e", .1f, 0);

      var root = new GameObject("LanternPost");
      // Stone footing and the post.
      Part(root.transform, "Base", Keep(Lathe(new[] { (.34f, 0f), (.34f, .12f), (.26f, .16f), (.22f, .3f), (.17f, .34f) }, 8, 0), "Base"), stone, Vector3.zero);
      Part(root.transform, "Post", Keep(Box(.16f, 2.9f, .16f), "Post"), wood, new Vector3(0, .3f, 0));
      Part(root.transform, "PostCap", Keep(Lathe(new[] { (.0f, 0f), (.15f, 0f), (.15f, .05f), (.1f, .14f), (.0f, .2f) }, 8, 0), "PostCap"), gold, new Vector3(0, 3.2f, 0));
      // Curved arm reaching out over the lantern.
      var arm = Keep(Arm(), "Arm");
      Part(root.transform, "Arm", arm, wood, new Vector3(0, 2.95f, 0));
      // The swinging lantern: hook, cap, ribbed paper, bottom cap, tassel. Pivot at the arm tip.
      var swing = new GameObject("Swing").transform;
      swing.SetParent(root.transform, false);
      swing.localPosition = new Vector3(0, 3.06f, .62f);
      Part(swing, "Cord", Keep(Box(.02f, .2f, .02f), "Cord"), cap, new Vector3(0, -.2f, 0));
      Part(swing, "TopCap", Keep(Lathe(new[] { (.0f, .12f), (.1f, .12f), (.2f, .04f), (.22f, 0f) }, 16, 0), "TopCap"), cap, new Vector3(0, -.32f, 0));
      Part(swing, "TopBand", Keep(Lathe(new[] { (.22f, 0f), (.23f, .015f), (.23f, .035f), (.22f, .05f) }, 24, 0), "Band"), gold, new Vector3(0, -.36f, 0));
      var body = new List<(float r, float y)>();
      for (int i = 0; i <= 14; i++) { float t = i / 14f; body.Add((.2f + .22f * Mathf.Sin(t * Mathf.PI), -t * .78f)); }
      body.Reverse();
      Part(swing, "Paper", Keep(Lathe(body.ToArray(), 32, 10), "Paper"), paper, new Vector3(0, -.34f, 0));
      Part(swing, "BottomBand", Keep(Lathe(new[] { (.2f, 0f), (.21f, .015f), (.21f, .035f), (.2f, .05f) }, 24, 0), "Band2"), gold, new Vector3(0, -1.15f, 0));
      Part(swing, "BottomCap", Keep(Lathe(new[] { (.2f, .0f), (.18f, -.06f), (.08f, -.1f), (.0f, -.1f) }, 16, 0), "BottomCap"), cap, new Vector3(0, -1.13f, 0));
      Part(swing, "TasselKnot", Keep(Lathe(new[] { (.0f, 0f), (.04f, -.02f), (.04f, -.06f), (.0f, -.08f) }, 10, 0), "Knot"), gold, new Vector3(0, -1.23f, 0));
      Part(swing, "Tassel", Keep(Lathe(new[] { (.02f, 0f), (.05f, -.1f), (.07f, -.42f), (.0f, -.44f) }, 12, 0), "Tassel"), tassel, new Vector3(0, -1.31f, 0));

      var glow = new GameObject("Glow");
      glow.transform.SetParent(swing, false);
      glow.transform.localPosition = new Vector3(0, -.75f, 0);
      var light = glow.AddComponent<Light>();
      light.type = LightType.Point;
      light.color = new Color(1f, .62f, .3f);
      light.range = 6f;
      light.intensity = 0;
      light.shadows = LightShadows.None;

      var prefabPath = Dir + "/LanternPost.prefab";
      var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
      Object.DestroyImmediate(root);
      var art = Resources.Load<ArtSet>("ArtSet");
      if (art) { art.LanternPost = prefab; EditorUtility.SetDirty(art); }
      AssetDatabase.SaveAssets();
      Debug.Log("[Bách Yêu] Đã dựng cột lồng đèn: " + prefabPath);
    }

    // ---------------- mesh helpers ----------------

    /// <summary>Surface of revolution through (radius, y) points; <paramref name="ribs"/> &gt; 0 adds paper ribs.</summary>
    static Mesh Lathe((float r, float y)[] pts, int seg, int ribs) {
      var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
      for (int i = 0; i < pts.Length; i++)
        for (int j = 0; j <= seg; j++) {
          float a = j / (float)seg * Mathf.PI * 2;
          float r = pts[i].r;
          if (ribs > 0) r *= 1f - .045f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a * ribs / 2f)), 8); // grooves between the ribs
          v.Add(new Vector3(Mathf.Cos(a) * r, pts[i].y, Mathf.Sin(a) * r));
          uv.Add(new Vector2(j / (float)seg, i / (float)(pts.Length - 1)));
        }
      int w = seg + 1;
      for (int i = 0; i < pts.Length - 1; i++)
        for (int j = 0; j < seg; j++) {
          int a = i * w + j, b = a + w;
          tri.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
        }
      var m = new Mesh { vertices = v.ToArray(), uv = uv.ToArray(), triangles = tri.ToArray() };
      if (pts[0].y < pts[pts.Length - 1].y) m.triangles = Flip(m.triangles); // keep normals pointing out whichever way the profile runs
      m.RecalculateNormals(); m.RecalculateBounds();
      return m;
    }

    static int[] Flip(int[] t) { for (int i = 0; i < t.Length; i += 3) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]); return t; }

    static Mesh Box(float x, float y, float z) {
      var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
      var src = go.GetComponent<MeshFilter>().sharedMesh;
      var verts = src.vertices;
      for (int i = 0; i < verts.Length; i++) verts[i] = Vector3.Scale(verts[i] + new Vector3(0, .5f, 0), new Vector3(x, y, z));
      var m = new Mesh { vertices = verts, normals = src.normals, uv = src.uv, triangles = src.triangles };
      m.RecalculateBounds();
      Object.DestroyImmediate(go);
      return m;
    }

    /// <summary>A square beam curving up and out from the post top to the hanging point (local +Z).</summary>
    static Mesh Arm() {
      const int n = 10; const float s = .055f;
      var v = new List<Vector3>(); var tri = new List<int>();
      for (int i = 0; i <= n; i++) {
        float t = i / (float)n;
        var c = new Vector3(0, Mathf.Sin(t * Mathf.PI) * .16f + t * .06f, t * .7f);
        v.Add(c + new Vector3(-s, -s, 0)); v.Add(c + new Vector3(s, -s, 0)); v.Add(c + new Vector3(s, s, 0)); v.Add(c + new Vector3(-s, s, 0));
      }
      for (int i = 0; i < n; i++)
        for (int k = 0; k < 4; k++) {
          int a = i * 4 + k, b = i * 4 + (k + 1) % 4, c = a + 4, d = b + 4;
          tri.AddRange(new[] { a, c, b, b, c, d });
        }
      var m = new Mesh { vertices = v.ToArray(), triangles = tri.ToArray() };
      m.RecalculateNormals(); m.RecalculateBounds();
      return m;
    }

    static void Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos) {
      var go = new GameObject(name);
      go.transform.SetParent(parent, false);
      go.transform.localPosition = pos;
      go.AddComponent<MeshFilter>().sharedMesh = mesh;
      go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static Material Mat(string name, string hex, float smooth, float metal) {
      var path = $"{Dir}/M_Lantern_{name}.mat";
      var m = AssetDatabase.LoadAssetAtPath<Material>(path);
      bool created = !m;
      if (created) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
      ColorUtility.TryParseHtmlString(hex, out var c);
      m.SetColor("_BaseColor", c);
      m.SetFloat("_Smoothness", smooth);
      m.SetFloat("_Metallic", metal);
      m.SetFloat("_Cull", 0); // two-sided: lathe parts are open shells
      if (created) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
      return m;
    }
  }
}
