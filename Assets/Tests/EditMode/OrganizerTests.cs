using NUnit.Framework;
namespace ElecPet.Tests
{
    public sealed class OrganizerTests
    {
        [Test] public void AutomaticOrganizationWithRealWorkerAndSyntheticDesktop()
        { Assert.That(OrganizerChecks.Run(), Is.GreaterThanOrEqualTo(7)); }
    }
}
