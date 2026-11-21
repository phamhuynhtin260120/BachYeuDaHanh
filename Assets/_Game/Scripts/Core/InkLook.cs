using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BachYeu {
  /// <summary>
  /// Lighting and post-processing for the ink-wash look, one preset per season.
  /// Sun color, angle and strength; three-color ambient (sky, horizon, ground); a light post stack:
  /// muted color, a soft bloom on lanterns and fire, and a dark ink vignette.
  /// Kept cheap for phones: no depth of field, no SSAO, no motion blur.
  /// </summary>
  public static class InkLook {
    struct Preset {
      public Color Sun, Sky, Horizon, Ground, Filter;
      public float SunIntensity, SunPitch, SunYaw, Saturation, Contrast, Exposure;
    }

    // Order: Xuân, Hạ, Thu, Đông (same as ArenaBuilder.Seasons).
    static readonly Preset[] Presets = {
      new() { Sun = Gfx.Hex("#fff0dc"), SunIntensity = 1.15f, SunPitch = 48, SunYaw = 330,
              Sky = Gfx.Hex("#dbe3dc"), Horizon = Gfx.Hex("#cdd0c0"), Ground = Gfx.Hex("#7f8b7c"),
              Filter = Gfx.Hex("#fff8f4"), Saturation = -10, Contrast = 10, Exposure = .18f },
      new() { Sun = Gfx.Hex("#fff3cf"), SunIntensity = 1.35f, SunPitch = 62, SunYaw = 320,
              Sky = Gfx.Hex("#dfe9dc"), Horizon = Gfx.Hex("#d3dbc0"), Ground = Gfx.Hex("#7d9277"),
              Filter = Gfx.Hex("#fffbec"), Saturation = -6, Contrast = 12, Exposure = .22f },
      new() { Sun = Gfx.Hex("#ffd2a0"), SunIntensity = 1.1f, SunPitch = 34, SunYaw = 300,
              Sky = Gfx.Hex("#e6dccb"), Horizon = Gfx.Hex("#d9c4a2"), Ground = Gfx.Hex("#8a7860"),
              Filter = Gfx.Hex("#fff0dc"), Saturation = -8, Contrast = 14, Exposure = .12f },
      new() { Sun = Gfx.Hex("#e6eeff"), SunIntensity = .95f, SunPitch = 40, SunYaw = 340,
              Sky = Gfx.Hex("#e3e9ef"), Horizon = Gfx.Hex("#d2dae1"), Ground = Gfx.Hex("#9aa5ae"),
              Filter = Gfx.Hex("#f2f6ff"), Saturation = -22, Contrast = 8, Exposure = .2f },
    };

    static Volume _volume;
    static ColorAdjustments _color;
    static Bloom _bloom;
    static Vignette _vignette;

    public static void Apply(int season) {
      var p = Presets[Mathf.Clamp(season, 0, 3)];

      var sun = RenderSettings.sun;
      if (!sun) foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude)) if (l.type == LightType.Directional) { sun = l; break; }
      if (sun) {
        sun.color = p.Sun;
        sun.intensity = p.SunIntensity;
        sun.transform.rotation = Quaternion.Euler(p.SunPitch, p.SunYaw, 0);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = .55f; // ink wash: shadows are a grey wash, not black
        RenderSettings.sun = sun;
      }

      RenderSettings.ambientMode = AmbientMode.Trilight;
      RenderSettings.ambientSkyColor = p.Sky;
      RenderSettings.ambientEquatorColor = p.Horizon;
      RenderSettings.ambientGroundColor = p.Ground;

      EnsureVolume();
      _color.colorFilter.Override(p.Filter);
      _color.saturation.Override(p.Saturation);
      _color.contrast.Override(p.Contrast);
      _color.postExposure.Override(p.Exposure);

      var cam = Camera.main;
      if (cam) cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
    }

    static void EnsureVolume() {
      if (_volume) return;
      var go = new GameObject("InkLook Volume");
      Object.DontDestroyOnLoad(go);
      _volume = go.AddComponent<Volume>();
      _volume.isGlobal = true;
      _volume.priority = 10;
      var profile = ScriptableObject.CreateInstance<VolumeProfile>();
      profile.name = "InkLook (runtime)";
      _volume.sharedProfile = profile;

      var tone = profile.Add<Tonemapping>(true);
      tone.mode.Override(TonemappingMode.Neutral);

      _color = profile.Add<ColorAdjustments>(true);

      _bloom = profile.Add<Bloom>(true);
      _bloom.threshold.Override(1.05f);
      _bloom.intensity.Override(.35f);
      _bloom.scatter.Override(.6f);
      _bloom.highQualityFiltering.Override(false);

      _vignette = profile.Add<Vignette>(true);
      _vignette.color.Override(Gfx.Hex("#2b302e"));
      _vignette.intensity.Override(.2f);
      _vignette.smoothness.Override(.55f);
      _vignette.rounded.Override(false);
    }
  }
}
