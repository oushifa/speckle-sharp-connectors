using Speckle.Converters.Common;
using Speckle.Sdk.Common;
using Speckle.Sdk.Common.Exceptions;

namespace Speckle.Converters.RevitShared.Services;

#if REVIT2020
// Revit 2020 has no ForgeTypeId; length units are exposed as DisplayUnitType.
public sealed class RevitToSpeckleUnitConverter : IHostToSpeckleUnitConverter<DB.DisplayUnitType>
{
  private readonly Dictionary<DB.DisplayUnitType, string> _unitMapping = new();

  public RevitToSpeckleUnitConverter()
  {
    _unitMapping[DB.DisplayUnitType.DUT_MILLIMETERS] = Units.Millimeters;
    _unitMapping[DB.DisplayUnitType.DUT_CENTIMETERS] = Units.Centimeters;
    _unitMapping[DB.DisplayUnitType.DUT_METERS] = Units.Meters;
    _unitMapping[DB.DisplayUnitType.DUT_DECIMAL_INCHES] = Units.Inches;
    _unitMapping[DB.DisplayUnitType.DUT_FRACTIONAL_INCHES] = Units.Inches;
    _unitMapping[DB.DisplayUnitType.DUT_DECIMAL_FEET] = Units.Feet;
    _unitMapping[DB.DisplayUnitType.DUT_FEET_FRACTIONAL_INCHES] = Units.Feet;
  }

  // POC: maybe just convert, it's not a Try method
  public string ConvertOrThrow(DB.DisplayUnitType hostUnit)
  {
    if (_unitMapping.TryGetValue(hostUnit, out string? value))
    {
      return value;
    }

    string unitLabel = DB.LabelUtils.GetLabelFor(hostUnit);
    throw new UnitNotSupportedException(
      $"The Unit System \"{unitLabel}\" is unsupported. Please change your document's unit system and try again."
    );
  }
}
#else
public sealed class RevitToSpeckleUnitConverter : IHostToSpeckleUnitConverter<DB.ForgeTypeId>
{
  private readonly Dictionary<DB.ForgeTypeId, string> _unitMapping = new();

  public RevitToSpeckleUnitConverter()
  {
    _unitMapping[DB.UnitTypeId.Millimeters] = Units.Millimeters;
    _unitMapping[DB.UnitTypeId.Centimeters] = Units.Centimeters;
    _unitMapping[DB.UnitTypeId.Meters] = Units.Meters;
    _unitMapping[DB.UnitTypeId.MetersCentimeters] = Units.Meters;
    _unitMapping[DB.UnitTypeId.Inches] = Units.Inches;
    _unitMapping[DB.UnitTypeId.FractionalInches] = Units.Inches;
    _unitMapping[DB.UnitTypeId.Feet] = Units.Feet;
    _unitMapping[DB.UnitTypeId.FeetFractionalInches] = Units.Feet;
  }

  // POC: maybe just convert, it's not a Try method
  public string ConvertOrThrow(DB.ForgeTypeId hostUnit)
  {
    if (_unitMapping.TryGetValue(hostUnit, out string? value))
    {
      return value;
    }

    string unitLabel = DB.LabelUtils.GetLabelForUnit(hostUnit);
    throw new UnitNotSupportedException(
      $"The Unit System \"{unitLabel}\" is unsupported. Please change your document's unit system and try again."
    );
  }
}
#endif
