using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace BachYeu {
  /// <summary>
  /// The hero standing in the middle of the hub: the model of the player's first mask, idling on a stage far below
  /// the arena, drawn by its own camera into a transparent RenderTexture shown with a RawImage. Drag to turn it.
  /// </summary>
  public class HubHero : MonoBehaviour, UnityEngine.EventSystems.IDragHandler {
    const int Layer = 29;
    static readonly Vector3 Origin = new(0, -900, 0);

    RenderTexture _rt;
    Transform _stage, _spin;
    Camera _cam, _mainCam;
    int _mainMask;
    bool _fogWas;
    int _mask = -1;
    float _yaw = 200;

    /// <summary>Put the hero view inside <paramref name="parent"/> at the given box.</summary>
    public static HubHero Create(RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size) {
      var rt = UiKit.Rect(parent, "Hero", anchor, anchor, pos, size);
      var raw = rt.gameObject.AddComponent<RawImage>();
      var h = rt.gameObject.AddComponent<HubHero>();
      h.Build(raw, size);
      return h;
    }

    void Build(RawImage raw, Vector2 size) {
      _rt = new RenderTexture((int)size.x, (int)size.y, 24, RenderTextureFormat.ARGB32) { name = "HubHeroRT", antiAliasing = 4 };
      raw.texture = _rt;
      raw.raycastTarget = true; // for dragging

      _stage = new GameObject("HubHeroStage").transform;
      _stage.position = Origin;
      var camGo = new GameObject("HubHeroCamera");
      camGo.transform.SetParent(_stage, false);
      _cam = camGo.AddComponent<Camera>();
      _cam.clearFlags = CameraClearFlags.SolidColor;
      _cam.backgroundColor = new Color(0, 0, 0, 0); // see-through: the hub backdrop shows behind the hero
      _cam.cullingMask = 1 << Layer;
      _cam.fieldOfView = 26;
      _cam.nearClipPlane = .1f; _cam.farClipPlane = 40;
      _cam.targetTexture = _rt;
      camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
      camGo.transform.position = Origin + new Vector3(0, 1.5f, -6.2f);
      camGo.transform.LookAt(Origin + new Vector3(0, 1.05f, 0));

      var lightGo = new GameObject("HubHeroLight");
      lightGo.transform.SetParent(_stage, false);
      var light = lightGo.AddComponent<Light>();
      light.type = LightType.Directional;
      light.intensity = 1.25f;
      light.color = Gfx.Hex("#ffe9c8");
      light.cullingMask = 1 << Layer;
      lightGo.transform.rotation = Quaternion.Euler(35, -25, 0);

      _spin = new GameObject("Spin").transform;
      _spin.SetParent(_stage, false);
      _spin.position = Origin;

      _mainCam = Camera.main;
      if (_mainCam) { _mainMask = _mainCam.cullingMask; _mainCam.cullingMask &= ~(1 << Layer); }
      RenderPipelineManager.beginCameraRendering += BeforeRender;
      RenderPipelineManager.endCameraRendering += AfterRender;
      Show(LocalProfile.MaskA);
    }

    /// <summary>Show the model for a mask (Masks.Fox ...).</summary>
    public void Show(int mask) {
      if (mask == _mask) return;
      _mask = mask;
      foreach (Transform c in _spin) Destroy(c.gameObject);
      var holder = new GameObject("Model").transform;
      holder.SetParent(_spin, false);
      var prefab = ArtSet.Current ? ArtSet.Current.PlayerModel(mask) : null;
      if (prefab) {
        var v = AnimUtil.SpawnVisual(prefab, holder);
        var anim = v ? v.GetComponentInChildren<Animator>() : null;
        if (anim) { anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; anim.applyRootMotion = false; }
      } else {
        var chibi = Models.BuildChibi(holder, Gfx.Hex("#c23a3a"), Gfx.Hex("#2b2420"));
        Models.ApplyMask(chibi, mask);
      }
      Fit(holder, 1.85f);
      SetLayer(_spin, Layer);
    }

    public void OnDrag(UnityEngine.EventSystems.PointerEventData e) => _yaw -= e.delta.x * .4f;

    void Update() {
      if (_spin) _spin.rotation = Quaternion.Euler(0, _yaw, 0);
      if (LocalProfile.MaskA != _mask) Show(LocalProfile.MaskA);
    }

    void BeforeRender(ScriptableRenderContext ctx, Camera cam) { if (cam == _cam) { _fogWas = RenderSettings.fog; RenderSettings.fog = false; } }
    void AfterRender(ScriptableRenderContext ctx, Camera cam) { if (cam == _cam) RenderSettings.fog = _fogWas; }

    void OnDestroy() {
      RenderPipelineManager.beginCameraRendering -= BeforeRender;
      RenderPipelineManager.endCameraRendering -= AfterRender;
      if (_mainCam) _mainCam.cullingMask = _mainMask;
      if (_stage) Destroy(_stage.gameObject);
      if (_rt) { _rt.Release(); Destroy(_rt); }
    }

    // Scale the model to the given height, standing centered on the stage.
    static void Fit(Transform model, float height) {
      var rs = model.GetComponentsInChildren<Renderer>();
      if (rs.Length == 0) return;
      var b = rs[0].bounds;
      foreach (var r in rs) b.Encapsulate(r.bounds);
      if (b.size.y < .01f) return;
      float k = height / b.size.y;
      model.localScale *= k;
      var p = model.parent.position;
      var center = p + (b.center - p) * k;
      var bottom = p.y + (b.min.y - p.y) * k;
      model.position += new Vector3(p.x - center.x, p.y - bottom, p.z - center.z);
    }

    static void SetLayer(Transform t, int layer) {
      t.gameObject.layer = layer;
      foreach (Transform c in t) SetLayer(c, layer);
    }
  }
}
