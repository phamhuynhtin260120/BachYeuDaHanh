using System.Collections.Generic;
using UnityEngine;

namespace BachYeu {
  /// <summary>Builds the floating-island arena, fog and falling petals from primitives.</summary>
  public class ArenaBuilder : MonoBehaviour {
    public const float PlayRadius = 12f;

    public struct Season { public string Name; public Color Bg, Ground, Petal; public Color[] Leaves; }
    public static readonly Season[] Seasons = {
      new() { Name = "Xuân", Bg = Gfx.Hex("#ece4d6"), Ground = Gfx.Hex("#a9c48a"), Petal = Gfx.Hex("#f4a7b9"), Leaves = new[] { Gfx.Hex("#f2b8c6"), Gfx.Hex("#f7d3dc"), Gfx.Hex("#e89aae") } },
      new() { Name = "Hạ", Bg = Gfx.Hex("#e4e7d8"), Ground = Gfx.Hex("#8fbf7a"), Petal = Gfx.Hex("#e8f59a"), Leaves = new[] { Gfx.Hex("#5f9e6e"), Gfx.Hex("#7fb88a"), Gfx.Hex("#4c8a63") } },
      new() { Name = "Thu", Bg = Gfx.Hex("#ebdfcf"), Ground = Gfx.Hex("#c9b27a"), Petal = Gfx.Hex("#e0803f"), Leaves = new[] { Gfx.Hex("#d9773f"), Gfx.Hex("#e8a050"), Gfx.Hex("#c25a32") } },
      new() { Name = "Đông", Bg = Gfx.Hex("#e1e5ea"), Ground = Gfx.Hex("#eef2f5"), Petal = Gfx.Hex("#ffffff"), Leaves = new[] { Gfx.Hex("#f4f6f8"), Gfx.Hex("#dfe7ee"), Gfx.Hex("#c9d6e0") } },
    };

    [Range(0, 3)] public int SeasonIndex;

    readonly List<Transform> _petals = new();
    Renderer _ground;
    readonly List<Renderer> _leaves = new();

    void Awake() {
      Build();
      ApplySeason(SeasonIndex);
    }

    void Build() {
      var root = transform;
      var env = ArtSet.Current ? ArtSet.Current.Environment : null;
      if (env) {
        AnimUtil.SpawnVisual(env, root);
        BuildPetals(root);
        return;
      }
      _ground =Gfx.Prim(PrimitiveType.Cylinder, root, new Vector3(0, -.8f, 0), new Vector3(30, .8f, 30), Gfx.NewLit(Color.white)).GetComponent<Renderer>();
      var under = Gfx.MeshObj(Gfx.Cone, root, new Vector3(0, -1.6f, 0), new Vector3(22, -9, 22), Gfx.Lit(Gfx.Hex("#8f8478")));
      under.name = "Underside";
      var path = Gfx.Prim(PrimitiveType.Cube, root, new Vector3(0, .005f, 5), new Vector3(2.6f, .01f, 11), Gfx.Lit(Gfx.Hex("#d8cdb8")));
      path.name = "Path";

      // Torii gate at the far (+Z) end.
      var red = Gfx.Lit(Gfx.Hex("#b3392b"));
      foreach (var x in new[] { -1.8f, 1.8f })
        Gfx.Prim(PrimitiveType.Cylinder, root, new Vector3(x, 1.8f, 13), new Vector3(.44f, 1.8f, .44f), red);
      Gfx.Prim(PrimitiveType.Cube, root, new Vector3(0, 3.6f, 13), new Vector3(5.2f, .3f, .45f), red);
      Gfx.Prim(PrimitiveType.Cube, root, new Vector3(0, 2.95f, 13), new Vector3(4.3f, .2f, .25f), red);

      // Moon.
      Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(3, 11, 36), Vector3.one * 6, Gfx.Unlit(Gfx.Hex("#fff1c9")));

