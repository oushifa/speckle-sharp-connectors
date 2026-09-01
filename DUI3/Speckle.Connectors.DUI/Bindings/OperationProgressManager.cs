using System.Collections.Concurrent;
using Speckle.Connectors.Common.Operations;
using Speckle.Connectors.DUI.Bridge;
using Speckle.Connectors.DUI.Models.Card;
using Speckle.InterfaceGenerator;

namespace Speckle.Connectors.DUI.Bindings;

/// <summary>
/// Debouncing progress for every %1 update for UI.
/// This class requires a specific bridge in its binding, so registering it will create random bridge which we don't want to.
/// </summary>
[GenerateAutoInterface]
public class OperationProgressManager : IOperationProgressManager
{
  /// <summary>
  /// 将进度上报委托到连接器线程上下文；避免宿主在非 UI 线程直接触碰桥接实现。
  /// </summary>
  private sealed class NonUIThreadProgress<T>(Action<T> handler) : IProgress<T>
  {
    /// <summary>触发构造时注册的回调，将进度值透出给宿主线程上的 <see cref="SetModelProgress"/> 等逻辑。</summary>
    /// <param name="value">当前进度快照。</param>
    public void Report(T value) => handler(value);
  }

  private const string SET_MODEL_PROGRESS_UI_COMMAND_NAME = "setModelProgress";
  private const string SET_MODEL_TASK_PROGRESS_UI_COMMAND_NAME = "setModelTaskProgress";
  private static readonly ConcurrentDictionary<string, (DateTime lastCallTime, string status)> s_lastProgressValues = new();

  /// <remarks>使用复合键（modelCardId + taskId），避免并行通道之间相互节流。</remarks>
  private static readonly ConcurrentDictionary<
    string,
    (DateTime lastCallTime, string status)
  > s_lastTaskLaneProgressValues = new();

  private const int THROTTLE_INTERVAL_MS = 200;

  /// <summary>
  /// 封装「发送到 DUI 的总体进度」：<see cref="SetModelProgress"/> 负责合并与节流。
  /// </summary>
  /// <param name="bridge">当前连接器绑定的浏览器桥。</param>
  /// <param name="modelCardId">发送卡片 ID。</param>
  /// <param name="cancellationToken">取消后不再上报。</param>
  /// <param name="mirrorTaskId">
  /// 若指定，则每次 <c>Report</c> 除 <see cref="SetModelProgress"/> 外，还以相同 <c>status</c>/<c>progress</c> 调用
  /// <see cref="SetModelTaskProgress"/>，供多次并行发送时按车道展示与单通道一致的中间过程（如 Converting）。
  /// </param>
  /// <returns>可在后台线程反复 <c>Report</c> 的进度适配器。</returns>
  public IProgress<CardProgress> CreateOperationProgressEventHandler(
    IBrowserBridge bridge,
    string modelCardId,
    CancellationToken cancellationToken,
    string? mirrorTaskId = null
  )
  {
    var progress = new NonUIThreadProgress<CardProgress>(args =>
    {
      SetModelProgress(
        bridge,
        modelCardId,
        new ModelCardProgress(modelCardId, args.Status, args.Progress),
        cancellationToken
      );

      if (mirrorTaskId is not null)
      {
        SetModelTaskProgress(
          bridge,
          modelCardId,
          mirrorTaskId,
          args.Status,
          args.Progress,
          cancellationToken
        );
      }
    });
    return progress;
  }

