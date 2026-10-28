using NUnit.Framework;

namespace ElecPet.Tests
{
    public sealed class ScreenCompanionTests
    {
        [Test] public void ContinuousObservationRespectsCooldownAndInvalidation() { Assert.That(ScreenCompanionChecks.Run(), Is.EqualTo(43)); }
    }
}
