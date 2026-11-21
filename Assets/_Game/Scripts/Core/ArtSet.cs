using System;
using System.Collections.Generic;
using UnityEngine;

namespace BachYeu {
  /// <summary>
  /// Art slots for characters and the arena. Lives at Assets/_Game/Resources/ArtSet.asset.
  /// Drag your model prefabs into the slots; any empty slot falls back to the placeholder primitives.
  ///
  /// Model rules:
  /// - Pivot at the feet, facing +Z, about 1.8 units tall for the player (scale the prefab, not the code).
  /// - No NetworkObject and no colliders needed (colliders are removed at runtime).
  /// - Optional Animator. These parameters are used when they exist (any can be missing):
  ///   Player: Speed (float), Attack, Skill, Swap, Dash, Ult, Hit (triggers), Downed (bool)
  ///   Enemy:  Speed (float), Hit (trigger)
  /// </summary>
  [CreateAssetMenu(menuName = "Bách Yêu/Art Set", fileName = "ArtSet")]
  public class ArtSet : ScriptableObject {
    [Header("Player (one prefab per mask; if only one is set it is used for both)")]
    public GameObject PlayerFox;
    public GameObject PlayerTengu;
    [Tooltip("Empty = reuse the Hồ Ly model")] public GameObject PlayerHaBa;
    [Tooltip("Empty = reuse the Tengu model")] public GameObject PlayerQuyDa;

    /// <summary>Player model for a mask id, falling back to another filled slot.</summary>
    public GameObject PlayerModel(int maskId) {
      var m = maskId switch { Masks.Tengu => PlayerTengu, Masks.HaBa => PlayerHaBa, Masks.QuyDa => PlayerQuyDa, _ => PlayerFox };
      if (m) return m;
      m = maskId == Masks.QuyDa ? PlayerTengu : PlayerFox;
      return m ? m : (PlayerFox ? PlayerFox : PlayerTengu);
    }

    [Header("Yokai")]
    public GameObject Ghost;
    public GameObject Umbrella;
    public GameObject Heavy;
    [Tooltip("Empty = Umbrella prefab scaled up")]
    public GameObject Elite;
    public GameObject Cat;
    public GameObject King;

    [Header("Player VFX (one row per effect; effects with no row keep the built-in look)")]
    public List<PlayerFxSlot> PlayerFx = new();

    [Serializable]
    public class PlayerFxSlot {
      [Tooltip("Which moment of which mask this replaces")] public PlayerAvatar.FxKind Kind;
      [Tooltip("Particle System / VFX Graph prefab. Pivot on the ground, facing +Z (the direction the player faces).")]
      public GameObject Prefab;
      [Tooltip("Seconds the effect keeps emitting; after that it stops and fades out")] public float Life = 2f;
      [Tooltip("Stick to the player (shields, guard) instead of staying where it was cast")] public bool FollowPlayer;
      [Tooltip("Also play the built-in effect (handy while comparing)")] public bool KeepBuiltIn;
      public Vector3 Offset;
      public float Scale = 1f;
    }

    Dictionary<PlayerAvatar.FxKind, PlayerFxSlot> _fxMap;

    PlayerFxSlot PlayerSlot(PlayerAvatar.FxKind kind) {
      if (_fxMap == null) {
        _fxMap = new();
        foreach (var s in PlayerFx) if (s != null && s.Prefab) _fxMap[s.Kind] = s;
      }
      return _fxMap.TryGetValue(kind, out var slot) ? slot : null;
    }

    /// <summary>True when a row with a prefab replaces this effect's built-in look (Keep Built In off).</summary>
    public bool ReplacesBuiltIn(PlayerAvatar.FxKind kind) {
      var slot = PlayerSlot(kind);
      return slot != null && !slot.KeepBuiltIn;
    }

    /// <summary>Spawns the prefab set for this effect, if any. Returns the slot used, or null.</summary>
    public PlayerFxSlot SpawnPlayerFx(PlayerAvatar.FxKind kind, Vector3 pos, float yaw, Transform owner) {
      var slot = PlayerSlot(kind);
      LastVfxClip = null;
      if (slot == null) return null;
      SpawnOneShot(slot.Prefab, pos, yaw, slot.FollowPlayer ? owner : null, slot.Scale, slot.Offset, slot.Life);
      return slot;
    }

