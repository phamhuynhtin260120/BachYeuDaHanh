using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

namespace BachYeu {
  /// <summary>
  /// Runs one night: phases, spawning, lanterns, bosses, blessings, win and loss.
  /// Owned by the master client (shared mode); everyone else reads its networked state.
  /// </summary>
  public class GameDirector : NetworkBehaviour {
    public static GameDirector Instance { get; private set; }

    public NetworkObject EnemyPrefab;

    [Networked] public int Phase { get; set; }
    [Networked] public RunState State { get; set; }
    [Networked] public int Night { get; set; }
    [Networked] public int Kills { get; set; }
    [Networked] public int PhaseKills { get; set; }
    [Networked] public NetworkBool EliteSpawned { get; set; }
    [Networked] public NetworkId BossId { get; set; }
    [Networked] public TickTimer StateTimer { get; set; }
    [Networked] public TickTimer SpawnTimer { get; set; }
    [Networked] public int ReadyCount { get; set; }
    [Networked] public int RunStartTick { get; set; }
    [Networked] public int RunEndTick { get; set; }
    [Networked] public NetworkBool Started { get; set; }
    [Networked, Capacity(3)] public NetworkArray<float> LanternProgress => default;

    public int PlayerCount => Runner ? Mathf.Max(1, Runner.ActivePlayers.Count()) : 1;
    public float RunSeconds => Runner ? ((State == RunState.Won || State == RunState.Lost ? RunEndTick : Runner.Tick.Raw) - RunStartTick) * Runner.DeltaTime : 0;
    public int LitCount { get { int n = 0; for (int i = 0; i < 3; i++) if (LanternProgress[i] >= 1f) n++; return n; } }

    public Enemy Boss => Runner && BossId.IsValid && Runner.TryFindObject(BossId, out var o) ? o.GetComponent<Enemy>() : null;

    readonly List<LanternView> _lanterns = new();

    public override void Spawned() {
      Instance = this;
      for (int i = 0; i < 3; i++) _lanterns.Add(new LanternView(Phases.Lanterns[i], transform));
      if (HasStateAuthority && !Started) {
        Started = true;
        Night = 1;
        StartRun();
      }
    }

    public override void Despawned(NetworkRunner runner, bool hasState) {
      if (Instance == this) Instance = null;
    }

    // ---------------- run flow (state authority) ----------------
    void StartRun() {
      Kills = 0;
      RunStartTick = Runner.Tick.Raw;
      StartPhase(0);
    }

    void StartPhase(int i) {
      DespawnAllEnemies();
      Phase = i;
      State = RunState.Playing;
      PhaseKills = 0;
      EliteSpawned = false;
      ReadyCount = 0;
      for (int k = 0; k < 3; k++) LanternProgress.Set(k, 0f);
      BossId = default;
      SpawnTimer = TickTimer.CreateFromSeconds(Runner, 1.5f);
      if (i == 2) BossId = SpawnEnemy(EnemyType.Cat, new Vector3(0, 0, 7))?.Object.Id ?? default;
      if (i == 3) BossId = SpawnEnemy(EnemyType.King, new Vector3(0, 0, 7))?.Object.Id ?? default;
      RPC_PhaseStarted(i, i == 0);
    }

    void ClearPhase() {
      if (State != RunState.Playing) return;
      State = RunState.Cleared;
      DespawnAllEnemies();
      StateTimer = TickTimer.CreateFromSeconds(Runner, 1.8f);
      RPC_Banner((Phase + 1).ToString(), Phases.Short[Phase] + " đã qua", "");
    }

    void EndRun(bool won) {
      State = won ? RunState.Won : RunState.Lost;
      RunEndTick = Runner.Tick.Raw;
      DespawnAllEnemies();
    }

    void DespawnAllEnemies() {
      foreach (var e in Enemy.All.ToArray())
        if (e.Object && e.Object.IsValid && e.HasStateAuthority) Runner.Despawn(e.Object);
    }

    public Enemy SpawnEnemy(EnemyType type, Vector3 pos) {
      if (!HasStateAuthority || EnemyPrefab == null) return null;
      var flat = new Vector2(pos.x, pos.z);
      if (flat.magnitude > 11f) { flat = flat.normalized * 11f; pos = new Vector3(flat.x, 0, flat.y); }
      pos.y = 0;
      int hp = Mathf.RoundToInt(EnemyStats.Hp(type) * (1f + .6f * (PlayerCount - 1)));
      var obj = Runner.Spawn(EnemyPrefab, pos, Quaternion.identity, null, (runner, o) => {
        var e = o.GetComponent<Enemy>();
        e.Type = type;
        e.MaxHp = hp;
        e.Hp = hp;
      }, NetworkSpawnFlags.SharedModeStateAuthMasterClient);
      return obj ? obj.GetComponent<Enemy>() : null;
    }

