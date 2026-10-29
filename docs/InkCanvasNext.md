# `InkCanvasNext`

封装 WPF `InkCanvas`，提供触摸手势、选区、形状、撤销与多指书写。

把 [`InkCanvasNext` 文件夹](../LightBoard/InkCanvasNext) 复制到目标项目，启用 WPF，并引用 NuGet 包 `DependencyPropertyGenerator`（`InkCanvasNext.Properties.cs` 里的依赖属性由它生成）。

## 公开类型

- `InkCanvasNext`：主控件
- `InkCanvasNextOptions`、`EraserParameters`：构造期选项
- `InkCanvasNextMode`：编辑模式
- `StampAction`：盖章开关（克隆 / 粘贴）
- `MouseWheelAction`：滚轮交互方式
- `InkCanvasStrokesChangedEventArgs`：`StrokesChanged` 的载荷（Added / Removed；选区整体变换时 `TransformOnly` 为 true）
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
| `Eraser.Diameter`    | 50     | 面积擦直径，视口 DIP       |
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
| `EraserDiameter`           | `double`            | 面积擦直径，初值取自构造期选项（默认 50）         |
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
| `ViewportSize`           | `Size`             | 当前视口尺寸（DIP）                       |
| `TouchDisplacementThreshold2` | `double`      | 单指起笔位移阈值，平方值（默认对应 20 视口 DIP） |
| `PanZoomDisplaceThreshold2`   | `double`      | 双指平移锁定阈值，平方值（默认对应 30 视口 DIP） |
| `PinchLockDistance2`          | `double`      | 双指捏合锁定间距，平方值（默认对应 24 视口 DIP） |
| `AutoScrollVerticalThreshold` / `AutoScrollHorizontalThreshold` | `double` | 抬手贴边自动滚动的边缘宽度（视口 DIP），默认 192 |

## 方法

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
