using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BachYeu {
  /// <summary>
  /// Character select (scene CharacterSelect.unity). Each character is one mask: you take two into a match.
  /// The scene is loaded on top of the game (additive), so it works from the title menu and from the room lobby
  /// without dropping the connection. Everything is built from code; the scene only holds this component
  /// and the prefab references below.
  /// </summary>
  public class CharacterSelect : MonoBehaviour {
    public const string SceneName = "CharacterSelect";
    const int StageLayer = 30;                          // stage objects render only to the stage camera
    static readonly Vector3 Hidden = new(0, -300, 0);   // far below the arena (no arena loaded: standalone test)
    static readonly Vector3 OnArena = new(0, 0, -6.5f); // on the real arena, in front of the lotus pond
    static Vector3 Origin = Hidden;
    /// <summary>The line-up stands in the real arena (living background) instead of on a blank stage.</summary>
    bool _live;

    [Tooltip("Model shown on the stage per mask, in order Hồ Ly, Tengu, Hà Bá, Quỷ Đá. Use the pack prefab (it has appear/display clips). Empty = ArtSet player model.")]
    public GameObject[] Showcase = new GameObject[4];
    [Tooltip("Looping effect under the character in focus, per mask (same order). Optional.")]
    public GameObject[] FocusFx = new GameObject[4];
    [Tooltip("Scale of every character on the stage (the packs are already about the same size)")]
    public float ShowScale = 1.1f;
    const float Spacing = 2.3f, CamDist = 13f, CamShift = 1.6f; // CamShift keeps the line-up left of the info scroll

    static Action _onDone;

    /// <summary>Open the select screen on top of whatever is running. Falls back to the flat mask picker if the scene is not in the build.</summary>
    /// <summary>True when the last select screen closed with "Xong" (not cancelled).</summary>
    public static bool Confirmed { get; private set; }

    public static void Open(Action onDone) {
      if (SceneManager.GetSceneByName(SceneName).isLoaded) return;
      Confirmed = false;
      if (!Application.CanStreamedLevelBeLoaded(SceneName)) { Confirmed = true; MaskPicker.Open(onDone); return; }
      _onDone = onDone;
      SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
    }

    class Slot {
      public int Mask;
      public Transform Root;
      public Animator Anim;
      public GameObject Fx;
      public string Idle, Display, Appear;
      public float DisplayLen;
      public float BackToIdleAt;
      public Vector3 Home;
    }

    readonly List<Slot> _slots = new();
    readonly List<int> _picked = new();
    int _focus;
    Camera _cam;

    // what we hide while open, put back on close
    Camera _hiddenCam;
    readonly List<Canvas> _hiddenCanvases = new();
    bool _fogWas;

    // UI
    Text _name, _element, _info, _status, _pickLabel;
    Image _role;
    Text _roleText;
    Button _ok, _pickBtn;
    readonly List<(Image card, Image stamp, Text stampText)> _tabs = new();

    void Start() {
      foreach (var r in gameObject.scene.GetRootGameObjects()) UiKit.Reskin(r.transform);
      _live = ArenaBuilder.I;
      Origin = _live ? new Vector3(OnArena.x, GroundMover.GroundY(OnArena), OnArena.z) : Hidden;
      HideGame();
      BuildStage();
      BuildUi();
      LocalProfile.LoadMasks();
      _picked.Add(LocalProfile.MaskA);
      _picked.Add(LocalProfile.MaskB);
      Focus(_picked[0], true);
      foreach (var s in _slots) Play(s, s.Appear, s.Idle);
    }

    // ---------------- hide / restore the game underneath ----------------
    void HideGame() {
      _hiddenCam = Camera.main;
      if (_hiddenCam && _hiddenCam.gameObject.scene != gameObject.scene) _hiddenCam.enabled = false; else _hiddenCam = null;
      foreach (var c in FindObjectsByType<Canvas>())
        if (c.isRootCanvas && c.enabled && c.gameObject.scene != gameObject.scene) { c.enabled = false; _hiddenCanvases.Add(c); }
      _fogWas = RenderSettings.fog;
      if (!_live) RenderSettings.fog = false; // the arena keeps its mist: far scenery fades, the line-up stays clear
      if (!EventSystem.current) {
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        SceneManager.MoveGameObjectToScene(es, gameObject.scene);
      }
    }

    void RestoreGame() {
      if (_hiddenCam) _hiddenCam.enabled = true;
      foreach (var c in _hiddenCanvases) if (c) c.enabled = true;
      RenderSettings.fog = _fogWas;
    }

    // ---------------- stage ----------------
    void BuildStage() {
      var camGo = new GameObject("StageCamera");
      SceneManager.MoveGameObjectToScene(camGo, gameObject.scene);
      _cam = camGo.AddComponent<Camera>();
      if (_live && _hiddenCam) _cam.CopyFrom(_hiddenCam); // same sky, fog colour and post as the arena camera
      _cam.clearFlags = CameraClearFlags.SolidColor;
      _cam.backgroundColor = _live && _hiddenCam ? _hiddenCam.backgroundColor : UiKit.InkPaper;
      _cam.cullingMask = _live ? ~(1 << 29) : 1 << StageLayer; // live: the whole arena (not the hub hero's private stage)
      _cam.fieldOfView = 34;
      _cam.nearClipPlane = .1f; _cam.farClipPlane = 80;
      _cam.depth = 50;
      var data = camGo.AddComponent<UniversalAdditionalCameraData>();
      data.renderPostProcessing = true;   // lets the scene's bloom light up the circles
      camGo.transform.position = Origin + new Vector3(CamShift, 2.8f, -CamDist);
      camGo.transform.LookAt(Origin + new Vector3(CamShift, 1f, 0));

      var lightGo = new GameObject("StageLight");
      SceneManager.MoveGameObjectToScene(lightGo, gameObject.scene);
      var light = lightGo.AddComponent<Light>();
      light.type = LightType.Directional;
      light.intensity = _live ? .45f : 1.1f; // live: a soft key light on top of the arena's sun
      light.color = Gfx.Hex("#fff3dd");
      light.cullingMask = 1 << StageLayer;
      light.shadows = LightShadows.Soft;
      lightGo.transform.rotation = Quaternion.Euler(38, -28, 0);

      // Blank stage only (no arena): a dark stone disc with a pale rim, and a large ink circle behind.
      if (!_live) {
        Gfx.Prim(PrimitiveType.Cylinder, transform, Origin + new Vector3(0, -.15f, .6f), new Vector3(13.5f, .15f, 6.5f), Gfx.Lit(Gfx.Hex("#4a423b")));
        Gfx.Prim(PrimitiveType.Cylinder, transform, Origin + new Vector3(0, -.17f, .6f), new Vector3(14.1f, .14f, 7.0f), Gfx.Lit(Gfx.Hex("#d9ceb6")));
        var moon = Gfx.Prim(PrimitiveType.Cylinder, transform, Origin + new Vector3(0, 3.4f, 9f), new Vector3(9f, .05f, 9f), Gfx.Unlit(Gfx.Hex("#e6dcc6")));
        moon.transform.rotation = Quaternion.Euler(90, 0, 0);
      }

      for (int i = 0; i < Masks.All.Length; i++) {
        var s = new Slot { Mask = i };
        float x = (i - (Masks.All.Length - 1) / 2f) * Spacing;
        s.Home = Origin + new Vector3(x, 0, Mathf.Abs(x) * .18f);
        if (_live) s.Home.y = GroundMover.GroundY(s.Home); // stand on the arena floor
        s.Root = new GameObject("Char_" + Masks.All[i].Name).transform;
        s.Root.SetParent(transform, false);
        s.Root.position = s.Home;
        s.Root.rotation = Quaternion.Euler(0, 180 - x * 4f, 0);  // face the camera, turned a little inwards

        var prefab = i < Showcase.Length && Showcase[i] ? Showcase[i] : ArtSet.Current ? ArtSet.Current.PlayerModel(i) : null;
        if (prefab) {
          var model = AnimUtil.SpawnVisual(prefab, s.Root);
          model.transform.localScale *= ShowScale;
          s.Anim = model.GetComponentInChildren<Animator>();
          if (s.Anim) {
            s.Anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            s.Anim.applyRootMotion = false;
            s.Idle = FindState(s.Anim, prefab.name, "idle");
            s.Display = FindState(s.Anim, prefab.name, "display") ?? FindState(s.Anim, prefab.name, "skill") ?? FindState(s.Anim, prefab.name, "attack");
            s.Appear = FindState(s.Anim, prefab.name, "appear");
            s.DisplayLen = ClipLength(s.Anim, "display", "skill", "attack");
          }
        } else {
          Models.BuildChibi(s.Root, Masks.All[i].Color, Gfx.Hex("#2b2420"));
        }

        if (i < FocusFx.Length && FocusFx[i]) {
          s.Fx = Instantiate(FocusFx[i], s.Root);
          s.Fx.transform.localPosition = new Vector3(0, .03f, 0);
          s.Fx.transform.localScale = FocusFx[i].transform.localScale * .9f;
          s.Fx.SetActive(false);
        }
        SetLayer(s.Root, StageLayer);
        _slots.Add(s);
      }
      SetLayer(transform, StageLayer);
    }

    static void SetLayer(Transform t, int layer) {
      t.gameObject.layer = layer;
      foreach (Transform c in t) SetLayer(c, layer);
    }

    static void FitHeight(Transform model, float height) {
      var rs = model.GetComponentsInChildren<Renderer>();
      if (rs.Length == 0) return;
      var b = rs[0].bounds;
      foreach (var r in rs) b.Encapsulate(r.bounds);
      if (b.size.y < .01f) return;
      model.localScale *= height / b.size.y;
    }

    // Pack controllers name their states "<Model>_idle", "idle", "idle02"...: take the exact one first.
    static string FindState(Animator a, string model, string key) {
      foreach (var n in new[] { $"{model}_{key}", key, $"{model}_{key}01", key + "01" })
        if (a.HasState(0, Animator.StringToHash(n))) return n;
      return null;
    }

    static float ClipLength(Animator a, params string[] keys) {
      if (!a.runtimeAnimatorController) return 1.5f;
      foreach (var k in keys)
        foreach (var c in a.runtimeAnimatorController.animationClips)
          if (c && c.name.ToLower().Contains(k) && !c.name.Contains("02")) return c.length;
      return 1.5f;
    }

    void Play(Slot s, string state, string then) {
      if (!s.Anim) return;
      if (state != null) {
        s.Anim.CrossFadeInFixedTime(state, .15f, 0, 0);
        s.BackToIdleAt = then != null ? Time.time + Mathf.Max(.5f, ClipLength(s.Anim, state.ToLower().Contains("appear") ? "appear" : "display", "skill") - .15f) : 0;
      } else if (then != null) s.Anim.CrossFadeInFixedTime(then, .2f, 0);
    }

    // ---------------- UI ----------------
    void BuildUi() {
      var canvas = UiKit.Canvas("CharacterSelectUi", 60);
      SceneManager.MoveGameObjectToScene(canvas.gameObject, gameObject.scene);
      var root = (RectTransform)canvas.transform;
      var TC = new Vector2(.5f, 1); var BC = new Vector2(.5f, 0); var R = new Vector2(1, .5f); var BL = new Vector2(0, 0); var BR = new Vector2(1, 0);
      var ink = UiKit.InkBlack; var grey = UiKit.InkGrey;

      var title = UiKit.LabelAt(root, "Chọn nhân vật", 46, ink, TC, TC, new Vector2(0, -22), new Vector2(900, 56), TextAnchor.MiddleCenter, FontStyle.Bold);
      var hint = UiKit.LabelAt(root, "Mỗi nhân vật là một mặt nạ. Mang theo 2: nhân vật chọn trước ra trận trước, bấm Đổi để chuyển sang người còn lại.",
        19, grey, TC, TC, new Vector2(0, -76), new Vector2(1200, 28), TextAnchor.MiddleCenter);
      if (_live) foreach (var l in new[] { title, hint }) { var o = l.gameObject.AddComponent<Outline>(); o.effectColor = new Color(.97f, .95f, .9f, .9f); o.effectDistance = new Vector2(2, -2); }

      // Info scroll on the right.
      var info = UiKit.InkImage(root, "Info", "Scroll", R, R, new Vector2(-18, 30), new Vector2(380, 600), Color.white);
      var t = info.transform;
      _role = UiKit.SealTag(t, "Role", "", TC, TC, new Vector2(0, -72), new Vector2(140, 34), Color.white, true, 17);
      _roleText = _role.GetComponentInChildren<Text>();
      _name = UiKit.LabelAt(t, "", 38, ink, TC, TC, new Vector2(0, -116), new Vector2(300, 48), TextAnchor.MiddleCenter, FontStyle.Bold);
      _element = UiKit.LabelAt(t, "", 18, ink, TC, TC, new Vector2(0, -158), new Vector2(300, 26), TextAnchor.MiddleCenter, FontStyle.Bold);
      _info = UiKit.LabelAt(t, "", 16, grey, TC, TC, new Vector2(0, -194), new Vector2(286, 330), TextAnchor.UpperLeft);
      _info.supportRichText = true;
      _info.lineSpacing = 1.08f;
      _pickBtn = UiKit.InkButton(t, "Mang theo", BC, BC, new Vector2(0, 54), new Vector2(250, 76), true, TogglePick, 24);
      _pickLabel = _pickBtn.GetComponentInChildren<Text>();

      // One tab per character along the bottom.
      int n = Masks.All.Length;
      for (int i = 0; i < n; i++) {
        int id = i;
        var def = Masks.All[i];
        var card = UiKit.InkImage(root, "Tab_" + def.Name, "Wash", BC, BC, new Vector2((i - (n - 1) / 2f) * 210 - 160, 120), new Vector2(196, 86), Color.white);
        card.raycastTarget = true;
        card.gameObject.AddComponent<Button>().onClick.AddListener(() => Focus(id, false));
        UiKit.LabelAt(card.transform, def.Name, 26, ink, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 8), new Vector2(180, 34), TextAnchor.MiddleCenter, FontStyle.Bold);
        UiKit.LabelAt(card.transform, def.Role, 15, def.Color, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -20), new Vector2(180, 22), TextAnchor.MiddleCenter, FontStyle.Bold);
        var stamp = UiKit.SealTag(card.transform, "Pick", "1", new Vector2(1, 1), new Vector2(1, 1), new Vector2(4, 12), new Vector2(40, 40), UiKit.Seal, true, 22);
        _tabs.Add((card, stamp, stamp.GetComponentInChildren<Text>()));
      }

      _status = UiKit.LabelAt(root, "", 22, ink, BL, BL, new Vector2(40, 40), new Vector2(700, 32), TextAnchor.MiddleLeft, FontStyle.Bold);
      _ok = UiKit.InkButton(root, "Xong", BR, BR, new Vector2(-30, 22), new Vector2(300, 92), true, Confirm, 28);
      UiKit.InkButton(root, "Huỷ", BR, BR, new Vector2(-350, 30), new Vector2(200, 72), false, Close, 24);
    }

    void Focus(int mask, bool instant) {
      _focus = mask;
      var def = Masks.All[mask];
      _name.text = def.Name;
      _element.text = "Hệ " + def.Element.ToLower();
      _element.color = def.Color;
      _role.color = def.Color;
      _roleText.text = def.Role;
      _info.text = $"<b>Đánh thường:</b> {def.Attack}\n\n<b>{def.S1}:</b> {def.S1Desc}\n\n<b>{def.S2}:</b> {def.S2Desc}\n\n<b>{def.Ult} (tuyệt kỹ):</b> {def.UltDesc}\n\n<b>Khi đổi vào:</b> {def.SwapDesc}";
      foreach (var s in _slots) if (s.Fx) s.Fx.SetActive(s.Mask == mask);
      var f = _slots[mask];
      if (!instant) Play(f, f.Display, f.Idle);
      Refresh();
    }

    void TogglePick() {
      if (_picked.Contains(_focus)) _picked.Remove(_focus);
      else if (_picked.Count < 2) _picked.Add(_focus);
      else { _picked.RemoveAt(1); _picked.Add(_focus); } // already two: replace the second
      Refresh();
    }

    void Refresh() {
      for (int i = 0; i < _tabs.Count; i++) {
        int order = _picked.IndexOf(i);
        var (card, stamp, text) = _tabs[i];
        stamp.gameObject.SetActive(order >= 0);
        if (order >= 0) text.text = (order + 1).ToString();
        card.color = i == _focus ? Color.white : new Color(1, 1, 1, .6f);
        card.transform.localScale = Vector3.one * (i == _focus ? 1.06f : 1f);
      }
      int me = _picked.IndexOf(_focus);
      _pickLabel.text = me >= 0 ? "Bỏ ra" : _picked.Count < 2 ? "Mang theo" : "Thay người thứ 2";
      bool ready = _picked.Count == 2;
      _status.text = ready ? $"Mang theo: 1. {Masks.All[_picked[0]].Name}   2. {Masks.All[_picked[1]].Name}" : $"Chọn thêm {2 - _picked.Count} nhân vật";
      _ok.interactable = ready;
    }

    void Update() {
      // Tap a character on the stage to look at it.
      var p = Pointer.current;
      if (p != null && p.press.wasPressedThisFrame && _cam && !(EventSystem.current && EventSystem.current.IsPointerOverGameObject())) {
        var pos = p.position.ReadValue();
        int best = -1; float bestD = 140f;
        foreach (var s in _slots) {
          var sp = _cam.WorldToScreenPoint(s.Root.position + Vector3.up);
          float d = Vector2.Distance(pos, sp);
          if (sp.z > 0 && d < bestD * Screen.height / 900f) { bestD = d; best = s.Mask; }
        }
        if (best >= 0 && best != _focus) Focus(best, false);
      }

      foreach (var s in _slots) {
        bool f = s.Mask == _focus;
        var target = s.Home + (f ? new Vector3(0, 0, -.8f) : Vector3.zero);
        s.Root.position = Vector3.Lerp(s.Root.position, target, Time.deltaTime * 6f);
        s.Root.localScale = Vector3.Lerp(s.Root.localScale, Vector3.one * (f ? 1.12f : .92f), Time.deltaTime * 6f);
        if (s.BackToIdleAt > 0 && Time.time >= s.BackToIdleAt) { s.BackToIdleAt = 0; if (s.Idle != null) s.Anim.CrossFadeInFixedTime(s.Idle, .25f, 0); }
      }
      if (_cam) {
        // Drift toward the character in focus, but stay to its right so it sits left of the info scroll.
        float camX = (_slots[_focus].Home.x - Origin.x) * .15f + CamShift;
        var camPos = Origin + new Vector3(camX, 2.8f, -CamDist);
        if (_live) { // a slow breathing drift and a small swing toward the character in focus, so the arena feels alive
          float t = Time.unscaledTime;
          camPos += new Vector3(Mathf.Sin(t * .22f) * .5f + (_slots[_focus].Home.x - Origin.x) * .35f, Mathf.Sin(t * .31f) * .15f, 0);
        }
        var look = Origin + new Vector3(camX, 1f, 0);
        _cam.transform.position = Vector3.Lerp(_cam.transform.position, camPos, Time.deltaTime * 4f);
        _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation, Quaternion.LookRotation(look - camPos), Time.deltaTime * 4f);
      }
    }

    void Confirm() {
      if (_picked.Count != 2) return;
      LocalProfile.SaveMasks(_picked[0], _picked[1]);
      Confirmed = true;
      if (PlayerAvatar.Local) PlayerAvatar.Local.SetMasks(_picked[0], _picked[1]);
      Close();
    }

    void Close() {
      RestoreGame();
      var done = _onDone;
      _onDone = null;
      if (SceneManager.sceneCount > 1) SceneManager.UnloadSceneAsync(gameObject.scene);
      else SceneManager.LoadScene(0); // opened on its own (testing in the Editor): go to the game
      done?.Invoke();
    }
  }
}
