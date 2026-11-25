# `InkCanvasNext` 类

封装 WPF `InkCanvas`，提供墨迹书写、擦除、选择、形状绘制、撤销与重做、多指书写、手写笔输入及边缘滚动提示。

命名空间：`InkCanvasNext`  
程序集：`LightBoard`

**继承层次结构**  
`Object` → `DispatcherObject` → `DependencyObject` → `Visual` → `UIElement` → `FrameworkElement` → `Control` → `ContentControl` → `UserControl` → `InkCanvasNext`

## 语法

```csharp
public partial class InkCanvasNext : UserControl
```

控件没有无参数构造函数，须在代码中创建并加入可视树。模板、画笔与按钮样式定义在 `InkCanvasNext.xaml`，不依赖宿主资源。

## 构造函数

### `InkCanvasNext(Size, Point, InkCanvasNextOptions)`

初始化 `InkCanvasNext` 类的新实例。

```csharp
public InkCanvasNext(Size canvasSize, Point initialOffset, InkCanvasNextOptions? options = null);
```

#### 参数

| 参数 | 类型 | 说明 |
| --- | --- | --- |
| `canvasSize` | `Size` | 内层墨迹画布尺寸，单位为内容 DIP；文档背景页定位在该矩形内 |
| `initialOffset` | `Point` | 初始视口左上角在内容坐标中的位置，单位为内容 DIP |
| `options` | `InkCanvasNextOptions` | 构造期选项；为 `null` 时使用默认值 |

#### 备注

选项只在创建时读取，创建后不可变。控件加入可视树并完成布局后，`ViewportSize` 与四侧边缘提示才返回有效值。

## 属性

| 属性 | 类型 | 说明 |
| --- | --- | --- |
| `CanRedo` | `bool` | 获取一个值，该值指示是否可以重做；只读依赖项属性 |
| `CanUndo` | `bool` | 获取一个值，该值指示是否可以撤销；只读依赖项属性 |
| `CanvasSize` | `Size` | 获取画布当前尺寸，单位为内容 DIP；随内容扩展而增大 |
| `CurrentScale` | `double` | 获取或设置当前缩放比例；值限制在 `MinScale` 与 `MaxScale` 之间 |
| `CurrentView` | `View` | 获取或设置当前视口；赋值时校验并限制缩放与偏移，写入此属性可一次设置整幅视图 |
| `DrawingAttributes` | `DrawingAttributesNext` | 获取或设置当前工具设置；默认双向绑定，写入时归一化并应用到编辑模式与笔触属性 |
| `EraserDiameter` | `double` | 获取或设置面积擦直径，单位为视口 DIP；初值取自构造期选项 |
| `EraserLabel` | `string?` | 获取或设置橡皮反馈圆显示的文本；`null` 或空字符串时不显示 |
| `HasSelection` | `bool` | 获取一个值，该值指示当前是否有选中的墨迹 |
| `IsGestureActive` | `bool` | 获取一个值，该值指示是否有触摸序列、合成触点或原生笔捕获在途 |
| `IsReadOnly` | `bool` | 获取或设置只读模式；只读时输入不改动墨迹与选区，平移和缩放仍可用 |
| `OffsetX` | `double` | 获取或设置画布横向滚动偏移，单位为视口 DIP |
| `OffsetY` | `double` | 获取或设置画布纵向滚动偏移，单位为视口 DIP |
| `SelectedCount` | `int` | 获取当前选中的笔触数 |
| `SelectedStrokes` | `IReadOnlyList<Stroke>` | 获取当前选中墨迹的只读快照；笔画实例与画布共享 |
| `Strokes` | `StrokeCollection` | 获取或设置墨迹集合；赋值会替换墨迹、清空选区与盖章状态并清空历史 |
| `UseDefaultCommands` | `bool` | 获取或设置控件是否接管键盘；默认 `false`，不影响命令的可用性 |
| `ViewportSize` | `Size` | 获取当前视口尺寸，单位为 DIP；布局完成前为 0 |
| `DefaultPenColor` | `Color` | 获取普通画笔的默认颜色 `#E6E6E6`；静态属性 |

