using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BachYeu {
  /// <summary>
  /// Title menu: play solo (GameMode.Single) or create/join a room by code (GameMode.Shared).
  /// After connecting it spawns the local player and, on the master client, the GameDirector.
  /// </summary>
  public class GameLauncher : MonoBehaviour {
    [Header("Network prefabs")]
    public NetworkObject PlayerPrefab;
    public NetworkObject DirectorPrefab;

    [Header("UI prefabs (leave empty to build the default layout from code)")]
    public MenuView MenuPrefab;
    public Hud HudPrefab;
    public LobbyView LobbyPrefab;

    [Header("Scene")]
    public ArenaBuilder Arena;

    NetworkRunner _runner;
    MenuView _menu;
    bool _starting;

    void Start() {
      if (Fx.I == null) new GameObject("Fx").AddComponent<Fx>();
      // The old title menu is kept hidden, only as the holder of the name and room code fields (and the prefab
      // hooks); everything the player sees before a match is built by MenuFlow.
      _menu = MenuView.Create(MenuPrefab);
#if UNITY_EDITOR
      if (_menu.RoomCode) _menu.RoomCode.text = "1234"; // same code in every Editor instance, for local multiplayer tests
#else
      if (_menu.RoomCode) _menu.RoomCode.text = Random.Range(1000, 9999).ToString();
#endif
      if (_menu.PlayerName) _menu.PlayerName.text = PlayerPrefs.GetString(NameKey, "");
      LocalProfile.Name = PlayerName;
      LocalProfile.Season = Mathf.Clamp(PlayerPrefs.GetInt(SeasonKey, 0), 0, 3);
      SelectSeason(LocalProfile.Season);
      SelectMode(Mathf.Clamp(PlayerPrefs.GetInt(ModeKey, 0), 0, 1));
      LocalProfile.LoadMasks();
      _menu.gameObject.SetActive(false);
      Quests.Refresh();
      Time.timeScale = 1;
      MenuFlow.Show(this);
    }

    const string NameKey = "byd.name", SeasonKey = "byd.season", ModeKey = "byd.mode";

    public string PlayerName {
      get => _menu && _menu.PlayerName ? _menu.PlayerName.text.Trim() : "";
      set {
        var v = (value ?? "").Trim();
        if (v.Length > 16) v = v.Substring(0, 16);
        if (_menu && _menu.PlayerName) _menu.PlayerName.text = v;
        LocalProfile.Name = v;
        PlayerPrefs.SetString(NameKey, v);
        PlayerPrefs.Save();
      }
    }

    public string RoomCode {
      get => _menu && _menu.RoomCode ? _menu.RoomCode.text.Trim() : "";
      set { if (_menu && _menu.RoomCode) _menu.RoomCode.text = (value ?? "").Trim(); }
    }

    public void SelectMode(int i) {
      LocalProfile.Mode = i;
      PlayerPrefs.SetInt(ModeKey, i);
      if (Arena) Arena.ApplyMode(i);
      UiKit.SelectSegment(_menu.ModeButtons, i);
    }

    public void SelectSeason(int i) {
      LocalProfile.Season = i;
      PlayerPrefs.SetInt(SeasonKey, i);
      if (Arena) Arena.ApplySeason(i);
      UiKit.SelectToggle(_menu.SeasonButtons, i);
    }

    /// <summary>Where launch messages go (the room screen or the hub shows them).</summary>
    public System.Action<string> StatusSink;
    void SetStatus(string s) { if (_menu && _menu.Status) _menu.Status.text = s; StatusSink?.Invoke(s); }

    /// <summary>Alone: pick the two masks for this match first, then go. Cancelling the pick stays on the menu.</summary>
    public void PlaySolo() {
      CharacterSelect.Open(() => { if (CharacterSelect.Confirmed) LaunchFromButton(GameMode.Single); });
    }

    /// <summary>Create or join the room in RoomCode. Masks are picked in the room's lobby.</summary>
    public void PlayOnline() => LaunchFromButton(GameMode.Shared);

    // Buttons call this so exceptions from the async launch always reach the console.
    async void LaunchFromButton(GameMode mode) {
      try { await Launch(mode); } catch (System.Exception e) { Debug.LogException(e); _starting = false; }
    }

    async Task Launch(GameMode mode) {
      if (_starting) return;
      _starting = true;
      string code = (_menu.RoomCode ? _menu.RoomCode.text : "").Trim();
      LocalProfile.Name = (_menu.PlayerName ? _menu.PlayerName.text : "").Trim();
      PlayerPrefs.SetString(NameKey, LocalProfile.Name);
      PlayerPrefs.Save();
      if (mode == GameMode.Shared) {
        if (string.IsNullOrEmpty(PhotonAppSettings.Global.AppSettings.AppIdFusion)) {
          SetStatus("Chưa có Photon App ID. Mở Tools > Fusion > Fusion Hub để cài đặt.");
          _starting = false;
          return;
        }
        if (code.Length == 0) { SetStatus("Nhập mã phòng trước đã."); _starting = false; return; }
      }
      SetStatus(mode == GameMode.Shared ? $"Đang kết nối phòng {code}..." : "Đang chuẩn bị...");
      LoadingScreen.Show(mode == GameMode.Shared ? $"Đang kết nối phòng {code}" : "Đang thắp đèn");

      var go = new GameObject("NetworkRunner");
      _runner = go.AddComponent<NetworkRunner>();
      // No Scene argument: everything networked is spawned at runtime, so Fusion must not reload the scene.
      var result = await _runner.StartGame(new StartGameArgs {
        GameMode = mode,
        SessionName = mode == GameMode.Shared ? "byd-" + code : "",
        PlayerCount = 4,
        // A fresh id per launch: Editor clones (Multiplayer Play Mode) share a device id, and Photon
        // drops the older connection when the same user id joins a room twice.
        AuthValues = new Photon.Realtime.AuthenticationValues(System.Guid.NewGuid().ToString()),
      });

      if (!result.Ok) {
        SetStatus(result.ShutdownReason switch {
          ShutdownReason.GameClosed => $"Phòng {code} đang chơi dở. Đợi họ về phòng chờ rồi vào lại nhé.",
          ShutdownReason.GameIsFull => $"Phòng {code} đã đủ 4 người.",
          _ => $"Không kết nối được ({result.ShutdownReason}). Thử lại nhé.",
        });
        Destroy(go);
        _starting = false;
        LoadingScreen.Hide();
        return;
      }

      _menu.gameObject.SetActive(false);
      MenuFlow.Hide();
      Hud.Create(HudPrefab, mode == GameMode.Shared ? code : null, Leave);
      if (mode == GameMode.Shared) LobbyView.Create(LobbyPrefab, code);

      var slot = _runner.LocalPlayer.PlayerId % 4;
      var spawnAt = LocalProfile.Mode == Modes.Contest ? Contest.SpawnPoint(_runner.LocalPlayer.PlayerId % 2, _runner.LocalPlayer.PlayerId / 2) : new Vector3(-1.5f + slot, 0, -5);
      var player = _runner.Spawn(PlayerPrefab, spawnAt, Quaternion.identity);
      _runner.SetPlayerObject(_runner.LocalPlayer, player);

      bool master = mode == GameMode.Single || _runner.IsSharedModeMasterClient;
      if (master && GameDirector.Instance == null)
        _runner.Spawn(DirectorPrefab, Vector3.zero, Quaternion.identity, null, null, NetworkSpawnFlags.SharedModeStateAuthMasterClient);
      Debug.Log($"[Bách Yêu] Đã vào trận ({mode}).");
      LoadingScreen.Hide(.6f); // a moment for the arena and HUD to settle under the curtain
    }

    async void Leave() {
      LoadingScreen.Show("Đang về phố");
      Profile.Save(); // keep Bách yêu lục counts from a match left early
      if (_runner) await _runner.Shutdown();
      SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
  }
}
