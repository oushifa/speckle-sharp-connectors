using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Speckle.Connectors.Common.Cancellation;
using Speckle.Connectors.Common.Extensions;
using Speckle.Connectors.Common.Operations;
using Speckle.Connectors.DUI.Bindings;
using Speckle.Connectors.DUI.Exceptions;
using Speckle.Connectors.DUI.Logging;
using Speckle.Connectors.DUI.Models;
using Speckle.Connectors.DUI.Models.Card;
using Speckle.Connectors.Revit.HostApp;
using Speckle.Sdk.Api;
using Speckle.Sdk.Common;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models.Extensions;

namespace Speckle.Connectors.Revit.Operations.Send;

public interface IRevitMultiSendOrchestrator
{
  /// <summary>
  /// 多次并行发送（当前为 Speckle 模型 + RVT 两路）；单路异常或失败不打断其余通道（用户取消仍通过共享 CancellationToken 统一中止）。
  /// </summary>
  Task RunMultiSendAsync(
    ISendBindingUICommands commands,
    string modelCardId,
    string rvtAbsolutePath,
    string? rvtDisplayName,
    long? rvtFileSizeBytes,
    Action<IServiceProvider, SenderModelCard> initializeScope,
    Func<SenderModelCard, Task<IReadOnlyList<DocumentToConvert>>> gatherObjectsAsync
  );
}

