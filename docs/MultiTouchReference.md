# InkCanvas 多指同时绘图实现详解

## 1. 概述

Ink Canvas 通过自定义 WPF 的 `InkCanvas` 事件处理，实现了多指同时书写（Multi-Touch Ink）能力。默认 WPF `InkCanvas` 同一时间只响应单个触摸输入；本项目通过为每一根手指/手写笔维护独立的 `StrokeVisual` 与 `VisualCanvas`，在 `PreviewTouchDown`、`PreviewTouchMove`、`PreviewTouchUp` 及 `StylusDown/Move/Up` 事件中分别处理，最终把每一路笔迹提交到 `inkCanvas.Strokes` 集合。

核心设计要点：

- **按设备 ID 隔离笔迹**：`Dictionary<int, StrokeVisual>` 与 `Dictionary<int, VisualCanvas>` 以 `TouchDevice.Id` / `StylusDevice.Id` 为键。
- **自定义渲染管线**：`StrokeVisual` 在 `DrawingVisual` 上实时绘制线段，`VisualCanvas` 作为 `FrameworkElement` 宿主这些视觉对象。
- **实时速度笔锋**：对无压感触摸输入，通过移动速度动态计算 `PressureFactor`，模拟毛笔/钢笔效果。
- **触摸插值与贝塞尔平滑**：低速触摸采样稀疏时，在点之间插入直线或基于切线的三次贝塞尔点。
- **与双指手势互斥**：启用多指书写后，同模式下的双指平移/缩放/旋转会被强制禁用。
- **按页面持久化**：多指模式开关状态随白板页面一起保存/恢复。

---

## 2. 核心数据结构

### 2.1 多指状态字段

位置：`Ink Canvas/MainWindow_cs/MW_TouchEvents.cs:20-65`

```csharp
private bool isInMultiTouchMode;                         // 是否处于显式“多指书写模式”
private List<int> dec = new List<int>();                 // 当前按下的触摸设备 ID 列表
private bool isSingleFingerDragMode;                     // 单指拖动模式标志
private Point centerPoint = new Point(0, 0);             // 多指手势中心点
private InkCanvasEditingMode lastInkCanvasEditingMode = InkCanvasEditingMode.Ink;
private DateTime lastTouchDownTime = DateTime.MinValue;
private const double MULTI_TOUCH_DELAY_MS = 100;         // 多指检测延迟
private bool isMultiTouchTimerActive;
private bool isPalmEraserActive;
private bool palmEraserWasEnabledBeforeMultiTouch;
private InkCanvasEditingMode palmEraserPreviousEditingMode = InkCanvasEditingMode.Ink;

// 实时速度笔锋状态，key = TouchDevice.Id / StylusDevice.Id / MouseRealtimeStrokeId
private readonly Dictionary<int, RealtimeBrushTipState> _realtimeBrushTipStates = new Dictionary<int, RealtimeBrushTipState>();
private readonly Guid RealtimeVelocityBrushTipAppliedGuid = new Guid("74E57D95-945F-4A8C-B52A-7D3EF2D4FD5B");
internal const int MouseRealtimeStrokeId = -100001;

private readonly HashSet<int> _activeRealtimeTouchStrokeIds = new HashSet<int>();
private readonly HashSet<int> _activeRealtimeStylusStrokeIds = new HashSet<int>();
private readonly HashSet<int> _activeTouchStrokeIds = new HashSet<int>();
```

### 2.2 按设备 ID 维护的笔迹容器

位置：`Ink Canvas/MainWindow_cs/MW_TouchEvents.cs:1431-1442`

```csharp
// 每个触摸/手写笔 ID 对应的按下点编辑模式
private Dictionary<int, InkCanvasEditingMode> TouchDownPointsList { get; } =
    new Dictionary<int, InkCanvasEditingMode>();

// 每个 ID 对应的 StrokeVisual（实时笔画对象）
private Dictionary<int, StrokeVisual> StrokeVisualList { get; } = new Dictionary<int, StrokeVisual>();

// 每个 ID 对应的 VisualCanvas（WPF 视觉宿主）
private Dictionary<int, VisualCanvas> VisualCanvasList { get; } = new Dictionary<int, VisualCanvas>();
```

---

## 3. 视觉渲染层：VisualCanvas 与 StrokeVisual

位置：`Ink Canvas/Helpers/MultiTouchInput.cs`

### 3.1 VisualCanvas

`VisualCanvas` 继承自 `FrameworkElement`，内部维护 `DrawingVisual` 列表，作为轻量级渲染宿主：

