using System.Collections.Generic;
using UnityEngine;

namespace BachYeu {
  /// <summary>One spoken line. Who: a Speaker id; text may use {mask} (your first mask) and {ten} (your name).</summary>
  public struct Line {
    public string Who, Text;
    public Line(string who, string text) { Who = who; Text = text; }
  }

  /// <summary>Extra rules for a Hành trình chapter (part two). Flags: a night can mix several.</summary>
  [System.Flags]
  public enum Twist { None = 0, Fog = 1, InkRain = 2, Swarm = 4, Elites = 8, Fury = 16 }

  public class Chapter {
    public string Title, Place, Teaser, Fragment;
    public int Season;
    public Twist Twist;
    public Line[] Intro, Outro;
  }

  /// <summary>
  /// Hành trình (story mode): six nights in phố Hồng Đăng, each a full night run (4 canh giờ) with a short talk
  /// before and after. Night N of a Hành trình run plays chapter N; after the last one the nights go on as "Đêm tự do".
  /// </summary>
  public static class Story {
    // Speakers: display name and seal color. "mask" = the player's own mask, named at runtime.
    public const string Narrator = "ke", Lantern = "ba", Mask = "mask", Cat = "meo", King = "vuong";

    public static (string name, Color color) Speaker(string who) => who switch {
      Narrator => ("Lời kể", UiKit.InkGrey),
      Lantern => ("Bà Lồng", UiKit.Seal),
      Cat => ("Hỏa Khuyển", Gfx.Hex("#9e2f22")),
      King => ("Quỷ Vương", Gfx.Hex("#3a2a2a")),
      _ => (Masks.All[Mathf.Clamp(LocalProfile.MaskA, 0, Masks.All.Length - 1)].Name, Masks.All[Mathf.Clamp(LocalProfile.MaskA, 0, Masks.All.Length - 1)].Color),
    };

    public static string Fill(string text) {
      string ten = string.IsNullOrWhiteSpace(LocalProfile.Name) ? "cháu" : LocalProfile.Name.Trim();
      return text.Replace("{ten}", ten).Replace("{mask}", Speaker(Mask).name);
    }

