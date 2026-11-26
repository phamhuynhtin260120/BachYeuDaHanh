using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BachYeu.EditorTools {
  /// <summary>
  /// Puts the Casual Fantasy monsters (Assets/CasualFantasy) into the game as the yokai, painted to match the ink arena.
  /// For each role it makes, in Assets/_Game/Art/Yokai:
  ///  - a wrapper prefab: feet on the ground, facing +Z, scaled to the role's height;
  ///  - BachYeu/InkCharacter materials (muted, paper-tinted, banded light, brush outline) from the pack's textures;
  ///  - an Animator Controller with the parameters the game drives (Speed, Attack, Hit).
  /// Then fills the yokai slots of Resources/ArtSet. The pack's own files are not changed. Running it again rebuilds everything.
  /// </summary>
  public static class YokaiArtBuilder {
    const string Pack = "Assets/CasualFantasy/";
    const string Root = "Assets/_Game/Art/Yokai";
    const string MatDir = Root + "/Materials", AnimDir = Root + "/Animators", PrefabDir = Root + "/Prefabs";
    const string ShaderName = "BachYeu/InkCharacter";

    struct Role {
      public EnemyType Type; public string Path; public float Height; public bool Shadow;
      public Color Tint; public float Desat; public bool Flyer;
    }

    static Role R(EnemyType t, string path, float h, Color? tint = null, float desat = .4f, bool shadow = false, bool flyer = false) =>
      new() { Type = t, Path = path, Height = h, Tint = tint ?? Color.white, Desat = desat, Shadow = shadow, Flyer = flyer };

    // Which monster plays which yokai. Tints nudge each one toward the arena's palette (vermilion, indigo, ink, jade).
    static readonly Role[] Roster = {
      R(EnemyType.Ghost, "Monsters7/Bat/Bat", .9f, new Color(1f, .86f, .82f), flyer: true), // height = the bat's own size; it keeps its hover
      R(EnemyType.Umbrella, "Monsters7/Slime/Slime", 1.1f, new Color(.9f, .92f, 1f)),
      R(EnemyType.Heavy, "Monsters2/Bear/Bear", 2.7f),
      R(EnemyType.Elite, "Monsters8/MonsterChest/Chest", 2.2f),
      R(EnemyType.Cat, "Monsters10/Hellhound/Hellhound", 2.7f, null, .3f),
      R(EnemyType.King, "Monsters11/Reaper/Reaper_v1", 4.6f, new Color(1f, .9f, .9f), .3f, flyer: true), // floats a little in its own idle
      R(EnemyType.Wisp, "Monsters11/EvilEye/EvilEye_v1", 1.7f, new Color(.85f, 1f, .95f), .3f),
      R(EnemyType.Inkstone, "Monsters8/MonsterPlant/MonsterPlant", 1.6f),
      R(EnemyType.Shade, "Monsters10/Ghost/Ghost", 1.8f, new Color(.8f, .82f, .9f), .55f, true),
      R(EnemyType.Firecracker, "Monsters9/Imp/imp", 1.25f, new Color(1f, .88f, .85f), .3f),
    };

    [MenuItem("Bách Yêu/Gắn quái Casual Fantasy (nhuộm mực)")]
    public static void BuildAll() {
      if (!Shader.Find(ShaderName)) { Debug.LogError("[Bách Yêu] Thiếu shader " + ShaderName); return; }
      foreach (var d in new[] { Root, MatDir, AnimDir, PrefabDir }) Directory.CreateDirectory(d);
      var art = Resources.Load<ArtSet>("ArtSet");
      if (!art) { Debug.LogError("[Bách Yêu] Không thấy Resources/ArtSet"); return; }

      foreach (var r in Roster) {
        var prefab = Wrap(r);
        if (prefab && !HasController(prefab)) Debug.LogWarning("[Bách Yêu] " + prefab.name + " chưa có Animator Controller.");
        typeof(ArtSet).GetField(r.Type.ToString()).SetValue(art, prefab);
      }
      EditorUtility.SetDirty(art);
      AssetDatabase.SaveAssets();
      // The prefab files are right on disk, but the editor can keep a stale in-memory copy without the new controller: reload them.
      foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir }))
        AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g), ImportAssetOptions.ForceUpdate);
      AssetDatabase.Refresh();
      Debug.Log("[Bách Yêu] Đã gắn " + Roster.Length + " loài quái Casual Fantasy vào ArtSet.");
    }

    // ---------------- prefab ----------------
    static GameObject Wrap(Role role) {
      var srcPath = Pack + role.Path + ".prefab";
      var src = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);
      if (!src) { Debug.LogWarning("[Bách Yêu] Không thấy " + srcPath); return null; }
      string name = "Yokai_" + role.Type;
      var clips = ClipsFor(src, srcPath);

      var root = new GameObject(name);
      var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
      inst.transform.SetParent(root.transform, false);
      inst.transform.localPosition = Vector3.zero;
      inst.transform.localRotation = Quaternion.identity;

      var anim = inst.GetComponentInChildren<Animator>();
      // Rig helper shapes (zero-size "...ControllerShape" meshes) are not part of the look: hide them, and leave them out of the size.
      foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
        if (r.name.Contains("ControllerShape") || r.name.Contains("RootShape")) r.enabled = false;
      var b = RendererBounds(inst);
      float k = role.Height / Mathf.Max(.01f, b.size.y);
      inst.transform.localScale = src.transform.localScale * k;
      b = RendererBounds(inst);
      // Feet on the ground, centered. Flyers keep their own hover above the pivot.
      inst.transform.localPosition = new Vector3(-b.center.x, role.Flyer ? 0 : -b.min.y, -b.center.z);
      PrefabUtility.RecordPrefabInstancePropertyModifications(inst.transform);

      foreach (var r in inst.GetComponentsInChildren<Renderer>(true)) {
        r.sharedMaterials = r.sharedMaterials.Select(m => Ink(m, role)).ToArray();
        PrefabUtility.RecordPrefabInstancePropertyModifications(r);
      }

      if (anim) {
        anim.runtimeAnimatorController = Controller(name, clips);
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        PrefabUtility.RecordPrefabInstancePropertyModifications(anim); // without this the controller is not saved as an override
      }

      if (role.Shadow) {
        var s = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        s.name = "Shadow";
        UnityEngine.Object.DestroyImmediate(s.GetComponent<Collider>());
        s.transform.SetParent(root.transform, false);
        s.transform.localPosition = new Vector3(0, .02f, 0);
        s.transform.localScale = new Vector3(1.3f, .01f, 1.3f);
        s.GetComponent<Renderer>().sharedMaterial = ShadowMat();
      }

      var saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{name}.prefab");
      UnityEngine.Object.DestroyImmediate(root);
      return saved;
    }

    static bool HasController(GameObject prefab) {
      var a = prefab.GetComponentInChildren<Animator>(true);
      return !a || a.runtimeAnimatorController;
    }

    /// <summary>The prefab's controller clips plus every clip inside the .fbx files of its folder (some prefabs only wire Idle).</summary>
    static AnimationClip[] ClipsFor(GameObject src, string srcPath) {
      var list = new List<AnimationClip>();
      var a = src.GetComponentInChildren<Animator>(true);
      if (a && a.runtimeAnimatorController) list.AddRange(a.runtimeAnimatorController.animationClips);
      var dir = Path.GetDirectoryName(srcPath);
      foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { dir }))
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(g)))
          if (o is AnimationClip c && !c.name.StartsWith("__preview")) list.Add(c);
      return list.Distinct().ToArray();
    }

    static Bounds RendererBounds(GameObject go) {
      var rs = go.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
      var b = rs.Length > 0 ? rs[0].bounds : new Bounds(go.transform.position, Vector3.one);
      foreach (var r in rs) b.Encapsulate(r.bounds);
      return b;
    }

    // ---------------- materials ----------------
    static Material Ink(Material m, Role role) {
      if (!m) return m;
      var path = $"{MatDir}/{role.Type}_{San(m.name)}.mat";
      var n = AssetDatabase.LoadAssetAtPath<Material>(path);
      bool created = !n;
      if (created) n = new Material(Shader.Find(ShaderName));
      else n.shader = Shader.Find(ShaderName);
      var tex = m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") ? m.GetTexture("_BaseMap") : m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
      var col = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
      n.SetTexture("_BaseMap", tex);
      if (tex) {
        var prop = m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") ? "_BaseMap" : "_MainTex";
        n.SetTextureScale("_BaseMap", m.GetTextureScale(prop));
        n.SetTextureOffset("_BaseMap", m.GetTextureOffset(prop));
      }
      n.SetColor("_BaseColor", col * role.Tint);
      n.SetFloat("_Desat", role.Desat);
      if (created) AssetDatabase.CreateAsset(n, path); else EditorUtility.SetDirty(n);
      return n;
    }

    static string San(string s) => string.Concat(s.Select(c => char.IsLetterOrDigit(c) ? c : '_'));

    static Material ShadowMat() {
      var path = MatDir + "/ShadeShadow.mat";
      var m = AssetDatabase.LoadAssetAtPath<Material>(path);
      if (m) return m;
      m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
      m.SetColor("_BaseColor", new Color(.16f, .16f, .2f, 1));
      m.SetFloat("_Smoothness", 0);
      AssetDatabase.CreateAsset(m, path);
      return m;
    }

    // ---------------- animator ----------------
    static AnimationClip Pick(AnimationClip[] clips, params string[] keys) {
      foreach (var k in keys) {
        var c = clips.FirstOrDefault(x => x.name.EndsWith(k, StringComparison.OrdinalIgnoreCase));
        if (c) return c;
      }
      foreach (var k in keys) {
        var c = clips.FirstOrDefault(x => x.name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);
        if (c) return c;
      }
      return null;
    }

    /// <summary>Speed (float) walks; Attack and Hit (triggers) play once and return to idle.</summary>
    static AnimatorController Controller(string name, AnimationClip[] clips) {
      // Reuse the asset when it exists (same GUID), so prefabs that point at it never lose the link.
      var path = $"{AnimDir}/{name}.controller";
      var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
      if (!c) c = AnimatorController.CreateAnimatorControllerAtPath(path);
      foreach (var p in c.parameters) c.RemoveParameter(p);
      var old = c.layers[0].stateMachine;
      foreach (var t in old.anyStateTransitions) old.RemoveAnyStateTransition(t);
      foreach (var st in old.states) old.RemoveState(st.state);
      c.AddParameter("Speed", AnimatorControllerParameterType.Float);
      var sm = c.layers[0].stateMachine;
      var idle = sm.AddState("Idle", new Vector2(300, 0)); idle.motion = Looping(Pick(clips, "Idle"), name);
      var move = sm.AddState("Move", new Vector2(300, 120)); move.motion = Looping(Pick(clips, "Move", "Walk", "Run"), name) ?? idle.motion;
      sm.defaultState = idle;
      var go = idle.AddTransition(move); go.hasExitTime = false; go.duration = .1f; go.AddCondition(AnimatorConditionMode.Greater, .4f, "Speed");
      var stop = move.AddTransition(idle); stop.hasExitTime = false; stop.duration = .15f; stop.AddCondition(AnimatorConditionMode.Less, .25f, "Speed");
      OneShot(c, sm, idle, "Attack", Pick(clips, "Attack"), new Vector2(600, 0));
      OneShot(c, sm, idle, "Hit", Pick(clips, "GetHit", "Hit"), new Vector2(600, 120)); // these models have no hit clip: the game punch-scales them instead
      // Die: played by the corpse the game leaves behind (EnemyCorpse); it holds the last frame.
      var die = Pick(clips, "Die", "Death");
      if (die) {
        c.AddParameter("Die", AnimatorControllerParameterType.Trigger);
        var d = sm.AddState("Die", new Vector2(300, 260)); d.motion = die;
        var t = sm.AddAnyStateTransition(d); t.AddCondition(AnimatorConditionMode.If, 0, "Die"); t.duration = .05f; t.canTransitionToSelf = false;
      }
      EditorUtility.SetDirty(c);
      return c;
    }

    /// <summary>
    /// Idle and walk must loop. Some pack clips are imported without Loop Time: use a looping copy in Animators/Clips
    /// (updated in place on rebuilds, so its GUID stays) instead of changing the pack's import settings.
    /// </summary>
    static AnimationClip Looping(AnimationClip src, string owner) {
      if (!src || src.isLooping) return src;
      Directory.CreateDirectory(AnimDir + "/Clips");
      var path = $"{AnimDir}/Clips/{owner}_{San(src.name)}.anim";
      var copy = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
      bool created = !copy;
      if (created) copy = new AnimationClip();
      EditorUtility.CopySerialized(src, copy);
      var set = AnimationUtility.GetAnimationClipSettings(copy);
      set.loopTime = true;
      AnimationUtility.SetAnimationClipSettings(copy, set);
      copy.name = Path.GetFileNameWithoutExtension(path);
      if (created) AssetDatabase.CreateAsset(copy, path); else EditorUtility.SetDirty(copy);
      return copy;
    }

    static void OneShot(AnimatorController c, AnimatorStateMachine sm, AnimatorState back, string trigger, Motion clip, Vector2 pos) {
      if (!clip) return;
      c.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
      var s = sm.AddState(trigger, pos); s.motion = clip;
      var t = sm.AddAnyStateTransition(s); t.AddCondition(AnimatorConditionMode.If, 0, trigger); t.duration = .06f; t.canTransitionToSelf = false;
      var r = s.AddTransition(back); r.hasExitTime = true; r.exitTime = .9f; r.duration = .12f;
    }
  }
}
