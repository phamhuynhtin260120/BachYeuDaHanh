using System;
using System.Collections.Generic;
using UnityEngine;

namespace BachYeu {
  /// <summary>
  /// Local-only visual effects: petals, rings, slashes, telegraphs, projectiles, heal orbs.
  /// Nothing here is networked; gameplay code decides who sees what.
  /// </summary>
  public class Fx : MonoBehaviour {
    public static Fx I { get; private set; }

    public static readonly Color Ink = Gfx.Hex("#2a221d");
    public static readonly Color Seal = Gfx.Hex("#a8321f");
    public static readonly Color Fire = Gfx.Hex("#ff8a3d");
    public static readonly Color FireDeep = Gfx.Hex("#d0552c");
    public static readonly Color Wind = Gfx.Hex("#2f6d9c");
    public static readonly Color WindLight = Gfx.Hex("#dfe9f0");
    public static readonly Color Gold = Gfx.Hex("#f2c14e");
    public static readonly Color Jade = Gfx.Hex("#6fbf7e");

    public Color PetalColor = Gfx.Hex("#f4a7b9");

    abstract class Item {
      public float Life, Max;
      public abstract bool Tick(float dt); // false = remove
      public virtual void Kill() { }
    }

    readonly List<Item> _items = new();
    readonly Stack<Renderer> _quadPool = new();
    float _hitstop;

    public static bool HitstopActive => I && I._hitstop > 0;
    public static void Hitstop(float t) { if (I) I._hitstop = Mathf.Max(I._hitstop, t); }

    void Awake() { I = this; }

    void Update() {
      float dt = Time.deltaTime;
      if (_hitstop > 0) { _hitstop -= dt; dt *= .15f; }
      for (int i = _items.Count - 1; i >= 0; i--) {
        var it = _items[i];
        it.Life -= dt;
        if (!it.Tick(dt) || it.Life <= 0) { it.Kill(); _items.RemoveAt(i); }
      }
    }

    // ---------- pooled quads ----------
    Renderer GetQuad() {
      Renderer r;
      if (_quadPool.Count > 0) { r = _quadPool.Pop(); r.gameObject.SetActive(true); }
      else r = Gfx.MeshObj(Gfx.Quad, transform, Vector3.zero, Vector3.one, Gfx.Fade).GetComponent<Renderer>();
      return r;
    }
    void ReleaseQuad(Renderer r) { r.gameObject.SetActive(false); _quadPool.Push(r); }

    class Particle : Item {
      public Fx Owner; public Renderer R; public Vector3 V; public Color C; public Vector3 Spin;
      public override bool Tick(float dt) {
        var t = R.transform;
        t.position += V * dt;
        V.y -= 4f * dt;
        t.Rotate(Spin * dt, Space.Self);
        var c = C; c.a = Mathf.Clamp01(Life / Max);
        Gfx.SetTint(R, c);
        return true;
      }
      public override void Kill() => Owner.ReleaseQuad(R);
    }

    public void Burst(Vector3 pos, Color c, int n, float speed) {
      if (_items.Count > 500) return;
      for (int i = 0; i < n; i++) {
        var r = GetQuad();
        r.transform.position = pos;
        r.transform.rotation = UnityEngine.Random.rotation;
        var dir = new Vector3(UnityEngine.Random.value - .5f, UnityEngine.Random.value * .8f + .2f, UnityEngine.Random.value - .5f).normalized;
        _items.Add(new Particle {
          Owner = this, R = r, C = c, Life = .8f, Max = .8f,
          V = dir * speed * (.5f + UnityEngine.Random.value),
          Spin = new Vector3(460, 290, 0)
        });
      }
    }

    // ---------- mesh based effects ----------
    class MeshFx : Item {
      public Renderer R; public Color C; public Action<MeshFx, float> OnTick; public bool OwnMesh;
      public override bool Tick(float dt) { OnTick?.Invoke(this, 1f - Life / Max); return true; }
      public override void Kill() {
        if (OwnMesh) Destroy(R.GetComponent<MeshFilter>().sharedMesh);
        Destroy(R.gameObject);
      }
      public void Alpha(float a) { var c = C; c.a = a; Gfx.SetTint(R, c); }
    }

    MeshFx AddMesh(Mesh mesh, Vector3 pos, Vector3 scale, Color c, float life, Action<MeshFx, float> tick, bool own = false) {
      var go = Gfx.MeshObj(mesh, transform, pos, scale, Gfx.Fade);
      var fx = new MeshFx { R = go.GetComponent<Renderer>(), C = c, Life = life, Max = life, OnTick = tick, OwnMesh = own };
      fx.Alpha(c.a);
      _items.Add(fx);
      return fx;
    }

