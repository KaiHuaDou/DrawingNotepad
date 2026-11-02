# `InkCanvasNext`

封装 WPF `InkCanvas`，提供触摸手势、选区、形状、撤销、多指书写与边缘滚动提示。

## 集成

- 把 [`InkCanvasNext` 文件夹](../LightBoard/InkCanvasNext) 复制到目标项目，启用 WPF。
- 引用 NuGet 包 `DependencyPropertyGenerator`：`InkCanvasNext.Properties.cs` 里的依赖属性由它生成。
- 控件不引用宿主资源，配色与按钮样式都写在 `InkCanvasNext.xaml`。
- 控件是 WPF 元素，公开 API 只在 UI 线程调用；放进可视树并完成布局后，`ViewportSize` 与热区按钮才有有效值。

## 坐标系

控件有两套长度单位，混用是最常见的出错来源：

| 单位 | 用在哪里 |
| --- | --- |
| 内容 DIP | 笔画坐标、画布尺寸、选区包围盒、`SetDocumentPage` 的适配盒 |
| 视口 DIP | `OffsetX` / `OffsetY`、`ViewportSize`、手势阈值、`EraserDiameter`、热区宽度 |

换算关系为 `视口坐标 = 内容坐标 × CurrentScale − (OffsetX, OffsetY)`。缩放由 `CanvasGrid` 的 `LayoutTransform` 实现，平移由 `ScrollViewer` 的偏移实现；内容尺寸乘 `CurrentScale` 之后才是它在视口中的尺寸。

## 公开类型

- `InkCanvasNext`：主控件
- `InkCanvasNextOptions`、`EraserParameters`：构造期选项
- `InkCanvasNextMode`：编辑模式
- `StampAction`：盖章开关（克隆 / 粘贴）
- `MouseWheelAction`：滚轮交互方式
- `View`：视口值（缩放与两个偏移）
- `EdgeScrollSide`：视口四侧的边缘热区
- `InkCanvasNextCommands`：默认命令（撤销、重做、删除、复制、剪切、粘贴、取消、放大、缩小、实际大小）
- `StrokeCollectionExtension`：墨迹导出与缩略图渲染
- `InkCanvasStrokesChangedEventArgs`：`StrokesChanged` 的载荷（只读的 Added / Removed 列表；选区整体变换时 `TransformOnly` 为 true）
- `HistorySnapshot`：撤销 / 重做历史栈快照，`Position` 为当前指针（`SwapHistory` 使用）

```csharp
public enum InkCanvasNextMode
{
    Ink,          // 墨迹书写
    EraseStroke,  // 按笔擦除
    EraseArea,    // 按区域擦除
    Select,       // 选择（自绘选择层 + 套索）
    Line,         // 绘制直线
    Circle,       // 绘制圆形
    Highlighter   // 荧光笔（收笔路径与 Ink 相同，半透明由 DefaultDrawingAttributes.IsHighlighter 表达）
}
```

## 构造函数

```csharp
public InkCanvasNext(Size canvasSize, Point initialOffset, InkCanvasNextOptions? options = null)
```

| 参数            | 说明                                       |
| --------------- | ------------------------------------------ |
| `canvasSize`    | 内层墨迹画布尺寸，文档背景页定位在此画布内 |
| `initialOffset` | 初始视口左上角在画布内容坐标中的位置       |
| `options`       | 构造期选项，`null` 取默认值                |

选项只在创建时读取，运行期不可变，改动要重建画布。

