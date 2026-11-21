using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BachYeu {
  /// <summary>
  /// Lobby panel shown while the room waits to start a night (online only).
  /// Lives in Assets/_Game/Prefabs/UI/Lobby.prefab. Guests press Ready; the host picks the season and starts.
  /// Players can walk around the island while they wait.
  /// </summary>
  public class LobbyView : MonoBehaviour {
    public GameObject Panel;
    public Text Title;
    public Text PlayerList;
    public Text Hint;
    public Button ReadyButton;
    public Text ReadyButtonText;
    public Button StartButton;
    public Text StartButtonText;
    [Tooltip("Host only. Order: Xuân, Hạ, Thu, Đông.")]
    public Button[] SeasonButtons = new Button[0];
    public Text SeasonText;
    [Tooltip("Host only. Order: Hành trình, Tranh Đăng.")]
    public Button[] ModeButtons = new Button[0];
    [Tooltip("Tranh Đăng only: switch between Đội Lam and Đội Chu.")]
    public Button TeamButton;
    [Tooltip("Opens the mask picker for your two masks.")]
    public Button MaskButton;
    public Text MaskButtonText;
    public Color StartReadyColor = Color.white, StartWaitColor = new(.62f, .62f, .62f, .85f);

    string _roomCode;

    public static LobbyView Create(LobbyView prefab, string roomCode) {
      LobbyView view;
      if (prefab) view = Instantiate(prefab);
      else {
        var canvas = UiKit.Canvas("Lobby", 15);
        view = canvas.gameObject.AddComponent<LobbyView>();
        view.BuildDefault((RectTransform)canvas.transform);
      }
      view.name = "Lobby";
      view.Init(roomCode);
      return view;
    }

    void Init(string roomCode) {
      _roomCode = roomCode;
      if (ReadyButton) ReadyButton.onClick.AddListener(() => {
        var me = PlayerAvatar.Local;
        if (!me) return;
        if (!_picked && !me.Ready) { Pick(() => { if (PlayerAvatar.Local) PlayerAvatar.Local.SetReady(true); }); return; }
        me.SetReady(!me.Ready);
      });
      if (StartButton) StartButton.onClick.AddListener(() => {
        var d = GameDirector.Instance;
        if (!d || !d.LocalIsHost) return;
        if (!_picked) { Pick(() => { var g = GameDirector.Instance; if (g && g.CanStart) g.RPC_RequestStart(); }); return; }
        if (d.AllGuestsReady) d.RPC_RequestStart();
      });
      for (int i = 0; i < SeasonButtons.Length; i++) {
        int k = i;
        if (SeasonButtons[i]) SeasonButtons[i].onClick.AddListener(() => {
          var d = GameDirector.Instance;
          if (d && d.LocalIsHost) d.RPC_SetSeason(k);
        });
      }
      for (int i = 0; i < ModeButtons.Length; i++) {
        int k = i;
        if (ModeButtons[i]) ModeButtons[i].onClick.AddListener(() => {
          var d = GameDirector.Instance;
          if (d && d.LocalIsHost) d.RPC_SetMode(k);
        });
      }
      if (MaskButton) MaskButton.onClick.AddListener(() => Pick(null));
      if (TeamButton) TeamButton.onClick.AddListener(() => {
        var me = PlayerAvatar.Local;
        if (me) me.SetTeam(1 - me.Team);
      });
      if (Panel) Panel.SetActive(false);
    }

    int _shownMode = -1;

    // Every match starts with a fresh pick: entering the lobby (first time or back from a match) opens the
    // character select, and Ready / Start ask for it first if it was skipped.
    bool _picked, _wasShown;
    float _pickAt = -1;

    void Pick(System.Action then) {
      CharacterSelect.Open(() => {
        if (!CharacterSelect.Confirmed) return;
        _picked = true;
        then?.Invoke();
      });
    }

    void Update() {
      var d = GameDirector.Instance;
      var me = PlayerAvatar.Local;
      bool show = d && me && d.State == RunState.Lobby;
      if (Panel && Panel.activeSelf != show) Panel.SetActive(show);
      if (show && !_wasShown) { _picked = false; _pickAt = Time.unscaledTime + .5f; }
      _wasShown = show;
      if (!show) return;
      if (_pickAt > 0 && Time.unscaledTime >= _pickAt) { _pickAt = -1; if (!_picked && !me.Ready) Pick(null); }

      bool host = d.LocalIsHost;
      Set(Title, $"Phòng chờ · mã {_roomCode}");
      bool contest = d.IsContest;
      string Line(PlayerAvatar p) {
        string tag = d.IsHostAvatar(p) ? "chủ phòng" : p.Ready ? "sẵn sàng" : "chưa sẵn sàng";
        string mark = d.IsHostAvatar(p) || p.Ready ? "●" : "○";
        if (contest) mark = $"<color=#{ColorUtility.ToHtmlStringRGB(p.TeamColor)}>{mark}</color>";
        return $"{mark}  {p.NameWithTitle}{(p == me ? " (bạn)" : "")} · {tag}  <size=16><color=#4b5352>{MaskPicker.PairName(p.MaskA, p.MaskB)}</color></size>";
      }
      Set(MaskButtonText, _picked ? "Tướng: " + MaskPicker.PairName(me.MaskA, me.MaskB) : "Chọn tướng cho trận này");
      var ordered = PlayerAvatar.All.OrderBy(p => contest ? p.Team : 0).ThenBy(p => p.Object.StateAuthority.PlayerId);
      if (contest)
        Set(PlayerList, string.Join("\n", new[] { 0, 1 }.Select(team =>
          $"<color=#{ColorUtility.ToHtmlStringRGB(Contest.TeamColor[team])}><b>{Contest.TeamName[team]}</b></color>\n" +
          string.Join("\n", ordered.Where(p => p.Team == team).Select(Line).DefaultIfEmpty("   còn trống")))));
      else Set(PlayerList, string.Join("\n", ordered.Select(Line)));
      if (TeamButton) TeamButton.gameObject.SetActive(contest);
      for (int i = 0; i < ModeButtons.Length; i++)
        if (ModeButtons[i]) ModeButtons[i].interactable = host;
      if (_shownMode != d.Mode) { _shownMode = d.Mode; UiKit.SelectSegment(ModeButtons, d.Mode); }

      if (ReadyButton) ReadyButton.gameObject.SetActive(!host);
      Set(ReadyButtonText, me.Ready ? "Huỷ sẵn sàng" : "Sẵn sàng");
      if (StartButton) StartButton.gameObject.SetActive(host);
      bool canStart = d.CanStart;
      if (StartButton && StartButton.targetGraphic) StartButton.targetGraphic.color = canStart ? StartReadyColor : StartWaitColor;
      Set(StartButtonText, contest ? "Bắt đầu trận" : "Bắt đầu đêm");

      int guests = d.Guests;
      Set(Hint, host
        ? (guests == 0 ? (contest ? "Gửi mã phòng cho bạn bè. Một mình thì chỉ chơi thử được." : "Gửi mã phòng cho bạn bè, hoặc bắt đầu một mình.")
          : !d.AllGuestsReady ? $"Đang chờ sẵn sàng ({d.ReadyGuests}/{guests})."
          : !d.TeamsOk ? "Mỗi đội cần ít nhất 1 người. Bấm Đổi đội để chia lại."
          : "Mọi người đã sẵn sàng.")
        : me.Ready ? "Đang chờ chủ phòng bắt đầu..." : contest ? "Chọn đội rồi bấm Sẵn sàng." : "Bấm Sẵn sàng khi bạn đã vào vị trí.");
      if (!_picked && !me.Ready) Set(Hint, "Mỗi trận chọn lại tướng: bấm \"Chọn tướng cho trận này\" trước nhé.");

      for (int i = 0; i < SeasonButtons.Length; i++)
        if (SeasonButtons[i]) SeasonButtons[i].gameObject.SetActive(host);
      UiKit.SelectToggle(SeasonButtons, d.Season);
      Set(SeasonText, $"Mùa: {ArenaBuilder.Seasons[Mathf.Clamp(d.Season, 0, 3)].Name}" + (host ? "" : " (chủ phòng chọn)"));
    }

    static void Set(Text t, string s) { if (t && t.text != s) t.text = s; }

    /// <summary>Default layout. Also used by the Editor builder to generate Lobby.prefab.</summary>
    public void BuildDefault(RectTransform root) {
      var TL = new Vector2(0, 1); var TR = new Vector2(1, 1);
      // Right side, so the players and ping callouts stay visible in the middle. Thủy mặc: no box, a soft paper wash.
      var p = UiKit.InkImage(root, "LobbyPanel", "Wash", TR, TR, new Vector2(0, -130), new Vector2(620, 790), new Color(1, 1, 1, .9f));
      Panel = p.gameObject;
      var body = UiKit.Rect(p.transform, "Body", TL, TL, new Vector2(70, -60), new Vector2(480, 640));
      var t = body.transform;
      Title = UiKit.LabelAt(t, "Phòng chờ · mã 1234", 34, UiKit.InkBlack, TL, TL, Vector2.zero, new Vector2(480, 44), TextAnchor.MiddleLeft, FontStyle.Bold);
      Title.transform.parent.name = "Title";
      UiKit.InkImage(t, "Rule", "BrushBar", TL, TL, new Vector2(0, -56), new Vector2(40, 8), UiKit.Seal);
      UiKit.LabelAt(t, "Đồng hành", 20, UiKit.InkGrey, TL, TL, new Vector2(52, -46), new Vector2(300, 28), TextAnchor.MiddleLeft);
      TeamButton = UiKit.InkButton(t, "Đổi đội", TL, TL, new Vector2(330, -36), new Vector2(150, 50), false, null, 19);
      TeamButton.name = "TeamButton";
      MaskButton = UiKit.InkButton(t, "Mặt nạ: Hồ Ly · Tengu", TL, TL, new Vector2(0, -84), new Vector2(480, 50), false, null, 19);
      MaskButton.name = "MaskButton";
      MaskButtonText = MaskButton.GetComponentInChildren<Text>();
      PlayerList = UiKit.LabelAt(t, "●  Tín (bạn) · chủ phòng\n○  Minh · chưa sẵn sàng", 21, UiKit.InkBlack, TL, TL, new Vector2(0, -142), new Vector2(480, 120), TextAnchor.UpperLeft);
      PlayerList.lineSpacing = 1.15f;
      PlayerList.verticalOverflow = VerticalWrapMode.Overflow;
      PlayerList.transform.parent.name = "PlayerList";

      ModeButtons = new Button[2];
      for (int i = 0; i < 2; i++) {
        ModeButtons[i] = UiKit.InkButton(t, Modes.Name[i], TL, TL, new Vector2(i * 245, -272), new Vector2(235, 58), false, null, 21);
        ModeButtons[i].name = "Mode_" + i;
      }
      UiKit.SelectSegment(ModeButtons, 0);

      SeasonText = UiKit.LabelAt(t, "Mùa: Xuân", 20, UiKit.InkGrey, TL, TL, new Vector2(0, -344), new Vector2(480, 28), TextAnchor.MiddleLeft, FontStyle.Bold);
      SeasonText.transform.parent.name = "SeasonText";
      SeasonButtons = new Button[4];
      for (int i = 0; i < 4; i++) {
        SeasonButtons[i] = UiKit.InkToggle(t, ArenaBuilder.Seasons[i].Name, TL, TL, new Vector2(i * 124, -378), 76, 18);
        SeasonButtons[i].name = "Season_" + i;
      }
      UiKit.SelectToggle(SeasonButtons, 0);

      ReadyButton = UiKit.InkButton(t, "Sẵn sàng", TL, TL, new Vector2(-10, -474), new Vector2(500, 104), true, null, 30);
      ReadyButton.name = "ReadyButton";
      ReadyButtonText = ReadyButton.GetComponentInChildren<Text>();
      StartButton = UiKit.InkButton(t, "Bắt đầu đêm", TL, TL, new Vector2(-10, -474), new Vector2(500, 104), true, null, 30);
      StartButton.name = "StartButton";
      StartButtonText = StartButton.GetComponentInChildren<Text>();
      Hint = UiKit.LabelAt(t, "Gửi mã phòng cho bạn bè.", 19, UiKit.InkGrey, TL, TL, new Vector2(0, -590), new Vector2(480, 56), TextAnchor.UpperLeft);
      Hint.transform.parent.name = "Hint";
      Panel.SetActive(false);
    }
  }
}
