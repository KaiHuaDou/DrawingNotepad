# 轻白板 / LightBoard

一个基于 WPF `InkCanvas` 的轻白板。

- **经过实地课堂检验，为实地课堂而优化的功能设计**
- 目标：替换希沃白板/希沃轻白板
- 超快的启动速度
    - 6 代 i7 上热启动时间：<1 秒
- 极低的书写/拖动/缩放延迟

## 功能

- 快速更换笔触颜色与粗细（8 种颜色 × 4 档粗细）
- 荧光笔模式
- 线擦 / 面积擦 / 选择
- 撤销 / 重做（上限 200 步）
- 多页面管理，支持页面切换、缩略图预览，每页独立保存视图状态（缩放/偏移/撤销历史）
- 复制 / 粘贴 / 克隆 / 删除选中墨迹（使用 Windows 墨迹剪贴板格式，可跨应用粘贴）
- 工具栏可一键收起/展开
- 打开 / 保存 `*.lbf` 轻白板文件（zip 容器：`manifest.json` + 每页独立 ISF，多页整体存档，可续课）
- 打开 `*.isf` Windows 墨迹文件
- 打开 `*.pptx / *.ppt / *.docx / *.doc` 演示文稿与 Word 文档（通过本机 Office 栅格化为页面，可继续书写批注）
- 打开 `*.xps` XPS 文档
- 打开 `*.pdf` PDF 文档（PDFium 渲染，无需 Office）
- 导出画布为 `*.png`（支持 25% / 50% / 100% 缩放）
- 自动备份当前墨迹（每分钟保存到 `recover/`），崩溃后下次启动可一键恢复
- 支持单实例运行
- 多人同时书写（大屏两侧各人独立绘制，互不干扰）
- 标题栏实时时间显示
- 透明背景模式

## 触摸手势

| 手指数量        | 状态        | 说明                                 |
| --------------- | ----------- | ------------------------------------ |
| 1               | 绘图        | 单指轻触即可书写或绘制               |
| 2（近距离）     | 平移 + 缩放 | 双指移动平移，张合缩放               |
| 3 ~ 4（近距离） | 平移        | 多指拖动平移画布                     |
| ≥ 5（近距离）   | 橡皮擦      | 多指用作大面积橡皮擦                 |
| ≥ 2（远距离）   | 多人绘制    | 多人在大屏两侧同时书写，每人独立笔迹 |

> 近距离/远距离由窗口宽度的 10% 作为阈值判断。

## 下载

