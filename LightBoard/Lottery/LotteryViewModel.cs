using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

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

    public LotteryViewModel( )
    {
        Status = string.Empty;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public LotteryMode Mode { get; private set; } = LotteryMode.Continuous;

    public int Max { get; private set; } = 51;

    public int Lower { get; private set; } = 1;

    public int Upper { get; private set; } = 51;

    public int Count { get; private set; } = 5;

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
            ClampCount( );
            Results.Clear( );

            Raise(nameof(SelectedDataset));
            Raise(nameof(HasDataset));
            RaisePool( );
            RaiseCount( );
        }
    }

    public string MaxText
    {
        get => Max.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                SetMax(parsed);
            }

            Raise(nameof(MaxText));
        }
    }

    public string CountText
    {
        get => Count.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                SetCount(parsed);
            }

            Raise(nameof(CountText));
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
            Raise(nameof(Status));
        }
    }

    public void SetMode(LotteryMode value)
    {
        if (Mode == value)
        {
            return;
        }

        Mode = value;
        Status = string.Empty;
        ResetDrawMarks( );
        ClampCount( );
        Results.Clear( );

        Raise(nameof(Mode));
        Raise(nameof(IsContinuous));
        Raise(nameof(IsCollection));
        RaisePool( );
        RaiseCount( );
    }

    public void SetMax(int value)
    {
        Max = Math.Clamp(value, 1, MaxLimit);

        Lower = Math.Min(Lower, Max);
        Upper = Math.Min(Upper, Max);
        ClampCount( );
        Results.Clear( );

        Raise(nameof(Max));
        Raise(nameof(MaxText));
        RaiseRange( );
        RaisePool( );
        RaiseCount( );
        Raise(nameof(CanDecreaseMax));
        Raise(nameof(CanIncreaseMax));
    }

    public void SetLower(int value)
    {
        var clamped = Math.Clamp(value, 1, Upper);
        if (clamped == Lower)
        {
            return;
        }

        Lower = clamped;
        ClampCount( );
        Results.Clear( );

        RaiseRange( );
        RaisePool( );
        RaiseCount( );
    }

    public void SetUpper(int value)
    {
        var clamped = Math.Clamp(value, Lower, Max);
        if (clamped == Upper)
        {
            return;
        }

        Upper = clamped;
        ClampCount( );
        Results.Clear( );

        RaiseRange( );
        RaisePool( );
        RaiseCount( );
    }

    public void SetCount(int value)
    {
        var clamped = Math.Clamp(value, 1, Math.Max(PoolSize, 1));
        if (clamped == Count)
        {
            Raise(nameof(CountText));
            return;
        }

        Count = clamped;
        RaiseCount( );
    }

    public void StepMax(int delta)
    {
        SetMax(Max + delta);
    }

    public void StepCount(int delta)
    {
        SetCount(Count + delta);
    }

    private void ClampCount( )
    {
        Count = Math.Clamp(Count, 1, Math.Max(PoolSize, 1));
    }

    private void RaiseRange( )
    {
        Raise(nameof(Lower));
        Raise(nameof(Upper));
    }

    private void RaisePool( )
    {
        Raise(nameof(PoolSize));
        Raise(nameof(CanDraw));
    }

    private void RaiseCount( )
    {
        Raise(nameof(Count));
        Raise(nameof(CountText));
        Raise(nameof(CanDecreaseCount));
        Raise(nameof(CanIncreaseCount));
    }

    private void Raise(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
