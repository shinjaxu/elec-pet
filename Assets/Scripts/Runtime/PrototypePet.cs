using UnityEngine;
using ElecPet.Core;

namespace ElecPet.Runtime
{
    public sealed class PrototypePet
    {
        public Transform Root { get; private set; }
        public Vector3 CarryAnchor { get { return head.TransformPoint(mouthOffset); } }
        private Vector3 mouthOffset = new Vector3(0, -.12f, -.48f);
        private readonly Transform torso, head, tail;
        private readonly Transform upperJaw, jaw, mouthInterior;
        private readonly Renderer closedFace, mouthLine;
        private readonly Mesh mouthMesh;
        private readonly Vector3[] mouthSeam, mouthVertices;
        private readonly Transform[] wings = new Transform[2];
        private readonly Transform[] ears = new Transform[2];
        private readonly Transform[] eyes = new Transform[2];
        private readonly Transform[] pupils = new Transform[2];
        private readonly Mesh[,] eyelids = new Mesh[2, 2];
        private readonly Vector3[][] lidVertices = new Vector3[4][];
        private float lidOpening = -1;
        private readonly Transform[] paws = new Transform[4];
        private readonly Transform[] legs = new Transform[4];
        private readonly Transform[] shins = new Transform[4];
        private readonly Mesh round = DragonMesh.Body(0);
        private float previousTime = -1;
        private float gaitPhase, walkBlend, previousYaw = -25;

