using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace BachYeu {
  /// <summary>
  /// Bách yêu lục screen: the list of yokai on the left, a slowly turning model in the middle, the page on the right.
  /// Kinds you have not met yet show as an ink silhouette with only a hint of where to find them.
  /// The model is rendered by a small camera far below the arena into a texture, so it works over any scene.
  /// </summary>
  public class BestiaryPanel : MonoBehaviour {
    public static BestiaryPanel Current { get; private set; }

    const int StageLayer = 30;
    static readonly Vector3 Origin = new(0, -600, 0);
    const float FitSize = 2.5f, SpinSpeed = 28f;

    Action _onClose;
    int _sel;

    // list
    readonly Image[] _rowBg = new Image[Bestiary.All.Length];
    readonly Image[] _rowSeal = new Image[Bestiary.All.Length];
    readonly Text[] _rowSealText = new Text[Bestiary.All.Length];
    readonly Text[] _rowName = new Text[Bestiary.All.Length];
    readonly Text[] _rowSub = new Text[Bestiary.All.Length];
    // page
    Text _count, _name, _where, _lore, _tip, _stats, _tipTitle, _unknown;
    Image _kind; Text _kindText;
    // preview stage
    Transform _stage, _spin;
    Camera _cam;
    RenderTexture _rt;
    readonly List<Material> _mats = new(); // materials made for this screen only (flash copies, silhouette)
    Camera _mainCam; int _mainMask;
    bool _fogWas;

    public static void Open(Action onClose = null) {
      if (Current) return;
      SettingsPanel.EnsureEventSystem();
      var canvas = UiKit.Canvas("Bestiary", 88);
      var p = canvas.gameObject.AddComponent<BestiaryPanel>();
      p._onClose = onClose;
      p.BuildStage();
      p.Build((RectTransform)canvas.transform);
      Current = p;
      p.Select(p.FirstSeen());
      Sfx.Play(SoundId.UiOpen);
    }

    int FirstSeen() {
      for (int i = 0; i < Bestiary.All.Length; i++) if (Bestiary.Seen(Bestiary.All[i].Type)) return i;
      return 0;
    }

    public void Close() {
      Sfx.Play(SoundId.UiBack);
      var cb = _onClose;
      Destroy(gameObject);
      cb?.Invoke();
    }

    void OnDestroy() {
      if (Current == this) Current = null;
      RenderPipelineManager.beginCameraRendering -= BeforeRender;
      RenderPipelineManager.endCameraRendering -= AfterRender;
      if (_mainCam) _mainCam.cullingMask = _mainMask;
      if (_stage) Destroy(_stage.gameObject);
      if (_rt) { _rt.Release(); Destroy(_rt); }
      foreach (var m in _mats) if (m) Destroy(m);
    }

    static readonly Vector2 TL = new(0, 1), TR = new(1, 1), C = new(.5f, .5f), BR = new(1, 0), TC = new(.5f, 1);

    // ---------------- UI ----------------
    void Build(RectTransform root) {
      var dim = UiKit.Rect(root, "Dim", C, C, Vector2.zero, Vector2.zero);
      UiKit.Stretch(dim);
      dim.gameObject.AddComponent<Image>().color = new Color(.15f, .15f, .14f, .45f);

      var card = UiKit.Rect(root, "Card", C, C, Vector2.zero, new Vector2(1240, 780));
      card.gameObject.AddComponent<Image>().color = UiKit.InkPaper;
      UiKit.InkImage(card, "Wash", "Wash", C, C, Vector2.zero, new Vector2(1420, 920), new Color(1, 1, 1, .9f));

      UiKit.LabelAt(card, "Bách yêu lục", 48, UiKit.InkBlack, TL, TL, new Vector2(48, -24), new Vector2(500, 64), TextAnchor.MiddleLeft, FontStyle.Bold);
      UiKit.LabelAt(card, $"Sổ ghi các loài yêu quái ở phố Hồng Đăng. Gặp loài mới lần đầu: +{Bestiary.FirstSightReward} Hồn đăng.", 19, UiKit.InkGrey, TL, TL, new Vector2(50, -86), new Vector2(900, 28), TextAnchor.MiddleLeft);
      _count = UiKit.LabelAt(card, "", 30, UiKit.InkBlack, TR, TR, new Vector2(-48, -32), new Vector2(320, 50), TextAnchor.MiddleRight, FontStyle.Bold);
      UiKit.InkImage(card, "Rule", "BrushBar", TL, TL, new Vector2(40, -122), new Vector2(1160, 8), UiKit.InkBlack);

      // List.
      for (int i = 0; i < Bestiary.All.Length; i++) {
        int k = i;
        var row = UiKit.Panel(card, "Row" + i, TL, TL, new Vector2(44, -142 - i * 62), new Vector2(370, 56), new Color(1, 1, 1, .5f));
        _rowBg[i] = row;
        var b = row.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => { Sfx.Play(SoundId.UiClick); Select(k); });
        _rowSeal[i] = UiKit.SealTag(row.transform, "Seal", "", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(8, 0), new Vector2(44, 44), UiKit.InkGrey, true, 20);
        _rowSealText[i] = _rowSeal[i].GetComponentInChildren<Text>();
        _rowName[i] = UiKit.LabelAt(row.transform, "", 20, UiKit.InkBlack, TL, TL, new Vector2(62, -3), new Vector2(300, 28), TextAnchor.MiddleLeft, FontStyle.Bold);
        _rowSub[i] = UiKit.LabelAt(row.transform, "", 15, UiKit.InkGrey, TL, TL, new Vector2(62, -30), new Vector2(300, 22), TextAnchor.MiddleLeft);
      }

      // Model.
      var frame = UiKit.Panel(card, "Preview", TL, TL, new Vector2(434, -144), new Vector2(380, 450), new Color(1, 1, 1, .5f));
      var raw = UiKit.Rect(frame.transform, "Model", C, C, Vector2.zero, new Vector2(372, 442)).gameObject.AddComponent<RawImage>();
      raw.texture = _rt;
      raw.raycastTarget = false;
      _unknown = UiKit.LabelAt(frame.transform, "?", 120, new Color(1, 1, 1, .85f), C, C, new Vector2(0, 30), new Vector2(200, 160), TextAnchor.MiddleCenter, FontStyle.Bold);
      _unknown.raycastTarget = false;
      _kind = UiKit.SealTag(card, "Kind", "", TL, TL, new Vector2(434, -610), new Vector2(180, 44), UiKit.Seal, true, 20);
      _kindText = _kind.GetComponentInChildren<Text>();
      _stats = UiKit.LabelAt(card, "", 18, UiKit.InkGrey, TL, TL, new Vector2(434, -662), new Vector2(380, 50), TextAnchor.UpperLeft);

      // Page.
      var x = 840f;
      _name = UiKit.LabelAt(card, "", 34, UiKit.InkBlack, TL, TL, new Vector2(x, -140), new Vector2(360, 50), TextAnchor.MiddleLeft, FontStyle.Bold);
      _where = UiKit.LabelAt(card, "", 18, UiKit.Gold, TL, TL, new Vector2(x, -192), new Vector2(360, 50), TextAnchor.UpperLeft, FontStyle.Bold);
      _lore = UiKit.LabelAt(card, "", 21, UiKit.InkBlack, TL, TL, new Vector2(x, -248), new Vector2(360, 170), TextAnchor.UpperLeft);
      _lore.fontStyle = FontStyle.Italic;
      _lore.lineSpacing = 1.1f;
      _tipTitle = UiKit.LabelAt(card, "Cách đối phó", 20, UiKit.Seal, TL, TL, new Vector2(x, -428), new Vector2(360, 30), TextAnchor.MiddleLeft, FontStyle.Bold);
      _tip = UiKit.LabelAt(card, "", 20, UiKit.InkBlack, TL, TL, new Vector2(x, -462), new Vector2(360, 160), TextAnchor.UpperLeft);
      _tip.lineSpacing = 1.1f;

      UiKit.InkButton(card, "Xong", BR, BR, new Vector2(-44, 24), new Vector2(220, 74), false, Close, 26).name = "Done";
    }

    void Select(int i) {
      _sel = Mathf.Clamp(i, 0, Bestiary.All.Length - 1);
      Refresh();
      ShowModel(Bestiary.All[_sel]);
    }

    void Refresh() {
      _count.text = $"Đã gặp {Bestiary.SeenCount}/{Bestiary.Count}";
      for (int i = 0; i < Bestiary.All.Length; i++) {
        var e = Bestiary.All[i];
        bool seen = Bestiary.Seen(e.Type);
        _rowBg[i].color = i == _sel ? new Color(1, 1, 1, .95f) : new Color(1, 1, 1, .45f);
        _rowSeal[i].color = seen ? e.Color : new Color(.55f, .57f, .56f);
        _rowSealText[i].text = seen ? Initial(e.Name) : "?";
        _rowName[i].text = seen ? e.Name : "Chưa gặp";
        _rowName[i].color = seen ? UiKit.InkBlack : UiKit.InkGrey;
        _rowSub[i].text = seen ? $"{e.Kind} · đã hạ {Bestiary.Slain(e.Type)}" : e.Kind;
      }
      var s = Bestiary.All[_sel];
      bool known = Bestiary.Seen(s.Type);
      _unknown.enabled = !known;
      _kind.color = s.Kind == "Boss" ? UiKit.Seal : s.Color;
      _kindText.text = s.Kind;
      _name.text = known ? s.Name : "Chưa gặp";
      _where.text = "Gặp ở: " + s.Where;
      _lore.text = known ? s.Lore : "Trang này còn trống. Hãy gặp nó trong một đêm ở phố Hồng Đăng để ghi vào sổ.";
      _tipTitle.enabled = known;
      _tip.text = known ? s.Tip : "";
      _stats.text = known
        ? $"Máu {EnemyStats.Hp(s.Type)} · Tốc độ {Speed(s.Type)} · Va chạm {Contact(s.Type)}\nĐã gặp {Profile.Data.YokaiSeen[(int)s.Type]} lần · đã hạ {Bestiary.Slain(s.Type)}"
        : "";
    }

    static string Initial(string name) {
      var parts = name.Split(' ');
      return parts[0].Length > 0 ? parts[0].Substring(0, 1).ToUpper() : "?";
    }

    static string Speed(EnemyType t) {
      float v = EnemyStats.Speed(t);
      if (t == EnemyType.Umbrella || t == EnemyType.Elite) return "nhảy";
      return v >= 2.4f ? "nhanh" : v >= 1.5f ? "vừa" : "chậm";
    }

    static string Contact(EnemyType t) => t == EnemyType.Wisp ? "không" : EnemyStats.Contact(t).ToString();

    // ---------------- preview stage ----------------
    void BuildStage() {
      _rt = new RenderTexture(744, 884, 24, RenderTextureFormat.ARGB32) { name = "BestiaryRT", antiAliasing = 4 };
      _stage = new GameObject("BestiaryStage").transform;
      _stage.position = Origin;

      var camGo = new GameObject("BestiaryCamera");
      camGo.transform.SetParent(_stage, false);
      _cam = camGo.AddComponent<Camera>();
      _cam.clearFlags = CameraClearFlags.SolidColor;
      _cam.backgroundColor = Gfx.Hex("#e9e4d8");
      _cam.cullingMask = 1 << StageLayer;
      _cam.fieldOfView = 30;
      _cam.nearClipPlane = .1f; _cam.farClipPlane = 40;
      _cam.targetTexture = _rt;
      camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
      camGo.transform.position = Origin + new Vector3(0, 2.1f, -6.8f);
      camGo.transform.LookAt(Origin + new Vector3(0, 1.15f, 0));

      var lightGo = new GameObject("BestiaryLight");
      lightGo.transform.SetParent(_stage, false);
      var light = lightGo.AddComponent<Light>();
      light.type = LightType.Directional;
      light.intensity = 1.15f;
      light.color = Gfx.Hex("#fff3dd");
      light.cullingMask = 1 << StageLayer;
      lightGo.transform.rotation = Quaternion.Euler(40, -30, 0);

      // A soft ink puddle to stand on.
      Gfx.Prim(PrimitiveType.Cylinder, _stage, new Vector3(0, -.02f, 0), new Vector3(3.4f, .01f, 1.6f), Gfx.Unlit(Gfx.Hex("#cfc8b8")));

      _spin = new GameObject("Spin").transform;
      _spin.SetParent(_stage, false);
      _spin.position = Origin;
      SetLayer(_stage, StageLayer);

      // The arena camera must not see the stage; fog is turned off only while this camera draws.
      _mainCam = Camera.main;
      if (_mainCam) { _mainMask = _mainCam.cullingMask; _mainCam.cullingMask &= ~(1 << StageLayer); }
      RenderPipelineManager.beginCameraRendering += BeforeRender;
      RenderPipelineManager.endCameraRendering += AfterRender;
    }

    void BeforeRender(ScriptableRenderContext ctx, Camera cam) {
      if (cam != _cam) return;
      _fogWas = RenderSettings.fog;
      RenderSettings.fog = false;
    }

    void AfterRender(ScriptableRenderContext ctx, Camera cam) {
      if (cam == _cam) RenderSettings.fog = _fogWas;
    }

    void ShowModel(YokaiEntry e) {
      foreach (Transform c in _spin) Destroy(c.gameObject);
      foreach (var m in _mats) if (m) Destroy(m);
      _mats.Clear();
      _spin.rotation = Quaternion.Euler(0, 200, 0);

      var holder = new GameObject("Model").transform;
      holder.SetParent(_spin, false);
      var art = ArtSet.Current;
      var (prefab, _) = art ? art.EnemyPrefab(e.Type) : (null, 1f);
      if (prefab) {
        var v = AnimUtil.SpawnVisual(prefab, holder);
        var anim = v ? v.GetComponentInChildren<Animator>() : null;
        if (anim) { anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; anim.applyRootMotion = false; }
      } else {
        Models.BuildEnemy(holder, e.Type, _mats);
      }
      if (!Bestiary.Seen(e.Type)) {
        var ink = new Material(Gfx.Unlit(Gfx.Hex("#3a3836"))); // own copy: Gfx materials are shared
        _mats.Add(ink);
        foreach (var r in holder.GetComponentsInChildren<Renderer>(true)) {
          var arr = new Material[r.sharedMaterials.Length];
          for (int i = 0; i < arr.Length; i++) arr[i] = ink;
          r.sharedMaterials = arr;
        }
        foreach (var ps in holder.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
      }
      Fit(holder);
      SetLayer(holder, StageLayer);
    }

    // Scale the model so its largest side is FitSize, centered and standing on the stage.
    static void Fit(Transform model) {
      var rs = model.GetComponentsInChildren<Renderer>();
      if (rs.Length == 0) return;
      var b = rs[0].bounds;
      foreach (var r in rs) b.Encapsulate(r.bounds);
      float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
      if (size < .01f) return;
      float k = FitSize / size;
      model.localScale *= k;
      var center = model.parent.position + (b.center - model.parent.position) * k;
      var bottom = model.parent.position.y + (b.min.y - model.parent.position.y) * k;
      model.position += new Vector3(model.parent.position.x - center.x, model.parent.position.y - bottom, model.parent.position.z - center.z);
    }

    static void SetLayer(Transform t, int layer) {
      t.gameObject.layer = layer;
      foreach (Transform c in t) SetLayer(c, layer);
    }

    void Update() {
      if (_spin) _spin.Rotate(0, SpinSpeed * Time.unscaledDeltaTime, 0, Space.World);
      var k = UnityEngine.InputSystem.Keyboard.current;
      if (k == null) return;
      if (k.escapeKey.wasPressedThisFrame) Close();
      else if (k.downArrowKey.wasPressedThisFrame) Select((_sel + 1) % Bestiary.All.Length);
      else if (k.upArrowKey.wasPressedThisFrame) Select((_sel + Bestiary.All.Length - 1) % Bestiary.All.Length);
    }
  }
}
