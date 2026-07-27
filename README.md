# 轻白板 / LightBoard

一个基于 WPF `InkCanvas` 的轻白板。

- 经过实地课堂检验优化的功能设计
- 超快启动速度、极低的书写/拖动/缩放延迟

## 功能

- 快速更换笔触颜色与粗细
- 荧光笔模式
- 线擦 / 面积擦 / 选择
- 撤销 / 重做
- 打开 / 保存 `*.isf` Windows 墨迹文件
- 导出画布为 `*.png`
- 崩溃时自动备份当前墨迹

## 触摸手势

| 手指数量        | 状态        | 说明                   |
| --------------- | ----------- | ---------------------- |
| 1               | 绘图        | 单指轻触即可书写或绘制 |
| 2（近距离）     | 平移 + 缩放 | 双指移动平移，张合缩放 |
| 3 ~ 4（近距离） | 平移        | 多指拖动平移画布       |
| ≥ 5（近距离）   | 橡皮擦      | 多指用作大面积橡皮擦   |
| ≥ 2（远距离）   | 多人绘制    | 多人在大屏两侧同时书写 |

> 近距离/远距离由窗口宽度的 60% 作为阈值判断。

## 下载

前往 [Releases](./releases/latest) 下载最新版本。

## 系统要求

- Windows 7 SP1 或更新版本
    - Windows 7 RTM 理论支持，但未经测试
- .NET 6 桌面运行时或更新版本
    - 建议使用最新的 .NET 桌面运行时以获得免费的性能提升
    - 对于 Windows 7，在安装特定补丁后，有可能能正常安装并使用最新的 .NET 桌面运行时
- [Segoe Fluent Icons 字体](https://learn.microsoft.com/zh-cn/windows/apps/design/style/segoe-fluent-icons-font)

## 构建

- .NET **9.0** SDK 或更新版本（用于支持 C# `latest` 语言版本）
- 为兼容 Windows 7，默认目标框架为 `net6.0-windows`
    - 如需针对其他目标，可自行修改 `.csproj`

```bash
dotnet publish -p:PublishProfile=FolderProfile -f net6.0-windows -c Release
```

输出位于 `LightBoard/bin/publish/`。

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

### `InkCanvasNext` 公开成员

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

#### 方法

| 方法                | 说明                               |
| ------------------- | ---------------------------------- |
| `Undo()`            | 撤销上一步墨迹变更                 |
| `Redo()`            | 重做上一步墨迹变更                 |
| `ResetTouchState()` | 重置当前触摸状态并释放所有触摸捕获 |

## 许可证

本项目以 [Apache-2.0 License](http://www.apache.org/licenses/) 提供