```csharp
public class VisualCanvas : FrameworkElement
{
    private readonly List<DrawingVisual> _visuals = new List<DrawingVisual>();

    protected override Visual GetVisualChild(int index) => _visuals[index];
    protected override int VisualChildrenCount => _visuals.Count;

    public VisualCanvas()
    {
        CacheMode = new BitmapCache();
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
        RenderOptions.SetCachingHint(this, CachingHint.Cache);
    }

    public void AddVisual(DrawingVisual visual) { ... }
    public void RemoveVisual(DrawingVisual visual) { ... }
    public void Clear() { ... }
}
```

### 3.2 StrokeVisual

`StrokeVisual` 封装了一条正在绘制的 `Stroke`，并提供基于 `DrawingVisual` 的增量重绘：

```csharp
public class StrokeVisual
{
    private int _lastCommittedPointCount = 0;
    private const int COMMIT_POINT_THRESHOLD = 24;
    private DrawingVisual _activeVisual;
    private VisualCanvas _visualCanvas;

    public Stroke Stroke { set; get; }

    public void SetVisualCanvas(VisualCanvas visualCanvas) => _visualCanvas = visualCanvas;

    public void Add(StylusPoint point)
    {
        if (Stroke == null)
        {
            var collection = new StylusPointCollection { point };
            Stroke = new Stroke(collection) { DrawingAttributes = _drawingAttributes };
        }
        else
        {
            Stroke.StylusPoints.Add(point);
        }
    }

    public void Redraw() { ... }
    public void ForceRedraw() { ... }
}
```

增量渲染逻辑（`Redraw`）为了避免每次移动都重绘整段笔迹：

```csharp
public void Redraw()
{
    if (Stroke == null || _visualCanvas == null) return;
    var currentPointCount = Stroke.StylusPoints.Count;
    if (currentPointCount == 0) return;

    if (_activeVisual == null)
    {
        _activeVisual = CreateDrawingVisual();
        _visualCanvas.AddVisual(_activeVisual);
    }

    var activeStartIndex = _lastCommittedPointCount == 0 ? 0 : _lastCommittedPointCount - 1;
    DrawSegment(_activeVisual, activeStartIndex, currentPointCount);

    if (currentPointCount - _lastCommittedPointCount >= COMMIT_POINT_THRESHOLD)
    {
        _visualCanvas.RemoveVisual(_activeVisual);
        _activeVisual = null;
        CommitActiveVisual(currentPointCount);
    }
}
```

当点数因“停顿拉直”等功能回退时，`ForceRedraw` 会清空已提交视觉并重建：

```csharp
public void ForceRedraw()
{
    if (currentPointCount < _lastCommittedPointCount)
    {
        _visualCanvas.Clear();
        _activeVisual = null;
        _lastCommittedPointCount = 0;
    }
    Redraw();
}
```

---

## 4. 创建/获取每指笔迹容器

位置：`Ink Canvas/MainWindow_cs/MW_TouchEvents.cs:1268-1293`

```csharp
private StrokeVisual GetStrokeVisual(int id)
{
    if (StrokeVisualList.TryGetValue(id, out var visual)) return visual;

    var strokeVisual = new StrokeVisual(inkCanvas.DefaultDrawingAttributes.Clone());
    StrokeVisualList[id] = strokeVisual;
    var visualCanvas = new VisualCanvas();
    strokeVisual.SetVisualCanvas(visualCanvas);
    VisualCanvasList[id] = visualCanvas;
    inkCanvas.Children.Add(visualCanvas);

    return strokeVisual;
}

private VisualCanvas GetVisualCanvas(int id)
{
    return VisualCanvasList.TryGetValue(id, out var visualCanvas) ? visualCanvas : null;
}
```

每出现新的 `id`，就会在 `inkCanvas.Children` 中新增一个 `VisualCanvas`，用于承载该手指的实时笔迹。

---

## 5. 多指模式开关

### 5.1 设置项

位置：`Ink Canvas/Resources/Settings.cs:377-395`

```csharp
[JsonProperty("isEnableMultiTouchMode")]
public bool IsEnableMultiTouchMode { get; set; } = false;

[JsonProperty("isEnableMultiTouchModeBoard")]
public bool IsEnableMultiTouchModeBoard { get; set; } = false;
```

### 5.2 开关事件

位置：`Ink Canvas/MainWindow_cs/MW_Settings.cs:872-988`

当用户打开多指书写开关时：

