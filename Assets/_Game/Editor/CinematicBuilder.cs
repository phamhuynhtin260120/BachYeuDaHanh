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

    static void Fx(CineFxTrack t, double start, GameFx kind, Vector3 at, SoundId sound, float scale = 1) {
      var c = t.CreateClip<CineFxClip>();
      c.start = start; c.duration = .5; c.displayName = kind.ToString();
      var a = (CineFxClip)c.asset;
      a.Kind = kind; a.Offset = at; a.Sound = sound; a.Scale = scale;
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
      Shot(shots, 8, 4, new Vector3(0, 3.2f, -1.5f), new Vector3(0, 1, 7), new Vector3(0, 2.2f, 2.8f), new Vector3(0, 1.2f, 8), 44, 0, "Yêu quái kéo đến");
      Prop(props, 8, 4, PropKind.Yokai, (int)EnemyType.Ghost, new Vector3(-2.4f, 0, 6.5f), 180);
      Prop(props2, 8, 4, PropKind.Yokai, (int)EnemyType.Umbrella, new Vector3(2.4f, 0, 6.8f), 190);
      Prop(props, 8.4, 3.6, PropKind.Yokai, (int)EnemyType.Heavy, new Vector3(0, 0, 9.5f), 180, 1, false);
      Fx(fx, 8.1, GameFx.EnemySpawn, new Vector3(-2.4f, 0, 6.5f), SoundId.EnemySpawn);
      Fx(fx, 8.3, GameFx.EnemySpawn, new Vector3(2.4f, 0, 6.8f), SoundId.EnemySpawn);
      Fx(fx, 8.5, GameFx.EnemySpawn, new Vector3(0, 0, 9.5f), SoundId.EnemySpawn);
      Caption(caps, 8.3, 3.4, CaptionStyle.Lower, "Trăm yêu quái kéo về phố", "Lồng đèn ma, ô giấy một mắt, yêu to khỏe...");

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
