using System.ComponentModel;

namespace LightBoard;

/// <summary>
/// 设置窗口的一项：标签、写回记录用的键，以及取值、折叠与清除
/// </summary>
public interface ISettingItem : INotifyPropertyChanged
{
    string Label { get; }

    /// <summary>
    /// 记录里的属性名，用于核对每一项都有对应属性
    /// </summary>
    string Key { get; }

    /// <summary>
    /// 用户是否设过此项；未设置时按程序默认值生效
    /// </summary>
    bool IsSet { get; }

    bool HasErrors { get; }

    void Load(LightBoardSettings settings);

    LightBoardSettings Apply(LightBoardSettings settings);

    /// <summary>
    /// 回到未设置状态
    /// </summary>
    void Clear( );
}