        public PrototypePet()
        {
            Root = new GameObject("无牙仔").transform;
            Root.localScale = Vector3.one * .48f;
            Root.localRotation = Quaternion.Euler(0, -25, 0);
            var coat = Skin(new Color(.048f, .064f, .079f), .32f);
            var membrane = Skin(new Color(.075f, .098f, .115f), .26f);
            var ridge = Skin(new Color(.075f, .092f, .105f), .34f);
            var green = Skin(new Color(.42f, .64f, .115f), .67f);
            var ink = Skin(new Color(.006f, .009f, .012f), .52f);
            var white = Skin(new Color(.91f, .98f, .89f), .75f);
            var claw = Skin(new Color(.23f, .26f, .28f), .38f);
            var red = Skin(new Color(.40f, .065f, .045f), .29f);
            var seam = Skin(new Color(.20f, .105f, .075f), .25f);
            var eyelidSkin = Skin(coat.color, .32f);
            var iris = DragonMesh.Iris();
            coat.mainTexture = Resources.Load<Texture2D>("DragonScales");
            coat.SetTexture("_BumpMap", Resources.Load<Texture2D>("DragonScalesNormal"));
            coat.SetFloat("_BumpScale", .10f);
            coat.EnableKeyword("_NORMALMAP");
            green.mainTexture = Resources.Load<Texture2D>("DragonIris");

            // 水平躯干与独立四爪构成低重心四足结构，卧躺时身体下沉而爪子保持着地。
            torso = Joint("躯干", Root, new Vector3(0, -.1f, .12f));
            Part("四足身体", torso, Vector3.zero, new Vector3(.86f, .64f, 1.44f), coat, DragonMesh.Torso());
            Part("颈部", torso, new Vector3(0, .04f, -.40f), new Vector3(.64f, .37f, .65f), coat);
            head = Joint("注视点", torso, new Vector3(0, .14f, -.68f));
            // 头吻使用同一张连续曲面，眼睛嵌在面颊上，避免独立球体堆叠出的接缝。
            var headMesh = DragonMesh.Head();
            closedFace = Part("宽扁头部", head, Vector3.zero, Vector3.one, coat, headMesh).GetComponent<Renderer>();
            for (int i = 0; i < 2; i++)
            {
                int sign = i == 0 ? -1 : 1;
                ears[i] = Joint("鳍耳", head, new Vector3(sign * .43f, .18f, .08f));
                Part("长耳鳍", ears[i], Vector3.zero, Vector3.one, coat, DragonMesh.EarFin(sign));
                for (int fin = 0; fin < 2; fin++)
                {
                    var sideFin = Part("侧面耳鳍", head, new Vector3(sign * (.48f + fin * .015f), -.01f - fin * .085f, .10f + fin * .13f),
                        new Vector3(.23f, .10f, .29f), coat);
                    sideFin.localRotation = Quaternion.Euler(0, sign * -28, sign * -20);
                }
                eyes[i] = Joint("眼神", head, new Vector3(sign * .305f, .070f, -.360f));
                eyes[i].localRotation = Quaternion.Euler(0, sign * -22, sign * -8);
                // 绿色只铺前眼面，后侧使用皮肤色眼壳，侧后方看闭眼也不会漏出绿色球背。
                Part("眼球壳", eyes[i], new Vector3(0, 0, .004f), new Vector3(.32f, .275f, .065f), eyelidSkin);
                Part("绿色虹膜", eyes[i], Vector3.zero, new Vector3(.32f, .275f, .065f), green, iris);
                pupils[i] = Joint("瞳孔注视", eyes[i], Vector3.zero);
                Part("竖瞳", pupils[i], new Vector3(0, 0, -.036f), new Vector3(.081f, .235f, .016f), ink);
                Part("眼睛高光", pupils[i], new Vector3(-.034f, .048f, -.048f), new Vector3(.033f, .040f, .013f), white);
                Curve("眼眶", eyes[i], new[] { new Vector3(-.17f, 0, 0), new Vector3(-.12f, .11f, -.008f),
                    new Vector3(0, .145f, -.01f), new Vector3(.12f, .11f, -.008f), new Vector3(.17f, 0, 0),
                    new Vector3(.11f, -.12f, -.005f), new Vector3(0, -.145f, -.008f),
                    new Vector3(-.11f, -.12f, -.005f), new Vector3(-.17f, 0, 0) },
                    new[] { .007f, .011f, .014f, .011f, .007f, .006f, .007f, .006f, .007f }, coat);
                CreateEyelid(i, 0, eyelidSkin);
                CreateEyelid(i, 1, eyelidSkin);
                Part("鼻孔", head, new Vector3(sign * .135f, -.095f, -.473f), new Vector3(.042f, .023f, .014f), ink);
                wings[i] = Joint("小翅膀", torso, new Vector3(sign * .29f, .18f, .04f));
                var wingMesh = DragonMesh.Wing(sign);
                Part("蝠翼翼膜", wings[i], Vector3.zero, new Vector3(.95f, .88f, 1), membrane, wingMesh);
                // 翼骨与翼膜使用相同缩放，避免翼尖骨骼悬在薄膜之外。
                var wingBones = Joint("翼骨结构", wings[i], Vector3.zero);
                wingBones.localScale = new Vector3(.95f, .88f, 1);
                SurfaceCurve("翼骨", wingBones, new[] { Vector3.zero, new Vector3(sign * .23f, .46f, .02f),
                    new Vector3(sign * .62f, .71f, .08f), new Vector3(sign * .83f, .38f, .11f) },
                    new[] { .046f, .031f, .022f, .003f }, coat, wingMesh);
                SurfaceCurve("翼指", wingBones, new[] { new Vector3(sign * .23f, .46f, .02f),
                    new Vector3(sign * .41f, .39f, .015f), new Vector3(sign * .55f, .14f, .01f) },
                    new[] { .023f, .014f, .002f }, ridge, wingMesh);
                SurfaceCurve("内翼指", wingBones, new[] { new Vector3(sign * .23f, .46f, .02f),
                    new Vector3(sign * .22f, .18f, -.01f), new Vector3(sign * .22f, -.06f, 0) },
                    new[] { .018f, .012f, .002f }, coat, wingMesh);
            }
            mouthLine = SurfaceCurve("嘴线", head, new[] { new Vector3(-.34f, -.15f, -.365f), new Vector3(-.20f, -.19f, -.435f),
                new Vector3(0, -.20f, -.468f), new Vector3(.20f, -.19f, -.435f), new Vector3(.34f, -.15f, -.365f) },
                new[] { .003f, .006f, .006f, .006f, .003f }, ink, headMesh).GetComponent<Renderer>();
            // 原始脸部碰撞体始终保留，开口几何只负责表现，避免逐帧改变桌面命中范围。
            upperJaw = Part("进食上颌", head, Vector3.zero, Vector3.one, coat, DragonMesh.HeadSection(headMesh, false), false);
            var hinge = new Vector3(0, -.13f, .10f);
            jaw = Joint("进食下颌", head, hinge);
            Part("下颌外壳", jaw, -hinge, Vector3.one, coat, DragonMesh.HeadSection(headMesh, true), false);
            var mouthDark = Skin(new Color(.012f, .008f, .014f), .08f);
            mouthDark.SetFloat("_SpecularHighlights", 0); mouthDark.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mouthMesh = DragonMesh.MouthLining(headMesh);
            mouthSeam = mouthMesh.vertices; mouthVertices = new Vector3[mouthSeam.Length];
            mouthInterior = Part("口腔暗部", head, Vector3.zero, Vector3.one, mouthDark, mouthMesh, false);
            var tongue = Skin(new Color(.24f, .09f, .12f), .34f);
            Part("小舌头", jaw, new Vector3(0, -.12f, -.40f) - hinge, new Vector3(.22f, .025f, .14f), tongue, interactive: false);
            for (int i = 0; i < 4; i++)
            {
                paws[i] = Joint(i < 2 ? "前爪" : "后爪", Root, PawHome(i));
                Part("掌部", paws[i], new Vector3(0, .01f, .012f), new Vector3(.29f, .17f, .34f), coat);
                legs[i] = Part("上肢", Root, Vector3.zero, Vector3.one, coat);
                shins[i] = Part("下肢", Root, Vector3.zero, Vector3.one, coat);
                for (int toe = 0; toe < 3; toe++)
                {
                    Part("脚趾", paws[i], new Vector3((toe - 1) * .081f, -.005f, -.105f), new Vector3(.09f, .105f, .16f), coat);
                    Part("爪尖", paws[i], new Vector3((toe - 1) * .081f, -.015f, -.178f), new Vector3(.035f, .038f, .081f), claw);
                }
            }
            tail = Joint("长尾", torso, new Vector3(0, -.04f, .49f));
            tail.localScale = Vector3.one * .8f;
            Curve("渐细尾巴", tail, new[] { Vector3.zero, new Vector3(.09f, -.09f, .35f),
                new Vector3(.19f, -.13f, .70f), new Vector3(.30f, -.08f, 1.03f), new Vector3(.34f, .02f, 1.20f) },
                new[] { .18f, .125f, .09f, .055f, .015f }, coat);
            for (int i = 0; i < 2; i++)
            {
                int sign = i == 0 ? -1 : 1;
                var fin = Joint(i == 0 ? "红色义尾鳍" : "黑色尾鳍", tail, new Vector3(.29f, -.06f, .99f));
                fin.localRotation = Quaternion.Euler(90, 0, 0);
                Part("尾翼膜", fin, Vector3.zero, new Vector3(.46f, .48f, 1), i == 0 ? red : membrane, DragonMesh.TailFin(sign));
                Curve("尾鳍支骨", fin, new[] { Vector3.zero, new Vector3(sign * .15f, .15f, .012f),
                    new Vector3(sign * .32f, .25f, .01f) }, new[] { .021f, .012f, .002f }, i == 0 ? seam : coat);
                if (i == 0)
                    for (int stitch = 0; stitch < 5; stitch++)
                        Part("义尾缝线", fin, new Vector3(-.055f - stitch * .034f, .07f + stitch * .028f, -.013f),
                            new Vector3(.028f, .009f, .009f), seam);
            }
            for (int i = 0; i < 6; i++)
                Curve("背鳍", torso, new[] { new Vector3(0, .26f - i * .015f, -.16f + i * .16f),
                    new Vector3(0, .37f - i * .015f, -.12f + i * .16f) }, new[] { .055f, .002f }, ridge);
            Animate(Vector2.zero, 0, false, 0);
        }

