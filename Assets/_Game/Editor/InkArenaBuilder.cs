using System.IO;
using UnityEditor;
using UnityEngine;

namespace BachYeu.EditorTools {
  /// <summary>
  /// Builds the ink-painting arena prefab from the Chinese Painting URP pack and plugs it into ArtSet.Environment.
  /// Menu: Bách Yêu/Dựng sân thủy mặc. Re-run after changing the layout below; it overwrites ArenaInk.prefab.
  /// Rules the game relies on: walkable floor at y = 0, play circle radius 12 (ArenaBuilder.PlayRadius) kept clear.
  /// A child named "ContestOnly" is shown only in Tranh Đăng.
  /// </summary>
  public static class InkArenaBuilder {
    const string PackDir = "Assets/_Game/Art/Enviroment/Chinese Painting/Chinese Painting URP/Asset/Prefab/";
    const string OutDir = "Assets/_Game/Prefabs/Env";
    const string OutPath = OutDir + "/ArenaInk.prefab";
    const string ArtSetPath = "Assets/_Game/Resources/ArtSet.asset";

    // Turned off: the arena is now designed by hand in ArenaInk.prefab, and running this would overwrite it.
    // To use it again, restore the line below: [MenuItem("Bách Yêu/Dựng sân thủy mặc")]
    public static void BuildMenu() {
      var path = Build();
      EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(path));
    }

    public static string Build() {
      Directory.CreateDirectory(OutDir);
      var root = new GameObject("ArenaInk");
      try {
        // Floor: the cloud-pattern stone stage. Its walk surface sits at 3.09 in pack units.
        const float stageScale = .6f;
        Place(root.transform, "SM_Stage_01", new Vector3(0, -3.09f * stageScale, 0), stageScale, 0, byBounds: false);
        // Floating island under the stage.
        Place(root.transform, "SM_Rock_Blue_01", new Vector3(0, -1.6f, 0), .6f, 0, top: true);

        // Back: cliffs, a pavilion and a waterfall, framing the far (+Z) side.
        // Pushed low and far so it never covers the top of the screen when you stand at the back edge.
        Place(root.transform, "SM_Rock_Green_08", new Vector3(0, -6.5f, 28), .9f, 90);
        Place(root.transform, "SM_Build_02", new Vector3(0, -4.3f, 29), .42f, 180);
        Place(root.transform, "SM_Rock_Green_07", new Vector3(-17, -4, 24), .7f, 30);
        Place(root.transform, "SM_Rock_Green_15", new Vector3(17, -4, 23), .7f, -20);
        // No waterfall: its shader samples the opaque texture, which the game's render settings keep off (cheaper on phones).
        Place(root.transform, "SM_Rock_Blue_03", new Vector3(-9, -6, 29), .8f, 0);
        Place(root.transform, "SM_Rock_Blue_04", new Vector3(10, -6, 31), .9f, 40);
        Place(root.transform, "SM_Rock_Green_05", new Vector3(-26, -8, 12), .8f, 70);
        Place(root.transform, "SM_Rock_Green_06", new Vector3(27, -10, 10), .7f, -60);

        // Pines in the stage corners, outside the play circle.
        Place(root.transform, "SM_Tree_03", new Vector3(-11.5f, 0, 11), .36f, 20);
        Place(root.transform, "SM_Tree_01", new Vector3(12, 0, 10.5f), .32f, 200);
        Place(root.transform, "SM_Tree_03", new Vector3(-12.5f, 0, -11), .3f, 140);
        Place(root.transform, "SM_Tree_02", new Vector3(12.5f, 0, -11.5f), .24f, 250);

        // Small dressing on the rim (radius > 12): rocks, stone lamps, plants.
        Place(root.transform, "SM_Rock_Green_16", new Vector3(-13.2f, 0, 3), .35f, 30);
        Place(root.transform, "SM_Rock_Blue_08", new Vector3(13.2f, 0, 2), .45f, 110);
        Place(root.transform, "SM_Plant_03", new Vector3(-12.6f, 0, -4), .7f, 0);
        Place(root.transform, "SM_Plant_02", new Vector3(12.6f, 0, -5), .7f, 0);
        Place(root.transform, "SM_Grass_01", new Vector3(-12.8f, 0, 7.5f), .8f, 0);
        Place(root.transform, "SM_Grass_01", new Vector3(12.8f, 0, 6.5f), .8f, 90);

        // Tranh Đăng only: an arched stone bridge over the river at Đèn Cầu, sunk so its crown is barely above the floor.
        var contest = new GameObject("ContestOnly").transform;
        contest.SetParent(root.transform, false);
        const float bridgeScale = .55f;
        Place(contest, "SM_Bridge_03", new Vector3(Contest.Lanterns[2].x, -2.0f * bridgeScale + .12f, Contest.Lanterns[2].z), bridgeScale, 0, byBounds: false);

        foreach (var c in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        PrefabUtility.SaveAsPrefabAsset(root, OutPath);
      } finally {
        Object.DestroyImmediate(root);
      }

      var art = AssetDatabase.LoadAssetAtPath<ArtSet>(ArtSetPath);
      if (art) {
        art.Environment = AssetDatabase.LoadAssetAtPath<GameObject>(OutPath);
        EditorUtility.SetDirty(art);
      }
      AssetDatabase.SaveAssets();
      Debug.Log("[Bách Yêu] Đã dựng sân thủy mặc: " + OutPath);
      return OutPath;
    }

    /// <summary>
    /// Place a pack prefab. byBounds: put the bottom-center of its bounds at pos (or the top-center with top = true),
    /// because the pack's pivots are not at the base. Otherwise pos is the pivot.
    /// </summary>
    static GameObject Place(Transform parent, string name, Vector3 pos, float scale, float yaw, bool byBounds = true, bool top = false) {
      var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackDir + name + ".prefab");
      if (!prefab) { Debug.LogWarning("[Bách Yêu] Thiếu prefab " + name); return null; }
      var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
      go.transform.localScale = Vector3.one * scale;
      go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
      go.transform.localPosition = pos;
      if (byBounds) {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0) {
          var b = rs[0].bounds;
          foreach (var r in rs) b.Encapsulate(r.bounds);
          var anchor = new Vector3(b.center.x, top ? b.max.y : b.min.y, b.center.z);
          go.transform.position += parent.TransformPoint(pos) - anchor;
        }
      }
      return go;
    }
  }
}
