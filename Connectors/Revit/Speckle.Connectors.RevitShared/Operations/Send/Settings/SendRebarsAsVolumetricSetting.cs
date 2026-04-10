using Speckle.Connectors.DUI.Settings;

namespace Speckle.Connectors.Revit.Operations.Send.Settings;

public class SendRebarsAsVolumetricSetting(bool value = SendRebarsAsVolumetricSetting.DEFAULT_VALUE) : ICardSetting
{
  public const string SETTING_ID = "sendRebarsAsVolumetric";
  public const bool DEFAULT_VALUE = false;

  public string? Id { get; set; } = SETTING_ID;
  public string? Title { get; set; } = "是否发送钢筋作为体积（禁用以提高性能）";
  public string? Type { get; set; } = "boolean";
  public object? Value { get; set; } = value;
  public List<string>? Enum { get; set; }
}
