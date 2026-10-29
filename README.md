# 轻白板 / LightBoard

基于 .NET 10 与 WPF 的全屏白板应用，用于课堂书写与课件批注，可作为希沃白板 / 希沃轻白板的替代。程序离线运行，无账号与广告。

## 功能

### 书写

- 画笔 8 种颜色、5 档粗细（1 / 2 / 3 / 5 / 10）
- 荧光笔：黄色、粗 36，半透明笔迹
- 直线 / 圆：画笔附加形状模式，再点当前颜色、粗细或形状按钮回到画笔
- 面积擦：橡皮圈直径 50 视口 DIP，按压拖动擦除
- 选择：拖动移动，拖手柄缩放 / 旋转，空白处套索圈选；手柄、描边与套索按屏幕尺寸绘制，缩放画布时保持原大小
- 撤销 / 重做：默认 200 步，可在设置中调整到 10~1000
- 多人同时书写：触点间距超过阈值的多指各自成笔，笔迹互不影响
- 画布尺寸按主屏分辨率派生（宽 4 屏、高 16 屏）；平移或缩放结束时视口贴近右 / 下边缘则扩展一屏，载入的笔画越出边界时同样扩展

### 页面与文档

- 多页白板：上一页 / 下一页（末页再点新建），点页码展开页面列表，缩略图按需生成
- 每页独立保留缩放、偏移与撤销历史，切换页面不丢失
- 打开 PowerPoint / Word（本机 Office 栅格化）、PDF（PDFium）、XPS 与图片（BMP / GIF / ICO / JPEG / PNG / TIFF）
- 每页记录背景来源（`源文件::页码`）与适配盒，`.lbf` 打开时重新加载；源文件缺失时保留引用并写入日志，引用写坏的页跳过并提示一次
- 附加：文档、图片、`.lbf` 的页、`.isf` 墨迹追加到当前板，不覆盖已有内容

### 文件

- `.lbf`：zip 容器（`manifest.json` 与每页 ISF），带版本与完整性校验；v1 文件可直接打开（无背景）
- `.isf`：Windows 墨迹文件
- 自动备份：每分钟写入 `recover/`，内容未变化时跳过；异常退出后可用「打开」载入该目录中的文件恢复
- 有未保存墨迹时关闭，可选择「快速保存」写入 `fastsave/`，或「手动保存」选择路径

### 导出

- 图片：选择 25% / 50% / 100% 后选目录，全部页面导出为按页码命名的 `*.png`，放在以时间命名的子目录中
- PDF：整板导出为单个 `*.pdf`，缩放选项同上
- 导出期间显示进度与「当前页 / 总页数」

### 界面

- 左工具栏：关闭、最小化、文件菜单、背景（默认网格 / 透明）、切换到浏览器窗口
- 主工具栏：下排为颜色、荧光笔、橡皮、清屏、撤销、重做与展开按钮；点展开后显示上排的粗细、直线、圆、选择、粘贴
- 右工具栏：收起工具栏、菜单、上一页、页码、下一页
- 右上角时间（HH:mm）
- 透明背景：画布透明、窗口置顶，叠加在其他窗口之上
- 帮助浮层（菜单勾选）：压暗画布，说明标签按工具栏排列、用箭头指向各按钮，画布中央列出手势；打开时显示主工具栏上排，关闭时还原
- 调试浮层（菜单勾选）：1:1 半径圆与距离线、抬手贴边自动滚动的热区虚线、触点 / 笔画 / 缩放 / 撤销 / 重做 / 系统缩放 / 渲染层与渲染模式读数、帧率与最长帧
- 计时窗口：倒计时与正计时，可暂停继续，窗口置顶，最大化显示大字
- 单实例：再次启动唤起已运行窗口，命令行传入的文件由该实例打开

### 设置

设置保存在程序目录的 `settings.json`。

