using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace BachYeu {
  /// <summary>
  /// A yokai. Simulated only on its state authority (the master client).
  /// Other clients see it through NetworkTransform and send damage with RPC_Damage.
  /// </summary>
  public class Enemy : NetworkBehaviour {
    public static readonly List<Enemy> All = new();

    [Networked] public EnemyType Type { get; set; }
    [Networked] public int Hp { get; set; }
    [Networked] public int MaxHp { get; set; }
    [Networked] public NetworkBool Rage { get; set; }
    /// <summary>Goes up each time the yokai starts an attack; every client plays the Attack animation when it changes.</summary>
    [Networked] public byte AtkSeq { get; set; }
    byte _atkSeen;

    public bool Alive => Object != null && Object.IsValid && Hp > 0 && _age > .5f;
    public float Radius => EnemyStats.Radius(Type);
    public bool IsBoss => EnemyStats.IsBoss(Type);

    readonly List<Material> _flashMats = new();
    readonly List<Color> _flashBase = new();
    Transform _model;
    Animator _anim;
    bool _custom;
    float _baseScale = 1f;
    float _age, _flash, _bob;
    Vector3 _lastPos;

    // AI state, only meaningful on the state authority.
    enum AiState { Move, Wind, Recover, Aim, Charge, Cast }
    AiState _st;
    float _stT, _atkT = 3f, _sumT = 8f, _hitCd, _hopT, _hopCd;
    Vector3 _kb, _hopV, _chargeDir;
    GroundMover _mover;
    bool _chargeHit;

    public static Enemy Nearest(Vector3 pos, float maxDist) {
      Enemy best = null;
      float bd = maxDist;
      foreach (var e in All) {
        if (!e.Alive) continue;
        float d = Fx.Flat(e.transform.position, pos);
        if (d < bd) { bd = d; best = e; }
      }
      return best;
    }

    public override void Spawned() {
      All.Add(this);
      Bestiary.NoteSeen(Type);
      var art = ArtSet.Current;
      var (prefab, extraScale) = art ? art.EnemyPrefab(Type) : (null, 1f);
      if (prefab) {
        _custom = true;
        _model = new GameObject("Model").transform;
        _model.SetParent(transform, false);
        AnimUtil.SpawnVisual(prefab, _model);
        _anim = _model.GetComponentInChildren<Animator>();
        _baseScale = extraScale;
      } else {
        _model = Models.BuildEnemy(transform, Type, _flashMats);
        _baseScale = Type == EnemyType.Elite ? 1.7f : Type == EnemyType.King ? 1.5f : 1f;
        foreach (var m in _flashMats) _flashBase.Add(m.IsKeywordEnabled("_EMISSION") ? m.GetColor("_EmissionColor") : Color.black);
        foreach (var m in _flashMats) m.EnableKeyword("_EMISSION");
      }
      _model.localScale = Vector3.one * _baseScale * .05f;
      _lastPos = transform.position;
      // Only the machine that runs the yokai walks it over the ground; others see the synced transform.
      if (HasStateAuthority) _mover = GroundMover.Attach(gameObject, Mathf.Min(Radius, 1.2f), Type == EnemyType.King ? 3.2f : IsBoss ? 2.4f : 1.6f);
      if (Type == EnemyType.King) _atkT = 2.5f;
      if (!ArtSet.Play(IsBoss ? GameFx.BossSpawn : GameFx.EnemySpawn, transform.position) && Fx.I) Fx.I.Burst(transform.position + Vector3.up * .5f, Fx.Ink, 8, 2);
      // The wisp aura replaces the built-in flame; a bought model (Mắt Trơi) stays visible inside it.
      if (Type == EnemyType.Wisp && ArtSet.Attach(GameFx.WispAura, transform) && _model && !_custom) _model.gameObject.SetActive(false);
      if (IsBoss) ArtSet.Attach(GameFx.BossAura, transform);
    }

    public override void Despawned(NetworkRunner runner, bool hasState) {
      All.Remove(this);
      if (hasState && Hp <= 0) Bestiary.NoteSlain(Type);
      // Bought models fall over: the model is left behind as a corpse that plays its death clip, then sinks away.
      // Not for Tiểu quỷ pháo (it blew itself up) or a Bóng không đèn that was still hidden.
      if (((hasState && Hp <= 0) || _slain) && _custom && _model && Type != EnemyType.Firecracker && AnimUtil.HasParam(_anim, "Die") && (Type != EnemyType.Shade || _shadeShown))
        EnemyCorpse.Leave(_model, _anim);
      if (!ArtSet.Play(IsBoss ? GameFx.BossDeath : GameFx.EnemyDeath, transform.position) && Fx.I) {
        Fx.I.Burst(transform.position + Vector3.up, IsBoss ? Gfx.Hex("#6b4a8a") : Fx.Ink, IsBoss ? 60 : 16, IsBoss ? 6 : 3.5f);
        Fx.I.Burst(transform.position + Vector3.up, Fx.I.PetalColor, 8, 3);
      }
      foreach (var m in _flashMats) Destroy(m);
    }

    // ---------------- damage ----------------
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Damage(int dmg, Vector3 knockback, NetworkBool big, PlayerRef from) {
      if (Hp <= 0) return;
      Hp -= dmg;
      if (!IsBoss) _kb = knockback;
      bool killed = Hp <= 0;
      bool drop = killed && !IsBoss && Random.value < (Type == EnemyType.Heavy || Type == EnemyType.Elite ? .4f : .12f);
      var dir = GameDirector.Instance;
      if (dir) {
        dir.RPC_HitFx(Object.Id, transform.position, dmg, big, from, killed, drop);
        dir.OnEnemyDamaged(this, dmg + Mathf.Min(0, Hp), from);
        if (killed) dir.OnEnemyKilled(this, from);
      }
      // Not despawned at once: it lies dead for a few ticks so every machine sees Hp reach 0 (and leaves a corpse)
      // before the despawn arrives. If this kill ended the hour or the night, the cleanup has despawned it already.
      if (killed) _deadT = DeadLinger;
    }

    /// <summary>Pull or push without damage (used by the tornado).</summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Push(Vector3 velocity) {
      if (Hp > 0 && !IsBoss) _kb = velocity;
    }

    /// <summary>Called on every client by the director when this enemy is hit.</summary>
    /// <summary>
    /// Told by the hit RPC that this blow killed it. Other machines may see the despawn before the Hp change,
    /// so this (not Hp) is how they know to leave a corpse rather than vanish (a phase-end cleanup).
    /// </summary>
    bool _slain;
    const float DeadLinger = .2f;
    float _deadT;
    public void MarkSlain() => _slain = true;

    public void PlayHitFlash() {
      _flash = .08f;
      AnimUtil.Trigger(_anim, "Hit");
    }

    // ---------------- simulation (state authority only) ----------------
    public override void FixedUpdateNetwork() {
      if (!HasStateAuthority) return;
      float dt = Runner.DeltaTime;
      if (Hp <= 0) { // slain, waiting out DeadLinger: no moving, no hitting
        _deadT -= dt;
        if (_deadT <= 0 && Object && Object.IsValid) Runner.Despawn(Object);
        return;
      }
      _age += dt;
      if (_age < .5f) return;
      var director = GameDirector.Instance;
      if (director == null || director.State != RunState.Playing) return;

      _hitCd -= dt;
      var start = transform.position;
      var p = start;
      var target = PlayerAvatar.NearestAlive(p);
      Vector3 to = target ? target.transform.position - p : Vector3.zero;
      to.y = 0;
      float dist = Mathf.Max(.001f, to.magnitude);
      Vector3 n = to / dist;

      p += _kb * dt;
      _kb *= Mathf.Max(0, 1 - dt * 6);
      bool face = true;
      float spd = EnemyStats.Speed(Type);

      switch (Type) {
        case EnemyType.Ghost:
          if (target && dist > .9f) p += n * spd * dt;
          break;

        case EnemyType.Inkstone: // Nghiên mực yêu: keeps its distance and flings ink where the player stands
          if (target) {
            if (dist > 7f) p += n * spd * dt;
            else if (dist < 4.5f) p -= n * spd * .8f * dt;
            _atkT -= dt;
            if (_atkT <= 0 && dist < 10f) {
              _atkT = Rage ? 1.9f : 2.7f;
              var at = target.transform.position + new Vector3(Random.Range(-.6f, .6f), 0, Random.Range(-.6f, .6f));
              AtkSeq++; director.Telegraph(at, 1.3f, 1.05f, 10);
            }
          }
          break;

        case EnemyType.Shade: // Bóng không đèn: fast and hard to see (the hiding is done in Render)
          if (target && dist > .9f) p += n * spd * dt;
          break;

        case EnemyType.Firecracker: // Pháo yêu: runs in, lights its fuse, and blows up (taking itself with it)
          if (_st == AiState.Move) {
            if (target) p += n * spd * dt;
            if (target && dist < 1.7f) { _st = AiState.Wind; _stT = .9f; AtkSeq++; director.Telegraph(p, 2.2f, .9f, 20); }
          } else {
            face = false;
            _stT -= dt;
            if (_stT <= 0) { Hp = 0; Runner.Despawn(Object); return; }
          }
          break;

        case EnemyType.Wisp: { // Ma Trơi ignores players and drifts to the lantern worth stealing
          face = false;
          var goal = director.WispGoal(p) - p; goal.y = 0;
          if (goal.magnitude > .6f) {
            var g = goal.normalized;
            p += g * spd * dt;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(g), dt * 6f);
          }
          break;
        }

        case EnemyType.Umbrella:
        case EnemyType.Elite:
          _hopCd -= dt;
          if (_hopT > 0) { _hopT -= dt; p += _hopV * dt; }
          else if (_hopCd <= 0 && target) {
            float hs = Type == EnemyType.Elite ? 4.6f : 5.6f;
            _hopV = n * hs; _hopT = .42f; _hopCd = Type == EnemyType.Elite ? 1.3f : 1.05f;
          }
          break;

        case EnemyType.Heavy:
          if (_st == AiState.Move) {
            if (target) p += n * spd * dt;
            if (target && dist < 2.7f) {
              _st = AiState.Wind; _stT = 1f;
              AtkSeq++; director.Telegraph(p, 2.5f, .95f, 18);
            }
          } else {
            _stT -= dt;
            face = _st != AiState.Wind;
            if (_stT <= 0) {
              if (_st == AiState.Wind) { _st = AiState.Recover; _stT = .5f; }
              else _st = AiState.Move;
            }
          }
          break;

        case EnemyType.Cat:
          if (!Rage && Hp < MaxHp / 2) { Rage = true; director.RPC_Banner("!", "Hỏa Khuyển nổi giận", "Lao tới nhanh hơn, gọi thêm dơi đèn"); }
          if (_st == AiState.Move) {
            if (target && dist > 2.4f) p += n * spd * dt;
            _atkT -= dt;
            if (_atkT <= 0 && target) {
              _st = AiState.Aim; _stT = Rage ? .65f : .9f; _chargeDir = n;
              AtkSeq++; director.RPC_TeleLine(p, n, 10.5f, 2.4f, _stT);
            }
          } else if (_st == AiState.Aim) {
            face = false;
            transform.rotation = Quaternion.LookRotation(_chargeDir);
            _stT -= dt;
            if (_stT <= 0) { _st = AiState.Charge; _stT = .6f; _chargeHit = false; }
          } else if (_st == AiState.Charge) {
            face = false;
            _stT -= dt;
            p += _chargeDir * 17f * dt;
            if (!_chargeHit && target && dist < Radius + .7f) { _chargeHit = true; target.RPC_Hurt(18); }
            var flat = new Vector2(p.x, p.z);
            if (_stT <= 0 || flat.magnitude > 12.5f) { _st = AiState.Move; _atkT = Random.Range(2.2f, 3.2f) * (Rage ? .7f : 1f); }
          }
          _sumT -= dt;
          if (_sumT <= 0) {
            _sumT = Rage ? 7 : 9;
            for (int i = 0; i < 2; i++) director.SpawnEnemy(EnemyType.Ghost, p + new Vector3(Random.Range(-3f, 3f), 0, Random.Range(-3f, 3f)));
          }
          break;

        case EnemyType.King:
          if (!Rage && Hp < MaxHp / 2) {
            Rage = true;
            director.RPC_Banner("!", "Quỷ Vương nổi giận", "Nhiều đòn mực hơn, ra đòn nhanh hơn");
            director.SpawnEnemy(EnemyType.Heavy, new Vector3(-5, 0, 4));
            director.SpawnEnemy(EnemyType.Heavy, new Vector3(5, 0, 4));
          }
          if (_st == AiState.Cast) { _stT -= dt; if (_stT <= 0) _st = AiState.Move; }
          else if (target && dist > 4.5f) p += n * spd * dt;
          _atkT -= dt;
          if (_atkT <= 0 && _st != AiState.Cast && target) {
            _atkT = Rage ? 2.3f : 3.3f;
            float r = Random.value;
            if (r < .45f) {
              int count = Rage ? 5 : 3;
              for (int i = 0; i < count; i++) {
                var tp = target.transform.position + (i == 0 ? Vector3.zero : new Vector3(Random.Range(-3.5f, 3.5f), 0, Random.Range(-3.5f, 3.5f)));
                AtkSeq++; director.Telegraph(tp, 1.9f, 1.1f + i * .08f, 16);
              }
            } else if (r < .75f) {
              _st = AiState.Cast; _stT = 1.35f;
              AtkSeq++; director.Telegraph(p, 5.4f, 1.3f, 22);
            } else {
              for (int i = 0; i < 3; i++)
                director.SpawnEnemy(Random.value < .5f ? EnemyType.Ghost : EnemyType.Umbrella, p + new Vector3(Random.Range(-4f, 4f), 0, Random.Range(-4f, 4f)));
            }
          }
          break;
      }

      // Keep yokai apart from each other and out of the players.
      foreach (var o in All) {
        if (o == this || !o.Alive) continue;
        var d = p - o.transform.position; d.y = 0;
        float m = Radius + o.Radius, l = d.magnitude;
        if (l > 0 && l < m) p += d / l * (m - l) * 3f * dt;
      }
      if (target && _st != AiState.Charge) {
        var d = p - target.transform.position; d.y = 0;
        float m = Radius + .45f, l = d.magnitude;
        if (l > 0 && l < m) p += d / l * (m - l);
      }
      var fl = new Vector2(p.x, p.z);
      if (fl.magnitude > 12.8f) { fl = fl.normalized * 12.8f; p.x = fl.x; p.z = fl.y; }
      if (_mover) p = _mover.Move(p - start, dt);
      else { p.y = 0; transform.position = p; }
      if (face && target) {
        var look = target.transform.position - p; look.y = 0;
        if (look.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(look);
      }

      // Contact damage.
      if (target && EnemyStats.Contact(Type) > 0 && _st != AiState.Wind && _hitCd <= 0 && dist < Radius + .5f) {
        AtkSeq++; target.RPC_Hurt(EnemyStats.Contact(Type));
        _hitCd = 1f;
      }
    }

    // Bóng không đèn: only its ground mark shows until a player is close, or while it is being hit.
    Renderer[] _shadeParts;
    bool _shadeShown = true;
    void UpdateShade() {
      if (_shadeParts == null) _shadeParts = System.Array.FindAll(_model.GetComponentsInChildren<Renderer>(true), r => r.name != "Shadow" && r.enabled); // rig helpers stay off
      var me = PlayerAvatar.Local;
      bool show = _flash > 0 || (me && Fx.Flat(me.transform.position, transform.position) < 4.5f) || Hp <= 0;
      if (show == _shadeShown) return;
      _shadeShown = show;
      foreach (var r in _shadeParts) if (r) r.enabled = show;
    }

    // ---------------- visuals (all clients) ----------------
    public override void Render() {
      float dt = Time.deltaTime;
      if (!HasStateAuthority) _age += dt;
      float spawnK = Mathf.Clamp01(_age / .5f);
      float punch = _custom && _flash > 0 ? 1f + _flash * 1.5f : 1f; // custom models punch-scale on hit instead of flashing
      _model.localScale = Vector3.one * Mathf.Max(.05f, spawnK) * _baseScale * punch;

      var pos = transform.position;
      float speed = Fx.Flat(pos, _lastPos) / Mathf.Max(dt, .0001f);
      _lastPos = pos;
      _bob += dt;
      AnimUtil.Float(_anim, "Speed", speed);
      if (AtkSeq != _atkSeen) { _atkSeen = AtkSeq; AnimUtil.Trigger(_anim, "Attack"); }
      float y = 0;
      if (!_custom || !AnimUtil.HasParam(_anim, "Speed")) {
        if (Type == EnemyType.Ghost) y = .15f + Mathf.Sin(_bob * 3) * .15f;
        else if (Type == EnemyType.King) y = Mathf.Sin(_bob * 1.6f) * .2f;
        else if ((Type == EnemyType.Umbrella || Type == EnemyType.Elite) && speed > 1f) y = Mathf.Abs(Mathf.Sin(_bob * 7.5f)) * .7f;
      }
      if (Type == EnemyType.Firecracker && _st == AiState.Wind) y = Mathf.Abs(Mathf.Sin(_bob * 30f)) * .12f; // shaking before the blast
      _model.localPosition = new Vector3(0, y, 0);
      if (Type == EnemyType.Shade) UpdateShade(); // custom models: keep a child named "Shadow" to show while hidden

      if (_custom) { if (_flash > 0) _flash -= dt; return; }
      if (_flash > 0) {
        _flash -= dt;
        for (int i = 0; i < _flashMats.Count; i++) _flashMats[i].SetColor("_EmissionColor", _flash > 0 ? Color.white * .55f : _flashBase[i]);
      }
    }
  }
}