前往 [Releases](https://github.com/KaiHuaDou/DrawingNotepad/releases/latest) 下载最新版本。

前往 [Actions](https://github.com/KaiHuaDou/DrawingNotepad/actions) 下载构建版本。

## 系统要求

- Windows 7 SP1 或更新版本
    - **已测试：Windows 7 SP1 上可以正常安装 .NET 9.0 Desktop Runtime，并能成功无错误运行本程序**
- .NET 9.0 Desktop Runtime（x64）或更新版本
    - 使用 `with-runtime` 版本可以免安装运行时
- Microsoft Office（打开 Office 文档时需要）
    - 打开 `*.pptx / *.ppt` 需要本机安装 PowerPoint
    - 打开 `*.docx / *.doc` 需要本机安装 Word
    - **要求任意已激活的 Office 版本，版本号 >= 2007**（XPS 导出功能自 Office 2007 起提供）
- [Segoe Fluent Icons 字体](https://learn.microsoft.com/zh-cn/windows/apps/design/style/segoe-fluent-icons-font)

## 开发与构建

- IDE：Visual Studio Community 2022 或更新版本
    - 工作负载：C# 桌面开发
    - 预览功能：使用 .NET SDK 预览版

- .NET **9.0** SDK 或更新版本（需要 C# `preview` 语言版本）

```bash
dotnet publish -p:PublishProfile=FolderProfile -c Release -f net9.0-windows
```

输出位于 `LightBoard/bin/publish/`。

如需免装运行时（自包含）版本：

```bash
dotnet publish -p:PublishProfile=FolderProfile -c Release -f net9.0-windows --self-contained
```

CI 在 GitHub Actions（`windows-latest` + .NET SDK 10.x）中构建并同时产出框架依赖版与自包含版两个构建物。

## 项目结构

```
LightBoard/               # 主程序
├─ InkCanvasNext/         # WPF InkCanvas 现代封装（可独立复用）
│  ├─ Devices.cs          # 触摸/鼠标设备事件处理与捕获
│  ├─ States.cs           # 触摸状态机
│  ├─ Gestures.cs         # 平移/缩放手势（带平滑）
│  ├─ MultiTouch.cs       # 多人同时绘制与增量渲染
│  ├─ Eraser.cs           # 橡皮擦反馈与增量命中
│  ├─ Strokes.cs          # 墨迹集合、剪贴板与预览/导出
│  ├─ UndoRedo.cs         # 历史栈管理
│  ├─ Geometry.cs         # 几何工具
│  └─ RingBuffer.cs       # 定容环形缓冲（撤销栈）
├─ Documents.cs           # 文档打开：Office COM → XPS、XPS/PDF 渲染源与缓存
├─ Paging.cs              # 多页面管理
├─ BoardFile.cs           # 多页整体存档（.lbf）与自动恢复
├─ MainWindow.xaml(.cs)   # 主窗口与工具栏
├─ MainWindowHandler.cs   # 工具栏交互、菜单与动画
├─ Theme.xaml             # 主题样式（图标/按钮/颜色选择器）
├─ External/NativeMethods.cs  # Win32 互操作（窗口切换）
└─ App.xaml(.cs)          # 应用入口、单实例与崩溃恢复
docs/                     # 设计文档与参考资料
```

## 路线图

见 [ROADMAP](docs/ROADMAP.md)。

## `InkCanvasNext`

WPF `InkCanvas` 现代封装

复制 [`InkCanvas` 文件夹](./LightBoard/InkCanvasNext) 到你的项目里面即可使用。

### 公开类型

- `InkCanvasNext`: 主控件
- `InkCanvasNextMode`: 编辑模式

```csharp
public enum InkCanvasNextMode
{
    Ink,         // 墨迹书写
    EraseStroke, // 线擦
    EraseArea,   // 面积擦
    Select       // 选择
}
```

### `InkCanvasNext`

#### 依赖属性

| 属性                       | 类型                | 说明                    |
| -------------------------- | ------------------- | ----------------------- |
| `CanRedo`                  | `bool`              | 只读，是否可以重做      |
| `CanUndo`                  | `bool`              | 只读，是否可以撤销      |
| `DefaultDrawingAttributes` | `DrawingAttributes` | 默认笔触属性            |
| `Mode`                     | `InkCanvasNextMode` | 当前编辑模式            |
| `EraserDiameter`           | `double`            | 面积擦直径，默认 `50.0` |
| `Strokes`                  | `StrokeCollection`  | 墨迹集合                |

#### 事件

| 事件             | 说明                 |
| ---------------- | -------------------- |
| `CanRedoChanged` | `CanRedo` 变化时触发 |
| `CanUndoChanged` | `CanUndo` 变化时触发 |
| `StrokesChanged` | 墨迹集合变化时触发   |

#### 属性

| 属性              | 类型               | 说明                                 |
| ----------------- | ------------------ | ------------------------------------ |
| `SelectedStrokes` | `StrokeCollection` | 当前选中的墨迹                       |
| `HasSelection`    | `bool`             | 是否有选中的墨迹                     |
| `InnerCanvas`     | `InkCanvas`        | 内部原生 `InkCanvas`，不建议直接使用 |
| `CurrentScale`    | `double`           | 当前画布缩放比例                     |
| `OffsetX`         | `double`           | 画布水平滚动偏移                     |
| `OffsetY`         | `double`           | 画布垂直滚动偏移                     |

#### 方法

| 方法                       | 说明                               |
| -------------------------- | ---------------------------------- |
| `Undo()`                   | 撤销上一步墨迹变更                 |
| `Redo()`                   | 重做上一步墨迹变更                 |
| `CopySelected()`           | 复制选中的墨迹到剪贴板             |
| `CutSelected()`            | 剪切选中的墨迹到剪贴板             |
| `Paste()`                  | 从剪贴板粘贴墨迹到画布中心         |
| `DeleteSelected()`         | 删除选中的墨迹                     |
| `CloneSelected()`          | 克隆选中的墨迹并偏移显示           |
| `ResetTouchState()`        | 重置当前触摸状态并释放所有触摸捕获 |
| `SwapHistory(...)`         | 交换控件当前的撤销/重做历史栈      |
| `SetDocumentPage(...)`     | 设置/清除文档页面背景图像          |
| `ClearMultiTouchVisuals()` | 清空进行中的多指笔画视觉           |

## 许可证

本项目以 [Apache-2.0 License](http://www.apache.org/licenses/) 提供。
