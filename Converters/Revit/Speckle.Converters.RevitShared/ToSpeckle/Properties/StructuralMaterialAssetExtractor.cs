using Speckle.Converters.Common;
using Speckle.Converters.RevitShared.Services;
using Speckle.Converters.RevitShared.Settings;
using Speckle.Sdk;
using Speckle.Sdk.Common;

namespace Speckle.Converters.RevitShared.ToSpeckle.Properties;

public readonly struct StructuralAssetProperties(
  string name,
  double density,
#if REVIT2020
  DB.DisplayUnitType densityUnitId,
#else
  DB.ForgeTypeId densityUnitId,
#endif
  string materialType,
  double? compressiveStrength,
#if REVIT2020
  DB.DisplayUnitType? compressiveStrengthUnitId
#else
  DB.ForgeTypeId? compressiveStrengthUnitId
#endif
)
{
  public string Name { get; } = name;
  public double Density { get; } = density;
#if REVIT2020
  public DB.DisplayUnitType DensityUnitId { get; } = densityUnitId;
  public string MaterialType { get; } = materialType;
  public double? CompressiveStrength { get; } = compressiveStrength;
  public DB.DisplayUnitType? CompressiveStrengthUnitId { get; } = compressiveStrengthUnitId;
#else
  public DB.ForgeTypeId DensityUnitId { get; } = densityUnitId;
  public string MaterialType { get; } = materialType;
  public double? CompressiveStrength { get; } = compressiveStrength;
  public DB.ForgeTypeId? CompressiveStrengthUnitId { get; } = compressiveStrengthUnitId;
#endif
}

public class StructuralMaterialAssetExtractor
{
  private readonly ScalingServiceToSpeckle _scalingService;
  private readonly IConverterSettingsStore<RevitConversionSettings> _converterSettings;
  private readonly Dictionary<string, StructuralAssetProperties> _structuralAssetCache = new();

  public StructuralMaterialAssetExtractor(
    ScalingServiceToSpeckle scalingService,
    IConverterSettingsStore<RevitConversionSettings> converterSettings
  )
  {
    _scalingService = scalingService;
    _converterSettings = converterSettings;
  }

  /// <summary>
  /// Attempts to get structural asset properties, using cached values if available.
  /// </summary>
  public StructuralAssetProperties TryGetProperties(DB.ElementId assetId)
  {
    // ensure safe string conversion
    string assetIdString = assetId.ToString().NotNull();

    // check cache if properties have already been extracted
    if (_structuralAssetCache.TryGetValue(assetIdString, out var cachedProperties))
    {
      return cachedProperties;
    }

    // if not in cache but structural asset id is valid => extract properties
    var extractedProperties = ExtractProperties(assetId);
    _structuralAssetCache[assetIdString] = extractedProperties;
    return extractedProperties;
  }

  /// <summary>
  /// Gets the material properties from a structural asset including density, material type,
  /// and material-specific properties like compressive strength for concrete.
  /// </summary>
  /// <remarks>
  /// All values are scaled from internal units to model units
  /// </remarks>
  private StructuralAssetProperties ExtractProperties(DB.ElementId structuralAssetId)
  {
    // NOTE: assetId != DB.ElementId.InvalidElementId checked in calling method. Assuming a valid StructuralAssetId
    if (
      _converterSettings.Current.Document.GetElement(structuralAssetId) is not DB.PropertySetElement propertySetElement
    )
    {
      throw new SpeckleException("Structural material asset is not of expected type.");
    }
    DB.StructuralAsset structuralAsset = propertySetElement.GetStructuralAsset();

    // get unit forge type id
#if REVIT2020
    DB.DisplayUnitType densityUnitId = _converterSettings
      .Current.Document.GetUnits()
      .GetFormatOptions(DB.UnitType.UT_MassDensity)
      .DisplayUnits;
#else
    DB.ForgeTypeId densityUnitId = _converterSettings
      .Current.Document.GetUnits()
      .GetFormatOptions(DB.SpecTypeId.MassDensity)
      .GetUnitTypeId();
#endif

    // scale from internal to model units
    double densityValue = _scalingService.Scale(structuralAsset.Density, densityUnitId);

    // get material type
    string materialType = structuralAsset.StructuralAssetClass.ToString();

    // initialize optional concrete properties
    double? compressiveStrength = null;
#if REVIT2020
    DB.DisplayUnitType? stressUnitId = null;
#else
    DB.ForgeTypeId? stressUnitId = null;
#endif

    // if concrete, extract compressive strength
    if (materialType == DB.StructuralAssetClass.Concrete.ToString())
    {
#if REVIT2020
      stressUnitId = _converterSettings
        .Current.Document.GetUnits()
        .GetFormatOptions(DB.UnitType.UT_Stress)
        .DisplayUnits;
#else
      stressUnitId = _converterSettings
        .Current.Document.GetUnits()
        .GetFormatOptions(DB.SpecTypeId.AreaForce)
        .GetUnitTypeId();
#endif

#if REVIT2020
      // stressUnitId is guaranteed to be set within this block, so .Value is safe.
      compressiveStrength = _scalingService.Scale(structuralAsset.ConcreteCompression, stressUnitId.Value);
#else
      compressiveStrength = _scalingService.Scale(structuralAsset.ConcreteCompression, stressUnitId);
#endif
    }

    // return value and units
    return new StructuralAssetProperties(
      name: structuralAsset.Name,
      density: densityValue,
      densityUnitId: densityUnitId,
      materialType: materialType,
      compressiveStrength: compressiveStrength,
      compressiveStrengthUnitId: stressUnitId
    );
  }
}
