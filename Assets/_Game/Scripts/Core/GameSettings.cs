using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BachYeu {
  /// <summary>
  /// Player options (Cài đặt), saved in PlayerPrefs. Call Load() once at startup; setters save and apply at once.
  /// </summary>
  public static class GameSettings {
    public const int QualityLow = 0, QualityMid = 1, QualityHigh = 2;
    public static readonly string[] QualityNames = { "Thấp", "Vừa", "Cao" };
    public static readonly string[] FpsNames = { "30", "60" };

    public static float MusicVolume { get; private set; } = .6f;
    public static float SfxVolume { get; private set; } = .8f;
    public static int Quality { get; private set; } = QualityMid;
    public static int FpsIndex { get; private set; } = 1;
    public static bool Vibrate { get; private set; } = true;
    public static bool ScreenShake { get; private set; } = true;
    /// <summary>Nét quái: how the yokai are painted, 0 = light ink, .5 = default, 1 = bold colors and thick outline.</summary>
    public static float InkStyle { get; private set; } = .5f;
    public static readonly string[] InkStyleNames = { "Nhạt", "Vừa", "Đậm" };
    public static readonly float[] InkStylePresets = { 0f, .5f, 1f };
    /// <summary>Giao diện: which UI skin UiKit paints with.</summary>
    public const int SkinInk = 0, SkinCoPhong = 1, SkinTienCanh = 2;
    public static readonly string[] SkinNames = { "Thuỷ mặc", "Cổ phong", "Tiên cảnh" };
    public static int Skin { get; private set; } = SkinInk;

    /// <summary>Raised after any option changes (the audio system listens to it).</summary>
    public static event Action Changed;

    const string KMusic = "byd.set.music", KSfx = "byd.set.sfx", KQuality = "byd.set.quality", KFps = "byd.set.fps",
      KVibrate = "byd.set.vibrate", KShake = "byd.set.shake", KInk = "byd.set.inkstyle", KSkin = "byd.set.skin";

    static bool _loaded;

    /// <summary>Play mode starts without a domain reload: forget the last session so Load() applies the options again.</summary>
    public static void ResetSession() { Application.quitting -= RestoreRenderAsset; _loaded = false; _asset = null; Changed = null; }

    public static void Load() {
      if (_loaded) return;
      _loaded = true;
      MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KMusic, .6f));
      SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KSfx, .8f));
      Quality = Mathf.Clamp(PlayerPrefs.GetInt(KQuality, Application.isMobilePlatform ? QualityMid : QualityHigh), 0, 2);
      FpsIndex = Mathf.Clamp(PlayerPrefs.GetInt(KFps, 1), 0, 1);
      Vibrate = PlayerPrefs.GetInt(KVibrate, 1) == 1;
      ScreenShake = PlayerPrefs.GetInt(KShake, 1) == 1;
      InkStyle = Mathf.Clamp01(PlayerPrefs.GetFloat(KInk, .5f));
      Skin = Mathf.Clamp(PlayerPrefs.GetInt(KSkin, SkinInk), 0, 2);
      ApplyInkStyle();
      Application.quitting += RestoreRenderAsset;
      ApplyGraphics();
    }

    public static void SetMusic(float v) { MusicVolume = Mathf.Clamp01(v); PlayerPrefs.SetFloat(KMusic, MusicVolume); Changed?.Invoke(); }
    public static void SetSfx(float v) { SfxVolume = Mathf.Clamp01(v); PlayerPrefs.SetFloat(KSfx, SfxVolume); Changed?.Invoke(); }
    public static void SetQuality(int q) { Quality = Mathf.Clamp(q, 0, 2); PlayerPrefs.SetInt(KQuality, Quality); ApplyGraphics(); Changed?.Invoke(); }
    public static void SetFps(int i) { FpsIndex = Mathf.Clamp(i, 0, 1); PlayerPrefs.SetInt(KFps, FpsIndex); ApplyGraphics(); Changed?.Invoke(); }
    public static void SetVibrate(bool on) { Vibrate = on; PlayerPrefs.SetInt(KVibrate, on ? 1 : 0); Changed?.Invoke(); }
    public static void SetScreenShake(bool on) { ScreenShake = on; PlayerPrefs.SetInt(KShake, on ? 1 : 0); Changed?.Invoke(); }
    public static void SetInkStyle(float v) { InkStyle = Mathf.Clamp01(v); PlayerPrefs.SetFloat(KInk, InkStyle); ApplyInkStyle(); Changed?.Invoke(); }
    /// <summary>Switch UI skin: sprites are reloaded; screens built from now on use it (open menus rebuild themselves).</summary>
    public static void SetSkin(int s) {
      s = Mathf.Clamp(s, 0, 2);
      if (s == Skin) return;
      Skin = s; PlayerPrefs.SetInt(KSkin, s);
      UiKit.ResetInk();
      Changed?.Invoke();
    }
    public static void Save() => PlayerPrefs.Save();

    /// <summary>Name of the nearest preset, for the label next to the slider.</summary>
    public static string InkStyleName(float v) => InkStyleNames[Mathf.Clamp(Mathf.RoundToInt(v * 2), 0, 2)];

    // The BachYeu/InkCharacter shader reads one global: -1 light .. 0 default .. +1 bold.
    static readonly int InkStyleId = Shader.PropertyToID("_InkStyle");
    static void ApplyInkStyle() => Shader.SetGlobalFloat(InkStyleId, InkStyle * 2f - 1f);

    /// <summary>Short phone buzz, if the player allows it. Throttled so a swarm does not buzz non-stop.</summary>
    public static void Buzz() {
      if (!Vibrate || Time.unscaledTime < _nextBuzz) return;
      _nextBuzz = Time.unscaledTime + .35f;
#if UNITY_IOS || UNITY_ANDROID
      Handheld.Vibrate();
#endif
    }
    static float _nextBuzz;

    // ---------- graphics ----------
    // URP's render scale and shadow distance live on the pipeline asset. The original values are restored on quit,
    // so playing in the Editor never leaves the asset changed.
    static UniversalRenderPipelineAsset _asset;
    static float _origScale, _origShadow;

    static void ApplyGraphics() {
      QualitySettings.vSyncCount = 0;
      Application.targetFrameRate = FpsIndex == 0 ? 30 : 60;
      var urp = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
      if (!urp) urp = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
      if (!urp) return;
      if (_asset != urp) { RestoreRenderAsset(); _asset = urp; _origScale = urp.renderScale; _origShadow = urp.shadowDistance; }
      urp.renderScale = Quality switch { QualityLow => .6f, QualityMid => Mathf.Min(.85f, _origScale), _ => 1f };
      urp.shadowDistance = Quality == QualityLow ? 0 : _origShadow;
    }

    static void RestoreRenderAsset() {
      if (!_asset) return;
      _asset.renderScale = _origScale;
      _asset.shadowDistance = _origShadow;
    }
  }
}
