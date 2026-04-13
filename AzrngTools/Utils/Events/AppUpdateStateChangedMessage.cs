using CommunityToolkit.Mvvm.Messaging.Messages;

namespace AzrngTools.Utils.Events;

public sealed class AppUpdateStateChangedMessage : ValueChangedMessage<DateTimeOffset>
{
    public AppUpdateStateChangedMessage() : base(DateTimeOffset.UtcNow)
    {
    }
}
