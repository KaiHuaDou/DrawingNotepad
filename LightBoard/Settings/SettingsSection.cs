using System.Collections.Generic;

namespace LightBoard;

/// <summary>
/// 设置窗口的一个分区：导航标题与条目
/// </summary>
public sealed record SettingsSection(string Title, IReadOnlyList<ISettingItem> Items);