| 字段                 | 默认   | 说明                       |
| -------------------- | ------ | -------------------------- |
| `PalmEraseFingers`   | 5      | 掌擦触点数量，取值 4 或 5  |
| `TouchDisplacementThreshold` | 20 | 单指起笔位移阈值，视口 DIP |
| `PanZoomDisplaceThreshold` | 30 | 双指平移锁定阈值，视口 DIP |
| `PinchLockDistance` | 24 | 双指捏合锁定间距，视口 DIP |
| `SmoothZoom` | `true` | 双指缩放按 T/Q/S 平滑曲线跟随（起手死区与过渡阻尼）；`false` 按指距比例线性缩放 |
| `TouchSpacingThreshold` | NaN | 触点间距阈值，视口 DIP；NaN 表示按主屏工作区宽度派生 |
| `EdgeScrollTopThreshold` / `EdgeScrollBottomThreshold` / `EdgeScrollLeftThreshold` / `EdgeScrollRightThreshold` | 64 / 192 / 64 / 96 | 四侧热区宽度，视口 DIP；0 关闭该侧 |
| `MaxHistoryCount` | 200 | 撤销历史容量；0 关闭历史 |
| `WheelAction` | `Scroll` | 滚轮行为：滚动 / 缩放 / 不接管（枚举类型 `MouseWheelAction`） |
| `Eraser.Diameter`    | 75     | 面积擦直径，视口 DIP       |
| `Eraser.PalmExtra`   | 8      | 掌擦圆在触点包络外的外扩量，视口 DIP |
| `Eraser.MinLogicalDiameter` | 1 | 缩放后逻辑直径的下限，内容 DIP |
| `Eraser.RebuildThreshold`   | 0.5 | 逻辑直径变化超过此值才重建命中测试器，内容 DIP |

## 依赖属性

| 属性                       | 类型                | 说明                                              |
| -------------------------- | ------------------- | ------------------------------------------------- |
| `CanRedo`                  | `bool`              | 只读，是否可以重做                                |
| `CanUndo`                  | `bool`              | 只读，是否可以撤销                                |
| `DefaultDrawingAttributes` | `DrawingAttributes` | 默认笔触属性（颜色 / 粗细 / 荧光笔均在此表达）    |
| `Mode`                     | `InkCanvasNextMode` | 当前编辑模式，默认 `Ink`                          |
| `EraserDiameter`           | `double`            | 面积擦直径，初值取自构造期选项（默认 75）         |
| `IsReadOnly`               | `bool`              | 只读模式：输入不改墨迹与选区，平移缩放仍可用      |
| `UseDefaultCommands`       | `bool`              | 让控件可聚焦并接管按键，默认 `false`；不影响命令可用性 |
| `StampAction`              | `StampAction`       | 当前盖章模式（克隆 / 粘贴），默认 `None`          |
| `Strokes`                  | `StrokeCollection`  | 墨迹集合，赋值会替换墨迹、清空选区与盖章状态并清空历史 |

## 事件

| 事件                     | 类型                                               | 说明                                             |
| ------------------------ | -------------------------------------------------- | ------------------------------------------------ |
| `CanRedoChanged`         | `EventHandler<DependencyPropertyChangedEventArgs>` | `CanRedo` 变化时触发                             |
| `CanUndoChanged`         | `EventHandler<DependencyPropertyChangedEventArgs>` | `CanUndo` 变化时触发                             |
| `StrokesChanged`         | `EventHandler<InkCanvasStrokesChangedEventArgs>`   | 墨迹增删或选区整体变换时触发，载荷为只读的 Added / Removed 列表 |
| `SelectionChanged`       | `EventHandler`                                     | 选中内容变化时触发                               |
| `ViewOrSelectionChanged` | `EventHandler`                                     | 视口（滚动 / 缩放）或选区包围盒变化，供工具栏跟随 |
| `EdgeHintsChanged`       | `EventHandler`                                     | 边缘提示的显示状态变化，供宿主刷新自绘的提示 |

## 属性

| 属性                     | 类型               | 说明                                      |
| ------------------------ | ------------------ | ----------------------------------------- |
| `SelectedStrokes`        | `IReadOnlyList<Stroke>` | 当前选中墨迹的快照，笔画实例与画布共享 |
| `SelectedCount`          | `int`              | 当前选中笔触数                            |
| `HasSelection`           | `bool`             | 是否有选中的墨迹                          |
| `CurrentView`            | `View`             | 当前视口；赋值时校验、夹取并写入缩放与偏移 |
| `CurrentScale`           | `double`           | 当前缩放比例，范围 0.1~10                 |
| `OffsetX` / `OffsetY`    | `double`           | 画布滚动偏移（视口单位）                  |
| `ViewportSize`           | `Size`             | 当前视口尺寸（DIP），布局完成前为 0       |