        private static Transform Joint(string name, Transform parent, Vector3 position)
        {
            var joint = new GameObject(name).transform;
            joint.SetParent(parent, false);
            joint.localPosition = position;
            return joint;
        }

        private static Vector3 PawHome(int index)
        {
            return new Vector3((index % 2 == 0 ? -1 : 1) * .36f, -.58f, index < 2 ? -.43f : .60f);
        }

        private static Material Skin(Color color, float smoothness = .22f)
        {
            // 保留构建中的哑光与高光着色器变体，深色皮肤依靠轮廓光呈现体积。
            var material = new Material(Resources.Load<Material>(smoothness < .4f ? "DragonMatte" : "Prototype")) { color = color };
            material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Metallic", 0);
            material.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.SetFloat("_SpecularHighlights", 1);
            material.SetTexture("_BumpMap", null);
            material.DisableKeyword("_NORMALMAP");
            return material;
        }

        private Transform Part(string name, Transform parent, Vector3 position, Vector3 scale, Material material, Mesh mesh = null, bool interactive = true)
        {
            var part = Joint(name, parent, position);
            part.localScale = scale;
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh != null ? mesh : round;
            part.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            // 碰撞体跟随模型姿态，翼膜之间的透明空隙仍可点击桌面。
            if (interactive) part.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh != null ? mesh : round;
            return part;
        }

