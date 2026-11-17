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
      if (Type == EnemyType.King) _atkT = 2.5f;
      if (Fx.I) Fx.I.Burst(transform.position + Vector3.up * .5f, Fx.Ink, 8, 2);
    }

    public override void Despawned(NetworkRunner runner, bool hasState) {
      All.Remove(this);
      if (Fx.I) {
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
        if (killed) dir.OnEnemyKilled(this);
      }
      if (killed) Runner.Despawn(Object);
    }

    /// <summary>Pull or push without damage (used by the tornado).</summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Push(Vector3 velocity) {
      if (Hp > 0 && !IsBoss) _kb = velocity;
    }

    /// <summary>Called on every client by the director when this enemy is hit.</summary>
    public void PlayHitFlash() {
      _flash = .08f;
      AnimUtil.Trigger(_anim, "Hit");
    }

    // ---------------- simulation (state authority only) ----------------
    public override void FixedUpdateNetwork() {
      if (!HasStateAuthority) return;
      float dt = Runner.DeltaTime;
      _age += dt;
      if (_age < .5f) return;
      var director = GameDirector.Instance;
      if (director == null || director.State != RunState.Playing) return;

      _hitCd -= dt;
      var p = transform.position;
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
              director.Telegraph(p, 2.5f, .95f, 18);
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
          if (!Rage && Hp < MaxHp / 2) { Rage = true; director.RPC_Banner("!", "Miêu yêu nổi giận", "Lao tới nhanh hơn, gọi thêm đèn ma"); }
          if (_st == AiState.Move) {
            if (target && dist > 2.4f) p += n * spd * dt;
            _atkT -= dt;
            if (_atkT <= 0 && target) {
              _st = AiState.Aim; _stT = Rage ? .65f : .9f; _chargeDir = n;
              director.RPC_TeleLine(p, n, 10.5f, 2.4f, _stT);
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
                director.Telegraph(tp, 1.9f, 1.1f + i * .08f, 16);
              }
            } else if (r < .75f) {
              _st = AiState.Cast; _stT = 1.35f;
              director.Telegraph(p, 5.4f, 1.3f, 22);
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
      p.y = 0;
      transform.position = p;
      if (face && target) {
        var look = target.transform.position - p; look.y = 0;
        if (look.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(look);
      }

      // Contact damage.
      if (target && _st != AiState.Wind && _hitCd <= 0 && dist < Radius + .5f) {
        target.RPC_Hurt(EnemyStats.Contact(Type));
        _hitCd = 1f;
      }
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
      float y = 0;
      if (!_custom || !AnimUtil.HasParam(_anim, "Speed")) {
        if (Type == EnemyType.Ghost) y = .15f + Mathf.Sin(_bob * 3) * .15f;
        else if (Type == EnemyType.King) y = Mathf.Sin(_bob * 1.6f) * .2f;
        else if ((Type == EnemyType.Umbrella || Type == EnemyType.Elite) && speed > 1f) y = Mathf.Abs(Mathf.Sin(_bob * 7.5f)) * .7f;
      }
      _model.localPosition = new Vector3(0, y, 0);

      if (_custom) { if (_flash > 0) _flash -= dt; return; }
      if (_flash > 0) {
        _flash -= dt;
        for (int i = 0; i < _flashMats.Count; i++) _flashMats[i].SetColor("_EmissionColor", _flash > 0 ? Color.white * .55f : _flashBase[i]);
      }
    }
  }
}