```csharp
private void ToggleSwitchEnableMultiTouchMode_Toggled(object sender, RoutedEventArgs e)
{
    ...
    if (isOn)
    {
        if (!isInMultiTouchMode)
        {
            InkCanvasEditingMode currentEditingMode = inkCanvas.EditingMode;
            int currentDrawingShapeMode = drawingShapeMode;
            bool currentForceEraser = forceEraser;

            // 注册手写笔与触摸事件，取消主网格默认 TouchDown
            inkCanvas.StylusDown += MainWindow_StylusDown;
            inkCanvas.StylusMove += MainWindow_StylusMove;
            inkCanvas.StylusUp += MainWindow_StylusUp;
            inkCanvas.TouchDown += MainWindow_TouchDown;
            inkCanvas.TouchDown -= Main_Grid_TouchDown;

            inkCanvas.EditingMode = InkCanvasEditingMode.None;
            var preservedElements = PreserveNonStrokeElements();
            inkCanvas.Children.Clear();
            RestoreNonStrokeElements(preservedElements);
            isInMultiTouchMode = true;

            palmEraserWasEnabledBeforeMultiTouch = Settings.Canvas.EnablePalmEraser;
            Settings.Canvas.EnablePalmEraser = false;
            SaveSettingsToFile();

            // 恢复编辑状态
            inkCanvas.EditingMode = currentEditingMode;
            drawingShapeMode = currentDrawingShapeMode;
            forceEraser = currentForceEraser;
        }
    }
    else { ... }

    EnsureRealtimeStylusPipelineBinding();

    // 启用多指书写则强制禁用同模式双指手势，避免冲突
    if (isOn)
    {
        if (isBoardSender)
        {
            Settings.Gesture.IsEnableTwoFingerTranslateBoard = false;
            Settings.Gesture.IsEnableTwoFingerZoomBoard = false;
            Settings.Gesture.IsEnableTwoFingerRotationBoard = false;
            ...
        }
        else { ... }
    }
}
```

### 5.3 白板页面持久化

位置：`Ink Canvas/MainWindow_cs/MW_BoardControls.cs:49-52` 与 `:363-391`

```csharp
// 每页保存一个多指模式状态
private bool[] savedMultiTouchModeStates = new bool[101];

private void RestoreMultiTouchModeState(int pageIndex)
{
    if (savedMultiTouchModeStates[pageIndex])
    {
        if (ToggleSwitchEnableMultiTouchMode != null)
            ToggleSwitchEnableMultiTouchMode.IsOn = true;
    }
    else
    {
        if (ToggleSwitchEnableMultiTouchMode != null)
            ToggleSwitchEnableMultiTouchMode.IsOn = false;
    }
}
```

在 `SaveStrokes` 与 `RestoreStrokes` 中同步保存/恢复，保证切页后多指模式状态不变。

---

## 6. 触摸按下：PreviewTouchDown

位置：`Ink Canvas/MainWindow_cs/MW_TouchEvents.cs:1558-1757`

`InkCanvas_PreviewTouchDown` 是多指书写的入口。主要分支：

### 6.1 图形绘制模式

```csharp
if (drawingShapeMode != 0)
{
    inkCanvas.EditingMode = InkCanvasEditingMode.None;
    ...
    isTouchDown = true;
    if (dec.Count == 0)
    {
        var inkTouchPoint = e.GetTouchPoint(inkCanvas);
        if (drawingShapeMode == 24 || drawingShapeMode == 25)
        {
            if (drawMultiStepShapeCurrentStep == 0)
                iniP = inkTouchPoint.Position;
        }
        else
        {
            iniP = inkTouchPoint.Position;
        }
        lastTouchDownStrokeCollection = inkCanvas.Strokes.Clone();
    }
    dec.Add(e.TouchDevice.Id);
    return;
}
```

### 6.2 实时速度笔锋分支

```csharp
if (ShouldUseRealtimeVelocityBrushTipForTouch()
    && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
    && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke
    && inkCanvas.EditingMode != InkCanvasEditingMode.Select)
{
    inkCanvas.EditingMode = InkCanvasEditingMode.None;
    var touchId = e.TouchDevice.Id;
    var p = e.GetTouchPoint(inkCanvas).Position;
    _activeRealtimeTouchStrokeIds.Add(touchId);
    BeginTouchInkInput();
    CancelPauseStraightenTimer(touchId);
    InitializeRealtimeBrushTipStateFromPoint(touchId, p);
    var sv = GetStrokeVisual(touchId);
    TryAppendRealtimeVelocityBrushTipInterpolatedPoints(sv, touchId, p);
    sv.Redraw();
    return;
}
```

### 6.3 普通多指书写分支

```csharp
if ((isInMultiTouchMode || (currentMode == 1 ? Settings.Gesture.IsEnableMultiTouchModeBoard : Settings.Gesture.IsEnableMultiTouchMode))
    && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
    && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke
    && inkCanvas.EditingMode != InkCanvasEditingMode.Select)
{
    inkCanvas.EditingMode = InkCanvasEditingMode.None;
    var touchId = e.TouchDevice.Id;
    var p = e.GetTouchPoint(inkCanvas).Position;
    _activeTouchStrokeIds.Add(touchId);
    BeginTouchInkInput();
    CancelPauseStraightenTimer(touchId);
    var sv = GetStrokeVisual(touchId);
    AppendInterpolatedTouchPoints(sv, touchId, p);
    sv.Redraw();
    return;
}
```

