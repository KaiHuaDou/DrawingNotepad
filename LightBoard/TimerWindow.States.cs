using System;
using System.Diagnostics;

namespace LightBoard;

internal enum TimerState
{
    Idle,
    Running,
    Alerting,
}

public partial class TimerWindow
{
    private TimerState state = TimerState.Idle;

    private void UpdateState( )
    {
        if (state == TimerState.Idle)
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

        var from = state;

        ExitState(from, newState);
        EnterState(newState, from);

        state = newState;
    }

    private void ExitState(TimerState from, TimerState to)
    {
        if (from != TimerState.Idle && to == TimerState.Idle)
        {
            Settle( );
            timer.Stop( );
        }

        if (from == TimerState.Alerting)
        {
            RestoreForeground( );
        }
    }

    private void EnterState(TimerState to, TimerState from)
    {
        if (from == TimerState.Idle && to != TimerState.Idle)
        {
            startedAt = Stopwatch.GetTimestamp( );
            timer.Start( );
        }

        if (to == TimerState.Alerting)
        {
            ApplyAlertForeground( );
        }
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
}