    public static readonly Chapter[] Chapters = {
      new() {
        Title = "Đêm hội không tàn", Place = "Phố chợ Hồng Đăng", Season = 0,
        Teaser = "Đêm rằm mở hội. Đến giờ Tuất, trăng không lặn và lồng đèn đỏ dần như máu.",
        Intro = new[] {
          new Line(Narrator, "Đêm rằm, phố Hồng Đăng mở hội. Đến giờ Tuất, trăng không lặn, còn lồng đèn thì đỏ dần như máu."),
          new Line(Lantern, "Lại nữa rồi... Cháu đeo mặt nạ vào đi. Ở phố này, yêu quái chỉ sợ người có hai gương mặt."),
          new Line(Mask, "Đeo ta vào. Thắp đủ ba ngọn đèn, ta sẽ mở đường cho ngươi."),
        },
        Outro = new[] {
          new Line(Lantern, "Trời sáng rồi... nhưng cổng Hồng Đăng vẫn không chịu đóng."),
          new Line(Lantern, "Bà thấy cảnh này nhiều lần lắm rồi. Tối mai cháu lại đến nhé."),
        },
        Fragment = "Đêm đầu tiên: cổng Hồng Đăng không đóng. Bà bán lồng đèn đầu phố nói đây không phải lần đầu.",
      },
      new() {
        Title = "Mặt nạ dưới gầm cầu", Place = "Bến sông", Season = 0,
        Teaser = "Nước sông dâng đen như mực. Có thứ gì đó dưới gầm cầu đang gọi tên chiếc mặt nạ.",
        Intro = new[] {
          new Line(Narrator, "Đêm thứ hai, nước sông dâng lên, đen như mực tàu."),
          new Line(Mask, "Dưới gầm cầu có thứ gì đó gọi ta. Một chiếc mặt nạ... giống hệt ta."),
          new Line(Lantern, "Đừng nhặt đồ của người đã khuất, cháu ạ. Cứ thắp đèn cho xong đêm đã."),
        },
        Outro = new[] {
          new Line(Narrator, "Dưới gầm cầu là một chiếc mặt nạ cáo nứt đôi. Nó còn ấm, như vừa có người đeo."),
          new Line(Mask, "Nó là ta... của một đêm nào đó rất xa. Ngươi từng đeo nó rồi, {ten} ạ."),
        },
        Fragment = "Dưới gầm cầu có một chiếc mặt nạ cáo nứt đôi. Nó còn ấm, như vừa có người đeo.",
      },
      new() {
        Title = "Lời thì thầm của Hỏa Khuyển", Place = "Ngõ hẻm", Season = 1,
        Teaser = "Chó trong phố bỏ đi hết. Chỉ còn một con chó đỏ đeo vòng gai, ngồi chờ bạn ở sân đền.",
        Intro = new[] {
          new Line(Narrator, "Đêm thứ ba, chó trong phố bỏ đi hết, không còn một tiếng sủa. Chỉ còn tiếng chuông gió kêu không ngớt."),
          new Line(Cat, "Gừ... Người hai mặt lại đến. Lần này ngươi định thắp đèn cho ai?"),
          new Line(Mask, "Đừng trả lời nó. Hỏa Khuyển chỉ nói nửa sự thật."),
        },
        Outro = new[] {
          new Line(Cat, "Người giữ đèn... không muốn trời sáng đâu..."),
          new Line(Lantern, "..."),
          new Line(Lantern, "Cháu về nghỉ đi. Đừng để tâm lời yêu quái."),
        },
        Fragment = "Hỏa Khuyển thì thầm trước khi tan: \"Người giữ đèn không muốn trời sáng.\"",
      },
      new() {
        Title = "Cái tên trên chuông đền", Place = "Sân đền", Season = 2,
        Teaser = "Lá rụng kín sân đền. Quả chuông cũ tự ngân lên dù chẳng có ai gõ.",
        Intro = new[] {
          new Line(Narrator, "Lá rụng kín sân đền. Quả chuông cũ tự ngân lên, dù chẳng có ai gõ."),
          new Line(Mask, "Trên chuông có khắc chữ. Đêm nay xong việc, ngươi đọc thử xem."),
          new Line(Lantern, "Bà mong cháu sẽ không đọc được nó."),
        },
        Outro = new[] {
          new Line(Narrator, "Trên chuông khắc tên một đứa trẻ. Cái tên ấy là \"{ten}\"."),
          new Line(Mask, "Vậy là ngươi đã ở phố này từ rất lâu, lâu hơn cả ta."),
        },
        Fragment = "Trên quả chuông đền khắc tên một đứa trẻ. Cái tên ấy giống hệt tên bạn.",
      },
      new() {
        Title = "Kẻ bị nhốt sau cổng", Place = "Cổng Hồng Đăng", Season = 3,
        Teaser = "Tuyết rơi trên lồng đèn đỏ. Cổng mở toang, và Quỷ Vương đã chờ sẵn.",
        Intro = new[] {
          new Line(Narrator, "Tuyết rơi trên những chiếc lồng đèn đỏ. Cổng Hồng Đăng mở toang."),
          new Line(King, "Ngươi lại đến. Đêm nào ngươi cũng đến, rồi sáng ra lại quên sạch."),
          new Line(Mask, "Đừng nghe nó. Thắp đèn đi."),
        },
        Outro = new[] {
          new Line(King, "Ta không canh cổng. Ta cũng bị nhốt sau cổng... giống như ngươi."),
          new Line(King, "Muốn biết vì sao thì đi hỏi bà già bán đèn: ai đã thắp ngọn đèn đầu tiên?"),
        },
        Fragment = "Quỷ Vương không canh cổng. Nó cũng bị nhốt sau cổng, giống như bạn.",
      },
      new() {
        Title = "Ngọn đèn đầu tiên", Place = "Cổng Hồng Đăng", Season = 3,
        Teaser = "Đêm cuối cùng. Bà Lồng có điều muốn nói với bạn trước khi trời sáng.",
        Intro = new[] {
          new Line(Lantern, "Cháu nhớ ra rồi, phải không?"),
          new Line(Lantern, "Năm ấy cháu lạc ở hội đèn. Chính bà thắp ngọn đèn đầu tiên để giữ cháu lại phố này... vì bà không muốn mất cháu."),
          new Line(Mask, "Đêm nay là đêm cuối. Thắp đèn, rồi để trời sáng."),
        },
        Outro = new[] {
          new Line(Narrator, "Bình minh lên. Lần đầu tiên sau rất nhiều năm, cổng Hồng Đăng khép lại."),
          new Line(Lantern, "Đi đi cháu. Mùa sau... nhớ quay lại thăm bà."),
          new Line(Narrator, "Hết phần một. Phần hai · Mùa đèn tắt đã mở trên bản đồ Hành trình."),
        },
        Fragment = "Bạn nhớ ra ai đã thắp ngọn đèn đầu tiên. Mùa sau, bạn sẽ quay lại tìm người ấy.",
      },

      // ---------------- Phần hai · Mùa đèn tắt ----------------
      new() {
        Title = "Ngọn đèn không chủ", Place = "Phố chợ", Season = 1, Twist = Twist.Fog,
        Teaser = "Một năm sau. Sạp đèn đầu phố bỏ trống, chỉ còn một ngọn đèn tự sáng giữa sương.",
        Intro = new[] {
          new Line(Narrator, "Một năm sau, bạn trở lại phố Hồng Đăng. Sạp đèn đầu phố bỏ trống, sương phủ kín các ngõ."),
          new Line(Mask, "Bà Lồng không có ở đây. Chỉ còn một ngọn đèn tự sáng, không ai thắp."),
          new Line(Mask, "Sương dày lắm. Đi sát nhau, đừng để yêu quái áp sát mà không thấy."),
        },
        Outro = new[] {
          new Line(Narrator, "Khi trời sáng, ngọn đèn không chủ vẫn cháy. Dưới đáy đèn có một sợi chỉ đỏ buộc vào đâu đó rất xa."),
          new Line(Mask, "Sợi chỉ dẫn về phía bến sông. Tối mai ta đi theo nó."),
        },
        Fragment = "Sạp đèn của Bà Lồng bỏ trống. Một ngọn đèn không chủ vẫn cháy, buộc một sợi chỉ đỏ.",
      },
      new() {
        Title = "Mưa mực trên bến", Place = "Bến sông", Season = 1, Twist = Twist.InkRain,
        Teaser = "Trời đổ mưa mực. Những cái tên viết trên đèn trôi dần xuống sông.",
        Intro = new[] {
          new Line(Narrator, "Đêm thứ hai, trời đổ mưa mực. Những cái tên viết trên lồng đèn chảy loang rồi trôi xuống sông."),
          new Line(Cat, "Gừ... lại là ngươi. Mưa này rơi theo người còn nhớ tên mình đấy. Né cho khéo."),
          new Line(Mask, "Thấy vòng mực dưới chân thì lướt đi ngay."),
        },
        Outro = new[] {
          new Line(Cat, "Muốn biết bà già đi đâu à? Bà ấy đi trả một món nợ, ở phía sau cổng."),
          new Line(Cat, "Nợ gì thì ta không nói. Ta chỉ nói nửa sự thật, ngươi biết rồi mà."),
        },
        Fragment = "Hỏa Khuyển nói Bà Lồng đã đi trả một món nợ, ở phía sau cổng Hồng Đăng.",
      },
      new() {
        Title = "Đàn thạch yêu", Place = "Ngõ hẻm", Season = 2, Twist = Twist.Swarm,
        Teaser = "Hàng trăm cục thạch yêu kéo về phố, đi tìm người đã đổ hỏng chúng.",
        Intro = new[] {
          new Line(Narrator, "Lá thu rơi. Hàng trăm cục thạch yêu lũ lượt kéo về phố, nhảy lò cò khắp các ngõ."),
          new Line(Mask, "Chúng đi tìm người đã bỏ chúng lại. Đông lắm, đừng để bị vây."),
        },
        Outro = new[] {
          new Line(Narrator, "Trong đàn thạch có một cục nhỏ đội chiếc ô giấy vẽ hình ngọn đèn. Nó không đánh ai, chỉ lặng lẽ đi theo bạn."),
          new Line(Mask, "Chiếc ô ấy là của Bà Lồng. Bà để nó lại để dẫn đường cho ngươi."),
        },
        Fragment = "Một cục thạch yêu đội chiếc ô vẽ hình ngọn đèn lặng lẽ đi theo bạn. Đó là ô của Bà Lồng.",
      },
      new() {
        Title = "Tiếng chuông không ngân", Place = "Sân đền", Season = 2, Twist = Twist.Elites,
        Teaser = "Quả chuông khắc tên bạn im bặt. Bầy Rương tham đứng canh khắp sân đền.",
        Intro = new[] {
          new Line(Narrator, "Quả chuông khắc tên bạn đã im bặt. Những chiếc Rương tham há miệng đứng canh khắp sân đền."),
          new Line(King, "Ngươi đến rồi. Lần này ta không chặn đường ngươi."),
          new Line(Mask, "Quỷ Vương ra được khỏi cổng ư? Vậy thì ai đang ở bên trong?"),
        },
        Outro = new[] {
          new Line(King, "Cổng đóng thì phải có người giữ khoá. Bà già ấy đã thế chỗ ta."),
          new Line(King, "Bà ấy giữ ngọn đèn đầu tiên, và giờ chính bà ấy đang bị nhốt."),
        },
        Fragment = "Quỷ Vương được tự do vì Bà Lồng đã thế chỗ nó, ở lại giữ khoá cổng.",
      },
      new() {
        Title = "Đèn lồng của Bà", Place = "Cổng Hồng Đăng", Season = 3, Twist = Twist.Fury,
        Teaser = "Tuyết phủ cổng. Yêu quái nổi giận ngay từ đầu đêm, như biết bạn đến để làm gì.",
        Intro = new[] {
          new Line(Narrator, "Tuyết phủ trắng cổng Hồng Đăng. Đêm nay yêu quái nổi giận ngay từ canh giờ đầu."),
          new Line(Mask, "Chúng biết ngươi đến để mở cổng. Không ai muốn thả người giữ khoá ra cả."),
          new Line(King, "Ta sẽ không giúp ngươi. Nhưng ta cũng sẽ không cản."),
        },
        Outro = new[] {
          new Line(Narrator, "Sau cánh cổng, bạn thấy ngọn đèn đầu tiên. Bà Lồng ngồi cạnh, đã mờ như khói."),
          new Line(Lantern, "Cháu đến rồi à. Bà đợi lâu lắm. Đêm mai, cháu tiễn bà một đoạn nhé."),
        },
        Fragment = "Sau cánh cổng, Bà Lồng ngồi cạnh ngọn đèn đầu tiên, mờ như khói.",
      },
      new() {
        Title = "Tiễn đèn", Place = "Bến sông", Season = 0, Twist = Twist.Fog | Twist.InkRain,
        Teaser = "Đêm cuối. Thắp đèn lần nữa, rồi thả ngọn đèn đầu tiên xuôi theo dòng sông.",
        Intro = new[] {
          new Line(Lantern, "Ngọn đèn này giữ cháu ở lại phố, rồi giữ bà ở lại sau cổng. Đủ rồi cháu ạ."),
          new Line(Lantern, "Thắp đủ đèn cho trời sáng, rồi thả nó xuống sông. Bà sẽ đi theo nó."),
          new Line(Mask, "Sương và mưa mực sẽ cản ngươi lần cuối. Đi thôi."),
        },
        Outro = new[] {
          new Line(Narrator, "Ngọn đèn đầu tiên trôi xuôi dòng sông. Sương tan, mưa tạnh, phố Hồng Đăng sáng lên trong nắng xuân."),
          new Line(Lantern, "Cảm ơn cháu. Mùa hội sau, cháu thắp một ngọn đèn cho bà nhé."),
          new Line(Narrator, "Hết phần hai. Đêm tự do vẫn mở để bạn rèn luyện và kiếm Hồn đăng."),
        },
        Fragment = "Bạn thả ngọn đèn đầu tiên xuôi dòng sông. Bà Lồng đi theo ánh đèn, và phố Hồng Đăng thấy nắng.",
      },
    };

