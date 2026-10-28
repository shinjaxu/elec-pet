using NUnit.Framework;

namespace ElecPet.Tests
{
    public sealed class PetLifeTests
    {
        [Test]
        public void LifeRespectsOwnerRestCooldownAndInterruption() { Assert.That(LifeChecks.Run(), Is.GreaterThan(5)); }
    }
}