      // Trees around the rim.
      var trunk = Gfx.Lit(Gfx.Hex("#6b4a3a"));
      float[,] trees = { { -9.5f, 8, 1.2f }, { 9.5f, 7.5f, 1.1f }, { -13, -1, 1 }, { 13, -2, 1.3f }, { -6, 12, .9f }, { 6.5f, 12, 1 }, { -12, 5, .8f }, { 12.5f, 4, .8f }, { -10, -8, 1 }, { 10.5f, -8.5f, .9f } };
      for (int i = 0; i < trees.GetLength(0); i++) {
        float x = trees[i, 0], z = trees[i, 1], s = trees[i, 2];
        Gfx.Prim(PrimitiveType.Cylinder, root, new Vector3(x, s, z), new Vector3(.4f * s, s, .4f * s), trunk);
        float[,] blobs = { { 0, 2.3f, 0, 1.2f }, { .7f, 2, .3f, .85f }, { -.6f, 2.1f, -.2f, .9f }, { .1f, 2.9f, -.1f, .8f } };
        for (int b = 0; b < 4; b++) {
          var leaf = Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(x + blobs[b, 0] * s, blobs[b, 1] * s, z + blobs[b, 2] * s), Vector3.one * blobs[b, 3] * 2 * s, Gfx.NewLit(Color.white));
          _leaves.Add(leaf.GetComponent<Renderer>());
        }
      }

      // Rocks and decorative lantern posts.
      var rock = Gfx.Lit(Gfx.Hex("#9a948a"));
      float[,] rocks = { { -4.5f, 9 }, { 5, 10 }, { -11, -5 }, { 11, -6 }, { 4, -12 }, { -4, -12.3f } };
      for (int i = 0; i < rocks.GetLength(0); i++) {
        var r = Gfx.Prim(PrimitiveType.Cube, root, new Vector3(rocks[i, 0], .3f, rocks[i, 1]), Vector3.one * (1f + i % 3 * .5f), rock);
        r.transform.rotation = Quaternion.Euler(i * 25, i * 50, 10);
      }
      var post = Gfx.Lit(Gfx.Hex("#4a3426"));
      var lamp = Gfx.NewLit(Gfx.Hex("#c0392b"), Gfx.Hex("#9a2a1a"));
      foreach (var p in new[] { new Vector2(-2, 2), new Vector2(2, 2), new Vector2(-2, 7), new Vector2(2, 7) }) {
        Gfx.Prim(PrimitiveType.Cylinder, root, new Vector3(p.x, .7f, p.y), new Vector3(.1f, .7f, .1f), post);
        Gfx.Prim(PrimitiveType.Cylinder, root, new Vector3(p.x, 1.55f, p.y), new Vector3(.44f, .21f, .44f), lamp);
      }

      BuildPetals(root);
    }

    void BuildPetals(Transform root) {
      var petalRoot = new GameObject("Petals").transform;
      petalRoot.SetParent(root, false);
      for (int i = 0; i < 160; i++) {
        var q = Gfx.MeshObj(Gfx.Quad, petalRoot, new Vector3(Random.Range(-17f, 17f), Random.Range(0f, 14f), Random.Range(-17f, 17f)), Vector3.one * 1.2f, Gfx.Fade);
        q.transform.rotation = Random.rotation;
        _petals.Add(q.transform);
      }
    }

    public void ApplySeason(int i) {
      var s = Seasons[Mathf.Clamp(i, 0, 3)];
      if (_ground) _ground.sharedMaterial.SetColor("_BaseColor", s.Ground);
      for (int k = 0; k < _leaves.Count; k++) _leaves[k].sharedMaterial.SetColor("_BaseColor", s.Leaves[k % 3]);
      foreach (var p in _petals) Gfx.SetTint(p.GetComponent<Renderer>(), new Color(s.Petal.r, s.Petal.g, s.Petal.b, .85f));
      if (Fx.I) Fx.I.PetalColor = s.Petal;

      RenderSettings.fog = true;
      RenderSettings.fogMode = FogMode.Linear;
      RenderSettings.fogColor = s.Bg;
      RenderSettings.fogStartDistance = 22;
      RenderSettings.fogEndDistance = 52;
      RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
      RenderSettings.ambientLight = Gfx.Hex("#b9ad9c");
      var cam = Camera.main;
      if (cam) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = s.Bg; }
    }

    void Update() {
      float dt = Time.deltaTime, t = Time.time;
      for (int i = 0; i < _petals.Count; i++) {
        var p = _petals[i];
        var pos = p.localPosition;
        pos.y -= dt * (.6f + i % 5 * .15f);
        pos.x += Mathf.Sin(t + i) * dt * .4f;
        if (pos.y < -1) pos.y = 14;
        p.localPosition = pos;
        p.Rotate(40 * dt, 60 * dt, 0);
      }
    }
  }
}
