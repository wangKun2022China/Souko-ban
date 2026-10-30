using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SokoBan.Rendering
{
    // 
    /// 一键生成运行时资源：调色板、玩家预制体及其网格/材质。
    /// 菜单 SokoBan/生成资源。幂等：已存在的资产复用/覆盖，缺则创建。
    /// 
    public static class SokobanAssetGenerator
    {
        private const string Root = "Assets/Resources/SokoBan";
        private const string MaterialsDir = Root + "/Materials";
        private const string MeshesDir = Root + "/Meshes";

        [MenuItem("SokoBan/生成资源")]
        public static void GenerateAll()
        {
            EnsureFolder("Assets/Resources", "SokoBan");
            EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Meshes");

            GeneratePalette();
            GeneratePlayer();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SokoBan] 资源生成完成：" + Root);
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static void GeneratePalette()
        {
            var path = Root + "/GamePalette.asset";
            if (AssetDatabase.LoadAssetAtPath<GamePalette>(path) == null)
            {
                var palette = ScriptableObject.CreateInstance<GamePalette>();
                AssetDatabase.CreateAsset(palette, path);
            }
        }

        private static void GeneratePlayer()
        {
            var palette = AssetDatabase.LoadAssetAtPath<GamePalette>(Root + "/GamePalette.asset");
            if (palette == null) palette = ScriptableObject.CreateInstance<GamePalette>();

            var orange = GetMaterial("Player_Orange", Shader.Find("Standard"), palette.orange, 0.55f);
            var cream = GetMaterial("Player_Cream", Shader.Find("Standard"), palette.cream, 0.6f);
            var white = GetMaterial("Player_White", Shader.Find("Standard"), Color.white, 0.5f);
            var dark = GetMaterial("Player_Dark", Shader.Find("Unlit/Color"), palette.dark, 0f);

            var feet = GetMesh("Player_Feet", Primitives.Cylinder(0.2f, 0.24f, 0.07f, 20));
            var torso = GetMesh("Player_Torso", Primitives.Cylinder(0.15f, 0.19f, 0.3f, 20));
            var scarf = GetMesh("Player_Scarf", Primitives.Cylinder(0.155f, 0.155f, 0.05f, 20));
            var hat = GetMesh("Player_Hat", Primitives.Cylinder(0f, 0.13f, 0.22f, 20));
            var brim = GetMesh("Player_Brim", Primitives.Cylinder(0.14f, 0.14f, 0.02f, 20));
            var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");

            var holder = new GameObject("Player");
            var body = new GameObject("Body");
            body.transform.SetParent(holder.transform, false);

            AddPart("Feet", body.transform, feet, orange, new Vector3(0f, 0.035f, 0f), Vector3.one, true);
            AddPart("Torso", body.transform, torso, cream, new Vector3(0f, 0.22f, 0f), Vector3.one, true);
            AddPart("Scarf", body.transform, scarf, orange, new Vector3(0f, 0.37f, 0f), Vector3.one, false);
            AddPart("Head", body.transform, sphere, white, new Vector3(0f, 0.45f, 0f), new Vector3(0.24f, 0.24f, 0.24f), true);
            AddPart("Hat", body.transform, hat, white, new Vector3(0f, 0.6f, 0f), Vector3.one, true);
            AddPart("Brim", body.transform, brim, white, new Vector3(0f, 0.52f, 0f), Vector3.one, false);
            AddPart("EyeL", body.transform, sphere, dark, new Vector3(-0.045f, 0.46f, 0.11f), new Vector3(0.036f, 0.036f, 0.036f), false);
            AddPart("EyeR", body.transform, sphere, dark, new Vector3(0.045f, 0.46f, 0.11f), new Vector3(0.036f, 0.036f, 0.036f), false);

            PrefabUtility.SaveAsPrefabAsset(holder, Root + "/Player.prefab");
            Object.DestroyImmediate(holder);
        }

        private static Material GetMaterial(string name, Shader shader, Color color, float roughness)
        {
            var path = MaterialsDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader;
            mat.color = color;
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 1f - roughness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Mesh GetMesh(string name, Mesh mesh)
        {
            var path = MeshesDir + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static void AddPart(string name, Transform parent, Mesh mesh, Material mat, Vector3 localPos, Vector3 localScale, bool castShadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            if (castShadows) mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
        }
    }
}
