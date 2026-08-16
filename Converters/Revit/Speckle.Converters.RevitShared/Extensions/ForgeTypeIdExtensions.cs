using Autodesk.Revit.DB;

namespace Speckle.Converters.RevitShared.Extensions;

#if REVIT2020
// Revit 2020 has no ForgeTypeId; symbols/units are handled via DisplayUnitType.
public static class ForgeTypeIdExtensions
{
  public static string? GetSymbol(this DisplayUnitType displayUnitType)
  {
    if (!FormatOptions.CanHaveUnitSymbol(displayUnitType))
    {
      return null;
    }
    var validSymbols = FormatOptions.GetValidUnitSymbols(displayUnitType);
    foreach (UnitSymbolType symbolId in validSymbols)
    {
      return LabelUtils.GetLabelFor(symbolId);
    }
    return null;
  }

  public static string ToUniqueString(this DisplayUnitType displayUnitType)
  {
    return displayUnitType.ToString();
  }
}
#else
public static class ForgeTypeIdExtensions
{
  public static string? GetSymbol(this ForgeTypeId forgeTypeId)
  {
    if (!FormatOptions.CanHaveSymbol(forgeTypeId))
    {
      return null;
    }
    var validSymbols = FormatOptions.GetValidSymbols(forgeTypeId);
    var typeId = validSymbols.Where(x => !x.Empty());
    foreach (DB.ForgeTypeId symbolId in typeId)
    {
      return LabelUtils.GetLabelForSymbol(symbolId);
    }
    return null;
  }

  public static string ToUniqueString(this ForgeTypeId forgeTypeId)
  {
    return forgeTypeId.TypeId;
  }
}
#endif
