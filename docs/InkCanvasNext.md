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
| 视口 DIP | `OffsetX` / `OffsetY`、`ViewportSize`、手势阈值、`EraserDiameter`、热区宽度、`EdgeHintMargin` |

换算关系为 `视口坐标 = 内容坐标 × CurrentScale − (OffsetX, OffsetY)`。缩放由 `CanvasGrid` 的 `LayoutTransform` 实现，平移由 `ScrollViewer` 的偏移实现；内容尺寸乘 `CurrentScale` 之后才是它在视口中的尺寸。

## 公开类型

- `InkCanvasNext`：主控件
- `InkCanvasNextOptions`、`EraserParameters`：构造期选项
- `InkCanvasNextMode`：编辑模式
- `StampAction`：盖章开关（克隆 / 粘贴）
- `MouseWheelAction`：滚轮交互方式
- `InkCanvasStrokesChangedEventArgs`：`StrokesChanged` 的载荷（Added / Removed；选区整体变换时 `TransformOnly` 为 true）
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
| `MouseWheelAction`         | `MouseWheelAction`  | 滚轮交互：滚动 / 缩放 / 不接管，默认滚动          |
| `StampAction`              | `StampAction`       | 当前盖章模式（克隆 / 粘贴），默认 `None`          |
| `Strokes`                  | `StrokeCollection`  | 墨迹集合，赋值会替换墨迹、清空选区与盖章状态并清空历史 |

## 事件

| 事件                     | 类型                                               | 说明                                             |
| ------------------------ | -------------------------------------------------- | ------------------------------------------------ |
| `CanRedoChanged`         | `EventHandler<DependencyPropertyChangedEventArgs>` | `CanRedo` 变化时触发                             |
| `CanUndoChanged`         | `EventHandler<DependencyPropertyChangedEventArgs>` | `CanUndo` 变化时触发                             |
| `StrokesChanged`         | `EventHandler<InkCanvasStrokesChangedEventArgs>`   | 墨迹增删或选区整体变换时触发，载荷带 Added / Removed |
| `SelectionChanged`       | `EventHandler`                                     | 选中内容变化时触发                               |
| `ViewOrSelectionChanged` | `EventHandler`                                     | 视口（滚动 / 缩放）或选区包围盒变化，供工具栏跟随 |

## 属性

| 属性                     | 类型               | 说明                                      |
| ------------------------ | ------------------ | ----------------------------------------- |
| `SelectedStrokes`        | `StrokeCollection` | 当前选中墨迹的快照副本（访问即拷贝）      |
| `SelectedCount`          | `int`              | 当前选中笔触数                            |
| `HasSelection`           | `bool`             | 是否有选中的墨迹                          |
| `CurrentScale`           | `double`           | 当前缩放比例，范围 0.1~10                 |
| `OffsetX` / `OffsetY`    | `double`           | 画布滚动偏移（视口单位）                  |
| `ViewportSize`           | `Size`             | 当前视口尺寸（DIP），布局完成前为 0       |
| `TouchDisplacementThreshold2` | `double`      | 单指起笔位移阈值，平方值（默认对应 20 视口 DIP） |
| `PanZoomDisplaceThreshold2`   | `double`      | 双指平移锁定阈值，平方值（默认对应 30 视口 DIP） |
| `PinchLockDistance2`          | `double`      | 双指捏合锁定间距，平方值（默认对应 24 视口 DIP） |
| `EdgeScrollTopThreshold` / `EdgeScrollBottomThreshold` / `EdgeScrollLeftThreshold` / `EdgeScrollRightThreshold` | `double` | 对应侧热区的宽度（视口 DIP），默认 64 / 192 / 64 / 96；置 0 关闭该侧提示 |
| `EdgeHintMargin` | `Thickness` | 提示按钮距视口四边的间距，默认 8；宿主用它让开工具栏等常驻界面，可随界面变化更新 |

## 边缘墨迹滚动提示

视口四边各有一条热区，宽度取对应侧的 `EdgeScroll*Threshold`。热区内有笔画包围盒时，该侧显示一个箭头按钮：贴视口边缘、沿该侧居中，尺寸与外观同工具栏按钮（36×36、圆角 5）。点击后画布沿该侧平移到热区内没有笔画为止，过渡 0.18 秒。

- 按钮随 `EdgeHintMargin` 贴边摆放，宿主可把某条边距加大以让开常驻界面。
- 按钮模板与颜色写在控件内（`EdgeHintStyle`），不引用宿主资源。
- 四侧阈值都为 0 时整个功能关闭，按钮不占用输入。
- 触摸按下、鼠标按下、滚轮与替换 `Strokes` 都会中断过渡。
- 下 / 右方向余量不足时先扩展画布；上 / 左没有负方向余量，位移超出当前偏移时不显示按钮。
- 判定与平移量都按笔画包围盒计算，边界相接不算命中。

## 方法

| 方法                             | 说明                                        |
| -------------------------------- | ------------------------------------------- |
| `Undo()`                         | 撤销上一步墨迹变更                          |
| `Redo()`                         | 重做上一步墨迹变更                          |
| `SetMaxHistoryCount(int)`        | 按新容量重建撤销历史，超出部分丢弃          |
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

## 常用常量

| 常量 | 值 | 说明 |
| --- | --- | --- |
| `MinScale` / `MaxScale` | 0.1 / 10 | 缩放范围 |
| `WheelZoomFactor` | 1.1 | 滚轮每格缩放倍数 |
| `ScrollStep` | 25 | 滚轮滚动一步的内容 DIP，乘缩放后为视口位移 |
| `TouchDisplacementThresholdDefault` | 20 | 单指起笔位移，视口 DIP |
| `PanZoomDisplaceThresholdDefault` | 30 | 双指平移锁定位移，视口 DIP |
| `PinchLockDistanceDefault` | 24 | 双指捏合锁定间距，视口 DIP |
| `MaxHistoryCount` | 200 | 撤销历史默认容量 |
| `PalmEraseFingersMin` / `PalmEraseFingersMax` / `PalmEraseFingersDefault` | 4 / 5 / 5 | 掌擦触点数量 |
| `EraserDiameterDefault` | 75 | 面积擦直径，视口 DIP |
| `EraserPalmExtraDefault` | 8 | 掌擦圆外扩量，视口 DIP |
| `EraserMinLogicalDiameterDefault` | 1 | 缩放后逻辑直径下限，内容 DIP |
| `EraserRebuildThresholdDefault` | 0.5 | 命中测试器重建阈值，内容 DIP |
| `EdgeScrollTopDefaultThreshold` / `EdgeScrollBottomDefaultThreshold` / `EdgeScrollLeftDefaultThreshold` / `EdgeScrollRightDefaultThreshold` | 64 / 192 / 64 / 96 | 热区宽度，视口 DIP |
| `EdgeHintMarginDefault` | 8 | 提示按钮边距，视口 DIP |

## 使用注意

- `Strokes` 赋值是替换集合：清空选区、盖章状态与撤销历史，并把画布扩展到容纳全部笔画。
- `OffsetX` / `OffsetY` 的写入是延迟命令（`ScrollViewer.ScrollTo*Offset`），布局完成后才反映到读取值；一次设置整幅视图时先写 `CurrentScale`，再写偏移。
- `Mode` 在触摸手势进行中切换时，进行中的形状被放弃、橡皮反馈按新工具刷新；离开选择模式时选区清空。
- `SwapHistory` 用于在多页之间保存与恢复撤销历史，`HistorySnapshot.Position` 表示当前指针位置。
