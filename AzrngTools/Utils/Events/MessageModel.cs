namespace AzrngTools.Utils.Events
{
    /// <summary>
    /// 消息发送模型
    /// </summary>
    public class MessageModel
    {
        public MessageModel(string title, string message, string filter)
        {
            Title = title;
            Message = message;
            Filter = filter;
        }

        /// <summary>
        /// 消息过滤  用来判断发送到哪个地方
        /// </summary>
        public string Filter { get; set; }

        /// <summary>
        /// 标题
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 消息内容
        /// </summary>
        public string Message { get; set; }
    }
}
