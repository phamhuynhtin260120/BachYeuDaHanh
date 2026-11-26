using System.Collections.Generic;
using UnityEngine;

namespace BachYeu {
  /// <summary>Procedural placeholder art: materials, primitives and simple meshes.</summary>
  public static class Gfx {
    static Shader _lit, _unlit;
    static readonly Dictionary<Color, Material> _litCache = new();
    static readonly Dictionary<Color, Material> _unlitCache = new();
    static Material _fadeShared;

    static Shader LitShader => _lit ? _lit : _lit = Shader.Find("Universal Render Pipeline/Simple Lit");
    static Shader UnlitShader => _unlit ? _unlit : _unlit = Shader.Find("Universal Render Pipeline/Unlit");

    // Template materials in Resources/GfxTemplates keep the shaders (and their keyword variants)
    // in player builds. Shader.Find alone would let the build strip them and draw everything pink.
    public const string TemplateDir = "GfxTemplates";
    static Material _tLit, _tLitEmissive, _tUnlit, _tFade;
    static bool _templatesLoaded;

    static void LoadTemplates() {
      if (_templatesLoaded) return;
      _templatesLoaded = true;
      _tLit = Resources.Load<Material>(TemplateDir + "/Lit");
      _tLitEmissive = Resources.Load<Material>(TemplateDir + "/LitEmissive");
      _tUnlit = Resources.Load<Material>(TemplateDir + "/Unlit");
      _tFade = Resources.Load<Material>(TemplateDir + "/Fade");
    }

    public static void ResetCaches() {
      _litCache.Clear();
      _unlitCache.Clear();
      _fadeShared = null;
      _cone = _disc = _ring = _quad = _card = _ground = null;
      _texFade.Clear();
      _mpb = null;
      _templatesLoaded = false;
      _tLit = _tLitEmissive = _tUnlit = _tFade = null;
    }

    /// <summary>Configures a material as transparent Unlit (used by the Editor to create the Fade template too).</summary>
    public static void MakeFade(Material m) {
      m.SetFloat("_Surface", 1f);
      m.SetFloat("_Blend", 0f);
      m.SetFloat("_Cull", 0f);
      m.SetFloat("_ZWrite", 0f);
      m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
      m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
      m.SetOverrideTag("RenderType", "Transparent");
      m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
      m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    public static Color Hex(string hex) {
      ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c);
      return c;
    }

    /// <summary>Shared lit material (do not tint per instance).</summary>
    public static Material Lit(Color c) {
      if (_litCache.TryGetValue(c, out var m)) return m;
      m = NewLit(c);
      _litCache[c] = m;
      return m;
    }

    /// <summary>A new lit material instance (safe to tint, e.g. for hit flash).</summary>
    public static Material NewLit(Color c, Color? emission = null) {
      LoadTemplates();
      var template = emission.HasValue ? (_tLitEmissive ? _tLitEmissive : _tLit) : _tLit;
      var m = template ? new Material(template) : new Material(LitShader);
      if (!emission.HasValue) m.DisableKeyword("_EMISSION");
      m.SetColor("_BaseColor", c);
      m.SetFloat("_Smoothness", 0f);
      if (emission.HasValue) {
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", emission.Value);
      }
      return m;
    }

    public static Material Unlit(Color c) {
      if (_unlitCache.TryGetValue(c, out var m)) return m;
      LoadTemplates();
      m = _tUnlit ? new Material(_tUnlit) : new Material(UnlitShader);
      m.SetColor("_BaseColor", c);
      _unlitCache[c] = m;
      return m;
    }

    /// <summary>Transparent unlit material. Tint per renderer with SetTint.</summary>
    public static Material Fade {
      get {
        if (_fadeShared) return _fadeShared;
        LoadTemplates();
        Material m;
        if (_tFade) m = new Material(_tFade);
        else { m = new Material(UnlitShader); MakeFade(m); }
        _fadeShared = m;
        return m;
      }
    }

    // ---------- textured fade materials (Resources/FxTex, baked from the Hovl Studio VFX textures) ----------
    public const string FxTexDir = "FxTex/";
    static readonly Dictionary<string, Material> _texFade = new();

    /// <summary>Fade material that draws a Resources/FxTex texture (white art with alpha: tint with SetTint).
    /// Falls back to the plain Fade material when the texture is missing.</summary>
    public static Material FadeTex(string name) {
      if (_texFade.TryGetValue(name, out var m) && m) return m;
      var tex = Resources.Load<Texture2D>(FxTexDir + name);
      if (!tex) { Debug.LogWarning($"[Gfx] Thiếu texture Resources/{FxTexDir}{name}.png"); return Fade; }
      m = new Material(Fade) { name = "Fade_" + name };
      m.SetTexture("_BaseMap", tex);
      m.mainTexture = tex;
      _texFade[name] = m;
      return m;
    }

    /// <summary>Texture radius (in units of the Ground mesh) where each ring texture draws its line,
    /// so callers can keep passing the radius they mean.</summary>
    public static float RingScale(string tex) => tex switch {
      "RingEdge" => 1.07f, "Fill" => 1.04f, "Shock" => 1.76f, "ZoneRing" => 1.12f, _ => 1f,
    };