    public static readonly string[] PartName = { "Phần một · Đêm Hồng Đăng", "Phần hai · Mùa đèn tắt" };
    public const int PerPart = 6;
    public static int PartOf(int night) => Mathf.Clamp((night - 1) / PerPart, 0, PartName.Length - 1);

    public static Twist TwistOf(int night) => Get(night)?.Twist ?? Twist.None;

    /// <summary>Short name and rule text for a night's twists (empty when none).</summary>
    public static string TwistText(Twist t, bool longForm = false) {
      var parts = new List<string>();
      if ((t & Twist.Fog) != 0) parts.Add(longForm ? "Sương dày: chỉ nhìn được gần" : "Sương dày");
      if ((t & Twist.InkRain) != 0) parts.Add(longForm ? "Mưa mực: vòng mực rơi theo người chơi" : "Mưa mực");
      if ((t & Twist.Swarm) != 0) parts.Add(longForm ? "Yêu quái kéo đàn: sinh nhanh và đông hơn" : "Yêu quái kéo đàn");
      if ((t & Twist.Elites) != 0) parts.Add(longForm ? "Đầu đàn: Rương tham có mặt từ đầu" : "Đầu đàn");
      if ((t & Twist.Fury) != 0) parts.Add(longForm ? "Nổi giận: boss nổi giận ngay từ đầu, máu dày hơn" : "Boss nổi giận");
      return string.Join(" · ", parts);
    }