`StrokesProperty`：标识 `Strokes` 依赖项属性的 `DependencyProperty` 字段。

## 方法

| 方法 | 说明 |
| --- | --- |
| `Undo()` | 撤销上一步墨迹变更 |
| `Redo()` | 重做上一步墨迹变更 |
| `CopySelected()` | 将选中的墨迹复制到剪贴板，返回是否成功 |
| `CutSelected()` | 将选中的墨迹剪切到剪贴板，返回是否成功 |
| `DeleteSelected()` | 删除选中的墨迹 |
| `StampCloneAt(Point)` | 以指定点为副本包围盒中心，克隆盖章当前选区 |
| `StampPasteAt(Point)` | 以指定点为副本包围盒中心，粘贴剪贴板墨迹，返回是否成功 |
| `ZoomTo(double, Point?)` | 缩放到指定比例，锚点默认为视口中心 |
| `FitToView(Rect, double)` | 把内容矩形等比装进视口并居中 |
| `GetSelectionScreenBounds(Visual)` | 返回选区包围盒在指定坐标系中的矩形；无选区时返回 `null` |
| `GetCanvasViewportBounds(UIElement)` | 返回可见视口在指定坐标系中的矩形 |
| `EnsureStrokesFit()` | 把画布扩展到容纳全部笔画；左/上越界时平移内容 |
| `EnsureStrokesVisible(StrokeCollection)` | 在笔画包围盒未完全进入可见内容区域时平移到视口中心 |
| `ResetTouchState()` | 重置当前触摸状态并释放所有触摸捕获 |
| `SwapHistory(out HistorySnapshot?, HistorySnapshot?)` | 交换撤销与重做历史栈，旧栈由 `out` 参数返回 |
| `SetDocumentPage(ImageSource?, Rect?)` | 设置或清除文档页面背景图像 |
| `ClearMultiTouchVisuals()` | 清空在途多指笔画的视觉与笔迹 |
| `IsEdgeHintVisible(EdgeScrollSide)` | 返回指定侧当前是否应显示边缘提示 |
| `ScrollEdgeClear(EdgeScrollSide)` | 平移画布直到指定侧热区内没有笔画 |

### 方法详情

#### `bool CopySelected()` / `bool CutSelected()`

返回值：剪贴板写入成功时为 `true`；剪贴板被占用等失败情况为 `false`。

#### `bool StampPasteAt(Point point)`

| 参数 | 类型 | 说明 |
| --- | --- | --- |
| `point` | `Point` | 副本包围盒的中心，单位为视口 DIP |

返回值：剪贴板包含墨迹并成功落章时为 `true`。

#### `void FitToView(Rect contentRect, double margin = 0)`

| 参数 | 类型 | 说明 |
| --- | --- | --- |
| `contentRect` | `Rect` | 要装入视口的内容矩形，单位为内容 DIP |
| `margin` | `double` | 视口 DIP 边距，默认 0 |

#### `void ZoomTo(double scale, Point? viewportAnchor = null)`

| 参数 | 类型 | 说明 |
| --- | --- | --- |
| `scale` | `double` | 目标缩放比例，限制在 `MinScale` 与 `MaxScale` 之间 |
| `viewportAnchor` | `Point?` | 视口坐标中的缩放锚点；为 `null` 时取视口中心 |

#### `void SetDocumentPage(ImageSource? page, Rect? fitBox)`

| 参数 | 类型 | 说明 |
| --- | --- | --- |
| `page` | `ImageSource` | 背景页图像；为 `null` 时清除背景 |
| `fitBox` | `Rect?` | 背景页在内容坐标中的等比适配盒，决定背景的位置与尺寸；越出左/上边界时平移内容 |

页面与适配盒和当前显示一致时不作改动。

#### `void SwapHistory(out HistorySnapshot? old, HistorySnapshot? @new)`

