using System;
using System.Collections.Generic;
using UnityEngine;

namespace ElecPet.Runtime
{
    // 大轮廓使用连续曲面，薄膜保留透空凹口；同一部位的接缝共享法线，避免拼装感。
    internal static class DragonMesh
    {
        public static Mesh Body(float pear)
        {
            return Rounded("幼龙圆润曲面", p => new Vector3(p.x * (1 - pear * p.y), p.y, p.z * (1 - pear * p.y)) * .5f);
        }

        public static Mesh Iris()
        {
            // 虹膜只保留朝外的眼面，背面由皮肤色眼壳承接；沿用整球法线，赤道不添加绿色封口。
            var mesh = Body(0);
            mesh.name = "幼龙前侧虹膜";
            var vertices = mesh.vertices;
            var source = mesh.triangles;
            var front = new List<int>();
            for (int i = 0; i < source.Length; i += 3)
                if (vertices[source[i]].z <= .000001f && vertices[source[i + 1]].z <= .000001f && vertices[source[i + 2]].z <= .000001f)
                    front.AddRange(new[] { source[i], source[i + 1], source[i + 2] });
            mesh.SetTriangles(front, 0);
            return mesh;
        }

        public static Mesh Head()
        {
            // 短吻从下半脸连续前伸，保留上方面颊的眼球空间，不再叠加独立吻块。
            return Rounded("幼龙连续头吻", p =>
            {
                float y = p.y * .295f;
                float x = p.x * .56f * (1 - .09f * Mathf.Clamp01((-y - .08f) / .215f));
                float muzzle = Mathf.Exp(-Mathf.Pow((y + .12f) / .135f, 2) - Mathf.Pow(x / .37f, 4));
                float z = p.z * .36f - .175f * muzzle * Mathf.Pow(Mathf.Max(0, -p.z), 3);
                return new Vector3(x, y, z);
            });
        }

        public static Mesh HeadSection(Mesh source, bool lower)
        {
            // 沿原有微笑嘴线裁开同一头吻曲面；进食时上下颌真正分离，闭嘴时仍使用原始完整网格。
            var points = source.vertices;
            var sourceNormals = source.normals;
            var sourceUv = source.uv;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            var polygon = new List<int>(4);
            var indices = source.triangles;
            for (int triangle = 0; triangle < indices.Length; triangle += 3)
            {
                polygon.Clear();
                for (int side = 0; side < 3; side++)
                {
                    int a = indices[triangle + side], b = indices[triangle + (side + 1) % 3];
                    float da = points[a].y + .20f - .12f * Mathf.Pow(points[a].x / .56f, 2);
                    float db = points[b].y + .20f - .12f * Mathf.Pow(points[b].x / .56f, 2);
                    bool inside = lower ? da <= 0 : da >= 0;
                    if (inside)
                    {
                        polygon.Add(vertices.Count); vertices.Add(points[a]);
                        normals.Add(sourceNormals[a]); uv.Add(sourceUv[a]);
                    }
                    if (inside == (lower ? db <= 0 : db >= 0)) continue;
                    float t = da / (da - db);
                    polygon.Add(vertices.Count); vertices.Add(Vector3.Lerp(points[a], points[b], t));
                    normals.Add(Vector3.Lerp(sourceNormals[a], sourceNormals[b], t).normalized);
                    uv.Add(Vector2.Lerp(sourceUv[a], sourceUv[b], t));
                }
                for (int i = 1; i < polygon.Count - 1; i++)
                    triangles.AddRange(new[] { polygon[0], polygon[i], polygon[i + 1] });
            }
            var mesh = new Mesh { name = lower ? "进食下颌曲面" : "进食上颌曲面" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            return mesh;
        }

        public static Mesh MouthLining(Mesh source)
        {
            // 以同一条裁切边构造可弯曲内壁，避免球形口腔从下颌外凸或在嘴角漏出背景。
            var points = source.vertices;
            var indices = source.triangles;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var edge = new List<Vector3>(2);
            for (int triangle = 0; triangle < indices.Length; triangle += 3)
            {
                edge.Clear();
                for (int side = 0; side < 3; side++)
                {
                    Vector3 a = points[indices[triangle + side]], b = points[indices[triangle + (side + 1) % 3]];
                    float da = a.y + .20f - .12f * Mathf.Pow(a.x / .56f, 2);
                    float db = b.y + .20f - .12f * Mathf.Pow(b.x / .56f, 2);
                    if ((da < 0) != (db < 0)) edge.Add(Vector3.Lerp(a, b, da / (da - db)));
                }
                if (edge.Count != 2) continue;
                int start = vertices.Count;
                for (int face = 0; face < 2; face++)
                for (int row = 0; row < 3; row++) { vertices.Add(edge[0]); vertices.Add(edge[1]); }
                for (int row = 0; row < 2; row++)
                {
                    int a = start + row * 2;
                    triangles.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2,
                        a + 6, a + 8, a + 7, a + 7, a + 8, a + 9 });
                }
            }
            var mesh = new Mesh { name = "进食凹入口腔内壁" };
            mesh.MarkDynamic(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            return mesh;
        }

