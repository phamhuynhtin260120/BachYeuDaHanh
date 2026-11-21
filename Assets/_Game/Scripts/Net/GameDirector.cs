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
    [Networked, OnChangedRender(nameof(OnSeasonChanged))] public int Season { get; set; }
    [Networked, OnChangedRender(nameof(OnModeChanged))] public int Mode { get; set; }

    // ---- Tranh Đăng state ----
    /// <summary>Points by team and source: index team * 3 + (0 lanterns, 1 yokai, 2 boss).</summary>
    [Networked, Capacity(6)] public NetworkArray<int> ScoreParts => default;
    /// <summary>Lantern owner + 1: 0 = nobody, 1 = Đội Lam, 2 = Đội Chu. Capture state is in LanternProgress (-1 Lam .. +1 Chu).</summary>
    [Networked, Capacity(3)] public NetworkArray<int> LanternOwner => default;
    [Networked, Capacity(2)] public NetworkArray<int> TeamBossDamage => default;
    [Networked] public float FullHold { get; set; }
    [Networked] public int FullHoldTeam { get; set; }
    /// <summary>-1 = draw, 0 = Đội Lam, 1 = Đội Chu.</summary>
    [Networked] public int Winner { get; set; }
    [Networked] public NetworkBool WonByFullLight { get; set; }
    [Networked] public PlayerRef BossFinisher { get; set; }
    /// <summary>Lời nguyền Hồng Đăng: -1 none, else the team yokai and the boss hunt first.</summary>
    [Networked] public int CursedTeam { get; set; }

    public bool IsContest => Mode == Modes.Contest;
    public int Score(int team) => ScoreParts[team * 3] + ScoreParts[team * 3 + 1] + ScoreParts[team * 3 + 2];
    public int ScorePart(int team, int part) => ScoreParts[team * 3 + part];
    public float MatchLeft => Mathf.Max(0, Contest.MatchSeconds - RunSeconds);
    public int TeamSize(int team) => PlayerAvatar.All.Count(p => p.Team == team);
    /// <summary>Tranh Đăng needs someone on each side once more than one person is in the room.</summary>
    public bool TeamsOk => !IsContest || PlayerAvatar.All.Count <= 1 || (TeamSize(0) > 0 && TeamSize(1) > 0);
    public bool CanStart => AllGuestsReady && TeamsOk;
    readonly float[] _lanternAcc = new float[2];

    public bool IsSingle => Runner && Runner.GameMode == GameMode.Single;
    /// <summary>True on the machine that runs the night (solo player or shared-mode master client).</summary>
    public bool LocalIsHost => HasStateAuthority;
    /// <summary>The player who currently owns the director (the host).</summary>
    public PlayerRef HostPlayer => Object ? Object.StateAuthority : default;
    public bool IsHostAvatar(PlayerAvatar p) => p && p.Object && (IsSingle || p.Object.StateAuthority == HostPlayer);
    public int ReadyGuests => PlayerAvatar.All.Count(p => !IsHostAvatar(p) && p.Ready);
    public int Guests => PlayerAvatar.All.Count(p => !IsHostAvatar(p));
    public bool AllGuestsReady => ReadyGuests >= Guests;
    [Networked, Capacity(3)] public NetworkArray<float> LanternProgress => default;

    public int PlayerCount => Runner ? Mathf.Max(1, Runner.ActivePlayers.Count()) : 1;
    public float RunSeconds => Runner ? ((State == RunState.Won || State == RunState.Lost ? RunEndTick : Runner.Tick.Raw) - RunStartTick) * Runner.DeltaTime : 0;
    public int LitCount { get { int n = 0; for (int i = 0; i < 3; i++) if (LanternProgress[i] >= 1f) n++; return n; } }

    public Enemy Boss => Runner && BossId.IsValid && Runner.TryFindObject(BossId, out var o) ? o.GetComponent<Enemy>() : null;

    readonly List<LanternView> _lanterns = new();
    readonly List<LanternView> _contestLanterns = new();

    public override void Spawned() {
      Instance = this;
      for (int i = 0; i < 3; i++) _lanterns.Add(new LanternView(Phases.Lanterns[i], transform, 1.9f));
      for (int i = 0; i < 3; i++) _contestLanterns.Add(new LanternView(Contest.Lanterns[i], transform, Contest.LanternRadius));
      if (HasStateAuthority && !Started) {
        Started = true;
        Mode = Mathf.Clamp(LocalProfile.Mode, 0, 1);
        // Hành trình starts at the chapter the host picked on the map; its season comes with it.
        Night = Mode == Modes.Journey ? Mathf.Max(1, LocalProfile.Chapter) : 1;
        var ch = Mode == Modes.Journey ? Story.Get(Night) : null;
        Season = ch != null ? ch.Season : Mathf.Clamp(LocalProfile.Season, 0, 3);
        CursedTeam = -1;
        if (IsSingle) StartRun();
        else EnterLobby();
      }
      OnSeasonChanged();
      OnModeChanged();
    }

    void OnSeasonChanged() {
      if (ArenaBuilder.I) ArenaBuilder.I.ApplySeason(Season);
    }

    void OnModeChanged() {
      if (ArenaBuilder.I) ArenaBuilder.I.ApplyMode(Mode);
    }

    void SetSessionOpen(bool open) {
      if (Runner.GameMode == GameMode.Shared && Runner.SessionInfo.IsValid) Runner.SessionInfo.IsOpen = open;
    }

    void EnterLobby() {
      DespawnAllEnemies();
      State = RunState.Lobby;
      Phase = 0;
      BossId = default;
      SetSessionOpen(true);
      RPC_LobbyEntered();
    }

    public override void Despawned(NetworkRunner runner, bool hasState) {
      if (Instance == this) Instance = null;
    }

    // ---------------- run flow (state authority) ----------------
    void StartRun() {
      Kills = 0;
      RunStartTick = Runner.Tick.Raw;
      if (IsContest) StartMatch();
      else StartPhase(0);
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
      if (i <= 1 && Has(Twist.Elites)) SpawnEnemy(EnemyType.Elite, EdgePosition()); // Đầu đàn: one from the start
      if (i == 0) _nextRain = 0;
      RPC_PhaseStarted(i, i == 0);
    }

    void ClearPhase() {
      if (State != RunState.Playing) return;
      State = RunState.Cleared;
      DespawnAllEnemies();
      StateTimer = TickTimer.CreateFromSeconds(Runner, 1.8f);
      RPC_Banner((Phase + 1).ToString(), Phases.Short[Phase] + " đã qua", "");
    }

    // ---------------- Tranh Đăng (state authority) ----------------
    void StartMatch() {
      DespawnAllEnemies();
      Phase = 0;
      State = RunState.Playing;
      PhaseKills = 0;
      EliteSpawned = false;
      BossId = default;
      BossFinisher = PlayerRef.None;
      for (int k = 0; k < 6; k++) ScoreParts.Set(k, 0);
      for (int k = 0; k < 3; k++) { LanternProgress.Set(k, 0f); LanternOwner.Set(k, 0); }
      for (int k = 0; k < 2; k++) { TeamBossDamage.Set(k, 0); _lanternAcc[k] = 0; }
      FullHold = 0; FullHoldTeam = -1; Winner = -1; WonByFullLight = false;
      CursedTeam = -1;
      _nextInk = Contest.InkRainFirst;
      _nextWisp = Contest.WispFirst;
      SpawnTimer = TickTimer.CreateFromSeconds(Runner, 1.5f);
      RPC_ContestAct(0);
    }

    void AddPoints(int team, int part, int points) {
      if (team < 0 || team > 1 || points == 0) return;
      ScoreParts.Set(team * 3 + part, ScoreParts[team * 3 + part] + points);
    }

    int TeamOf(PlayerRef who) {
      foreach (var p in PlayerAvatar.All) if (p.Object && p.Owner == who) return Mathf.Clamp(p.Team, 0, 1);
      return -1;
    }

    void TickContest(float dt) {
      float t = RunSeconds;
      int act = t < Contest.ActSeconds ? 0 : t < Contest.ActSeconds * 2 ? 1 : 2;
      if (act != Phase) {
        Phase = act;
        if (act == 1) {
          // One elite on each bank. Beating your own sends a heavy yokai to the other bank.
          for (int team = 0; team < 2; team++) SpawnEnemy(EnemyType.Elite, Contest.Base[team] + new Vector3(team == 0 ? 2.5f : -2.5f, 0, 3.5f));
          EliteSpawned = true;
        }
        if (act == 2) BossId = SpawnEnemy(EnemyType.King, Contest.BossSpawn)?.Object.Id ?? default;
        RPC_ContestAct(act);
      }

      int alive = Enemy.All.Count(e => e.Hp > 0);
      int cap = act == 0 ? 4 + 2 * PlayerCount : act == 1 ? 6 + 2 * PlayerCount : 3 + PlayerCount;
      if (SpawnTimer.ExpiredOrNotRunning(Runner) && alive < cap) {
        float r = Random.value;
        var type = act == 0 ? (r < .6f ? EnemyType.Ghost : EnemyType.Umbrella)
                 : (r < .4f ? EnemyType.Ghost : r < .8f ? EnemyType.Umbrella : EnemyType.Heavy);
        SpawnEnemy(type, EdgePosition());
        SpawnTimer = TickTimer.CreateFromSeconds(Runner, (act == 1 ? 1.3f : 2.2f) / Mathf.Sqrt(PlayerCount));
      }

      // Lanterns: a tug of war. Only one side standing in the ring moves it; both sides block each other.
      // A Ma Trơi in the ring drains it toward neutral and then takes it (owner 3: no one scores).
      for (int i = 0; i < 3; i++) {
        int lam = 0, chu = 0;
        foreach (var p in PlayerAvatar.All) {
          if (p.Downed || Fx.Flat(p.transform.position, Contest.Lanterns[i]) >= Contest.LanternRadius) continue;
          if (p.Team == 0) lam++; else chu++;
        }
        bool wispIn = WispAt(Contest.Lanterns[i]);
        float c = LanternProgress[i];
        if (wispIn) c = Mathf.MoveTowards(c, 0f, dt / Contest.WispStealSeconds);
        else if (lam > 0 && chu == 0) c -= dt * lam / Contest.CaptureSeconds;
        else if (chu > 0 && lam == 0) c += dt * chu / Contest.CaptureSeconds;
        c = Mathf.Clamp(c, -1f, 1f);
        LanternProgress.Set(i, c);
        int owner = LanternOwner[i];
        int next = owner;
        if (wispIn && Mathf.Abs(c) < .001f) next = 3;
        else if (c <= -1f) next = 1;
        else if (c >= 1f) next = 2;
        else if (owner == 1 && c > 0) next = 0;
        else if (owner == 2 && c < 0) next = 0;
        if (next != owner) {
          LanternOwner.Set(i, next);
          if (next > 0) RPC_LanternTaken(i, next - 1);
        }
        if (next == 1 || next == 2) {
          int team = next - 1;
          _lanternAcc[team] += dt * Contest.LanternPoints[i] / 2f;
        }
      }
      for (int team = 0; team < 2; team++) {
        int whole = Mathf.FloorToInt(_lanternAcc[team]);
        if (whole > 0) { _lanternAcc[team] -= whole; AddPoints(team, 0, whole); }
      }

      // Toàn Đăng: one team holds all three lanterns long enough.
      int o0 = LanternOwner[0];
      bool all = (o0 == 1 || o0 == 2) && LanternOwner[1] == o0 && LanternOwner[2] == o0;
      if (all) {
        if (FullHoldTeam != o0 - 1) { FullHoldTeam = o0 - 1; FullHold = 0; RPC_Banner("!", Contest.TeamName[o0 - 1] + " giữ cả 3 đèn", $"Giữ thêm {Contest.FullHoldSeconds:0} giây là thắng ngay"); }
        FullHold += dt;
        if (FullHold >= Contest.FullHoldSeconds) { EndMatch(o0 - 1, true); return; }
      } else { FullHold = 0; FullHoldTeam = -1; }

      TickHazards(t);
      if (t >= Contest.MatchSeconds) EndMatch(-1, false);
    }

    // ---------------- Tranh Đăng hazards (state authority) ----------------
    float _nextInk, _nextWisp;

    bool WispAt(Vector3 lantern) {
      foreach (var e in Enemy.All)
        if (e.Type == EnemyType.Wisp && e.Hp > 0 && Fx.Flat(e.transform.position, lantern) < Contest.LanternRadius) return true;
      return false;
    }

    /// <summary>Where a Ma Trơi heads: the team-held lantern worth the most right now (Đèn Cầu counts double); with nothing to steal it drifts to the middle.</summary>
    public Vector3 WispGoal(Vector3 from) {
      int best = -1; float bestScore = -1;
      for (int i = 0; i < 3; i++) {
        int owner = LanternOwner[i];
        float score = owner == 1 || owner == 2 ? 10f * Contest.LanternPoints[i] : 0f; // only lanterns a team is scoring from
        if (score <= 0) continue;
        score -= Fx.Flat(from, Contest.Lanterns[i]) * .05f; // nearer wins a tie
        if (score > bestScore) { bestScore = score; best = i; }
      }
      if (best >= 0) return Contest.Lanterns[best];
      // Nothing left to steal: sit on the nearest lantern it already took, so a team has to kill it to win it back.
      float bestD = float.MaxValue;
      for (int i = 0; i < 3; i++) {
        if (LanternOwner[i] != 3) continue;
        float dd = Fx.Flat(from, Contest.Lanterns[i]);
        if (dd < bestD) { bestD = dd; best = i; }
      }
      return best >= 0 ? Contest.Lanterns[best] : Vector3.zero;
    }

    void TickHazards(float t) {
      // Mưa mực: ink falls on lanterns people are holding (one random lantern if nobody is on one).
      if (t >= _nextInk) {
        _nextInk = t + Contest.InkRainEvery;
        var targets = new List<int>();
        for (int i = 0; i < 3; i++)
          foreach (var p in PlayerAvatar.All)
            if (!p.Downed && Fx.Flat(p.transform.position, Contest.Lanterns[i]) < Contest.LanternRadius + 1f) { targets.Add(i); break; }
        if (targets.Count == 0) targets.Add(Random.Range(0, 3));
        foreach (int i in targets)
          for (int k = 0; k < 3; k++) {
            var off = k == 0 ? Vector3.zero : Quaternion.Euler(0, Random.Range(0, 360f), 0) * Vector3.forward * Random.Range(1f, 2f);
            Telegraph(Contest.Lanterns[i] + off, Contest.InkRainRadius, Contest.InkRainWarn + k * .15f, Contest.InkRainDamage);
          }
        RPC_Toast(targets.Count > 1 ? "Mưa mực trút xuống các lồng đèn!" : $"Mưa mực trút xuống {Contest.LanternName[targets[0]]}!", Fx.Ink);
      }

      // Ma Trơi: one at a time, from 2:00, every minute.
      if (t >= _nextWisp) {
        bool alive = false;
        foreach (var e in Enemy.All) if (e.Type == EnemyType.Wisp && e.Hp > 0) alive = true;
        if (!alive) {
          SpawnEnemy(EnemyType.Wisp, Contest.WispSpawn);
          RPC_Banner("", "Ma Trơi xuất hiện", $"Nó đi trộm lồng đèn. Ra đòn cuối được +{Contest.WispPoints} điểm");
        }
        _nextWisp = t + Contest.WispEvery;
      }

      // Lời nguyền Hồng Đăng: the team far ahead gets hunted (see PlayerAvatar.NearestAlive).
      int s0 = Score(0), s1 = Score(1);
      int lead = s0 > s1 ? 0 : 1, gap = Mathf.Abs(s0 - s1);
      int cursed = CursedTeam;
      if (cursed < 0 && gap >= Contest.CurseOn) cursed = lead;
      else if (cursed >= 0 && (gap < Contest.CurseOff || lead != cursed)) cursed = -1;
      if (cursed != CursedTeam) {
        CursedTeam = cursed;
        if (cursed >= 0) RPC_Banner("!", $"Lời nguyền Hồng Đăng giáng lên {Contest.TeamName[cursed]}", "Đội dẫn quá xa: yêu quái và boss sẽ săn đội này trước");
        else RPC_Toast("Lời nguyền Hồng Đăng đã tan", Fx.Ink);
      }
    }

    void AwardBossDamage() {
      int a = TeamBossDamage[0], b = TeamBossDamage[1];
      if (a == b) return;
      AddPoints(a > b ? 0 : 1, 2, Contest.BossDamagePoints);
    }

    void EndMatch(int forcedWinner, bool fullLight) {
      if (State != RunState.Playing) return;
      var boss = Boss;
      if (boss != null && boss.Hp > 0) AwardBossDamage(); // time ran out with the boss still standing
      WonByFullLight = fullLight;
      int s0 = Score(0), s1 = Score(1);
      Winner = forcedWinner >= 0 ? forcedWinner : s0 == s1 ? -1 : s0 > s1 ? 0 : 1;
      EndRun(true);
    }

    public void OnEnemyDamaged(Enemy e, int dmg, PlayerRef from) {
      if (!HasStateAuthority || !IsContest || !e.IsBoss || State != RunState.Playing) return;
      int team = TeamOf(from);
      if (team >= 0) TeamBossDamage.Set(team, TeamBossDamage[team] + Mathf.Max(0, dmg));
    }

    void OnContestKill(Enemy e, PlayerRef from) {
      int team = TeamOf(from);
      if (e.IsBoss) {
        BossFinisher = from;
        AwardBossDamage();
        AddPoints(team, 2, Contest.BossFinishPoints);
        Fx.Hitstop(.25f);
        EndMatch(-1, false);
        return;
      }
      if (e.Type == EnemyType.Wisp) {
        AddPoints(team, 1, Contest.WispPoints);
        RPC_Toast(team >= 0 ? $"{Contest.TeamName[team]} hạ Ma Trơi · +{Contest.WispPoints}" : "Ma Trơi tan biến", team >= 0 ? Contest.TeamColor[team] : Fx.Ink);
        return;
      }
      if (e.Type == EnemyType.Elite) {
        AddPoints(team, 1, Contest.ElitePoints);
        if (team >= 0) {
          int rival = 1 - team;
          SpawnEnemy(EnemyType.Heavy, Contest.Base[rival] + new Vector3(rival == 0 ? 2f : -2f, 0, 2f));
          RPC_Banner("!", Contest.TeamName[team] + " gửi Yêu to khỏe", "sang bờ " + Contest.TeamName[rival]);
        }
        return;
      }
      AddPoints(team, 1, Contest.KillPoints);
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
      float nightMul = IsContest ? 1f : Story.Difficulty(Night); // later nights of Hành trình are tougher
      int hp = Mathf.RoundToInt(EnemyStats.Hp(type) * (1f + .6f * (PlayerCount - 1)) * nightMul);
      bool fury = EnemyStats.IsBoss(type) && Has(Twist.Fury);
      if (fury) hp = Mathf.RoundToInt(hp * 1.3f);
      var obj = Runner.Spawn(EnemyPrefab, pos, Quaternion.identity, null, (runner, o) => {
        var e = o.GetComponent<Enemy>();
        e.Type = type;
        e.MaxHp = hp;
        e.Hp = hp;
        e.Rage = fury; // Nổi giận: enraged from the first moment
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

    public void OnEnemyKilled(Enemy e, PlayerRef from) {
      if (!HasStateAuthority) return;
      Kills++;
      if (State != RunState.Playing) return;
      if (IsContest) { OnContestKill(e, from); return; }
      if (Phase == 1) PhaseKills++;
      if (e.IsBoss) { Fx.Hitstop(.25f); ClearPhase(); }
    }

    public override void FixedUpdateNetwork() {
      if (!HasStateAuthority) return;
      float dt = Runner.DeltaTime;
      switch (State) {
        case RunState.Playing:
          if (IsContest) { TickContest(dt); break; }
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

    /// <summary>Extra rules of this Hành trình chapter (none in Tranh Đăng and free nights).</summary>
    public Twist TwistNow => IsContest ? Twist.None : Story.TwistOf(Night);
    bool Has(Twist t) => (TwistNow & t) != 0;
    float _nextRain;

    // Mưa mực in a night: every few seconds ink circles drop on (and near) the players.
    void TickInkRain() {
      if (!Has(Twist.InkRain) || Phase > 3) return;
      float t = RunSeconds;
      if (_nextRain <= 0) _nextRain = t + 6f;
      if (t < _nextRain) return;
      _nextRain = t + 7.5f;
      foreach (var p in PlayerAvatar.All) {
        if (p.Downed) continue;
        var at = p.transform.position + new Vector3(Random.Range(-1.2f, 1.2f), 0, Random.Range(-1.2f, 1.2f));
        Telegraph(at, 1.7f, 1.4f, 12);
      }
    }

    /// <summary>
    /// Which yokai walks in next during a night. Newer kinds join as the chapters go on:
    /// Nghiên mực yêu from night 3, Bóng không đèn from night 5, Pháo yêu from night 7.
    /// </summary>
    EnemyType PickYokai(int phase) {
      float r = Random.value;
      if (Night >= 7 && r < .1f) return EnemyType.Firecracker;
      if (Night >= 5 && phase == 1 && r < .22f) return EnemyType.Shade;
      if (Night >= 3 && r < (phase == 0 ? .22f : .32f)) return EnemyType.Inkstone;
      float q = Random.value;
      return phase == 0 ? (q < .6f ? EnemyType.Ghost : EnemyType.Umbrella)
                        : (q < .4f ? EnemyType.Ghost : q < .75f ? EnemyType.Umbrella : EnemyType.Heavy);
    }

    void TickPhase(float dt) {
      int alive = Enemy.All.Count(e => e.Hp > 0);
      TickInkRain();
      bool swarm = Has(Twist.Swarm);
      int extra = swarm ? 3 : 0;
      float pace = swarm ? .6f : 1f;
      if (Phase == 0) {
        if (SpawnTimer.ExpiredOrNotRunning(Runner) && alive < 4 + extra + 2 * PlayerCount) {
          SpawnEnemy(PickYokai(0), EdgePosition());
          SpawnTimer = TickTimer.CreateFromSeconds(Runner, pace * 2f / Mathf.Sqrt(PlayerCount));
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
        if (SpawnTimer.ExpiredOrNotRunning(Runner) && alive < 5 + extra + 2 * PlayerCount) {
          SpawnEnemy(PickYokai(1), EdgePosition());
          SpawnTimer = TickTimer.CreateFromSeconds(Runner, pace * 1.4f / Mathf.Sqrt(PlayerCount));
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
      AdvanceNight();
      RPC_ResetPlayers();
      StartRun();
    }

    /// <summary>Host: start the night from the lobby once every guest is ready.</summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestStart() {
      if (State != RunState.Lobby || !CanStart) return;
      SetSessionOpen(false); // no one joins in the middle of a night
      RPC_ResetPlayers();
      StartRun();
    }

    /// <summary>Back to the lobby after a night ends; new friends can join again.</summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestLobby() {
      if (State != RunState.Won && State != RunState.Lost) return;
      AdvanceNight();
      EnterLobby();
    }

    /// <summary>After a night: dawn moves on to the next night (next chapter, with its season); a loss replays the same night.</summary>
    void AdvanceNight() {
      if (IsContest) { Night++; return; }
      if (State != RunState.Won) return;
      Night++;
      var ch = Story.Get(Night);
      if (ch != null) Season = ch.Season;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetSeason(int season) {
      if (State == RunState.Lobby) Season = Mathf.Clamp(season, 0, 3);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetMode(int mode) {
      if (State == RunState.Lobby) Mode = Mathf.Clamp(mode, 0, 1);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_LobbyEntered() {
      if (Hud.I) Hud.I.Banner("", "Phòng chờ", "Bấm Sẵn sàng. Chủ phòng bắt đầu khi mọi người đã sẵn sàng.");
      if (Fx.I) Fx.I.ClearHeals();
      if (PlayerAvatar.Local) PlayerAvatar.Local.OnLobbyEntered();
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
        bool big = radius > 3;
        if (!ArtSet.Play(big ? GameFx.WarningImpactBig : GameFx.WarningImpact, pos, 0, radius / (big ? 4f : 2f))) {
          Fx.I.Burst(pos + Vector3.up * .4f, Fx.Ink, big ? 24 : 14, 3.5f);
          if (big) Fx.I.RingFx(pos, Fx.Ink, radius + .2f);
        }
        if (big) CameraRig.Shake(.3f);
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
      var twist = TwistNow;
      string sub = phase == 0 && twist != Twist.None ? Phases.Sub[phase] + "  ·  Luật đêm nay: " + Story.TwistText(twist) : Phases.Sub[phase];
      if (Hud.I) Hud.I.Banner((phase + 1).ToString(), Phases.Title[phase], sub);
      if (Fx.I) Fx.I.ClearHeals();
      if (PlayerAvatar.Local) PlayerAvatar.Local.OnPhaseStarted(phase);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_ContestAct(int act) {
      if (Hud.I) Hud.I.Banner((act + 1).ToString(), Contest.ActTitle[act], Contest.ActSub[act]);
      if (act == 0) {
        if (Fx.I) Fx.I.ClearHeals();
        if (PlayerAvatar.Local) PlayerAvatar.Local.OnPhaseStarted(0);
      }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_LanternTaken(int i, int team) {
      var col = Contest.OwnerColor(team); // team 2 = Ma Trơi
      var lfx = team == 0 ? GameFx.LanternTakenLam : team == 1 ? GameFx.LanternTakenChu : GameFx.LanternTakenWisp;
      if (!ArtSet.Play(lfx, Contest.Lanterns[i]) && Fx.I) Fx.I.Burst(Contest.Lanterns[i] + Vector3.up * 2, col, 30, 4);
      var me = PlayerAvatar.Local;
      if (team < 2 && me && me.Team == team && Fx.Flat(me.transform.position, Contest.Lanterns[i]) < Contest.LanternRadius + 1) me.AddUlt(10);
      if (Hud.I) Hud.I.Toast(team < 2 ? $"{Contest.TeamName[team]} giữ {Contest.LanternName[i]}" : $"Ma Trơi trộm mất {Contest.LanternName[i]}!", col);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_Toast(string text, Color color) {
      if (Hud.I) Hud.I.Toast(text, color);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_ResetPlayers() {
      if (PlayerAvatar.Local) PlayerAvatar.Local.ResetForNewRun();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_LanternLit(int i) {
      if (!ArtSet.Play(GameFx.LanternLit, Phases.Lanterns[i]) && Fx.I) Fx.I.Burst(Phases.Lanterns[i] + Vector3.up * 2, Fx.Gold, 30, 4);
      if (PlayerAvatar.Local) PlayerAvatar.Local.AddUlt(15);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_HitFx(NetworkId enemy, Vector3 pos, int dmg, NetworkBool big, PlayerRef from, NetworkBool killed, NetworkBool drop) {
      bool boss = false;
      if (Runner.TryFindObject(enemy, out var o) && o.TryGetComponent<Enemy>(out var e)) { e.PlayHitFlash(); boss = e.IsBoss; }
      ArtSet.Play(big ? GameFx.EnemyHitBig : GameFx.EnemyHit, pos + Vector3.up * (boss ? 2f : 1f), Random.Range(0f, 360f));
      if (Hud.I) Hud.I.DamageNumber(pos + Vector3.up * (boss ? 4.2f : 2.3f), dmg, big, from == Runner.LocalPlayer);
      if (drop && Fx.I) Fx.I.HealOrb(pos);
      if (killed && from == Runner.LocalPlayer && PlayerAvatar.Local) PlayerAvatar.Local.OnKillCredit();
    }

    // ---------------- lantern visuals (all clients) ----------------
    public override void Render() {
      bool contest = IsContest;
      bool show = !contest && Phase == 0 && State == RunState.Playing;
      for (int i = 0; i < _lanterns.Count; i++) _lanterns[i].Update(show, LanternProgress[i]);
      bool showContest = contest && (State == RunState.Playing || State == RunState.Lobby);
      for (int i = 0; i < _contestLanterns.Count; i++) _contestLanterns[i].UpdateContest(showContest, LanternProgress[i], LanternOwner[i] - 1);
    }

    class LanternView {
      readonly GameObject _root, _mark, _ring, _fill;
      readonly Material _body;
      readonly float _radius;
      bool _lit;
      int _owner = -2;

      public LanternView(Vector3 pos, Transform parent, float radius) {
        _radius = radius;
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
        _ring = Gfx.MeshObj(Gfx.Ring, t, new Vector3(0, .05f, 0), Vector3.one * radius, Gfx.Fade);
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
        _fill.transform.localScale = Vector3.one * Mathf.Max(.01f, progress * _radius);
      }

      /// <summary>Tranh Đăng: capture in -1 (Lam) .. +1 (Chu); owner -1 none, 0 Lam, 1 Chu.</summary>
      public void UpdateContest(bool show, float capture, int owner) {
        _root.SetActive(show);
        if (!show) return;
        if (owner != _owner) {
          _owner = owner;
          var c = owner >= 0 ? Contest.OwnerColor(owner) : Gfx.Hex("#7a5a50");
          _body.SetColor("_BaseColor", c);
          _body.SetColor("_EmissionColor", owner >= 0 ? c * .9f : Color.black);
        }
        float t = Time.time;
        _mark.SetActive(owner < 0);
        if (owner < 0) {
          _mark.transform.localPosition = new Vector3(0, 3 + Mathf.Sin(t * 3) * .15f, 0);
          _mark.transform.Rotate(0, 120 * Time.deltaTime, 0, Space.World);
        }
        _ring.SetActive(true);
        _fill.SetActive(true);
        var side = capture < 0 ? Contest.TeamColor[0] : Contest.TeamColor[1];
        var ringCol = owner >= 0 ? Contest.OwnerColor(owner) : new Color(.85f, .66f, .23f);
        Gfx.SetTint(_ring.GetComponent<Renderer>(), new Color(ringCol.r, ringCol.g, ringCol.b, .5f + Mathf.Sin(t * 4) * .15f));
        Gfx.SetTint(_fill.GetComponent<Renderer>(), new Color(side.r, side.g, side.b, .35f));
        _fill.transform.localScale = Vector3.one * Mathf.Max(.01f, Mathf.Abs(capture) * _radius);
      }
    }
  }
}
