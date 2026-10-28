using ElecPet.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ElecPet.Tests
{
    public sealed class DragonAttachmentTests
    {
        [Test]
        public void MouthLineRemainsAttachedToTheSculptedFace()
        {
            var pet = new PrototypePet();
            try
            {
                var mouth = FindPart(pet, "嘴线");
                var face = FindPart(pet, "宽扁头部");
                // 检查真实三角面距离，连续短吻改形后嘴线也必须仍贴着脸部。
                float distance = MaximumSurfaceDistance(mouth, face, false);
                Assert.That(distance, Is.LessThan(.015f), "嘴线不能作为悬空黑弧漂浮在脸前");
            }
            finally { Object.DestroyImmediate(pet.Root.gameObject); }
        }

        [TestCase("翼指")]
        [TestCase("内翼指")]
        public void WingFingersFollowTheCurvedMembrane(string partName)
        {
            var pet = new PrototypePet();
            try
            {
                int checkedParts = 0;
                foreach (var finger in pet.Root.GetComponentsInChildren<MeshFilter>())
                {
                    if (finger.name != partName) continue;
                    var wing = finger.transform.parent.parent;
                    var membrane = wing.Find("蝠翼翼膜").GetComponent<MeshFilter>();
                    // 骨骼有截面厚度，允许轻微凸起；细小指尖必须接触翼膜，不能延伸成悬骨。
                    Assert.That(MaximumSurfaceDistance(finger, membrane, false), Is.LessThan(.03f), partName + "应沿着曲面翼膜生长");
                    Assert.That(MaximumSurfaceDistance(finger, membrane, true), Is.LessThan(.015f), partName + "尖端应接触翼膜");
                    checkedParts++;
                }
                Assert.That(checkedParts, Is.EqualTo(2), "左右两侧翼指都需要检查");
            }
            finally { Object.DestroyImmediate(pet.Root.gameObject); }
        }

        private static MeshFilter FindPart(PrototypePet pet, string name)
        {
            foreach (var part in pet.Root.GetComponentsInChildren<MeshFilter>())
                if (part.name == name) return part;
            Assert.Fail("缺少模型部位：" + name);
            return null;
        }

        private static float MaximumSurfaceDistance(MeshFilter source, MeshFilter surface, bool onlyTip)
        {
            // 统一到目标部位坐标，距离采用模型尺寸，避免根节点桌面缩放掩盖接缝。
            var points = source.sharedMesh.vertices;
            float lowest = float.PositiveInfinity;
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = surface.transform.InverseTransformPoint(source.transform.TransformPoint(points[i]));
                lowest = Mathf.Min(lowest, points[i].y);
            }
            var vertices = surface.sharedMesh.vertices;
            var indices = surface.sharedMesh.triangles;
            float maximum = 0;
            foreach (var point in points)
            {
                if (onlyTip && point.y > lowest + .01f) continue;
                float nearest = float.PositiveInfinity;
                for (int i = 0; i < indices.Length; i += 3)
                    nearest = Mathf.Min(nearest, TriangleDistanceSquared(point, vertices[indices[i]], vertices[indices[i + 1]], vertices[indices[i + 2]]));
                maximum = Mathf.Max(maximum, nearest);
            }
            return Mathf.Sqrt(maximum);
        }

        private static float TriangleDistanceSquared(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, offset = point - a;
            float aa = Vector3.Dot(ab, ab), bb = Vector3.Dot(ac, ac), abac = Vector3.Dot(ab, ac);
            float denominator = aa * bb - abac * abac;
            if (denominator > 1e-16f)
            {
                float alongAB = (Vector3.Dot(offset, ab) * bb - Vector3.Dot(offset, ac) * abac) / denominator;
                float alongAC = (Vector3.Dot(offset, ac) * aa - Vector3.Dot(offset, ab) * abac) / denominator;
                if (alongAB >= 0 && alongAC >= 0 && alongAB + alongAC <= 1)
                    return (offset - alongAB * ab - alongAC * ac).sqrMagnitude;
            }
            return Mathf.Min(SegmentDistanceSquared(point, a, b),
                Mathf.Min(SegmentDistanceSquared(point, b, c), SegmentDistanceSquared(point, c, a)));
        }

        private static float SegmentDistanceSquared(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 edge = b - a;
            float along = edge.sqrMagnitude == 0 ? 0 : Mathf.Clamp01(Vector3.Dot(point - a, edge) / edge.sqrMagnitude);
            return (point - a - along * edge).sqrMagnitude;
        }
    }
}
