using ElecPet.Runtime;
using ElecPet.Core;
using NUnit.Framework;
using UnityEngine;

namespace ElecPet.Tests
{
    public sealed class DragonPresentationTests
    {
        [Test]
        public void BlinkCoversTheIrisWithoutFlatteningTheEye()
        {
            var pet = new PrototypePet();
            try
            {
                var eyes = System.Array.FindAll(pet.Root.GetComponentsInChildren<Transform>(), part => part.name == "眼神");
                Assert.That(eyes, Has.Length.EqualTo(2));
                var eyeScales = new Vector3[eyes.Length];
                var irisScales = new Vector3[eyes.Length];
                var open = new int[eyes.Length];
                for (int i = 0; i < eyes.Length; i++)
                {
                    var eye = eyes[i];
                    var iris = eye.Find("绿色虹膜");
                    eyeScales[i] = eye.localScale; irisScales[i] = iris.localScale;
                    open[i] = VisibleIrisSamples(eye);
                    Assert.That(open[i], Is.GreaterThan(100), "睁眼时虹膜应有足够的可见面积");
                }
                var minimumVisible = (int[])open.Clone();
                // 按实际 30 帧连续推进，检查闭合保持阶段的最小可见面积，避免跳时掩盖缓动闭不上的问题。
                for (int frame = 1; frame <= 138; frame++)
                {
                    pet.Animate(Vector2.zero, frame / 30f, false, 0);
                    for (int i = 0; i < eyes.Length; i++)
                    {
                        Assert.That(Vector3.Distance(eyes[i].localScale, eyeScales[i]), Is.LessThan(.001f), "眨眼不能压扁整个眼球组");
                        Assert.That(Vector3.Distance(eyes[i].Find("绿色虹膜").localScale, irisScales[i]), Is.LessThan(.001f), "眨眼不能压扁虹膜");
                        if (frame >= 132) minimumVisible[i] = Mathf.Min(minimumVisible[i], VisibleIrisSamples(eyes[i]));
                    }
                }
                for (int i = 0; i < eyes.Length; i++)
                    Assert.That(minimumVisible[i], Is.LessThanOrEqualTo(open[i] * .01f), "连续眨眼应完全合拢，不能残留亮绿色眼缝");
            }
            finally { Object.DestroyImmediate(pet.Root.gameObject); }
        }

        private static int VisibleIrisSamples(Transform eye)
        {
            var iris = eye.Find("绿色虹膜").GetComponent<MeshFilter>();
            Vector3[] irisTriangles = EyeTriangles(iris, eye);
            var lidTriangles = new System.Collections.Generic.List<Vector3>();
            foreach (var filter in eye.GetComponentsInChildren<MeshFilter>())
                if (filter.name.Contains("眼睑")) lidTriangles.AddRange(EyeTriangles(filter, eye));
            var bounds = new Bounds(irisTriangles[0], Vector3.zero);
            foreach (var vertex in irisTriangles) bounds.Encapsulate(vertex);
            int visible = 0;
            Vector3[] lids = lidTriangles.ToArray();
            // 在眼睛自身坐标内采样几何遮挡，不依赖图形设备或动态碰撞网格。
            for (int y = 0; y < 24; y++)
            for (int x = 0; x < 24; x++)
            {
                var point = new Vector2(Mathf.Lerp(bounds.min.x, bounds.max.x, (x + .5f) / 24),
                    Mathf.Lerp(bounds.min.y, bounds.max.y, (y + .5f) / 24));
                float irisDepth = FrontDepth(irisTriangles, point);
                if (!float.IsPositiveInfinity(irisDepth) && FrontDepth(lids, point) >= irisDepth) visible++;
            }
            return visible;
        }

