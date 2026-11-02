using LightBoard.Tests.Toolbar;

namespace LightBoard.Tests;

/// <summary>倒计时状态机：暂停后重置回到本轮起点，其余情况清零</summary>
[Collection("Toolbar")]
public class TimerViewModelTests
{
    [Fact]
    public void Reset_WhenIdle_Clears( )
    {
        UiThread.Run(( ) =>
        {
            var timer = new TimerViewModel( );
            try
            {
                timer.ApplyPreset(5);

                timer.Reset( );

                Assert.True(timer.IsIdle);
                Assert.Equal(0, timer.Minutes);
                Assert.False(timer.CanToggle);
            }
            finally
            {
                timer.Stop( );
            }
        });
    }

    [Fact]
    public void Reset_WhenPaused_RestoresStartValue( )
    {
        UiThread.Run(( ) =>
        {
            var timer = new TimerViewModel( );
            try
            {
                timer.ApplyPreset(5);
                timer.Toggle( );

                Assert.True(timer.IsActive);
                Assert.False(timer.CanReset);

                timer.Toggle( );

                Assert.False(timer.IsActive);
                Assert.True(timer.CanReset);

                timer.Reset( );

                Assert.True(timer.IsIdle);
                Assert.Equal(5, timer.Minutes);
                Assert.Equal(0, timer.Seconds);
            }
            finally
            {
                timer.Stop( );
            }
        });
    }

    [Fact]
    public void Adjust_BelowZero_StopsAtZero( )
    {
        UiThread.Run(( ) =>
        {
            var timer = new TimerViewModel( );
            try
            {
                timer.ApplyPreset(1);

                timer.Adjust(TimeSpan.FromMinutes(-2));

                Assert.Equal(0, timer.Minutes);
                Assert.False(timer.CanToggle);
            }
            finally
            {
                timer.Stop( );
            }
        });
    }

    [Fact]
    public void SetMinutes_KeepsHours( )
    {
        UiThread.Run(( ) =>
        {
            var timer = new TimerViewModel( );
            try
            {
                timer.ApplyPreset(75);

                timer.SetMinutes(40);

                Assert.Equal(1, timer.Hours);
                Assert.Equal(40, timer.Minutes);
                Assert.Equal(0, timer.Seconds);
            }
            finally
            {
                timer.Stop( );
            }
        });
    }

    [Fact]
    public void SetMinutes_NotAdjustable_Ignored( )
    {
        UiThread.Run(( ) =>
        {
            var timer = new TimerViewModel( );
            try
            {
                timer.SetMode(TimerMode.Countup);

                timer.SetMinutes(40);

                Assert.Equal(0, timer.Minutes);
            }
            finally
            {
                timer.Stop( );
            }
        });
    }

    [Fact]
    public void HourLabels_FollowDisplayedHour( )
    {
        UiThread.Run(( ) =>
        {
            var timer = new TimerViewModel( );
            try
            {
                timer.ApplyPreset(75);

                Assert.Equal(0, timer.PreviousHour);
                Assert.Equal(2, timer.NextHour);
            }
            finally
            {
                timer.Stop( );
            }
        });
    }

    [Fact]
    public void ShowsProgress_OnlyWithinCountdownRun( )
    {
        UiThread.Run(( ) =>
        {
            var timer = new TimerViewModel( );
            try
            {
                timer.ApplyPreset(5);
                Assert.False(timer.ShowsProgress);

                timer.Toggle( );
                Assert.True(timer.ShowsProgress);

                timer.Toggle( );
                Assert.True(timer.ShowsProgress);

                timer.Reset( );
                Assert.False(timer.ShowsProgress);
            }
            finally
            {
                timer.Stop( );
            }
        });
    }

    [Fact]
    public void DecreaseMinute_AtZeroMinutes_BorrowsFromHours( )
    {
        UiThread.Run(( ) =>
        {
            var timer = new TimerViewModel( );
            try
            {
                timer.ApplyPreset(60);

                Assert.Equal(0, timer.Minutes);
                Assert.True(timer.CanDecreaseMinute);

                timer.Adjust(TimeSpan.FromMinutes(-1));

                Assert.Equal(0, timer.Hours);
                Assert.Equal(59, timer.Minutes);

                timer.ApplyPreset(0);

                Assert.False(timer.CanDecreaseMinute);
            }
            finally
            {
                timer.Stop( );
            }
        });
    }

    [Fact]
    public void SetMode_SwitchesTitleAndValue( )
    {
        UiThread.Run(( ) =>
        {
            var timer = new TimerViewModel( );
            try
            {
                timer.ApplyPreset(5);

                timer.SetMode(TimerMode.Countup);

                Assert.Equal("计时器", timer.Title);
                Assert.Equal(0, timer.Minutes);
                Assert.True(timer.CanToggle);
            }
            finally
            {
                timer.Stop( );
            }
        });
    }
}
