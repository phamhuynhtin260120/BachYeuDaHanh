using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BachYeu {
  // ---------------- Đạo cụ: puts a yokai or a mask character on stage while the clip runs ----------------
  [TrackColor(.4f, .3f, .55f), TrackClipType(typeof(CinePropClip))]
  public class CinePropTrack : TrackAsset { }
}
