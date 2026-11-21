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
    /// <summary>Which of the two carried masks is on: 0 = MaskA, 1 = MaskB.</summary>
    [Networked, OnChangedRender(nameof(OnMaskChanged))] public int Mask { get; set; }
    /// <summary>The two masks this player carries (ids into Masks.All), picked before the run.</summary>
    [Networked, OnChangedRender(nameof(OnMaskChanged))] public int MaskA { get; set; }
    [Networked, OnChangedRender(nameof(OnMaskChanged))] public int MaskB { get; set; }
    /// <summary>Id of the mask being worn right now.</summary>
    public int MaskId => Mask == 0 ? MaskA : MaskB;
    public MaskDef MaskDef => Masks.All[Mathf.Clamp(MaskId, 0, Masks.All.Length - 1)];
    /// <summary>Damage absorbed before HP (Hà Bá shields). Owner only.</summary>
    public float Shield { get; private set; }
    [Networked, OnChangedRender(nameof(OnDownedChanged))] public NetworkBool Downed { get; set; }
    [Networked] public float ReviveProgress { get; set; }
    [Networked] public NetworkBool Ready { get; set; }
    [Networked] public NetworkString<_16> Nick { get; set; }
    /// <summary>Tranh Đăng team: 0 = Đội Lam, 1 = Đội Chu.</summary>
    [Networked] public int Team { get; set; }
    // Tranh Đăng match stats, written by the owner, shown on the result screen.
    [Networked] public int Pushes { get; set; }
    [Networked] public int KillCount { get; set; }
    [Networked] public int BossDamage { get; set; }
    [Networked] public float HoldTime { get; set; }
    // Result board: damage dealt to yokai, teammates picked up, times fallen (this match).
    [Networked] public int DamageDealt { get; set; }
    [Networked] public int Revives { get; set; }
    [Networked] public int Downs { get; set; }
    /// <summary>Danh hiệu from Thành tựu, shown beside the name.</summary>
    [Networked] public NetworkString<_32> Title { get; set; }

    public static bool InContest => GameDirector.Instance && GameDirector.Instance.Mode == Modes.Contest;
    public Color TeamColor => Contest.TeamColor[Mathf.Clamp(Team, 0, 1)];
    /// <summary>The player who controls this avatar. Solo mode has no state authority player, so use the local one.</summary>
    public PlayerRef Owner => Object.StateAuthority == PlayerRef.None ? Runner.LocalPlayer : Object.StateAuthority;

    public Blessings B { get; private set; } = new();
    public float Ult { get; private set; }
    public float ResonanceTime { get; private set; }
    public float SwapCooldown => _swapCd;
    public float DashCooldown => _dashCd;
    public float SkillCooldown(int skill) => skill == 1 ? _cd[Mask, 0] : _cd[Mask, 1];
    public bool Grounded => _leapT < 0 && !Downed;
    public void SetTitle(string t) { if (HasStateAuthority) Title = string.IsNullOrEmpty(t) ? "" : t.Length > 32 ? t.Substring(0, 32) : t; }

    /// <summary>Name plus the worn danh hiệu as a small grey tag (rich text).</summary>
    public string NameWithTitle {
      get { var t = Title.ToString(); return string.IsNullOrEmpty(t) ? DisplayName : $"{DisplayName} <size=15><color=#8a6a2e>« {t} »</color></size>"; }
    }

    public string DisplayName {
      get {
        var n = Nick.ToString();
        return string.IsNullOrWhiteSpace(n) ? "Người chơi " + Object.StateAuthority.PlayerId : n;
      }
    }

    const float Speed = 5.2f;
    readonly float[,] _cd = new float[2, 2];
    float _swapCd, _dashCd, _atkCd, _inv, _comboT, _dashT, _leapT = -1;
    int _combo;
    float _yaw = 0;
    Vector3 _dashDir, _leapFrom, _leapTo, _moveDir, _knock;
    float _downT;
    float _guardT, _shieldT;
    /// <summary>Quỷ Đá Gồng is up: less damage, can't be shoved.</summary>
    public bool Guarding => _guardT > 0;

    PlayerVisual _vis;
    Vector3 _lastPos;
    float _bob;

    /// <summary>The player a yokai goes after. Under Lời nguyền Hồng Đăng, players of the cursed team count as closer.</summary>
    public static PlayerAvatar NearestAlive(Vector3 pos) {
      PlayerAvatar best = null;
      float bd = float.MaxValue;
      var dir = GameDirector.Instance;
      int cursed = dir && dir.IsContest ? dir.CursedTeam : -1;
      foreach (var p in All) {
        if (p.Downed) continue;
        float d = Fx.Flat(p.transform.position, pos);
        if (cursed >= 0 && p.Team == cursed) d *= Contest.CurseTargetWeight;
        if (d < bd) { bd = d; best = p; }
      }
      return best;
    }

    public override void Spawned() {
      All.Add(this);
      if (HasStateAuthority) {
        LocalProfile.LoadMasks();
        MaskA = LocalProfile.MaskA;
        MaskB = LocalProfile.MaskB;
      }
      _vis = new PlayerVisual(transform);
      _vis.ApplyMask(MaskId);
      _lastPos = transform.position;
      if (HasStateAuthority) {
        Local = this;
        MaxHp = 100;
        Gear.ApplyTo(this);
        Hp = MaxHp;
        var name = (LocalProfile.Name ?? "").Trim();
        Nick = name.Length > 16 ? name.Substring(0, 16) : name;
        SetTitle(Profile.Data.Title);
        Team = Runner.LocalPlayer.PlayerId % 2;
        CameraRig.SetTarget(transform);
        GameInput.ClearAll();
        _mover = GroundMover.Attach(gameObject, .4f, 1.6f);
      }
      OnDownedChanged();
    }

    public override void Despawned(NetworkRunner runner, bool hasState) {
      All.Remove(this);
      if (Local == this) Local = null;
    }

    float _pingCd;

    void Update() {
      if (!(Object && Object.IsValid && HasStateAuthority)) return;
      GameInput.PollKeyboard();
      _pingCd -= Time.deltaTime;
      int ping = GameInput.ConsumePing();
      if (ping >= 0 && _pingCd <= 0) {
        _pingCd = 1f;
        RPC_Ping(ping, transform.position);
      }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_Ping(int kind, Vector3 pos) {
      kind = Mathf.Clamp(kind, 0, Pings.Text.Length - 1);
      if (Hud.I) Hud.I.ShowPing(pos + Vector3.up * 2.8f, DisplayName + ": " + Pings.Text[kind], Pings.Color[kind]);
      if (!ArtSet.Play(GameFx.Ping, pos) && Fx.I) Fx.I.RingFx(pos, Pings.Color[kind], 2.2f, .8f);
    }

    /// <summary>Local player: toggled from the lobby panel.</summary>
    public void SetReady(bool ready) { if (HasStateAuthority) Ready = ready; }

    /// <summary>Local player: change the two carried masks (menu or lobby only, not mid-run).</summary>
    public void SetMasks(int a, int b) {
      if (!HasStateAuthority || !Masks.Valid(a, b)) return;
      var d = GameDirector.Instance;
      if (d && d.State != RunState.Lobby) return;
      MaskA = a; MaskB = b; Mask = 0;
    }

    /// <summary>Local player: switch between Đội Lam and Đội Chu in the lobby.</summary>
    public void SetTeam(int team) {
      if (!HasStateAuthority) return;
      Team = Mathf.Clamp(team, 0, 1);
      if (TryGetComponent<NetworkTransform>(out var nt)) nt.Teleport(OnGround(Contest.SpawnPoint(Team, Runner.LocalPlayer.PlayerId / 2)), Quaternion.identity);
    }

    /// <summary>Local player: called when the room goes (back) to the lobby.</summary>
    public void OnLobbyEntered() {
      if (!HasStateAuthority) return;
      ResetForNewRun();
      Ready = false;
      OnPhaseStarted(0);
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
      _guardT -= dt;
      _shieldT -= dt;
      if (_shieldT <= 0) Shield = 0;

      if (Downed) { TickRevive(dt); GameInput.ClearAll(); return; }
      _downT = 0;

      var mv = GameInput.Move;
      _moveDir = new Vector3(mv.x, 0, mv.y);
      var startPos = transform.position;
      var pos = startPos;
      bool placed = false; // leap frames set the position directly instead of walking

      if (_leapT >= 0) {
        _leapT += dt;
        float k = Mathf.Min(1, _leapT / .38f);
        pos = Vector3.Lerp(_leapFrom, _leapTo, k);
        pos.y += Mathf.Sin(k * Mathf.PI) * 2.2f;
        placed = true;
        if (k >= 1) { pos = _leapTo; _leapT = -1; _mover?.Place(pos); LeapLand(); }
      } else if (_dashT > 0) {
        _dashT -= dt;
        pos += _dashDir * 19f * dt;
      } else if (_moveDir.sqrMagnitude > .02f) {
        pos += _moveDir * Speed * dt;
        if (_atkCd <= 0) _yaw = Mathf.Atan2(_moveDir.x, _moveDir.z) * Mathf.Rad2Deg;
      }
      // Shoves from the other team (Tranh Đăng). No damage, only position.
      if (_knock.sqrMagnitude > .01f) {
        if (_leapT < 0) pos += _knock * dt;
        _knock *= Mathf.Max(0, 1 - dt * 5f);
      }

      var flat = new Vector2(pos.x, pos.z);
      if (flat.magnitude > ArenaBuilder.PlayRadius) { flat = flat.normalized * ArenaBuilder.PlayRadius; pos.x = flat.x; pos.z = flat.y; }
      // Walk over the ground: slopes, steps, walls and gravity come from the environment's colliders.
      if (!placed && _mover) pos = _mover.Move(pos - startPos, dt);
      else if (!_mover && _leapT < 0) pos.y = 0;
      transform.SetPositionAndRotation(pos, Quaternion.Euler(0, _yaw, 0));

      if (!playing) { GameInput.ClearAll(); return; }
      if (director.Mode == Modes.Contest)
        for (int i = 0; i < 3; i++)
          if (Fx.Flat(pos, Contest.Lanterns[i]) < Contest.LanternRadius) { HoldTime += dt; break; }
      if (GameInput.AttackHeld) Attack();
      if (GameInput.Consume(GameAction.Skill1)) Skill(0);
      if (GameInput.Consume(GameAction.Skill2)) Skill(1);
      if (GameInput.Consume(GameAction.Swap)) Swap();
      if (GameInput.Consume(GameAction.Dash)) Dash();
      if (GameInput.Consume(GameAction.Ult)) UltCast();
    }

    void TickRevive(float dt) {
      bool contest = InContest;
      bool helped = false;
      foreach (var p in All)
        if (p != this && !p.Downed && (!contest || p.Team == Team) && Fx.Flat(p.transform.position, transform.position) < 2.2f) helped = true;
      ReviveProgress = helped ? ReviveProgress + dt / 3f : Mathf.Max(0, ReviveProgress - dt * .5f);
      if (ReviveProgress >= 1f) {
        Downed = false;
        ReviveProgress = 0;
        foreach (var p in All) // credit whoever stood beside us
          if (p != this && !p.Downed && (!contest || p.Team == Team) && Fx.Flat(p.transform.position, transform.position) < 2.2f) p.RPC_CreditRevive();
        Hp = Mathf.RoundToInt(MaxHp * .4f);
        _inv = 1.5f;
        return;
      }
      // Tranh Đăng: nobody loses the match by falling; you get up at your own bank after a while.
      _downT += dt;
      if (contest && _downT >= Contest.AutoReviveSeconds) {
        Downed = false;
        ReviveProgress = 0;
        Hp = Mathf.RoundToInt(MaxHp * .5f);
        _inv = 2f;
        _knock = Vector3.zero;
        var start = OnGround(Contest.SpawnPoint(Team, Runner.LocalPlayer.PlayerId / 2));
        if (TryGetComponent<NetworkTransform>(out var nt)) nt.Teleport(start, Quaternion.identity);
        else transform.position = start;
      }
    }

    // ---------------- Tranh Đăng shoves ----------------
    /// <summary>Sent to the owner of a rival avatar: push it without damage.</summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Knock(Vector3 velocity) {
      if (Downed || _dashT > 0 || _leapT >= 0 || _guardT > 0) return;
      _knock = velocity;
      _vis?.Trigger("Hit");
    }

    /// <summary>Push players of the other team inside a radius (optionally only in front). Returns how many were pushed.</summary>
    int PushRivals(Vector3 center, float radius, float strength, float halfAngleRad = Mathf.PI) {
      if (!InContest) return 0;
      int n = 0;
      var f = Forward;
      foreach (var p in All) {
        if (p == this || p.Team == Team || p.Downed) continue;
        var d = Flat(p.transform.position - center);
        float l = d.magnitude;
        if (l > radius) continue;
        if (halfAngleRad < Mathf.PI && l > .7f && Vector3.Dot(d / l, f) < Mathf.Cos(halfAngleRad)) continue;
        var dir = l > .05f ? d / l : f;
        p.RPC_Knock(dir * strength);
        n++;
      }
      Pushes += n;
      return n;
    }

    // ---------------- helpers ----------------
    GroundMover _mover;
    static Vector3 OnGround(Vector3 p) { p.y = GroundMover.GroundY(p); return p; }

    Vector3 Forward => Quaternion.Euler(0, _yaw, 0) * Vector3.forward;

    /// <summary>Turn to face a direction (local player only), e.g. toward the camera of the end-of-night film.</summary>
    public void Face(float yaw) { if (HasStateAuthority) _yaw = yaw; }
    Vector3 Flat(Vector3 v) { v.y = 0; return v; }

    void AutoFace() {
      var n = Enemy.Nearest(transform.position, 7f);
      if (n == null) return;
      var d = Flat(n.transform.position - transform.position);
      if (d.sqrMagnitude > .001f) _yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    float DamageMul => (ResonanceTime > 0 ? 1f + B.Res : 1f) * B.DmgMul;

    void Hit(Enemy e, int baseDmg, float kb = 0, bool big = false, float stop = 0) {
      if (e == null || !e.Alive) return;
      int dmg = Mathf.RoundToInt(baseDmg * DamageMul * Random.Range(.92f, 1.08f));
      var dir = Flat(e.transform.position - transform.position);
      var kbv = dir.sqrMagnitude > .001f ? dir.normalized * kb * 7f : Vector3.zero;
      // Read before the RPC: on the host it runs at once and may despawn the yokai.
      bool boss = e.IsBoss;
      int hpBefore = e.Hp;
      e.RPC_Damage(dmg, kbv, big, Runner.LocalPlayer);
      if (boss) BossDamage += Mathf.Min(dmg, Mathf.Max(0, hpBefore));
      DamageDealt += Mathf.Min(dmg, Mathf.Max(0, hpBefore));
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
      _atkCd = MaskDef.AtkCd;
      switch (MaskId) {
        case Masks.Fox: {
          if (_comboT <= 0) _combo = 0;
          _comboT = .9f;
          int i = _combo % 3; _combo++;
          bool fin = i == 2;
          HitArc(2.5f, 1.3f, fin ? 48 : 28, fin ? 1f : .25f, fin ? .05f : 0);
          if (fin) PushRivals(transform.position, 2.6f, 9f, 1.3f);
          _atkCd *= fin ? 1.6f : 1f;
          RPC_Fx(fin ? FxKind.FoxSlashBig : FxKind.FoxSlash, transform.position, _yaw, Vector3.zero);
          if (B.FoxAuto > 0 && Random.value < B.FoxAuto) FoxOrb(Enemy.Nearest(transform.position, 10));
          break;
        }
        case Masks.Tengu:
          HitArc(3f, 1.9f, 38, 1.1f, .03f);
          PushRivals(transform.position, 3.1f, 10f, 1.9f);
          RPC_Fx(FxKind.WindSlash, transform.position, _yaw, Vector3.zero);
          if (B.WindBlade) Fx.I.WindBlade(transform.position, Forward, e => Hit(e, 30, .4f));
          break;
        case Masks.HaBa:
          HitArc(2.9f, 1.1f, 30, .3f, 0);
          RPC_Fx(FxKind.WaterWhip, transform.position, _yaw, Vector3.zero);
          break;
        case Masks.QuyDa:
          HitArc(2.4f, 1.2f, 54, 1.4f, .06f);
          PushRivals(transform.position, 2.5f, 11f, 1.2f);
          RPC_Fx(FxKind.StonePunch, transform.position, _yaw, Vector3.zero);
          break;
      }
    }

    // ---------------- support (Hà Bá) ----------------
    /// <summary>Teammates near a point, including yourself. In Tranh Đăng only your own team.</summary>
    List<PlayerAvatar> AlliesNear(Vector3 center, float radius) {
      var list = new List<PlayerAvatar>();
      bool contest = InContest;
      foreach (var p in All)
        if (!p.Downed && (!contest || p.Team == Team) && Fx.Flat(p.transform.position, center) < radius) list.Add(p);
      return list;
    }

    void SupportAllies(Vector3 center, float radius, int heal, int shield) {
      foreach (var p in AlliesNear(center, radius)) {
        if (p == this) ReceiveSupport(heal, shield);
        else p.RPC_Support(heal, shield);
      }
    }

    /// <summary>Sent to an ally's owner: heal and/or a damage shield.</summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Support(int heal, int shield) => ReceiveSupport(heal, shield);

    void ReceiveSupport(int heal, int shield) {
      if (!HasStateAuthority || Downed) return;
      if (heal > 0) Heal(heal);
      if (shield > 0) { Shield = Mathf.Max(Shield, shield); _shieldT = 5f; }
    }

    // ---------------- stone wall (Quỷ Đá) ----------------
    /// <summary>Every machine builds the same wall with a collider, so it blocks players and yokai everywhere.</summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_StoneWall(Vector3 pos, float yaw) {
      var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
      go.name = "StoneWall";
      go.transform.SetPositionAndRotation(pos + Vector3.up * .8f, Quaternion.Euler(0, yaw, 0));
      go.transform.localScale = new Vector3(3.4f, 1.6f, .7f);
      go.GetComponent<Renderer>().sharedMaterial = Gfx.Lit(Gfx.Hex("#7d6a56"));
      Physics.SyncTransforms();
      if (!ArtSet.Play(GameFx.StoneWallRise, pos, yaw) && Fx.I) { Fx.I.Burst(pos + Vector3.up * .4f, Gfx.Hex("#8a6a4e"), 20, 4); Fx.I.RingFx(pos, Gfx.Hex("#8a6a4e"), 2.2f); }
      CameraRig.Shake(.15f);
      Destroy(go, 6f);
    }

    void FoxOrb(Enemy target) {
      Fx.I.FoxOrb(transform.position, Forward, target, e => Hit(e, 62, 0, false, .02f));
    }

    void Skill(int s) {
      if (_leapT >= 0) return;
      var def = MaskDef;
      if (_cd[Mask, s] > 0) return;
      _cd[Mask, s] = (s == 0 ? def.S1Cd : def.S2Cd) * B.CdMul;
      int id = MaskId;
      if (id == Masks.HaBa) {
        var c = transform.position;
        if (s == 0) { // Suối: heal around you
          SupportAllies(c, 5f, 22, 0);
          HitRadius(c, 3f, 35, .4f);
          RPC_Fx(FxKind.WaterSpring, c, _yaw, Vector3.zero);
        } else {      // Thủy thuẫn: shield around you
          SupportAllies(c, 5f, 0, 35);
          RPC_Fx(FxKind.WaterShield, c, _yaw, Vector3.zero);
        }
        return;
      }
      if (id == Masks.QuyDa) {
        if (s == 0) { // Gồng: damage reduction, can't be shoved
          _guardT = 4f;
          Heal(8);
          RPC_Fx(FxKind.StoneGuard, transform.position, _yaw, Vector3.zero);
        } else {      // Tường đá: a wall in front of you
          AutoFace();
          var wall = OnGround(transform.position + Forward * 2.4f);
          HitRadius(wall, 2.2f, 40, 1.6f, false, .04f);
          PushRivals(wall, 2.2f, 12f);
          RPC_StoneWall(wall, _yaw);
        }
        return;
      }
      if (id == Masks.Fox && s == 0) {
        AutoFace();
        var targets = new List<Enemy>();
        foreach (var e in Enemy.All) if (e.Alive && Fx.Flat(e.transform.position, transform.position) < 13) targets.Add(e);
        targets.Sort((a, b) => Fx.Flat(a.transform.position, transform.position).CompareTo(Fx.Flat(b.transform.position, transform.position)));
        for (int i = 0; i < 3; i++) FoxOrb(targets.Count > 0 ? targets[i % targets.Count] : null);
        RPC_Fx(FxKind.FoxOrbs, transform.position, _yaw, Vector3.zero);
      } else if (id == Masks.Fox) {
        Blink();
      } else if (s == 0) {
        AutoFace();
        var c = transform.position + Forward * 3.2f;
        var fl = new Vector2(c.x, c.z);
        if (fl.magnitude > 12) { fl = fl.normalized * 12; c = new Vector3(fl.x, 0, fl.y); }
        float shoveCd = 0;
        Fx.I.TornadoFx(c, center => HitRadius(center, 2.7f, 22), (center, dt) => {
          shoveCd -= dt;
          if (shoveCd <= 0 && PushRivals(center, 3.2f, 12f) > 0) shoveCd = .6f;
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
      transform.position = OnGround(b);
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
      to = OnGround(to);
      var fl = new Vector2(to.x, to.z);
      if (fl.magnitude > 12) { fl = fl.normalized * 12; to = new Vector3(fl.x, 0, fl.y); }
      _leapFrom = transform.position;
      _leapTo = to;
      _leapT = 0;
      _inv = Mathf.Max(_inv, .6f);
    }

    void LeapLand() {
      HitRadius(transform.position, 3.3f, 130, 1.6f, false, .07f);
      PushRivals(transform.position, 3.4f, 13f);
      RPC_Fx(FxKind.LeapLand, transform.position, _yaw, Vector3.zero);
    }

    void Swap() {
      if (_swapCd > 0 || _leapT >= 0) return;
      Mask = 1 - Mask;
      _swapCd = 2.5f * B.SwapMul;
      ResonanceTime = B.ResDur;
      var p = transform.position;
      switch (MaskId) { // what the incoming mask does as it lands
        case Masks.Fox: HitRadius(p, 3.2f, 65, .8f); RPC_Fx(FxKind.SwapToFox, p, _yaw, Vector3.zero); break;
        case Masks.Tengu: HitRadius(p, 3.6f, 55, 2f); PushRivals(p, 3.6f, 11f); RPC_Fx(FxKind.SwapToWind, p, _yaw, Vector3.zero); break;
        case Masks.HaBa: SupportAllies(p, 3.5f, 10, 0); HitRadius(p, 3f, 30, .5f); RPC_Fx(FxKind.SwapToWater, p, _yaw, Vector3.zero); break;
        case Masks.QuyDa: HitRadius(p, 3f, 60, 1.6f, false, .05f); PushRivals(p, 3f, 12f); RPC_Fx(FxKind.SwapToStone, p, _yaw, Vector3.zero); break;
      }
    }

    void Dash() {
      if (_dashCd > 0 || _leapT >= 0) return;
      _dashCd = 1.1f * B.DashMul;
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
      var c = transform.position;
      switch (MaskId) {
        case Masks.Fox:
          RPC_Fx(FxKind.FoxUlt, c, _yaw, Vector3.zero);
          for (int i = 0; i < 3; i++) { int k = i; DelayLocal(.15f + i * .22f, () => HitRadius(transform.position, 7.5f, 115, .5f, k == 2, k == 2 ? .08f : 0)); }
          break;
        case Masks.Tengu:
          RPC_Fx(FxKind.WindUlt, c, _yaw, Vector3.zero);
          for (int i = 0; i < 5; i++) { int k = i; DelayLocal(i * .17f, () => HitRadius(transform.position, 7f, 105, .7f, k == 4, k == 4 ? .08f : 0)); }
          break;
        case Masks.HaBa: // Hồng thủy: the whole team nearby gets healed and shielded, yokai get washed away
          RPC_Fx(FxKind.WaterUlt, c, _yaw, Vector3.zero);
          SupportAllies(c, 10f, 45, 40);
          for (int i = 0; i < 3; i++) { int k = i; DelayLocal(.1f + i * .25f, () => HitRadius(transform.position, 7f, 80, 1.4f, k == 2, k == 2 ? .06f : 0)); }
          break;
        case Masks.QuyDa: // Địa chấn: three stomps
          RPC_Fx(FxKind.StoneUlt, c, _yaw, Vector3.zero);
          for (int i = 0; i < 3; i++) { int k = i; DelayLocal(.12f + i * .3f, () => { HitRadius(transform.position, 6.5f, 110, 1.2f, k == 2, .07f); PushRivals(transform.position, 6.5f, 13f); }); }
          break;
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
      if (MaskId == Masks.QuyDa) dmg = Mathf.RoundToInt(dmg * .85f); // stone skin
      if (_guardT > 0) dmg = Mathf.RoundToInt(dmg * .4f);             // Gồng
      if (Shield > 0) {                                                // Thủy thuẫn
        float absorbed = Mathf.Min(Shield, dmg);
        Shield -= absorbed;
        dmg -= Mathf.RoundToInt(absorbed);
        if (dmg <= 0) { _inv = .3f; return; }
      }
      Hp -= dmg;
      _inv = .7f;
      CameraRig.Shake(.25f);
      if (Hud.I) Hud.I.HurtFlash();
      Sfx.Play(SoundId.Hurt);
      GameSettings.Buzz();
      _vis?.Trigger("Hit");
      if (Hp <= 0) { Hp = 0; Downed = true; ReviveProgress = 0; Downs++; }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_CreditRevive() => Revives++;

    public void Heal(int amount) {
      if (!HasStateAuthority || Downed) return;
      Hp = Mathf.Min(MaxHp, Hp + amount);
    }

    public void AddMaxHp(int amount) { MaxHp += amount; Hp = MaxHp; }
    public void AddUlt(float v) => Ult = Mathf.Min(100, Ult + v);

    public void OnKillCredit() {
      KillCount++;
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
      var start = OnGround(InContest ? Contest.SpawnPoint(Team, Runner.LocalPlayer.PlayerId / 2) : new Vector3(-1.5f + slot * 1f, 0, -5f));
      _knock = Vector3.zero; _downT = 0;
      _leapT = -1; _dashT = 0;
      _yaw = 0;
      // Pháp khí: Gương bát quái (shield) and Đèn kéo quân (ult) at the start of every phase.
      if (B.PhaseShield > 0) { Shield = Mathf.Max(Shield, B.PhaseShield); _shieldT = 15f; }
      if (B.PhaseUlt > 0) Ult = Mathf.Max(Ult, B.PhaseUlt);
      if (TryGetComponent<NetworkTransform>(out var nt)) nt.Teleport(start, Quaternion.identity);
      else transform.SetPositionAndRotation(start, Quaternion.identity);
    }

    public void ResetForNewRun() {
      if (!HasStateAuthority) return;
      B = new Blessings();
      MaxHp = 100;
      Gear.ApplyTo(this);
      Hp = MaxHp;
      Downed = false;
      Mask = 0;
      Ult = 0;
      ResonanceTime = 0;
      System.Array.Clear(_cd, 0, _cd.Length);
      _swapCd = _dashCd = _atkCd = 0;
      Pushes = KillCount = BossDamage = 0;
      DamageDealt = Revives = Downs = 0;
      HoldTime = 0;
      Shield = 0; _guardT = _shieldT = 0;
    }

    // ---------------- visuals (all clients) ----------------
    public enum FxKind : byte {
      FoxSlash, FoxSlashBig, WindSlash, FoxOrbs, Blink, Tornado, LeapLand, SwapToWind, SwapToFox, Dash, InkRing, FoxUlt, WindUlt,
      WaterWhip, WaterSpring, WaterShield, WaterUlt, SwapToWater, StonePunch, StoneGuard, StoneUlt, SwapToStone,
    }
    static readonly Color Water = Gfx.Hex("#2f8f8a"), WaterLight = Gfx.Hex("#bfe6e0"), Stone = Gfx.Hex("#8a6a4e"), StoneDust = Gfx.Hex("#cdbb9f");

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_Fx(FxKind kind, Vector3 pos, float yaw, Vector3 extra) {
      var fx = Fx.I;
      if (!fx) return;
      var fwd = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
      bool mine = HasStateAuthority;
      _vis?.Trigger(kind switch {
        FxKind.FoxSlash or FxKind.FoxSlashBig or FxKind.WindSlash or FxKind.WaterWhip or FxKind.StonePunch => "Attack",
        FxKind.SwapToWind or FxKind.SwapToFox or FxKind.SwapToWater or FxKind.SwapToStone => "Swap",
        FxKind.WaterUlt or FxKind.StoneUlt => "Ult",
        FxKind.Dash => "Dash",
        FxKind.FoxUlt or FxKind.WindUlt => "Ult",
        FxKind.InkRing => "",
        _ => "Skill",
      });
      // A prefab set in ArtSet > Player VFX replaces the built-in look. Fox orbs and the tornado also carry the
      // caster's hits, so their built-in objects always exist and the prefab plays on top
      // (the tornado's cone is hidden by Fx.TornadoFx when the row replaces it).
      var art = ArtSet.Current;
      var slot = art ? art.SpawnPlayerFx(kind, pos, yaw, transform) : null;
      var snd = Sfx.For(kind);
      if (snd.HasValue) Sfx.Play(snd.Value, pos, slot != null ? ArtSet.LastVfxClip : null);
      if (slot != null && kind == FxKind.Blink) art.SpawnPlayerFx(kind, extra, yaw, null); // vanish here, appear there
      if (slot != null && !slot.KeepBuiltIn && kind != FxKind.FoxOrbs && kind != FxKind.Tornado) return;
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
        // ---- Hà Bá ----
        case FxKind.WaterWhip: fx.Slash(pos, yaw, 2.7f, false, Water); fx.Burst(pos + fwd * 2f + Vector3.up * .6f, WaterLight, 6, 2.5f); break;
        case FxKind.WaterSpring:
          fx.RingFx(pos, Water, 5f, .7f); fx.RingFx(pos, WaterLight, 3f, .5f);
          fx.Burst(pos + Vector3.up * .5f, WaterLight, 26, 3.5f); fx.Burst(pos + Vector3.up * .5f, Fx.Jade, 10, 2f);
          break;
        case FxKind.WaterShield:
          fx.RingFx(pos, WaterLight, 5f, .8f);
          for (int i = 0; i < 3; i++) { int k = i; StartCoroutine(DelayCo(i * .12f, () => fx.RingFx(pos + Vector3.up * (.4f + k * .5f), Water, 1.4f, .5f))); }
          break;
        case FxKind.SwapToWater: fx.RingFx(pos, Water, 3.5f); fx.Burst(pos + Vector3.up, WaterLight, 20, 4); break;
        case FxKind.WaterUlt:
          fx.Spiral(pos, WaterLight);
          for (int i = 0; i < 3; i++) { int k = i; StartCoroutine(DelayCo(.1f + i * .25f, () => { fx.RingFx(pos, k % 2 == 0 ? Water : WaterLight, 7.4f, .6f); fx.Burst(pos + Vector3.up, WaterLight, 30, 6); CameraRig.Shake(.18f); })); }
          break;
        // ---- Quỷ Đá ----
        case FxKind.StonePunch: fx.Slash(pos, yaw, 2.2f, true, Stone); fx.Burst(pos + fwd * 1.6f + Vector3.up * .5f, StoneDust, 10, 3f); CameraRig.Shake(mine ? .12f : 0); break;
        case FxKind.StoneGuard:
          fx.RingFx(pos, Stone, 2f, .6f);
          for (int i = 0; i < 4; i++) { int k = i; StartCoroutine(DelayCo(k, () => { if (this) fx.RingFx(transform.position, StoneDust, 1.3f, .5f); })); }
          break;
        case FxKind.SwapToStone: fx.RingFx(pos, Stone, 3.2f); fx.Burst(pos + Vector3.up * .3f, StoneDust, 22, 4.5f); CameraRig.Shake(mine ? .2f : .05f); break;
        case FxKind.StoneUlt:
          for (int i = 0; i < 3; i++) { int k = i; StartCoroutine(DelayCo(.12f + i * .3f, () => { var at = this ? transform.position : pos; fx.RingFx(at, k == 2 ? Fx.Seal : Stone, 6.6f, .5f); fx.Burst(at + Vector3.up * .3f, StoneDust, 34, 6); CameraRig.Shake(.3f); })); }
          break;
      }
    }

    void OnMaskChanged() => _vis?.ApplyMask(MaskId);

    bool _wasDowned;
    void OnDownedChanged() {
      _vis?.SetDowned(Downed);
      if (Downed == _wasDowned) return;
      _wasDowned = Downed;
      ArtSet.Play(Downed ? GameFx.PlayerDowned : GameFx.PlayerRevived, transform.position);
    }

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