- 抬手贴边自动滚动开关：抬手位置距视口边缘不足 192 视口 DIP 时滚动画布
- 三个手势阈值：单指起笔位移、双指平移锁定、双指捏合锁定间距（1~200 视口 DIP）
- 撤销历史条数（10~1000，默认 200）
- 橡皮触点数量：四指及以上或五指及以上进入掌心擦除（默认五指）。画布创建时读取，修改后重启生效

## 触摸手势

| 触点                  | 状态        | 说明                                         |
| --------------------- | ----------- | -------------------------------------------- |
| 1                     | 绘图        | 位移超过起笔阈值后开始绘制                   |
| 1（选择工具）         | 选区        | 拖动移动，拖手柄缩放 / 旋转，空白处套索圈选   |
| 2（间距 ≤ 阈值）      | 平移 + 缩放 | 双指移动平移，张合缩放                       |
| 2（选择工具）         | 选区变换    | 选区随双指平移、旋转、缩放                   |
| 3（间距 ≤ 阈值）      | 平移        | 拖动平移画布；选择工具下附加手指转为平移画布  |
| 4（间距 ≤ 阈值）      | 平移 / 擦除 | 橡皮触点数量为五指时平移，为四指时掌心擦除    |
| ≥ 5（间距 ≤ 阈值）    | 掌心擦除    | 触点包络作为橡皮                             |
| ≥ 2（间距 > 阈值）    | 多人书写    | 各触点独立成笔（擦除工具除外）                |

- 触点间距阈值取主屏工作区宽度的 10%。
- 双指手势中，中点位移超过 30 视口 DIP 或间距小于 24 视口 DIP 后锁定缩放，当前手势内缩放保持不变。
- 双指缩放中抬起一指转入单指平移，此后落下第二指也不恢复缩放，需全部抬起重新开始。

## 鼠标与键盘

- 左键绘制、擦除、选择与盖章，自定义工具的交互规则与单指一致
- 滚轮滚动画布，Ctrl + 滚轮缩放
- Esc 取消盖章（克隆 / 粘贴）

## 下载