在多页之间保存与恢复撤销历史。`old` 接收交换前的历史；传入 `null` 表示清空目标页历史。`HistorySnapshot.Position` 表示当前指针位置。

## 事件

| 事件 | 类型 | 说明 |
| --- | --- | --- |
| `CanRedoChanged` | `EventHandler<DependencyPropertyChangedEventArgs>` | `CanRedo` 变化时触发 |
| `CanUndoChanged` | `EventHandler<DependencyPropertyChangedEventArgs>` | `CanUndo` 变化时触发 |
| `StrokesChanged` | `EventHandler<InkCanvasStrokesChangedEventArgs>` | 墨迹增删或选区整体变换时触发 |
| `OperationCompleted` | `EventHandler<InkCanvasOperationEventArgs>` | 一笔书写完成或一次擦除手势结束时触发；撤销与重做的重放不触发 |
| `DrawingAttributesChanged` | `EventHandler` | `DrawingAttributes` 变化时触发；同值写入不触发 |
| `SelectionChanged` | `EventHandler` | 选中内容变化时触发 |
| `ViewOrSelectionChanged` | `EventHandler` | 视口滚动、缩放或选区包围盒变化，供工具栏跟随 |
| `EdgeHintsChanged` | `EventHandler` | 四侧边缘提示的显示状态变化，供宿主刷新提示元素 |
| `ContentShifted` | `EventHandler<InkCanvasContentShiftedEventArgs>` | 左/上扩展后内容整体平移时触发 |

## 字段

`InkCanvasNext` 上的常量：

| 字段 | 值 | 说明 |
| --- | --- | --- |
| `MinScale` | 0.1 | 最小缩放比例 |
| `MaxScale` | 10 | 最大缩放比例 |
| `WheelZoomFactor` | 1.1 | 滚轮与缩放命令每格使用的倍数 |
| `ScrollStep` | 25 | 滚轮横滚一步的内容 DIP，乘缩放后为视口位移 |
| `SelectionToolbarGap` | 8 | 选区工具栏与选区之间的间距，单位为视口 DIP |

选项与设置使用的常量见各选项类型小节。

## 枚举

### `InkCanvasNextMode` 枚举

表示编辑模式。

```csharp
public enum InkCanvasNextMode
{
    Ink, EraseStroke, EraseArea, Select, Line, Circle
}
```

| 成员 | 说明 |
| --- | --- |
| `Ink` | 使用当前笔触属性绘制墨迹 |
| `EraseStroke` | 按笔画擦除墨迹 |
| `EraseArea` | 按区域擦除墨迹 |
| `Select` | 选择墨迹笔画 |
| `Line` | 绘制直线 |
| `Circle` | 绘制圆形 |

### `StampAction` 枚举

表示盖章动作。

| 成员 | 说明 |
| --- | --- |
| `None` | 无盖章动作 |
| `Clone` | 克隆选区墨迹 |
| `Paste` | 粘贴剪贴板中的墨迹 |

### `SelectionDragMode` 枚举

表示在空白处拖动创建选区的方式。

| 成员 | 说明 |
| --- | --- |
| `Lasso` | 套索圈选 |
| `Rectangle` | 对角线矩形框选 |

### `MouseWheelAction` 枚举

表示鼠标滚轮的动作模式。

| 成员 | 说明 |
| --- | --- |
| `Scroll` | 滚动画布 |
| `Zoom` | 缩放画布 |
| `None` | 忽略鼠标滚轮 |

### `EdgeScrollSide` 枚举

表示视口四侧的边缘热区。

| 成员 | 说明 |
| --- | --- |
| `Top` | 上侧 |
| `Bottom` | 下侧 |
| `Left` | 左侧 |
| `Right` | 右侧 |

### `InkCanvasOperation` 枚举

表示一次工具操作的种类。

| 成员 | 说明 |
| --- | --- |
| `Stroke` | 一笔书写完成 |
| `Erase` | 一次擦除手势结束 |

## 相关类型

