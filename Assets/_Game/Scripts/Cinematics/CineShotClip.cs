using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BachYeu {
  [Serializable]
  public class CineShotClip : PlayableAsset, ITimelineClipAsset {
    public Vector3 FromPos = new(0, 3, -8), FromLook = new(0, 1, 0);
    public Vector3 ToPos = new(0, 3, -6), ToLook = new(0, 1, 0);
    public float FromFov = 40, ToFov = 40;
    [Tooltip("Ease in and out instead of moving at a constant speed")] public bool Smooth = true;
    [Tooltip("Small handheld shake")] public float Shake;
    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) {
      var p = ScriptPlayable<Behaviour>.Create(graph);
      p.GetBehaviour().Clip = this;
      return p;
    }

    public class Behaviour : PlayableBehaviour {
      public CineShotClip Clip;
      public override void ProcessFrame(Playable playable, FrameData info, object playerData) {
        var cam = Cinematic.Cam;
        if (!cam || Clip == null) return;
        float t = (float)(playable.GetTime() / Math.Max(.001, playable.GetDuration()));
        t = Mathf.Clamp01(t);
        if (Clip.Smooth) t = t * t * (3f - 2f * t);
        var pos = Cinematic.World(Vector3.Lerp(Clip.FromPos, Clip.ToPos, t));
        var look = Cinematic.World(Vector3.Lerp(Clip.FromLook, Clip.ToLook, t));
        if (Clip.Shake > 0) pos += UnityEngine.Random.insideUnitSphere * Clip.Shake;
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.LookRotation(look - pos);
        cam.fieldOfView = Mathf.Lerp(Clip.FromFov, Clip.ToFov, t);
      }
    }
  }
}