/// <summary>
/// 实现 <see cref="IRevitMultiSendOrchestrator"/>：各发送车道经 <see cref="Task.WhenAll"/> 并行。
/// Speckle 模型车道成功与否仍决定是否调用 <see cref="ISendBindingUICommands.SetModelSendResult"/>。
/// </summary>
internal sealed class RevitMultiSendOrchestrator(
  IServiceProvider rootServiceProvider,
  IOperationProgressManager operationProgressManager,
  DocumentModelStore store,
  ICancellationManager cancellationManager,
  ISdkActivityFactory activityFactory,
  IClientFactory clientFactory,
  IAccountManager accountManager,
  SpeckleProjectFileUploader speckleProjectFileUploader,
  ILogger<RevitMultiSendOrchestrator> logger
) : IRevitMultiSendOrchestrator
{
  /// <inheritdoc />
  public async Task RunMultiSendAsync(
    ISendBindingUICommands commands,
    string modelCardId,
    string rvtAbsolutePath,
    string? rvtDisplayName,
    long? rvtFileSizeBytes,
    Action<IServiceProvider, SenderModelCard> initializeScope,
    Func<SenderModelCard, Task<IReadOnlyList<DocumentToConvert>>> gatherObjectsAsync
  )
  {
    using var activity = activityFactory.Start();
    using var cancellationItem = cancellationManager.GetCancellationItem(modelCardId);
    var ct = cancellationItem.Token;

    if (store.GetModelById(modelCardId) is not SenderModelCard modelCard)
    {
      throw new InvalidOperationException("No publish model card was found.");
    }

    SendInfo sendInfo = GetSendInfo(modelCard);
    using var _ = sendInfo;
    using var userScope = UserActivityScope.AddUserScope(sendInfo.Account);

    using var scope = rootServiceProvider.CreateScope();
    var sp = scope.ServiceProvider;
    initializeScope(sp, modelCard);

    var sendOperation = sp.GetRequiredService<ISendOperation<DocumentToConvert>>();

    var lanes = new ConcurrentBag<SendLaneResult>();
    string? versionId = null;
    SendOperationResult? speckleModelResult = null;
    var speckleCancelled = false;
    Exception? speckleError = null;

    async Task LaneSpeckleModelAsync()
    {
      // 勿使用 BCL Progress{T}：会按 SyncContext Post，Revit 主线程 BuildSync 期间进度与 DUI 更新被推迟。
      var composite = operationProgressManager.CreateOperationProgressEventHandler(
        commands.Bridge,
        modelCardId,
        ct,
        RevitMultiSendTaskIds.SpeckleModel
      );

      try
      {
        var objects = await gatherObjectsAsync(modelCard).ConfigureAwait(false);
        if (objects.Count == 0)
        {
          throw new SpeckleSendFilterException("No objects were found to convert. Please update your publish filter!");
        }

        (SendOperationResult sendResult, string vId) = await sendOperation
          .Send(objects, sendInfo, rvtDisplayName, rvtFileSizeBytes, null, composite, ct)
          .ConfigureAwait(false);

        speckleModelResult = sendResult;
        versionId = vId;
        lanes.Add(
          new SendLaneResult(RevitMultiSendTaskIds.SpeckleModel, true, $"version={vId}", null)
        );

        operationProgressManager.SetModelTaskProgress(
          commands.Bridge,
          modelCardId,
          RevitMultiSendTaskIds.SpeckleModel,
          "Speckle 模型发送完成",
          1,
          ct,
          forceSend: true
        );
      }
      catch (OperationCanceledException) when (ct.IsCancellationRequested)
      {
        speckleCancelled = true;
      }
      catch (Exception ex)
      {
        logger.LogModelCardHandledError(ex);
        speckleError = ex;
        lanes.Add(
          new SendLaneResult(RevitMultiSendTaskIds.SpeckleModel, false, null, ex.ToFormattedString())
        );
        operationProgressManager.SetModelTaskProgress(
          commands.Bridge,
          modelCardId,
          RevitMultiSendTaskIds.SpeckleModel,
          $"失败: {ex.Message}",
          null,
          ct,
          forceSend: true
        );
      }
    }

    async Task LaneSpeckleRvtFileAsync()
    {
      try
      {
        operationProgressManager.SetModelTaskProgress(
          commands.Bridge,
          modelCardId,
          RevitMultiSendTaskIds.SpeckleRvtFile,
          "Speckle RVT 上传准备中",
          0,
          ct,
          forceSend: true
        );

        var normalized = new Progress<double>(r =>
        {
          operationProgressManager.SetModelTaskProgress(
            commands.Bridge,
            modelCardId,
            RevitMultiSendTaskIds.SpeckleRvtFile,
            "Speckle RVT 上传中",
            r,
            ct
          );
        });

        var detail = await speckleProjectFileUploader
          .UploadRvtAsync(
            sendInfo.Client,
            sendInfo.ProjectId,
            sendInfo.ModelId,
            rvtAbsolutePath,
            normalized,
            ct
          )
          .ConfigureAwait(false);

        lanes.Add(new SendLaneResult(RevitMultiSendTaskIds.SpeckleRvtFile, true, detail, null));
        operationProgressManager.SetModelTaskProgress(
          commands.Bridge,
          modelCardId,
          RevitMultiSendTaskIds.SpeckleRvtFile,
          "Speckle RVT 上传完成",
          1,
          ct,
          forceSend: true
        );
      }
      catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
      catch (Exception ex)
      {
        logger.LogWarning(ex, "Speckle RVT 文件上传失败");
        lanes.Add(
          new SendLaneResult(RevitMultiSendTaskIds.SpeckleRvtFile, false, null, ex.ToFormattedString())
        );
        operationProgressManager.SetModelTaskProgress(
          commands.Bridge,
          modelCardId,
          RevitMultiSendTaskIds.SpeckleRvtFile,
          $"失败: {ex.Message}",
          null,
          ct,
          forceSend: true
        );
      }
    }

    await Task.WhenAll(LaneSpeckleModelAsync(), LaneSpeckleRvtFileAsync()).ConfigureAwait(false);

    await commands.SetModelSendLaneResults(modelCardId, lanes.ToArray()).ConfigureAwait(false);

    if (speckleCancelled)
    {
      return;
    }

    if (speckleError is not null)
    {
      await commands.SetModelError(modelCardId, speckleError).ConfigureAwait(false);
      return;
    }

    if (versionId is not null && speckleModelResult is not null)
    {
      await commands
        .SetModelSendResult(modelCardId, versionId, speckleModelResult.ConversionResults)
        .ConfigureAwait(false);
    }
  }

  private SendInfo GetSendInfo(SenderModelCard modelCard)
  {
    Account account = accountManager.GetAccount(modelCard.AccountId.NotNull());
    var client = clientFactory.Create(account);
    return new(client, modelCard.ProjectId.NotNull(), modelCard.ModelId.NotNull());
  }
}
