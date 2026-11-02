# 轻白板 / LightBoard

[![Release](https://img.shields.io/github/v/release/KaiHuaDou/DrawingNotepad)](https://github.com/KaiHuaDou/DrawingNotepad/releases/latest)
[![Build](https://img.shields.io/github/actions/workflow/status/KaiHuaDou/DrawingNotepad/build.yml?branch=master&label=Build)](https://github.com/KaiHuaDou/DrawingNotepad/actions/workflows/build.yml)
[![License](https://img.shields.io/github/license/KaiHuaDou/DrawingNotepad)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Platform](https://img.shields.io/badge/Windows-7%20SP1%2B-0078D6)](#系统要求)

基于 .NET 10 和 WPF 的全屏白板，用于课堂书写与课件批注。

- 即开即用
- 设计经过实地课堂检验，按课堂反馈持续改进
- 可替代希沃白板 / 希沃轻白板

## 课堂检验

轻白板在真实课堂中日常使用，功能与交互按课堂反馈持续调整。

- 书写与手势面向触控屏教学场景：掌擦防误触、多指同时书写、边缘滚动提示
- 每分钟自动备份、快速保存与单实例运行防止课堂中意外退出丢失内容

## 性能

- 测试机：希沃大屏，Windows 10，6 代 i7
    - 启动时间 < 0.5s
    - 缩放、拖动轻松吃满屏幕帧率 30 Hz
    - 绘制链路延迟 < 30ms
    - 擦除链路延迟 < 50ms

## 功能

### 书写

- 画笔 8 种颜色、5 档粗细，另有黄色荧光笔、直线与圆
- 橡皮按面积擦除
- 选择后可移动、缩放、旋转、克隆或粘贴；空白处拖动为套索圈选
- 撤销与重做，默认 200 步，可在设置中调整
- 多人同时书写：触点间距超过阈值的多指各自成笔
- 画布范围大于屏幕，平移到右 / 下边缘时自动扩展
- 边缘热区内有墨迹时，该侧出现箭头按钮，点击平移画布直到热区无墨迹

### 页面与文档

- 多页白板：上一页 / 下一页，末页再点新建；点击页码展开页面列表与缩略图
- 每页独立保留缩放、偏移与撤销历史
- 页面背景支持 PowerPoint、Word、PDF、XPS 与图片
- 背景按来源记录，打开文件时重新加载；源文件缺失时保留引用
- 可将文档、图片、`.lbf` 的页面或 `.isf` 墨迹附加到当前板

### 文件

- `.lbf`：保存墨迹与每页的背景引用
- `.isf`：Windows 墨迹文件，可打开或附加
- 每分钟自动备份一次，异常退出后可从备份恢复
- 关闭时若有未保存墨迹，可选择快速保存或另选路径
- 单实例运行，重复启动时交由已运行的窗口处理

### 导出

- PNG：全部页面按页码命名导出，可选 25%、50%、100% 缩放
- PDF：整板导出为单个文件，缩放选项相同
- 导出过程显示进度与当前页码

### 视频展台

- 背景切换选「视频」：摄像头画面成为当前页的背景，直接在视频上书写批注，全部画布工具可用
- 画面按设备原生档协商：分辨率优先，同一分辨率取最高帧率；展台工具栏显示协商到的分辨率与帧率
- 视频背景同一时刻只有一页；「拍照」把当前帧冻结为该页的静态背景并新增一页接续视频
- 选其他背景或按展台工具栏的关闭按钮退出；旋转、镜像、冻结画面，多台设备按键循环切换
- 视频背景不写入 `.lbf`（拍照生成的静态背景与其他图片背景一致，随文件保存）
- 构建加 `-p:WithOpenCv=false` 时不含此功能

### 界面

- 左工具栏：关闭、最小化、文件菜单、背景、切换窗口
- 主工具栏：颜色、荧光笔、橡皮、清屏、撤销、重做；展开后为粗细、直线、圆、选择、粘贴
- 右工具栏：收起工具栏、菜单、上一页、页码、下一页
- 背景：默认网格、纯白、纯黑、透明与视频展台；透明时窗口置顶
- 右上角显示时间；菜单包含关于、设置、帮助、计时与调试

### 手势

| 触点数 | 作用 |
| --- | --- |
| 单指 | 书写；选择工具下移动、缩放、旋转选区 |
| 双指 | 平移与缩放 |
| 三指 | 平移 |
| 四指 | 平移；橡皮触点数量设为四指时改为掌擦 |
| 五指及以上 | 掌擦 |
| 多指分开 | 各自成笔（擦除工具除外） |

- 手势与多指书写按触点间距区分：间距不超过阈值时按画布手势处理，超过时各自成笔。
- 双指缩放中抬起一指转入平移，本次手势内不恢复缩放，需全部抬起重新开始。

### 设置

设置保存在程序目录的 `settings.json`，除切换目标进程即时生效外，全部设置在重启后生效。设置窗口按手势、边缘滚动提示、橡皮、撤销、工具栏、切换目标分组；数值可点 − + 调整，也可直接输入。工具栏大小按倍率缩放，三处工具栏与选区工具条同步。

## 鼠标与键盘

- 左键：书写、擦除、选择，放置克隆或粘贴的副本
- 滚轮：滚动画布；`Ctrl` + 滚轮：缩放；`Alt` + 滚轮：横向滚动
- `Ctrl+Z` / `Ctrl+Y`：撤销 / 重做
- `Delete`：删除选中墨迹；`Ctrl+C` / `Ctrl+X` / `Ctrl+V`：复制 / 剪切 / 粘贴
- `Ctrl` + `=`（或小键盘 `+`）/ `Ctrl` + `-`（或小键盘 `-`）/ `Ctrl+0`：放大 / 缩小 / 实际大小
- `Esc`：取消当前手势并退出盖章模式

## 下载

从 [Releases](https://github.com/KaiHuaDou/DrawingNotepad/releases/latest) 下载：

- `LightBoard-<版本>.zip`：免安装（框架依赖），需要 .NET 10 桌面运行时
- `LightBoard-runtime-<版本>.zip`：免安装（自包含）
- `LightBoard-nocv-<版本>.zip`、`LightBoard-nocv-runtime-<版本>.zip`：同上，不含 OpenCV（无视频展台）
- `LightBoard-installer-<版本>.exe`：安装包（框架依赖），需要 .NET 10 桌面运行时
- `LightBoard-installer-runtime-<版本>.exe`：安装包（自包含）
- `LightBoard-installer-nocv-<版本>.exe`、`LightBoard-installer-nocv-runtime-<版本>.exe`：同上，不含 OpenCV（无视频展台）

免安装包解压后运行 `LightBoard.exe`。

## 系统要求

- Windows 7 SP1 或更新版本
    - 已测试：Windows 7 SP1 上可安装 .NET 10.0 Desktop Runtime 并运行本程序
    - 安装包（`LightBoard-installer-*.exe`）需要 Windows 10 或更新版本
- .NET 10.0 Desktop Runtime（x64）或更新版本
    - 使用 `runtime` 版本可免安装运行时
- Microsoft Office（打开 Office 文档时需要）
    - 需要已激活版本，部分绿色版本需确认 COM 组件已注册
    - Office 2010 及更新版本
    - Office 2007 需安装插件 [Microsoft Save as PDF or XPS](https://legacyupdate.net/download-center/download/7/2007-microsoft-office-add-in-microsoft-save-as-pdf-or-xps)

## 构建

- IDE：Visual Studio Community 2026 或更新版本
  - 工作负载 「C# 桌面开发」
- .NET **10.0** SDK 或更新版本（`LangVersion` 为 `preview`）

```bash
dotnet publish LightBoard -p:PublishProfile=FolderProfile -c Release    # 框架依赖
dotnet publish LightBoard -p:PublishProfile=SelfContained -c Release    # 自包含
dotnet test
```

产物在 `LightBoard/bin/publish/`（框架依赖）与 `LightBoard/bin/publish-runtime/`（自包含）。加 `-p:WithOpenCv=false` 构建不含 OpenCV 的变体，两者分别为 `LightBoard/bin/publish-nocv/` 与 `LightBoard/bin/publish-nocv-runtime/`。

测试使用 xunit.v3 与 Microsoft Testing Platform。

## 项目结构

- `LightBoard/`：主程序。`InkCanvasNext/` 为画布控件，`Document/` 为文档渲染与背景，`Camera/` 为视频展台
- `LightBoard.Tests/`：测试
- `LightBoard.Installer/`：Inno Setup 打包脚本
- `docs/`：设计文档

## `InkCanvasNext`

`LightBoard/InkCanvasNext/` 是可复用的 WPF 控件

复制该文件夹到目标项目，启用 WPF，并引用 NuGet 包 `DependencyPropertyGenerator`。

接口说明见 [docs/InkCanvasNext.md](docs/InkCanvasNext.md)。

## 相关文档

- [更新日志](CHANGELOG.md)
- [路线图](docs/ROADMAP.md)

## 许可证

本项目以 [Apache-2.0 License](http://www.apache.org/licenses/) 提供。
