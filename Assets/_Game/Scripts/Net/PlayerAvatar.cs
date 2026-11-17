using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace BachYeu {
  /// <summary>
  /// A player. Each client has state authority over its own avatar (shared mode):
  /// movement, skills and hit detection run locally; damage to yokai goes out by RPC.
  /// </summary>
  public class PlayerAvatar : NetworkBehaviour {
    public static readonly List<PlayerAvatar> All = new();
    public static PlayerAvatar Local { get; private set; }

    [Networked] public int Hp { get; set; }
    [Networked] public int MaxHp { get; set; }
    [Networked, OnChangedRender(nameof(OnMaskChanged))] public int Mask { get; set; }
    [Networked, OnChangedRender(nameof(OnDownedChanged))] public NetworkBool Downed { get; set; }
    [Networked] public float ReviveProgress { get; set; }

    public Blessings B { get; private set; } = new();
    public float Ult { get; private set; }
    public float ResonanceTime { get; private set; }
    public float SwapCooldown => _swapCd;
    public float DashCooldown => _dashCd;
    public float SkillCooldown(int skill) => skill == 1 ? _cd[Mask, 0] : _cd[Mask, 1];
    public bool Grounded => _leapT < 0 && !Downed;
    public string DisplayName => "Người chơi " + Object.StateAuthority.PlayerId.ToString();

    const float Speed = 5.2f;
    readonly float[,] _cd = new float[2, 2];
    float _swapCd, _dashCd, _atkCd, _inv, _comboT, _dashT, _leapT = -1;
    int _combo;
    float _yaw = 0;
    Vector3 _dashDir, _leapFrom, _leapTo, _moveDir;

    PlayerVisual _vis;
    Vector3 _lastPos;
    float _bob;

    public static PlayerAvatar NearestAlive(Vector3 pos) {
      PlayerAvatar best = null;
      float bd = float.MaxValue;
      foreach (var p in All) {
        if (p.Downed) continue;
        float d = Fx.Flat(p.transform.position, pos);
        if (d < bd) { bd = d; best = p; }
      }
      return best;
    }

    public override void Spawned() {
      All.Add(this);
      _vis = new PlayerVisual(transform);
      _vis.ApplyMask(Mask);
      _lastPos = transform.position;
      if (HasStateAuthority) {
        Local = this;
        MaxHp = 100;
        Hp = MaxHp;
        CameraRig.SetTarget(transform);
        GameInput.ClearAll();
      }
      OnDownedChanged();
    }

    public override void Despawned(NetworkRunner runner, bool hasState) {
      All.Remove(this);
      if (Local == this) Local = null;
    }

    void Update() {
      if (Object && Object.IsValid && HasStateAuthority) GameInput.PollKeyboard();
    }

    // ---------------- simulation (own client only) ----------------
    public override void FixedUpdateNetwork() {
      if (!HasStateAuthority) return;
      float dt = Runner.DeltaTime;
      var director = GameDirector.Instance;
      bool playing = director && director.State == RunState.Playing;

      for (int m = 0; m < 2; m++) for (int s = 0; s < 2; s++) _cd[m, s] -= dt;
      _swapCd -= dt; _dashCd -= dt; _atkCd -= dt; _inv -= dt; _comboT -= dt;
      ResonanceTime -= dt;

      if (Downed) { TickRevive(dt); GameInput.ClearAll(); return; }

      var mv = GameInput.Move;
      _moveDir = new Vector3(mv.x, 0, mv.y);
      var pos = transform.position;

      if (_leapT >= 0) {
        _leapT += dt;
        float k = Mathf.Min(1, _leapT / .38f);
        pos = Vector3.Lerp(_leapFrom, _leapTo, k);
        pos.y = Mathf.Sin(k * Mathf.PI) * 2.2f;
        if (k >= 1) { pos.y = 0; _leapT = -1; transform.position = pos; LeapLand(); }
      } else if (_dashT > 0) {
        _dashT -= dt;
        pos += _dashDir * 19f * dt;
      } else if (_moveDir.sqrMagnitude > .02f) {
        pos += _moveDir * Speed * dt;
        if (_atkCd <= 0) _yaw = Mathf.Atan2(_moveDir.x, _moveDir.z) * Mathf.Rad2Deg;
      }

      var flat = new Vector2(pos.x, pos.z);
      if (flat.magnitude > ArenaBuilder.PlayRadius) { flat = flat.normalized * ArenaBuilder.PlayRadius; pos.x = flat.x; pos.z = flat.y; }
      if (_leapT < 0) pos.y = 0;
      transform.SetPositionAndRotation(pos, Quaternion.Euler(0, _yaw, 0));

      if (!playing) { GameInput.ClearAll(); return; }
      if (GameInput.AttackHeld) Attack();
      if (GameInput.Consume(GameAction.Skill1)) Skill(0);
      if (GameInput.Consume(GameAction.Skill2)) Skill(1);
      if (GameInput.Consume(GameAction.Swap)) Swap();
      if (GameInput.Consume(GameAction.Dash)) Dash();
      if (GameInput.Consume(GameAction.Ult)) UltCast();
    }

    void TickRevive(float dt) {
      bool helped = false;
      foreach (var p in All)
        if (p != this && !p.Downed && Fx.Flat(p.transform.position, transform.position) < 2.2f) helped = true;
      ReviveProgress = helped ? ReviveProgress + dt / 3f : Mathf.Max(0, ReviveProgress - dt * .5f);
      if (ReviveProgress >= 1f) {
        Downed = false;
        ReviveProgress = 0;
        Hp = Mathf.RoundToInt(MaxHp * .4f);
        _inv = 1.5f;
      }
    }

    // ---------------- helpers ----------------
    Vector3 Forward => Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
    Vector3 Flat(Vector3 v) { v.y = 0; return v; }

    void AutoFace() {
      var n = Enemy.Nearest(transform.position, 7f);
      if (n == null) return;
      var d = Flat(n.transform.position - transform.position);
      if (d.sqrMagnitude > .001f) _yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    float DamageMul => ResonanceTime > 0 ? 1f + B.Res : 1f;

    void Hit(Enemy e, int baseDmg, float kb = 0, bool big = false, float stop = 0) {
      if (e == null || !e.Alive) return;
      int dmg = Mathf.RoundToInt(baseDmg * DamageMul * Random.Range(.92f, 1.08f));
      var dir = Flat(e.transform.position - transform.position);
      var kbv = dir.sqrMagnitude > .001f ? dir.normalized * kb * 7f : Vector3.zero;
      e.RPC_Damage(dmg, kbv, big, Runner.LocalPlayer);
      AddUlt(1.6f * B.UltMul);
      if (stop > 0) Fx.Hitstop(stop);
    }

    void HitRadius(Vector3 center, float r, int dmg, float kb = 0, bool big = false, float stop = 0) {
      foreach (var e in Enemy.All.ToArray())
        if (e.Alive && Fx.Flat(e.transform.position, center) < r + e.Radius) Hit(e, dmg, kb, big, stop);
    }

    void HitArc(float r, float halfAngleRad, int dmg, float kb, float stop) {
      var f = Forward;
      foreach (var e in Enemy.All.ToArray()) {
        if (!e.Alive) continue;
        var d = Flat(e.transform.position - transform.position);
        float l = d.magnitude;
        if (l > r + e.Radius) continue;
        if (l < e.Radius + .7f || Vector3.Dot(d / Mathf.Max(l, .001f), f) >= Mathf.Cos(halfAngleRad)) Hit(e, dmg, kb, false, stop);
      }
    }

    // ---------------- actions ----------------
    void Attack() {
      if (_atkCd > 0 || _dashT > 0 || _leapT >= 0) return;
      AutoFace();
      if (Mask == 0) {
        if (_comboT <= 0) _combo = 0;
        _comboT = .9f;
        int i = _combo % 3; _combo++;
        bool fin = i == 2;
        HitArc(2.5f, 1.3f, fin ? 48 : 28, fin ? 1f : .25f, fin ? .05f : 0);
        _atkCd = Masks.All[0].AtkCd * (fin ? 1.6f : 1f);
        RPC_Fx(fin ? FxKind.FoxSlashBig : FxKind.FoxSlash, transform.position, _yaw, Vector3.zero);
        if (B.FoxAuto > 0 && Random.value < B.FoxAuto) FoxOrb(Enemy.Nearest(transform.position, 10));
      } else {
        HitArc(3f, 1.9f, 38, 1.1f, .03f);
        _atkCd = Masks.All[1].AtkCd;
        RPC_Fx(FxKind.WindSlash, transform.position, _yaw, Vector3.zero);
        if (B.WindBlade) Fx.I.WindBlade(transform.position, Forward, e => Hit(e, 30, .4f));
      }
    }

    void FoxOrb(Enemy target) {
      Fx.I.FoxOrb(transform.position, Forward, target, e => Hit(e, 62, 0, false, .02f));
    }

    void Skill(int s) {
      if (_leapT >= 0) return;
      var def = Masks.All[Mask];
      if (_cd[Mask, s] > 0) return;
      _cd[Mask, s] = (s == 0 ? def.S1Cd : def.S2Cd) * B.CdMul;
      if (Mask == 0 && s == 0) {
        AutoFace();
        var targets = new List<Enemy>();
        foreach (var e in Enemy.All) if (e.Alive && Fx.Flat(e.transform.position, transform.position) < 13) targets.Add(e);
        targets.Sort((a, b) => Fx.Flat(a.transform.position, transform.position).CompareTo(Fx.Flat(b.transform.position, transform.position)));
        for (int i = 0; i < 3; i++) FoxOrb(targets.Count > 0 ? targets[i % targets.Count] : null);
        RPC_Fx(FxKind.FoxOrbs, transform.position, _yaw, Vector3.zero);
      } else if (Mask == 0) {
        Blink();
      } else if (s == 0) {
        AutoFace();
        var c = transform.position + Forward * 3.2f;
        var fl = new Vector2(c.x, c.z);
        if (fl.magnitude > 12) { fl = fl.normalized * 12; c = new Vector3(fl.x, 0, fl.y); }
        Fx.I.TornadoFx(c, center => HitRadius(center, 2.7f, 22), (center, dt) => {
          foreach (var e in Enemy.All) {
            if (!e.Alive || e.IsBoss) continue;
            var d = Flat(center - e.transform.position);
            if (d.magnitude < 4.9f && d.magnitude > .4f && Random.value < dt * 4) e.RPC_Push(d.normalized * 5f);
          }
        });
        RPC_Fx(FxKind.Tornado, c, _yaw, Vector3.zero);
      } else {
        Leap();
      }
    }

    void Blink() {
      var dir = _moveDir.sqrMagnitude > .02f ? _moveDir.normalized : (AutoFaceAndForward());
      var a = Flat(transform.position);
      var b = a + dir * 5.2f;
      var fl = new Vector2(b.x, b.z);
      if (fl.magnitude > 12) { fl = fl.normalized * 12; b = new Vector3(fl.x, 0, fl.y); }
      foreach (var e in Enemy.All.ToArray())
        if (e.Alive && SegDist(e.transform.position, a, b) < 1.3f + e.Radius) Hit(e, 85, .4f, false, .04f);
      transform.position = b;
      _yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
      _inv = Mathf.Max(_inv, .35f);
      RPC_Fx(FxKind.Blink, a, _yaw, b);
    }

    Vector3 AutoFaceAndForward() { AutoFace(); return Forward; }

    static float SegDist(Vector3 p, Vector3 a, Vector3 b) {
      var ab = b - a; ab.y = 0;
      var ap = p - a; ap.y = 0;
      float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / Mathf.Max(ab.sqrMagnitude, .0001f));
      return (ap - ab * t).magnitude;
    }

    void Leap() {
      var t = Enemy.Nearest(transform.position, 9);
      Vector3 to;
      if (t != null) {
        var d = Flat(t.transform.position - transform.position);
        to = t.transform.position - d.normalized * 1.1f;
        _yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
      } else {
        var f = _moveDir.sqrMagnitude > .02f ? _moveDir.normalized : Forward;
        to = transform.position + f * 5;
      }
      to.y = 0;
      var fl = new Vector2(to.x, to.z);
      if (fl.magnitude > 12) { fl = fl.normalized * 12; to = new Vector3(fl.x, 0, fl.y); }
      _leapFrom = Flat(transform.position);
      _leapTo = to;
      _leapT = 0;
      _inv = Mathf.Max(_inv, .6f);
    }

    void LeapLand() {
      HitRadius(transform.position, 3.3f, 130, 1.6f, false, .07f);
      RPC_Fx(FxKind.LeapLand, transform.position, _yaw, Vector3.zero);
    }

    void Swap() {
      if (_swapCd > 0 || _leapT >= 0) return;
      Mask = 1 - Mask;
      _swapCd = 2.5f * B.SwapMul;
      ResonanceTime = B.ResDur;
      if (Mask == 1) { HitRadius(transform.position, 3.6f, 55, 2f); RPC_Fx(FxKind.SwapToWind, transform.position, _yaw, Vector3.zero); }
      else { HitRadius(transform.position, 3.2f, 65, .8f); RPC_Fx(FxKind.SwapToFox, transform.position, _yaw, Vector3.zero); }
    }

    void Dash() {
      if (_dashCd > 0 || _leapT >= 0) return;
      _dashCd = 1.1f;
      _dashT = .18f;
      _inv = Mathf.Max(_inv, .3f);
      _dashDir = _moveDir.sqrMagnitude > .02f ? _moveDir.normalized : Forward;
      RPC_Fx(FxKind.Dash, transform.position, _yaw, Vector3.zero);
      if (B.InkDash) {
        var end = transform.position + _dashDir * 19f * .18f;
        HitRadius(end, 3f, 50);
        RPC_Fx(FxKind.InkRing, end, _yaw, Vector3.zero);
      }
    }

    void UltCast() {
      if (Ult < 100 || _leapT >= 0) return;
      Ult = 0;
      Fx.Hitstop(.12f);
      CameraRig.Shake(.45f);
      var c = Flat(transform.position);
      if (Mask == 0) {
        RPC_Fx(FxKind.FoxUlt, c, _yaw, Vector3.zero);
        for (int i = 0; i < 3; i++) { int k = i; DelayLocal(.15f + i * .22f, () => HitRadius(transform.position, 7.5f, 115, .5f, k == 2, k == 2 ? .08f : 0)); }
      } else {
        RPC_Fx(FxKind.WindUlt, c, _yaw, Vector3.zero);
        for (int i = 0; i < 5; i++) { int k = i; DelayLocal(i * .17f, () => HitRadius(transform.position, 7f, 105, .7f, k == 4, k == 4 ? .08f : 0)); }
      }
    }

    void DelayLocal(float t, System.Action a) => StartCoroutine(DelayCo(t, a));
    System.Collections.IEnumerator DelayCo(float t, System.Action a) { yield return new WaitForSeconds(t); if (Object && Object.IsValid) a(); }

    // ---------------- health ----------------
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Hurt(int dmg) => ApplyHurt(dmg);

    public void ApplyHurt(int dmg) {
      if (!HasStateAuthority || Downed || _inv > 0 || _dashT > 0 || _leapT >= 0) return;
      var dir = GameDirector.Instance;
      if (dir == null || dir.State != RunState.Playing) return;
      Hp -= dmg;
      _inv = .7f;
      CameraRig.Shake(.25f);
      if (Hud.I) Hud.I.HurtFlash();
      _vis?.Trigger("Hit");
      if (Hp <= 0) { Hp = 0; Downed = true; ReviveProgress = 0; }
    }

    public void Heal(int amount) {
      if (!HasStateAuthority || Downed) return;
      Hp = Mathf.Min(MaxHp, Hp + amount);
    }

    public void AddMaxHp(int amount) { MaxHp += amount; Hp = MaxHp; }
    public void AddUlt(float v) => Ult = Mathf.Min(100, Ult + v);

    public void OnKillCredit() {
      AddUlt(4 * B.UltMul);
      if (B.Leech > 0) Heal(B.Leech);
    }

    public void ApplyBlessing(BlessingDef d, float healFraction) {
      d.Apply(this);
      B.Picked.Add(d.Name);
      Heal(Mathf.RoundToInt(MaxHp * healFraction));
    }

    /// <summary>Local player: called on every phase start. Revives and regroups at the start line.</summary>
    public void OnPhaseStarted(int phase) {
      if (!HasStateAuthority) return;
      if (Downed) { Downed = false; Hp = Mathf.Max(1, Mathf.RoundToInt(MaxHp * .3f)); }
      ReviveProgress = 0;
      int slot = Runner.LocalPlayer.PlayerId % 4;
      var start = new Vector3(-1.5f + slot * 1f, 0, -5f);
      _leapT = -1; _dashT = 0;
      _yaw = 0;
      if (TryGetComponent<NetworkTransform>(out var nt)) nt.Teleport(start, Quaternion.identity);
      else transform.SetPositionAndRotation(start, Quaternion.identity);
    }

    public void ResetForNewRun() {
      if (!HasStateAuthority) return;
      B = new Blessings();
      MaxHp = 100;
      Hp = MaxHp;
      Downed = false;
      Mask = 0;
      Ult = 0;
      ResonanceTime = 0;
      System.Array.Clear(_cd, 0, _cd.Length);
      _swapCd = _dashCd = _atkCd = 0;
    }

    // ---------------- visuals (all clients) ----------------
    public enum FxKind : byte { FoxSlash, FoxSlashBig, WindSlash, FoxOrbs, Blink, Tornado, LeapLand, SwapToWind, SwapToFox, Dash, InkRing, FoxUlt, WindUlt }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_Fx(FxKind kind, Vector3 pos, float yaw, Vector3 extra) {
      var fx = Fx.I;
      if (!fx) return;
      var fwd = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
      bool mine = HasStateAuthority;
      _vis?.Trigger(kind switch {
        FxKind.FoxSlash or FxKind.FoxSlashBig or FxKind.WindSlash => "Attack",
        FxKind.SwapToWind or FxKind.SwapToFox => "Swap",
        FxKind.Dash => "Dash",
        FxKind.FoxUlt or FxKind.WindUlt => "Ult",
        FxKind.InkRing => "",
        _ => "Skill",
      });
      switch (kind) {
        case FxKind.FoxSlash: fx.Slash(pos, yaw, 2.3f, false, Fx.FireDeep); break;
        case FxKind.FoxSlashBig: fx.Slash(pos, yaw, 2.3f, true, Fx.FireDeep); fx.Burst(pos + fwd + Vector3.up, fx.PetalColor, 8, 3); break;
        case FxKind.WindSlash: fx.Slash(pos, yaw, 2.9f, true, Fx.Wind); break;
        case FxKind.FoxOrbs:
          if (!mine) for (int i = 0; i < 3; i++) fx.FoxOrb(pos, fwd, null, null);
          break;
        case FxKind.Blink:
          for (int i = 0; i <= 8; i++) fx.Burst(Vector3.Lerp(pos, extra, i / 8f) + Vector3.up * .8f, i % 2 == 0 ? Fx.Fire : fx.PetalColor, 3, 1.5f);
          break;
        case FxKind.Tornado:
          if (!mine) fx.TornadoFx(pos, null, null);
          break;
        case FxKind.LeapLand:
          fx.RingFx(pos, Fx.Wind, 3.6f);
          fx.Burst(pos + Vector3.up * .4f, Fx.WindLight, 24, 5);
          CameraRig.Shake(mine ? .3f : .1f);
          break;
        case FxKind.SwapToWind:
          fx.RingFx(pos, Fx.Wind, 3.8f);
          fx.Burst(pos + Vector3.up, Fx.WindLight, 18, 4);
          break;
        case FxKind.SwapToFox:
          fx.RingFx(pos, Fx.FireDeep, 3.4f);
          fx.Burst(pos + Vector3.up, Fx.Fire, 26, 5);
          break;
        case FxKind.Dash:
          fx.Burst(pos + Vector3.up * .6f, fx.PetalColor, 8, 1.5f);
          break;
        case FxKind.InkRing:
          fx.RingFx(pos, Fx.Ink, 3f);
          break;
        case FxKind.FoxUlt:
          for (int i = 0; i < 8; i++) {
            float a = i / 8f * Mathf.PI * 2;
            fx.Pillar(pos + new Vector3(Mathf.Cos(a) * 3.6f, 0, Mathf.Sin(a) * 3.6f), i * .05f);
          }
          fx.Pillar(pos, 0);
          fx.Spiral(pos, Fx.Fire);
          break;
        case FxKind.WindUlt:
          fx.Spiral(pos, Fx.WindLight);
          for (int i = 0; i < 5; i++) { int k = i; StartCoroutine(DelayCo(i * .17f, () => { fx.RingFx(pos, k % 2 == 0 ? Fx.WindLight : Fx.Wind, 7.2f); CameraRig.Shake(.15f); })); }
          break;
      }
    }

    void OnMaskChanged() => _vis?.ApplyMask(Mask);

    void OnDownedChanged() => _vis?.SetDowned(Downed);

    public override void Render() {
      if (_vis == null) return;
      float dt = Time.deltaTime;
      var pos = transform.position;
      float speed = Fx.Flat(pos, _lastPos) / Mathf.Max(dt, .0001f);
      _lastPos = pos;
      if (Downed) return;
      _bob = speed > .5f ? _bob + dt * 12 : 0;
      _vis.Move(speed, _bob);
      bool blink = HasStateAuthority && _inv > 0 && _inv < .45f && _dashT <= 0 && _leapT < 0 && Mathf.FloorToInt(Time.time * 20) % 2 == 0;
      _vis.SetVisible(!blink);
    }
  }
}
