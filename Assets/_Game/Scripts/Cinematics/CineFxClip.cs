using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BachYeu {
  [Serializable]
  public class CineFxClip : PlayableAsset, ITimelineClipAsset {
    public GameFx Kind = GameFx.LanternLit;
    public Vector3 Offset;
    public float Scale = 1;
    public SoundId Sound = SoundId.LanternLit;
    public bool PlaySound = true;
    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) {
      var p = ScriptPlayable<Behaviour>.Create(graph);
      p.GetBehaviour().Clip = this;
      return p;
    }

    public class Behaviour : PlayableBehaviour {
      public CineFxClip Clip;
      public override void OnBehaviourPlay(Playable playable, FrameData info) {
        if (Clip == null || !Cinematic.Active) return;
        var pos = Cinematic.World(Clip.Offset);
        if (!ArtSet.Play(Clip.Kind, pos, 0, Clip.Scale) && Fx.I) Fx.I.Burst(pos + Vector3.up, UiKit.Seal, 18, 3f);
        if (Clip.PlaySound) Sfx.Play(Clip.Sound, pos);
      }
    }
  }
}