### 6.4 双指手势切换逻辑

当检测到第二根手指在 100ms 内按下，并且当前处于 `Ink` 模式时，启动延迟任务把 `EditingMode` 切为 `None`，从而释放 WPF 默认的单笔画绘制，转为多指手控：

```csharp
if (dec.Count > 1 || isSingleFingerDragMode || !Settings.Gesture.IsEnableTwoFingerGesture)
{
    if (isInMultiTouchMode || !Settings.Gesture.IsEnableTwoFingerGesture) return;
    if (inkCanvas.EditingMode == InkCanvasEditingMode.None ||
        inkCanvas.EditingMode == InkCanvasEditingMode.Select) return;

    var timeSinceLastTouch = (DateTime.Now - lastTouchDownTime).TotalMilliseconds;
    if (timeSinceLastTouch < MULTI_TOUCH_DELAY_MS && inkCanvas.EditingMode == InkCanvasEditingMode.Ink)
    {
        if (!isMultiTouchTimerActive)
        {
            isMultiTouchTimerActive = true;
            var remainingTime = MULTI_TOUCH_DELAY_MS - timeSinceLastTouch;
            Task.Delay((int)remainingTime).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (dec.Count > 1 && inkCanvas.EditingMode == InkCanvasEditingMode.Ink)
                        inkCanvas.EditingMode = InkCanvasEditingMode.None;
                    isMultiTouchTimerActive = false;
                });
            });
        }
        return;
    }

    lastInkCanvasEditingMode = inkCanvas.EditingMode;
    if (inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
        && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke
        && drawingShapeMode == 0)
    {
        inkCanvas.EditingMode = InkCanvasEditingMode.None;
    }
}
```

---

## 7. 触摸移动：PreviewTouchMove

位置：`Ink Canvas/MainWindow_cs/MW_TouchEvents.cs:1767-1807`

```csharp
private void InkCanvas_PreviewTouchMove(object sender, TouchEventArgs e)
{
    if (isPalmEraserActive)
    {
        var touchPoint = e.GetTouchPoint(inkCanvas);
        EraserOverlay_PointerMove(sender, touchPoint.Position);
        return;
    }

    var touchId = e.TouchDevice.Id;

    // 实时速度笔锋路径
    if (_activeRealtimeTouchStrokeIds.Contains(touchId))
    {
        var p = e.GetTouchPoint(inkCanvas).Position;
        var sv = GetStrokeVisual(touchId);
        if (TryAppendRealtimeVelocityBrushTipInterpolatedPoints(sv, touchId, p))
            sv.Redraw();
        return;
    }

    // 普通多指路径
    if (_activeTouchStrokeIds.Contains(touchId))
    {
        var p = e.GetTouchPoint(inkCanvas).Position;
        var sv = GetStrokeVisual(touchId);
        AppendInterpolatedTouchPoints(sv, touchId, p);
        sv.Redraw();
    }
}
```

---

## 8. 触摸抬起：PreviewTouchUp

位置：`Ink Canvas/MainWindow_cs/MW_TouchEvents.cs:1831-1986`

主要工作：

1. 将 `StrokeVisual.Stroke` 提交到 `inkCanvas.Strokes`。
2. 触发 `inkCanvas_StrokeCollected` 事件，以便时间机器记录。
3. 清理字典与 `VisualCanvas`。
4. 恢复编辑模式。
5. 处理手掌擦退出逻辑。

```csharp
private void InkCanvas_PreviewTouchUp(object sender, TouchEventArgs e)
{
    var touchId = e.TouchDevice.Id;

    // 实时速度笔锋路径提交
    if (_activeRealtimeTouchStrokeIds.Contains(touchId))
    {
        try
        {
            var sv = GetStrokeVisual(touchId);
            sv?.ForceRedraw();
            var stroke = sv?.Stroke;
            if (stroke != null)
            {
                if (!stroke.ContainsPropertyData(RealtimeVelocityBrushTipAppliedGuid))
                    stroke.AddPropertyData(RealtimeVelocityBrushTipAppliedGuid, true);
                inkCanvas.Strokes.Add(stroke);
                inkCanvas_StrokeCollected(inkCanvas, new InkCanvasStrokeCollectedEventArgs(stroke));
            }
        }
        finally
        {
            if (VisualCanvasList.TryGetValue(touchId, out var visualCanvas) && inkCanvas.Children.Contains(visualCanvas))
                inkCanvas.Children.Remove(visualCanvas);
            StrokeVisualList.Remove(touchId);
            VisualCanvasList.Remove(touchId);
            TouchDownPointsList.Remove(touchId);
            CleanupRealtimeBrushTipState(touchId);
            CancelPauseStraightenTimer(touchId);
            _activeRealtimeTouchStrokeIds.Remove(touchId);
            EndTouchInkInputIfIdle();
        }
    }
    else if (_activeTouchStrokeIds.Contains(touchId))
    {
        // 普通多指路径提交
        ...
    }

    inkCanvas.ReleaseAllTouchCaptures();
    ...
    dec.Remove(e.TouchDevice.Id);
    ...
}
```