        public static Mesh Torso()
        {
            // 肩胸与后胯保留体积，腰部轻收；归一化尺寸让现有四足支撑与安全边距继续生效。
            return Rounded("幼龙肩胸腰胯", p =>
            {
                float z = p.z * .5f;
                float chest = Mathf.Exp(-Mathf.Pow((z + .24f) / .18f, 2));
                float haunch = Mathf.Exp(-Mathf.Pow((z - .29f) / .18f, 2));
                float waist = Mathf.Exp(-Mathf.Pow(z / .19f, 2));
                float x = p.x * .5f * (.83f + .15f * chest + .11f * haunch - .04f * waist);
                float y = p.y * .5f * (.85f + .10f * chest + .09f * haunch) + .035f * Mathf.Pow(Mathf.Max(0, -p.y), 4);
                return new Vector3(x, y, z);
            });
        }

        public static Mesh EarFin(int sign)
        {
            // 扁平叶形耳鳍在头内收拢根部，向上后方舒展，避免圆截面长角的观感。
            return Rounded("幼龙柔和鳍耳", p =>
            {
                float t = (p.y + 1) * .5f;
                return new Vector3(sign * (.13f * t + p.x * (.065f + .025f * Mathf.Sin(t * Mathf.PI))),
                    .39f * t, .17f * t * t + p.z * .038f * (1 - .5f * t));
            }, sign);
        }

        private static Mesh Rounded(string name, Func<Vector3, Vector3> shape, int sign = 1)
        {
            const int rings = 24, sides = 40;
            var vertices = new List<Vector3> { shape(Vector3.up) };
            var uv = new List<Vector2> { new Vector2(.5f, 1) };
            var triangles = new List<int>();
            for (int r = 1; r < rings; r++)
            {
                float latitude = Mathf.PI * r / rings;
                for (int s = 0; s <= sides; s++)
                {
                    float longitude = Mathf.PI * 2 * s / sides;
                    vertices.Add(shape(new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude))));
                    uv.Add(new Vector2(s / (float)sides, 1 - r / (float)rings));
                }
            }
            int bottom = vertices.Count;
            vertices.Add(shape(Vector3.down));
            uv.Add(new Vector2(.5f, 0));
            for (int s = 0; s < sides; s++)
            {
                triangles.AddRange(new[] { 0, s + 2, s + 1 });
                for (int r = 0; r < rings - 2; r++)
                {
                    int a = 1 + r * (sides + 1) + s, b = a + sides + 1;
                    triangles.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
                int end = 1 + (rings - 2) * (sides + 1) + s;
                triangles.AddRange(new[] { end, end + 1, bottom });
            }
            MirrorWinding(triangles, sign);
            var mesh = Finish(vertices, triangles, uv, name);
            SmoothSeams(mesh, 1, rings - 1, sides);
            return mesh;
        }

