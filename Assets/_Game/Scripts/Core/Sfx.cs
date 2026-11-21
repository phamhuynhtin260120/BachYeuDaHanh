using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace BachYeu {
  /// <summary>Sound moments. Append only: SoundSet rows are saved by value.</summary>
  public enum SoundId {
    UiClick, UiBack, UiOpen,
    Swing, SwingBig, Hit, HitBig, EnemySpawn, EnemyDeath, BossSpawn, BossDeath,
    Fire, Wind, Water, Stone, Swap, Dash, Blink, Ult, Shield, Heal, Leap,
    Hurt, Downed, Revived, Ping, Warning, LanternTaken, LanternLit, Pickup,
    Victory, Defeat, PhaseClear,
  }

  public enum MusicId { Menu, Battle, Boss }

  /// <summary>
  /// Plays sound effects and music. Created automatically, survives scene loads.
  /// Clip priority: SoundSet row, then the clip a VFX prefab carries, then the Synth placeholder.
  /// </summary>
  public class Sfx : MonoBehaviour {
    public static Sfx I { get; private set; }

    const int Voices = 14;
    readonly List<AudioSource> _pool = new();
    int _next;
    AudioSource _musicA, _musicB;
    MusicId? _music;
    float _musicSlotVol = 1;
    readonly Dictionary<SoundId, AudioClip> _synth = new();
    readonly Dictionary<MusicId, AudioClip> _musicClips = new();
    readonly HashSet<MusicId> _musicPending = new();
    readonly Dictionary<SoundId, float> _last = new();
    SoundSet _set;
    bool _setLoaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() {
      GameSettings.Load();
      if (I) return;
      var go = new GameObject("Sfx");
      DontDestroyOnLoad(go);
      go.AddComponent<Sfx>();
    }

    void Awake() {
      I = this;
      for (int i = 0; i < Voices; i++) { var s = gameObject.AddComponent<AudioSource>(); s.playOnAwake = false; s.spatialBlend = 0; _pool.Add(s); }
      _musicA = gameObject.AddComponent<AudioSource>(); _musicB = gameObject.AddComponent<AudioSource>();
      foreach (var m in new[] { _musicA, _musicB }) { m.loop = true; m.playOnAwake = false; m.volume = 0; m.priority = 0; }
      // Make every placeholder sound on a worker thread so the first hit of each kind has no hitch.
      SynthClip(SoundId.UiClick);
      Task.Run(() => {
        foreach (SoundId id in Enum.GetValues(typeof(SoundId)))
          try { _ready.Enqueue((id, Synth.Make(id))); } catch (Exception e) { Debug.LogException(e); }
      });
    }

    readonly System.Collections.Concurrent.ConcurrentQueue<(SoundId id, float[] data)> _ready = new();

    void OnDestroy() { if (I == this) I = null; }

    SoundSet Set {
      get { if (!_setLoaded) { _set = Resources.Load<SoundSet>("SoundSet"); _setLoaded = true; } return _set; }
    }

    static float Curve(float v) => v * v; // sliders feel linear to the ear

    // ---------------- effects ----------------

    /// <summary>Plays a sound. pos = where it happened (quieter when far from you); clip = a clip carried by the VFX prefab.</summary>
    public static void Play(SoundId id, Vector3? pos = null, AudioClip vfxClip = null, float volume = 1f) {
      if (I) I.PlayInternal(id, pos, vfxClip, volume);
    }

    void PlayInternal(SoundId id, Vector3? pos, AudioClip vfxClip, float volume) {
      float master = Curve(GameSettings.SfxVolume);
      if (master <= 0.001f) return;
      float now = Time.unscaledTime;
      float gap = id is SoundId.Hit or SoundId.HitBig or SoundId.EnemyDeath ? .05f : .03f;
      if (_last.TryGetValue(id, out var t) && now - t < gap) return;
      _last[id] = now;

      if (pos.HasValue) {
        var me = PlayerAvatar.Local;
        var ear = me ? me.transform.position : (Camera.main ? Camera.main.transform.position : pos.Value);
        float d = Fx.Flat(ear, pos.Value);
        volume *= Mathf.Clamp01(1.25f - d / 22f);
        if (volume <= .02f) return;
      }

      AudioClip clip; float pitchJ = .05f, slotVol = 1;
      var slot = Set ? Set.Sound(id) : null;
      if (slot != null) { clip = slot.Clips[UnityEngine.Random.Range(0, slot.Clips.Length)]; pitchJ = slot.PitchJitter; slotVol = slot.Volume; }
      else if (vfxClip) { clip = vfxClip; pitchJ = .03f; slotVol = .55f; }
      else clip = SynthClip(id);
      if (!clip) return;

      var src = _pool[_next]; _next = (_next + 1) % _pool.Count;
      src.pitch = 1 + UnityEngine.Random.Range(-pitchJ, pitchJ);
      src.PlayOneShot(clip, Mathf.Clamp01(master * slotVol * volume));
    }

    AudioClip SynthClip(SoundId id) {
      if (_synth.TryGetValue(id, out var c) && c) return c;
      c = Synth.Clip("sfx_" + id, Synth.Make(id));
      _synth[id] = c;
      return c;
    }

    /// <summary>Sound for a player effect (RPC_Fx).</summary>
    public static SoundId? For(PlayerAvatar.FxKind k) => k switch {
      PlayerAvatar.FxKind.FoxSlash => SoundId.Swing,
      PlayerAvatar.FxKind.FoxSlashBig => SoundId.SwingBig,
      PlayerAvatar.FxKind.WindSlash => SoundId.SwingBig,
      PlayerAvatar.FxKind.WaterWhip => SoundId.Water,
      PlayerAvatar.FxKind.StonePunch => SoundId.Stone,
      PlayerAvatar.FxKind.FoxOrbs => SoundId.Fire,
      PlayerAvatar.FxKind.Blink => SoundId.Blink,
      PlayerAvatar.FxKind.Tornado => SoundId.Wind,
      PlayerAvatar.FxKind.LeapLand => SoundId.Leap,
      PlayerAvatar.FxKind.InkRing => SoundId.Leap,
      PlayerAvatar.FxKind.Dash => SoundId.Dash,
      PlayerAvatar.FxKind.SwapToFox or PlayerAvatar.FxKind.SwapToWind or PlayerAvatar.FxKind.SwapToWater or PlayerAvatar.FxKind.SwapToStone => SoundId.Swap,
      PlayerAvatar.FxKind.FoxUlt or PlayerAvatar.FxKind.WindUlt or PlayerAvatar.FxKind.WaterUlt or PlayerAvatar.FxKind.StoneUlt => SoundId.Ult,
      PlayerAvatar.FxKind.WaterSpring => SoundId.Heal,
      PlayerAvatar.FxKind.WaterShield or PlayerAvatar.FxKind.StoneGuard => SoundId.Shield,
      _ => null,
    };

    /// <summary>Sound for a game moment (ArtSet.Play).</summary>
    public static SoundId? For(GameFx k) => k switch {
      GameFx.EnemySpawn => SoundId.EnemySpawn,
      GameFx.EnemyHit or GameFx.FoxOrbHit => SoundId.Hit,
      GameFx.EnemyHitBig => SoundId.HitBig,
      GameFx.EnemyDeath => SoundId.EnemyDeath,
      GameFx.BossSpawn => SoundId.BossSpawn,
      GameFx.BossDeath => SoundId.BossDeath,
      GameFx.WarningImpact or GameFx.WarningImpactBig => SoundId.Warning,
      GameFx.LanternTakenLam or GameFx.LanternTakenChu or GameFx.LanternTakenWisp => SoundId.LanternTaken,
      GameFx.LanternLit => SoundId.LanternLit,
      GameFx.HealOrbPickup => SoundId.Pickup,
      GameFx.PlayerDowned => SoundId.Downed,
      GameFx.PlayerRevived => SoundId.Revived,
      GameFx.Ping => SoundId.Ping,
      GameFx.StoneWallRise => SoundId.Stone,
      _ => null,
    };

    // ---------------- music ----------------

    /// <summary>Cross-fades to a music track (no-op if it is already playing).</summary>
    public static void Music(MusicId id) { if (I) I.SetMusic(id); }

    void SetMusic(MusicId id) {
      if (_music == id) return;
      var slot = Set ? Set.Track(id) : null;
      AudioClip clip = slot != null ? slot.Clip : null;
      if (!clip && !_musicClips.TryGetValue(id, out clip)) { BuildMusic(id); return; } // plays when ready
      _music = id;
      _musicSlotVol = slot != null ? slot.Volume : .8f;
      var old = _musicA; _musicA = _musicB; _musicB = old; // A = incoming, B = outgoing
      _musicA.clip = clip; _musicA.volume = 0; _musicA.time = 0; _musicA.Play();
    }

    async void BuildMusic(MusicId id) {
      if (!_musicPending.Add(id)) return;
      float[] data = null;
      try { data = await Task.Run(() => Synth.Music(id)); } catch (Exception e) { Debug.LogException(e); }
      _musicPending.Remove(id);
      if (!this || data == null) return;
      _musicClips[id] = Synth.Clip("music_" + id, data);
      _wanted = _wanted ?? id;
      if (_wanted == id) SetMusic(id);
    }

    MusicId? _wanted;


    // ---------------- per frame: music state, fades, UI taps ----------------

    RunState _lastState = RunState.Lobby;
    bool _hadDirector;

    void Update() {
      while (_ready.TryDequeue(out var r)) if (!_synth.ContainsKey(r.id)) _synth[r.id] = Synth.Clip("sfx_" + r.id, r.data);

      // Which music fits right now.
      var dir = GameDirector.Instance;
      MusicId want = MusicId.Menu;
      if (dir && dir.Object && dir.Object.IsValid && dir.State != RunState.Lobby) {
        want = MusicId.Battle;
        foreach (var e in Enemy.All) if (e && e.Alive && e.IsBoss) { want = MusicId.Boss; break; }
        if (!_hadDirector) _lastState = dir.State;
        if (dir.State != _lastState) {
          if (dir.State == RunState.Won) Play(SoundId.Victory);
          else if (dir.State == RunState.Lost) Play(SoundId.Defeat);
          else if (dir.State == RunState.Cleared) Play(SoundId.PhaseClear);
          _lastState = dir.State;
        }
        _hadDirector = true;
      } else _hadDirector = false;
      _wanted = want;
      SetMusic(want);

      // Fades: incoming up, outgoing down. Quieter on the result screen so the jingle reads.
      bool ended = dir && dir.Object && dir.Object.IsValid && (dir.State == RunState.Won || dir.State == RunState.Lost);
      float target = Curve(GameSettings.MusicVolume) * _musicSlotVol * (ended ? .35f : 1f);
      float k = Time.unscaledDeltaTime / 1.2f;
      _musicA.volume = Mathf.MoveTowards(_musicA.volume, target, k);
      _musicB.volume = Mathf.MoveTowards(_musicB.volume, 0, k);
      if (_musicB.isPlaying && _musicB.volume <= 0) _musicB.Stop();

      // Tap sound for ordinary UI buttons (the action pad has its own skill sounds).
      var p = Pointer.current;
      if (p != null && p.press.wasPressedThisFrame && EventSystem.current) {
        var ped = new PointerEventData(EventSystem.current) { position = p.position.ReadValue() };
        _hits.Clear();
        EventSystem.current.RaycastAll(ped, _hits);
        if (_hits.Count > 0) {
          var go = _hits[0].gameObject;
          var btn = go ? go.GetComponentInParent<UnityEngine.UI.Selectable>() : null;
          if (btn && btn.IsInteractable() && !go.GetComponentInParent<TouchButton>()) Play(SoundId.UiClick);
        }
      }
    }

    readonly List<RaycastResult> _hits = new();
  }
}
