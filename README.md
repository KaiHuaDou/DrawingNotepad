# LightBoard / 轻白板

一个基于 WPF `InkCanvas` 的轻白板。

- 经过实地课堂检验优化的功能设计。
- 超快启动速度、极低的书写/拖动/缩放延迟。

## 功能

- 快速更换笔触颜色与粗细
- 荧光笔模式
- 线擦 / 面积擦 / 选择
- 撤销 / 重做
- 打开 / 保存 `*.isf` Windows 墨迹文件
- 导出画布为 `*.png`
- 崩溃时自动备份当前墨迹

## 触摸手势

| 手指数量 | 状态 | 说明 |
|---------|------|------|
| 1 | 绘图 | 单指轻触即可书写或绘制 |
| 2（近距离）| 平移 + 缩放 | 双指移动平移，张合缩放 |
| 3 ~ 4（近距离）| 平移 | 多指拖动平移画布 |
| ≥ 5（近距离）| 橡皮擦 | 多指用作大面积橡皮擦 |
| ≥ 2（远距离）| 多人绘制 | 多人在大屏两侧同时书写 |

> 近距离/远距离由窗口宽度的 60% 作为阈值判断。

## 系统要求

- Windows 7 SP1 或更新版本
    - Windows 7 RTM 理论支持，但未经测试
- .NET 6 桌面运行时或更新版本
    - 建议使用最新的 .NET 桌面运行时以获得免费的性能提升
    - 对于 Windows 7，在安装特定补丁后，有可能能正常安装并使用最新的 .NET 桌面运行时
- [Segoe Fluent Icons 字体](https://learn.microsoft.com/zh-cn/windows/apps/design/style/segoe-fluent-icons-font)

## 构建

```bash
dotnet publish -p:PublishProfile=FolderProfile -f net6.0-windows -c Release
```

## 文档

- [TouchStates.md](./TouchStates.md) — 触摸状态机说明

## 许可证

本项目以 [Apache-2.0 License](http://www.apache.org/licenses/) 提供