    public static Chapter Get(int night) => night >= 1 && night <= Chapters.Length ? Chapters[night - 1] : null;

    /// <summary>Hồn đăng for the first time a chapter is cleared.</summary>
    public static int FirstClearReward(int night) => 100 + 25 * (night - 1);

    /// <summary>Enemy health multiplier for a night (also scales the free nights after the story).</summary>
    public static float Difficulty(int night) => 1f + .12f * Mathf.Max(0, night - 1);

    // ---------- per-match flow (called from the HUD every frame) ----------
    static string _introKey, _outroKey;
    public static void ResetSession() { _introKey = _outroKey = null; _fogOn = false; }

    // Sương dày: pull the fog in close around the camera (about 14 m from the player) while the night runs.
    static bool _fogOn;
    static float _fogStart, _fogEnd;
    const float FogNear = 13f, FogFar = 26f;

    static void ApplyFog(GameDirector dir) {
      bool want = dir && !dir.IsContest && (TwistOf(dir.Night) & Twist.Fog) != 0
        && (dir.State == RunState.Playing || dir.State == RunState.Cleared || dir.State == RunState.Blessing);
      // The season change at the start of a night re-applies the normal fog: take its values as the ones to restore.
      if (want && _fogOn && !Mathf.Approximately(RenderSettings.fogEndDistance, FogFar)) _fogOn = false;
      if (want == _fogOn) return;
      _fogOn = want;
      if (want) {
        _fogStart = RenderSettings.fogStartDistance; _fogEnd = RenderSettings.fogEndDistance;
        RenderSettings.fogStartDistance = FogNear; RenderSettings.fogEndDistance = FogFar;
      } else if (Mathf.Approximately(RenderSettings.fogEndDistance, FogFar)) { // untouched by anyone else since
        RenderSettings.fogStartDistance = _fogStart; RenderSettings.fogEndDistance = _fogEnd;
      }
    }

