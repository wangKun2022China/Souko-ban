using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SokoBan.App;

namespace SokoBan.Rendering
{
    public static class SokobanSceneBuilder
    {
        [MenuItem("Sokoban/Build Preview Scene")]
        public static void BuildPreviewScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Sokoban");
            go.AddComponent<GameController>();

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Main.unity");
            Debug.Log("Built preview scene: Assets/Scenes/Main.unity");
        }
    }
}
