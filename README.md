# 轻白板 / LightBoard

基于 WPF 的全屏白板，为真实课堂使用而设计。

- **经过实地课堂检验**，为实地课堂而优化的功能设计
- 用于替换希沃白板/希沃轻白板

## 为什么选择轻白板

- **快**
    - 6 代 i7 上热启动时间 < 0.5 秒
    - 书写、拖动、缩放延迟极低
- **简洁**
    - 无主页、无向导、无弹窗层级，全部工具单击直达
    - 离线可用，无账号、无广告
- **符合课堂逻辑**
    - 触摸交互建立在显式状态机之上：误触规则明确，掌压（≥ 5 指）即整掌擦除
    - 多人同时书写：大屏两侧学生各写各的，笔迹互不干扰
    - 课件即开即批注：PowerPoint / Word / PDF / XPS / 图片打开后直接书写，逐页生成可切换的白板页
- **经过实地课堂检验**
    - 功能取舍来自真实课堂反馈：荧光笔、克隆盖章、透明模式、时间显示、一键收起工具栏等均来自课堂场景
    - 超过 150 个自动化测试持续守护触摸状态机、工具栏交互与文件读写，重构不改行为

## 功能

- 快速更换笔触颜色与粗细（8 种颜色 × 6 档粗细）
- 荧光笔模式
- 直线 / 圆形状工具（画笔附加模式；再点当前颜色/粗细或当前形状按钮即回画笔）
- 线擦 / 面积擦 / 选择（移动、缩放、旋转、套索圈选）
- 撤销 / 重做（上限 200 步）
- 多页面管理，支持页面切换、缩略图预览，每页独立保存视图状态（缩放/偏移/撤销历史）
- 新建 / 附加
    - 新建：清空当前墨迹与文档视图，回到单张空白页
    - 附加：文档、图片、`*.lbf` 的页、`*.isf` 墨迹追加到当前白板，不覆盖已有内容
- 文档与图片
    - 打开 PowerPoint 演示文稿与 Word 文档（使用本机 Office 栅格化，逐页生成背景）
    - 打开 XPS、PDF（PDFium 渲染）与图片（BMP / GIF / ICO / JPEG / PNG / TIFF，作为单页背景）
    - 页面列表缩略图按需生成（显示时后台渲染）
- 墨迹管理
    - 复制 / 粘贴 / 克隆 / 删除选中墨迹（可跨应用粘贴）
    - `*.lbf` 轻白板文件（zip 容器，可解压获得单页 ISF；带版本校验与完整性检查）
    - `*.isf` Windows 墨迹文件
    - 自动备份当前墨迹（每分钟一次，内容未变化时跳过；写入 `recover/`），崩溃后可用「打开」载入该目录中的文件恢复
    - 快速保存：一键写入 `fastsave/` 目录，无需文件对话框
- 导出
    - 画布导出为 `*.png`（支持 25% / 50% / 100% 缩放，可导出全部页面）
    - 整板导出为 `*.pdf`
    - 导出期间显示进度条与「当前页 / 总页数」文案
- 画布按主屏分辨率派生（宽 4 屏 × 高 16 屏），平移/缩放结束视口贴近右/下边缘时自动扩展一屏
- 支持单实例运行：再次启动自动唤起已运行实例，并可加载传入的文件
- 多人同时书写（大屏两侧各人独立绘制，互不干扰）
- 标题栏实时时间显示
- 网格背景
- 透明背景模式（白板悬浮于桌面与其他应用之上，用于投影讲评）
- 调试模式（菜单勾选）：在画布上叠加参数可视化（半径与距离的 1:1 标注、缩放范围、手势死区）

## 触摸手势

