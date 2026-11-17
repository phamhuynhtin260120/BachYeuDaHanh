using UnityEngine;

namespace BachYeu {
  /// <summary>Third-person follow camera, angled down from behind (-Z side), with screen shake.</summary>
  public class CameraRig : MonoBehaviour {
    public static CameraRig I { get; private set; }
    public Vector3 Offset = new(0, 10.2f, -9.4f);
    public float Follow = 6f;

    Transform _target;
    float _shake;
    Vector3 _focus;

    void Awake() { I = this; _focus = new Vector3(0, 0, -5); }

    public static void SetTarget(Transform t) { if (I) I._target = t; }
    public static void Shake(float amount) { if (I) I._shake = Mathf.Max(I._shake, amount); }

    void LateUpdate() {
      if (_target) _focus = Vector3.Lerp(_focus, new Vector3(_target.position.x, 0, _target.position.z), 1 - Mathf.Exp(-Follow * Time.deltaTime));
      float k = Screen.height > Screen.width ? 1.6f : 1f; // pull back in portrait
      var pos = _focus + Offset * k;
      if (_shake > 0) {
        _shake -= Time.deltaTime;
        pos += new Vector3(Random.Range(-1f, 1f) * _shake * .7f, Random.Range(-1f, 1f) * _shake * .5f, 0);
      }
      transform.position = pos;
      transform.LookAt(_focus + Vector3.up);
    }
  }
}
