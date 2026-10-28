using NUnit.Framework;

namespace ElecPet.Tests
{
    public sealed class PetSaveTests
    {
        [Test]
        public void SaveRecoversWithoutOverwritingProgress() { Assert.That(SaveChecks.Run(), Is.GreaterThan(10)); }
    }
}
