using System.Collections.Generic;
using UnityEngine;

namespace BachYeu {
  /// <summary>Sets Animator parameters only if the controller defines them, so any subset works.</summary>
  public static class AnimUtil {
    static readonly Dictionary<RuntimeAnimatorController, HashSet<int>> _params = new();

    static bool Has(Animator a, int hash) {
      if (a == null || a.runtimeAnimatorController == null || !a.isActiveAndEnabled) return false;
      var ctrl = a.runtimeAnimatorController;
      if (!_params.TryGetValue(ctrl, out var set)) {
        set = new HashSet<int>();
        foreach (var p in a.parameters) set.Add(p.nameHash);
        _params[ctrl] = set;
      }
      return set.Contains(hash);
    }

    public static void Float(Animator a, string name, float v) { int h = Animator.StringToHash(name); if (Has(a, h)) a.SetFloat(h, v); }
    public static void Bool(Animator a, string name, bool v) { int h = Animator.StringToHash(name); if (Has(a, h)) a.SetBool(h, v); }
    public static void Trigger(Animator a, string name) { int h = Animator.StringToHash(name); if (Has(a, h)) a.SetTrigger(h); }
    public static bool HasParam(Animator a, string name) => Has(a, Animator.StringToHash(name));
    public static void ResetCache() => _params.Clear();

    /// <summary>Instantiate an art prefab as a pure visual: strip colliders and rigidbodies.</summary>
    public static GameObject SpawnVisual(GameObject prefab, Transform parent) {
      var go = Object.Instantiate(prefab, parent, false);
      go.transform.localPosition = Vector3.zero;
      go.transform.localRotation = Quaternion.identity;
      foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
      foreach (var r in go.GetComponentsInChildren<Rigidbody>(true)) Object.Destroy(r);
      return go;
    }
  }

  /// <summary>Player look: a custom model per mask from ArtSet, or the placeholder chibi.</summary>
  public class PlayerVisual {
    public readonly Transform Root;
    readonly Models.Chibi _chibi;
    readonly GameObject[] _models = new GameObject[2];
    readonly Animator[] _anims = new Animator[2];
    int _mask;

    public bool IsCustom => _chibi == null;
    public Animator Anim => _anims[_models[1] ? _mask : 0];

    public PlayerVisual(Transform parent) {
      Root = new GameObject("Visual").transform;
      Root.SetParent(parent, false);
      var art = ArtSet.Current;
      if (art && (art.PlayerFox || art.PlayerTengu)) {
        var fox = art.PlayerFox ? art.PlayerFox : art.PlayerTengu;
        var tengu = art.PlayerTengu ? art.PlayerTengu : art.PlayerFox;
        _models[0] = AnimUtil.SpawnVisual(fox, Root);
        if (tengu != fox) _models[1] = AnimUtil.SpawnVisual(tengu, Root);
        for (int i = 0; i < 2; i++) if (_models[i]) _anims[i] = _models[i].GetComponentInChildren<Animator>();
      } else {
        _chibi = Models.BuildChibi(Root, Gfx.Hex("#c23a3a"), Gfx.Hex("#2b2420"));
      }
    }

    public void ApplyMask(int mask) {
      _mask = mask;
      if (_chibi != null) { Models.ApplyMask(_chibi, mask); return; }
      if (_models[1]) {
        _models[0].SetActive(mask == 0);
        _models[1].SetActive(mask == 1);
      }
    }

    public void SetDowned(bool downed) {
      AnimUtil.Bool(Anim, "Downed", downed);
      bool animated = AnimUtil.HasParam(Anim, "Downed");
      Root.localRotation = downed && !animated ? Quaternion.Euler(80, 0, 0) : Quaternion.identity;
      Root.localPosition = downed && !animated ? new Vector3(0, .35f, 0) : Vector3.zero;
    }

    /// <summary>Procedural hop for models without a Speed parameter.</summary>
    public void Move(float speed, float bobPhase) {
      AnimUtil.Float(Anim, "Speed", speed);
      if (!AnimUtil.HasParam(Anim, "Speed")) Root.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(bobPhase)) * .12f, 0);
    }

    public void Trigger(string name) => AnimUtil.Trigger(Anim, name);
    public void SetVisible(bool v) => Root.gameObject.SetActive(v);
  }
}
