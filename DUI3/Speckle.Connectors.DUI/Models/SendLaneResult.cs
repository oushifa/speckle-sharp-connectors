namespace Speckle.Connectors.DUI.Models;

/// <summary>
/// 单条并行通道的执行结果：用于同一发送卡片下多路并行上传（例如 Revit 模型 + RVT）。
/// DUI 前端通过 <c>setModelSendLaneResults</c> 一次收到全部通道的汇总。
/// </summary>
/// <param name="TaskId">
/// 通道标识，与连接器约定一致（如 <c>speckleModel</c> / <c>speckleRvtFile</c>）。
/// </param>
/// <param name="Success">是否成功完成该通道。</param>
/// <param name="Detail">成功时的概要信息（如 versionId、fileId）；失败时通常为 <c>null</c>。</param>
/// <param name="ErrorMessage">失败时的格式化错误信息；成功时为 <c>null</c>。</param>
public sealed record SendLaneResult(
  string TaskId,
  bool Success,
  string? Detail,
  string? ErrorMessage
);
