using UnityEngine;

namespace BachYeu {
  /// <summary>
  /// Walks a player or yokai over the environment's colliders: follows slopes and steps, slides along walls
  /// and rocks, falls with gravity. Uses a CharacterController, so no Rigidbody and no physics simulation needed.
  /// Only the machine that moves the object (its state authority) adds one; other machines just see the synced transform.
  /// Where there is no collider under you (the old primitive island), the floor is the y = 0 plane.
  /// Actors are on the "Actor" layer and never collide with each other (spacing is done in code).
  /// </summary>
  [DisallowMultipleComponent]
  public class GroundMover : MonoBehaviour {
    public const string ActorLayerName = "Actor";
    const float Gravity = 28f, MaxFall = 30f;
    /// <summary>Position changes bigger than this between ticks count as a teleport, smaller ones as render smoothing.</summary>
    const float TeleportDistance = 1f;
    /// <summary>How far down a grounded walker is pulled each tick to follow slopes and small steps.</summary>
    const float GroundSnap = .3f;
    static int _syncFrame = -1;

    public static int ActorLayer { get; private set; } = -1;
    /// <summary>Layers that count as ground and walls: everything except actors and Ignore Raycast.</summary>
    public static int EnvMask { get; private set; } = ~0;

    CharacterController _cc;
    float _vy;
    Vector3 _last;

    static void InitLayers() {
      if (ActorLayer >= 0) return;
      ActorLayer = LayerMask.NameToLayer(ActorLayerName);
      if (ActorLayer < 0) { Debug.LogWarning("[Bách Yêu] Thiếu layer \"Actor\" (Project Settings > Tags and Layers). Nhân vật sẽ va chạm với nhau."); ActorLayer = 0; }
      else Physics.IgnoreLayerCollision(ActorLayer, ActorLayer, true);
      EnvMask = ~((ActorLayer > 0 ? 1 << ActorLayer : 0) | (1 << 2));
    }

    public static GroundMover Attach(GameObject go, float radius, float height) {
      InitLayers();
      go.layer = ActorLayer;
      // TryGetComponent, not ??: in the Editor a missing component comes back as a fake null that ?? treats as real.
      if (!go.TryGetComponent<CharacterController>(out var cc)) cc = go.AddComponent<CharacterController>();
      cc.radius = radius;
      cc.height = Mathf.Max(height, radius * 2 + .1f);
      cc.center = new Vector3(0, cc.height / 2f, 0);
      cc.skinWidth = .04f;
      cc.stepOffset = Mathf.Min(.45f, cc.height * .3f);
      cc.slopeLimit = 50;
      cc.minMoveDistance = 0;
      if (!go.TryGetComponent<GroundMover>(out var m)) m = go.AddComponent<GroundMover>();
      m._cc = cc;
      m._last = go.transform.position;
      return m;
    }

    /// <summary>Height of the ground under (x, z): the top collider below head height, or 0 if there is none.</summary>
    public static float GroundY(Vector3 pos) {
      InitLayers();
      var from = new Vector3(pos.x, pos.y + 3f, pos.z);
      return Physics.Raycast(from, Vector3.down, out var hit, 60f, EnvMask, QueryTriggerInteraction.Ignore) ? hit.point.y : 0f;
    }

    public bool Grounded { get; private set; } = true;

    /// <summary>Move by a horizontal step this tick; gravity and ground are handled here. Returns the new position.</summary>
    public Vector3 Move(Vector3 horizontal, float dt) {
      var t = transform;
      // Between ticks Fusion's NetworkTransform moves the transform a little for smooth rendering.
      // That small offset is not a real move: start the step from where the last tick ended, or every
      // tick would start from a different in-between point and the walk would stutter.
      // A big jump (spawn, teleport, blink, revive) is real: let the controller pick it up.
      float off = (t.position - _last).sqrMagnitude;
      if (off > TeleportDistance * TeleportDistance) { _cc.enabled = false; _cc.enabled = true; }
      else if (off > 0f) t.position = _last;
      // Rendering moved every transform (interpolation) without telling physics (autoSyncTransforms is off).
      // Sync once per frame before the first move, or controllers start from the rendered spot and lose distance.
      if (_syncFrame != Time.frameCount) { _syncFrame = Time.frameCount; Physics.SyncTransforms(); }
      horizontal.y = 0;
      // Walk and fall in two passes. One combined move pushes into the floor every tick and
      // loses speed on the small lips and seams of the stone floor, which reads as stutter.
      bool wasGrounded = Grounded;
      if (horizontal.sqrMagnitude > 0f) _cc.Move(horizontal);
      _vy = wasGrounded ? 0f : Mathf.Max(-MaxFall, _vy - Gravity * dt);
      float down = wasGrounded ? GroundSnap : -_vy * dt;   // stay glued to the floor going down slopes and steps
      var flags = _cc.Move(Vector3.down * down);
      Grounded = (flags & CollisionFlags.Below) != 0;
      if (!Grounded && wasGrounded) _vy = 0f;               // just walked off an edge: start falling from rest
      var p = t.position;
      if (!Grounded) {
        // No collider below at all (procedural arena, or off the art): the y = 0 plane is the floor.
        bool groundBelow = Physics.Raycast(p + Vector3.up * .5f, Vector3.down, 60f, EnvMask, QueryTriggerInteraction.Ignore);
        if (!groundBelow && p.y <= 0f) { p.y = 0f; Grounded = true; }
        if (p.y < -15f) { p.y = GroundY(new Vector3(p.x, 20f, p.z)); Grounded = true; } // fell through: put back on top
        if (p != t.position) { _cc.enabled = false; t.position = p; _cc.enabled = true; }
      }
      if (Grounded) _vy = 0;
      _last = t.position;
      return _last;
    }

    /// <summary>Place directly (e.g. end of a leap) and keep the controller in sync.</summary>
    public void Place(Vector3 pos) {
      _cc.enabled = false;
      transform.position = pos;
      _cc.enabled = true;
      _last = pos;
      _vy = 0;
    }
  }
}