        private Transform Curve(string name, Transform parent, Vector3[] centers, float[] radii, Material material)
        {
            return Part(name, parent, Vector3.zero, Vector3.one, material, DragonMesh.Curve(centers, radii));
        }

        private Transform SurfaceCurve(string name, Transform parent, Vector3[] centers, float[] radii, Material material, Mesh surface)
        {
            // 以实际网格采样嘴线与翼骨，模型塑形后仍贴住曲面；密集控制点防止插值跨过弧面。
            var vertices = surface.vertices;
            var triangles = surface.triangles;
            var fitted = new Vector3[(centers.Length - 1) * 4 + 1];
            var widths = new float[fitted.Length];
            for (int i = 0; i < fitted.Length; i++)
            {
                int segment = Mathf.Min(i / 4, centers.Length - 2);
                float t = (i - segment * 4) / 4f;
                Vector3 point = Vector3.Lerp(centers[segment], centers[segment + 1], t);
                float depth = float.PositiveInfinity;
                for (int index = 0; index < triangles.Length; index += 3)
                {
                    Vector3 a = vertices[triangles[index]], b = vertices[triangles[index + 1]], c = vertices[triangles[index + 2]];
                    float divisor = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                    if (Mathf.Abs(divisor) < .00000001f) continue;
                    float u = ((b.y - c.y) * (point.x - c.x) + (c.x - b.x) * (point.y - c.y)) / divisor;
                    float v = ((c.y - a.y) * (point.x - c.x) + (a.x - c.x) * (point.y - c.y)) / divisor;
                    if (u >= -.00001f && v >= -.00001f && u + v <= 1.00001f)
                        depth = Mathf.Min(depth, a.z * u + b.z * v + c.z * (1 - u - v));
                }
                if (!float.IsPositiveInfinity(depth)) point.z = depth - .001f;
                fitted[i] = point;
                widths[i] = Mathf.Lerp(radii[segment], radii[segment + 1], t);
            }
            return Curve(name, parent, fitted, widths, material);
        }

        private void CreateEyelid(int eye, int half, Material material)
        {
            const int columns = 24, rows = 6;
            var mesh = new Mesh { name = half == 0 ? "上眼睑曲面" : "下眼睑曲面" };
            mesh.MarkDynamic();
            lidVertices[eye * 2 + half] = new Vector3[(columns + 1) * (rows + 1)];
            var triangles = new int[columns * rows * 6];
            int index = 0;
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
            {
                int a = y * (columns + 1) + x, b = a + columns + 1;
                // 上下眼睑都朝眼球外侧，闭眼只改变覆盖范围，保持眼球体积和高光尺寸。
                triangles[index++] = a; triangles[index++] = half == 0 ? b : a + 1;
                triangles[index++] = half == 0 ? a + 1 : b;
                triangles[index++] = a + 1; triangles[index++] = half == 0 ? b : b + 1;
                triangles[index++] = half == 0 ? b + 1 : b;
            }
            mesh.vertices = lidVertices[eye * 2 + half];
            mesh.triangles = triangles;
            eyelids[eye, half] = mesh;
            // 眼睑只负责视觉遮挡；点击沿用眼球和头部碰撞体，避免逐帧重建碰撞网格。
            Part(half == 0 ? "上眼睑" : "下眼睑", eyes[eye], Vector3.zero, Vector3.one, material, mesh, false);
        }