## 边缘墨迹滚动提示

视口四边各有一条热区，宽度取构造期选项里对应侧的 `EdgeScroll*Threshold`。热区内有笔画包围盒时，该侧应有提示。

控件只回答状态与动作，不绘制提示：

- `IsEdgeHintVisible(EdgeScrollSide)` 给出该侧当前是否应显示。
- `EdgeHintsChanged` 在四侧状态变化时触发，宿主据此刷新自己的提示元素。
- `ScrollEdgeClear(EdgeScrollSide)` 平移画布直到该侧热区内没有笔画，过渡 0.18 秒。

细节：

- 四侧阈值都为 0 时整个功能关闭，四侧恒为 false。
- 触摸按下、鼠标按下、滚轮与替换 `Strokes` 都会中断过渡。
- 下 / 右方向余量不足时先扩展画布；上 / 左没有负方向余量，位移超出当前偏移时不显示。
- 判定与平移量都按笔画包围盒计算，边界相接不算命中；目标会多走 1 视口 DIP，避免相切时的浮点误差把提示留在原地。

## 方法

| 方法                             | 说明                                        |
| -------------------------------- | ------------------------------------------- |
| `Undo()`                         | 撤销上一步墨迹变更；擦除手势中调用时，已擦除的部分作为独立一步回退 |
| `Redo()`                         | 重做上一步墨迹变更                          |
| `CopySelected()`                 | 复制选中的墨迹到剪贴板，返回是否成功        |
| `CutSelected()`                  | 剪切选中的墨迹到剪贴板，返回是否成功        |
| `DeleteSelected()`               | 删除选中的墨迹                              |
| `StampCloneAt(Point)`            | 以点击点为副本包围盒中心，克隆盖章当前选区  |
| `StampPasteAt(Point)`            | 以点击点为副本包围盒中心，粘贴剪贴板墨迹，返回是否成功 |
| `ZoomTo(double, Point?)`         | 缩放到指定比例，锚点默认视口中心            |
| `FitToView(Rect, double)`        | 把内容矩形等比装进视口并居中，第二参数为视口 DIP 边距 |
| `GetSelectionScreenBounds(Visual)` | 选区包围盒在指定坐标系下的矩形（无选区返回 null） |
| `GetCanvasViewportBounds(UIElement)` | 可见视口在指定坐标系下的矩形             |
| `EnsureStrokesFit()`             | 笔画越出画布右 / 下边界时扩展画布           |
| `EnsureStrokesVisible(StrokeCollection)` | 笔画包围盒不完全在可见内容区域内时平移到视口中心 |
| `ResetTouchState()`              | 重置当前触摸状态并释放所有触摸捕获          |
| `SwapHistory(...)`               | 交换撤销 / 重做历史栈，旧栈经 `out` 参数返回 |
| `SetDocumentPage(ImageSource?, Rect?)` | 设置或清除文档页面背景图像；第二参数为背景页在内容坐标中的 Uniform 适配盒，决定位置与尺寸，页面为 null 或盒子无效时移除；与当前显示一致时不变 |
| `ClearMultiTouchVisuals()`       | 清空进行中的多指笔画（视觉与笔迹）          |
| `IsEdgeHintVisible(EdgeScrollSide)` | 该侧边缘提示是否应显示                   |
| `ScrollEdgeClear(EdgeScrollSide)` | 平移画布直到该侧热区内没有墨迹            |

## 命令

构造期与控件绑定；可用性由画布状态决定，与 `UseDefaultCommands` 无关。

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

