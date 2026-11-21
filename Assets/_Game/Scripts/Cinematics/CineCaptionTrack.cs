using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BachYeu {
  // ---------------- Chữ: title cards, captions, lower thirds ----------------
  [TrackColor(.66f, .2f, .12f), TrackClipType(typeof(CineCaptionClip))]
  public class CineCaptionTrack : TrackAsset { }
}
