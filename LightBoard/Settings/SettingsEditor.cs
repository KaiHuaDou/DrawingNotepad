using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace LightBoard;

/// <summary>
/// 设置窗口的表单状态：分区表、当前分区与折叠
/// </summary>
public sealed class SettingsEditor : INotifyPropertyChanged
{
    private SettingsSection selectedSection;

    public SettingsEditor(LightBoardSettings settings)
    {
        Sections = SettingsSchema.Create( );
        selectedSection = Sections[0];

        foreach (var item in Items)
        {
            item.Load(settings);
            item.PropertyChanged += ItemChanged;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<SettingsSection> Sections { get; }

    public IEnumerable<ISettingItem> Items => Sections.SelectMany(section => section.Items);

    public SettingsSection SelectedSection
    {
        get => selectedSection;
        set
        {
            if (selectedSection == value)
            {
                return;
            }

            selectedSection = value;
            Raise(nameof(SelectedSection));
        }
    }

    public bool HasErrors => Items.Any(item => item.HasErrors);

    /// <summary>
    /// 第一个不合范围的项及其分区，供跨分区聚焦
    /// </summary>
    public (SettingsSection Section, ISettingItem Item)? FirstError
    {
        get
        {
            foreach (var section in Sections)
            {
                foreach (var item in section.Items)
                {
                    if (item.HasErrors)
                    {
                        return (section, item);
                    }
                }
            }

            return null;
        }
    }

    /// <summary>
    /// 折叠成设置记录；未设置的项不写入
    /// </summary>
    public LightBoardSettings Build( )
    {
        var result = new LightBoardSettings( );
        foreach (var item in Items)
        {
            result = item.Apply(result);
        }

        return result;
    }

    /// <summary>
    /// 全部回到未设置状态
    /// </summary>
    public void Reset( )
    {
        foreach (var item in Items)
        {
            item.Clear( );
        }
    }

    private void ItemChanged(object? o, PropertyChangedEventArgs e)
    {
        Raise(nameof(HasErrors));
    }

    private void Raise(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
