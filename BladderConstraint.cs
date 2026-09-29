using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace BladderMin
{
    public struct BladderConstraintResult
    {
        public bool IsMet { get; set; }
        public double Volume { get; set; }
    }
    public class BladderConstraint
    {
        public string Name { get; private set; }
        public DoseValue Dose { get; private set; }
        public double ConstraintVolume { get; private set; }

        public VolumePresentation ConstraintVolumeRepresentation { get; private set; }
        public BladderConstraint(string name, DoseValue dose, VolumePresentation volumePresentation, double constraintVolume)
        {
            Name = name;
            Dose = dose;
            ConstraintVolumeRepresentation = volumePresentation;
            ConstraintVolume = constraintVolume;
        }

        //A tuple to store a the bladdermin volume and whether it is less than the minimum volume constraint or not.
        public (bool, double) GetVolumeAtConstraint(PlanningItem p, Structure s)
        {
            var vol = p.GetVolumeAtDose(s, Dose, ConstraintVolumeRepresentation);
            if (vol < ConstraintVolume)
                return (true, vol);
            else
                return (false, vol);
        }
    }
}
