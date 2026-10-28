using System.Collections.Generic;
using System.Reflection;
using ElecPet.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ElecPet.Tests
{
    public sealed class DragonMeshQualityTests
    {
        private readonly List<Mesh> meshes = new List<Mesh>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
            meshes.Clear();
        }

        [Test]
        public void BodyAndCurvesProvideFiniteNormalsAndTextureCoordinates()
        {
            AssertSurface(Create("Body", 0f));
            AssertSurface(Create("Curve", new[] { Vector3.zero, new Vector3(.02f, .08f, .3f), new Vector3(0, .18f, .6f) },
                new[] { .12f, .08f, .025f }));
        }

        [Test]
        public void CurvesCloseVisibleEndsAndRemainContinuousNearForwardTangents()
        {
            var mesh = Create("Curve", new[] { Vector3.zero, new Vector3(0, .08f, .3f), new Vector3(.02f, 0, .6f) },
                new[] { .12f, .08f, .025f });
            var welded = new Dictionary<Vector3Int, int>();
            var edges = new Dictionary<long, int>();
            var vertices = mesh.vertices;
            var indices = new int[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                var key = Vector3Int.RoundToInt(vertices[i] * 100000);
                if (!welded.TryGetValue(key, out int index))
                {
                    index = welded.Count;
                    welded.Add(key, index);
                }
                indices[i] = index;
            }
            var triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
                for (int side = 0; side < 3; side++)
                {
                    int a = indices[triangles[i + side]], b = indices[triangles[i + (side + 1) % 3]];
                    long edge = ((long)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
                    edges.TryGetValue(edge, out int count);
                    edges[edge] = count + 1;
                }
            foreach (int count in edges.Values)
                Assert.That(count, Is.EqualTo(2), "封口后每条几何边应恰好连接两个三角面");
            AssertSurface(mesh);
        }

        [Test]
        public void WingPreservesOpenScallopsAndSubdividesTheCurvedMembrane()
        {
            var mesh = Create("Wing", 1);
            Assert.That(Contains(mesh, new Vector2(.44f, .42f)), Is.True, "翼骨之间有完整翼膜");
            Assert.That(Contains(mesh, new Vector2(.69f, .30f)), Is.False, "外侧扇贝凹口保持透空");
            Assert.That(Contains(mesh, new Vector2(.42f, .12f)), Is.False, "内侧扇贝凹口保持透空");
            bool interior = false;
            foreach (var vertex in mesh.vertices)
                interior |= vertex.x > .35f && vertex.x < .50f && vertex.y > .35f && vertex.y < .50f;
            Assert.That(interior, Is.True, "翼膜内部需要曲面采样，不能只连接轮廓顶点");
            AssertSurface(mesh);
        }

        [TestCase("Wing")]
        [TestCase("TailFin")]
        [TestCase("EarFin")]
        public void PairedFinsMirrorGeometryAndKeepOutwardNormals(string method)
        {
            var left = Create(method, -1);
            var right = Create(method, 1);
            Assert.That(left.vertexCount, Is.EqualTo(right.vertexCount));
            var leftVertices = left.vertices;
            var rightVertices = right.vertices;
            var leftNormals = left.normals;
            var rightNormals = right.normals;
            for (int i = 0; i < leftVertices.Length; i++)
            {
                Assert.That(Vector3.Distance(leftVertices[i], Vector3.Scale(rightVertices[i], new Vector3(-1, 1, 1))), Is.LessThan(.0001f));
                Assert.That(Vector3.Distance(leftNormals[i], Vector3.Scale(rightNormals[i], new Vector3(-1, 1, 1))), Is.LessThan(.001f));
            }
            AssertSurface(left);
            AssertSurface(right);
        }

        [Test]
        public void SculptedHeadAndTorsoFitExistingAnimationAndCarrySpace()
        {
            var head = Create("Head");
            Assert.That(head.bounds.min.x, Is.InRange(-.58f, -.53f));
            Assert.That(head.bounds.max.x, Is.InRange(.53f, .58f));
            Assert.That(head.bounds.min.z, Is.InRange(-.51f, -.46f), "连续短吻保留叼持位置");
            Assert.That(head.bounds.max.y, Is.InRange(.26f, .31f));
            AssertSurface(head);
            var torso = Create("Torso");
            foreach (var vertex in torso.vertices)
            {
                Assert.That(Mathf.Abs(vertex.x), Is.LessThanOrEqualTo(.501f));
                Assert.That(Mathf.Abs(vertex.y), Is.LessThanOrEqualTo(.501f));
                Assert.That(Mathf.Abs(vertex.z), Is.LessThanOrEqualTo(.501f));
            }
            AssertSurface(torso);
        }

        private Mesh Create(string method, params object[] arguments)
        {
            var type = typeof(PrototypePet).Assembly.GetType("ElecPet.Runtime.DragonMesh");
            var factory = type.GetMethod(method, BindingFlags.Public | BindingFlags.Static);
            Assert.That(factory, Is.Not.Null, method + " 应提供对应的专用曲面");
            var mesh = (Mesh)factory.Invoke(null, arguments);
            meshes.Add(mesh);
            return mesh;
        }

        private static void AssertSurface(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var uv = mesh.uv;
            Assert.That(uv.Length, Is.EqualTo(vertices.Length), mesh.name + " 需要连续纹理坐标");
            for (int i = 0; i < vertices.Length; i++)
            {
                Assert.That(float.IsNaN(vertices[i].sqrMagnitude) || float.IsInfinity(vertices[i].sqrMagnitude), Is.False);
                Assert.That(normals[i].magnitude, Is.EqualTo(1).Within(.001f), mesh.name + " 法线不能退化");
                Assert.That(uv[i].x, Is.InRange(0f, 1f));
                Assert.That(uv[i].y, Is.InRange(0f, 1f));
            }
            var triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var area = Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]);
                Assert.That(area.sqrMagnitude, Is.GreaterThan(1e-16f), mesh.name + " 不应含零面积三角面");
            }
        }

        private static bool Contains(Mesh mesh, Vector2 point)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector2 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                float ab = Cross(b - a, point - a), bc = Cross(c - b, point - b), ca = Cross(a - c, point - c);
                if ((ab >= 0 && bc >= 0 && ca >= 0) || (ab <= 0 && bc <= 0 && ca <= 0)) return true;
            }
            return false;
        }

        private static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }
    }
}