    /// <summary>Expanding ring on the ground.</summary>
    public void RingFx(Vector3 pos, Color c, float radius, float dur = .45f) {
      pos.y = .07f;
      AddMesh(Gfx.Ring, pos, Vector3.one * .3f * radius, c, dur, (f, k) => {
        f.R.transform.localScale = Vector3.one * radius * (.3f + .7f * k);
        f.Alpha((1 - k) * .85f);
      });
    }

    /// <summary>Slash arc in front of a character. yaw in degrees.</summary>
    public void Slash(Vector3 pos, float yaw, float radius, bool big, Color c) {
      float len = big ? 206f : 137f;
      var mesh = Gfx.Arc(radius * .55f, radius, yaw - len / 2f, len, 24, "Slash");
      pos.y = .5f;
      AddMesh(mesh, pos, Vector3.one, new Color(c.r, c.g, c.b, .55f), .18f, (f, k) => f.Alpha((1 - k) * .55f), true);
    }

    public void Pillar(Vector3 pos, float delay) {
      pos.y = 0;
      var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      DestroyImmediate(cyl.GetComponent<Collider>());
      var mesh = cyl.GetComponent<MeshFilter>().sharedMesh;
      Destroy(cyl);
      float total = 1f + delay;
      AddMesh(mesh, pos, new Vector3(1.2f, .01f, 1.2f), new Color(Fire.r, Fire.g, Fire.b, .75f), total, (f, k) => {
        float age = k * total - delay;
        if (age < 0) { f.R.transform.localScale = new Vector3(1.2f, .01f, 1.2f); return; }
        float h = Mathf.Min(1f, age / .18f) * 2.25f;
        f.R.transform.localScale = new Vector3(1.2f, h, 1.2f);
        f.R.transform.position = new Vector3(pos.x, h, pos.z);
        f.Alpha(age > .5f ? Mathf.Max(0, .75f * (1 - (age - .5f) / .5f)) : .75f);
      });
    }

    public void Spiral(Vector3 center, Color c) {
      for (int i = 0; i < 70; i++) {
        var r = GetQuad();
        float a = UnityEngine.Random.value * Mathf.PI * 2, rad = .5f + UnityEngine.Random.value, y = UnityEngine.Random.value * .5f;
        var col = i % 3 == 0 ? Seal : c;
        _items.Add(new SpiralItem { Owner = this, R = r, A = a, Rad = rad, Y = y, Center = center, C = col, Life = 1.3f, Max = 1.3f });
      }
    }

    class SpiralItem : Item {
      public Fx Owner; public Renderer R; public float A, Rad, Y; public Vector3 Center; public Color C;
      public override bool Tick(float dt) {
        A += dt * 7; Rad += dt * 2.6f; Y += dt * 2;
        R.transform.position = Center + new Vector3(Mathf.Cos(A) * Rad, Y, Mathf.Sin(A) * Rad);
        R.transform.Rotate(0, 500 * dt, 0);
        var c = C; c.a = Mathf.Min(1, Life); Gfx.SetTint(R, c);
        return true;
      }
      public override void Kill() => Owner.ReleaseQuad(R);
    }

    /// <summary>Red ground circle that fills up, then calls onDone.</summary>
    public void TeleCircle(Vector3 pos, float radius, float dur, Action onDone) {
      pos.y = .05f;
      var ring = AddMesh(Gfx.Ring, pos, Vector3.one * radius, new Color(Seal.r, Seal.g, Seal.b, .85f), dur, null);
      var fill = AddMesh(Gfx.Disc, pos + Vector3.up * .01f, Vector3.one * .01f, new Color(Seal.r, Seal.g, Seal.b, .3f), dur, (f, k) => {
        f.R.transform.localScale = Vector3.one * Mathf.Max(.01f, radius * k);
      });
      fill.OnTick += (f, k) => { if (k >= .999f) { } };
      _items.Add(new Callback { Life = dur, Max = dur, Done = onDone });
    }

    class Callback : Item {
      public Action Done;
      public override bool Tick(float dt) => true;
      public override void Kill() => Done?.Invoke();
    }

    /// <summary>Red lane on the ground, used for charges. dir is flattened.</summary>
    public void TeleLine(Vector3 pos, Vector3 dir, float len, float width, float dur) {
      dir.y = 0; dir.Normalize();
      var center = pos + dir * len / 2f; center.y = .06f;
      var fx = AddMesh(Gfx.Quad, center, Vector3.one, new Color(Seal.r, Seal.g, Seal.b, .15f), dur, (f, k) => f.Alpha(.12f + .35f * k));
      var t = fx.R.transform;
      t.rotation = Quaternion.LookRotation(Vector3.down, dir);
      t.localScale = new Vector3(width / .2f, len / .13f, 1f);
    }

