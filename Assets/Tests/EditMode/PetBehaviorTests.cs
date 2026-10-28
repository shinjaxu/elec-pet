using NUnit.Framework;

namespace ElecPet.Tests
{
    public sealed class PetBehaviorTests
    {
        [Test]
        public void InteractionInterruptsAutonomyAndRecoversWithCooldown()
        {
            Assert.That(BehaviorChecks.Run(), Is.GreaterThan(20));
        }
    }
}
