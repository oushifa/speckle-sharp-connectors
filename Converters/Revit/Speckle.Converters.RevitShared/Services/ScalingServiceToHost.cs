using Autodesk.Revit.DB;
using Speckle.Sdk.Common.Exceptions;

namespace Speckle.Converters.RevitShared.Services;

public sealed class ScalingServiceToHost
{
  public double ScaleToNative(double value, string units)
  {
    if (string.IsNullOrEmpty(units))
    {
      return value;
    }

    return ScaleToNative(value, UnitsToNative(units));
  }

#if REVIT2020
  public double ScaleToNative(double value, DisplayUnitType displayUnitType)
  {
    return UnitUtils.ConvertToInternalUnits(value, displayUnitType);
  }

  /// <exception cref="UnitNotSupportedException">Throws if unit is not supported</exception>
  public DisplayUnitType UnitsToNative(string units)
  {
    var u = Sdk.Common.Units.GetUnitsFromString(units);

    return u switch
    {
      Sdk.Common.Units.Millimeters => DisplayUnitType.DUT_MILLIMETERS,
      Sdk.Common.Units.Centimeters => DisplayUnitType.DUT_CENTIMETERS,
      Sdk.Common.Units.Meters => DisplayUnitType.DUT_METERS,
      Sdk.Common.Units.Inches => DisplayUnitType.DUT_DECIMAL_INCHES,
      Sdk.Common.Units.Feet => DisplayUnitType.DUT_DECIMAL_FEET,
      _ => throw new UnitNotSupportedException($"The Unit System \"{units}\" is unsupported."),
    };
  }
#else
  public double ScaleToNative(double value, ForgeTypeId typeId)
  {
    return UnitUtils.ConvertToInternalUnits(value, typeId);
  }

  /// <exception cref="UnitNotSupportedException">Throws if unit is not supported</exception>
  public ForgeTypeId UnitsToNative(string units)
  {
    var u = Sdk.Common.Units.GetUnitsFromString(units);

    return u switch
    {
      Sdk.Common.Units.Millimeters => UnitTypeId.Millimeters,
      Sdk.Common.Units.Centimeters => UnitTypeId.Centimeters,
      Sdk.Common.Units.Meters => UnitTypeId.Meters,
      Sdk.Common.Units.Inches => UnitTypeId.Inches,
      Sdk.Common.Units.Feet => UnitTypeId.Feet,
      _ => throw new UnitNotSupportedException($"The Unit System \"{units}\" is unsupported."),
    };
  }
#endif
}