    static MaterialPropertyBlock _mpb;
    public static void SetTint(Renderer r, Color c) {
      _mpb ??= new MaterialPropertyBlock();
      _mpb.SetColor("_BaseColor", c);
      r.SetPropertyBlock(_mpb);
    }

    public static GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m) {
      var go = GameObject.CreatePrimitive(t);
      Object.DestroyImmediate(go.GetComponent<Collider>());
      go.transform.SetParent(parent, false);
      go.transform.localPosition = pos;
      go.transform.localScale = scale;
      go.GetComponent<MeshRenderer>().sharedMaterial = m;
      return go;
    }

    public static GameObject MeshObj(Mesh mesh, Transform parent, Vector3 pos, Vector3 scale, Material m) {
      var go = new GameObject(mesh.name);
      go.transform.SetParent(parent, false);
      go.transform.localPosition = pos;
      go.transform.localScale = scale;
      go.AddComponent<MeshFilter>().sharedMesh = mesh;
      go.AddComponent<MeshRenderer>().sharedMaterial = m;
      return go;
    }

    // ---------- meshes ----------
    static Mesh _cone, _disc, _ring, _quad, _card, _ground;

    /// <summary>Unit quad (1 x 1) on the XY plane with UVs, for textured petals and particles.</summary>
    public static Mesh Card {
      get {
        if (_card) return _card;
        _card = new Mesh { name = "Card" };
        _card.SetVertices(new List<Vector3> { new(-.5f, -.5f, 0), new(.5f, -.5f, 0), new(.5f, .5f, 0), new(-.5f, .5f, 0) });
        _card.SetUVs(0, new List<Vector2> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) });
        _card.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
        _card.RecalculateNormals();
        return _card;
      }
    }

    /// <summary>Quad from -1 to 1 on the XZ plane with UVs, for textured rings and discs on the ground.</summary>
    public static Mesh Ground {
      get {
        if (_ground) return _ground;
        _ground = new Mesh { name = "GroundDecal" };
        _ground.SetVertices(new List<Vector3> { new(-1, 0, -1), new(1, 0, -1), new(1, 0, 1), new(-1, 0, 1) });
        _ground.SetUVs(0, new List<Vector2> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) });
        _ground.SetTriangles(new[] { 0, 3, 2, 0, 2, 1 }, 0);
        _ground.RecalculateNormals();
        return _ground;
      }
    }

    /// <summary>Textured ring or disc on the ground. radius = where the drawn line sits.</summary>
    public static GameObject GroundDecal(string tex, Transform parent, Vector3 pos, float radius) =>
      MeshObj(Ground, parent, pos, Vector3.one * radius * RingScale(tex), FadeTex(tex));

    /// <summary>Cone with base radius 0.5 at y=0 and apex at y=1.</summary>
    public static Mesh Cone {
      get {
        if (_cone) return _cone;
        const int seg = 14;
        var v = new List<Vector3> { new(0, 1, 0), Vector3.zero };
        for (int i = 0; i <= seg; i++) {
          float a = i / (float)seg * Mathf.PI * 2f;
          v.Add(new Vector3(Mathf.Cos(a) * .5f, 0, Mathf.Sin(a) * .5f));
        }
        var t = new List<int>();
        for (int i = 0; i < seg; i++) {
          t.AddRange(new[] { 0, 3 + i, 2 + i });
          t.AddRange(new[] { 1, 2 + i, 3 + i });
        }
        _cone = new Mesh { name = "Cone" };
        _cone.SetVertices(v);
        _cone.SetTriangles(t, 0);
        _cone.RecalculateNormals();
        return _cone;
      }
    }

    /// <summary>Flat disc of radius 1 on the XZ plane.</summary>
    public static Mesh Disc => _disc ? _disc : _disc = Arc(0f, 1f, 0f, 360f, 40, "Disc");

    /// <summary>Flat ring (inner .82, outer 1) on the XZ plane.</summary>
    public static Mesh Ring => _ring ? _ring : _ring = Arc(.82f, 1f, 0f, 360f, 48, "Ring");

    public static Mesh Quad {
      get {
        if (_quad) return _quad;
        _quad = new Mesh { name = "Petal" };
        _quad.SetVertices(new List<Vector3> { new(-.1f, -.065f, 0), new(.1f, -.065f, 0), new(.1f, .065f, 0), new(-.1f, .065f, 0) });
        _quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
        _quad.RecalculateNormals();
        return _quad;
      }
    }

    /// <summary>Flat arc on the XZ plane. Angles in degrees, 0 = +Z, clockwise seen from above.</summary>
    public static Mesh Arc(float inner, float outer, float startDeg, float lenDeg, int seg, string name = "Arc") {
      var v = new List<Vector3>();
      var t = new List<int>();
      for (int i = 0; i <= seg; i++) {
        float a = (startDeg + lenDeg * i / seg) * Mathf.Deg2Rad;
        var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
        v.Add(d * inner);
        v.Add(d * outer);
        if (i < seg) {
          int k = i * 2;
          t.AddRange(new[] { k, k + 1, k + 3, k, k + 3, k + 2 });
        }
      }
      var m = new Mesh { name = name };
      m.SetVertices(v);
      m.SetTriangles(t, 0);
      m.RecalculateNormals();
      return m;
    }
  }
}
