using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BachYeu {
  // ---------------- Màn mực: full-screen ink (or paper) fade ----------------
  [TrackColor(.15f, .15f, .15f), TrackClipType(typeof(CineFadeClip))]
  public class CineFadeTrack : TrackAsset { }
}
