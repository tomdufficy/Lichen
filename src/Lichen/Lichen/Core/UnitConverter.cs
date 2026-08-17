using Rhino;

namespace Lichen.Core
{
    internal static class UnitConverter
    {
        public static double MillimetersToModel(RhinoDoc doc, double millimeters)
        {
            return millimeters * RhinoMath.UnitScale(
                UnitSystem.Millimeters,
                doc.ModelUnitSystem);
        }

        public static double ScaleBetween(UnitSystem sourceUnits, UnitSystem targetUnits)
        {
            return RhinoMath.UnitScale(sourceUnits, targetUnits);
        }
    }
}