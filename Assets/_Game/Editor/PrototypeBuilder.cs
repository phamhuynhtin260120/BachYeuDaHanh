using System.IO;
using System.Linq;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace BachYeu.EditorTools {
  /// <summary>
  /// Menu "Bách Yêu":
  /// - Dựng scene prototype: network prefabs, UI prefabs (only if missing), ArtSet (only if missing) and the scene.
  /// - Tạo lại prefab UI: overwrite HUD.prefab and Menu.prefab with the default layout.
  /// - Mở Art Set: select the asset where model prefabs are assigned.
  /// </summary>
  public static class PrototypeBuilder {
    const string Root = "Assets/_Game";
    const string PrefabDir = Root + "/Prefabs";
    const string UiPrefabDir = PrefabDir + "/UI";
    const string ArtDir = Root + "/Art";
    const string ScenePath = Root + "/Scenes/Prototype.unity";
    const string HudPath = UiPrefabDir + "/HUD.prefab";
    const string MenuPath = UiPrefabDir + "/Menu.prefab";
    const string LobbyPath = UiPrefabDir + "/Lobby.prefab";
    const string TemplateDir = Root + "/Resources/" + Gfx.TemplateDir;
    const string RoundSpritePath = ArtDir + "/UI/Round.png";
    const string ArtSetPath = Root + "/Resources/ArtSet.asset";

    [MenuItem("Bách Yêu/Dựng scene prototype")]
    public static void Build() {
      if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Dựng scene prototype",
            "Scene Prototype.unity đã có. Dựng lại sẽ ghi đè scene (mọi object bạn thêm vào scene sẽ mất).\n\nPrefab mạng, prefab UI và ArtSet đã có sẽ được giữ nguyên.",
            "Ghi đè scene", "Huỷ")) return;
      Directory.CreateDirectory(PrefabDir);
      Directory.CreateDirectory(Root + "/Scenes");

      var enemy = SaveNetworkPrefab("Enemy", go => {
        go.AddComponent<NetworkObject>();
        go.AddComponent<NetworkTransform>();
        go.AddComponent<Enemy>();
      });
      var player = SaveNetworkPrefab("Player", go => {
        go.AddComponent<NetworkObject>();
        go.AddComponent<NetworkTransform>();
        go.AddComponent<PlayerAvatar>();
      });
      var director = SaveNetworkPrefab("GameDirector", go => {
        go.AddComponent<NetworkObject>();
        go.AddComponent<GameDirector>().EnemyPrefab = enemy;
      });
      BuildUiPrefabs(overwrite: false);
      EnsureArtSet();
      EnsureGfxTemplates();

      var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

      var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
      var cam = camGo.AddComponent<Camera>();
      cam.clearFlags = CameraClearFlags.SolidColor;
      cam.backgroundColor = new Color(.925f, .894f, .839f);
      cam.fieldOfView = 40;
      cam.farClipPlane = 200;
      camGo.AddComponent<AudioListener>();
      camGo.AddComponent<CameraRig>();
      camGo.transform.position = new Vector3(0, 10.2f, -14.4f);
      camGo.transform.LookAt(new Vector3(0, 1, -5));

      var lightGo = new GameObject("Sun");
      var light = lightGo.AddComponent<Light>();
      light.type = LightType.Directional;
      light.intensity = 1.1f;
      light.color = new Color(1f, .97f, .9f);
      light.shadows = LightShadows.Soft;
      lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);

      var arena = new GameObject("Arena").AddComponent<ArenaBuilder>();
      new GameObject("Fx").AddComponent<Fx>();

      var launcher = new GameObject("GameLauncher").AddComponent<GameLauncher>();
      launcher.PlayerPrefab = player;
      launcher.DirectorPrefab = director;
      launcher.Arena = arena;
      launcher.HudPrefab = AssetDatabase.LoadAssetAtPath<Hud>(HudPath);
      launcher.MenuPrefab = AssetDatabase.LoadAssetAtPath<MenuView>(MenuPath);
      launcher.LobbyPrefab = AssetDatabase.LoadAssetAtPath<LobbyView>(LobbyPath);

      var es = new GameObject("EventSystem");
      es.AddComponent<EventSystem>();
      es.AddComponent<InputSystemUIInputModule>();

      EditorSceneManager.SaveScene(scene, ScenePath);
      var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
      scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
      EditorBuildSettings.scenes = scenes.ToArray();
      AssetDatabase.SaveAssets();
      Debug.Log("[Bách Yêu] Đã dựng xong scene prototype: " + ScenePath);
    }

    [MenuItem("Bách Yêu/Tạo lại prefab UI (ghi đè chỉnh sửa)")]
    static void RebuildUiMenu() {
      if (!EditorUtility.DisplayDialog("Tạo lại prefab UI",
            "HUD.prefab, Menu.prefab và Lobby.prefab sẽ bị ghi đè bằng bố cục mặc định (thủy mặc). Mọi chỉnh sửa bạn đã làm trên các prefab này sẽ mất.",
            "Ghi đè", "Huỷ")) return;
      BuildUiPrefabs(overwrite: true);
      WireExistingScene();
    }

    [MenuItem("Bách Yêu/Mở Art Set (thay model)")]
    static void SelectArtSet() {
      var art = EnsureArtSet();
      Selection.activeObject = art;
      EditorGUIUtility.PingObject(art);
    }

    /// <summary>Create HUD.prefab and Menu.prefab from the default code layout. Existing prefabs are kept unless overwrite is true.</summary>
    public static void BuildUiPrefabs(bool overwrite) {
      Directory.CreateDirectory(UiPrefabDir);
      EnsureInkSprites();
      var round = EnsureRoundSprite();
      UiKit.RoundOverride = round;
      try {
        if (overwrite || !File.Exists(HudPath)) {
          var canvas = UiKit.Canvas("HUD", 10);
          var hud = canvas.gameObject.AddComponent<Hud>();
          hud.BuildDefault((RectTransform)canvas.transform);
          PrefabUtility.SaveAsPrefabAsset(canvas.gameObject, HudPath);
          Object.DestroyImmediate(canvas.gameObject);
        }
        if (overwrite || !File.Exists(MenuPath)) {
          var canvas = UiKit.Canvas("Menu", 20);
          var menu = canvas.gameObject.AddComponent<MenuView>();
          menu.BuildDefault((RectTransform)canvas.transform);
          PrefabUtility.SaveAsPrefabAsset(canvas.gameObject, MenuPath);
          Object.DestroyImmediate(canvas.gameObject);
        }
        if (overwrite || !File.Exists(LobbyPath)) {
          var canvas = UiKit.Canvas("Lobby", 15);
          var lobby = canvas.gameObject.AddComponent<LobbyView>();
          lobby.BuildDefault((RectTransform)canvas.transform);
          PrefabUtility.SaveAsPrefabAsset(canvas.gameObject, LobbyPath);
          Object.DestroyImmediate(canvas.gameObject);
        }
      } finally {
        UiKit.RoundOverride = null;
      }
      AssetDatabase.SaveAssets();
    }

    /// <summary>Point the GameLauncher in Prototype.unity at the UI prefabs, without rebuilding the scene.</summary>
    public static void WireExistingScene() {
      if (!File.Exists(ScenePath)) return;
      var active = EditorSceneManager.GetActiveScene();
      var scene = active.path == ScenePath ? active : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
      var launcher = Object.FindAnyObjectByType<GameLauncher>();
      if (!launcher) return;
      launcher.HudPrefab = AssetDatabase.LoadAssetAtPath<Hud>(HudPath);
      launcher.MenuPrefab = AssetDatabase.LoadAssetAtPath<MenuView>(MenuPath);
      launcher.LobbyPrefab = AssetDatabase.LoadAssetAtPath<LobbyView>(LobbyPath);
      EditorUtility.SetDirty(launcher);
      EditorSceneManager.SaveScene(scene);
    }

    const string InkSpriteDir = Root + "/Resources/" + UiKit.InkDir;

    /// <summary>Ink-wash sprites (thủy mặc): import as UI sprites with 9-slice borders. Safe to call often.</summary>
    public static void EnsureInkSprites() {
      var borders = new System.Collections.Generic.Dictionary<string, Vector4> {
        // Vector4 = left, bottom, right, top (pixels)
        ["Brush"] = new(90, 0, 90, 0), ["BrushBar"] = new(40, 0, 40, 0), ["Lines"] = new(40, 20, 40, 20),
        ["Seal"] = new(24, 24, 24, 24), ["SealLine"] = new(24, 24, 24, 24), ["Wash"] = new(110, 110, 110, 110),
      };
      if (!Directory.Exists(InkSpriteDir)) { Debug.LogWarning("[Bách Yêu] Chưa có thư mục " + InkSpriteDir); return; }
      foreach (var file in Directory.GetFiles(InkSpriteDir, "*.png")) {
        var path = file.Replace('\\', '/');
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (!imp) continue;
        var name = Path.GetFileNameWithoutExtension(path);
        var border = borders.TryGetValue(name, out var b) ? b : Vector4.zero;
        if (imp.textureType == TextureImporterType.Sprite && imp.spriteBorder == border && !imp.mipmapEnabled) continue;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spriteBorder = border;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.maxTextureSize = 2048;
        imp.SaveAndReimport();
      }
      UiKit.ResetInk();
    }

    static Sprite EnsureRoundSprite() {
      if (!File.Exists(RoundSpritePath)) {
        Directory.CreateDirectory(Path.GetDirectoryName(RoundSpritePath));
        UiKit.RoundOverride = null;
        _ = UiKit.Round; // generates the texture
        File.WriteAllBytes(RoundSpritePath, UiKit.RoundTexture.EncodeToPNG());
        AssetDatabase.ImportAsset(RoundSpritePath);
        var imp = (TextureImporter)AssetImporter.GetAtPath(RoundSpritePath);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spriteBorder = new Vector4(60, 60, 60, 60);
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.SaveAndReimport();
      }
      return AssetDatabase.LoadAssetAtPath<Sprite>(RoundSpritePath);
    }

    static ArtSet EnsureArtSet() {
      var art = AssetDatabase.LoadAssetAtPath<ArtSet>(ArtSetPath);
      if (art) return art;
      Directory.CreateDirectory(Path.GetDirectoryName(ArtSetPath));
      art = ScriptableObject.CreateInstance<ArtSet>();
      AssetDatabase.CreateAsset(art, ArtSetPath);
      AssetDatabase.SaveAssets();
      return art;
    }

    /// <summary>Material templates that keep the URP shaders and keyword variants in player builds (see Gfx).</summary>
    public static void EnsureGfxTemplates() {
      Directory.CreateDirectory(TemplateDir);
      void Make(string name, string shader, System.Action<Material> setup) {
        var path = $"{TemplateDir}/{name}.mat";
        if (File.Exists(path)) return;
        var m = new Material(Shader.Find(shader));
        setup?.Invoke(m);
        AssetDatabase.CreateAsset(m, path);
      }
      Make("Lit", "Universal Render Pipeline/Simple Lit", m => m.SetFloat("_Smoothness", 0f));
      Make("LitEmissive", "Universal Render Pipeline/Simple Lit", m => {
        m.SetFloat("_Smoothness", 0f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", Color.black);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
      });
      Make("Unlit", "Universal Render Pipeline/Unlit", null);
      Make("Fade", "Universal Render Pipeline/Unlit", Gfx.MakeFade);
      AssetDatabase.SaveAssets();
    }

    /// <summary>Creates a network prefab only if it does not exist yet, so your edits to it are never overwritten.</summary>
    static NetworkObject SaveNetworkPrefab(string name, System.Action<GameObject> setup) {
      var path = $"{PrefabDir}/{name}.prefab";
      var existing = AssetDatabase.LoadAssetAtPath<NetworkObject>(path);
      if (existing) return existing;
      var go = new GameObject(name);
      setup(go);
      var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
      Object.DestroyImmediate(go);
      return prefab.GetComponent<NetworkObject>();
    }
  }
}
