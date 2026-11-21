using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BachYeu {
  public enum CaptionStyle { Lower, Big, Card }

  [Serializable]
  public class CineCaptionClip : PlayableAsset, ITimelineClipAsset {
    [Tooltip("May use tokens set by the game, e.g. {tieu_de}, {phu_de}, {dem}")] [TextArea] public string Title = "";
    [TextArea] public string Sub = "";
    public CaptionStyle Style = CaptionStyle.Lower;
    public float Fade = .35f;
    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) {
      var p = ScriptPlayable<Behaviour>.Create(graph);
      p.GetBehaviour().Clip = this;
      return p;
    }

    public class Behaviour : PlayableBehaviour {
      public CineCaptionClip Clip;
      public override void OnBehaviourPlay(Playable playable, FrameData info) { if (Clip != null) Cinematic.ShowCaption(Clip.Style, Cinematic.Fill(Clip.Title), Cinematic.Fill(Clip.Sub)); }
      public override void OnBehaviourPause(Playable playable, FrameData info) { if (Clip != null) Cinematic.HideCaption(Clip.Style); }
      public override void ProcessFrame(Playable playable, FrameData info, object playerData) {
        if (Clip == null) return;
        double t = playable.GetTime(), d = playable.GetDuration();
        float a = Mathf.Clamp01((float)Math.Min(t / Clip.Fade, (d - t) / Clip.Fade));
        Cinematic.CaptionAlpha(Clip.Style, a, (float)(t / Math.Max(.001, d)));
      }
    }
  }
}