| 手指数量        | 状态        | 说明                                 |
| --------------- | ----------- | ------------------------------------ |
| 1               | 绘图        | 单指轻触即可书写或绘制               |
| 1（选择模式）   | 选区        | 拖拽移动选区、拖手柄缩放/旋转、空白处套索圈选 |
| 2（近距离）     | 平移 + 缩放 | 双指移动平移，张合缩放               |
| 2（选择模式）   | 选区        | 选区随双指整体平移、旋转、缩放       |
| 3 ~ 4（近距离） | 平移        | 多指拖动平移画布；选择模式下由选区手势转为平移画布 |
| ≥ 5（近距离）   | 橡皮擦      | 多指用作大面积橡皮擦                 |
| ≥ 2（远距离）   | 多人绘制    | 多人在大屏两侧同时书写，每人独立笔迹 |

> 近距离/远距离由主屏工作区宽度的 10% 作为阈值判断。
> 双指缩放中抬起一指会转入单指平移，此后落下第二指也不再恢复缩放，需全部抬起重新开始。

## 下载

前往 [Releases](https://github.com/KaiHuaDou/DrawingNotepad/releases/latest) 下载最新版本。

- `LightBoard.zip`：框架依赖，需安装 .NET 10 桌面运行时
- `LightBoard-with-runtime.zip`：自包含，免安装运行时

前往 [Actions](https://github.com/KaiHuaDou/DrawingNotepad/actions) 下载构建版本。

## 系统要求

- Windows 7 SP1 或更新版本
    - **已测试：Windows 7 SP1 上可以正常安装 .NET 10.0 Desktop Runtime，并能成功无错误运行本程序**
- .NET 10.0 Desktop Runtime（x64）或更新版本
    - 使用 `with-runtime` 版本可以免安装运行时
- Microsoft Office（打开 Office 文档时需要）
    - 需要已激活版本。部分绿色版本需确认相关 COM 组件已注册
    - Office 2010 及更新版本
    - Office 2007 需安装插件 [Microsoft Save as PDF or XPS](https://legacyupdate.net/download-center/download/7/2007-microsoft-office-add-in-microsoft-save-as-pdf-or-xps)
- [Segoe Fluent Icons 字体](https://learn.microsoft.com/zh-cn/windows/apps/design/style/segoe-fluent-icons-font)

## 开发与构建

- IDE：Visual Studio Community 2026 或更新版本
    - 工作负载：C# 桌面开发
    - 预览功能：使用 .NET SDK 预览版

- .NET **10.0** SDK 或更新版本（此时 C# `preview` >= 14）

```bash
dotnet publish -p:PublishProfile=FolderProfile -c Release
```

输出位于 `LightBoard/bin/publish/`。

如需免装运行时（自包含）版本：

```bash
dotnet publish -p:PublishProfile=FolderProfile -c Release --self-contained
```

运行测试（STA 测试宿主，含触摸状态机、工具栏交互与文件读写用例）：

```bash
dotnet test
```

## 项目结构

```
LightBoard/               # 主程序
├─ InkCanvasNext/         # WPF InkCanvas 现代封装（可独立复用）
│  ├─ InkCanvasNext.xaml(.cs) # 主控件：依赖属性、模式切换与文档页背景
│  ├─ Devices.cs          # 触摸/鼠标设备事件处理与捕获
│  ├─ Touches.cs          # 触摸事件的转发与坐标处理
│  ├─ States.cs           # 触摸状态机
│  ├─ Gestures.cs         # 平移/缩放手势（带平滑）与画布边缘扩展
│  ├─ MultiTouch.cs       # 多人同时绘制与增量渲染
│  ├─ Parameters.cs       # 控件参数常量
│  ├─ Eraser.cs           # 橡皮擦反馈与增量命中
│  ├─ Strokes.cs          # 墨迹集合、剪贴板与预览/导出
│  ├─ UndoRedo.cs         # 历史栈管理
│  ├─ Selection.cs        # 自绘选择控制器（选区集合与包围盒）
│  ├─ SelectionInput.cs   # 选择手势入口（落点判定、手柄命中）
│  ├─ SelectionTransform.cs  # 选区变换管线（增量应用、双指相似变换、撤销单元）
│  ├─ SelectionLasso.cs   # 套索圈选
│  ├─ SelectionVisual.cs  # 选择视觉（高亮/边框/手柄/套索轨迹）
│  ├─ Shapes.cs           # 直线/圆形形状绘制
│  ├─ Geometry.cs         # 几何工具
│  └─ RingBuffer.cs       # 定容环形缓冲（撤销栈）
├─ Document/              # 渲染源：XpsSource / PdfSource / ImagePageSource / PdfExport
├─ Documents.cs           # 文档/图片打开：Office COM → XPS、XPS/PDF/图片渲染源与缓存
├─ Page.cs                # 多页面管理
├─ BoardFile.cs           # 多页整体存档（.lbf）与自动恢复
├─ MainWindow.xaml(.cs)   # 主窗口与工具栏
├─ MainWindow.Handler.cs  # 工具栏交互、菜单与动画
├─ MainWindow.Toolbar.cs  # 工具栏状态（模式/笔迹配置）与选区工具栏吸附
├─ MainWindow.IO.cs       # 打开/保存/导出流程
├─ MainWindow.File.cs     # 文档状态（新建/附加、未保存确认、关闭文档）
├─ MainWindow.Loading.cs  # 加载浮层与导出进度
├─ ParametersDebugVisual.cs   # 调试浮层（参数可视化、运行状态与帧率）
├─ Theme.xaml             # 主题样式（图标/按钮/颜色选择器）
├─ External/NativeMethods.cs  # Win32 互操作（窗口切换）
└─ App.xaml(.cs)          # 应用入口、单实例与崩溃恢复
docs/                     # 设计文档与参考资料
```

## 路线图

见 [ROADMAP](docs/ROADMAP.md)。

## 更新日志

见 [CHANGELOG](CHANGELOG.md)。

## `InkCanvasNext`

WPF `InkCanvas` 现代封装

复制 [`InkCanvasNext` 文件夹](./LightBoard/InkCanvasNext) 到你的项目里面即可使用。

### 公开类型

- `InkCanvasNext`: 主控件
- `InkCanvasNextMode`: 编辑模式（`Line`/`Circle` 为笔迹上的附加形状模式，`Highlighter` 为荧光笔模式）
- `StampAction`: 盖章开关（克隆/粘贴）
- `InkCanvasStrokesChangedEventArgs`: `StrokesChanged` 事件的载荷（Added/Removed，选区整体变换时 `TransformOnly` 为 true）
- `MouseWheelAction`: 滚轮交互方式（Scroll/Zoom/None）
- `HistorySnapshot`: 撤销/重做历史栈快照（`SwapHistory` 使用）

```csharp
public enum InkCanvasNextMode
{
    Ink,         // 墨迹书写
    EraseStroke, // 线擦
    EraseArea,   // 面积擦
    Select,      // 选择（自绘选择层 + 套索）
    Line,        // 直线（Pen 附加）
    Circle,      // 圆（Pen 附加）
    Highlighter  // 荧光笔（独立模式，黄色高亮笔迹）
}
```

### `InkCanvasNext`

#### 构造函数

```csharp
public InkCanvasNext(Size canvasSize, Point initialOffset)
```

| 参数            | 说明                                       |
| --------------- | ------------------------------------------ |
| `canvasSize`    | 内层墨迹画布尺寸，文档背景页在此画布内居中 |
| `initialOffset` | 初始视口左上角在画布内容坐标中的位置       |

#### 依赖属性

| 属性                       | 类型                | 说明                                            |
| -------------------------- | ------------------- | ----------------------------------------------- |
| `CanRedo`                  | `bool`              | 只读，是否可以重做                              |
| `CanUndo`                  | `bool`              | 只读，是否可以撤销                              |
| `DefaultDrawingAttributes` | `DrawingAttributes` | 默认笔触属性（颜色/粗细/荧光笔均在此表达）      |
| `Mode`                     | `InkCanvasNextMode` | 当前编辑模式                                    |
| `EraserDiameter`           | `double`            | 面积擦直径默认 `50.0`  |
| `MouseWheelAction`         | `MouseWheelAction`  | 滚轮交互：滚动 / 缩放 / 不接管，默认滚动        |
| `Strokes`                  | `StrokeCollection`  | 墨迹集合，赋值会替换墨迹、清空选区/盖章态并清空历史 |

#### 事件

| 事件               | 类型                                            | 说明                         |
| ------------------ | ----------------------------------------------- | ---------------------------- |
| `CanRedoChanged`   | `EventHandler<DependencyPropertyChangedEventArgs>` | `CanRedo` 变化时触发      |
| `CanUndoChanged`   | `EventHandler<DependencyPropertyChangedEventArgs>` | `CanUndo` 变化时触发      |
| `StrokesChanged`   | `EventHandler<InkCanvasStrokesChangedEventArgs>` | 墨迹增删或选区整体变换时触发，载荷带 Added/Removed |
| `SelectionChanged` | `EventHandler`                                  | 选中内容变化时触发           |
| `ViewOrSelectionChanged` | `EventHandler`                              | 视口（滚动/缩放）或选区包围盒变化，供外部工具栏跟随 |

#### 属性

| 属性               | 类型               | 说明                                  |
| ------------------ | ------------------ | ------------------------------------- |
| `SelectedStrokes`  | `StrokeCollection` | 当前选中墨迹的快照副本（访问即拷贝）  |
| `SelectedCount`    | `int`              | 当前选中笔触数（轻量读法）            |
| `HasSelection`     | `bool`             | 是否有选中的墨迹                      |
| `CurrentScale`     | `double`           | 当前画布缩放比例（自动钳制 0.1–10）   |
| `OffsetX`          | `double`           | 画布水平滚动偏移                      |
| `OffsetY`          | `double`           | 画布垂直滚动偏移                      |
| `ViewportSize`     | `Size`             | 当前视口尺寸（DIP）                   |
| `StampAction`      | `StampAction`      | 当前盖章模式（克隆/粘贴/无）          |
| `DocumentPage`     | `ImageSource?`     | 当前文档背景页图像（无则为 `null`）   |
| `MouseWheelAction` | `MouseWheelAction` | （依赖属性，见上）                    |

#### 方法

| 方法                             | 说明                                        |
| -------------------------------- | ------------------------------------------- |
| `Undo()`                         | 撤销上一步墨迹变更                          |
| `Redo()`                         | 重做上一步墨迹变更                          |
| `CopySelected()`                 | 复制选中的墨迹到剪贴板                      |
| `CutSelected()`                  | 剪切选中的墨迹到剪贴板                      |
| `DeleteSelected()`               | 删除选中的墨迹                              |
| `StampCloneAt(Point)`            | 以点击点为副本包围盒中心，克隆盖章当前选区  |
| `StampPasteAt(Point)`            | 以点击点为副本包围盒中心，粘贴剪贴板墨迹    |
| `GetSelectionScreenBounds(Visual)` | 选区包围盒在指定坐标系下的矩形（无选区返回 null） |
| `GetCanvasViewportBounds(UIElement)` | 可见视口在指定坐标系下的矩形             |
| `EnsureStrokesFit()`             | 笔画越出画布右/下边界时扩展画布              |
| `ResetTouchState()`              | 重置当前触摸状态并释放所有触摸捕获          |
| `SwapHistory(...)`               | 交换撤销/重做历史栈，旧栈经 `out` 参数返回  |
| `SetDocumentPage(ImageSource?)`  | 设置/清除文档页面背景图像（传 `null` 清除） |
| `ClearMultiTouchVisuals()`       | 清空进行中的多指笔画（视觉与笔迹）          |

## 许可证

本项目以 [Apache-2.0 License](http://www.apache.org/licenses/) 提供。