        public static Mesh Curve(Vector3[] centers, float[] radii)
        {
            var sampled = new List<Vector3>();
            var widths = new List<float>();
            for (int i = 0; i < centers.Length - 1; i++)
            {
                Vector3 p0 = centers[Mathf.Max(i - 1, 0)], p1 = centers[i];
                Vector3 p2 = centers[i + 1], p3 = centers[Mathf.Min(i + 2, centers.Length - 1)];
                for (int j = 0; j < 6; j++)
                {
                    float t = j / 6f;
                    sampled.Add(Catmull(p0, p1, p2, p3, t));
                    widths.Add(Mathf.Lerp(radii[i], radii[i + 1], t));
                }
            }
            sampled.Add(centers[centers.Length - 1]);
            widths.Add(radii[radii.Length - 1]);
            centers = sampled.ToArray();
            radii = widths.ToArray();
            const int sides = 20;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uv = new List<Vector2>();
            var distances = new float[centers.Length];
            for (int r = 1; r < centers.Length; r++)
                distances[r] = distances[r - 1] + Vector3.Distance(centers[r - 1], centers[r]);
            Vector3 previous = (centers[1] - centers[0]).normalized;
            Vector3 normal = Vector3.Cross(previous, Vector3.forward).normalized;
            if (normal.sqrMagnitude < .01f) normal = Vector3.Cross(previous, Vector3.up).normalized;
            for (int r = 0; r < centers.Length; r++)
            {
                Vector3 tangent = (centers[Mathf.Min(r + 1, centers.Length - 1)] - centers[Mathf.Max(0, r - 1)]).normalized;
                // 截面沿曲线平行搬运，切线穿过前向时也不会突然翻转。
                normal = Vector3.ProjectOnPlane(Quaternion.FromToRotation(previous, tangent) * normal, tangent).normalized;
                Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
                previous = tangent;
                for (int s = 0; s <= sides; s++)
                {
                    float angle = 2 * Mathf.PI * s / sides;
                    vertices.Add(centers[r] + radii[r] * (normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle)));
                    uv.Add(new Vector2(s / (float)sides, distances[r] / distances[distances.Length - 1]));
                    if (r == centers.Length - 1 || s == sides) continue;
                    int a = r * (sides + 1) + s, b = a + sides + 1;
                    triangles.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
            }
            // 尾尖、嘴线和翼骨有非零端半径；单独封口避免露出空管，同时保留侧面柔和法线。
            for (int end = 0; end < 2; end++)
            {
                int ring = end == 0 ? 0 : (centers.Length - 1) * (sides + 1);
                int cap = vertices.Count;
                vertices.Add(centers[end == 0 ? 0 : centers.Length - 1]);
                uv.Add(new Vector2(.5f, .5f));
                for (int s = 0; s <= sides; s++)
                {
                    vertices.Add(vertices[ring + s]);
                    float angle = 2 * Mathf.PI * s / sides;
                    uv.Add(new Vector2(.5f + .5f * Mathf.Cos(angle), .5f + .5f * Mathf.Sin(angle)));
                    if (s < sides) triangles.AddRange(end == 0 ? new[] { cap, cap + s + 2, cap + s + 1 } : new[] { cap, cap + s + 1, cap + s + 2 });
                }
            }
            var mesh = Finish(vertices, triangles, uv, "幼龙封闭弯曲曲面");
            SmoothSeams(mesh, 0, centers.Length, sides);
            return mesh;
        }

        public static Mesh Wing(int sign)
        {
            Vector3[] outline = {
                Vector3.zero, new Vector3(.23f, .46f, 0), new Vector3(.62f, .71f, 0), new Vector3(.83f, .38f, 0),
                new Vector3(.70f, .40f, 0), new Vector3(.58f, .34f, 0), new Vector3(.55f, .14f, 0),
                new Vector3(.43f, .25f, 0), new Vector3(.29f, .22f, 0), new Vector3(.22f, -.06f, 0)
            };
            return Membrane(outline, sign, .11f, .65f, "幼龙弧面蝠翼");
        }

        public static Mesh TailFin(int sign)
        {
            Vector3[] outline = {
                Vector3.zero, new Vector3(.18f, .32f, 0), new Vector3(.42f, .60f, 0), new Vector3(.68f, .69f, 0),
                new Vector3(.78f, .60f, 0), new Vector3(.72f, .42f, 0), new Vector3(.61f, .21f, 0),
                new Vector3(.40f, .06f, 0), new Vector3(.22f, -.07f, 0)
            };
            return Membrane(outline, sign, 0, .20f, "幼龙圆润扇形尾鳍");
        }

