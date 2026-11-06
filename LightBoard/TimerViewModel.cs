using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Threading;

namespace LightBoard;

public enum TimerMode
{
    Countdown,
    Countup,
}

internal enum TimerState
{
    Idle,
    Running,
    Alerting,
    Paused,
}

/// <summary>
/// 倒计时窗口的计时状态：模式、运行状态、显示值与可用操作
/// </summary>
public sealed class TimerViewModel : INotifyPropertyChanged
{
    private static readonly TimeSpan AlertSpan = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MaxSpan = TimeSpan.FromSeconds(99 * 3600 + 59 * 60 + 59);

    private readonly DispatcherTimer timer;

    private TimerMode mode = TimerMode.Countdown;
    private TimerState state = TimerState.Idle;
    private TimeSpan countdownRemaining;
    private TimeSpan countupElapsed;

    /// <summary>
    /// 本轮开始时的数值，暂停后重置回到这里
    /// </summary>
    private TimeSpan startValue;

    private long startedAt;
    private int totalSeconds;

    /// <summary>
    /// 上次推送属性通知时的状态，用于跳过重复通知
    /// </summary>
    private (TimerState State, TimerMode Mode, int Seconds) shown;

    public TimerViewModel( )
    {
        timer = new(DispatcherPriority.Normal, Dispatcher.CurrentDispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(100),
        };
        timer.Tick += (_, _) => Refresh( );

        Refresh( );
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 每次刷新显示值后触发
    /// </summary>
    public event EventHandler? DisplayChanged;

    public string Title => mode == TimerMode.Countdown ? "倒计时" : "计时器";

    public int Hours => totalSeconds / 3600;

    public int Minutes => totalSeconds % 3600 / 60;

    public int Seconds => totalSeconds % 60;

    /// <summary>
    /// 倒计时模式
    /// </summary>
    public bool IsCountdown => mode == TimerMode.Countdown;

    /// <summary>
    /// 计时推进中
    /// </summary>
    public bool IsActive => state is TimerState.Running or TimerState.Alerting;

    /// <summary>
    /// 未开始或本轮已结束
    /// </summary>
    public bool IsIdle => state == TimerState.Idle;

    public bool IsAlerting => state == TimerState.Alerting;

    /// <summary>
    /// 倒计时在本轮内（推进或暂停）时按进度显示
    /// </summary>
    public bool ShowsProgress => mode == TimerMode.Countdown && state != TimerState.Idle;

    /// <summary>
    /// 本轮已完成的比例 0~1
    /// </summary>
    public double Progress => ShowsProgress && startValue > TimeSpan.Zero
        ? Math.Clamp(1 - DisplayValue( ).TotalSeconds / startValue.TotalSeconds, 0, 1)
        : 0;

    /// <summary>
    /// 倒计时且未开始时才能调整时长
    /// </summary>
    public bool IsAdjustable => IsIdle && mode == TimerMode.Countdown;

    /// <summary>
    /// 推进中可暂停，否则需要能启动
    /// </summary>
    public bool CanToggle => IsActive || CanStart( );

    /// <summary>
    /// 未开始、已结束或暂停时可重置
    /// </summary>
    public bool CanReset => state is TimerState.Idle or TimerState.Paused;

    public bool CanDecreaseHour => Hours > 0;

    /// <summary>
    /// 分钟还能再减一分钟，分钟为 0 时向小时借位
    /// </summary>
    public bool CanDecreaseMinute => totalSeconds > 0;

    public void Toggle( )
    {
        if (IsActive)
        {
            SetState(TimerState.Paused);
        }
        else if (CanStart( ))
        {
            SetState(StartState( ));
        }

        Refresh( );
    }

    /// <summary>
    /// 暂停后回到本轮开始时的数值，其余情况清零
    /// </summary>
    public void Reset( )
    {
        if (!CanReset)
        {
            return;
        }

        var value = state == TimerState.Paused ? startValue : TimeSpan.Zero;

        if (mode == TimerMode.Countdown)
        {
            countdownRemaining = value;
        }
        else
        {
            countupElapsed = value;
        }

        SetState(TimerState.Idle);
        Refresh( );
    }

    /// <summary>
    /// 只在未开始时切换模式，暂停中与推进中保持当前模式
    /// </summary>
    public void SetMode(TimerMode value)
    {
        if (mode == value || !IsIdle)
        {
            return;
        }

        mode = value;
        Raise(nameof(Title));
        Raise(nameof(IsCountdown));
        Refresh( );
    }

    /// <summary>
    /// 倒计时未开始时增减时长
    /// </summary>
    public void Adjust(TimeSpan delta)
    {
        if (!IsAdjustable)
        {
            return;
        }

        countdownRemaining = Clamp(countdownRemaining + delta, TimeSpan.Zero, MaxSpan);
        Refresh( );
    }

    /// <summary>
    /// 只替换分钟字段，保留小时；倒计时可调整时生效
    /// </summary>
    public void SetMinutes(int minutes)
    {
        if (!IsAdjustable || minutes == Minutes)
        {
            return;
        }

        countdownRemaining = Clamp(TimeSpan.FromHours(Hours) + TimeSpan.FromMinutes(minutes), TimeSpan.Zero, MaxSpan);
        Refresh( );
    }

    public void Stop( )
    {
        timer.Stop( );
    }

    private void Refresh( )
    {
        UpdateState( );

        var seconds = (int) DisplayValue( ).TotalSeconds;
        var signature = (state, mode, seconds);

        // 秒数、状态与模式未变时推导出来的属性也没有变化，每拍重复通知只会让绑定重复求值
        if (signature != shown)
        {
            shown = signature;
            totalSeconds = seconds;

            Raise(nameof(Hours));
            Raise(nameof(Minutes));
            Raise(nameof(Seconds));
            Raise(nameof(CanDecreaseHour));
            Raise(nameof(CanDecreaseMinute));
            Raise(nameof(IsActive));
            Raise(nameof(IsIdle));
            Raise(nameof(IsAlerting));
            Raise(nameof(IsAdjustable));
            Raise(nameof(CanToggle));
            Raise(nameof(CanReset));
        }

        DisplayChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateState( )
    {
        if (!IsActive)
        {
            return;
        }

        var value = DisplayValue( );

        if (mode == TimerMode.Countdown)
        {
            if (value <= TimeSpan.Zero)
            {
                SetState(TimerState.Idle);
            }
            else if (value <= AlertSpan)
            {
                SetState(TimerState.Alerting);
            }
        }
        else if (value >= MaxSpan)
        {
            SetState(TimerState.Idle);
        }
    }

    private void SetState(TimerState newState)
    {
        if (state == newState)
        {
            return;
        }

        var counting = newState is TimerState.Running or TimerState.Alerting;

        // 只在从停止状态起步时重开秒表，运行与告警之间切换沿用本轮计时
        if (counting && !IsActive)
        {
            if (state == TimerState.Idle)
            {
                startValue = DisplayValue( );
            }

            startedAt = Stopwatch.GetTimestamp( );
            timer.Start( );
        }
        else if (!counting)
        {
            Settle( );
            timer.Stop( );
        }

        state = newState;
    }

    /// <summary>
    /// 从暂停恢复时若已进入告警区间，直接进入告警状态
    /// </summary>
    private TimerState StartState( )
    {
        return mode == TimerMode.Countdown && DisplayValue( ) <= AlertSpan
            ? TimerState.Alerting
            : TimerState.Running;
    }

    /// <summary>当前显示的时长：倒计时为剩余，正计时为累计</summary>
    private TimeSpan DisplayValue( )
    {
        var used = Elapsed( );

        return mode == TimerMode.Countdown
            ? Clamp(countdownRemaining - used, TimeSpan.Zero, MaxSpan)
            : Clamp(countupElapsed + used, TimeSpan.Zero, MaxSpan);
    }

    /// <summary>本轮已运行的时长；取单调时钟，不受系统时间调整影响</summary>
    private TimeSpan Elapsed( )
    {
        return IsActive ? Stopwatch.GetElapsedTime(startedAt) : TimeSpan.Zero;
    }

    private bool CanStart( )
    {
        return mode == TimerMode.Countup ? countupElapsed < MaxSpan : countdownRemaining > TimeSpan.Zero;
    }

    /// <summary>把本轮已运行时长结算进当前模式的数值</summary>
    private void Settle( )
    {
        var used = Elapsed( );

        if (mode == TimerMode.Countdown)
        {
            countdownRemaining = Clamp(countdownRemaining - used, TimeSpan.Zero, MaxSpan);
        }
        else
        {
            countupElapsed = Clamp(countupElapsed + used, TimeSpan.Zero, MaxSpan);
        }
    }

    private static TimeSpan Clamp(TimeSpan value, TimeSpan min, TimeSpan max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }

    private void Raise(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
