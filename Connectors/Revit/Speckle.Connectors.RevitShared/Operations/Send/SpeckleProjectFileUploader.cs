using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Speckle.Sdk.Api;

namespace Speckle.Connectors.Revit.Operations.Send;

/// <summary>
/// 将磁盘上的 Revit *.rvt 绑定到已存在的 Speckle 版本（Commit）上。
/// </summary>
public sealed class SpeckleProjectFileUploader(ILogger<SpeckleProjectFileUploader>? logger = null)
{
  /// <summary>
  /// 通过 bind-file REST 接口将本地 *.rvt 上传并绑定到指定版本。
  /// </summary>
  /// <param name="serverUrl">Speckle 服务端地址（<c>account.serverInfo.url</c>）。</param>
  /// <param name="projectId">目标项目 ID。</param>
  /// <param name="versionId">已存在的模型版本（Commit）ID。</param>
  /// <param name="rvtAbsolutePath">磁盘上的 *.rvt 绝对路径（须已通过门禁校验可读）。</param>
  /// <param name="normalizedProgress">可选；0～1，用于 DUI 条状进度映射。</param>
  /// <param name="cancellationToken">用户取消时中止 HTTP 请求。</param>
  /// <returns>简要成功文本。</returns>
  [System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1054:URI parameters should not be strings",
    Justification = "Matches account.serverInfo.url string from Speckle credentials."
  )]
  public async Task<string> BindRvtFileAsync(
    string serverUrl,
    string accessToken,
    string projectId,
    string versionId,
    string rvtAbsolutePath,
    IProgress<double>? normalizedProgress,
    CancellationToken cancellationToken
  )
  {
    if (string.IsNullOrWhiteSpace(serverUrl))
    {
      throw new ArgumentException("Value cannot be empty.", nameof(serverUrl));
    }

    if (string.IsNullOrWhiteSpace(accessToken))
    {
      throw new ArgumentException("Value cannot be empty.", nameof(accessToken));
    }

    if (string.IsNullOrWhiteSpace(projectId))
    {
      throw new ArgumentException("Value cannot be empty.", nameof(projectId));
    }

    if (string.IsNullOrWhiteSpace(versionId))
    {
      throw new ArgumentException("Value cannot be empty.", nameof(versionId));
    }

    cancellationToken.ThrowIfCancellationRequested();

    normalizedProgress?.Report(0);

    var requestUri = new Uri(new Uri(serverUrl, UriKind.Absolute), $"api/v1/projects/{projectId}/versions/{versionId}/bind-file");
    var fileName = Path.GetFileName(rvtAbsolutePath);

    logger?.LogInformation(
      "Speckle RVT bind-file: uploading {File} to version {VersionId}",
      fileName,
      versionId
    );

    using var content = new MultipartFormDataContent();
    using var fileStream = OpenRvtFileForRead(rvtAbsolutePath);
    var fileContent = new StreamContent(fileStream);
    content.Add(fileContent, "file", fileName);

    using var http = new HttpClient();
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    using var response = await http.PostAsync(requestUri, content, cancellationToken).ConfigureAwait(false);

    if (!response.IsSuccessStatusCode)
    {
      var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
      throw new HttpRequestException(
        $"bind-file failed with status {(int)response.StatusCode} ({response.StatusCode}): {body}"
      );
    }

    normalizedProgress?.Report(1);
    logger?.LogInformation(
      "Speckle RVT bind-file: completed for version {VersionId}, file {File}",
      versionId,
      fileName
    );

    return "bind-file ok";
  }

  /// <summary>
  /// 打开待上传的 *.rvt。Revit 运行时原文件常被本进程或其它程序（如云同步）占用，
  /// 故先以 <see cref="FileShare.ReadWrite"/> 尝试直读，失败则复制到临时目录再读。
  /// </summary>
  private static FileStream OpenRvtFileForRead(string rvtAbsolutePath)
  {
    try
    {
      return new FileStream(
        rvtAbsolutePath,
        FileMode.Open,
        FileAccess.Read,
        FileShare.ReadWrite,
        bufferSize: 4096,
        FileOptions.SequentialScan
      );
    }
    catch (IOException)
    {
      var tempDir = Path.Combine(Path.GetTempPath(), "speckle-rvt-upload");
      Directory.CreateDirectory(tempDir);
      var tempPath = Path.Combine(tempDir, $"{Guid.NewGuid():N}_{Path.GetFileName(rvtAbsolutePath)}");

      try
      {
        File.Copy(rvtAbsolutePath, tempPath, overwrite: true);
        return new FileStream(
          tempPath,
          FileMode.Open,
          FileAccess.Read,
          FileShare.Read,
          bufferSize: 4096,
          FileOptions.DeleteOnClose | FileOptions.SequentialScan
        );
      }
      catch (Exception copyEx)
      {
        throw new IOException(
          $"无法读取 Revit 模型文件，可能被云同步、杀毒或其它程序占用：{rvtAbsolutePath}",
          copyEx
        );
      }
    }
    catch (Exception ex) when (ex is not IOException)
    {
      throw new IOException($"无法读取 Revit 模型文件：{rvtAbsolutePath}", ex);
    }
  }

  /// <summary>Speckle 官方文件流水线：generateUploadUrl → PUT → startFileImport。</summary>
  /// <param name="client">已与账户绑定的 Speckle GraphQL/REST 客户端。</param>
  /// <param name="projectId">目标项目 ID。</param>
  /// <param name="modelId">要将文件关联到的 Speckle 模型 ID。</param>
  /// <param name="rvtAbsolutePath">磁盘上的 *.rvt 绝对路径（须已通过门禁校验可读）。</param>
  /// <param name="normalizedProgress">可选；0～1，用于 DUI 条状进度映射。</param>
  /// <param name="cancellationToken">取消 PUT 循环与后续导入任务提交。</param>
  /// <returns>简要文本，形如 <c>fileId=...</c>。</returns>
  public Task<string> UploadRvtAsync(
    IClient client,
    string projectId,
    string modelId,
    string rvtAbsolutePath,
    IProgress<double>? normalizedProgress,
    CancellationToken cancellationToken
  )
  {
    _ = client;
    _ = projectId;
    _ = modelId;
    _ = rvtAbsolutePath;
    _ = normalizedProgress;
    _ = cancellationToken;

    /*
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
    */

    return Task.FromException<string>(
      new NotSupportedException(
        "UploadRvtAsync is deprecated; use BindRvtFileAsync with the bind-file REST API."
      )
    );
  }
}
