using UnityEngine;
using UnityEngine.InputSystem;

namespace BachYeu {
  public enum GameAction { Skill1, Skill2, Swap, Dash, Ult }

  /// <summary>
  /// Merges keyboard and on-screen controls. Presses are buffered every frame
  /// and consumed inside FixedUpdateNetwork, so no press is lost between ticks.
  /// </summary>
  public static class GameInput {
    public static Vector2 Joystick;
    public static bool AttackButtonHeld;
    static readonly bool[] _pressed = new bool[5];

    public static void Press(GameAction a) => _pressed[(int)a] = true;

    public static bool Consume(GameAction a) {
      if (!_pressed[(int)a]) return false;
      _pressed[(int)a] = false;
      return true;
    }

    public static void ClearAll() {
      for (int i = 0; i < _pressed.Length; i++) _pressed[i] = false;
    }

    /// <summary>Call once per rendered frame from the local player.</summary>
    public static void PollKeyboard() {
      var k = Keyboard.current;
      if (k == null) return;
      if (k.kKey.wasPressedThisFrame) Press(GameAction.Skill1);
      if (k.lKey.wasPressedThisFrame) Press(GameAction.Skill2);
      if (k.qKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame) Press(GameAction.Swap);
      if (k.spaceKey.wasPressedThisFrame || k.leftShiftKey.wasPressedThisFrame) Press(GameAction.Dash);
      if (k.uKey.wasPressedThisFrame || k.rKey.wasPressedThisFrame) Press(GameAction.Ult);
    }

    /// <summary>Movement on the XZ plane: x = right, y = forward (+Z).</summary>
    public static Vector2 Move {
      get {
        var v = Joystick;
        var k = Keyboard.current;
        if (k != null) {
          if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1;
          if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1;
          if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1;
          if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1;
        }
        return Vector2.ClampMagnitude(v, 1f);
      }
    }

    public static bool AttackHeld {
      get {
        var k = Keyboard.current;
        return AttackButtonHeld || (k != null && k.jKey.isPressed);
      }
    }
  }
}
