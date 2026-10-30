using UnityEngine;

namespace SokoBan.Rendering
{
    // 相机自适应：给定内容包围盒
    internal sealed class BoardCameraRig
    {
        private readonly Camera _cam;
        private float _theta = Mathf.PI / 6f;   // 俯视约 30°
        private float _phi = Mathf.PI / 2f;     // 从 +z 正视
        private float _camRadius = 8f;
        private Vector3 _target = new Vector3(0, 0.2f, 0);

        public BoardCameraRig(Camera cam) { _cam = cam; }

        public void Refit(Bounds box)
        {
            _target = box.center;
            var corners = Corners(box);

            bool Fits(float r)
            {
                SetPose(r);
                for (int j = 0; j < corners.Length; j++)
                {
                    Vector3 v = _cam.WorldToViewportPoint(corners[j]);
                    if (v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f || v.z < 0f) return false;
                }
                return true;
            }

            float lo = 3f, hi = 70f;
            if (!Fits(hi)) { SetPose(hi); return; }
            if (Fits(lo)) { SetPose(lo); return; }
            for (int k = 0; k < 48; k++)
            {
                float mid = (lo + hi) / 2f;
                if (Fits(mid)) hi = mid; else lo = mid;
            }
            SetPose(hi * 1.05f);
        }

        private void SetPose(float r)
        {
            _camRadius = Mathf.Clamp(r, 3f, 70f);
            FrameCamera();
        }

        private void FrameCamera()
        {
            float r = _camRadius;
            _cam.transform.position = new Vector3(
                _target.x + r * Mathf.Sin(_theta) * Mathf.Cos(_phi),
                _target.y + r * Mathf.Cos(_theta),
                _target.z + r * Mathf.Sin(_theta) * Mathf.Sin(_phi));
            _cam.transform.LookAt(_target);
        }

        private static Vector3[] Corners(Bounds b)
        {
            return new Vector3[]
            {
                new Vector3(b.min.x, b.min.y, b.min.z),
                new Vector3(b.max.x, b.min.y, b.min.z),
                new Vector3(b.min.x, b.max.y, b.min.z),
                new Vector3(b.max.x, b.max.y, b.min.z),
                new Vector3(b.min.x, b.min.y, b.max.z),
                new Vector3(b.max.x, b.min.y, b.max.z),
                new Vector3(b.min.x, b.max.y, b.max.z),
                new Vector3(b.max.x, b.max.y, b.max.z),
            };
        }
    }
}
