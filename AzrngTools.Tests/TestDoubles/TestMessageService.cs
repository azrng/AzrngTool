using AzrngTools.Utils.Events;

namespace AzrngTools.Tests.TestDoubles;

internal sealed class TestMessageService : IMessageService
{
    public List<(string Message, string Title, string FilterName)> Messages { get; } = [];

    public void SendMessage(string message, string title = "提示", string filterName = "Main")
    {
        Messages.Add((message, title, filterName));
    }
}
