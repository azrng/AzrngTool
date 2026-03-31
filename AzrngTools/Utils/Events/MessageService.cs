using CommunityToolkit.Mvvm.Messaging;

namespace AzrngTools.Utils.Events
{
    public interface IMessageService
    {
        void SendMessage(string message, string title = "提示", string filterName = "Main");
    }

    public class MessageService : IMessageService, ITransientDependency
    {
        public void SendMessage(string message, string title = "提示", string filterName = "Main")
        {
            WeakReferenceMessenger.Default.Send(new MessageModel(title, message, filterName), filterName);
        }
    }
}