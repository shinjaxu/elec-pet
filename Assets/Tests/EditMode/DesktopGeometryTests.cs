using NUnit.Framework;

namespace ElecPet.Tests
{
    public sealed class DesktopGeometryTests
    {
        [Test]
        public void OffsetMonitorsKeepThePetVisibleAndAllowCrossing()
        {
            Assert.That(CoreChecks.RunMultiMonitor(), Is.EqualTo(9009));
        }

        [Test]
        public void DesktopMotionAndCoordinateScenarios()
        {
            Assert.That(CoreChecks.Run(), Is.EqualTo(16));
        }
    }
}
