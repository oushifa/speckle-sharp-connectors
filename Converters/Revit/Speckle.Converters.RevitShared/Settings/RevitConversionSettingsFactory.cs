using Speckle.Converters.Common;
using Speckle.Converters.RevitShared.Helpers;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Common;

namespace Speckle.Converters.RevitShared.Settings;

[GenerateAutoInterface]
public class RevitConversionSettingsFactory(
  RevitContext revitContext,
#if REVIT2020
  IHostToSpeckleUnitConverter<DB.DisplayUnitType> unitConverter
#else
  IHostToSpeckleUnitConverter<DB.ForgeTypeId> unitConverter
#endif
) : IRevitConversionSettingsFactory
{
  public RevitConversionSettings Create(
    DetailLevelType detailLevelType,
    DB.Transform? referencePointTransform,
    bool sendEmptyOrNullParams,
    bool sendLinkedModels,
    bool sendRebarsAsVolumetric,
    bool sendAreasAsMesh,
    double tolerance = 0.0164042 // 5mm in ft
  )
  {
    var document = revitContext.UIApplication.NotNull().ActiveUIDocument.Document;
    return new(
      document,
      detailLevelType,
      referencePointTransform,
#if REVIT2020
      unitConverter.ConvertOrThrow(document.GetUnits().GetFormatOptions(DB.UnitType.UT_Length).DisplayUnits),
#else
      unitConverter.ConvertOrThrow(document.GetUnits().GetFormatOptions(DB.SpecTypeId.Length).GetUnitTypeId()),
#endif
      sendEmptyOrNullParams,
      sendLinkedModels,
      sendRebarsAsVolumetric,
      sendAreasAsMesh,
      tolerance
    );
  }
}
