# 面积擦实现

- 参考  `docs/EraserReference.md` 中 InkCanvasForClass-community 的实现

## 具体要求

- 总目标：利用自定义橡皮擦替换 `InkCanvasEditingMode.EraseByPoint`
- 适用范围：触摸（`Touch`）、鼠标（`Mouse`）
- 约定：
    - 触摸周期：从上一个手指（或鼠标）的落下/抬起事件到下一个之间的过程。
- 文件：`Eraser.cs`
    - 据情况适当修改其他文件
    - 纯工具函数放置在静态类
- 代码实现
    - C# (`LangVersion=latest`)
    - 保持代码风格与项目其他部分一致
- 替换为面积擦
    - 颜色形状：白色圆形
    - 大小
        - `state == TouchState.Eraser` 且多指：照当前代码实现
        - 其他情况：50px
        - 在进入新的触摸周期时更新大小
- 激活/隐藏时机
    - 注：隐藏时需同步隐藏白色圆形（避免干扰视线）、失去擦除功能（避免误擦除）
    - 工具栏未选中面积擦
        - 严格遵循现有状态机模型：`state == TouchState.Eraser`
    - 工具栏已选中面积擦
        - 严格遵循现有状态机模型：`state == TouchState.Eraser || state == TouchState.Draw || state == TouchState.EvalDraw || state == TouchState.MultiDraw`
            - `TouchState.EvalDraw` 状态下显示白色圆形但无擦除功能
- 撤销、重做功能
    - 将每个触摸周期擦除的部分作为整体插入历史记录栈
    - 以该整体为单位进行撤销/重做
- 不要的功能
    - 冻结功能
    - 手掌擦功能
