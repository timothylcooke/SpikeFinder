using System.Windows;
using System.Windows.Input;
using ToastNotifications.Core;

namespace SpikeFinder.Toast
{
    public class SfNotificationDisplayPart : NotificationDisplayPart
    {
        public SfNotificationDisplayPart(SfNotificationMessage notification)
        {
            DataContext = Notification = notification;
            InputBindings.Add(new KeyBinding { Modifiers = ModifierKeys.Alt, Key = Key.F4, Command = notification.CloseCommand });
            Style = Application.Current.FindResource(typeof(SfNotificationDisplayPart)) as Style;
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Keyboard.Focus(this);
        }
    }
}
