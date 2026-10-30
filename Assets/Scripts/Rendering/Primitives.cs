using System.Collections.Generic;
using UnityEngine;

namespace SokoBan.Rendering
{
    // 程序化几何 / 材质工具。Unity 无圆锥/圆台内建体，此处手动生成。
    public static class Primitives
    {
        // Standard 材质。Three.js roughness 映射为 Unity smoothness（1 - roughness）。
        public static Material Standard(Color color, float roughness = 0.5f, float metalness = 0f)
        {
            var m = new Material(Shader.Find("Standard"));
            m.color = color;
            m.SetFloat("_Glossiness", 1f - roughness);
            m.SetFloat("_Metallic", metalness);
            return m;
        }

        // 不受光照影响的纯色材质（对应 Three.js MeshBasicMaterial）。
        public static Material Basic(Color color)
        {
            var m = new Material(Shader.Find("Unlit/Color"));
            m.color = color;
            return m;
        }

        // 设置自发光（对应 Three.js emissive + emissiveIntensity）。
        public static void SetEmissive(Material m, Color color, float intensity)
        {
            if (intensity > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * intensity);
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
            }
        }

        // 
        /// 生成圆台/圆柱/圆锥网格：rTop 为上半径、rBottom 为下半径、h 为高，几何中心在原点。
        /// rTop == 0 时为圆锥；rTop == rBottom 时为圆柱。
        /// 
        public static Mesh Cylinder(float rTop, float rBottom, float h, int segments = 20)
        {
            segments = Mathf.Max(3, segments);
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            float half = h * 0.5f;
            float slope = (rBottom - rTop) / h;
            float nlen = Mathf.Sqrt(slope * slope + 1f);

            // 侧面（上环 + 下环，各 segments+1 个顶点）
            int sideStart = verts.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                var n = new Vector3(cos / nlen, slope / nlen, sin / nlen);
                verts.Add(new Vector3(cos * rTop, half, sin * rTop));
                verts.Add(new Vector3(cos * rBottom, -half, sin * rBottom));
                norms.Add(n); norms.Add(n);
                uvs.Add(new Vector2(i / (float)segments, 1f));
                uvs.Add(new Vector2(i / (float)segments, 0f));
            }
            for (int i = 0; i < segments; i++)
            {
                int t0 = sideStart + i * 2;
                int b0 = t0 + 1;
                int t1 = t0 + 2;
                int b1 = t0 + 3;
                tris.Add(t0); tris.Add(t1); tris.Add(b0);
                tris.Add(t1); tris.Add(b1); tris.Add(b0);
            }

            // 顶盖（面朝 +y）
            if (rTop > 0f)
            {
                int c = verts.Count;
                verts.Add(Vector3.up * half);
                norms.Add(Vector3.up);
                uvs.Add(new Vector2(0.5f, 0.5f));
                for (int i = 0; i <= segments; i++)
                {
                    float a = i / (float)segments * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(a) * rTop, half, Mathf.Sin(a) * rTop));
                    norms.Add(Vector3.up);
                    uvs.Add(new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f));
                }
                for (int i = 0; i < segments; i++)
                {
                    tris.Add(c); tris.Add(c + 2 + i); tris.Add(c + 1 + i);
                }
            }

            // 底盖（面朝 -y）
            if (rBottom > 0f)
            {
                int c = verts.Count;
                verts.Add(Vector3.down * half);
                norms.Add(Vector3.down);
                uvs.Add(new Vector2(0.5f, 0.5f));
                for (int i = 0; i <= segments; i++)
                {
                    float a = i / (float)segments * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(a) * rBottom, -half, Mathf.Sin(a) * rBottom));
                    norms.Add(Vector3.down);
                    uvs.Add(new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f));
                }
                for (int i = 0; i < segments; i++)
                {
                    tris.Add(c); tris.Add(c + 1 + i); tris.Add(c + 2 + i);
                }
            }

            var mesh = new Mesh();
            mesh.vertices = verts.ToArray();
            mesh.normals = norms.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
