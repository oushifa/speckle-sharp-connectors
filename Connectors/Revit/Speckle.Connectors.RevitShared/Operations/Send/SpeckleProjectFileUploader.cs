using System.IO;
using Microsoft.Extensions.Logging;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Transports;

namespace Speckle.Connectors.Revit.Operations.Send;

/// <summary>
/// 将磁盘上的 Revit *.rvt 上传至 Speckle（预签名 URL → PUT）。
/// 完成上传后触发 <see cref="StartFileImportInput"/>；与 Speckle 文档中的「上传 IFC 及其他文件」流程一致。
/// </summary>
public sealed class SpeckleProjectFileUploader(ILogger<SpeckleProjectFileUploader>? logger = null)
{
  /// <summary>Speckle 官方文件流水线：generateUploadUrl → PUT → startFileImport。</summary>
  /// <param name="client">已与账户绑定的 Speckle GraphQL/REST 客户端。</param>
  /// <param name="projectId">目标项目 ID。</param>
  /// <param name="modelId">要将文件关联到的 Speckle 模型 ID。</param>
  /// <param name="rvtAbsolutePath">磁盘上的 *.rvt 绝对路径（须已通过门禁校验可读）。</param>
  /// <param name="normalizedProgress">可选；0～1，用于 DUI 条状进度映射。</param>
  /// <param name="cancellationToken">取消 PUT 循环与后续导入任务提交。</param>
  /// <returns>简要文本，形如 <c>fileId=...</c>。</returns>
  public async Task<string> UploadRvtAsync(
    IClient client,
    string projectId,
    string modelId,
    string rvtAbsolutePath,
    IProgress<double>? normalizedProgress,
    CancellationToken cancellationToken
  )
  {
    if (string.IsNullOrWhiteSpace(projectId))
    {
      throw new ArgumentException("Value cannot be empty.", nameof(projectId));
    }

    if (string.IsNullOrWhiteSpace(modelId))
    {
      throw new ArgumentException("Value cannot be empty.", nameof(modelId));
    }

    normalizedProgress?.Report(0);

    var fileName = Path.GetFileName(rvtAbsolutePath);

    // 步骤 1：向 Speckle 申请针对该文件名的一次性写入 URL。
#pragma warning disable CS0618 // 按计划使用 file import job API；服务端若已切换 ingestion 由 Speckle 侧处理
    var generated = await client.FileImport
      .GenerateUploadUrl(new GenerateFileUploadUrlInput(projectId, fileName), cancellationToken)
      .ConfigureAwait(false);
#pragma warning restore CS0618

    logger?.LogInformation("Speckle RVT upload: acquired upload URL for {File}", fileName);

    normalizedProgress?.Report(0.05);

    // 步骤 2：直传对象存储并根据 SDK 报告的已写字节换算 5%～95% 区间进度。
#pragma warning disable CS0618
    string etag = await client
      .FileImport.UploadFile(
        rvtAbsolutePath,
        generated.url,
        new Progress<ProgressArgs>(pa =>
        {
          if (
            normalizedProgress != null && pa.ProgressEvent == ProgressEvent.UploadBytes &&
            pa.Total > 0
          )
          {
            var ratio = 0.05 + 0.9 * pa.Count / (double)pa.Total;
            normalizedProgress.Report(Math.Min(ratio, 0.95));
          }
        }),
        cancellationToken
      )
      .ConfigureAwait(false);
#pragma warning restore CS0618

    normalizedProgress?.Report(0.96);

    // 步骤 3：通知 Speckle 将已上传二进制与给定 model/version 链路挂钩（服务端异步 ingestion）。
#pragma warning disable CS0618
    await client
      .FileImport.StartFileImportJob(
        new StartFileImportInput(projectId, modelId, generated.fileId, etag),
        cancellationToken
      )
      .ConfigureAwait(false);
#pragma warning restore CS0618

    normalizedProgress?.Report(1);
    logger?.LogInformation(
      "Speckle RVT upload: startFileImport submitted for model {ModelId}, file id {FileId}",
      modelId,
      generated.fileId
    );

    return $"fileId={generated.fileId}";
  }
}
