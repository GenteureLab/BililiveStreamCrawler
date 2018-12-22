namespace BililiveStreamCrawler.Common
{
    public enum CommandType
    {
        /// <summary>
        /// Client 向 Server 发送，申请分配任务
        /// </summary>
        Request = 0,
        /// <summary>
        /// Server 向 Client 发送，分配任务
        /// </summary>
        Issue = 1,
        /// <summary>
        /// Client 向 Server 发送，任务完成
        /// </summary>
        Response = 2
    }
}