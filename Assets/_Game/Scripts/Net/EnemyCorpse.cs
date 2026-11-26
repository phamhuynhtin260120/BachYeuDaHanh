using UnityEngine;

namespace BachYeu {
  /// <summary>
  /// What is left of a slain yokai with a bought model: the model, cut loose from the despawned network object,
  /// plays its Die clip, lies there a moment, then sinks into the ground and is removed. Purely visual, on every client.
  /// </summary>
  public class EnemyCorpse : MonoBehaviour {
    const float Lie = 1.1f, Sink = .6f;
    float _t;
    Vector3 _scale;

    public static void Leave(Transform model, Animator anim) {
      model.SetParent(null, true);
      foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
      if (anim) { anim.SetFloat("Speed", 0); anim.SetTrigger("Die"); }
      var corpse = model.gameObject.AddComponent<EnemyCorpse>();
      corpse._scale = model.localScale;
    }

    void Update() {
      _t += Time.deltaTime;
      if (_t < Lie) return;
      float k = Mathf.Clamp01((_t - Lie) / Sink);
      transform.position += Vector3.down * Time.deltaTime * 1.4f;
      transform.localScale = _scale * (1f - k * .6f);
      if (k >= 1f) Destroy(gameObject);
    }
  }
}
