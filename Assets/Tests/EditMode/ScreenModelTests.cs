using System;
using System.Reflection;
using ElecPet.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ElecPet.Tests
{
    public sealed class ScreenModelTests
    {
        [Serializable] private sealed class Request { public string model; public bool stream, think; public Message[] messages; }
        [Serializable] private sealed class Message { public string role, content; public string[] images; }

        [Test]
        public void ImageRequestPreservesTheImageAndUsesTheInstalledModel()
        {
            string json = (string)Call("CreateChat", new byte[] { 1, 2, 3, 255 });
            var request = JsonUtility.FromJson<Request>(json);
            Assert.That(request.model, Is.EqualTo("qwen3.5:4b"));
            Assert.That(request.stream, Is.False);
            Assert.That(request.think, Is.False);
            Assert.That(request.messages.Length, Is.EqualTo(2));
            Assert.That(request.messages[0].role, Is.EqualTo("system"));
            Assert.That(request.messages[1].images, Is.EqualTo(new[] { "AQID/w==" }));
        }

        [Test]
        public void ReadinessRequiresExplicitVisionCapability()
        {
            Assert.That(Call("HasVision", "{\"capabilities\":[\"completion\",\"vision\"]}"), Is.True);
            Assert.That(Call("HasVision", "{\"capabilities\":[\"completion\"]}"), Is.False);
            Assert.That(Call("HasVision", "{}"), Is.False);
        }

        [Test]
        public void OnlyCompletedVisibleContentBecomesAReply()
        {
            Assert.That(Call("ReadReply", "{\"done\":true,\"message\":{\"content\":\"  陪你看会儿。  \",\"thinking\":\"不要显示的思考\"}}"), Is.EqualTo("陪你看会儿。"));
            var error = Assert.Throws<TargetInvocationException>(() => Call("ReadReply", "{\"done\":false,\"message\":{\"content\":\"还没说完\"}}"));
            Assert.That(error.InnerException, Is.TypeOf<FormatException>());
        }

        [Test]
        public void SilenceAndEmptyContentDoNotBecomeBubbles()
        {
            Assert.That(Call("ReadReply", "{\"done\":true,\"message\":{\"content\":\" \\n[安静] \\n\"}}"), Is.EqualTo(""));
            Assert.That(Call("ReadReply", "{\"done\":true,\"message\":{\"content\":\"\"}}"), Is.EqualTo(""));
        }

        [Test]
        public void LongRepliesAreBoundedWithoutSplittingAnEmoji()
        {
            string reply = new string('看', 79) + "🐉再陪你一会儿";
            string json = "{\"done\":true,\"message\":{\"content\":\"" + reply + "\"}}";
            string actual = (string)Call("ReadReply", json);
            Assert.That(actual, Is.EqualTo(new string('看', 79)));
        }

        private static object Call(string method, object argument)
        {
            var type = typeof(DesktopPetController).Assembly.GetType("ElecPet.Runtime.OllamaCompanionProtocol");
            Assert.That(type, Is.Not.Null, "本地视觉模型协议尚未实现");
            return type.GetMethod(method).Invoke(null, new[] { argument });
        }
    }
}
