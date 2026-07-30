# 轻白板 / LightBoard

一个基于 WPF `InkCanvas` 的轻白板。

- 经过实地课堂检验优化的功能设计
- 超快启动速度、极低的书写/拖动/缩放延迟

## 功能

- 快速更换笔触颜色与粗细
- 荧光笔模式
- 线擦 / 面积擦 / 选择
- 撤销 / 重做
- 多页面管理，支持页面切换、缩略图预览
- 复制 / 粘贴 / 克隆 / 删除选中墨迹
- 工具栏可一键收起/展开
- 打开 / 保存 `*.isf` Windows 墨迹文件
- 导出画布为 `*.png`
- 自动备份当前墨迹（每分钟保存到 `recover/` 目录，崩溃时额外备份到程序目录）
- 支持单实例运行
- 多人同时书写（大屏两侧各人独立绘制，互不干扰）
- 标题栏实时时间显示

## 触摸手势

| 手指数量        | 状态        | 说明                               |
| --------------- | ----------- | ---------------------------------- |
| 1               | 绘图        | 单指轻触即可书写或绘制             |
| 2（近距离）     | 平移 + 缩放 | 双指移动平移，张合缩放             |
| 3 ~ 4（近距离） | 平移        | 多指拖动平移画布                   |
| ≥ 5（近距离）   | 橡皮擦      | 多指用作大面积橡皮擦               |
| ≥ 2（远距离）   | 多人绘制    | 多人在大屏两侧同时书写，每人独立笔迹 |

> 近距离/远距离由窗口宽度的 60% 作为阈值判断。
>
> 进入多人绘制状态时会为当前所有触摸点各自启动一条独立笔迹，新加入的触摸点也会被显式捕获并开启新笔迹，避免落笔丢失。

## 下载

前往 [Releases](https://github.com/KaiHuaDou/DrawingNotepad/releases/latest) 下载最新版本。

前往 [Actions](https://github.com/KaiHuaDou/DrawingNotepad/action) 下载构建版本。

## 系统要求

- Windows 7 SP1 或更新版本
    - Windows 7 RTM 理论支持，但未经测试
- .NET 6 桌面运行时或更新版本
    - 建议使用最新的 .NET 桌面运行时以获得免费的性能提升
    - 对于 Windows 7 SP1，在安装特定补丁后，有可能能正常安装并使用最新的 .NET 桌面运行时
- [Segoe Fluent Icons 字体](https://learn.microsoft.com/zh-cn/windows/apps/design/style/segoe-fluent-icons-font)

## 开发与构建

- IDE：Visual Studio Community 2022 或更新版本
    - 工作负载：C# 桌面开发
    - 预览功能：使用 .NET SDK 预览版

- .NET **9.0** SDK 或更新版本（需要 C# `preview` 语言版本）
- 为兼容 Windows 7，默认目标框架为 `net6.0-windows`
    - 如需针对其他目标，可自行修改 `.csproj`

```bash
dotnet publish -p:PublishProfile=FolderProfile -f net6.0-windows -c Release
```

输出位于 `LightBoard/bin/publish/`。

## 项目结构

```
LightBoard/               # 主程序
├─ InkCanvasNext/         # WPF InkCanvas 现代封装（可独立复用）
│  ├─ Devices.cs          # 触摸/鼠标设备事件处理与捕获
│  ├─ States.cs           # 触摸状态机
│  ├─ Gestures.cs         # 平移/缩放手势
│  ├─ MultiTouch.cs       # 多人同时绘制与增量渲染
│  ├─ Eraser.cs           # 橡皮擦反馈与命中
│  ├─ Strokes.cs          # 墨迹集合与撤销/重做
│  └─ UndoRedo.cs         # 历史栈管理
├─ Paging.cs              # 多页面管理
├─ MainWindow.xaml(.cs)   # 主窗口与工具栏
└─ App.xaml(.cs)          # 应用入口、单实例与崩溃恢复
docs/                     # 设计文档与参考资料
```

## 路线图

见 [ROADMAP](docs/ROADMAP.md)。

## `InkCanvasNext`

WPF `InkCanvas` 现代封装

复制 [`InkCanvas` 文件夹](./LightBoard/InkCanvasNext) 到你的项目里面即可使用。

### 公开类型

- `InkCanvasNext`: 主控件
- `InkCanvasNextMode`: 编辑模式

```csharp
public enum InkCanvasNextMode
{
    Ink,         // 墨迹书写
    EraseStroke, // 线擦
    EraseArea,   // 面积擦
    Select       // 选择
}
```

### `InkCanvasNext`

#### 依赖属性

| 属性                       | 类型                | 说明                    |
| -------------------------- | ------------------- | ----------------------- |
| `CanRedo`                  | `bool`              | 只读，是否可以重做      |
| `CanUndo`                  | `bool`              | 只读，是否可以撤销      |
| `DefaultDrawingAttributes` | `DrawingAttributes` | 默认笔触属性            |
| `Mode`                     | `InkCanvasNextMode` | 当前编辑模式            |
| `EraserDiameter`           | `double`            | 面积擦直径，默认 `50.0` |
| `Strokes`                  | `StrokeCollection`  | 墨迹集合                |

#### 事件

| 事件             | 说明                 |
| ---------------- | -------------------- |
| `CanRedoChanged` | `CanRedo` 变化时触发 |
| `CanUndoChanged` | `CanUndo` 变化时触发 |
| `StrokesChanged` | 墨迹集合变化时触发   |

#### 属性

| 属性              | 类型               | 说明                                 |
| ----------------- | ------------------ | ------------------------------------ |
| `SelectedStrokes` | `StrokeCollection` | 当前选中的墨迹                       |
| `HasSelection`    | `bool`             | 是否有选中的墨迹                     |
| `InnerCanvas`     | `InkCanvas`        | 内部原生 `InkCanvas`，不建议直接使用 |
| `CurrentScale`    | `double`           | 当前画布缩放比例                     |
| `OffsetX`         | `double`           | 画布水平滚动偏移                     |
| `OffsetY`         | `double`           | 画布垂直滚动偏移                     |

#### 方法

| 方法                | 说明                               |
| ------------------- | ---------------------------------- |
| `Undo()`            | 撤销上一步墨迹变更                 |
| `Redo()`            | 重做上一步墨迹变更                 |
| `CopySelected()`    | 复制选中的墨迹到剪贴板             |
| `CutSelected()`     | 剪切选中的墨迹到剪贴板             |
| `Paste()`           | 从剪贴板粘贴墨迹到画布中心         |
| `DeleteSelected()`  | 删除选中的墨迹                     |
| `CloneSelected()`   | 克隆选中的墨迹并偏移显示           |
| `ResetTouchState()` | 重置当前触摸状态并释放所有触摸捕获 |
| `SwapHistory(...)`  | 交换控件当前的撤销/重做历史栈      |

## 许可证

本项目以 [Apache-2.0 License](http://www.apache.org/licenses/) 提供。
