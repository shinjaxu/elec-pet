using NUnit.Framework;
namespace ElecPet.Tests
{
    public sealed class OrganizationTests
    {
        [Test] public void StableLayoutAndOccupiedSlots() { Assert.That(OrganizationChecks.Run(), Is.GreaterThanOrEqualTo(20)); }
        [Test] public void TwelveIconsFormACenteredStableHeart() { OrganizationChecks.HeartOutlineIsCenteredAndIdempotent(); }
        [Test] public void OverflowUsesNamesAndLeavesTheHeartEmpty() { OrganizationChecks.OverflowPreservesNameOrderAndHeartInterior(); }
        [Test] public void SparseHeartsKeepSymmetry() { OrganizationChecks.SparseHeartsRemainSymmetric(); }
        [Test] public void HeartSwapsDoNotOverlapIcons() { OrganizationChecks.HeartCyclesUseRealVacantCells(); }
        [Test] public void HeartCapacityIsCheckedBeforeMoving() { OrganizationChecks.HeartCapacityFailsBeforeMoving(); }
        [Test] public void SmallDesktopsAndSecondScreenRemainSafe() { OrganizationChecks.SmallLayoutsKeepSafeColumnOrder(); }
    }
}