### `DrawingAttributesNext` 类

表示当前工具设置。读写 `DrawingAttributes` 时取副本，外部修改持有的实例不影响画布。

```csharp
public sealed record DrawingAttributesNext
{
    public InkCanvasNextMode Mode { get; init; }
    public StampAction Stamp { get; init; }
    public DrawingAttributes DrawingAttributes { get; init; }
}
```

| 属性 | 类型 | 说明 |
| --- | --- | --- |
| `Mode` | `InkCanvasNextMode` | 获取或设置编辑模式，默认 `Ink` |
| `Stamp` | `StampAction` | 获取或设置盖章动作，默认 `None` |
| `DrawingAttributes` | `DrawingAttributes` | 获取或设置笔触属性；写入时克隆 |

`Mode` 或 `Stamp` 取未定义枚举值时分别取 `Ink` 与 `None`；`Mode` 非 `Ink` / `Select` 时 `Stamp` 取 `None`。荧光笔是笔触属性：`DrawingAttributes.IsHighlighter` 为 `true` 时外观由调用方提供的 `DrawingAttributes` 决定，控件不内置颜色与宽度。

### `View` 结构

表示视口值，缩放与两个偏移均为内容坐标系中的量。

```csharp
public readonly record struct View(double Scale, double OffsetX, double OffsetY)
```

| 方法 | 说明 |
| --- | --- |
| `ToViewport(Point)` | 内容坐标转换为视口坐标 |
| `ToContent(Point)` | 视口坐标转换为内容坐标 |
| `ZoomAt(Point, double)` | 以视口锚点缩放到指定比例 |
| `Pan(Vector)` | 按视口位移平移 |
| `Clamp(Size, Size, double, double)` | 按内容尺寸、视口尺寸与缩放范围限制 |
| `Clamp(double, double)` | 按横纵最大偏移限制 |

### `InkCanvasNextOptions` 类

表示构造期选项。所有属性在初始化时校验并收拢越界值，创建后不可变。

| 属性 | 类型 | 默认 | 说明 |
| --- | --- | --- | --- |
| `PalmEraseFingers` | `int` | 5 | 掌擦触点数量，取值 4 或 5 |
| `PalmMinWidth` | `double` | 150 | 手掌触点长边阈值，单位为视口 DIP |
| `PalmMinHeight` | `double` | 100 | 手掌触点短边阈值，单位为视口 DIP |
| `PalmMergeDistance` | `double` | 120 | 手掌碎片聚类距离，单位为视口 DIP |
| `TouchDisplacementThreshold` | `double` | 20 | 单指起笔位移阈值，单位为视口 DIP |
| `PanZoomDisplaceThreshold` | `double` | 30 | 双指平移锁定阈值，单位为视口 DIP |
| `PinchLockDistance` | `double` | 24 | 双指捏合锁定间距，单位为视口 DIP |
| `SmoothZoom` | `bool` | `true` | 双指缩放按 T / Q / S 平滑曲线跟随；`false` 时按指距比例缩放 |
| `TouchSpacingThreshold` | `double` | NaN | 触点间距阈值，单位为视口 DIP；NaN 时按主屏工作区宽度派生 |
| `EdgeScrollTopThreshold` 等四侧 | `double` | 64 / 192 / 64 / 96 | 四侧热区宽度，单位为视口 DIP；0 关闭该侧 |
| `MaxHistoryCount` | `int` | 200 | 撤销历史容量；0 关闭历史 |
| `WheelAction` | `MouseWheelAction` | `Scroll` | 滚轮行为 |
| `SelectionDragMode` | `SelectionDragMode` | `Lasso` | 空白处拖动创建选区的方式 |
| `Eraser` | `EraserParameters` | — | 橡皮参数 |
| `Taper` | `TaperParameters` | — | 笔锋参数 |
| `Pen` | `PenParameters` | — | 手写笔参数 |

常用常量：

