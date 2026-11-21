using System;
using System.Collections.Generic;
using UnityEngine;

namespace BachYeu {
  /// <summary>
  /// Sound slots. Lives at Assets/_Game/Resources/SoundSet.asset. Drag clips in to replace the placeholder sounds
  /// made by Synth; several clips in one row are picked at random. Empty rows keep the placeholder.
  /// </summary>
  [CreateAssetMenu(menuName = "Bách Yêu/Sound Set", fileName = "SoundSet")]
  public class SoundSet : ScriptableObject {
    [Serializable]
    public class SoundSlot {
      public SoundId Id;
      [Tooltip("One is picked at random each time")] public AudioClip[] Clips = new AudioClip[0];
      [Range(0, 2)] public float Volume = 1f;
      [Tooltip("Random pitch change, e.g. 0.06 = ±6%")] public float PitchJitter = .06f;
    }

    [Serializable]
    public class MusicSlot {
      public MusicId Id;
      [Tooltip("Looping music. Empty = the built-in đàn tranh placeholder.")] public AudioClip Clip;
      [Range(0, 2)] public float Volume = 1f;
    }

    [Header("Sound effects (empty rows use the placeholder)")]
    public List<SoundSlot> Sounds = new();
    [Header("Music")]
    public List<MusicSlot> Music = new();

    public SoundSlot Sound(SoundId id) { foreach (var s in Sounds) if (s != null && s.Id == id && s.Clips != null && s.Clips.Length > 0) return s; return null; }
    public MusicSlot Track(MusicId id) { foreach (var s in Music) if (s != null && s.Id == id && s.Clip) return s; return null; }
  }
}