    Vector3 EdgePosition() {
      for (int i = 0; i < 12; i++) {
        float a = Random.Range(0f, Mathf.PI * 2), r = Random.Range(8f, 11.5f);
        var p = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
        bool clear = true;
        foreach (var pl in PlayerAvatar.All) if (Fx.Flat(pl.transform.position, p) < 6.5f) clear = false;
        if (clear) return p;
      }
      return new Vector3(0, 0, 10);
    }

    public void OnEnemyKilled(Enemy e) {
      if (!HasStateAuthority) return;
      Kills++;
      if (State != RunState.Playing) return;
      if (Phase == 1) PhaseKills++;
      if (e.IsBoss) { Fx.Hitstop(.25f); ClearPhase(); }
    }

    public override void FixedUpdateNetwork() {
      if (!HasStateAuthority) return;
      float dt = Runner.DeltaTime;
      switch (State) {
        case RunState.Playing:
          TickPhase(dt);
          if (PlayerAvatar.All.Count > 0 && PlayerAvatar.All.All(p => p.Downed)) EndRun(false);
          break;
        case RunState.Cleared:
          if (StateTimer.Expired(Runner)) {
            if (Phase < 3) { State = RunState.Blessing; ReadyCount = 0; StateTimer = TickTimer.CreateFromSeconds(Runner, 20f); }
            else EndRun(true);
          }
          break;
        case RunState.Blessing:
          if (StateTimer.Expired(Runner) || ReadyCount >= PlayerCount) StartPhase(Phase + 1);
          break;
      }
    }

    void TickPhase(float dt) {
      int alive = Enemy.All.Count(e => e.Hp > 0);
      if (Phase == 0) {
        if (SpawnTimer.ExpiredOrNotRunning(Runner) && alive < 4 + 2 * PlayerCount) {
          SpawnEnemy(Random.value < .6f ? EnemyType.Ghost : EnemyType.Umbrella, EdgePosition());
          SpawnTimer = TickTimer.CreateFromSeconds(Runner, 2f / Mathf.Sqrt(PlayerCount));
        }
        for (int i = 0; i < 3; i++) {
          float prog = LanternProgress[i];
          if (prog >= 1f) continue;
          int near = PlayerAvatar.All.Count(p => !p.Downed && Fx.Flat(p.transform.position, Phases.Lanterns[i]) < 1.9f);
          prog = near > 0 ? prog + dt * near / 2.2f : Mathf.Max(0, prog - dt * .25f);
          if (prog >= 1f) { prog = 1f; RPC_LanternLit(i); }
          LanternProgress.Set(i, prog);
        }
        if (LitCount == 3) ClearPhase();
      } else if (Phase == 1) {
        if (SpawnTimer.ExpiredOrNotRunning(Runner) && alive < 5 + 2 * PlayerCount) {
          float r = Random.value;
          SpawnEnemy(r < .4f ? EnemyType.Ghost : r < .75f ? EnemyType.Umbrella : EnemyType.Heavy, EdgePosition());
          SpawnTimer = TickTimer.CreateFromSeconds(Runner, 1.4f / Mathf.Sqrt(PlayerCount));
        }
        if (PhaseKills >= 8 && !EliteSpawned) {
          EliteSpawned = true;
          SpawnEnemy(EnemyType.Elite, EdgePosition());
          RPC_Banner("", "Ô yêu đầu đàn xuất hiện", "");
        }
        if (PhaseKills >= Phases.Phase2Kills) ClearPhase();
      }
    }

