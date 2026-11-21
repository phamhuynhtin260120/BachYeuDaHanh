using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BachYeu {
  [Serializable]
  public class CineFadeClip : PlayableAsset, ITimelineClipAsset {
    public Color Color = new(.1f, .11f, .11f, 1);
    [Range(0, 1)] public float From = 1, To = 0;
    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) {
      var p = ScriptPlayable<Behaviour>.Create(graph);
      p.GetBehaviour().Clip = this;
      return p;
    }

    public class Behaviour : PlayableBehaviour {
      public CineFadeClip Clip;
      public override void ProcessFrame(Playable playable, FrameData info, object playerData) {
        if (Clip == null) return;
        float t = Mathf.Clamp01((float)(playable.GetTime() / Math.Max(.001, playable.GetDuration())));
        var c = Clip.Color; c.a = Mathf.Lerp(Clip.From, Clip.To, t);
        Cinematic.SetFade(c);
      }
    }
  }
}
