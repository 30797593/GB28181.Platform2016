using Grpc.Core;
using MediaContract;

namespace GrpcAgent
{

    /// <summary>
    /// 实时视频播放事件处理
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="state"></param>
    public delegate void LivePlayRequestHandler(StartLiveRequest request, ServerCallContext context);
    /// <summary>
    /// 视频回放播放事件处理
    /// </summary>
    /// <param name="cata"></param>
    public delegate void PlaybackRequestHandler(StartPlaybackRequest request, ServerCallContext context);
    /// <summary>
    /// 录像点播事件处理
    /// </summary>
    /// <param name="request"></param>
    /// <param name="context"></param>
    public delegate void HistoryPlayRequestHandler(StartHistoryRequest request, ServerCallContext context);
    /// <summary>
    /// 视频文件下载事件处理
    /// </summary>
    /// <param name="record"></param>
    public delegate void VideoDownloadRequestHandler(VideoDownloadRequest request, ServerCallContext context);
    

    public class MediaEventSource
    {
        public event LivePlayRequestHandler LivePlayRequestReceived = null;
        public event PlaybackRequestHandler PlaybackRequestReceived = null;
        public event HistoryPlayRequestHandler HistoryPlayRequestReceived = null;
        public event VideoDownloadRequestHandler VideoDownloadRequestReceived = null;

        internal void FileLivePlayRequestEvent(StartLiveRequest request, ServerCallContext context)
        {
            LivePlayRequestReceived?.Invoke(request, context);
        }
                
        internal void FilePlaybackRequestEvent(StartPlaybackRequest request, ServerCallContext context)
        {
            PlaybackRequestReceived?.Invoke(request, context);
        }

        internal void FileHistoryPlayRequestEvent(StartHistoryRequest request, ServerCallContext context)
        {
            HistoryPlayRequestReceived?.Invoke(request, context);
        }

        internal void VideoDownloadRequestEvent(VideoDownloadRequest request, ServerCallContext context)
        {
            VideoDownloadRequestReceived?.Invoke(request, context);
        }
    }
}