    // ---------------- requests from clients ----------------
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Ready() {
      if (State == RunState.Blessing) ReadyCount++;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestRestart() {
      if (State != RunState.Won && State != RunState.Lost) return;
      Night++;
      RPC_ResetPlayers();
      StartRun();
    }

    // ---------------- broadcasts ----------------
    /// <summary>Show a ground warning on every client. Each client checks its own player when it fires.</summary>
    public void Telegraph(Vector3 pos, float radius, float duration, int damage) => RPC_Telegraph(pos, radius, duration, damage);

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_Telegraph(Vector3 pos, float radius, float duration, int damage) {
      if (!Fx.I) return;
      Fx.I.TeleCircle(pos, radius, duration, () => {
        var me = PlayerAvatar.Local;
        if (me && Fx.Flat(me.transform.position, pos) < radius + .15f && me.Grounded) me.ApplyHurt(damage);
        Fx.I.Burst(pos + Vector3.up * .4f, Fx.Ink, radius > 3 ? 24 : 14, 3.5f);
        if (radius > 3) { Fx.I.RingFx(pos, Fx.Ink, radius + .2f); CameraRig.Shake(.3f); }
      });
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_TeleLine(Vector3 pos, Vector3 dir, float len, float width, float duration) {
      if (Fx.I) Fx.I.TeleLine(pos, dir, len, width, duration);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_Banner(string big, string title, string sub) {
      if (Hud.I) Hud.I.Banner(big, title, sub);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_PhaseStarted(int phase, NetworkBool newRun) {
      if (Hud.I) Hud.I.Banner((phase + 1).ToString(), Phases.Title[phase], Phases.Sub[phase]);
      if (Fx.I) Fx.I.ClearHeals();
      if (PlayerAvatar.Local) PlayerAvatar.Local.OnPhaseStarted(phase);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_ResetPlayers() {
      if (PlayerAvatar.Local) PlayerAvatar.Local.ResetForNewRun();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_LanternLit(int i) {
      if (Fx.I) Fx.I.Burst(Phases.Lanterns[i] + Vector3.up * 2, Fx.Gold, 30, 4);
      if (PlayerAvatar.Local) PlayerAvatar.Local.AddUlt(15);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_HitFx(NetworkId enemy, Vector3 pos, int dmg, NetworkBool big, PlayerRef from, NetworkBool killed, NetworkBool drop) {
      bool boss = false;
      if (Runner.TryFindObject(enemy, out var o) && o.TryGetComponent<Enemy>(out var e)) { e.PlayHitFlash(); boss = e.IsBoss; }
      if (Hud.I) Hud.I.DamageNumber(pos + Vector3.up * (boss ? 4.2f : 2.3f), dmg, big, from == Runner.LocalPlayer);
      if (drop && Fx.I) Fx.I.HealOrb(pos);
      if (killed && from == Runner.LocalPlayer && PlayerAvatar.Local) PlayerAvatar.Local.OnKillCredit();
    }

    // ---------------- lantern visuals (all clients) ----------------
    public override void Render() {
      bool show = Phase == 0 && State == RunState.Playing;
      for (int i = 0; i < _lanterns.Count; i++) _lanterns[i].Update(show, LanternProgress[i]);
    }

    class LanternView {
      readonly GameObject _root, _mark, _ring, _fill;
      readonly Material _body;
      bool _lit;

      public LanternView(Vector3 pos, Transform parent) {
        _root = new GameObject("Lantern");
        _root.transform.SetParent(parent, false);
        _root.transform.position = pos;
        var t = _root.transform;
        Gfx.Prim(PrimitiveType.Cylinder, t, new Vector3(0, .9f, 0), new Vector3(.14f, .9f, .14f), Gfx.Lit(Gfx.Hex("#4a3426")));
        _body = Gfx.NewLit(Gfx.Hex("#7a5a50"));
        _body.EnableKeyword("_EMISSION");
        _body.SetColor("_EmissionColor", Color.black);
        Gfx.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 2, 0), new Vector3(.68f, .3f, .68f), _body);
        _mark = Gfx.Prim(PrimitiveType.Cube, t, new Vector3(0, 3, 0), Vector3.one * .3f, Gfx.Unlit(Gfx.Hex("#d8a83a")));
        _mark.transform.rotation = Quaternion.Euler(45, 0, 45);
        _ring = Gfx.MeshObj(Gfx.Ring, t, new Vector3(0, .05f, 0), Vector3.one * 1.9f, Gfx.Fade);
        _fill = Gfx.MeshObj(Gfx.Disc, t, new Vector3(0, .04f, 0), Vector3.one * .01f, Gfx.Fade);
        Gfx.SetTint(_fill.GetComponent<Renderer>(), new Color(.85f, .66f, .23f, .35f));
      }

      public void Update(bool show, float progress) {
        _root.SetActive(show);
        if (!show) return;
        bool lit = progress >= 1f;
        if (lit != _lit) {
          _lit = lit;
          _body.SetColor("_BaseColor", lit ? Gfx.Hex("#e0503a") : Gfx.Hex("#7a5a50"));
          _body.SetColor("_EmissionColor", lit ? Gfx.Hex("#ff9a3d") * .9f : Color.black);
        }
        _mark.SetActive(!lit);
        _ring.SetActive(!lit);
        _fill.SetActive(!lit);
        if (lit) return;
        float t = Time.time;
        _mark.transform.localPosition = new Vector3(0, 3 + Mathf.Sin(t * 3) * .15f, 0);
        _mark.transform.Rotate(0, 120 * Time.deltaTime, 0, Space.World);
        Gfx.SetTint(_ring.GetComponent<Renderer>(), new Color(.85f, .66f, .23f, .45f + Mathf.Sin(t * 4) * .2f));
        _fill.transform.localScale = Vector3.one * Mathf.Max(.01f, progress * 1.9f);
      }
    }
  }
}
