using UnityEngine;

namespace BachYeu {
  /// <summary>
  /// Art slots for characters and the arena. Lives at Assets/_Game/Resources/ArtSet.asset.
  /// Drag your model prefabs into the slots; any empty slot falls back to the placeholder primitives.
  ///
  /// Model rules:
  /// - Pivot at the feet, facing +Z, about 1.8 units tall for the player (scale the prefab, not the code).
  /// - No NetworkObject and no colliders needed (colliders are removed at runtime).
  /// - Optional Animator. These parameters are used when they exist (any can be missing):
  ///   Player: Speed (float), Attack, Skill, Swap, Dash, Ult, Hit (triggers), Downed (bool)
  ///   Enemy:  Speed (float), Hit (trigger)
  /// </summary>
  [CreateAssetMenu(menuName = "Bách Yêu/Art Set", fileName = "ArtSet")]
  public class ArtSet : ScriptableObject {
    [Header("Player (one prefab per mask; if only one is set it is used for both)")]
    public GameObject PlayerFox;
    public GameObject PlayerTengu;

    [Header("Yokai")]
    public GameObject Ghost;
    public GameObject Umbrella;
    public GameObject Heavy;
    [Tooltip("Empty = Umbrella prefab scaled up")]
    public GameObject Elite;
    public GameObject Cat;
    public GameObject King;

    [Header("Arena")]
    [Tooltip("Replaces the procedural floating island, trees and gate. Keep the play area flat at y = 0 within radius 12.")]
    public GameObject Environment;

    static ArtSet _current;
    static bool _loaded;

    public static ArtSet Current {
      get {
        if (!_loaded) { _current = Resources.Load<ArtSet>("ArtSet"); _loaded = true; }
        return _current;
      }
    }

    public static void ResetCache() { _current = null; _loaded = false; }

    /// <summary>Prefab for an enemy type, plus an extra scale when a fallback prefab is reused.</summary>
    public (GameObject prefab, float scale) EnemyPrefab(EnemyType t) => t switch {
      EnemyType.Ghost => (Ghost, 1f),
      EnemyType.Umbrella => (Umbrella, 1f),
      EnemyType.Heavy => (Heavy, 1f),
      EnemyType.Elite => Elite ? (Elite, 1f) : (Umbrella, 1.7f),
      EnemyType.Cat => (Cat, 1f),
      _ => (King, 1f),
    };
  }
}
