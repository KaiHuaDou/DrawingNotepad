using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace LightBoard;

/// <summary>
/// 抽选窗口的状态：模式、范围、数据集与最近一次结果
/// </summary>
public sealed partial class LotteryViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// 最大值上限
    /// </summary>
    public const int MaxLimit = 9999;

    // 两种模式各自保留数量，切换模式不会互相覆盖
    private int continuousCount = 5;
    private int collectionCount = 5;

    public event PropertyChangedEventHandler? PropertyChanged;

    public LotteryMode Mode
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Status = string.Empty;
            ResetDrawMarks( );
            Results.Clear( );

            Raise( );
            Raise(nameof(IsContinuous));
            Raise(nameof(IsCollection));
            RaisePool( );

            // 取新模式自己保留的数量；数量没变时只有上限可用状态会随候选变化
            var restored = ClampCount(value == LotteryMode.Continuous ? continuousCount : collectionCount);
            if (restored == Count)
            {
                Raise(nameof(CanIncreaseCount));
            }
            else
            {
                Count = restored;
            }
        }
    } = LotteryMode.Continuous;

    public int Max
    {
        get;
        set
        {
            var clamped = Math.Clamp(value, 1, MaxLimit);
            if (field == clamped)
            {
                return;
            }

            field = clamped;
            Results.Clear( );

            // 范围越界时由两个范围 setter 夹紧并通知候选与数量
            Lower = Math.Min(Lower, clamped);
            Upper = Math.Min(Upper, clamped);

            Raise( );
            Raise(nameof(MaxText));
            Raise(nameof(CanDecreaseMax));
            Raise(nameof(CanIncreaseMax));
        }
    } = 51;

    public int Lower
    {
        get;
        set
        {
            var clamped = Math.Clamp(value, 1, Upper);
            if (field == clamped)
            {
                return;
            }

            field = clamped;
            Results.Clear( );

            Raise( );
            RaisePool( );
            RefreshCount( );
        }
    } = 1;

    public int Upper
    {
        get;
        set
        {
            var clamped = Math.Clamp(value, Lower, Max);
            if (field == clamped)
            {
                return;
            }

            field = clamped;
            Results.Clear( );

            Raise( );
            RaisePool( );
            RefreshCount( );
        }
    } = 51;

    public int Count
    {
        get;
        set
        {
            var clamped = ClampCount(value);
            if (field == clamped)
            {
                return;
            }

            field = clamped;

            if (Mode == LotteryMode.Continuous)
            {
                continuousCount = clamped;
            }
            else
            {
                collectionCount = clamped;
            }

            Raise( );
            Raise(nameof(CountText));
            Raise(nameof(CanDecreaseCount));
            Raise(nameof(CanIncreaseCount));
        }
    } = 5;

    /// <summary>
    /// 当前模式下一次抽选允许抽到的值的个数；集合模式的不重复开关打开后只算未抽中的选项
    /// </summary>
    public int PoolSize => Mode == LotteryMode.Continuous ? Upper - Lower + 1
        : NoRepeat ? Options.Count(option => !option.IsDrawn)
        : SelectedDataset?.Count ?? 0;

    public bool IsContinuous => Mode == LotteryMode.Continuous;

    public bool IsCollection => Mode == LotteryMode.Collection;

    public bool CanDraw => PoolSize > 0;

    public bool CanDecreaseMax => Max > 1;

    public bool CanIncreaseMax => Max < MaxLimit;

    public bool CanDecreaseCount => Count > 1;

    public bool CanIncreaseCount => Count < Math.Max(PoolSize, 1);

    public bool HasDataset => SelectedDataset is not null;

    public ObservableCollection<LotteryDataset> Datasets { get; } = [];

    public ObservableCollection<string> Results { get; } = [];

    public LotteryDataset? SelectedDataset
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            RefreshOptions( );
            Results.Clear( );

            Raise( );
            Raise(nameof(HasDataset));
            RaisePool( );
            RefreshCount( );
        }
    }

    public string MaxText
    {
        get => Max.ToString(CultureInfo.InvariantCulture);
        set
        {
            var previous = Max;

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                Max = parsed;
            }

            // 输入非法或被夹回原值时 Max 不会通知，需要把规范文本写回输入框
            if (previous == Max)
            {
                Raise( );
            }
        }
    }

    public string CountText
    {
        get => Count.ToString(CultureInfo.InvariantCulture);
        set
        {
            var previous = Count;

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                Count = parsed;
            }

            if (previous == Count)
            {
                Raise( );
            }
        }
    }

    public string Status
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Raise( );
        }
    } = string.Empty;

    public void StepMax(int delta)
    {
        Max += delta;
    }

    public void StepCount(int delta)
    {
        Count += delta;
    }

    /// <summary>
    /// 候选集合变化后按当前模式夹紧数量；数量没变时只有上限可用状态会变
    /// </summary>
    private void RefreshCount( )
    {
        var clamped = ClampCount(Count);
        if (clamped != Count)
        {
            Count = clamped;
            return;
        }

        Raise(nameof(CanIncreaseCount));
    }

    /// <summary>
    /// 连续模式按取值范围、集合模式按数据集大小夹紧；集合模式没有数据集时不设上限，保留设定值
    /// </summary>
    private int ClampCount(int value)
    {
        if (Mode == LotteryMode.Continuous)
        {
            return Math.Clamp(value, 1, Upper - Lower + 1);
        }

        return SelectedDataset is { } dataset ? Math.Clamp(value, 1, dataset.Count) : Math.Max(value, 1);
    }

    private void RaisePool( )
    {
        Raise(nameof(PoolSize));
        Raise(nameof(CanDraw));
    }

    private void Raise([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
