# 轻白板 / LightBoard

[![Release](https://img.shields.io/github/v/release/KaiHuaDou/DrawingNotepad)](https://github.com/KaiHuaDou/DrawingNotepad/releases/latest)
[![Build](https://img.shields.io/github/actions/workflow/status/KaiHuaDou/DrawingNotepad/build.yml?branch=master&label=Build)](https://github.com/KaiHuaDou/DrawingNotepad/actions/workflows/build.yml)
[![License](https://img.shields.io/github/license/KaiHuaDou/DrawingNotepad)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-11.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/11.0)
[![Platform](https://img.shields.io/badge/Windows-7%20SP1%2B-0078D6)](#系统要求)

基于 .NET 11 和 WPF 的全屏白板，用于课堂书写与课件批注。

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
- 选择后可移动、缩放、旋转、克隆或粘贴；空白处拖动默认套索圈选，可在设置中改为对角线矩形框选
- 撤销与重做，默认 200 步，可在设置中调整
- 橡皮、荧光笔、选择可在设置中设为用完指定次数操作后自动回到画笔，橡皮圈上可显示剩余次数
- 多人同时书写：触点间距超过阈值的多指各自成笔
- 支持手写笔：按设备压力改变粗细、反转笔擦除、笔杆按钮临时擦除、悬停显示笔尖；与触摸、鼠标按优先级仲裁
- 画布在右 / 下边缘自动扩展：缩放、拖动、滚轮与翻页后都保留一屏余量
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
- 每分钟把整块板备份到程序目录的 `recover`，异常退出后可打开其中最近一份
- 关闭时若有未保存内容，可选择快速保存或另选路径
- 单实例运行，重复启动时交由已运行的窗口处理

### 导出

- PNG：全部页面按页码命名导出，可选 25%、50%、100% 缩放
- PDF：整板导出为单个文件，缩放选项相同
- 导出过程显示进度与当前页码

### 抽选

- 菜单的「抽选」打开抽选窗口，窗口置顶
- 连续模式：设定最大值与范围，抽一个或按数量抽多个
- 集合模式：导入多个逗号分隔的文本文件作为数据集，从选中数据集的全部元素中抽选
- 模式、范围、数量、数据集与选中项保存在程序目录的 `lottery.json`，下次打开自动恢复
- 结果区显示最近一次结果；切换模式、改动范围或改动数据集时清空

### 视频展台

- 更多菜单里选「视频展台」：摄像头画面成为当前页的背景，与其他图片背景一样随画布缩放平移，直接在视频上书写批注，全部画布工具可用
- 画面按设备原生档协商：分辨率优先，同一分辨率取最高帧率；展台工具栏显示协商到的分辨率与帧率
- 视频背景同一时刻只有一页；「拍照」把当前帧冻结为该页的静态背景并新增一页接续视频；该页已有背景时照片另开一页追加到最后，原页背景保留
- 取消勾选或按展台工具栏的关闭按钮退出；旋转、镜像、冻结画面，多台设备按键循环切换
- 视频背景不写入 `.lbf`（拍照生成的静态背景与其他图片背景一致，随文件保存）
- 构建加 `-p:WithOpenCv=false` 时不含此功能

### 界面

- 左工具栏：关闭、最小化、文件菜单、背景、切换窗口
- 主工具栏：颜色、荧光笔、橡皮、清屏、撤销、重做；展开后为粗细、直线、圆、选择、粘贴
- 右工具栏：收起工具栏、菜单、上一页、页码、下一页
- 底色：背景弹窗里选网格、纯白、纯黑，随 `.lbf` 保存
- 更多菜单：透明演示与视频展台；透明演示时窗口置顶并留出鼠标穿透区
- 更多菜单：设为背景与清除本页背景，只作用当前页；铺多页用「附加」
- 右上角显示时间；菜单包含关于、设置、帮助、计时、抽选与调试

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

设置保存在程序目录的 `settings.json`，只记录改过的项；没改过的项跟随程序默认值，升级后按新的默认值生效。设置窗口左侧是分区导航，右侧是当前分区的条目；数值可点 − + 调整，也可直接输入。改过的项行尾有清除按钮，点它回到默认值；底部「恢复默认值」清空全部设置。除切换目标进程即时生效外，全部设置在重启后生效。工具栏大小按倍率缩放，三处工具栏与选区工具条同步。

## 鼠标与键盘

- 左键：书写、擦除、选择，放置克隆或粘贴的副本
- 滚轮：滚动画布；`Ctrl` + 滚轮：缩放；`Alt` + 滚轮：横向滚动
- `Ctrl+Z` / `Ctrl+Y`：撤销 / 重做
- `Delete`：删除选中墨迹；`Ctrl+C` / `Ctrl+X` / `Ctrl+V`：复制 / 剪切 / 粘贴
- `Ctrl` + `=`（或小键盘 `+`）/ `Ctrl` + `-`（或小键盘 `-`）/ `Ctrl+0`：放大 / 缩小 / 实际大小
- `Esc`：取消当前手势并退出盖章模式

## 下载

从 [Releases](https://github.com/KaiHuaDou/DrawingNotepad/releases/latest) 下载：

- `LightBoard-x64-<版本>.zip`：免安装（框架依赖），需要 .NET 11 桌面运行时
- `LightBoard-runtime-x64-<版本>.zip`：免安装（自包含）
- `LightBoard-nocv-x64-<版本>.zip`、`LightBoard-nocv-runtime-x64-<版本>.zip`：同上，不含 OpenCV（无视频展台）
- `LightBoard-arm64-<版本>.zip`、`LightBoard-runtime-arm64-<版本>.zip`：Windows Arm64，其余同上
- `LightBoard-nocv-arm64-<版本>.zip`、`LightBoard-nocv-runtime-arm64-<版本>.zip`：Windows Arm64，不含 OpenCV
- `LightBoard-installer-x64-<版本>.exe`：安装包（框架依赖），需要 .NET 11 桌面运行时
- `LightBoard-installer-runtime-x64-<版本>.exe`：安装包（自包含）
- `LightBoard-installer-nocv-x64-<版本>.exe`、`LightBoard-installer-nocv-runtime-x64-<版本>.exe`：同上，不含 OpenCV（无视频展台）
- `LightBoard-installer-arm64-<版本>.exe`、`LightBoard-installer-runtime-arm64-<版本>.exe`：Windows Arm64 安装包
- `LightBoard-installer-nocv-arm64-<版本>.exe`、`LightBoard-installer-nocv-runtime-arm64-<版本>.exe`：同上，不含 OpenCV

免安装包解压后运行 `LightBoard.exe`。

## 系统要求

- Windows 7 SP1 或更新版本
    - 已测试：Windows 7 SP1 上可安装 .NET 11.0 Desktop Runtime 并运行本程序
    - 安装包（`LightBoard-installer-*.exe`）需要 Windows 10 或更新版本
- Windows 11 Arm（Arm64 变体）
    - 安装包（`LightBoard-installer-*-arm64.exe`）需要 Windows 11 Arm
    - 免安装包需要 Arm64 版 .NET 11 桌面运行时，Windows 10 Arm 未测试
- .NET 11.0 Desktop Runtime（x64 或 Arm64）或更新版本
    - 使用 `runtime` 版本可免安装运行时
- Microsoft Office（打开 Office 文档时需要）
    - 需要已激活版本，部分绿色版本需确认 COM 组件已注册
    - Office 2010 及更新版本
    - Office 2007 需安装插件 [Microsoft Save as PDF or XPS](https://legacyupdate.net/download-center/download/7/2007-microsoft-office-add-in-microsoft-save-as-pdf-or-xps)

## 构建

- IDE：Visual Studio Community 2026 或更新版本
  - 工作负载 「C# 桌面开发」
- .NET **11.0** SDK 或更新版本（`LangVersion` 为 `preview`）

```bash
dotnet publish LightBoard -p:PublishProfile=FolderProfile -c Release    # 框架依赖
dotnet publish LightBoard -p:PublishProfile=SelfContained -c Release    # 自包含
dotnet test
```

产物在 `LightBoard/bin/publish-x64/`（框架依赖）与 `LightBoard/bin/publish-runtime-x64/`（自包含）。加 `-p:WithOpenCv=false` 构建不含 OpenCV 的变体，两者分别为 `LightBoard/bin/publish-nocv-x64/` 与 `LightBoard/bin/publish-nocv-runtime-x64/`。

Arm64 使用 `FolderProfileArm64` 与 `SelfContainedArm64`，产物在 `LightBoard/bin/publish-arm64/` 与 `LightBoard/bin/publish-runtime-arm64/`，nocv 变体为 `publish-nocv-arm64/` 与 `publish-nocv-runtime-arm64/`。

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

## LLM 声明

- 所有测试都是 AI 写的，文档编写用了不少 AI 辅助。

## 许可证

本项目以 [Apache-2.0 License](http://www.apache.org/licenses/) 提供。
