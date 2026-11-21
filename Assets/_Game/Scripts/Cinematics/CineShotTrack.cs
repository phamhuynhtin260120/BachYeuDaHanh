using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BachYeu {
  // ---------------- Cảnh quay: camera moves from one pose to another ----------------
  [TrackColor(.25f, .45f, .4f), TrackClipType(typeof(CineShotClip))]
  public class CineShotTrack : TrackAsset { }
}
