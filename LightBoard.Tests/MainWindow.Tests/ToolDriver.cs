using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace LightBoard.Tests.Toolbar;

/// <summary>
/// 模拟真实用户交互：单选组先执行 WPF 的组内互斥再触发 Click；ToggleButton 先翻转状态再触发 Click。
/// </summary>
internal static class ToolDriver
{
    /// <summary>模拟点击单选按钮（颜色、粗细、工具）。</summary>
    public static void ClickRadio(MainWindowHost host, RadioButton radio)
    {
        if (radio.GroupName.Length > 0)
        {
            foreach (var sibling in host.RadiosInGroup(radio.GroupName))
            {
                if (!ReferenceEquals(sibling, radio))
                {
                    sibling.IsChecked = false;
                }
            }
        }

        radio.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, radio));
    }

    /// <summary>模拟点击 ToggleButton：先翻转勾选，再触发 Click。</summary>
    public static void Toggle(ToggleButton toggle, bool on)
    {
        toggle.IsChecked = on;
        toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, toggle));
    }

    public static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
    }

    public static void Esc(MainWindow window)
    {
        // 窗口未显示，没有真实 PresentationSource 派发键盘事件，故传入一个最小桩实现。
        var e = new KeyEventArgs(Keyboard.PrimaryDevice, new NullPresentationSource( ), 0, Key.Escape)
        {
            RoutedEvent = Keyboard.KeyUpEvent
        };
        window.RaiseEvent(e);
    }

    private sealed class NullPresentationSource : PresentationSource
    {
        private Visual? root;

        public override Visual RootVisual
        {
            get => root!;
            set => root = value;
        }

        public override bool IsDisposed => false;

        protected override CompositionTarget? GetCompositionTargetCore( )
        {
            return null;
        }
    }
}