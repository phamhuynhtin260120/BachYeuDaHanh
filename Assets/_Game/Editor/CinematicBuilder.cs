using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace BachYeu.EditorTools {
  /// <summary>
  /// Writes the default films as Timeline assets in Assets/_Game/Resources/Cinematics (Trailer, Win, Lose).
  /// After that they are ordinary .playable files: open one, then Window > Sequencing > Timeline to retime shots,
  /// move the camera poses or rewrite captions. Running this menu again OVERWRITES those edits.
  /// Offsets are relative to the film's anchor: the arena center for the trailer, the local player for Win / Lose.
  /// </summary>
  public static class CinematicBuilder {
    const string Dir = "Assets/_Game/Resources/Cinematics";

    [MenuItem("Bách Yêu/Tạo lại phim Timeline (Trailer, Thắng, Thua)")]
    public static void BuildAll() {
      Directory.CreateDirectory(Dir);
      BuildTrailer();
      BuildWin();
      BuildLose();
      BuildMenuStart();
      BuildMenuHub();
      BuildHubVignettes();
      BuildResultWin();
      BuildResultLose();
      AssetDatabase.SaveAssets();
      AssetDatabase.Refresh();
      Debug.Log("[Bách Yêu] Đã tạo phim Timeline: Trailer, Win, Lose trong " + Dir);
    }

    // ---------------- helpers ----------------
    static TimelineAsset New(string name) {
      var path = $"{Dir}/{name}.playable";
      AssetDatabase.DeleteAsset(path);
      var tl = ScriptableObject.CreateInstance<TimelineAsset>();
      tl.name = name;
      AssetDatabase.CreateAsset(tl, path);
      return tl;
    }

    static void Shot(CineShotTrack t, double start, double dur, Vector3 fromPos, Vector3 fromLook, Vector3 toPos, Vector3 toLook, float fov = 40, float shake = 0, string label = null) {
      var c = t.CreateClip<CineShotClip>();
      c.start = start; c.duration = dur; c.displayName = label ?? "Cảnh quay";
      var a = (CineShotClip)c.asset;
      a.FromPos = fromPos; a.FromLook = fromLook; a.ToPos = toPos; a.ToLook = toLook;
      a.FromFov = a.ToFov = fov; a.Shake = shake;
    }

    static void Caption(CineCaptionTrack t, double start, double dur, CaptionStyle style, string title, string sub = "") {
      var c = t.CreateClip<CineCaptionClip>();
      c.start = start; c.duration = dur; c.displayName = title;
      var a = (CineCaptionClip)c.asset;
      a.Style = style; a.Title = title; a.Sub = sub;
    }

    static void Fx(CineFxTrack t, double start, GameFx kind, Vector3 at, SoundId sound, float scale = 1, bool silent = false) {
      var c = t.CreateClip<CineFxClip>();
      c.start = start; c.duration = .5; c.displayName = kind.ToString();
      var a = (CineFxClip)c.asset;
      a.Kind = kind; a.Offset = at; a.Sound = sound; a.Scale = scale; a.PlaySound = !silent;
    }

    static void Prop(CinePropTrack t, double start, double dur, PropKind kind, int id, Vector3 at, float yaw = 180, float scale = 1, bool bob = true) {
      var c = t.CreateClip<CinePropClip>();
      c.start = start; c.duration = dur; c.displayName = kind == PropKind.Yokai ? ((EnemyType)id).ToString() : Masks.All[id].Name;
      var a = (CinePropClip)c.asset;
      a.Kind = kind; a.Id = id; a.Offset = at; a.Yaw = yaw; a.Scale = scale; a.Bob = bob;
    }

    static void Fade(CineFadeTrack t, double start, double dur, float from, float to, Color? color = null) {
      var c = t.CreateClip<CineFadeClip>();
      c.start = start; c.duration = dur; c.displayName = from > to ? "Sáng dần" : "Tối dần";
      var a = (CineFadeClip)c.asset;
      a.From = from; a.To = to; a.Color = color ?? new Color(.1f, .11f, .11f, 1);
    }

    // ---------------- Trailer (about 24 s, anchor: arena center) ----------------
    static void BuildTrailer() {
      var tl = New("Trailer");
      var shots = tl.CreateTrack<CineShotTrack>(null, "Cảnh quay");
      var caps = tl.CreateTrack<CineCaptionTrack>(null, "Chữ");
      var fx = tl.CreateTrack<CineFxTrack>(null, "Hiệu ứng");
      var props = tl.CreateTrack<CinePropTrack>(null, "Đạo cụ");
      var props2 = tl.CreateTrack<CinePropTrack>(null, "Đạo cụ 2");
      var fade = tl.CreateTrack<CineFadeTrack>(null, "Màn mực");

      // 1. The festival street from above.
      Shot(shots, 0, 4, new Vector3(0, 28, -30), new Vector3(0, 0, 4), new Vector3(9, 21, -25), new Vector3(0, 0, 4), 40, 0, "Toàn cảnh phố");
      Fade(fade, 0, 1.4, 1, 0);
      Caption(caps, .8, 3, CaptionStyle.Lower, "Đêm rằm, phố Hồng Đăng mở hội", "Nhưng năm nay, trăng không chịu lặn");

      // 2. Low along the lanterns as they light.
      Shot(shots, 4, 2, new Vector3(-11, 1.9f, -1.5f), new Vector3(-7, 1.6f, 3), new Vector3(-9.6f, 2.3f, .4f), new Vector3(-7, 1.6f, 3), 42, 0, "Lồng đèn chợ");
      Shot(shots, 6, 2, new Vector3(11, 1.9f, -.5f), new Vector3(7, 1.6f, 4), new Vector3(9.6f, 2.4f, 1.4f), new Vector3(7, 1.6f, 4), 42, 0, "Lồng đèn bến");
      Fx(fx, 4.5, GameFx.LanternLit, new Vector3(-7, 0, 3), SoundId.LanternLit);
      Fx(fx, 6.6, GameFx.LanternLit, new Vector3(7, 0, 4), SoundId.LanternLit);
      Fx(fx, 7.2, GameFx.LanternLit, new Vector3(0, 0, 10), SoundId.LanternLit);
      Caption(caps, 4.3, 3.4, CaptionStyle.Lower, "Đến giờ Tuất, lồng đèn đỏ dần như máu", "");

      // 3. The yokai come.
      Shot(shots, 8, 4, new Vector3(0, 3.4f, -.8f), new Vector3(0, 1, 7), new Vector3(0, 2.6f, 1.8f), new Vector3(0, 1.2f, 8), 58, 0, "Yêu quái kéo đến"); // wide lens: the bought models are wider; stays past the lotus pond
      Prop(props, 8, 4, PropKind.Yokai, (int)EnemyType.Ghost, new Vector3(-2.4f, 0, 6.5f), 180);
      Prop(props2, 8, 4, PropKind.Yokai, (int)EnemyType.Umbrella, new Vector3(2.4f, 0, 6.8f), 190);
      Prop(props, 8.4, 3.6, PropKind.Yokai, (int)EnemyType.Heavy, new Vector3(0, 0, 9.5f), 180, 1, false);
      Fx(fx, 8.1, GameFx.EnemySpawn, new Vector3(-2.4f, 0, 6.5f), SoundId.EnemySpawn);
      Fx(fx, 8.3, GameFx.EnemySpawn, new Vector3(2.4f, 0, 6.8f), SoundId.EnemySpawn);
      Fx(fx, 8.5, GameFx.EnemySpawn, new Vector3(0, 0, 9.5f), SoundId.EnemySpawn);
      Caption(caps, 8.3, 3.4, CaptionStyle.Lower, "Trăm yêu quái kéo về phố", "Dơi đèn, thạch yêu, gấu tinh...");

      // 4. The four masks.
      Shot(shots, 12, 4, new Vector3(0, 1.9f, -12.5f), new Vector3(0, 1.1f, -4), new Vector3(0, 1.7f, -10.5f), new Vector3(0, 1f, -4), 36, 0, "Bốn mặt nạ");
      for (int i = 0; i < 4; i++) Prop(i % 2 == 0 ? props : props2, 12 + i * .15, 4 - i * .15, PropKind.Mask, i, new Vector3(-2.55f + i * 1.7f, 0, -4), 180 - (-2.55f + i * 1.7f) * 5, 1, false);
      Caption(caps, 12.4, 3.3, CaptionStyle.Lower, "Bốn mặt nạ · bốn sức mạnh", "Hồ Ly · Tengu · Hà Bá · Quỷ Đá");

      // 5. The boss.
      Shot(shots, 16, 4, new Vector3(-6.5f, 3.2f, 1.2f), new Vector3(0, 2.4f, 9.5f), new Vector3(6.5f, 3.4f, 1.8f), new Vector3(0, 2.4f, 9.5f), 38, .03f, "Quỷ Vương");
      Prop(props, 16, 4, PropKind.Yokai, (int)EnemyType.King, new Vector3(0, 0, 9.5f), 180, 1f);
      Fx(fx, 16.1, GameFx.BossSpawn, new Vector3(0, 0, 9.5f), SoundId.BossSpawn, 1.4f);
      Caption(caps, 16.4, 3.3, CaptionStyle.Big, "Hồng Đăng Quỷ Vương thức giấc", "Một đến bốn người · thắp đèn cho đến khi trời sáng");

      // 6. Title card.
      Shot(shots, 20, 4, new Vector3(0, 6, -13), new Vector3(0, 3, 0), new Vector3(0, 14, -19), new Vector3(0, 2, 0), 40, 0, "Tựa phim");
      Caption(caps, 20.3, 3.7, CaptionStyle.Card, "Bách Yêu\nDạ Hành", "Đêm hội Hồng Đăng · 1 đến 4 người");
      Fade(fade, 23.2, .8, 0, 1);
    }

    // ---------------- Menu backdrops (looping, anchor: arena centre) ----------------
    // Played behind the menu by Cinematic.PlayBackdrop: no captions or fades, silent effects, and each film ends on
    // the shot it starts with so the loop does not jump.

    /// <summary>
    /// Start screen, 48 s: a grand tour of the valley. High over the whole arena, down past the tea pavilion and its
    /// bridge, across to the waterfall falling off the floating rock, and back up to where it began (seamless loop).
    /// </summary>
    static void BuildMenuStart() {
      var tl = New("MenuStart");
      var shots = tl.CreateTrack<CineShotTrack>(null, "Cảnh quay");
      var fx = tl.CreateTrack<CineFxTrack>(null, "Hiệu ứng");
      var props = tl.CreateTrack<CinePropTrack>(null, "Đạo cụ");
      Vector3 high = new(0, 45, -60), highL = new(0, 0, 10);
      Vector3 tea = new(40, 5, 22), teaL = new(62, 1, -4);
      Vector3 fall = new(8, 7, -28), fallL = new(-45, 9, -64);
      Vector3 pond = new(-12, 11, -17), pondL = new(0, 1, 4);
      Shot(shots, 0, 12, high, highL, tea, teaL, 50, 0, "Toàn cảnh → Đình trà");
      Shot(shots, 12, 12, tea, teaL, fall, fallL, 52, 0, "Đình trà → Thác nước");
      Shot(shots, 24, 12, fall, fallL, pond, pondL, 50, 0, "Thác nước → Hồ sen");
      Shot(shots, 36, 12, pond, pondL, high, highL, 50, 0, "Hồ sen → Toàn cảnh");
      foreach (var (t, at) in new[] { (38.0, new Vector3(-7, 0, 3)), (42.0, new Vector3(7, 0, 4)), (45.0, new Vector3(0, 0, 10)) })
        Fx(fx, t, GameFx.LanternLit, at, SoundId.LanternLit, 1, true);
      Prop(props, 26, 10, PropKind.Yokai, (int)EnemyType.Ghost, new Vector3(-6, 6, -40), 230);
    }

    /// <summary>
    /// Extra hub backdrops, 24 s each: the hub picks a different one each time it opens, so the place behind the hero
    /// changes (the MenuHub film by the lotus pond is the first of them). Each drifts out and back for a clean loop.
    /// </summary>
    static void BuildHubVignettes() {
      Vignette("MenuHub_DinhTra", new(44, 2.6f, 19), new(49, 3.2f, 15), new(62, 1, -4), 50);   // tea pavilion by the wooden bridge
      Vignette("MenuHub_DinhTung", new(-6, 4, 22), new(-13, 3.4f, 27), new(3, 5.5f, 45), 50);  // two-tier pavilion under the old pine
      Vignette("MenuHub_ThacNuoc", new(18, 4, -22), new(10, 5, -16), new(-30, 15, -66), 58); // wider, tilted up: the floating rock shows beside the hero     // waterfall off the floating rock
    }

    static void Vignette(string name, Vector3 a, Vector3 b, Vector3 look, float fov) {
      var tl = New(name);
      var shots = tl.CreateTrack<CineShotTrack>(null, "Cảnh quay");
      Shot(shots, 0, 12, a, look, b, look + new Vector3(0, .4f, 0), fov, 0, "Đi");
      Shot(shots, 12, 12, b, look + new Vector3(0, .4f, 0), a, look, fov, 0, "Về");
    }

    /// <summary>Hub, 24 s: a gentle sway beside the lotus pond, out and back (the hero stands in front, in the UI).</summary>
    static void BuildMenuHub() {
      var tl = New("MenuHub");
      var shots = tl.CreateTrack<CineShotTrack>(null, "Cảnh quay");
      var fx = tl.CreateTrack<CineFxTrack>(null, "Hiệu ứng");
      Vector3 a = new(-4, 3.2f, -12), la = new(1.5f, 1.8f, 4), b = new(-1.5f, 3.6f, -11.2f), lb = new(2.5f, 1.8f, 4.5f);
      Shot(shots, 0, 12, a, la, b, lb, 46, 0, "Đi");
      Shot(shots, 12, 12, b, lb, a, la, 46, 0, "Về");
      Fx(fx, 4, GameFx.LanternLit, new Vector3(7, 0, 4), SoundId.LanternLit, .8f, true);
      Fx(fx, 16, GameFx.LanternLit, new Vector3(-7, 0, 3), SoundId.LanternLit, .8f, true);
    }

    /// <summary>
    /// Behind the win result, 20 s loop, anchor: the local player (+Z toward the arena centre). The camera circles
    /// the player at a respectful distance in four legs, lanterns flare around them now and then.
    /// </summary>
    static void BuildResultWin() {
      var tl = New("ResultWin");
      var shots = tl.CreateTrack<CineShotTrack>(null, "Cảnh quay");
      var fx = tl.CreateTrack<CineFxTrack>(null, "Hiệu ứng");
      var look = new Vector3(0, 1.1f, 0);
      Vector3 P(float deg) { float r = deg * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(r) * 6.5f, 2.4f, Mathf.Cos(r) * 6.5f); }
      float[] ring = { 200, 250, 300, 250 }; // sway across the outer side and back (the pond stays behind the player)
      for (int i = 0; i < 4; i++) Shot(shots, i * 5, 5, P(ring[i]), look, P(ring[(i + 1) % 4]), look, 40, 0, "Vòng " + (i + 1));
      Fx(fx, 2, GameFx.LanternLit, new Vector3(2.4f, 0, 2.2f), SoundId.LanternLit, 1, true);
      Fx(fx, 8, GameFx.LanternLit, new Vector3(-2.4f, 0, 2.6f), SoundId.LanternLit, 1, true);
      Fx(fx, 14, GameFx.LanternLit, new Vector3(0, 0, 3.6f), SoundId.LanternLit, 1, true);
    }

    /// <summary>Behind the lose result, 20 s loop, anchor: the local player. High and still, a slow drift over the fallen night.</summary>
    static void BuildResultLose() {
      var tl = New("ResultLose");
      var shots = tl.CreateTrack<CineShotTrack>(null, "Cảnh quay");
      Vector3 a = new(.8f, 10, -6.5f), b = new(-1.6f, 9.4f, -7.2f), look = new(0, 0, 1.5f);
      Shot(shots, 0, 10, a, look, b, look, 44, 0, "Trôi");
      Shot(shots, 10, 10, b, look, a, look, 44, 0, "Trôi về");
    }

    // ---------------- Win (about 4.5 s, anchor: local player) ----------------
    static void BuildWin() {
      var tl = New("Win");
      var shots = tl.CreateTrack<CineShotTrack>(null, "Cảnh quay");
      var caps = tl.CreateTrack<CineCaptionTrack>(null, "Chữ");
      var fx = tl.CreateTrack<CineFxTrack>(null, "Hiệu ứng");
      var fade = tl.CreateTrack<CineFadeTrack>(null, "Màn mực");
      Shot(shots, 0, 4.5, new Vector3(3.6f, 1.5f, -3.4f), new Vector3(0, 1, 0), new Vector3(-3.4f, 2.4f, -3.2f), new Vector3(0, 1.1f, 0), 42, 0, "Vòng quanh người thắng");
      Fx(fx, .15, GameFx.PlayerRevived, Vector3.zero, SoundId.Victory, 1.4f);
      Fx(fx, 1.0, GameFx.LanternLit, new Vector3(2.2f, 0, 2.2f), SoundId.LanternLit);
      Fx(fx, 1.6, GameFx.LanternLit, new Vector3(-2.2f, 0, 2.6f), SoundId.LanternLit);
      Fx(fx, 2.2, GameFx.LanternLit, new Vector3(0, 0, 3.4f), SoundId.LanternLit);
      Caption(caps, .5, 3.7, CaptionStyle.Big, "{tieu_de}", "{phu_de}");
      Fade(fade, 3.9, .6, 0, .9f, new Color(.95f, .93f, .88f, 1));
    }

    // ---------------- Lose (about 4 s, anchor: local player) ----------------
    static void BuildLose() {
      var tl = New("Lose");
      var shots = tl.CreateTrack<CineShotTrack>(null, "Cảnh quay");
      var caps = tl.CreateTrack<CineCaptionTrack>(null, "Chữ");
      var fade = tl.CreateTrack<CineFadeTrack>(null, "Màn mực");
      Shot(shots, 0, 4, new Vector3(0, 2.2f, -3.2f), new Vector3(0, .8f, 0), new Vector3(.8f, 10, -6.5f), new Vector3(0, 0, 0), 44, 0, "Kéo lên khỏi người gục");
      Caption(caps, .7, 2.9, CaptionStyle.Lower, "{tieu_de}", "{phu_de}");
      Fade(fade, 2.6, 1.4, 0, .92f);
    }
  }
}