    // ---------- projectiles (visual + optional caster-side hit) ----------
    class Orb : Item {
      public Fx Owner; public Transform T; public Vector3 V; public Enemy Target; public Action<Enemy> OnHit; public int Trail;
      public override bool Tick(float dt) {
        if (Target == null || !Target.Alive) Target = Enemy.Nearest(T.position, 12f);
        if (Target != null) {
          var to = Target.transform.position + Vector3.up - T.position;
          V = Vector3.Lerp(V, to.normalized * 13f, Mathf.Min(1, dt * 5));
          if (to.magnitude < Target.Radius + .4f) {
            OnHit?.Invoke(Target);
            Owner.Burst(T.position, Fire, 8, 2.5f);
            return false;
          }
        }
        T.position += V * dt;
        if (++Trail % 3 == 0) Owner.Burst(T.position, Gfx.Hex("#ffc27a"), 1, .6f);
        return true;
      }
      public override void Kill() => Destroy(T.gameObject);
    }

    public void FoxOrb(Vector3 from, Vector3 forward, Enemy target, Action<Enemy> onHit) {
      var go = Gfx.Prim(PrimitiveType.Sphere, transform, from + Vector3.up * 1.2f, Vector3.one * .44f, Gfx.Unlit(Fire));
      var v = new Vector3(UnityEngine.Random.Range(-3f, 3f), 3f, UnityEngine.Random.Range(-3f, 3f)) + forward * 4f;
      _items.Add(new Orb { Owner = this, T = go.transform, V = v, Target = target, OnHit = onHit, Life = 2.5f, Max = 2.5f });
    }

    class Blade : Item {
      public Transform T; public Vector3 V; public Action<Enemy> OnHit; public readonly HashSet<Enemy> Hit = new();
      public override bool Tick(float dt) {
        T.position += V * dt;
        if (OnHit != null)
          foreach (var e in Enemy.All)
            if (e.Alive && !Hit.Contains(e) && Flat(e.transform.position, T.position) < .9f + e.Radius) { Hit.Add(e); OnHit(e); }
        return true;
      }
      public override void Kill() => Destroy(T.gameObject);
    }

    public void WindBlade(Vector3 from, Vector3 dir, Action<Enemy> onHit) {
      var go = Gfx.Prim(PrimitiveType.Cube, transform, from + dir + Vector3.up, new Vector3(1.6f, .08f, .3f), Gfx.Unlit(WindLight));
      go.transform.rotation = Quaternion.LookRotation(dir);
      _items.Add(new Blade { T = go.transform, V = dir * 15f, OnHit = onHit, Life = .6f, Max = .6f });
    }

    class Tornado : Item {
      public Transform T; public Renderer R; public Vector3 C; public float Tick_; public Action<Vector3> OnTickHit; public Action<Vector3, float> OnPull;
      public override bool Tick(float dt) {
        T.Rotate(0, 520 * dt, 0);
        Gfx.SetTint(R, new Color(.81f, .89f, .93f, Mathf.Min(.5f, Life * 1.6f)));
        OnPull?.Invoke(C, dt);
        Tick_ -= dt;
        if (Tick_ <= 0) { Tick_ = .3f; OnTickHit?.Invoke(C); }
        return true;
      }
      public override void Kill() => Destroy(T.gameObject);
    }

    public void TornadoFx(Vector3 center, Action<Vector3> onTickHit, Action<Vector3, float> onPull) {
      center.y = 0;
      var go = Gfx.MeshObj(Gfx.Cone, transform, center + Vector3.up * 3.2f, new Vector3(5.2f, -3.2f, 5.2f), Gfx.Fade);
      var r = go.GetComponent<Renderer>();
      _items.Add(new Tornado { T = go.transform, R = r, C = center, OnTickHit = onTickHit, OnPull = onPull, Life = 2.6f, Max = 2.6f });
    }

    // ---------- heal orbs (instanced per client) ----------
    class Heal : Item {
      public Transform T; public float Seed;
      public override bool Tick(float dt) {
        T.Rotate(0, 170 * dt, 0);
        var p = T.position; p.y = .7f + Mathf.Sin(Time.time * 4 + Seed) * .12f; T.position = p;
        var me = PlayerAvatar.Local;
        if (me != null && !me.Downed && Flat(me.transform.position, p) < 1.4f) {
          me.Heal(10);
          I.Burst(p, Jade, 10, 2);
          return false;
        }
        return true;
      }
      public override void Kill() => Destroy(T.gameObject);
    }

    public void HealOrb(Vector3 pos) {
      pos.y = .7f;
      var go = Gfx.Prim(PrimitiveType.Sphere, transform, pos, new Vector3(.4f, .5f, .4f), Gfx.Unlit(Jade));
      _items.Add(new Heal { T = go.transform, Seed = UnityEngine.Random.value * 10, Life = 12, Max = 12 });
    }

    public void ClearHeals() {
      for (int i = _items.Count - 1; i >= 0; i--)
        if (_items[i] is Heal h) { h.Kill(); _items.RemoveAt(i); }
    }

    public static float Flat(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }
  }
}