| 常量 | 值 | 说明 |
| --- | --- | --- |
| `GestureMin` / `GestureMax` | 1 / 200 | 手势阈值共同范围，单位为视口 DIP |
| `PalmEraseFingersMin` / `PalmEraseFingersMax` / `PalmEraseFingersDefault` | 4 / 5 / 5 | 掌擦触点数量范围与默认值 |
| `PalmMinWidthMin` / `PalmMinWidthMax` / `PalmMinWidthDefault` | 100 / 400 / 150 | 手掌长边阈值范围与默认值，单位为视口 DIP |
| `PalmMinHeightMin` / `PalmMinHeightMax` / `PalmMinHeightDefault` | 60 / 300 / 100 | 手掌短边阈值范围与默认值，单位为视口 DIP |
| `PalmMergeDistanceMin` / `PalmMergeDistanceMax` / `PalmMergeDistanceDefault` | 60 / 200 / 120 | 聚类距离范围与默认值，单位为视口 DIP |
| `TouchDisplacementThresholdDefault` | 20 | 单指起笔位移默认值 |
| `PanZoomDisplaceThresholdDefault` | 30 | 双指平移锁定阈值默认值 |
| `PinchLockDistanceDefault` | 24 | 双指捏合锁定间距默认值 |
| `EdgeScrollTopDefault` / `EdgeScrollBottomDefault` / `EdgeScrollLeftDefault` / `EdgeScrollRightDefault` | 64 / 192 / 64 / 96 | 四侧热区默认值，单位为视口 DIP |
| `EdgeScrollThresholdMin` / `EdgeScrollThresholdMax` | 0 / 1000 | 热区宽度范围，单位为视口 DIP |
| `MaxHistoryCountDefault` / `MaxHistoryCountMin` / `MaxHistoryCountMax` | 200 / 0 / 1000 | 撤销历史默认值与范围 |

### `EraserParameters` 类

表示橡皮参数，单位为视口 DIP，末两项除外。

| 属性 | 类型 | 默认 | 说明 |
| --- | --- | --- | --- |
| `Diameter` | `double` | 75 | 面积擦直径，单位为视口 DIP |
| `PalmExtra` | `double` | 8 | 掌擦圆在触点包络外的外扩量，单位为视口 DIP |
| `PalmBaseDiameter` | `double` | 75 | 面积掌擦基础直径，按接触区域对角线分档放大，单位为视口 DIP |
| `PalmMinDiameter` | `double` | 120 | 面积掌擦最小直径，单位为视口 DIP |
| `PalmSizeAdjustDuration` | `int` | 100 | 擦除开始后按面积调整直径的时长，单位为毫秒 |
| `MinLogicalDiameter` | `double` | 1 | 缩放后逻辑直径下限，单位为内容 DIP |
| `RebuildThreshold` | `double` | 0.5 | 逻辑直径变化超过此值时重建命中测试器，单位为内容 DIP |

常用常量：`DiameterDefault` 75，`DiameterMin` 1，`DiameterMax` 400，`PalmExtraDefault` 8，`PalmExtraMin` 0，`PalmExtraMax` 100，`PalmBaseDiameterDefault` 75，`PalmBaseDiameterMin` 40，`PalmBaseDiameterMax` 150，`PalmMinDiameterDefault` 120，`PalmMinDiameterMin` 40，`PalmMinDiameterMax` 300，`PalmSizeAdjustDurationDefault` 100，`PalmSizeAdjustDurationMin` 0，`PalmSizeAdjustDurationMax` 500，`MinLogicalDiameterDefault` 1，`MinLogicalDiameterMin` 0.1，`MinLogicalDiameterMax` 10，`RebuildThresholdDefault` 0.5，`RebuildThresholdMin` 0.01，`RebuildThresholdMax` 10。

### `TaperParameters` 类

表示笔锋参数。

| 属性 | 类型 | 默认 | 说明 |
| --- | --- | --- | --- |
| `Enabled` | `bool` | `true` | 笔画末端渐细开关 |
| `Points` | `int` | 8 | 末端渐细的采样点数，取值 2~40 |
| `Wet` | `bool` | `true` | 书写过程中显示渐细；关闭后湿墨按无笔锋绘制，落笔后的笔锋保留 |

