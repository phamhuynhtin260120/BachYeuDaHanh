using UnityEngine;
using UnityEngine.EventSystems;

namespace BachYeu {
  /// <summary>Floating joystick: touch anywhere in this zone, drag to move.</summary>
  public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler {
    public RectTransform Base, Knob;
    public float Range = 70f;
    Vector2 _restPos;
    int _pointer = int.MinValue;

    void Start() { _restPos = Base.anchoredPosition; }

    public void OnPointerDown(PointerEventData e) {
      if (_pointer != int.MinValue) return;
      _pointer = e.pointerId;
      RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)Base.parent, e.position, e.pressEventCamera, out var lp);
      Base.anchoredPosition = lp;
      OnDrag(e);
    }

    public void OnDrag(PointerEventData e) {
      if (e.pointerId != _pointer) return;
      RectTransformUtility.ScreenPointToLocalPointInRectangle(Base, e.position, e.pressEventCamera, out var lp);
      var v = Vector2.ClampMagnitude(lp, Range);
      Knob.anchoredPosition = v;
      GameInput.Joystick = v / Range;
    }

    public void OnPointerUp(PointerEventData e) {
      if (e.pointerId != _pointer) return;
      _pointer = int.MinValue;
      Knob.anchoredPosition = Vector2.zero;
      Base.anchoredPosition = _restPos;
      GameInput.Joystick = Vector2.zero;
    }

    void OnDisable() { GameInput.Joystick = Vector2.zero; _pointer = int.MinValue; }
  }
}