---

## 9. 手写笔事件处理

位置：`Ink Canvas/MainWindow_cs/MW_TouchEvents.cs:902-1252`

手写笔通过 `StylusDown / StylusMove / StylusUp` 处理。关键点：

- 忽略来自触摸屏的 Stylus 设备（`IsTouchStylusDevice`），避免与 `Touch` 事件重复处理。
- 倒置笔尾自动切换为 `EraseByPoint`。
- 实时速度笔锋路径与普通 `Ink` 路径并存。

### 9.1 StylusDown

```csharp
private void MainWindow_StylusDown(object sender, StylusDownEventArgs e)
{
    if (IsTouchStylusDevice(e.StylusDevice))
        return;

    // 点击浮动栏则放行
    var stylusPoint = e.GetPosition(this);
    if (TryBlockInkInputOverFloatingBar(stylusPoint, e))
        return;

    if (e.StylusDevice.Inverted)
    {
        inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
    }
    else
    {
        ...
        inkCanvas.EditingMode = ShouldUseRealtimeVelocityBrushTip()
            ? InkCanvasEditingMode.None
            : InkCanvasEditingMode.Ink;
    }

    inkCanvas.CaptureStylus();
    ...

    var stylusId = e.StylusDevice.Id;
    if (ShouldUseRealtimeVelocityBrushTip() && ...)
    {
        // 清理旧视觉
        if (VisualCanvasList.TryGetValue(stylusId, out var staleCanvas) && inkCanvas.Children.Contains(staleCanvas))
            inkCanvas.Children.Remove(staleCanvas);
        StrokeVisualList.Remove(stylusId);
        VisualCanvasList.Remove(stylusId);
        TouchDownPointsList.Remove(stylusId);
        CleanupRealtimeBrushTipState(stylusId);

        var p = e.GetPosition(inkCanvas);
        _activeRealtimeStylusStrokeIds.Add(stylusId);
        BeginTouchInkInput();
        CancelPauseStraightenTimer(stylusId);
        InitializeRealtimeBrushTipState(stylusId, e);
        var sv = GetStrokeVisual(stylusId);
        TryAppendRealtimeVelocityBrushTipInterpolatedPoints(sv, stylusId, p);
        sv.Redraw();
        ...
        e.Handled = true;
        return;
    }

    InitializeRealtimeBrushTipState(e.StylusDevice.Id, e);
    ...
}
```

### 9.2 StylusMove

```csharp
private void MainWindow_StylusMove(object sender, StylusEventArgs e)
{
    ...
    var stylusId = e.StylusDevice.Id;
    if (_activeRealtimeStylusStrokeIds.Contains(stylusId))
    {
        var p = e.GetPosition(inkCanvas);
        var sv = GetStrokeVisual(stylusId);
        if (TryAppendRealtimeVelocityBrushTipInterpolatedPoints(sv, stylusId, p))
            sv.Redraw();
        ResetPauseStraightenTimer(stylusId);
        e.Handled = true;
        return;
    }

    ...
    var strokeVisual = GetStrokeVisual(e.StylusDevice.Id);
    var isHandledByRealtime = TryAppendRealtimeVelocityBrushTipPoints(strokeVisual, e);
    if (!isHandledByRealtime)
    {
        var stylusPointCollection = e.GetStylusPoints(this);
        foreach (var stylusPoint in stylusPointCollection)
            strokeVisual.Add(new StylusPoint(stylusPoint.X, stylusPoint.Y, stylusPoint.PressureFactor));
    }

    ResetPauseStraightenTimer(e.StylusDevice.Id);
    strokeVisual.Redraw();
}
```

### 9.3 StylusUp

与普通 TouchUp 类似，提交 `Stroke` 到 `inkCanvas.Strokes`，清理资源。

---

## 10. 触摸插值与平滑

位置：`Ink Canvas/MainWindow_cs/MW_TouchEvents.cs:280-374`

### 10.1 普通多指插值

当触摸采样率不足时，在相邻点之间线性插值，避免笔迹出现明显折线：

