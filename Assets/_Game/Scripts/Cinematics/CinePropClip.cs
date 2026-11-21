using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BachYeu {
  public enum PropKind { Yokai, Mask }

  [Serializable]
  public class CinePropClip : PlayableAsset, ITimelineClipAsset {
    public PropKind Kind;
    [Tooltip("EnemyType number for a yokai, mask id (0 Hồ Ly, 1 Tengu, 2 Hà Bá, 3 Quỷ Đá) for a mask")] public int Id;
    public Vector3 Offset;
    public float Yaw = 180, Scale = 1;
    [Tooltip("Gentle bob, like the yokai float")] public bool Bob = true;
    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) {
      var p = ScriptPlayable<Behaviour>.Create(graph);
      p.GetBehaviour().Clip = this;
      return p;
    }

    public class Behaviour : PlayableBehaviour {
      public CinePropClip Clip;
      Transform _prop;
      public override void OnBehaviourPlay(Playable playable, FrameData info) {
        if (Clip == null || !Cinematic.Playing || _prop) return;
        _prop = Cinematic.SpawnProp(Clip.Kind, Clip.Id, Clip.Offset, Clip.Yaw, Clip.Scale);
      }
      public override void ProcessFrame(Playable playable, FrameData info, object playerData) {
        if (!_prop || Clip == null) return;
        float t = (float)playable.GetTime();
        float grow = Mathf.Clamp01(t / .35f);
        _prop.localScale = Vector3.one * Clip.Scale * (grow * grow * (3 - 2 * grow));
        if (Clip.Bob) _prop.position = Cinematic.World(Clip.Offset) + Vector3.up * (Mathf.Sin(t * 2.4f) * .12f);
      }
      public override void OnBehaviourPause(Playable playable, FrameData info) { if (_prop) UnityEngine.Object.Destroy(_prop.gameObject); _prop = null; }
      public override void OnPlayableDestroy(Playable playable) { if (_prop) UnityEngine.Object.Destroy(_prop.gameObject); }
    }
  }
}
