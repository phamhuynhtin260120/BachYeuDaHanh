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

    [Header("Scene")]
    public ArenaBuilder Arena;

    NetworkRunner _runner;
    MenuView _menu;
    bool _starting;

    void Start() {
      if (Fx.I == null) new GameObject("Fx").AddComponent<Fx>();
      _menu = MenuView.Create(MenuPrefab);
#if UNITY_EDITOR
      if (_menu.RoomCode) _menu.RoomCode.text = "1234"; // same code in every Editor instance, for local multiplayer tests
#else
      if (_menu.RoomCode) _menu.RoomCode.text = Random.Range(1000, 9999).ToString();
#endif
      if (_menu.SoloButton) _menu.SoloButton.onClick.AddListener(() => LaunchFromButton(GameMode.Single));
      if (_menu.OnlineButton) _menu.OnlineButton.onClick.AddListener(() => LaunchFromButton(GameMode.Shared));
      for (int i = 0; i < _menu.SeasonButtons.Length; i++) {
        int k = i;
        if (_menu.SeasonButtons[i]) _menu.SeasonButtons[i].onClick.AddListener(() => { if (Arena) Arena.ApplySeason(k); });
      }
    }

    void SetStatus(string s) { if (_menu && _menu.Status) _menu.Status.text = s; }

    // Buttons call this so exceptions from the async launch always reach the console.
    async void LaunchFromButton(GameMode mode) {
      try { await Launch(mode); } catch (System.Exception e) { Debug.LogException(e); _starting = false; }
    }

    async Task Launch(GameMode mode) {
      if (_starting) return;
      _starting = true;
      string code = (_menu.RoomCode ? _menu.RoomCode.text : "").Trim();
      if (mode == GameMode.Shared) {
        if (string.IsNullOrEmpty(PhotonAppSettings.Global.AppSettings.AppIdFusion)) {
          SetStatus("Chưa có Photon App ID. Mở Tools > Fusion > Fusion Hub để cài đặt.");
          _starting = false;
          return;
        }
        if (code.Length == 0) { SetStatus("Nhập mã phòng trước đã."); _starting = false; return; }
      }
      SetStatus(mode == GameMode.Shared ? $"Đang kết nối phòng {code}..." : "Đang chuẩn bị...");

      var go = new GameObject("NetworkRunner");
      _runner = go.AddComponent<NetworkRunner>();
      // No Scene argument: everything networked is spawned at runtime, so Fusion must not reload the scene.
      var result = await _runner.StartGame(new StartGameArgs {
        GameMode = mode,
        SessionName = mode == GameMode.Shared ? "byd-" + code : "",
        PlayerCount = 4,
      });

      if (!result.Ok) {
        SetStatus($"Không kết nối được ({result.ShutdownReason}). Thử lại nhé.");
        Destroy(go);
        _starting = false;
        return;
      }

      _menu.gameObject.SetActive(false);
      Hud.Create(HudPrefab, mode == GameMode.Shared ? code : null, Leave);

      var slot = _runner.LocalPlayer.PlayerId % 4;
      var player = _runner.Spawn(PlayerPrefab, new Vector3(-1.5f + slot, 0, -5), Quaternion.identity);
      _runner.SetPlayerObject(_runner.LocalPlayer, player);

      bool master = mode == GameMode.Single || _runner.IsSharedModeMasterClient;
      if (master && GameDirector.Instance == null)
        _runner.Spawn(DirectorPrefab, Vector3.zero, Quaternion.identity, null, null, NetworkSpawnFlags.SharedModeStateAuthMasterClient);
      Debug.Log($"[Bách Yêu] Đã vào trận ({mode}).");
    }

    async void Leave() {
      if (_runner) await _runner.Shutdown();
      SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
  }
}