常用常量：`EnabledDefault` `true`，`WetDefault` `true`，`PointsDefault` 8，`PointsMin` 2，`PointsMax` 40。

### `PenParameters` 类

表示手写笔参数。

| 属性 | 类型 | 默认 | 说明 |
| --- | --- | --- | --- |
| `UseDevicePressure` | `bool` | `true` | 手写笔按设备压力改变粗细 |
| `HoverCursor` | `bool` | `true` | 手写笔悬停时显示笔尖圆环 |
| `BarrelErase` | `bool` | `true` | 落笔前按住笔杆按钮时临时按笔擦除 |

常用常量：`UseDevicePressureDefault`、`HoverCursorDefault`、`BarrelEraseDefault` 均为 `true`。

### `HistorySnapshot` 类

表示撤销与重做历史栈快照。

| 属性 | 类型 | 说明 |
| --- | --- | --- |
| `Position` | `int` | 当前指针位置；之前的条目为可撤销项，之后的条目为可重做项 |

### `InkCanvasStrokesChangedEventArgs` 类

`StrokesChanged` 的载荷。

| 属性 | 类型 | 说明 |
| --- | --- | --- |
| `Added` | `IReadOnlyList<Stroke>` | 新增的笔画；无新增时为空列表 |
| `Removed` | `IReadOnlyList<Stroke>` | 移除的笔画；无移除时为空列表 |
| `TransformOnly` | `bool` | 选区整体变换时为 `true`，此时无笔画增删 |

### `InkCanvasContentShiftedEventArgs` 类

`ContentShifted` 的载荷。

| 属性 | 类型 | 说明 |
| --- | --- | --- |
| `Offset` | `Vector` | 内容在内容坐标中的平移量 |
| `Strokes` | `StrokeCollection` | 被平移的墨迹集合，用于判定内容归属 |

### `InkCanvasOperationEventArgs` 类

`OperationCompleted` 的载荷。

| 属性 | 类型 | 说明 |
| --- | --- | --- |
| `Operation` | `InkCanvasOperation` | 完成的操作种类 |
| `DrawingAttributes` | `DrawingAttributesNext` | 完成该次操作时的工具设置 |

### `InkCanvasNextCommands` 类

表示画布的默认命令。命令在构造期与控件绑定，可用性由画布状态决定，与 `UseDefaultCommands` 无关。

| 命令 | 默认按键 | 可用条件 |
| --- | --- | --- |
| `Undo` | `Ctrl+Z` | 可撤销且非只读 |
| `Redo` | `Ctrl+Y`、`Ctrl+Shift+Z` | 可重做且非只读 |
| `Delete` | `Delete` | 有选区且非只读 |
| `Copy` | `Ctrl+C` | 有选区 |
| `Cut` | `Ctrl+X` | 有选区且非只读 |
| `Paste` | `Ctrl+V` | 非只读 |
| `Cancel` | `Esc` | 始终可执行 |
| `ZoomIn` | `Ctrl+=`、`Ctrl+小键盘 +` | 始终可执行 |
| `ZoomOut` | `Ctrl+-`、`Ctrl+小键盘 -` | 始终可执行 |
| `ActualSize` | `Ctrl+0`、`Ctrl+小键盘 0` | 始终可执行 |

`ZoomIn` / `ZoomOut` 以视口中心为锚点，一步为 `WheelZoomFactor`；`ActualSize` 回到 100%。宿主把命令挂到工具栏按钮时，`CommandTarget` 必须指向画布。

### `StrokeCollectionExtension` 类

提供墨迹的导出、缩略图渲染与边界计算。