`ZoomIn` / `ZoomOut` 以视口中心为锚点，一步为 `WheelZoomFactor`；`ActualSize` 回到 100%。

宿主把命令挂到工具栏按钮时 `CommandTarget` 必须指向画布：按钮与画布不在同一条路由上。

## 常用常量

`InkCanvasNext` 上的常量：

| 常量 | 值 | 说明 |
| --- | --- | --- |
| `MinScale` / `MaxScale` | 0.1 / 10 | 缩放范围 |
| `WheelZoomFactor` | 1.1 | 滚轮每格缩放倍数 |
| `ScrollStep` | 25 | 滚轮横滚一步的内容 DIP，乘缩放后为视口位移 |

`InkCanvasNextOptions` / `EraserParameters` 上的常量，供选项与设置窗口取默认值与范围：

| 常量 | 值 | 说明 |
| --- | --- | --- |
| `TouchDisplacementThresholdDefault` | 20 | 单指起笔位移，视口 DIP |
| `PanZoomDisplaceThresholdDefault` | 30 | 双指平移锁定位移，视口 DIP |
| `PinchLockDistanceDefault` | 24 | 双指捏合锁定间距，视口 DIP |
| `GestureMin` / `GestureMax` | 1 / 200 | 手势阈值共同范围，视口 DIP |
| `MaxHistoryCountDefault` | 200 | 撤销历史默认容量 |
| `PalmEraseFingersMin` / `PalmEraseFingersMax` / `PalmEraseFingersDefault` | 4 / 5 / 5 | 掌擦触点数量 |
| `EraserParameters.DiameterDefault` | 75 | 面积擦直径，视口 DIP |
| `EraserParameters.PalmExtraDefault` | 8 | 掌擦圆外扩量，视口 DIP |
| `EraserParameters.MinLogicalDiameterDefault` | 1 | 缩放后逻辑直径下限，内容 DIP |
| `EraserParameters.RebuildThresholdDefault` | 0.5 | 命中测试器重建阈值，内容 DIP |
| `EdgeScrollTopDefault` / `EdgeScrollBottomDefault` / `EdgeScrollLeftDefault` / `EdgeScrollRightDefault` | 64 / 192 / 64 / 96 | 热区宽度，视口 DIP |

各选项属性在 init 时把越界值收拢到对应范围。

## 使用注意

- 手势阈值、四侧热区、历史容量与滚轮行为只在构造时读取；`PalmEraseFingers`、橡皮参数同理。改动需重建画布。
- `Strokes` 赋值是替换集合：清空选区、盖章状态与撤销历史，结束进行中的手势，并把画布扩展到容纳全部笔画；赋 null 会变成空集合。
- `OffsetX` / `OffsetY` 的写入是延迟命令（`ScrollViewer.ScrollTo*Offset`），布局完成后才反映到读取值；一次设置整幅视图用 `CurrentView = new View(...)`。
- 普通滚轮交给内层 `ScrollViewer`；Ctrl+滚轮缩放，Alt+滚轮横滚。`WheelAction` 为 `Zoom` 时普通滚轮缩放，`None` 下不接管。
- `Mode` 在触摸手势进行中切换时，进行中的形状被放弃、橡皮反馈按新工具刷新；离开选择模式时选区清空。
- `IsReadOnly` 只挡输入：单指不落笔、不选框、不盖章，五指按平移处理；`Undo` 等公共方法仍可调用。
- `UseDefaultCommands` 只决定控件是否接管键盘：为 true 时控件可聚焦，画布拿到焦点后自身快捷键生效。命令的可用性由画布状态决定，与它无关。
- 宿主在窗口上注册 `InputBindings` 并把 `CommandTarget` 指向画布时，键盘不依赖画布焦点。
- `SwapHistory` 用于在多页之间保存与恢复撤销历史，`HistorySnapshot.Position` 表示当前指针位置。
- 擦除手势按一次手势记一步撤销；手势中发生的清空、粘贴等画布改动各自成为独立的撤销项，次序与发生先后一致。
