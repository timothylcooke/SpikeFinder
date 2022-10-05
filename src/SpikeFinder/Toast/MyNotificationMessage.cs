using ReactiveUI;
using System.Reactive;
using System.Windows;
using ToastNotifications.Core;
using ToastNotifications.Messages.Core;

namespace SpikeFinder.Toast
{
    public class SfNotificationMessage : MessageBase<SfNotificationDisplayPart>
    {
        public SfNotificationMessage(Severity notificationType, string message, string? title, ToastButton? actionButton) : this(notificationType, message, title, actionButton, new MessageOptions())
        {

        }
        public SfNotificationMessage(Severity notificationType, string message, string? title, ToastButton? actionButton, MessageOptions options) : base(message, options)
        {
            Title = title;
            ActionButton = actionButton;
            NotificationType = notificationType;
            CloseCommand = ReactiveCommand.Create(Close);
        }

        public string? Title { get; }
        public Visibility TitleVisibility => string.IsNullOrEmpty(Title) ? Visibility.Collapsed : Visibility.Visible;

        public ToastButton? ActionButton { get; }
        public Visibility ActionButtonVisibility => ActionButton == null ? Visibility.Collapsed : Visibility.Visible;

        public Severity NotificationType { get; }

        public ReactiveCommand<Unit, Unit> CloseCommand { get; }

        protected override SfNotificationDisplayPart CreateDisplayPart() => new SfNotificationDisplayPart(this);
        protected override void UpdateDisplayOptions(SfNotificationDisplayPart displayPart, MessageOptions options) { }
    }
}
