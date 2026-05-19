# Revit 多次并行发送 — 源码位置

说明：各发送车道在编排器中通过 `Task.WhenAll` 并行启动；本节记录**各自实际执行上传或模型发送的具体类型与方法**所在的文件路径（相对于仓库根目录）。

## 编排（非上传实现，仅供参考）

| 说明 | 文件 |
|------|------|
| 多次并行调度、`sendOperation.Send`、`SpeckleProjectFileUploader.UploadRvtAsync` | `Connectors/Revit/Speckle.Connectors.RevitShared/Operations/Send/RevitMultiSendOrchestrator.cs` |

内部局部函数示例：`LaneSpeckleModelAsync`、`LaneSpeckleRvtFileAsync`（当前为两路，后续可扩展）。

---

## 1. Speckle 模型发送（几何/模型数据发送至 Speckle）

| 项目 | 路径 |
|------|------|
| **核心发送实现**（`Send`、`SendViaIngestion`、`SendViaVersionCreate`） | `Sdk/Speckle.Connectors.Common/Operations/SendOperation.cs` |
| **发送执行辅助**（如版本创建与上传细节） | `Sdk/Speckle.Connectors.Common/Operations/SendOperationExecutor.cs` |

Revit 侧在编排器中通过 DI 解析 `ISendOperation<DocumentToConvert>`，注册的实现类型为通用程序集中的 `SendOperation<DocumentToConvert>`（见 `Connectors/Revit/Speckle.Connectors.RevitShared/DependencyInjection/RevitConnectorModule.cs`）。

---

## 2. Speckle RVT 二进制上传（预签名 URL → PUT → `startFileImport`）

| 项目 | 路径 |
|------|------|
| **方法**：`UploadRvtAsync` | `Connectors/Revit/Speckle.Connectors.RevitShared/Operations/Send/SpeckleProjectFileUploader.cs` |

---

## 关联常量

各通道任务标识字符串定义于：`Connectors/Revit/Speckle.Connectors.RevitShared/Operations/Send/RevitMultiSendTaskIds.cs`。