```csharp
private static IEnumerable<Point> InterpolateTouchPoints(Point from, Point to)
{
    var dx = to.X - from.X;
    var dy = to.Y - from.Y;
    var distance = Math.Sqrt(dx * dx + dy * dy);
    var steps = Math.Min(24, Math.Max(1, (int)Math.Ceiling(distance / 1.2)));
    for (var i = 1; i <= steps; i++)
    {
        var t = (double)i / steps;
        yield return new Point(from.X + dx * t, from.Y + dy * t);
    }
}
```

### 10.2 基于方向的贝塞尔插值

如果已知上一次的移动方向，则使用三次贝塞尔曲线插值，使笔迹更平滑：

```csharp
private static IEnumerable<Point> InterpolateTouchPoints(RealtimeBrushTipState state, Point to)
{
    if (!state.HasTouchDirection)
    {
        foreach (var p in InterpolateTouchPoints(state.LastTouchPoint, to))
            yield return p;
        yield break;
    }

    var from = state.LastTouchPoint;
    var chord = to - from;
    var distance = chord.Length;
    if (distance < 0.1)
        yield break;

    var incoming = state.LastTouchDirection;
    if (incoming.LengthSquared > 0.0001)
    {
        incoming.Normalize();
        var current = chord;
        current.Normalize();
        var dot = Math.Max(-1, Math.Min(1, incoming.X * current.X + incoming.Y * current.Y));
        var angle = Math.Acos(dot);
        if (angle < Math.PI * 0.72)
        {
            var tangentLength = Math.Min(distance * 0.45, 18);
            var c1 = from + incoming * tangentLength;
            var c2 = to - current * tangentLength;
            var steps = Math.Min(24, Math.Max(1, (int)Math.Ceiling(distance / 1.2)));
            for (var i = 1; i <= steps; i++)
            {
                var t = (double)i / steps;
                var u = 1 - t;
                yield return new Point(
                    u * u * u * from.X + 3 * u * u * t * c1.X + 3 * u * t * t * c2.X + t * t * t * to.X,
                    u * u * u * from.Y + 3 * u * u * t * c1.Y + 3 * u * t * t * c2.Y + t * t * t * to.Y);
            }
            yield break;
        }
    }

    foreach (var p in InterpolateTouchPoints(from, to))
        yield return p;
}
```

### 10.3 追加插值点

```csharp
private void AppendInterpolatedTouchPoints(StrokeVisual strokeVisual, int strokeId, Point point)
{
    if (strokeVisual == null) return;
    if (!_realtimeBrushTipStates.TryGetValue(strokeId, out var state))
    {
        state = new RealtimeBrushTipState { HasTouchPoint = true, LastTouchPoint = point };
        _realtimeBrushTipStates[strokeId] = state;
        strokeVisual.Add(new StylusPoint(point.X, point.Y, 0.5f));
        return;
    }

    if (!state.HasTouchPoint)
    {
        ...
    }

    foreach (var p in InterpolateTouchPoints(state, point))
    {
        strokeVisual.Add(new StylusPoint(p.X, p.Y, 0.5f));
    }
    UpdateTouchInterpolationState(state, point);
}
```

---

## 11. 实时速度笔锋（模拟压感）

位置：`Ink Canvas/MainWindow_cs/MW_TouchEvents.cs:111-576`

### 11.1 状态对象

```csharp
private sealed class RealtimeBrushTipState
{
    public float LastRawX { get; set; }
    public float LastRawY { get; set; }
    public long LastTimestampMs { get; set; }
    public float SmoothedSampleRateHz { get; set; } = 120f;
    public bool SawPressureVariation { get; set; }
    public bool HasSeed { get; set; }
    public bool HasTouchPoint { get; set; }
    public Point PreviousTouchPoint { get; set; }
    public Point LastTouchPoint { get; set; }
    public bool HasTouchDirection { get; set; }
    public Vector LastTouchDirection { get; set; }
    public float LastSmoothX { get; set; }
    public float LastSmoothY { get; set; }
    public float LastSmoothPressure { get; set; } = 0.5f;
    public OneEuroFilter FilterX { get; } = new OneEuroFilter(1.2f, 0.015f, 1f);
    public OneEuroFilter FilterY { get; } = new OneEuroFilter(1.2f, 0.015f, 1f);
    public OneEuroFilter FilterPressure { get; } = new OneEuroFilter(1f, 0.02f, 1f);
}
```

### 11.2 One Euro Filter（一欧元滤波）

用于平滑坐标与压感，兼顾低频稳定性与高频响应：

