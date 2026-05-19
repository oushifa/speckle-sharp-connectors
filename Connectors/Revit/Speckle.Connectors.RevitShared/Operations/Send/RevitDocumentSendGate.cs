using System.IO;
using Autodesk.Revit.DB;
using Speckle.Sdk;

namespace Speckle.Connectors.Revit.Operations.Send;

/// <summary>Revit 侧「磁盘 RVT」相关上传门禁：RVT 文件车道需有效已保存的工程路径。</summary>
public static class RevitDocumentSendGate
{
  /// <summary>
  /// 校验当前文档可用于基于文件的上传：<see cref="Document.PathName"/> 存在且未修改且文件在磁盘上。
  /// 必须在 Revit <b>主线程</b> 调用（与其它 Revit API 一致）。
  /// </summary>
  /// <param name="document">当前要发送的工程。</param>
  /// <exception cref="Speckle.Sdk.SpeckleException">任一校验失败。</exception>
  public static void EnsureDocumentReadyForSend(Document document)
  {
    // 未保存的工程没有稳定磁盘路径；无法定位待上传 *.rvt。
    if (string.IsNullOrWhiteSpace(document.PathName))
    {
      throw new SpeckleException("请先保存项目后再上传。");
    }

    // Revit 允许内存与磁盘分叉；分叉时上传会得到陈旧文件。
    if (document.IsModified)
    {
      throw new SpeckleException("检测到未保存更改，请先保存后再上传。");
    }

    // 路径可能被移动、删除或未同步到本机。
    if (!File.Exists(document.PathName))
    {
      throw new SpeckleException("无法在磁盘找到模型文件。");
    }
  }
}
