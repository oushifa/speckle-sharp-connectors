using Speckle.InterfaceGenerator;

namespace Speckle.Converters.RevitShared;

[GenerateAutoInterface]
public class RevitVersionConversionHelper : IRevitVersionConversionHelper
{
  public bool IsCurveClosed(DB.NurbSpline nurbsSpline)
  {
    try
    {
#if REVIT2020
      // Revit 2020 exposes this property with a lowercase 'i' (renamed to IsClosed in 2021).
      return nurbsSpline.isClosed;
#else
      return nurbsSpline.IsClosed;
#endif
    }
    catch (Autodesk.Revit.Exceptions.ApplicationException)
    {
      // POC: is this actually a good assumption?
      return true;
    }
  }
}