        private void UpdateEyelids(float opening)
        {
            opening = Mathf.Clamp(opening, -.025f, .995f);
            if (Mathf.Abs(opening - lidOpening) < .0005f) return;
            lidOpening = opening;
            const int columns = 24, rows = 6;
            for (int eye = 0; eye < 2; eye++)
            for (int half = 0; half < 2; half++)
            {
                var vertices = lidVertices[eye * 2 + half];
                for (int y = 0; y <= rows; y++)
                for (int x = 0; x <= columns; x++)
                {
                    float across = Mathf.Lerp(-1, 1, x / (float)columns);
                    float edge = Mathf.Sqrt(Mathf.Max(0, 1 - across * across));
                    float height = Mathf.Lerp(opening, 1, y / (float)rows) * edge;
                    float depth = Mathf.Sqrt(Mathf.Max(0, 1 - across * across - height * height));
                    vertices[y * (columns + 1) + x] = new Vector3(across * .176f,
                        height * .155f * (half == 0 ? 1 : -1), -.009f - depth * .055f);
                }
                var mesh = eyelids[eye, half];
                mesh.vertices = vertices;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            }
        }

        public void Animate(Vector2 look, float time, bool walking, float affection,
            PetState state = PetState.Wander, float progress = 0, PetTouch touch = PetTouch.Body, bool likesHead = false,
            Vector2 velocity = default(Vector2), float organizingGrip = 0, float organizingBend = 0,
            float feedingProgress = -1, bool feedingFailed = false, float stalking = 0, float flying = 0)
        {
            // 进出片段保留缓冲；被抚摸或提起打断时，按活动时间插值，暂停不会积累位移。
            float dt = previousTime < 0 ? 1 : Mathf.Clamp(time - previousTime, 0, .1f);
            previousTime = time;
            float blend = 1 - Mathf.Exp(-dt * 16);
            bool feeding = feedingProgress >= 0 && feedingProgress <= 1;
            float feed = Mathf.Clamp01(feedingProgress);
            float feedEnvelope = feeding ? Mathf.SmoothStep(0, 1, feed / .08f)
                * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.92f, 1, feed))) : 0;
            float sniff = feeding ? Mathf.Sin(Mathf.InverseLerp(0, .18f, feed) * Mathf.PI) : 0;
            float bite = feeding ? Mathf.Sin(Mathf.InverseLerp(.18f, .32f, feed) * Mathf.PI) : 0;
            float chewing = feeding && feed >= .32f && feed < .67f
                ? (.5f + .5f * Mathf.Sin((feed - .32f) / .35f * Mathf.PI * 6)) : 0;
            float swallow = feeding && !feedingFailed ? Mathf.Sin(Mathf.InverseLerp(.67f, .80f, feed) * Mathf.PI) : 0;
            float satisfied = feeding && !feedingFailed
                ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.78f, .85f, feed)) * feedEnvelope : 0;
            float refusal = feeding && feedingFailed ? Mathf.Sin(Mathf.InverseLerp(.67f, 1, feed) * Mathf.PI) : 0;
            float mouthOpen = Mathf.Max(bite, chewing * .42f) * (feedingFailed && feed >= .67f ? 0 : 1);
            // 叼图标仍沿用原锚点；张嘴进食时把代理落点下移到真实上下颌之间。
            mouthOffset = new Vector3(0, -.12f - feedEnvelope * .08f - mouthOpen * .105f, -.48f + mouthOpen * .035f);
            upperJaw.gameObject.SetActive(mouthOpen > .015f);
            jaw.gameObject.SetActive(mouthOpen > .015f);
            mouthInterior.gameObject.SetActive(mouthOpen > .015f);
            closedFace.enabled = mouthLine.enabled = mouthOpen <= .015f;
            jaw.localRotation = Quaternion.Euler(-mouthOpen * 22, 0, 0);
            if (mouthOpen > .015f)
            {
                for (int i = 0; i < mouthVertices.Length; i++)
                {
                    float t = (i % 6 / 2) * .5f;
                    Vector3 lower = jaw.localPosition + jaw.localRotation * (mouthSeam[i] - jaw.localPosition);
                    Vector3 point = Vector3.Lerp(mouthSeam[i], lower, t);
                    float inset = t == .5f ? .80f : .995f;
                    point.x *= inset; point.z *= inset;
                    mouthVertices[i] = point;
                }
                mouthMesh.vertices = mouthVertices; mouthMesh.RecalculateNormals(); mouthMesh.RecalculateBounds();
            }
            float speed = walking && state == PetState.Wander ? velocity.magnitude : 0;
            walkBlend = Mathf.MoveTowards(walkBlend, speed > 1 ? 1 : 0, dt * 5);
            // 身体按移动方向转身，停步保留最后朝向；鼠标只影响头部，不再强制身体面向镜头。
            if (velocity.sqrMagnitude > 1 && state != PetState.Carried)
            {
                float yaw = Mathf.Atan2(-velocity.x, -velocity.y) * Mathf.Rad2Deg;
                Root.localRotation = Quaternion.RotateTowards(Root.localRotation, Quaternion.Euler(0, yaw, 0), dt * 240);
            }
            float turn = Mathf.DeltaAngle(previousYaw, Root.localEulerAngles.y);
            previousYaw = Root.localEulerAngles.y;
            // 支撑阶段占一个步周期的七成；步频由实际位移决定，脚掌后移速度抵消身体前进速度。
            const float stance = .72f, strideLength = .64f;
            gaitPhase = Mathf.Repeat(gaitPhase + speed / (100 * Root.localScale.x) * dt * stance / strideLength, 1);
            float envelope = Mathf.SmoothStep(0, 1, progress / .18f) * Mathf.SmoothStep(0, 1, (1 - progress) / .18f);
            float rest = state == PetState.Lying || state == PetState.Drowsy ? envelope : 0;
            float alert = state == PetState.Alert || state == PetState.Guarding ? envelope : 0;
            float crouch = 0, leap = 0;
            if (state == PetState.Hunting)
            {
                // 前段伏低蓄势，中段腾身扑出，末段前爪落地并恢复四足支撑。
                crouch = (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.30f, .46f, progress))) * envelope;
                leap = Mathf.Sin(Mathf.InverseLerp(.35f, .78f, progress) * Mathf.PI);
            }
            float stretch = state == PetState.Stretching ? envelope : 0;
            // 伏低时用短促呼吸和轻晃表现兴奋；飞行时收爪并连续振翼。
            crouch = Mathf.Max(crouch, stalking * (.88f + Mathf.Sin(time * 12) * .08f));
            float carried = Mathf.Max(state == PetState.Carried ? 1 : 0, flying);
            float breath = Mathf.Sin(time * (rest > .1f ? 1.5f : 2.3f)) * .009f;
            var bodyPosition = new Vector3(0, -.1f - rest * .23f - crouch * .16f + leap * .22f + breath - organizingBend * .10f
                - sniff * .035f + swallow * .035f, .12f - leap * .18f + refusal * .035f);
            torso.localPosition = Vector3.Lerp(torso.localPosition, bodyPosition, blend);
            torso.localRotation = Quaternion.Slerp(torso.localRotation, Quaternion.Euler(stretch * 13 - leap * 12,
                Mathf.Sin(gaitPhase * Mathf.PI * 2) * 2 * walkBlend, Mathf.Sin(gaitPhase * Mathf.PI * 4) * 1.5f * walkBlend), blend);

            Vector3 localLook = Root.InverseTransformDirection(new Vector3(look.x, 0, look.y));
            float headYaw = look.sqrMagnitude < .01f ? 0 : Mathf.Clamp(Mathf.Atan2(-localLook.x, -localLook.z) * Mathf.Rad2Deg, -42, 42);
            Vector3 headPose = new Vector3(-look.y * 6 - alert * 16 + rest * 13 + crouch * 8, headYaw, 0);
            // 四足叼持保留行走支撑；拾起和放下时低头，不用被提起姿态代替搬运。
            headPose.x += organizingBend * 23 - organizingGrip * 6;
            headPose.y *= 1 - organizingGrip * .75f;
            if (state == PetState.Petted) headPose += new Vector3(-12, 0, -look.x * 8) * affection;
            if (state == PetState.Curious) headPose += new Vector3(likesHead ? -10 : 0, 0, -15) * envelope;
            if (state == PetState.Grooming) headPose += new Vector3(18, 30, -12) * envelope;
            if (state == PetState.Guarding) headPose.y += (touch == PetTouch.Tail ? -30 : 30) * envelope;
            if (state == PetState.Recovering) headPose.z = Mathf.Sin(progress * Mathf.PI * 6) * 8 * envelope;
            if (state == PetState.Startled) headPose.x += 18 * envelope;
            if (feeding)
            {
                // 嗅闻、咬入、咀嚼与吞咽各有独立节奏；失败段只退让摇头，不播放满意反馈。
                headPose *= 1 - feedEnvelope;
                headPose += new Vector3(sniff * 15 + bite * 7 + chewing * 4 - swallow * 15 - satisfied * 8,
                    Mathf.Sin(feed * Mathf.PI * 14) * refusal * 16, sniff * -5 + satisfied * 4);
            }
            head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(headPose), blend);
            float lean = state == PetState.Petted && touch == PetTouch.Head && likesHead ? affection * .04f : 0;
            head.localPosition = Vector3.Lerp(head.localPosition, new Vector3(0, .14f - rest * .09f + alert * .10f - stretch * .12f, -.68f - rest * .07f - lean), blend);
            tail.localRotation = Quaternion.Slerp(tail.localRotation, Quaternion.Euler(-leap * 15,
                Mathf.Sin(time * 2.2f) * (rest > .1f ? 3 : 6) + stalking * Mathf.Sin(time * 10) * 8 + Mathf.Sin(feed * Mathf.PI * 16) * satisfied * 12
                - Mathf.Clamp(turn * 3, -22, 22) + rest * 22 + alert * 15, 0), 1 - Mathf.Exp(-dt * 7));
            float blink = Mathf.Repeat(time, 4.7f);
            // 短暂保留闭合阶段，让缓动后的眼睑真正合拢，再自然睁开。
            float close = Mathf.SmoothStep(0, 1, (blink - 4.4f) / .09f);
            float reopen = Mathf.SmoothStep(0, 1, (blink - 4.62f) / .08f);
            float eyeOpen = Mathf.Lerp(1, -.02f, close * (1 - reopen));
            eyeOpen *= Mathf.Lerp(1, .45f, affection) * Mathf.Lerp(1, .45f, rest);
            if (state == PetState.Drowsy) eyeOpen = Mathf.Lerp(eyeOpen, -.02f, rest);
            if (feeding) eyeOpen *= 1 - chewing * .24f - swallow * .95f - satisfied * .55f;
            for (int i = 0; i < 2; i++)
            {
                float sign = i == 0 ? -1 : 1;
                wings[i].localRotation = Quaternion.Slerp(wings[i].localRotation,
                    Quaternion.Euler(65 + rest * 20 - alert * 45 - leap * 50 - flying * 60 - satisfied * 13,
                        sign * (25 + rest * 15 - alert * 25 - leap * 25 - flying * 25), sign * (-12 + flying * (35 + Mathf.Sin(time * 15) * 30))), blend);
                ears[i].localRotation = Quaternion.Slerp(ears[i].localRotation,
                    Quaternion.Euler(rest * 35 - alert * 18 - stalking * 14 - sniff * 12 + chewing * 6 + refusal * 18,
                        0, sign * (rest * 18 - alert * 10 + satisfied * 8)), blend);
                pupils[i].localPosition = Vector3.Lerp(pupils[i].localPosition,
                    new Vector3(Mathf.Clamp(localLook.x, -1, 1) * .016f, -look.y * .009f, 0), blend);
            }
            // 合眼快于睁眼，并让上下眼睑略有交叠，保证连续帧中也能完整遮住绿色虹膜。
            float lidBlend = eyeOpen < lidOpening ? 1 - Mathf.Exp(-dt * 55) : blend;
            UpdateEyelids(Mathf.Lerp(previousTime <= 0 ? 1 : lidOpening, eyeOpen, lidBlend));
            for (int i = 0; i < 4; i++)
            {
                Vector3 paw = PawHome(i);
                // 四足错峰抬起，大部分时间保留三足支撑；摆动脚先抬起再前送，支撑脚在局部后移。
                float phase = Mathf.Repeat(gaitPhase + (i < 2 ? i * .5f : i == 2 ? .75f : .25f), 1);
                float swing = Mathf.Clamp01((phase - stance) / (1 - stance));
                float footTravel = phase < stance ? Mathf.Lerp(-strideLength / 2, strideLength / 2, phase / stance)
                    : Mathf.Lerp(strideLength / 2, -strideLength / 2, Mathf.SmoothStep(0, 1, swing));
                paw.z += footTravel * walkBlend - (i < 2 ? stretch * .20f + leap * .22f : -crouch * .10f);
                paw.x *= 1 + rest * .22f + crouch * .12f;
                paw.y += Mathf.Sin(swing * Mathf.PI) * .15f * walkBlend + leap * (i < 2 ? .21f : .12f) + carried * (.16f + Mathf.Sin(time * 4 + i) * .05f);
                if (i == 0 && (state == PetState.Greeting || state == PetState.Curious || state == PetState.Grooming))
                    paw.y += envelope * (state == PetState.Greeting ? .24f + Mathf.Sin(progress * Mathf.PI * 8) * .06f : .17f);
                if (i == 0) { paw.y += organizingBend * .08f; paw.z -= organizingBend * .10f; }
                // 行走目标轨迹本身连续，着地时直接锁定脚掌；再做位置缓动会让脚掌一直追赶地面而滑步。
                paws[i].localPosition = speed > 1 ? paw : Vector3.Lerp(paws[i].localPosition, paw, blend);
                paws[i].localRotation = Quaternion.Slerp(paws[i].localRotation, Quaternion.Euler(-leap * 20 + carried * 25, 0, 0), blend);
                Vector3 shoulder = torso.localPosition + torso.localRotation * new Vector3((i % 2 == 0 ? -1 : 1) * .30f, -.04f, i < 2 ? -.40f : .47f);
                BendLeg(i, shoulder, paws[i].localPosition);
            }
        }

        private void BendLeg(int index, Vector3 shoulder, Vector3 foot)
        {
            // 两段肢体围绕肘/膝弯曲；卧躺时折叠，跨步时伸展，避免整根四肢像活塞一样伸缩。
            float upperLength = index < 2 ? .32f : .36f, lowerLength = .33f;
            Vector3 direction = (foot - shoulder).normalized;
            float distance = Mathf.Clamp(Vector3.Distance(shoulder, foot), .01f, upperLength + lowerLength - .001f);
            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2 * distance);
            Vector3 bend = Vector3.ProjectOnPlane(index < 2 ? Vector3.forward : Vector3.back, direction).normalized;
            Vector3 knee = shoulder + direction * along + bend * Mathf.Sqrt(Mathf.Max(0, upperLength * upperLength - along * along));
            PlaceBone(legs[index], shoulder, knee, index < 2 ? .22f : .29f);
            PlaceBone(shins[index], knee, foot, index < 2 ? .155f : .18f);
        }

        private static void PlaceBone(Transform bone, Vector3 start, Vector3 end, float width)
        {
            bone.localPosition = (start + end) * .5f;
            bone.localRotation = Quaternion.FromToRotation(Vector3.up, start - end);
            bone.localScale = new Vector3(width, Vector3.Distance(start, end) + width * .80f, width);
        }
    }
}
