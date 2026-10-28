using NUnit.Framework;
namespace ElecPet.Tests
{
    public sealed class IconWorkerTests
    {
        [Test] public void CancellationAndBoundedShellQueue() { Assert.That(IconWorkerChecks.Run(), Is.GreaterThanOrEqualTo(8)); }
    }
}