    [Header("Game VFX (yokai, lanterns, warnings, pickups...; empty rows keep the built-in look)")]
    public List<GameFxSlot> GameFx = new();

    [Serializable]
    public class GameFxSlot {
      public BachYeu.GameFx Kind;
      [Tooltip("Particle System prefab. One-shot moments play once; Aura/Trail moments loop until their owner goes away.")]
      public GameObject Prefab;
      [Tooltip("One-shot moments: seconds the effect keeps emitting before it fades. Ignored for Aura/Trail.")] public float Life = 1.5f;
      [Tooltip("Also play the built-in effect / keep the built-in mesh")] public bool KeepBuiltIn;
      public Vector3 Offset;
      public float Scale = 1f;
    }

    Dictionary<BachYeu.GameFx, GameFxSlot> _gameMap;

    GameFxSlot GameSlot(BachYeu.GameFx kind) {
      if (_gameMap == null) {
        _gameMap = new();
        foreach (var s in GameFx) if (s != null && s.Prefab) _gameMap[s.Kind] = s;
      }
      return _gameMap.TryGetValue(kind, out var slot) ? slot : null;
    }

    /// <summary>
    /// Plays the prefab set for a game moment at pos. Returns true when the built-in effect should be skipped.
    /// sizeMul scales on top of the row's Scale (e.g. a warning circle's radius).
    /// </summary>
    public static bool Play(BachYeu.GameFx kind, Vector3 pos, float yaw = 0, float sizeMul = 1f) {
      var art = Current;
      var slot = art ? art.GameSlot(kind) : null;
      LastVfxClip = null;
      if (slot != null) SpawnOneShot(slot.Prefab, pos, yaw, null, slot.Scale * sizeMul, slot.Offset, slot.Life);
      var snd = Sfx.For(kind);
      if (snd.HasValue) Sfx.Play(snd.Value, pos, LastVfxClip);
      return slot != null && !slot.KeepBuiltIn;
    }

    /// <summary>The sound clip carried by the last spawned one-shot prefab (asset-store VFX often bring one), or null.</summary>
    public static AudioClip LastVfxClip { get; private set; }

    /// <summary>Asset-store VFX play their own sound at full volume (and some repeat). Silence it; return its clip so Sfx can play it once at the player's volume.</summary>
    static AudioClip MuteBuiltInSound(GameObject go) {
      AudioClip clip = null;
      foreach (var hs in go.GetComponentsInChildren<Hovl.HS_EffectSound>(true)) {
        if (!clip) clip = hs.clip ? hs.clip : (hs.soundComponent ? hs.soundComponent.clip : null);
        hs.enabled = false; // before Start, so it never plays
      }
      foreach (var src in go.GetComponentsInChildren<AudioSource>(true)) {
        if (!clip) clip = src.clip;
        src.playOnAwake = false;
        src.Stop();
        src.enabled = false;
      }
      return clip;
    }

    /// <summary>
    /// Puts a looping prefab (aura, projectile trail) on a moving object; it goes away with the object.
    /// Returns true when the object's built-in look should be hidden.
    /// </summary>
    public static bool Attach(BachYeu.GameFx kind, Transform parent, float sizeMul = 1f) {
      var art = Current;
      var slot = art && parent ? art.GameSlot(kind) : null;
      if (slot == null) return false;
      var go = Instantiate(slot.Prefab, parent);
      MuteBuiltInSound(go); // auras and trails stay silent
      go.transform.localPosition = slot.Offset;
      go.transform.localRotation = slot.Prefab.transform.rotation;
      var ls = parent.lossyScale;
      var inv = new Vector3(ls.x != 0 ? 1f / ls.x : 1f, ls.y != 0 ? 1f / ls.y : 1f, ls.z != 0 ? 1f / ls.z : 1f);
      go.transform.localScale = Vector3.Scale(slot.Prefab.transform.localScale * slot.Scale * sizeMul, inv); // ignore the owner's squash
      foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) {
        var main = ps.main;
        main.loop = true;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        if (!ps.isPlaying) ps.Play(false);
      }
      return !slot.KeepBuiltIn;
    }