  /// <summary>向 DUI 发送「整张卡片」的聚合进度消息（命令 <c>setModelProgress</c>），并按时间与状态合并重复调用。</summary>
  public void SetModelProgress(
    IBrowserBridge bridge,
    string modelCardId,
    ModelCardProgress progress,
    CancellationToken cancellationToken
  )
  {
    if (cancellationToken.IsCancellationRequested)
    {
      return;
    }

    // 首张进度：不设节流阈值，立刻推送到 DUI。
    if (!s_lastProgressValues.TryGetValue(modelCardId, out (DateTime, string) t))
    {
      t.Item1 = DateTime.Now;
      s_lastProgressValues[modelCardId] = (t.Item1, progress.Status);
      // Since it's the first time we get a call for this model card, we should send it out
      SendProgress(bridge, modelCardId, progress);
      return;
    }

    // 后续上报：在时间窗口内若状态文案未变则丢弃（减轻 Web 侧压力）。
    var currentTime = DateTime.Now;
    var elapsedMs = (currentTime - t.Item1).TotalMilliseconds;

    if (elapsedMs < THROTTLE_INTERVAL_MS && t.Item2 == progress.Status)
    {
      return;
    }
    Console.WriteLine($"Progress: {progress.Status} - {progress.Progress}");
    s_lastProgressValues[modelCardId] = (currentTime, progress.Status);
    SendProgress(bridge, modelCardId, progress);
  }

  /// <summary>
  /// 并行多通道场景的「按任务粒度」进度：命令 <c>setModelTaskProgress</c>，键为 (<paramref name="modelCardId"/>, <paramref name="taskId"/>)。
  /// </summary>
  /// <param name="bridge">浏览器桥。</param>
  /// <param name="modelCardId">发送卡片 ID。</param>
  /// <param name="taskId">并行通道 ID（参见 Revit <c>RevitMultiSendTaskIds</c> 等）。</param>
  /// <param name="status">可读状态文案。</param>
  /// <param name="progress">0～1 归一进度；不确定进度时可 <c>null</c>。</param>
  /// <param name="cancellationToken">取消后不再发送。</param>
  /// <param name="part">当前分片序号（从 1 起）。</param>
  /// <param name="totalParts">分片总数；与 <paramref name="part"/> 同时存在时表示分片粒度更新。</param>
  /// <param name="forceSend">为 <c>true</c> 时跳过时间节流（用于起止态、里程碑）。</param>
  /// <remarks>
  /// 当 <paramref name="part"/> 与 <paramref name="totalParts"/> 均有值时，视为分片进度，按计划每片都推送；否则复用与时间/状态组合的节流逻辑。
  /// </remarks>
  public void SetModelTaskProgress(
    IBrowserBridge bridge,
    string modelCardId,
    string taskId,
    string status,
    double? progress,
    CancellationToken cancellationToken,
    int? part = null,
    int? totalParts = null,
    bool forceSend = false
  )
  {
    if (cancellationToken.IsCancellationRequested)
    {
      return;
    }

    var throttleKey = $"{modelCardId}\u001f{taskId}";
    bool isChunkSizedUpdate = part is not null && totalParts is not null;

    // 分片进度：按计划每片都通知前端，不做时间节流。
    if (!forceSend && !isChunkSizedUpdate && s_lastTaskLaneProgressValues.TryGetValue(throttleKey, out (DateTime, string) prior))
    {
      var elapsedMs = (DateTime.Now - prior.Item1).TotalMilliseconds;
      if (elapsedMs < THROTTLE_INTERVAL_MS && prior.Item2 == status)
      {
        return;
      }
    }

    s_lastTaskLaneProgressValues[throttleKey] = (DateTime.Now, status);
    var payload = new { modelCardId, taskId, status, progress, part, totalParts };
    // 发往 DUI：载荷含分片序号时前端可做「第 N/M 片」展示。
    bridge.SendProgress(SET_MODEL_TASK_PROGRESS_UI_COMMAND_NAME, payload, cancellationToken);
  }

  /// <summary>实际写出 <c>setModelProgress</c> JSON 载荷。</summary>
  private static void SendProgress(IBrowserBridge bridge, string modelCardId, ModelCardProgress progress) =>
    bridge.SendProgress(SET_MODEL_PROGRESS_UI_COMMAND_NAME, new { modelCardId, progress });
}