| 成员 | 说明 |
| --- | --- |
| `DefaultCanvasColor` | 未指定画布底色时导出使用的颜色，静态属性 |
| `Render(StrokeCollection, DpiScale, int, Color?, ImageSource?, Rect?)` | 返回按缩放比例渲染的冻结位图 |
| `ExportPng(StrokeCollection, string, DpiScale, int, Color?, ImageSource?, Rect?)` | 按缩放比例导出 PNG 文件 |
| `PreviewEmpty(Color?)` | 返回空页缩略图 |
| `Preview(StrokeCollection, ImageSource?, Rect?, Color?)` | 返回页面缩略图，可在后台线程调用 |
| `ImageBounds(Rect, ImageSource)` | 返回背景图在内容坐标中的显示矩形，等比装进适配盒并在盒内居中 |
| `ContentBounds(StrokeCollection, ImageSource?, Rect?)` | 返回导出裁剪框；内容为空时使用固定画布尺寸并留出边距 |

## 备注

### 集成

- 把 [`InkCanvasNext` 文件夹](../LightBoard/InkCanvasNext) 复制到目标项目并启用 WPF。
- 引用 NuGet 包 `DependencyPropertyGenerator`：`InkCanvasNext.Properties.cs` 与 `DrawingAttributesNext.cs` 中的依赖属性由它生成。
- 控件是 WPF 元素，公开成员只在 UI 线程使用。

```csharp
var canvas = new InkCanvasNext(canvasSize, initialOffset);
MainGrid.Children.Insert(0, canvas);
```

### 坐标系

控件使用两套长度单位：

| 单位 | 用在哪里 |
| --- | --- |
| 内容 DIP | 笔画坐标、画布尺寸、选区包围盒、`SetDocumentPage` 的适配盒 |
| 视口 DIP | `OffsetX` / `OffsetY`、`ViewportSize`、手势阈值、`EraserDiameter`、热区宽度 |

换算关系为 `视口坐标 = 内容坐标 × CurrentScale − (OffsetX, OffsetY)`。内容尺寸乘 `CurrentScale` 之后才是它在视口中的尺寸。

内容坐标从 (0, 0) 起算。内容越过左/上边界，或空闲视口在左/上方向的余量不足半屏时，控件按整屏扩展画布并把墨迹、文档背景与历史锚点整体向对应方向平移，同时抬高偏移；屏幕画面不动，`ContentShifted` 提供平移量供宿主同步依赖内容坐标的数据。右/下扩展只改画布尺寸，不移动墨迹。

### 工具设置

荧光笔是笔触属性，不是编辑模式。`DrawingAttributes` 的子项在写入与读取时都克隆，外部持有或修改实例不影响画布。切换编辑模式并清除印章时须显式设置 `Mode` 与 `Stamp`。手势进行中更改 `DrawingAttributes` 会放弃在途形状并按新工具刷新橡皮反馈；离开选择模式时选区清空。

### 手写笔

触摸、鼠标与手写笔按“触摸 > 鼠标 > 手写笔”仲裁：高优先级按下时低优先级不落墨，在途笔迹放弃；高优先级抬起后，低优先级须重新按下。

- `Ink` 与 `EraseStroke` 使用原生收笔；`Line`、`Circle`、`Select`、`EraseArea` 与盖章经合成触点进入触摸状态机。
- 手写笔宽度等于 `笔宽 × 设备压力`，压力低于 0.25 时按 25% 宽度计算；触摸与鼠标宽度固定为笔宽。
- 手写笔不应用末端笔锋；触摸与鼠标按 `TaperParameters` 渐细。
- 绘制中迁入多指书写时，被提交的原生笔画末端不渐细，由续接的多指笔画连接。
- 反转笔在 `EraseArea` 下按面积擦，其余工具按笔擦；只读时正锋与反转都不改动墨迹。
- 悬停圆环直径跟随当前笔宽与缩放，按下、离开范围、触摸或鼠标驱动、窗口失活时隐藏。
- 设备压力只对确认属于手写笔的平板设备改写。

### 掌擦

手掌触点由触点尺寸分类，与按触点数量触发的掌擦共用擦除状态：

