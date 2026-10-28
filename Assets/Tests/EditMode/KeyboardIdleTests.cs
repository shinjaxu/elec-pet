using NUnit.Framework;
namespace ElecPet.Tests
{
    public sealed class KeyboardIdleTests
    {
        [Test] public void OnlyKeyboardResetsThirtySecondClock() { Assert.That(KeyboardIdleChecks.Run(), Is.EqualTo(5)); }
    }
}