        private static Mesh Membrane(Vector3[] control, int sign, float depth, float camber, string name)
        {
            var outline = new List<Vector3>();
            for (int i = 0; i < control.Length; i++)
                for (int step = 0; step < 3; step++)
                {
                    var p = Catmull(control[(i + control.Length - 1) % control.Length], control[i], control[(i + 1) % control.Length], control[(i + 2) % control.Length], step / 3f);
                    p.x = Mathf.Max(0, p.x);
                    outline.Add(p);
                }
            var vertices = new List<Vector3>(outline);
            var triangles = Triangulate(outline);
            // 细分后按全局弧度鼓起翼膜，边缘仍落在翼骨上；共用中点防止内部出现光照裂缝。
            for (int level = 0; level < 2; level++)
            {
                var midpoints = new Dictionary<long, int>();
                var refined = new List<int>();
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    int ab = Midpoint(a, b, vertices, midpoints), bc = Midpoint(b, c, vertices, midpoints), ca = Midpoint(c, a, vertices, midpoints);
                    refined.AddRange(new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca });
                }
                triangles = refined;
            }
            float width = 0, low = float.MaxValue, high = float.MinValue;
            foreach (var p in outline) { width = Mathf.Max(width, p.x); low = Mathf.Min(low, p.y); high = Mathf.Max(high, p.y); }
            var uv = new List<Vector2>();
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 p = vertices[i];
                float distance = InteriorDistance(p, outline);
                p.z = depth * Mathf.Pow(p.x / width, 1.3f) + camber * distance;
                uv.Add(new Vector2(p.x / width, (p.y - low) / (high - low)));
                p.x *= sign;
                vertices[i] = p;
            }
            int frontCount = vertices.Count, triangleCount = triangles.Count;
            // 正反面分离法线；镜像时同步翻转绕序，左右翅膀接受相同方向的光照。
            vertices.AddRange(vertices.ToArray());
            uv.AddRange(uv.ToArray());
            for (int i = 0; i < triangleCount; i += 3)
                triangles.AddRange(new[] { frontCount + triangles[i], frontCount + triangles[i + 2], frontCount + triangles[i + 1] });
            MirrorWinding(triangles, sign);
            return Finish(vertices, triangles, uv, name);
        }

        private static List<int> Triangulate(List<Vector3> outline)
        {
            var remaining = new List<int>();
            var triangles = new List<int>();
            for (int i = 0; i < outline.Count; i++) remaining.Add(i);
            // 凹口逐耳裁切，避免从翼根铺三角扇后把透明区域补成实面。
            while (remaining.Count > 2)
            {
                bool clipped = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int a = remaining[(i + remaining.Count - 1) % remaining.Count], b = remaining[i], c = remaining[(i + 1) % remaining.Count];
                    if (Cross(outline[b] - outline[a], outline[c] - outline[b]) >= -1e-9f) continue;
                    bool occupied = false;
                    foreach (int p in remaining)
                        if (p != a && p != b && p != c && Cross(outline[b] - outline[a], outline[p] - outline[a]) <= 0
                            && Cross(outline[c] - outline[b], outline[p] - outline[b]) <= 0
                            && Cross(outline[a] - outline[c], outline[p] - outline[c]) <= 0) occupied = true;
                    if (occupied) continue;
                    triangles.AddRange(new[] { a, b, c });
                    remaining.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) throw new InvalidOperationException("幼龙鳍膜轮廓无法三角化。");
            }
            return triangles;
        }

        private static int Midpoint(int a, int b, List<Vector3> vertices, Dictionary<long, int> midpoints)
        {
            long key = ((long)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
            if (midpoints.TryGetValue(key, out int index)) return index;
            index = vertices.Count;
            vertices.Add((vertices[a] + vertices[b]) * .5f);
            midpoints.Add(key, index);
            return index;
        }

        private static float InteriorDistance(Vector3 point, List<Vector3> outline)
        {
            float inverse = 0;
            for (int i = 0; i < outline.Count; i++)
            {
                Vector3 a = outline[i], edge = outline[(i + 1) % outline.Count] - a;
                float t = Mathf.Clamp01(Vector3.Dot(point - a, edge) / edge.sqrMagnitude);
                float distance = (point - a - edge * t).sqrMagnitude;
                if (distance < 1e-12f) return 0;
                inverse += 1 / (distance * distance);
            }
            // 平滑的近边距离让膜内弧度连续，避免最近边切换时产生折痕。
            return Mathf.Pow(inverse, -.25f);
        }

        private static Vector3 Catmull(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            return .5f * (2 * b + (c - a) * t + (2 * a - 5 * b + 4 * c - d) * t * t + (-a + 3 * b - 3 * c + d) * t * t * t);
        }

        private static float Cross(Vector3 a, Vector3 b) { return a.x * b.y - a.y * b.x; }

        private static void MirrorWinding(List<int> triangles, int sign)
        {
            if (sign > 0) return;
            for (int i = 0; i < triangles.Count; i += 3)
            {
                int swap = triangles[i + 1];
                triangles[i + 1] = triangles[i + 2];
                triangles[i + 2] = swap;
            }
        }

        private static void SmoothSeams(Mesh mesh, int first, int rings, int sides)
        {
            var normals = mesh.normals;
            for (int r = 0; r < rings; r++)
            {
                int a = first + r * (sides + 1), b = a + sides;
                normals[a] = normals[b] = (normals[a] + normals[b]).normalized;
            }
            mesh.normals = normals;
            mesh.RecalculateTangents();
        }

        private static Mesh Finish(List<Vector3> vertices, List<int> triangles, List<Vector2> uv, string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uv);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
