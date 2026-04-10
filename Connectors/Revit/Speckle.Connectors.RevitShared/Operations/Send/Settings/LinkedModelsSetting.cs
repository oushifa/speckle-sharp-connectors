using Speckle.Connectors.DUI.Settings;

namespace Speckle.Connectors.Revit.Operations.Send.Settings;

public class LinkedModelsSetting(bool value = LinkedModelsSetting.DEFAULT_VALUE) : ICardSetting
{
  public const string SETTING_ID = "includeLinkedModels";
  public const bool DEFAULT_VALUE = true;

  public string? Id { get; set; } = SETTING_ID;
  public string? Title { get; set; } = "是否包含链接模型";
  public string? Type { get; set; } = "boolean";
  public object? Value { get; set; } = value;
  public List<string>? Enum { get; set; }
}
