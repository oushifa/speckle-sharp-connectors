using Speckle.Connectors.Common.Conversion;
using Speckle.Connectors.DUI.Bridge;
using Speckle.Connectors.DUI.Models;

namespace Speckle.Connectors.DUI.Bindings;

public interface ISendBindingUICommands
{
  Task RefreshSendFilters();

  Task SetModelsExpired(IEnumerable<string> expiredModelIds);
  Task SetModelError(string modelCardId, Exception exception);

  Task SetModelSendResult(
    string modelCardId,
    string versionId,
    IEnumerable<SendConversionResult> sendConversionResults
  );

  /// <summary>
  /// 多通道并行上传场景下，各并行任务的执行结果（支持部分成功、部分跳过）。
  /// 对应 DUI 命令 <c>setModelSendLaneResults</c>。
  /// </summary>
  /// <param name="modelCardId">发送卡片的标识。</param>
  /// <param name="laneResults">各通道的 <see cref="SendLaneResult"/> 列表。</param>
  Task SetModelSendLaneResults(string modelCardId, IReadOnlyList<SendLaneResult> laneResults);

  IBrowserBridge Bridge { get; }
}
