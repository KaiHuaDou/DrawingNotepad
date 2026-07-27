# InkCanvas 橡皮擦实现详解

本文档基于 `Ink Canvas` 项目代码，梳理 WPF `InkCanvas` 橡皮擦（面积擦 / 笔画擦 / 手掌擦）的完整实现。

---

## 1. 总体架构

项目里没有直接依赖 `InkCanvas` 内置的 `EraseByPoint`/`EraseByStroke` 默认交互，而是在 `InkCanvas` 上方覆盖了一个自定义的 `Canvas` 层：

- **几何橡皮擦（面积擦）**：通过 `EraserOverlayCanvas` 捕获鼠标/手写笔事件，使用 `StrokeCollection.GetIncrementalStrokeHitTester` 做增量碰撞检测，把被擦中的笔画拆成多段或删除。
- **笔画橡皮擦（线擦）**：把 `InkCanvas.EditingMode` 切到 `InkCanvasEditingMode.EraseByStroke`，由 WPF 在笔画级别自动擦除。
- **手掌橡皮擦**：在 `PreviewTouchDown` 中根据触摸接触面大小自动切换到面积擦。

核心文件：

| 文件 | 职责 |
| ------ | ------ |
| [MW_Eraser.cs](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_Eraser.cs) | 橡皮擦覆盖层、增量碰撞检测、尺寸/形状计算 |
| [MW_FloatingBarIcons.cs](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_FloatingBarIcons.cs) | 工具栏按钮点击、模式切换、UI 高亮 |
| [MainWindow.xaml.cs](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow.xaml.cs) | `SetCurrentToolMode`、`SetCursorBasedOnEditingMode`、自动切回批注 |
| [MW_TouchEvents.cs](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_TouchEvents.cs) | 触摸/手掌橡皮擦逻辑 |
| [MW_Timer.cs](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_Timer.cs) | 橡皮擦自动切回批注计时器 |
| [Helpers/TimeMachine.cs](file:///d:/Code/Clones/community/Ink%20Canvas/Helpers/TimeMachine.cs) | 撤销/重做历史 |
| [MW_Eraser.xaml](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_Eraser.xaml) | 橡皮擦视觉图标资源 |

---

## 2. 视觉覆盖层（EraserOverlayCanvas）

### 2.1 XAML 定义

覆盖层位于 `MainWindow.xaml` [L177-L194](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow.xaml#L177-L194)：

```xml
<Canvas x:Name="EraserOverlayCanvas"
        Background="Transparent"
        IsHitTestVisible="False"
        Loaded="EraserOverlayCanvas_Loaded"
        Panel.ZIndex="1000">
    <Image x:Name="EraserFeedback"
           Canvas.Left="0"
           Canvas.Top="0"
           RenderTransformOrigin="0,0"
           Width="0"
           Height="0"
           Visibility="Collapsed">
        <Image.RenderTransform>
            <TranslateTransform x:Name="EraserFeedbackTranslateTransform"/>
        </Image.RenderTransform>
    </Image>
</Canvas>
```

`EraserFeedback` 是一张跟随指针的橡皮擦图片；`EraserOverlayCanvas` 默认 `IsHitTestVisible="False"`，需要面积擦时才启用。

图标资源在 [MW_Eraser.xaml](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_Eraser.xaml) 中定义，包含 `RectangleEraserImageSource`（黑板擦）与 `EllipseEraserImageSource`（圆形擦）。

### 2.2 覆盖层事件绑定

[MW_Eraser.cs L33-L80](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_Eraser.cs#L33-L80)

```csharp
private void EraserOverlayCanvas_Loaded(object sender, RoutedEventArgs e)
{
    var canvas = (System.Windows.Controls.Canvas)sender;
    eraserOverlayCanvas = canvas;

    eraserFeedback = FindName("EraserFeedback") as Image;
    if (eraserFeedback != null)
    {
        eraserFeedbackTranslateTransform = eraserFeedback.RenderTransform as TranslateTransform;
    }

    canvas.StylusDown += ((o, args) =>
    {
        e.Handled = true;
        if (args.StylusDevice.TabletDevice.Type == TabletDeviceType.Stylus) canvas.CaptureStylus();
        EraserOverlay_PointerDown(sender);
    });
    canvas.StylusUp += ((o, args) =>
    {
        e.Handled = true;
        if (args.StylusDevice.TabletDevice.Type == TabletDeviceType.Stylus) canvas.ReleaseStylusCapture();
        EraserOverlay_PointerUp(sender);
    });
    canvas.StylusMove += ((o, args) =>
    {
        e.Handled = true;
        EraserOverlay_PointerMove(sender, args.GetPosition(inkCanvas));
    });
    canvas.MouseDown += ((o, args) =>
    {
        canvas.CaptureMouse();
        EraserOverlay_PointerDown(sender);
    });
    canvas.MouseUp += ((o, args) =>
    {
        canvas.ReleaseMouseCapture();
        EraserOverlay_PointerUp(sender);
    });
    canvas.MouseMove += ((o, args) =>
    {
        EraserOverlay_PointerMove(sender, args.GetPosition(inkCanvas));
    });

    UpdateEraserStyle();
}
```

---

## 3. 模式切换入口

### 3.1 工具栏按钮

浮动工具栏中的两个橡皮按钮：

- **面积擦**：`builtin.eraser` → [EraserToolItem.cs](file:///d:/Code/Clones/community/Ink%20Canvas/Controls/Toolbar/FloatingToolbar/Items/EraserToolItem.cs)
- **线擦**：`builtin.eraserByStrokes` → [EraserByStrokesToolItem.cs](file:///d:/Code/Clones/community/Ink%20Canvas/Controls/Toolbar/FloatingToolbar/Items/EraserByStrokesToolItem.cs)

白板工具栏对应的实现：

- [BoardEraserToolItem.cs](file:///d:/Code/Clones/community/Ink%20Canvas/Controls/Toolbar/BoardToolbar/Items/BoardEraserToolItem.cs)
- [BoardStrokeEraserToolItem.cs](file:///d:/Code/Clones/community/Ink%20Canvas/Controls/Toolbar/BoardToolbar/Items/BoardStrokeEraserToolItem.cs)

### 3.2 面积擦按钮点击

[MW_FloatingBarIcons.cs L3763-L3832](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_FloatingBarIcons.cs#L3763-L3832)

```csharp
internal void EraserIcon_Click(object sender, MouseButtonEventArgs e)
{
    if (TryBlockFrozenPageMutation("切换到橡皮擦")) return;

    bool isAlreadyEraser = inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint;
    forceEraser = false;
    forcePointEraser = true;
    drawingShapeMode = 0;

    if (!IsAnnotating)
    {
        PenIcon_Click(sender, e);
    }

    if (!isAlreadyEraser && currentMode != 0)
    {
        SaveStrokes();
    }

    if (!isAlreadyEraser)
    {
        ResetTouchStates();
    }

    EnableEraserOverlay();
    SetCurrentToolMode(InkCanvasEditingMode.EraseByPoint);
    UpdateCurrentToolMode("eraser");

    ApplyAdvancedEraserShape();
    SetCursorBasedOnEditingMode(inkCanvas);
    HideSubPanels("eraser");

    if (Settings.Canvas.EnableEraserAutoSwitchBack)
    {
        StopEraserAutoSwitchBackTimer();
    }

    if (isAlreadyEraser)
    {
        // 再次点击打开/关闭橡皮擦尺寸面板
        ...
    }
}
```

### 3.3 线擦按钮点击

[MW_FloatingBarIcons.cs L3897-L3930](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_FloatingBarIcons.cs#L3897-L3930)

```csharp
internal void EraserIconByStrokes_Click(object sender, MouseButtonEventArgs e)
{
    if (TryBlockFrozenPageMutation("切换到线擦")) return;

    if (!IsAnnotating)
    {
        PenIcon_Click(sender, e);
    }

    DisableEraserOverlay();

    forceEraser = true;
    forcePointEraser = false;

    inkCanvas.EraserShape = new EllipseStylusShape(5, 5);
    SetCurrentToolMode(InkCanvasEditingMode.EraseByStroke);
    UpdateCurrentToolMode("eraserByStrokes");
    drawingShapeMode = 0;

    inkCanvas_EditingModeChanged(inkCanvas, null);
    CancelSingleFingerDragMode();

    HideSubPanels("eraserByStrokes");
}
```

关键区别：

| | 面积擦 | 线擦 |
| --- | --- | --- |
| `EditingMode` | `EraseByPoint` | `EraseByStroke` |
| 覆盖层 | 启用 `EraserOverlayCanvas` | 禁用覆盖层 |
| 擦除粒度 | 按几何点拆分笔画 | 整根笔画删除 |

### 3.4 集中式模式切换

[MainWindow.xaml.cs L3226-L3270](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow.xaml.cs#L3226-L3270)

```csharp
internal bool SetCurrentToolMode(InkCanvasEditingMode newMode, Action additionalActions = null)
{
    try
    {
        if (IsCurrentPageFrozen && IsFreezeMutatingMode(newMode))
        {
            TryBlockFrozenPageMutation("切换到编辑工具");
            return false;
        }

        if (newMode != InkCanvasEditingMode.EraseByPoint && newMode != InkCanvasEditingMode.EraseByStroke)
        {
            DisableEraserOverlay();
        }

        inkCanvas.EditingMode = newMode;

        bool isMouseMode = newMode == InkCanvasEditingMode.None;

        if (_globalHotkeyManager != null)
        {
            _globalHotkeyManager.UpdateHotkeyStateForToolMode(isMouseMode);
        }

        if (IsInPPTPresentationMode)
        {
            UpdateToolbarComponentVisibility();
        }

        additionalActions?.Invoke();
        return true;
    }
    catch (Exception ex)
    {
        LogHelper.WriteLogToFile($"设置工具模式时出错：{ex.Message}", LogHelper.LogType.Error);
        return false;
    }
}
```

---

## 4. 几何橡皮擦（面积擦）核心逻辑

### 4.1 按下初始化

[MW_Eraser.cs L102-L141](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_Eraser.cs#L102-L141)

```csharp
private void EraserOverlay_PointerDown(object sender)
{
    if (TryBlockFrozenPageMutation("擦除冻结页面")) return;
    if (isUsingGeometryEraser) return;

    isUsingGeometryEraser = true;

    var _h = eraserWidth * 56 / 38;

    StylusShape eraserShape;
    if (isEraserCircleShape)
    {
        eraserShape = new EllipseStylusShape(eraserWidth, eraserWidth);
    }
    else
    {
        eraserShape = new RectangleStylusShape(eraserWidth, _h);
    }

    hitTester = inkCanvas.Strokes.GetIncrementalStrokeHitTester(eraserShape);
    hitTester.StrokeHit += EraserGeometry_StrokeHit;

    var scaleX = eraserWidth / 38;
    var scaleY = _h / 56;
    scaleMatrix = new Matrix();
    scaleMatrix.ScaleAt(scaleX, scaleY, 0, 0);

    if (eraserFeedback != null)
    {
        eraserFeedback.Width = Math.Max(eraserWidth, 10);
        eraserFeedback.Height = isEraserCircleShape ? eraserFeedback.Width : _h;
        eraserFeedback.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
        eraserFeedback.Visibility = Visibility.Collapsed;
    }
}
```

### 4.2 移动碰撞检测

[MW_Eraser.cs L189-L224](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_Eraser.cs#L189-L224)

```csharp
private void EraserOverlay_PointerMove(object sender, Point pt)
{
    if (TryBlockFrozenPageMutation("擦除冻结页面")) return;
    if (!isUsingGeometryEraser) return;

    if (isUsingStrokesEraser)
    {
        var _filtered = inkCanvas.Strokes.HitTest(pt)
            .Where(stroke => !stroke.ContainsPropertyData(FrozenStrokePropertyGuid));
        var filtered = _filtered as Stroke[] ?? _filtered.ToArray();
        if (!filtered.Any()) return;
        inkCanvas.Strokes.Remove(new StrokeCollection(filtered));
    }
    else
    {
        if (eraserFeedback != null && eraserFeedback.Visibility == Visibility.Collapsed)
        {
            eraserFeedback.Visibility = Visibility.Visible;
        }

        if (eraserFeedbackTranslateTransform != null)
        {
            eraserFeedbackTranslateTransform.X = pt.X - eraserFeedback.ActualWidth / 2;
            eraserFeedbackTranslateTransform.Y = pt.Y - eraserFeedback.ActualHeight / 2;
        }

        if (hitTester != null)
        {
            hitTester.AddPoint(pt);
        }
    }
}
```

### 4.3 笔画命中回调

[MW_Eraser.cs L229-L251](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_Eraser.cs#L229-L251)

```csharp
private void EraserGeometry_StrokeHit(object sender, StrokeHitEventArgs args)
{
    StrokeCollection eraseResult = args.GetPointEraseResults();
    StrokeCollection strokesToReplace = new StrokeCollection { args.HitStroke };

    var filtered_2replace = strokesToReplace
        .Where(stroke => !stroke.ContainsPropertyData(FrozenStrokePropertyGuid));
    var filtered2Replace = filtered_2replace as Stroke[] ?? filtered_2replace.ToArray();
    if (!filtered2Replace.Any()) return;

    var filtered_result = eraseResult
        .Where(stroke => !stroke.ContainsPropertyData(FrozenStrokePropertyGuid));
    var filteredResult = filtered_result as Stroke[] ?? filtered_result.ToArray();

    if (filteredResult.Any())
    {
        inkCanvas.Strokes.Replace(new StrokeCollection(filtered2Replace), new StrokeCollection(filteredResult));
    }
    else
    {
        inkCanvas.Strokes.Remove(new StrokeCollection(filtered2Replace));
    }
}
```

`GetPointEraseResults` 会根据橡皮擦形状把被命中的笔画拆成两段或多段，从而只删除局部。

### 4.4 抬起收尾

[MW_Eraser.cs L146-L174](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_Eraser.cs#L146-L174)

```csharp
private void EraserOverlay_PointerUp(object sender)
{
    if (!isUsingGeometryEraser) return;

    isUsingGeometryEraser = false;
    ((UIElement)sender).ReleaseMouseCapture();

    if (eraserFeedback != null)
    {
        eraserFeedback.Visibility = Visibility.Collapsed;
    }

    if (hitTester != null)
    {
        hitTester.EndHitTesting();
        hitTester = null;
    }

    CommitPendingGeometryEraseHistory();
    HandleEraserOperationEnded();
}
```

---

## 5. 覆盖层启用/禁用

[MW_Eraser.cs L256-L294](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_Eraser.cs#L256-L294)

```csharp
public void EnableEraserOverlay()
{
    if (eraserOverlayCanvas != null)
    {
        eraserOverlayCanvas.IsHitTestVisible = true;
        eraserOverlayCanvas.Visibility = Visibility.Visible;
    }
}

public void DisableEraserOverlay()
{
    if (eraserOverlayCanvas != null)
    {
        eraserOverlayCanvas.IsHitTestVisible = false;
        eraserOverlayCanvas.Visibility = Visibility.Collapsed;
    }

    if (isUsingGeometryEraser)
    {
        isUsingGeometryEraser = false;
        if (hitTester != null)
        {
            hitTester.EndHitTesting();
            hitTester = null;
        }
    }

    if (eraserFeedback != null)
    {
        eraserFeedback.Visibility = Visibility.Collapsed;
    }

    CommitPendingGeometryEraseHistory();
}
```

---

## 6. 橡皮擦尺寸与形状

### 6.1 设置项

[Resources/Settings.cs L263-L268](file:///d:/Code/Clones/community/Ink%20Canvas/Resources/Settings.cs#L263-L268)

```csharp
[JsonProperty("eraserSize")]
public int EraserSize { get; set; } = 2;

[JsonProperty("eraserType")]
public int EraserType { get; set; } // 0 - 图标切换模式  1 - 面积擦  2 - 线条擦

[JsonProperty("eraserShapeType")]
public int EraserShapeType { get; set; } // 0 - 圆形擦  1 - 黑板擦
```

### 6.2 尺寸计算

[MW_Eraser.cs L299-L327](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_Eraser.cs#L299-L327)

```csharp
public void UpdateEraserSize()
{
    double k = 1.0;

    switch (Settings.Canvas.EraserSize)
    {
        case 0: k = Settings.Canvas.EraserShapeType == 0 ? 0.5 : 0.7; break;
        case 1: k = Settings.Canvas.EraserShapeType == 0 ? 0.8 : 0.9; break;
        case 2: k = 1.0; break;
        case 3: k = Settings.Canvas.EraserShapeType == 0 ? 1.25 : 1.2; break;
        case 4: k = Settings.Canvas.EraserShapeType == 0 ? 1.5 : 1.3; break;
    }

    isEraserCircleShape = (Settings.Canvas.EraserShapeType == 0);

    if (isEraserCircleShape)
    {
        eraserWidth = k * 90;
    }
    else
    {
        eraserWidth = k * 90 * 0.6;
    }

    UpdateEraserStyle();
}
```

### 6.3 应用到 InkCanvas

[MW_Eraser.cs L350-L378](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_Eraser.cs#L350-L378)

```csharp
public void ApplyAdvancedEraserShape()
{
    try
    {
        UpdateEraserSize();

        StylusShape eraserShape;
        if (isEraserCircleShape)
        {
            eraserShape = new EllipseStylusShape(eraserWidth, eraserWidth);
        }
        else
        {
            var height = eraserWidth * 56 / 38;
            eraserShape = new RectangleStylusShape(eraserWidth, height);
        }

        inkCanvas.EraserShape = eraserShape;
    }
    catch (Exception ex)
    {
        Trace.WriteLine($"Eraser: Error applying shape - {ex.Message}");
    }
}
```

---

## 7. 手掌橡皮擦

### 7.1 触发条件

[MW_TouchEvents.cs L1663-L1714](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_TouchEvents.cs#L1663-L1714)

```csharp
if (Settings.Canvas.EnablePalmEraser && !isPalmEraserActive && drawingShapeMode == 0)
{
    var touchPoint = e.GetTouchPoint(inkCanvas);
    double boundWidth = GetTouchBoundWidth(e);

    if ((Settings.Advanced.TouchMultiplier != 0 || !Settings.Advanced.IsSpecialScreen)
        && (boundWidth > BoundsWidth))
    {
        double thresholdMultiplier;
        switch (Settings.Canvas.PalmEraserSensitivity)
        {
            case 0: thresholdMultiplier = 3.0; break;
            case 1: thresholdMultiplier = 2.5; break;
            case 2:
            default: thresholdMultiplier = 2.0; break;
        }

        double EraserThresholdValue = Settings.Startup.IsEnableNibMode
            ? Settings.Advanced.NibModeBoundsWidthThresholdValue
            : Settings.Advanced.FingerModeBoundsWidthThresholdValue;

        if (boundWidth > BoundsWidth * EraserThresholdValue * thresholdMultiplier)
        {
            boundWidth *= Settings.Startup.IsEnableNibMode
                ? Settings.Advanced.NibModeBoundsWidthEraserSize
                : Settings.Advanced.FingerModeBoundsWidthEraserSize;

            if (Settings.Advanced.IsSpecialScreen)
                boundWidth *= Settings.Advanced.TouchMultiplier;

            palmEraserPreviousEditingMode = inkCanvas.EditingMode;
            inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
            isPalmEraserActive = true;

            EnableEraserOverlay();
            eraserWidth = boundWidth;
            UpdateEraserStyle();
            touchPoint = e.GetTouchPoint(inkCanvas);
            EraserOverlay_PointerDown(sender);
            EraserOverlay_PointerMove(sender, touchPoint.Position);
        }
    }
}
```

### 7.2 抬起恢复

[MW_TouchEvents.cs L1965-L1974](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_TouchEvents.cs#L1965-L1974)

```csharp
if (isPalmEraserActive)
{
    isPalmEraserActive = false;
    DisableEraserOverlay();
    if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint)
    {
        inkCanvas.EditingMode = palmEraserPreviousEditingMode;
        SetCursorBasedOnEditingMode(inkCanvas);
    }
}
```

---

## 8. 撤销 / 重做历史

面积擦通过 `ReplacedStroke` / `AddedStroke` 记录被替换和新增的笔画，抬起时一次性提交给 `TimeMachine`。

[MW_Eraser.cs L176-L184](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_Eraser.cs#L176-L184)

```csharp
private void CommitPendingGeometryEraseHistory()
{
    if (ReplacedStroke == null && AddedStroke == null) return;

    timeMachine.CommitStrokeEraseHistory(ReplacedStroke, AddedStroke);
    MarkCurrentPageInkChanged();
    AddedStroke = null;
    ReplacedStroke = null;
}
```

`TimeMachine.CommitStrokeEraseHistory` 把本次操作记录为 `Clear` 类型：

[Helpers/TimeMachine.cs L93-L102](file:///d:/Code/Clones/community/Ink%20Canvas/Helpers/TimeMachine.cs#L93-L102)

```csharp
public void CommitStrokeEraseHistory(StrokeCollection stroke, StrokeCollection sourceStroke = null)
{
    if (_currentIndex + 1 < _currentStrokeHistory.Count)
    {
        _currentStrokeHistory.RemoveRange(_currentIndex + 1, (_currentStrokeHistory.Count - 1) - _currentIndex);
    }
    _currentStrokeHistory.Add(new TimeMachineHistory(stroke, TimeMachineHistoryType.Clear, true, sourceStroke));
    _currentIndex = _currentStrokeHistory.Count - 1;
    NotifyUndoRedoState();
}
```

`ApplyHistoryToCanvas` 中，当 `CommitType == Clear` 时，根据 `StrokeHasBeenCleared` 布尔值决定是恢复原始笔画还是重新执行擦除：

[MW_TimeMachine.cs L215-L240](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_TimeMachine.cs#L215-L240)

```csharp
else if (item.CommitType == TimeMachineHistoryType.Clear)
{
    if (!item.StrokeHasBeenCleared)
    {
        if (item.CurrentStroke != null)
            foreach (var currentStroke in item.CurrentStroke)
                if (!canvas.Strokes.Contains(currentStroke))
                    canvas.Strokes.Add(currentStroke);

        if (item.ReplacedStroke != null)
            foreach (var replacedStroke in item.ReplacedStroke)
                if (canvas.Strokes.Contains(replacedStroke))
                    canvas.Strokes.Remove(replacedStroke);
    }
    else
    {
        if (item.ReplacedStroke != null)
            foreach (var replacedStroke in item.ReplacedStroke)
                if (!canvas.Strokes.Contains(replacedStroke))
                    canvas.Strokes.Add(replacedStroke);

        if (item.CurrentStroke != null)
            foreach (var currentStroke in item.CurrentStroke)
                if (canvas.Strokes.Contains(currentStroke))
                    canvas.Strokes.Remove(currentStroke);
    }
}
```

---

## 9. 光标处理

面积擦模式下隐藏系统光标，改为显示自定义 `EraserFeedback`；线擦使用默认光标。

[MainWindow.xaml.cs L2192-L2210](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow.xaml.cs#L2192-L2210)

```csharp
public void SetCursorBasedOnEditingMode(InkCanvas canvas)
{
    if (canvas.EditingMode == InkCanvasEditingMode.Select)
    {
        canvas.UseCustomCursor = true;
        canvas.ForceCursor = true;
        canvas.Cursor = Cursors.Cross;
        System.Windows.Forms.Cursor.Show();
        return;
    }

    if (canvas.EditingMode == InkCanvasEditingMode.EraseByPoint)
    {
        canvas.UseCustomCursor = true;
        canvas.ForceCursor = true;
        canvas.Cursor = Cursors.None;
        return;
    }

    // 其他模式按用户设置处理 ...
}
```

---

## 10. 自动切回批注

设置项：

```csharp
[JsonProperty("enableEraserAutoSwitchBack")]
public bool EnableEraserAutoSwitchBack { get; set; } = false;

[JsonProperty("eraserAutoSwitchBackDelaySeconds")]
public int EraserAutoSwitchBackDelaySeconds { get; set; } = 10;
```

[MainWindow.xaml.cs L2402-L2419](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow.xaml.cs#L2402-L2419)

```csharp
private void HandleEraserOperationEnded()
{
    try
    {
        if ((inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint ||
             inkCanvas.EditingMode == InkCanvasEditingMode.EraseByStroke) &&
            Settings.Canvas.EnableEraserAutoSwitchBack)
        {
            StartEraserAutoSwitchBackTimer();
        }
    }
    catch (Exception ex)
    {
        LogHelper.WriteLogToFile($"处理橡皮擦操作结束事件失败：{ex.Message}", LogHelper.LogType.Error);
    }
}
```

计时器到时间后调用 `PenIcon_Click` 回到画笔模式：

[MW_Timer.cs L1748-L1782](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_Timer.cs#L1748-L1782)

```csharp
private void EraserAutoSwitchBackTimer_Tick(object sender, EventArgs e)
{
    if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
    try
    {
        if (inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint &&
            inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke)
        {
            StopEraserAutoSwitchBackTimer();
            return;
        }

        if (!Settings.Canvas.EnableEraserAutoSwitchBack)
        {
            StopEraserAutoSwitchBackTimer();
            return;
        }

        PenIcon_Click(null, null);
        StopEraserAutoSwitchBackTimer();
    }
    catch (Exception ex)
    {
        LogHelper.WriteLogToFile($"橡皮擦自动切换计时器事件处理失败：{ex.Message}", LogHelper.LogType.Error);
    }
}
```

---

## 11. 冻结笔画保护

冻结功能通过给 Stroke 附加一个自定义 GUID 属性来标记不可变笔画：

[MW_InkFreeze.cs L18](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_InkFreeze.cs#L18)

```csharp
internal static readonly Guid FrozenStrokePropertyGuid = new Guid("12345678-1234-1234-1234-123456789ABC");
```

橡皮擦在命中检测和笔画橡皮擦中都会过滤掉这些笔画：

- [MW_Eraser.cs L197](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_Eraser.cs#L197)
- [MW_Eraser.cs L235](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_Eraser.cs#L235)
- [MW_Eraser.cs L239](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_Eraser.cs#L239)

同时在切到橡皮擦前会检查 `TryBlockFrozenPageMutation`，避免误改冻结页面。

---

## 12. UI 弹出面板

橡皮擦设置面板由 [EraserPopupContent.xaml](file:///d:/Code/Clones/community/Ink%20Canvas/Controls/Popups/EraserPopupContent.xaml) 提供，包含：

- 尺寸下拉框（5 档）
- 形状 Tab（圆形 / 黑板擦）
- 清墨、清墨 + 清空历史按钮

事件在 `MainWindow.xaml.cs` [L333-L351](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow.xaml.cs#L333-L351) 中统一绑定：

```csharp
private void WireUpSingleEraserPopupContentEvents(EraserPopupContent content)
{
    if (content == null) return;

    content.EraserSizeComboBox.SelectionChanged += ComboBoxEraserSizeFloatingBar_SelectionChanged;
    content.EraserTypeTab.SelectionChanged += EraserTypeTab_SelectionChanged;
    content.ClearInkBtn.Click += EraserPanelSymbolIconDelete_MouseUp;
    content.ClearInkAndHistoryBtn.Click += BoardSymbolIconDeleteInkAndHistories_MouseUp;
    content.CloseButtonControl.Click += CloseBordertools_Click;
}
```

尺寸变化处理：

[MainWindow.xaml.cs L2927-L2951](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow.xaml.cs#L2927-L2951)

```csharp
private void ComboBoxEraserSizeFloatingBar_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    try
    {
        if (!isLoaded) return;
        var comboBox = sender as System.Windows.Controls.ComboBox;
        if (comboBox == null) return;

        Settings.Canvas.EraserSize = comboBox.SelectedIndex;
        SaveSettingsToFile();

        if (comboBox.Name == "ComboBoxEraserSizeFloatingBar" && BoardComboBoxEraserSize != null)
        {
            BoardComboBoxEraserSize.SelectedIndex = comboBox.SelectedIndex;
        }
        else if (comboBox.Name == "BoardComboBoxEraserSize" && ComboBoxEraserSizeFloatingBar != null)
        {
            ComboBoxEraserSizeFloatingBar.SelectedIndex = comboBox.SelectedIndex;
        }
    }
    catch (Exception ex)
    {
        LogHelper.WriteLogToFile($"切换橡皮擦大小时出错：{ex.Message}", LogHelper.LogType.Error);
    }
}
```

形状 Tab 变化时除了保存设置，还会立即重设 `EditingMode` 以应用新的 `EraserShape`：

[MW_Settings.cs L701-L713](file:///d:/Code/Clones/community/Ink%20Canvas/MainWindow_cs/MW_Settings.cs#L701-L713)

```csharp
private void EraserTypeTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (!isLoaded) return;
    if (sender is TabControl tabControl)
    {
        Settings.Canvas.EraserShapeType = tabControl.SelectedIndex;
        SaveSettingsToFile();
        CheckEraserTypeTab();
        ApplyAdvancedEraserShape();
        inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
        inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
    }
}
```

---

## 13. 关键状态变量汇总

| 变量 | 文件 | 含义 |
| --- | --- | --- |
| `isUsingGeometryEraser` | MW_Eraser.cs | 是否正在使用几何橡皮擦 |
| `isUsingStrokesEraser` | MW_Eraser.cs | 是否使用笔画橡皮擦（HitTest 模式） |
| `isEraserCircleShape` | MW_Eraser.cs | 当前是否为圆形橡皮擦 |
| `eraserWidth` | MW_Eraser.cs | 当前橡皮擦宽度 |
| `hitTester` | MW_Eraser.cs | 增量笔画碰撞检测器 |
| `isPalmEraserActive` | MW_TouchEvents.cs | 手掌橡皮擦是否激活 |
| `palmEraserPreviousEditingMode` | MW_TouchEvents.cs | 手掌橡皮擦前的前一个模式 |
| `forcePointEraser` / `forceEraser` | MW_FloatingBarIcons.cs | 标记当前强制为点擦/线擦 |

---

## 14. 流程总结

1. 用户点击面积擦按钮 → `EraserIcon_Click`
2. 启用 `EraserOverlayCanvas`、设置 `EditingMode = EraseByPoint`
3. 用户按下时创建 `IncrementalStrokeHitTester` 并订阅 `StrokeHit`
4. 移动时不断调用 `hitTester.AddPoint(pt)`，命中笔画后拆分/删除
5. 抬起时结束碰撞检测、提交历史、optionally 启动自动切回批注计时器
6. 线擦则直接切到 `EraseByStroke`，由 WPF 完成整笔画擦除