        [Test]
        public void IrisDoesNotExposeAGreenSurfaceFromBehindTheEye()
        {
            var pet = new PrototypePet();
            try
            {
                var eyes = System.Array.FindAll(pet.Root.GetComponentsInChildren<Transform>(), part => part.name == "眼神");
                Assert.That(eyes, Has.Length.EqualTo(2));
                foreach (var eye in eyes)
                {
                    var iris = eye.Find("绿色虹膜").GetComponent<MeshFilter>();
                    Vector3[] triangles = EyeTriangles(iris, eye);
                    float frontArea = 0, backArea = 0;
                    // 从眼睛局部 +Z 看背面，只统计朝向观察者的真实三角形投影面积，避免远侧眼露出绿色背壳。
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        Vector3 normal = Vector3.Cross(triangles[i + 1] - triangles[i], triangles[i + 2] - triangles[i]);
                        frontArea += Mathf.Max(0, -normal.z) * .5f;
                        backArea += Mathf.Max(0, normal.z) * .5f;
                    }
                    Assert.That(frontArea, Is.GreaterThan(.01f), "虹膜正面必须保留实际可见的绿色眼面");
                    Assert.That(backArea, Is.LessThanOrEqualTo(frontArea * .001f), "背面不能露出绿色虹膜，应由皮肤色眼球壳遮住");
                }
            }
            finally { Object.DestroyImmediate(pet.Root.gameObject); }
        }

        private static Vector3[] EyeTriangles(MeshFilter filter, Transform eye)
        {
            var vertices = filter.sharedMesh.vertices;
            var indices = filter.sharedMesh.triangles;
            var triangles = new Vector3[indices.Length];
            for (int i = 0; i < indices.Length; i++)
                triangles[i] = eye.InverseTransformPoint(filter.transform.TransformPoint(vertices[indices[i]]));
            return triangles;
        }

