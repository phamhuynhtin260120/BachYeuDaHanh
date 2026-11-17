using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BachYeu {
  /// <summary>Fires on press (not release) so actions feel instant on touch screens.</summary>
  public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler {
    public Action OnDown, OnUp;
    public void OnPointerDown(PointerEventData e) { OnDown?.Invoke(); transform.localScale = Vector3.one * .93f; }
    public void OnPointerUp(PointerEventData e) { OnUp?.Invoke(); transform.localScale = Vector3.one; }
    public void OnPointerExit(PointerEventData e) { if (e.pointerPress == gameObject) { OnUp?.Invoke(); transform.localScale = Vector3.one; } }
  }
}
