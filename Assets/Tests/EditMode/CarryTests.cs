using NUnit.Framework;
namespace ElecPet.Tests
{
    public sealed class CarryTests
    {
        [Test] public void IdleGatedContinuousCarry() { Assert.That(CarryChecks.Run(), Is.GreaterThan(16)); }
    }
}
