using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BachYeu.EditorTools {
  /// <summary>
  /// Menu "Bách Yêu > Tạo player từ prefab đang chọn": turns any character model prefab into a game-ready
  /// player model. It never edits the source pack; it creates next to Assets/_Game/Art/Characters/:
  ///  - Player_[Name].controller  with the game's parameters (Speed, Attack, Skill, Ult, Swap, Hit, Downed)
  ///  - Player_[Name].prefab      a prefab variant of the source using that controller
  /// Clips are found by name in the source controller / folder: idle, walk, run, attack, skill, hit, dead/die.
  /// </summary>
  public static class CharacterSetup {
    const string OutDir = "Assets/_Game/Art/Characters";

    [MenuItem("Bách Yêu/Tạo player từ prefab đang chọn")]
    static void FromSelection() {
      var src = Selection.activeObject as GameObject;
      if (!src || !PrefabUtility.IsPartOfPrefabAsset(src)) {
        EditorUtility.DisplayDialog("Tạo player", "Chọn một prefab model trong cửa sổ Project trước.", "OK");
        return;
      }
      var masks = new[] { "Fox", "Tengu", "HaBa", "QuyDa" };
      var names = new[] { "Hồ Ly", "Tengu", "Hà Bá", "Quỷ Đá" };
      int pick = -1;
      for (int i = 0; i < masks.Length && pick < 0; i++)
        if (EditorUtility.DisplayDialog("Tạo player", $"Dùng '{src.name}' cho mặt nạ {names[i]}?", "Đúng", "Không, xem mặt nạ khác")) pick = i;
      if (pick < 0) return;
      var prefab = CreatePlayer(AssetDatabase.GetAssetPath(src), masks[pick]);
      Selection.activeObject = prefab;
    }

    /// <summary>Create controller + prefab variant from a model prefab and assign it to ArtSet. mask = "Fox", "Tengu", "HaBa" or "QuyDa".</summary>
    public static GameObject CreatePlayer(string sourcePrefabPath, string mask) {
      var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath);
      if (!source) throw new System.ArgumentException("Không tìm thấy prefab: " + sourcePrefabPath);
      Directory.CreateDirectory(OutDir);
      string baseName = "Player_" + source.name;

      var clips = FindClips(source, sourcePrefabPath);
      var ctrl = BuildController($"{OutDir}/{baseName}.controller", clips);

      var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
      try {
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var anim = instance.GetComponentInChildren<Animator>();
        if (!anim) anim = instance.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var prefab = PrefabUtility.SaveAsPrefabAsset(instance, $"{OutDir}/{baseName}.prefab");

        var art = AssetDatabase.LoadAssetAtPath<ArtSet>("Assets/_Game/Resources/ArtSet.asset");
        if (art) {
          Undo.RecordObject(art, "Assign player model");
          switch (mask) {
            case "Fox": art.PlayerFox = prefab; break;
            case "HaBa": art.PlayerHaBa = prefab; break;
            case "QuyDa": art.PlayerQuyDa = prefab; break;
            default: art.PlayerTengu = prefab; break;
          }
          EditorUtility.SetDirty(art);
          AssetDatabase.SaveAssets();
        }
        Debug.Log($"[Bách Yêu] Đã tạo {baseName}.prefab ({string.Join(", ", clips.Select(c => c.Key + "=" + (c.Value ? c.Value.name : "trống")))})");
        return prefab;
      } finally {
        Object.DestroyImmediate(instance);
      }
    }

    // ---------- clips ----------
    static readonly (string key, string[] words)[] Wanted = {
      ("idle", new[] { "idle" }),
      ("walk", new[] { "walk" }),
      ("run", new[] { "run" }),
      ("attack", new[] { "attack", "atk" }),
      ("skill", new[] { "skill", "cast" }),
      ("hit", new[] { "hit", "damage", "hurt" }),
      ("dead", new[] { "dead", "die", "death" }),
    };

    static Dictionary<string, AnimationClip> FindClips(GameObject source, string sourcePath) {
      var pool = new List<AnimationClip>();
      // 1) Clips used by the model's own controller.
      var a = source.GetComponentInChildren<Animator>();
      if (a && a.runtimeAnimatorController) pool.AddRange(a.runtimeAnimatorController.animationClips);
      // 2) Clips in the pack folder (one level up from Prefabs/).
      var dir = Path.GetDirectoryName(Path.GetDirectoryName(sourcePath))?.Replace('\\', '/');
      if (!string.IsNullOrEmpty(dir))
        foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { dir }))
          foreach (var o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
            if (o is AnimationClip c && !c.name.StartsWith("__preview__")) pool.Add(c);
      pool = pool.Distinct().ToList();

      var result = new Dictionary<string, AnimationClip>();
      foreach (var (key, words) in Wanted) {
        // Prefer an exact name match ("idle"), then the shortest name containing a keyword ("idle02" loses to "idle").
        var match = pool.Where(c => words.Any(w => c.name.ToLowerInvariant() == w)).FirstOrDefault()
                 ?? pool.Where(c => words.Any(w => c.name.ToLowerInvariant().Contains(w))).OrderBy(c => c.name.Length).FirstOrDefault();
        result[key] = match;
      }
      if (!result["idle"]) result["idle"] = pool.FirstOrDefault();
      return result;
    }

    // ---------- controller ----------
    static AnimatorController BuildController(string path, Dictionary<string, AnimationClip> clips) {
      AssetDatabase.DeleteAsset(path);
      var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
      ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
      foreach (var t in new[] { "Attack", "Skill", "Ult", "Swap", "Dash", "Hit" }) ctrl.AddParameter(t, AnimatorControllerParameterType.Trigger);
      ctrl.AddParameter("Downed", AnimatorControllerParameterType.Bool);

      var sm = ctrl.layers[0].stateMachine;

      // Locomotion blend tree: idle → walk → run by Speed (m/s, player runs at 5.2).
      var loco = ctrl.CreateBlendTreeInController("Locomotion", out var tree);
      tree.blendParameter = "Speed";
      tree.useAutomaticThresholds = false;
      tree.AddChild(clips["idle"], 0f);
      if (clips["walk"]) tree.AddChild(clips["walk"], 1.5f);
      if (clips["run"]) tree.AddChild(clips["run"], 4.5f);
      sm.defaultState = loco;

      AnimatorState Action(string name, AnimationClip clip) {
        if (!clip) return null;
        var s = sm.AddState(name);
        s.motion = clip;
        var back = s.AddTransition(loco);
        back.hasExitTime = true;
        back.exitTime = .85f;
        back.duration = .1f;
        return s;
      }

      void FromAny(AnimatorState s, string trigger) {
        if (!s) return;
        var t = sm.AddAnyStateTransition(s);
        t.AddCondition(AnimatorConditionMode.If, 0, trigger);
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "Downed");
        t.hasExitTime = false;
        t.duration = .05f;
        t.canTransitionToSelf = true;
      }

      var attack = Action("Attack", clips["attack"]);
      var skill = Action("Skill", clips["skill"] ? clips["skill"] : clips["attack"]);
      var hit = Action("Hit", clips["hit"]);
      FromAny(attack, "Attack");
      FromAny(skill, "Skill");
      FromAny(skill, "Ult");
      FromAny(skill, "Swap");
      FromAny(hit, "Hit");

      if (clips["dead"]) {
        var dead = sm.AddState("Dead");
        dead.motion = clips["dead"];
        var toDead = sm.AddAnyStateTransition(dead);
        toDead.AddCondition(AnimatorConditionMode.If, 0, "Downed");
        toDead.hasExitTime = false;
        toDead.duration = .1f;
        toDead.canTransitionToSelf = false;
        var up = dead.AddTransition(loco);
        up.AddCondition(AnimatorConditionMode.IfNot, 0, "Downed");
        up.hasExitTime = false;
        up.duration = .2f;
      }
      EditorUtility.SetDirty(ctrl);
      AssetDatabase.SaveAssets();
      return ctrl;
    }
  }
}
