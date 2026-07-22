# Touch State Machine

- `and`: all conditions should be true.
- `d`: the current max distance of the fingers.
- `l`: distance threshold, set to `0.6 * ActualWidth`.
- `c`: displacement threshold, set to 15px.
- `count`: the current finger count.
- `x`: the displacement of the first finger.

- Assumes
    - if something changed, but not match any condition of current state, stay.
    - The transition only happends when `count` changes.
    - `d > l` never happend unless a new finger down.
        - Because:
          - This state machines is designed for a enough huge screen.
          - `d > l` goes to `MultiDraw`. `MultiDraw` is designed to let two people put down finger on the two side.

- Requires
    - When `EvalDraw --> Draw`, keep the drawed stroke.
    - When `Idle --> *`, save the editing mode.
    - When `* --> Idle`, restore the editing mode.

- Coding
    - Notice we're using C# latest.
    - Use `var` when the type is obvious.
    - Follow the code style of other part.
    - Avoid allocating big heap item (like `List<T>`).
    - When not sure, ask me.
    - When compressing the context, keep this document.
    - `MultiDraw` now unimpl, use `InkCanvasEditingMode.Ink` as current impl.

```mermaid
stateDiagram-v2
    [*] --> Idle

    Idle --> EvalDraw: count == 1
    Idle --> PanZoom: count == 2 and d <= l
    Idle --> Pan: (count == 3 or count == 4) and d <= l
    Idle --> Eraser: count >= 5 and d <= l
    Idle --> MultiDraw: count >= 2 and d > l

    EvalDraw --> Idle: count == 0
    EvalDraw --> Draw: x > c (Exception: We should track x in order to activate this transition)
    EvalDraw --> PanZoom: count == 2 and d <= l
    EvalDraw --> Pan: (count == 3 or count == 4) and d <= l
    EvalDraw --> Eraser: count >= 5 and d <= l
    EvalDraw --> MultiDraw: count >= 2 and d > l

    Draw --> Idle: count == 0
    Draw --> MultiDraw: count >= 2 and d > l

    MultiDraw --> Idle: count == 0
    MultiDraw --> Draw: count == 1

    PanZoom --> Idle: count == 0
    PanZoom --> Draw: count == 1
    PanZoom --> Pan: (count == 3 or count == 4) and d <= l
    PanZoom --> Eraser: count >= 5 and d <= l
    PanZoom --> MultiDraw: count > 2 and d > l

    Pan --> Idle: count == 0
    Pan --> Draw: count == 1
    Pan --> Eraser: count >= 5 and d <= l
    Pan --> MultiDraw: count > 3 and d > l

    Eraser --> Idle: count == 0
    Eraser --> EvalDraw: count == 1
    Eraser --> MultiDraw: count > 5 and d > l
```
