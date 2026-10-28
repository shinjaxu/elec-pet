using System;
using System.Reflection;
using ElecPet.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ElecPet.Tests
{
    public sealed class FeedingPresentationTests
    {
        [Test]
        public void FeedingOpensTheJawAndRestoresTheOriginalClosedFace()
        {
            var pet = new PrototypePet();
            try
            {
                AnimateFeeding(pet, .25f);
                var head = pet.Root.Find("躯干/注视点");
                var jaw = head.Find("进食下颌");
                Assert.That(jaw, Is.Not.Null, "咬入必须具有独立下颌，不能让文件穿过封闭头部");
                Assert.That(Quaternion.Angle(jaw.localRotation, Quaternion.identity), Is.GreaterThan(8));
                Assert.That(head.Find("宽扁头部").GetComponent<Renderer>().enabled, Is.False,
                    "开口时原始封闭脸部不能继续挡住口腔");
                Assert.That(head.Find("进食上颌").gameObject.activeSelf, Is.True);
                Assert.That(head.Find("口腔暗部").gameObject.activeSelf, Is.True);
                Assert.That(head.Find("嘴线").GetComponent<Renderer>().enabled, Is.False);
                float upperLip = float.PositiveInfinity, lowerLip = float.NegativeInfinity;
                foreach (var vertex in head.Find("进食上颌").GetComponent<MeshFilter>().sharedMesh.vertices)
                    if (Mathf.Abs(vertex.x) < .08f && vertex.z < -.3f) upperLip = Mathf.Min(upperLip, vertex.y);
                var shell = jaw.Find("下颌外壳").GetComponent<MeshFilter>();
                foreach (var vertex in shell.sharedMesh.vertices)
                {
                    Vector3 point = head.InverseTransformPoint(shell.transform.TransformPoint(vertex));
                    if (Mathf.Abs(point.x) < .08f && point.z < -.3f) lowerLip = Mathf.Max(lowerLip, point.y);
                }
                Assert.That(upperLip - lowerLip, Is.GreaterThan(.06f), "上下颌前缘必须存在实际几何开口");
                float anchorHeight = head.InverseTransformPoint(pet.CarryAnchor).y;
                Assert.That(anchorHeight, Is.LessThan(upperLip).And.GreaterThan(lowerLip),
                    "咬入锚点必须位于真实开口内，不能继续使用鼻吻上方的整理图标锚点");

                AnimateFeeding(pet, -1, 2);
                Assert.That(head.Find("宽扁头部").GetComponent<Renderer>().enabled, Is.True);
                Assert.That(head.Find("嘴线").GetComponent<Renderer>().enabled, Is.True);
                Assert.That(jaw.gameObject.activeSelf, Is.False);
                Assert.That(head.Find("进食上颌").gameObject.activeSelf, Is.False);
                Assert.That(head.Find("口腔暗部").gameObject.activeSelf, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(pet.Root.gameObject); }
        }

        [Test]
        public void FeedingKeepsSupportPawsOnTheGroundAndTheFaceInsideTheDesktopMargin()
        {
            var pet = new PrototypePet();
            try
            {
                float time = 0;
                foreach (bool failed in new[] { false, true })
                for (int frame = 0; frame <= 150; frame++)
                {
                    AnimateFeeding(pet, frame / 150f, time += 1f / 30, failed, 1);
                    foreach (var part in pet.Root.GetComponentsInChildren<Transform>())
                        if (part.name == "前爪" || part.name == "后爪")
                            Assert.That(part.localPosition.y, Is.EqualTo(-.58f).Within(.001f),
                                "原地进食时四足保持支撑");
                    foreach (var renderer in pet.Root.GetComponentsInChildren<Renderer>())
                    {
                        if (!renderer.enabled) continue;
                        Assert.That(renderer.bounds.min.x, Is.GreaterThanOrEqualTo(-.88f), renderer.name);
                        Assert.That(renderer.bounds.max.x, Is.LessThanOrEqualTo(.88f), renderer.name);
                        Assert.That(renderer.bounds.min.y, Is.GreaterThanOrEqualTo(-.88f), renderer.name);
                        Assert.That(renderer.bounds.max.y, Is.LessThanOrEqualTo(.88f), renderer.name);
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(pet.Root.gameObject); }
        }

        [Test]
        public void FoodProxyFollowsTheMouthAndKeepsFailedFoodVisible()
        {
            var type = typeof(PrototypePet).Assembly.GetType("ElecPet.Runtime.PetFoodVisual");
            Assert.That(type, Is.Not.Null, "喂食代理尚未实现");
            var visual = (IDisposable)Activator.CreateInstance(type);
            var pet = new PrototypePet();
            var host = new GameObject("喂食代理测试相机");
            var camera = host.AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 80, -80);
            camera.transform.LookAt(Vector3.zero);
            try
            {
                var present = type.GetMethod("Present");
                foreach (string extension in new[] { ".png", ".mp4", ".txt" })
                {
                    pet.Root.position += new Vector3(3, 0, 2);
                    AnimateFeeding(pet, .25f);
                    present.Invoke(visual, new object[] { pet, camera, .25f, extension, false });
                    var root = (Transform)type.GetField("root", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(visual);
                    Assert.That(root.gameObject.activeSelf, Is.True);
                    Assert.That(Vector3.Distance(root.position, pet.CarryAnchor), Is.LessThan(.7f),
                        "文件代理应跟随当前帧嘴部，而非固定屏幕坐标");
                    present.Invoke(visual, new object[] { pet, camera, .9f, extension, true });
                    var card = root.Find("食物卡片");
                    Assert.That(card.gameObject.activeSelf, Is.True, "回收失败必须把代理退回，不能继续显示吞食成功");
                    present.Invoke(visual, new object[] { pet, camera, -1f, extension, false });
                    Assert.That(root.gameObject.activeSelf, Is.False);
                }
            }
            finally
            {
                visual.Dispose();
                UnityEngine.Object.DestroyImmediate(pet.Root.gameObject);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static void AnimateFeeding(PrototypePet pet, float progress, float time = 1, bool failed = false, int frames = 20)
        {
            // 通过反射允许新增接口之前编译测试，缺失接口应作为明确的功能失败呈现。
            MethodInfo method = typeof(PrototypePet).GetMethod("Animate");
            ParameterInfo[] parameters = method.GetParameters();
            int progressIndex = Array.FindIndex(parameters, parameter => parameter.Name == "feedingProgress");
            Assert.That(progressIndex, Is.GreaterThanOrEqualTo(0), "模型尚未接入喂食进度");
            var args = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++) args[i] = parameters[i].DefaultValue;
            args[0] = Vector2.zero; args[2] = false; args[3] = 0f;
            args[progressIndex] = progress;
            args[progressIndex + 1] = failed;
            for (int frame = 0; frame < frames; frame++)
            {
                args[1] = time + frame / 30f;
                method.Invoke(pet, args);
            }
        }
    }
}