    public static void Tick(GameDirector dir) {
      ApplyFog(dir);
      if (!dir || dir.IsContest || StoryDialog.Current || Cinematic.Playing || InkTransition.Busy) return; // talk waits for the film
      var ch = Get(dir.Night);
      if (ch == null) return;
      bool solo = dir.IsSingle;
      if (dir.State == RunState.Playing && dir.Phase == 0) {
        string key = dir.RunStartTick + "/intro";
        if (_introKey == key) return;
        _introKey = key;
        if (!Profile.Data.StorySeen.Contains(dir.Night)) {
          Profile.Data.StorySeen.Add(dir.Night);
          Profile.Save();
          StoryDialog.Show(ch.Intro, solo ? StoryDialog.Style.Pause : StoryDialog.Style.Subtitle, $"Chương {dir.Night} · {ch.Title}");
        }
      } else if (dir.State == RunState.Won && Hud.I && Hud.I.ResultPanel && Hud.I.ResultPanel.activeSelf) { // after the Win film, over the result page
        string key = dir.RunStartTick + "/outro";
        if (_outroKey == key) return;
        _outroKey = key;
        bool first = Profile.Data.StoryCleared < dir.Night;
        if (LocalProfile.Chapter <= dir.Night) LocalProfile.SetChapter(dir.Night + 1); // the map opens on the next chapter
        if (first) {
          Profile.Data.StoryCleared = dir.Night;
          int bonus = FirstClearReward(dir.Night);
          Profile.Data.Coins += bonus;
          Profile.Data.Earned += bonus;
          Profile.Save();
          Achievements.Check();
        }
        StoryDialog.Show(ch.Outro, StoryDialog.Style.Modal, first ? $"Hoàn thành chương {dir.Night} · +{FirstClearReward(dir.Night)} Hồn đăng" : $"Chương {dir.Night} · {ch.Title}");
      }
    }
  }
}