- 尺寸在 20~2000 视口 DIP 内才参与判定；长边大于 `PalmMinWidth` 且短边大于 `PalmMinHeight` 的触点记为手掌。
- 离已有手掌触点不超过 `PalmMergeDistance` 的触点同样记为手掌；相邻碎片的面积和超过 `PalmMinWidth × PalmMinHeight` 时，候选触点一并记为手掌。
- 手掌触点不计入手指数、指距与多指书写，只参与擦除几何。
- 空闲、单指评估与平移评估下立即接管；绘制、多指、平移与缩放已提交时等手指全部抬起；选区手势要求全部触点都在选区外。只读时不接管。
- 直径取面积直径与包络直径的较大者：面积直径按手掌簇包围盒对角线分档放大 `PalmBaseDiameter`，包络直径取簇质心到最远触点的距离乘二加 `PalmExtra`；结果限制在 `PalmMinDiameter` 与 `EraserParameters.DiameterMax` 之间。
- 直径在擦除开始后的 `PalmSizeAdjustDuration` 内随手掌调整，此后固定；手掌簇成员变化时在新中心重建命中测试器并重开调整窗口。
- 本次擦除用过手掌后，手掌抬起即结束擦除，剩余手指不续擦；没有手掌时仍按 `PalmEraseFingers` 的触点包络擦除。
- 设备不上报触点尺寸（0 或 1 × 1）时没有手掌触点，行为与按触点数量擦除一致。

### 边缘滚动提示

视口四边各有一条热区，宽度取构造期选项中对应侧的 `EdgeScroll*Threshold`。热区内有笔画包围盒时，该侧应有提示。控件只提供状态与动作，提示元素由宿主绘制：

- `IsEdgeHintVisible(EdgeScrollSide)` 返回该侧当前是否应显示。
- `EdgeHintsChanged` 在四侧状态变化时触发，宿主据此刷新提示元素。
- `ScrollEdgeClear(EdgeScrollSide)` 平移画布直到该侧热区内没有笔画，过渡 0.18 秒。

细节：

- 四侧阈值均为 0 时该功能关闭，四侧均返回 `false`。
- 触摸按下、鼠标按下、滚轮与替换 `Strokes` 都会中断过渡。
- 四侧余量不足时先扩展画布；左/上扩展会平移内容并抬高偏移。
- 判定与平移量按笔画包围盒计算，边界相接不算命中；目标多走 1 视口 DIP，避免相切时的浮点误差把提示留在原地。

### 使用注意

- 手势阈值、四侧热区、历史容量与滚轮行为只在构造时读取；`PalmEraseFingers`、手掌阈值与橡皮参数同理。改动需重建画布。
- `Strokes` 赋值是替换集合：清空选区、盖章状态与撤销历史，结束在途手势，并把画布扩展到容纳全部笔画；赋 `null` 时为空集合。
- `OffsetX` / `OffsetY` 的写入在布局完成后才反映到读取值；一次设置整幅视图用 `CurrentView`。
- `WheelAction` 为 `Scroll` 时普通滚轮交给内层滚动视图；`Ctrl` + 滚轮缩放，`Alt` + 滚轮横滚。`Zoom` 时普通滚轮缩放，`None` 时不接管。
- `IsReadOnly` 只挡输入：单指不落笔、不选框、不盖章，手指数量达到掌擦阈值时按平移处理；`Undo` 等公共方法仍可调用。
- `UseDefaultCommands` 只决定控件是否接管键盘；命令的可用性由画布状态决定，与它无关。宿主在窗口上注册 `InputBindings` 并把 `CommandTarget` 指向画布时，键盘不依赖画布焦点。
- 擦除手势按一次手势记一步撤销；手势中发生的清空、粘贴等画布改动各自成为独立撤销项，次序与调用先后一致。

## 适用于

| 产品 | 版本 |
| --- | --- |
| Windows Desktop | .NET 11 及更高版本 |
| 操作系统 | Windows 7 SP1 及更高版本 |
