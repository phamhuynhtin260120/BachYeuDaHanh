using UnityEngine;

namespace BachYeu {
  /// <summary>
  /// The project enters Play mode without a domain reload, so static caches survive between
  /// sessions while the objects they point to are destroyed. Clear them before every session.
  /// </summary>
  static class StaticReset {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() {
      Gfx.ResetCaches();
      UiKit.ResetCaches();
      ArtSet.ResetCache();
      AnimUtil.ResetCache();
      Enemy.All.Clear();
      PlayerAvatar.All.Clear();
      GameInput.Joystick = Vector2.zero;
      GameInput.AttackButtonHeld = false;
      GameInput.ClearAll();
      GameSettings.ResetSession();
      Profile.ResetSession();
      Story.ResetSession();
      Tutorial.ResetSession();
      Quests.ResetSession();
      Achievements.ResetSession();
      MenuFlow.ResetSession();
      LocalProfile.ResetSession();
    }
  }
}