```csharp
private sealed class OneEuroFilter
{
    private readonly float _minCutoff;
    private readonly float _beta;
    private readonly float _dCutoff;
    private bool _initialized;
    private float _xPrev;
    private float _dxPrev;

    public OneEuroFilter(float minCutoff, float beta, float dCutoff)
    {
        _minCutoff = minCutoff;
        _beta = beta;
        _dCutoff = dCutoff;
    }

    public float Filter(float value, float dt, float speed)
    {
        if (!_initialized)
        {
            _initialized = true;
            _xPrev = value;
            _dxPrev = 0f;
            return value;
        }

        var dx = (value - _xPrev) / Math.Max(1e-6f, dt);
        var aD = Alpha(_dCutoff, dt);
        var dxHat = Lerp(_dxPrev, dx, aD);
        var a = Alpha(_minCutoff + _beta * speed, dt);
        var xHat = Lerp(_xPrev, value, a);
        _xPrev = xHat;
        _dxPrev = dxHat;
        return xHat;
    }

    private static float Alpha(float cutoff, float dt) { ... }
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
```

### 11.3 速度转压感

核心公式：移动越快，笔画越细；移动越慢，笔画越粗。同时可混入硬件压感：

```csharp
private bool TryAppendRealtimeVelocityBrushTipPoint(StrokeVisual strokeVisual, int strokeId, Point point, float rawPressure = 0.5f)
{
    ...
    var mix = RealtimeClamp((float)Settings.Canvas.VelocityBrushTipMix, 0f, 1f);
    var nowMs = RealtimeNowMs();
    var dtMs = Math.Max(1L, nowMs - state.LastTimestampMs);
    var dt = dtMs / 1000f;
    var sampleRate = 1f / Math.Max(1e-4f, dt);
    state.SmoothedSampleRateHz = state.SmoothedSampleRateHz * 0.85f + sampleRate * 0.15f;
    var baseWidth = (float)Math.Max(0.35,
        strokeVisual.Stroke?.DrawingAttributes?.Width ?? inkCanvas.DefaultDrawingAttributes.Width);

    var rawX = (float)point.X;
    var rawY = (float)point.Y;
    var dx = rawX - state.LastRawX;
    var dy = rawY - state.LastRawY;
    var dist = (float)Math.Sqrt(dx * dx + dy * dy);
    var speed = dist / dt;

    var filteredX = state.FilterX.Filter(rawX, dt, speed);
    var filteredY = state.FilterY.Filter(rawY, dt, speed);

    rawPressure = RealtimeClamp(rawPressure, 0f, 1f);
    if (Math.Abs(rawPressure - 0.5f) > 0.02f)
        state.SawPressureVariation = true;
    var usePressure = state.SawPressureVariation && rawPressure > 0f;

    var width = baseWidth;
    if (usePressure)
        width *= 0.25f + 0.75f * rawPressure;
    var speedNormalization = 1800f + state.SmoothedSampleRateHz * 3.5f;
    width *= RealtimeClamp(1.15f - (speed / speedNormalization), 0.45f, 1.25f);
    var speedPressure = WidthToPressure(width, baseWidth);

    var pressure = usePressure
        ? ((1f - mix) * rawPressure + mix * speedPressure)
        : speedPressure;
    pressure = RealtimeClamp(pressure, 0.08f, 1f);
    pressure = state.FilterPressure.Filter(pressure, dt, speed);
    ...
}
```

### 11.4 最小距离过滤与抖动抑制

为避免过密采样导致视觉锯齿，对鼠标输入启用最小距离过滤；对触摸输入则通过插值补充：

```csharp
var minDist = GetRealtimeBrushTipMinDistance(state.SmoothedSampleRateHz);
if (dist < minDist && state.HasSeed && strokeId == MouseRealtimeStrokeId)
{
    state.LastRawX = rawX;
    state.LastRawY = rawY;
    state.LastTimestampMs = nowMs;
    return true;
}

if (!state.HasSeed)
{
    state.HasSeed = true;
    state.LastSmoothX = filteredX;
    state.LastSmoothY = filteredY;
    state.LastSmoothPressure = pressure;
    strokeVisual.Add(new StylusPoint(filteredX, filteredY, pressure));
}
else
{
    // 中点链减抖：保持实时笔锋同时降低折线锯齿
    var midX = (state.LastSmoothX + filteredX) * 0.5f;
    var midY = (state.LastSmoothY + filteredY) * 0.5f;
    var midPressure = (state.LastSmoothPressure + pressure) * 0.5f;
    strokeVisual.Add(new StylusPoint(midX, midY, midPressure));
    state.LastSmoothX = filteredX;
    state.LastSmoothY = filteredY;
    state.LastSmoothPressure = pressure;
}
```

### 11.5 启用条件

```csharp
private bool ShouldUseRealtimeVelocityBrushTip()
{
    return Settings.Canvas.InkStyle == 3
        && Settings.Canvas.VelocityBrushTipMix > 0
        && !Settings.Canvas.DisablePressure;
}

private bool ShouldUseRealtimeVelocityBrushTipForTouch()
{
    return Settings.Canvas.InkStyle == 3
        && Settings.Canvas.VelocityBrushTipMix > 0
        && !Settings.Canvas.DisablePressure
        && drawingShapeMode == 0
        && !isPalmEraserActive;
}
```

