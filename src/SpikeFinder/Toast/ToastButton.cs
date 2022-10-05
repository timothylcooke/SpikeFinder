using System.Windows.Input;

namespace SpikeFinder.Toast
{
    public record ToastButton(ICommand Command, object? CommandParameter, string ButtonText);
}