        private static float FrontDepth(Vector3[] triangles, Vector2 point)
        {
            float depth = float.PositiveInfinity;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                float denominator = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                if (Mathf.Abs(denominator) < .00000001f) continue;
                float u = ((b.y - c.y) * (point.x - c.x) + (c.x - b.x) * (point.y - c.y)) / denominator;
                float v = ((c.y - a.y) * (point.x - c.x) + (a.x - c.x) * (point.y - c.y)) / denominator;
                float w = 1 - u - v;
                if (u >= 0 && v >= 0 && w >= 0) depth = Mathf.Min(depth, a.z * u + b.z * v + c.z * w);
            }
            return depth;
        }

        [Test]
        public void RefinedModelStaysWithinDesktopRenderingBudget()
        {
            var pet = new PrototypePet();
            try
            {
                var meshes = new System.Collections.Generic.HashSet<Mesh>();
                int vertices = 0;
                foreach (var filter in pet.Root.GetComponentsInChildren<MeshFilter>())
                    if (meshes.Add(filter.sharedMesh)) vertices += filter.sharedMesh.vertexCount;
                TestContext.WriteLine("模型网格顶点：" + vertices + "；渲染部件：" + pet.Root.GetComponentsInChildren<Renderer>().Length);
                // 限制持续运行的桌宠成本，保留造型空间，不依赖某一版模型的精确数量。
                Assert.That(vertices, Is.LessThan(65000), "独立网格顶点总数不能超过桌宠预算");
                Assert.That(pet.Root.GetComponentsInChildren<Renderer>().Length, Is.LessThan(150), "渲染部件数量不能随细节无限增长");
            }
            finally { Object.DestroyImmediate(pet.Root.gameObject); }
        }

        [Test]
        public void AllPosesAndCarryStayInsideTheRuntimeProjectedSafetyMargin()
        {
            var host = new GameObject("模型真实投影边界检查");
            var controller = host.AddComponent<DesktopPetController>();
            var type = typeof(DesktopPetController);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var pet = new PrototypePet();
            Camera camera = null;
            try
            {
                type.GetMethod("CreateCamera", flags).Invoke(controller, null);
                camera = (Camera)type.GetField("petCamera", flags).GetValue(controller);
                camera.orthographicSize = 3;
                camera.pixelRect = new Rect(0, 0, 600, 600);
                float clock = 0;
                var progress = new[] { 0f, .18f, .35f, .55f, .78f, 1f };
                var looks = new[] { Vector2.zero, Vector2.one, -Vector2.one, new Vector2(-1, 1), new Vector2(1, -1), Vector2.zero };
                for (int direction = 0; direction < 8; direction++)
                {
                    float yaw = direction * 45;
                    pet.Root.localRotation = Quaternion.Euler(0, yaw, 0);
                    var velocity = new Vector2(-Mathf.Sin(yaw * Mathf.Deg2Rad), -Mathf.Cos(yaw * Mathf.Deg2Rad)) * 80;
                    foreach (PetState state in System.Enum.GetValues(typeof(PetState)))
                    for (int phase = 0; phase < progress.Length; phase++)
                    {
                        for (int frame = 0; frame < 20; frame++)
                            pet.Animate(looks[phase], clock += 1f / 30, state == PetState.Wander, 1,
                                state, progress[phase], PetTouch.Tail, true, state == PetState.Wander ? velocity : Vector2.zero);
                        AssertProjectedMargin(pet, camera, direction + ":" + state + ":" + progress[phase]);
                    }
                    // 拾起、叼持、放下会额外改变头颈与前爪，单独覆盖业务参数的边界组合。
                    foreach (var carry in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one })
                    {
                        for (int frame = 0; frame < 20; frame++)
                            pet.Animate(Vector2.one, clock += 1f / 30, true, 0, velocity: velocity,
                                organizingGrip: carry.x, organizingBend: carry.y);
                        AssertProjectedMargin(pet, camera, direction + ":叼持:" + carry);
                    }
                }
            }
            finally
            {
                if (camera != null) Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(pet.Root.gameObject);
                Object.DestroyImmediate(host);
            }
        }

        private static void AssertProjectedMargin(PrototypePet pet, Camera camera, string pose)
        {
            Vector3 origin = camera.WorldToScreenPoint(pet.Root.position);
            float horizontal = 0, vertical = 0;
            string horizontalPart = "", verticalPart = "";
            // 俯视相机会把世界 Z 投到屏幕 Y，必须投影真实顶点，不能只检查世界 X/Y 包围盒。
            foreach (var filter in pet.Root.GetComponentsInChildren<MeshFilter>())
            foreach (var vertex in filter.sharedMesh.vertices)
            {
                Vector3 screen = camera.WorldToScreenPoint(filter.transform.TransformPoint(vertex));
                float x = Mathf.Abs(screen.x - origin.x), y = Mathf.Abs(screen.y - origin.y);
                if (x > horizontal) { horizontal = x; horizontalPart = filter.name; }
                if (y > vertical) { vertical = y; verticalPart = filter.name; }
            }
            Assert.That(horizontal, Is.LessThanOrEqualTo(88), pose + ":" + horizontalPart + ":水平安全边距");
            Assert.That(vertical, Is.LessThanOrEqualTo(88), pose + ":" + verticalPart + ":纵向安全边距");
        }

        [TestCase(80, 0)]
        [TestCase(0, 80)]
        [TestCase(0, -80)]
        public void RuntimeProjectedSupportPawStaysPlanted(float x, float y)
        {
            var host = new GameObject("屏幕步态检查");
            var controller = host.AddComponent<DesktopPetController>();
            var type = typeof(DesktopPetController);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var pet = new PrototypePet();
            Camera camera = null;
            try
            {
                type.GetMethod("CreateCamera", flags).Invoke(controller, null);
                camera = (Camera)type.GetField("petCamera", flags).GetValue(controller);
                camera.orthographicSize = 3;
                camera.pixelRect = new Rect(0, 0, 600, 600);
                var motion = new PetMotion(new DesktopRect(-10000, -10000, 20000, 20000), 88);
                motion.DragTo(new Point2(300, 300));
                type.GetField("motion", flags).SetValue(controller, motion);
                type.GetField("behavior", flags).SetValue(controller, new PetBehavior(new System.Random(2)));
                type.GetField("pet", flags).SetValue(controller, pet);
                var origin = new Vector2(300, 300);
                var velocity = new Vector2(x, y);
                var paw = pet.Root.Find("前爪");
                Vector3 previous = Vector3.zero;
                float previousHeight = 0;
                int planted = 0, stable = 0;
                for (int i = 1; i <= 180; i++)
                {
                    Vector2 point = origin + velocity * (i / 30f);
                    Point2 previousPosition = motion.Position;
                    motion.DragTo(new Point2(point.x, point.y));
                    type.GetMethod("UpdatePresentation", flags).Invoke(controller, new object[] { Vector2.zero, previousPosition, 1f / 30 });
                    var screen = camera.WorldToScreenPoint(paw.position);
                    if (i > 60 && Mathf.Abs(paw.localPosition.y + .58f) < .002f && Mathf.Abs(previousHeight + .58f) < .002f)
                    {
                        planted++;
                        if (Vector2.Distance(previous, screen) < .6f) stable++;
                    }
                    previous = screen;
                    previousHeight = paw.localPosition.y;
                }
                Assert.That(planted, Is.GreaterThan(20));
                Assert.That(stable, Is.GreaterThan(planted * .8f), "实际相机投影下，横向和纵向移动的支撑脚都不应随身体滑动");
            }
            finally
            {
                type.GetField("behavior", flags).SetValue(controller, null);
                if (camera != null) Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(pet.Root.gameObject);
                Object.DestroyImmediate(host);
            }
        }

        [TestCase(80, 0)]
        [TestCase(-80, 0)]
        [TestCase(0, 80)]
        [TestCase(0, -80)]
        public void BodyFacesTravelEvenWhenPointerIsElsewhere(float x, float y)
        {
            var pet = new PrototypePet();
            try
            {
                var velocity = new Vector2(x, y);
                for (int i = 1; i <= 60; i++)
                    pet.Animate(Vector2.left, i / 30f, true, 0, velocity: velocity);
                Vector3 nose = pet.Root.TransformDirection(Vector3.back);
                Assert.That(Vector3.Dot(nose, new Vector3(x, 0, y).normalized), Is.GreaterThan(.99f), "向左右及前后移动都应转身，不能始终正对镜头");
                foreach (var renderer in pet.Root.GetComponentsInChildren<Renderer>())
                {
                    Assert.That(renderer.bounds.min.x, Is.GreaterThanOrEqualTo(-.88f), renderer.name);
                    Assert.That(renderer.bounds.max.x, Is.LessThanOrEqualTo(.88f), renderer.name);
                }
            }
            finally { Object.DestroyImmediate(pet.Root.gameObject); }
        }

        [Test]
        public void PlantedPawDoesNotSlideWhileBodyTravels()
        {
            var pet = new PrototypePet();
            try
            {
                float time = 0;
                for (int i = 0; i < 60; i++) pet.Animate(Vector2.zero, time += 1f / 30, true, 0, velocity: Vector2.right * 80);
                var paw = pet.Root.Find("前爪");
                Vector3 previous = paw.position;
                int planted = 0, stable = 0;
                for (int i = 0; i < 120; i++)
                {
                    pet.Root.position += Vector3.right * (.8f / 30);
                    pet.Animate(Vector2.zero, time += 1f / 30, true, 0, velocity: Vector2.right * 80);
                    if (Mathf.Abs(paw.localPosition.y + .58f) < .002f && Mathf.Abs(previous.y + .2784f) < .002f)
                    {
                        planted++;
                        if (Vector3.Distance(previous, paw.position) < .006f) stable++;
                    }
                    previous = paw.position;
                }
                Assert.That(planted, Is.GreaterThan(20), "存在足够长的着地支撑阶段");
                Assert.That(stable, Is.GreaterThan(planted * .8f), "支撑脚随身体前进在局部后移，避免滑步");
            }
            finally { Object.DestroyImmediate(pet.Root.gameObject); }
        }

        [Test]
        public void PointerMovementDoesNotTurnAnIdleBody()
        {
            var pet = new PrototypePet();
            try
            {
                Quaternion facing = pet.Root.rotation;
                for (int i = 1; i <= 60; i++) pet.Animate(Vector2.right, i / 30f, false, 0);
                Assert.That(Quaternion.Angle(facing, pet.Root.rotation), Is.LessThan(.1f), "停下后身体保持朝向，头部独立注视");
            }
            finally { Object.DestroyImmediate(pet.Root.gameObject); }
        }

        [Test]
        public void ResizeDiscardsStaleHuntTrajectory()
        {
            var host = new GameObject("扑食缩放检查");
            var controller = host.AddComponent<DesktopPetController>();
            var type = typeof(DesktopPetController);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            try
            {
                var behavior = new PetBehavior(new System.Random(2));
                behavior.TryHunt(130);
                behavior.Tick(1.2);
                var motion = new PetMotion(new DesktopRect(0, 0, 100000, 100000), 88);
                motion.DragTo(new Point2(Screen.width + 500, 300));
                var origin = new Vector2((float)motion.Position.X, (float)motion.Position.Y);
                type.GetField("behavior", flags).SetValue(controller, behavior);
                type.GetField("motion", flags).SetValue(controller, motion);
                type.GetField("petCamera", flags).SetValue(controller, host.AddComponent<Camera>());
                type.GetField("huntOrigin", flags).SetValue(controller, origin);
                type.GetField("huntTarget", flags).SetValue(controller, origin + Vector2.right * 65);
                // 实际调用工作区重算，再扩大运动范围，检查旧轨迹不会重新拉回模型。
                type.GetMethod("RecalculateArea", flags).Invoke(controller, null);
                Point2 clamped = motion.Position;
                motion.Resize(new DesktopRect(0, 0, 100000, 100000));
                type.GetMethod("UpdateAutonomy", flags).Invoke(controller, new object[] { Vector2.zero, 1f / 30, false });
                Assert.That(motion.Position.X, Is.EqualTo(clamped.X).Within(.001), "尺寸恢复后不跳回旧轨迹");
            }
            finally
            {
                // 测试实例不保存习惯数据，避免改动本机真实宠物的熟悉度。
                type.GetField("behavior", flags).SetValue(controller, null);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RestLowersTheHeadAndBodyInsteadOfOnlyNodding()
        {
            var pet = new PrototypePet();
            try
            {
                pet.Animate(Vector2.zero, 0, false, 0);
                var head = pet.Root.Find("躯干/注视点");
                float awakeHead = head.position.y;
                // 入睡时头部应随躯干明显下沉，不能只改变头部旋转。
                for (int i = 1; i <= 60; i++)
                    pet.Animate(Vector2.zero, i / 30f, false, 0, PetState.Drowsy, .5f);
                Assert.That(head.position.y, Is.LessThan(awakeHead - .1f));
                foreach (var part in pet.Root.GetComponentsInChildren<Transform>())
                    if (part.name == "前爪" || part.name == "后爪")
                        Assert.That(part.localPosition.y, Is.EqualTo(-.58f).Within(.01f), "卧躺时四爪仍着地");
            }
            finally { Object.DestroyImmediate(pet.Root.gameObject); }
        }

        [Test]
        public void AnimatedDragonFitsExistingDesktopSafetyMargin()
        {
            var pet = new PrototypePet();
            try
            {
                float clock = 0;
                // 桌面相机每世界单位对应 100 像素；模型动作不能越出已有的 88 像素安全边距。
                foreach (var look in new[] { Vector2.zero, Vector2.one, -Vector2.one, new Vector2(-1, 1), new Vector2(1, -1) })
                foreach (float time in new[] { 0f, .6f, 1.2f, 2.1f, 4.6f })
                {
                    clock += time;
                    for (int frame = 0; frame < 20; frame++)
                        pet.Animate(look, clock += 1f / 30, true, 1);
                    foreach (var renderer in pet.Root.GetComponentsInChildren<Renderer>())
                    {
                        var bounds = renderer.bounds;
                        Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(-.88f), renderer.name);
                        Assert.That(bounds.max.x, Is.LessThanOrEqualTo(.88f), renderer.name);
                        Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(-.88f), renderer.name);
                        Assert.That(bounds.max.y, Is.LessThanOrEqualTo(.88f), renderer.name);
                    }
                }
            }
            finally { Object.DestroyImmediate(pet.Root.gameObject); }
        }

        [Test]
        public void InteractionPosesStayInsideDesktopAndResetAfterwards()
        {
            var pet = new PrototypePet();
            try
            {
                float clock = 0;
                foreach (PetState state in System.Enum.GetValues(typeof(PetState)))
                foreach (float progress in new[] { 0f, .25f, .5f, .75f, 1f })
                foreach (var look in new[] { Vector2.zero, Vector2.one, -Vector2.one, new Vector2(-1, 1), new Vector2(1, -1) })
                {
                    for (int frame = 0; frame < 20; frame++)
                        pet.Animate(look, clock += 1f / 30, false, 1, state, progress, PetTouch.Tail, true);
                    foreach (var renderer in pet.Root.GetComponentsInChildren<Renderer>())
                    {
                        var bounds = renderer.bounds;
                        Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(-.88f), state + ":" + renderer.name);
                        Assert.That(bounds.max.x, Is.LessThanOrEqualTo(.88f), state + ":" + renderer.name);
                        Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(-.88f), state + ":" + renderer.name);
                        Assert.That(bounds.max.y, Is.LessThanOrEqualTo(.88f), state + ":" + renderer.name);
                    }
                }
                pet.Animate(Vector2.zero, clock += 1f / 30, false, 0, PetState.Stretching, .5f);
                for (int frame = 0; frame < 60; frame++)
                    pet.Animate(Vector2.zero, clock += 1f / 30, false, 0);
                Assert.That(pet.Root.Find("前爪").localPosition.z, Is.EqualTo(-.43f).Within(.001));
                Assert.That(pet.Root.Find("躯干/注视点").localPosition.y, Is.EqualTo(.14f).Within(.001));
            }
            finally { Object.DestroyImmediate(pet.Root.gameObject); }
        }

        [Test]
        public void HuntCrouchesThenLeapsAndCanBlendBackToWalking()
        {
            var pet = new PrototypePet();
            try
            {
                float clock = 0;
                var body = pet.Root.Find("躯干");
                for (int i = 0; i < 30; i++) pet.Animate(Vector2.zero, clock += 1f / 30, false, 0, PetState.Hunting, .25f);
                Assert.That(body.localPosition.y, Is.LessThan(-.22f), "起扑前伏低蓄势");
                for (int i = 0; i < 30; i++) pet.Animate(Vector2.zero, clock += 1f / 30, false, 0, PetState.Hunting, .55f);
                Assert.That(body.localPosition.y, Is.GreaterThan(.06f), "扑出阶段身体腾起");
                float previous = body.position.y;
                pet.Animate(Vector2.zero, clock += 1f / 30, false, 0, PetState.Petted, .5f);
                Assert.That(Mathf.Abs(body.position.y - previous), Is.LessThan(.06f), "互动打断时没有瞬间跳回基础姿势");
                for (int i = 0; i < 60; i++) pet.Animate(Vector2.zero, clock += 1f / 30, false, 0);
                Assert.That(body.localPosition.y, Is.InRange(-.12f, -.08f));
            }
            finally { Object.DestroyImmediate(pet.Root.gameObject); }
        }
    }
}
