using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BachYeu {
  // ---------------- Hiệu ứng: plays a game effect when the clip starts ----------------
  [TrackColor(.85f, .6f, .15f), TrackClipType(typeof(CineFxClip))]
  public class CineFxTrack : TrackAsset { }
}
