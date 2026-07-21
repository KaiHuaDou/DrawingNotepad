# Touch State Machine

- `and`: all conditions should be true.
- `d`: the current max distance of the fingers.
- `l`: distance threshold, set to `0.5 * ActualWidth`.
- `c`: displacement threshold, set to 10px.
- `cnt`: the current finger count.
- `x`: the displacement of the first finger.

- Assumes
    - if something changed, but not match any condition of current state, stay.
    - The transition only happends when `cnt` changes.
    - `d > l` never happend unless a new finger down.
        - Because: `d > l` goes to `MultiDrawing`, `MultiDrawing` is designed to let two people put down finger on the two side (far away in distance) of a huge screen. In real situation, `l` is set to the value that a normal human can hardly let `d` reaching.

- Requires
    - When `EvaluateDraw --> Drawing`, keep the drawed stroke.
    - When `Idle --> *`, save the editing mode.
    - When `* --> Idle`, restore the editing mode.

- Coding
    - Notice we're using C# latest.
    - Use `var` when the type is obvious.
    - Follow the code style of other part.
    - When writing math calculate function, use `LINQ` in order to improve performance.
    - Avoid allocate big heap item (like `List<T>`), use alternative data structure or methods.
    - When not sure, ask me.
    - When compressing the context, keep this document.
    - `MultiDrawing` now unimpl, use `InkCanvasEditingMode.Ink` as current impl.

```mermaid
stateDiagram-v2
    [*] --> Idle

    Idle --> EvaluateDraw: cnt == 1
    Idle --> PanZoom: cnt == 2 and d <= l
    Idle --> Pan: cnt == 3 and d <= l
    Idle --> Eraser: cnt >= 4 and d <= l
    Idle --> MultiDrawing: cnt >= 2 and d > l

    EvaluateDraw --> Idle: cnt == 0
    EvaluateDraw --> Drawing: cnt == 1 and x > c
    EvaluateDraw --> PanZoom: cnt == 2 and x <= c and d <= l
    EvaluateDraw --> Pan: cnt == 3 and d <= l
    EvaluateDraw --> Eraser: cnt >= 4 and d <= l
    EvaluateDraw --> MultiDrawing: cnt >= 2 and d > l

    Drawing --> Idle: cnt == 0
    Drawing --> PanZoom: cnt == 2 and d <= l
    Drawing --> Pan: cnt == 3 and d <= l
    Drawing --> Eraser: cnt >= 4 and d <= l
    Drawing --> MultiDrawing: cnt >= 2 and d > l

    MultiDrawing --> Idle: cnt == 0
    MultiDrawing --> Drawing: cnt == 1

    PanZoom --> Idle: cnt == 0
    PanZoom --> Drawing: cnt == 1
    PanZoom --> Pan: cnt == 3 and d <= l
    PanZoom --> Eraser: cnt >= 4 and d <= l
    PanZoom --> MultiDrawing: cnt > 2 and d > l

    Pan --> Idle: cnt == 0
    Pan --> Drawing: cnt == 1
    Pan --> PanZoom: cnt == 2
    Pan --> Eraser: cnt >= 4 and d <= l
    Pan --> MultiDrawing: cnt > 3 and d > l

    Eraser --> Idle: cnt == 0
    Eraser --> EvaluateDraw: cnt == 1
    Eraser --> MultiDrawing: cnt > 4 and d > l
```