前往 [Releases](https://github.com/KaiHuaDou/DrawingNotepad/releases/latest) 下载最新版本。

- `LightBoard.zip`：框架依赖，需安装 .NET 10 桌面运行时
- `LightBoard-with-runtime.zip`：自包含，免安装运行时
- `LightBoard-Installer-<版本>.exe`：安装包（框架依赖），缺少 .NET 10 桌面运行时时由安装程序下载并安装
- `LightBoard-Installer-with-runtime-<版本>.exe`：安装包（自包含）

前往 [Actions](https://github.com/KaiHuaDou/DrawingNotepad/actions) 下载构建产物。

## 系统要求

- Windows 7 SP1 或更新版本
    - 已测试：Windows 7 SP1 上可安装 .NET 10.0 Desktop Runtime 并运行本程序
    - 安装包（`LightBoard-Installer-*.exe`）需要 Windows 10 或更新版本
- .NET 10.0 Desktop Runtime（x64）或更新版本
    - 使用 `with-runtime` 版本可免安装运行时
- Microsoft Office（打开 Office 文档时需要）
    - 需要已激活版本，部分绿色版本需确认 COM 组件已注册
    - Office 2010 及更新版本
    - Office 2007 需安装插件 [Microsoft Save as PDF or XPS](https://legacyupdate.net/download-center/download/7/2007-microsoft-office-add-in-microsoft-save-as-pdf-or-xps)

## 开发与构建

- IDE：Visual Studio Community 2026 或更新版本，工作负载「C# 桌面开发」
- .NET **10.0** SDK 或更新版本（`LangVersion` 为 `preview`）

发布（框架依赖）：

```bash
dotnet publish LightBoard -p:PublishProfile=FolderProfile -c Release
```

输出位于 `LightBoard/bin/publish/`。

发布（自包含）：

```bash
dotnet publish LightBoard -p:PublishProfile=SelfContained -c Release
```

输出位于 `LightBoard/bin/publish-with-runtime/`。

运行测试：

```bash
dotnet test
```

测试宿主为 xunit.v3 + Microsoft Testing Platform，共 264 个用例，覆盖触摸状态机、工具栏交互、文件读写与背景引用。宿主不可用时可直接运行 `LightBoard.Tests/bin/Debug/net10.0-windows/LightBoard.Tests.exe` 查看全量结果。

## 项目结构

```
LightBoard/                       # 主程序
├─ InkCanvasNext/                 # WPF InkCanvas 封装（可独立复用）
│  ├─ InkCanvasNext.xaml(.cs)     # 主控件：模式切换、文档页背景、坐标查询
│  ├─ InkCanvasNext.Properties.cs # 依赖属性注册、公共事件与 CLR 属性
│  ├─ States.cs                   # 触摸状态机（状态元数据与迁移）
│  ├─ Devices.cs / Touches.cs     # 触摸事件采集、转发与捕获
│  ├─ Mouse.cs                    # 鼠标输入路径（形状 / 选区 / 盖章 / 橡皮 / 滚轮）
│  ├─ Gestures.cs                 # 平移与缩放、画布边缘扩展
│  ├─ AutoScroll.cs               # 抬手贴边自动滚动
│  ├─ MultiTouch.cs               # 多指分画与增量渲染
│  ├─ Parameters.cs               # 参数常量与可调阈值
│  ├─ View.cs                     # 视口与内容坐标的相似变换（纯函数）
│  ├─ Eraser.cs                   # 橡皮擦反馈与增量命中
│  ├─ Strokes.cs                  # 墨迹集合、剪贴板、预览与导出
│  ├─ UndoRedo.cs                 # 撤销历史栈
│  ├─ Selection.cs                # 选区集合与包围盒
│  ├─ SelectionInput.cs           # 选择手势入口（触点判定、手柄命中）
│  ├─ SelectionTransform.cs       # 选区变换管线（增量、双指、撤销单元）
│  ├─ SelectionLasso.cs           # 套索圈选
│  ├─ SelectionVisual.cs          # 选区视觉（高亮 / 边框 / 手柄 / 套索轨迹）
│  ├─ Shapes.cs                   # 直线与圆
│  ├─ Geometry.cs                 # 几何工具
│  └─ RingBuffer.cs               # 定容环形缓冲（撤销栈）
├─ Document/                      # 文档渲染与背景引用
│  ├─ Documents.cs                # 打开 / 附加：页集合装配与视图捕获
│  ├─ Backgrounds.cs              # 背景引用（路径 + 页码 + 适配盒）与已打开文档缓存
│  ├─ DocumentService.cs          # Office / XPS 转换与缓存
│  ├─ PdfSource.cs                # PDFium 渲染源
│  ├─ XpsSource.cs                # XPS 渲染源
│  ├─ ImagePageSource.cs          # 图片渲染源
│  ├─ PdfExport.cs                # PDF 导出
│  └─ LruCache.cs                 # 页渲染缓存
├─ External/                      # NativeMethods（窗口切换、窗口区域）、ProgramIcon（进程图标）
├─ MainWindow.xaml(.cs)           # 主窗口、工具栏与画布装配
├─ MainWindow.Handler.cs          # 工具栏交互、菜单与动画
├─ MainWindow.Help.cs             # 帮助浮层的标注清单与开关
├─ MainWindow.IO.cs               # 打开 / 保存 / 导出流程
├─ MainWindow.Loading.cs          # 加载浮层与导出进度
├─ MainWindow.Pages.cs            # 页面列表面板与翻页
├─ MainWindow.Toolbar.cs          # 工具状态（模式 / 笔迹）与选区工具栏定位
├─ HelpVisual.cs                  # 帮助浮层（箭头标注渲染）
├─ DebugVisual.cs                 # 调试浮层（参数可视化、计数、帧率）
├─ FrameRateMeter.cs              # 帧率与最长帧统计
├─ Board.cs                       # .lbf 读写
├─ File.cs                        # 未保存确认、板级载入与附加、快照
├─ Page.cs / Page.Preview.cs      # 页面模型与缩略图
├─ Settings.xaml(.cs)             # 设置窗口
├─ TimerWindow.xaml(.cs)          # 计时窗口
├─ TimerWindow.Size.cs            # 计时窗口尺寸与过渡
├─ TimerWindow.States.cs          # 倒计时 / 正计时状态
├─ Theme.xaml                     # 主题样式
└─ App.xaml(.cs)                  # 入口、单实例、自动备份与异常处理
docs/                             # 设计文档
LightBoard.Tests/                 # 测试（xunit.v3 + MTP）
LightBoard.Installer/             # Inno Setup 安装包脚本
```

## 路线图

见 [ROADMAP](docs/ROADMAP.md)。

## 更新日志

见 [CHANGELOG](CHANGELOG.md)。

## `InkCanvasNext`

WPF `InkCanvas` 现代封装。

复制 [`InkCanvasNext` 文件夹](./LightBoard/InkCanvasNext) 到你的项目即可使用。项目需启用 WPF，并引用 NuGet 包 `DependencyPropertyGenerator`：`InkCanvasNext.Properties.cs` 中的依赖属性由该源生成器产生。

### 公开类型

- `InkCanvasNext`：主控件
- `InkCanvasNextMode`：编辑模式（`Line` / `Circle` 为笔迹上的附加形状模式，`Highlighter` 为荧光笔模式）
- `StampAction`：盖章开关（克隆 / 粘贴）
- `InkCanvasStrokesChangedEventArgs`：`StrokesChanged` 事件的载荷（Added / Removed，选区整体变换时 `TransformOnly` 为 true）
- `MouseWheelAction`：滚轮交互方式（Scroll / Zoom / None）
- `HistorySnapshot`：撤销 / 重做历史栈快照（`SwapHistory` 使用）

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
public InkCanvasNext(Size canvasSize, Point initialOffset, InkCanvasNextOptions? options = null)
```

| 参数            | 说明                                       |
| --------------- | ------------------------------------------ |
| `canvasSize`    | 内层墨迹画布尺寸，文档背景页定位在此画布内 |
| `initialOffset` | 初始视口左上角在画布内容坐标中的位置       |
| `options`       | 构造期选项（`PalmEraseFingers`：掌擦触点数量，4 或 5），`null` 取默认值 |

#### 依赖属性

| 属性                       | 类型                | 说明                                              |
| -------------------------- | ------------------- | ------------------------------------------------- |
| `CanRedo`                  | `bool`              | 只读，是否可以重做                                |
| `CanUndo`                  | `bool`              | 只读，是否可以撤销                                |
| `DefaultDrawingAttributes` | `DrawingAttributes` | 默认笔触属性（颜色 / 粗细 / 荧光笔均在此表达）    |
| `Mode`                     | `InkCanvasNextMode` | 当前编辑模式，默认 `Ink`                          |
| `EraserDiameter`           | `double`            | 面积擦直径，默认 `50.0`                           |
| `MouseWheelAction`         | `MouseWheelAction`  | 滚轮交互：滚动 / 缩放 / 不接管，默认滚动          |
| `StampAction`              | `StampAction`       | 当前盖章模式（克隆 / 粘贴），默认 `None`          |
| `Strokes`                  | `StrokeCollection`  | 墨迹集合，赋值会替换墨迹、清空选区与盖章状态并清空历史 |

#### 事件

| 事件                     | 类型                                               | 说明                                             |
| ------------------------ | -------------------------------------------------- | ------------------------------------------------ |
| `CanRedoChanged`         | `EventHandler<DependencyPropertyChangedEventArgs>` | `CanRedo` 变化时触发                             |
| `CanUndoChanged`         | `EventHandler<DependencyPropertyChangedEventArgs>` | `CanUndo` 变化时触发                             |
| `StrokesChanged`         | `EventHandler<InkCanvasStrokesChangedEventArgs>`   | 墨迹增删或选区整体变换时触发，载荷带 Added / Removed |
| `SelectionChanged`       | `EventHandler`                                     | 选中内容变化时触发                               |
| `ViewOrSelectionChanged` | `EventHandler`                                     | 视口（滚动 / 缩放）或选区包围盒变化，供工具栏跟随 |

#### 属性

| 属性                     | 类型               | 说明                                      |
| ------------------------ | ------------------ | ----------------------------------------- |
| `SelectedStrokes`        | `StrokeCollection` | 当前选中墨迹的快照副本（访问即拷贝）      |
| `SelectedCount`          | `int`              | 当前选中笔触数                            |
| `HasSelection`           | `bool`             | 是否有选中的墨迹                          |
| `CurrentScale`           | `double`           | 当前缩放比例（钳制在 0.1~10）             |
| `OffsetX` / `OffsetY`    | `double`           | 画布滚动偏移（视口单位）                  |
| `ViewportSize`           | `Size`             | 当前视口尺寸（DIP）                       |
| `TouchDisplacementThreshold2` | `double`      | 单指起笔位移阈值，平方值（默认对应 20 视口 DIP） |
| `PanZoomDisplaceThreshold2`   | `double`      | 双指平移锁定阈值，平方值（默认对应 30 视口 DIP） |
| `PinchLockDistance2`          | `double`      | 双指捏合锁定间距，平方值（默认对应 24 视口 DIP） |
| `AutoScrollVerticalThreshold` / `AutoScrollHorizontalThreshold` | `double` | 抬手贴边自动滚动的边缘宽度（视口 DIP），默认 192 |

#### 方法

| 方法                             | 说明                                        |
| -------------------------------- | ------------------------------------------- |
| `Undo()`                         | 撤销上一步墨迹变更                          |
| `Redo()`                         | 重做上一步墨迹变更                          |
| `SetMaxHistoryCount(int)`        | 设置撤销历史容量                            |
| `CopySelected()`                 | 复制选中的墨迹到剪贴板                      |
| `CutSelected()`                  | 剪切选中的墨迹到剪贴板                      |
| `DeleteSelected()`               | 删除选中的墨迹                              |
| `StampCloneAt(Point)`            | 以点击点为副本包围盒中心，克隆盖章当前选区  |
| `StampPasteAt(Point)`            | 以点击点为副本包围盒中心，粘贴剪贴板墨迹    |
| `GetSelectionScreenBounds(Visual)` | 选区包围盒在指定坐标系下的矩形（无选区返回 null） |
| `GetCanvasViewportBounds(UIElement)` | 可见视口在指定坐标系下的矩形             |
| `EnsureStrokesFit()`             | 笔画越出画布右 / 下边界时扩展画布           |
| `EnsureStrokesVisible(StrokeCollection)` | 笔画包围盒不完全在可见内容区域内时平移到视口中心 |
| `ResetTouchState()`              | 重置当前触摸状态并释放所有触摸捕获          |
| `SwapHistory(...)`               | 交换撤销 / 重做历史栈，旧栈经 `out` 参数返回 |
| `SetDocumentPage(ImageSource?, Rect?)` | 设置或清除文档页面背景图像；第二参数为背景页在内容坐标中的 Uniform 适配盒，决定位置与尺寸，页面为 null 或盒子无效时移除 |
| `ClearMultiTouchVisuals()`       | 清空进行中的多指笔画（视觉与笔迹）          |

## 许可证

本项目以 [Apache-2.0 License](http://www.apache.org/licenses/) 提供。
