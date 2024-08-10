using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
namespace RenderPipelineConverter {
    public class FindReadWriteMaterials : EditorWindow {
        [MenuItem("Tools/Find Read-Write Materials")]
        public static void FindMaterials() {
            string[] materialGUIDs = AssetDatabase.FindAssets("t:Material");
            foreach (string guid in materialGUIDs) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material baseMat = AssetDatabase.LoadAssetAtPath<Material>(path);
            }
        }
    }
}
#endif
