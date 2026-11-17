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

    public static void ResetCaches() {
      _litCache.Clear();
      _unlitCache.Clear();
      _fadeShared = null;
      _cone = _disc = _ring = _quad = null;
      _mpb = null;
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
      var m = new Material(LitShader);
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
      m = new Material(UnlitShader);
      m.SetColor("_BaseColor", c);
      _unlitCache[c] = m;
      return m;
    }

    /// <summary>Transparent unlit material. Tint per renderer with SetTint.</summary>
    public static Material Fade {
      get {
        if (_fadeShared) return _fadeShared;
        var m = new Material(UnlitShader);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_Cull", 0f);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        _fadeShared = m;
        return m;
      }
    }

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
    static Mesh _cone, _disc, _ring, _quad;

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