    static void SpawnOneShot(GameObject prefab, Vector3 pos, float yaw, Transform follow, float scale, Vector3 offset, float life) {
      var rot = Quaternion.Euler(0, yaw, 0);
      // Keep the prefab's own tilt (many packs lay slashes and circles flat with a root rotation).
      var go = Instantiate(prefab, pos + rot * offset, rot * prefab.transform.rotation, follow);
      LastVfxClip = MuteBuiltInSound(go);
      go.transform.localScale *= scale;
      var systems = go.GetComponentsInChildren<ParticleSystem>(true);
      foreach (var ps in systems) {
        var main = ps.main;
        main.loop = false;                                        // asset-store demos loop; in game each one plays once
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // so Scale also grows child systems
      }
      foreach (var ps in systems) if (!ps.isPlaying) ps.Play(false);
      // Never stop before the prefab's own last scheduled burst, or that part of the effect would never show.
      float lastBurst = 0;
      foreach (var ps in systems) {
        var em = ps.emission;
        if (!em.enabled || em.burstCount == 0) continue;
        float delay = ps.main.startDelay.constantMax;
        for (int i = 0; i < em.burstCount; i++) lastBurst = Mathf.Max(lastBurst, delay + em.GetBurst(i).time);
      }
      FxOneShot.Run(go, systems, Mathf.Max(.1f, life, lastBurst + .05f));
    }

    void OnValidate() { _fxMap = null; _gameMap = null; }

    [Header("Arena")]
    [Tooltip("Replaces the procedural floating island, trees and gate. Keep the play area flat at y = 0 within radius 12.")]
    public GameObject Environment;

    static ArtSet _current;
    static bool _loaded;

    public static ArtSet Current {
      get {
        if (!_loaded) { _current = Resources.Load<ArtSet>("ArtSet"); _loaded = true; }
        return _current;
      }
    }

    public static void ResetCache() { _current = null; _loaded = false; }

    /// <summary>Prefab for an enemy type, plus an extra scale when a fallback prefab is reused.</summary>
    public (GameObject prefab, float scale) EnemyPrefab(EnemyType t) => t switch {
      EnemyType.Ghost => (Ghost, 1f),
      EnemyType.Umbrella => (Umbrella, 1f),
      EnemyType.Heavy => (Heavy, 1f),
      EnemyType.Elite => Elite ? (Elite, 1f) : (Umbrella, 1.7f),
      EnemyType.Cat => (Cat, 1f),
      EnemyType.Wisp => (null, 1f), // always the built-in will-o'-wisp
      EnemyType.Inkstone or EnemyType.Shade or EnemyType.Firecracker => (null, 1f), // built-in models for now
      _ => (King, 1f),
    };
  }
}

namespace BachYeu {
  /// <summary>Stops a spawned effect's emitters after its life, lets the last particles fade, then removes it.</summary>
  public class FxOneShot : MonoBehaviour {
    const float MaxFade = 2.5f;
    ParticleSystem[] _systems;
    float _stopAt, _killAt;

    public static void Run(GameObject go, ParticleSystem[] systems, float life) {
      var s = go.AddComponent<FxOneShot>();
      s._systems = systems;
      s._stopAt = Time.time + life;
      s._killAt = s._stopAt + MaxFade;
    }

    void Update() {
      if (_stopAt > 0 && Time.time >= _stopAt) {
        _stopAt = 0;
        foreach (var ps in _systems) if (ps) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
      }
      if (_stopAt == 0) {
        bool alive = false;
        foreach (var ps in _systems) if (ps && ps.IsAlive(false)) { alive = true; break; }
        if (!alive || Time.time >= _killAt) Destroy(gameObject);
      }
    }
  }
}

namespace BachYeu {
  /// <summary>Game moments that can take a VFX prefab (ArtSet > Game VFX). Append only: rows are saved by value.</summary>
  public enum GameFx {
    EnemySpawn, EnemyHit, EnemyHitBig, EnemyDeath,
    BossSpawn, BossDeath,
    WarningImpact, WarningImpactBig,
    LanternTakenLam, LanternTakenChu, LanternTakenWisp, LanternLit,
    HealOrbPickup, PlayerDowned, PlayerRevived, Ping, StoneWallRise, FoxOrbHit,
    // looping, attached to a moving object
    HealOrbAura, FoxOrbTrail, WindBladeTrail, WispAura, BossAura,
  }
}