---

## 12. 与 Manipulation（双指手势）的协调

位置：`Ink Canvas/MainWindow_cs/MW_TouchEvents.cs:1996-2196`

`Main_Grid_ManipulationDelta` 处理双指平移/旋转/缩放。多指书写模式打开时，会强制禁用同模式下的双指手势；在普通模式下，两根手指同时按下会触发手势而非书写。

---

## 13. 手掌擦（Palm Eraser）的交互

位置：`Ink Canvas/MainWindow_cs/MW_TouchEvents.cs:1663-1715`

在 `InkCanvas_PreviewTouchDown` 中，如果检测到接触面积超过阈值，则自动切换到 `EraseByPoint`：

```csharp
if (Settings.Canvas.EnablePalmEraser && !isPalmEraserActive && drawingShapeMode == 0)
{
    var touchPoint = e.GetTouchPoint(inkCanvas);
    double boundWidth = GetTouchBoundWidth(e);

    if ((Settings.Advanced.TouchMultiplier != 0 || !Settings.Advanced.IsSpecialScreen)
        && (boundWidth > BoundsWidth))
    {
        ...
        if (boundWidth > BoundsWidth * EraserThresholdValue * thresholdMultiplier)
        {
            ...
            palmEraserPreviousEditingMode = inkCanvas.EditingMode;
            inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
            isPalmEraserActive = true;
            EnableEraserOverlay();
            eraserWidth = boundWidth;
            UpdateEraserStyle();
            ...
        }
    }
}
```

进入显式多指书写模式时，`EnablePalmEraser` 会被临时关闭，避免手掌被误判为橡皮擦。

---

## 14. 关键流程图

```
用户触摸屏幕
    │
    ▼
InkCanvas_PreviewTouchDown
    │
    ├─ 图形模式？ ──► 记录起点，加入 dec
    │
    ├─ 实时速度笔锋开启？ ──► _activeRealtimeTouchStrokeIds.Add(id)
    │                          GetStrokeVisual(id) + TryAppendRealtimeVelocityBrushTipInterpolatedPoints
    │
    ├─ 多指模式开启？ ──► _activeTouchStrokeIds.Add(id)
    │                      GetStrokeVisual(id) + AppendInterpolatedTouchPoints
    │
    └─ 双指手势？ ──► inkCanvas.EditingMode = None

移动：InkCanvas_PreviewTouchMove
    │
    ├─ _activeRealtimeTouchStrokeIds ──► TryAppendRealtimeVelocityBrushTipInterpolatedPoints + Redraw
    └─ _activeTouchStrokeIds ──► AppendInterpolatedTouchPoints + Redraw

抬起：InkCanvas_PreviewTouchUp
    │
    ├─ 提交 Stroke 到 inkCanvas.Strokes
    ├─ 触发 inkCanvas_StrokeCollected
    └─ 清理 VisualCanvas、字典、状态
```

---

## 15. 文件索引

| 文件 | 说明 |
| ------ | ------ |
| [MW_TouchEvents.cs](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_TouchEvents.cs) | 多指/手写笔事件处理、实时笔锋、插值、停顿拉直 |
| [MultiTouchInput.cs](file:///d:/Code/Clones/community/Ink%20Canvas/Helpers/MultiTouchInput.cs) | `VisualCanvas`、`StrokeVisual` 自定义渲染 |
| [MW_Settings.cs](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_Settings.cs) | 多指模式开关事件、与双指手势互斥 |
| [MW_BoardControls.cs](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_BoardControls.cs) | 多指模式按白板页面保存/恢复 |
| [Settings.cs](file:///d:/Code/Clones/community/Ink%20Canvas/Resources/Settings.cs) | `IsEnableMultiTouchMode` / `IsEnableMultiTouchModeBoard` 等设置项 |
| [MW_ShapeDrawing.cs](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_ShapeDrawing.cs) | 几何图形绘制模式与多指模式的交互 |

---

## 16. 结论

Ink Canvas 的多指同时绘图实现采用了“按设备 ID 独立维护 `StrokeVisual` + 自定义 `VisualCanvas` 渲染 + 事件拦截”的方案，突破了 WPF `InkCanvas` 单点触摸的限制。通过实时速度笔锋、触摸插值与一欧元滤波，在普通电容屏上也能获得接近压感笔的书写体验。多指模式与双指手势、手掌擦、图形绘制、白板页面持久化等模块均有明确的互斥/恢复逻辑，保证了功能之间的稳定切换。
