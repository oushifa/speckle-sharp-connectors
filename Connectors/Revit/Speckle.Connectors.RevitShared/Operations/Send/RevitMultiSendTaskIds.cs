namespace Speckle.Connectors.Revit.Operations.Send;

/// <summary>
/// Revit 多次并行发送各通道的稳定标识字符串；需与 DUI / Web 前端订阅的 <c>taskId</c> 保持一致。
/// </summary>
public static class RevitMultiSendTaskIds
{
  /// <summary>几何与属性经 Speckle 常规 Send 流水线写入的版本（主车道）。</summary>
  public const string SpeckleModel = "speckleModel";

  /// <summary>磁盘 *.rvt 经 Speckle FileImport（预签名 + PUT）上传并触发导入。</summary>
  public const string SpeckleRvtFile = "speckleRvtFile";
}
