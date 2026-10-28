using System;

namespace ElecPet.Core
{
    [Serializable]
    public sealed class PetSaveData
    {
        public int Version;
        public double Familiarity, HeadPreference;

        public bool IsValid()
        {
            return Version == 1 && Unit(Familiarity) && Unit(HeadPreference);
        }

        public static PetSaveData Default()
        { return new PetSaveData { Version = 1 }; }

        private static bool Unit(double value) { return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0 && value <= 1; }
    }
}
