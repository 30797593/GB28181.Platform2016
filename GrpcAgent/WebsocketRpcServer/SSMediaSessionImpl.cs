using Grpc.Core;
using MediaContract;
using System.Threading.Tasks;
using SIPSorcery.GB28181.Servers;
using System;
using Logger4Net;
using System.Collections.Generic;

namespace GrpcAgent.WebsocketRpcServer
{
    public class SSMediaSessionImpl : VideoSession.VideoSessionBase
    {
        private static ILog logger = LogManager.GetLogger("RpcServer");
        private MediaEventSource _eventSource = null;
        private ISIPServiceDirector _sipServiceDirector = null;

        public SSMediaSessionImpl(MediaEventSource eventSource, ISIPServiceDirector sipServiceDirector)
        {
            _eventSource = eventSource;
            _sipServiceDirector = sipServiceDirector;
        }

        public override Task<KeepAliveReply> KeepAlive(KeepAliveRequest request, ServerCallContext context)
        {
            foreach (Dictionary<string, DateTime> dict in _sipServiceDirector.VideoSessionAlive)
            {
                if (dict.ContainsKey(request.Gbid + "," + request.Hdr.Sessionid))
                {
                    dict[request.Gbid + "," + request.Hdr.Sessionid] = DateTime.Now;
                }
            }
            var keepAliveReply = new KeepAliveReply()
            {
                Status = new MediaContract.Status()
                {
                    Code = 200,
                    Msg = "KeepAlive Successful!"
                }
            };
            return Task.FromResult(keepAliveReply);
        }

        public override Task<StartLiveReply> StartLive(StartLiveRequest request, ServerCallContext context)
        {
            try
            {
                _eventSource?.FileLivePlayRequestEvent(request, context);
                var reqeustProcessResult = _sipServiceDirector.RealVideoReq(request.Gbid, new int[] { request.Port }, request.Ipaddr);

                reqeustProcessResult?.Wait(System.TimeSpan.FromSeconds(1));

                //get the response .
                var resReply = new StartLiveReply()
                {
                    Ipaddr = reqeustProcessResult.Result.Item1,
                    Port = reqeustProcessResult.Result.Item2,
                    Hdr = GetHeaderBySipHeader(reqeustProcessResult.Result.Item3),
                    Status = new MediaContract.Status()
                    {
                        Code = 200,
                        Msg = "Request Successful!"
                    }
                };
                //add Video Session Alive
                Dictionary<string, DateTime> _Dictionary = new Dictionary<string, DateTime>();
                _Dictionary.Add(request.Gbid + ',' + resReply.Hdr.Sessionid, DateTime.Now);
                _sipServiceDirector.VideoSessionAlive.Add(_Dictionary);

                return Task.FromResult(resReply);
            }
            catch (Exception ex)
            {
                logger.Error("Exception GRPC StartLive: " + ex.Message);
                var resReply = new StartLiveReply()
                {
                    Status = new MediaContract.Status()
                    {
                        Msg = ex.Message
                    }
                };
                return Task.FromResult(resReply);
            }
        }

        public override Task<StartPlaybackReply> StartPlayback(StartPlaybackRequest request, ServerCallContext context)
        {
            try
            {
                _eventSource?.FilePlaybackRequestEvent(request, context);
                var reqeustProcessResult = _sipServiceDirector.BackVideoReq(request.Gbid, new int[] { request.Port }, request.Ipaddr, Convert.ToUInt64(request.BeginTime), Convert.ToUInt64(request.EndTime));
                reqeustProcessResult?.Wait(System.TimeSpan.FromSeconds(1));

                //get the response .
                var resReply = new StartPlaybackReply()
                {
                    Ipaddr = reqeustProcessResult.Result.Item1,
                    Port = reqeustProcessResult.Result.Item2,
                    Hdr = GetHeaderBySipHeader(reqeustProcessResult.Result.Item3),
                    Status = new MediaContract.Status()
                    {
                        Code = 200,
                        Msg = "Request Successful!"
                    }
                };
                //add Video Session Alive
                Dictionary<string, DateTime> _Dictionary = new Dictionary<string, DateTime>();
                _Dictionary.Add(request.Gbid + ',' + resReply.Hdr.Sessionid, DateTime.Now);
                _sipServiceDirector.VideoSessionAlive.Add(_Dictionary);

                return Task.FromResult(resReply);
            }
            catch (Exception ex)
            {
                logger.Error("Exception GRPC StartPlayback: " + ex.Message);
                var resReply = new StartPlaybackReply()
                {
                    Status = new MediaContract.Status()
                    {
                        Msg = ex.Message
                    }
                };
                return Task.FromResult(resReply);
            }
        }

        //public override Task<VideoDownloadReply> VideoDownload(VideoDownloadRequest request, ServerCallContext context)
        //{
        //    try
        //    {
        //        _eventSource?.VideoDownloadRequestEvent(request, context);
        //        var reqeustProcessResult = _sipServiceDirector.VideoDownloadReq(Convert.ToDateTime(request.BeginTime), Convert.ToDateTime(request.EndTime), request.Gbid, new int[] { request.Port }, request.Ipaddr);

        //        reqeustProcessResult?.Wait(System.TimeSpan.FromSeconds(1));

        //        //get the response .
        //        var resReply = new VideoDownloadReply()
        //        {
        //            Ipaddr = reqeustProcessResult.Result.Item1,
        //            Port = reqeustProcessResult.Result.Item2,
        //            Hdr = GetHeaderBySipHeader(reqeustProcessResult.Result.Item3),
        //            Status = new MediaContract.Status()
        //            {
        //                Code = 200,
        //                Msg = "Request Successful!"
        //            }
        //        };
        //        //add Video Session Alive
        //        Dictionary<string, DateTime> _Dictionary = new Dictionary<string, DateTime>();
        //        _Dictionary.Add(request.Gbid + ',' + resReply.Hdr.Sessionid, DateTime.Now);
        //        _sipServiceDirector.VideoSessionAlive.Add(_Dictionary);
        //        return Task.FromResult(resReply);
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Error("Exception GRPC VideoDownloadReply: " + ex.Message);
        //        var resReply = new VideoDownloadReply()
        //        {
        //            Status = new MediaContract.Status()
        //            {
        //                Msg = ex.Message
        //            }
        //        };
        //        return Task.FromResult(resReply);
        //    }
        //}

        public override Task<StopReply> Stop(StopRequest request, ServerCallContext context)
        {
            try
            {
                var processResult = _sipServiceDirector.Stop(string.IsNullOrEmpty(request.Gbid) ? "42010000001180000184" : request.Gbid, request.Hdr.Sessionid);

                var reply = new StopReply()
                {
                    Status = new MediaContract.Status()
                    {
                        Code = 200,
                        Msg = "Stop Successful!"
                    }
                };
                return Task.FromResult(reply);
            }
            catch (Exception ex)
            {
                logger.Error("Exception GRPC StopVideo: " + ex.Message);
                var reply = new StopReply()
                {
                    Status = new MediaContract.Status()
                    {
                        Code = 400,
                        Msg = ex.Message
                    }
                };
                return Task.FromResult(reply);
            }
        }

        private Header GetHeaderBySipHeader(SIPSorcery.GB28181.SIP.SIPHeader sipHeader)
        {
            Header header = new Header();
            header.Sequence = sipHeader.CSeq;
            header.Sessionid = sipHeader.CallId;
            //header.Version = sipHeader.CSeq + sipHeader.CallId;
            return header;
        }

        /// <summary>
        /// BackVideoStop
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override Task<BackVideoStopReply> BackVideoStop(BackVideoStopRequest request, ServerCallContext context)
        {
            try
            {
                var processResult = _sipServiceDirector.BackVideoStopPlayingControlReq(string.IsNullOrEmpty(request.Gbid) ? "42010000001180000184" : request.Gbid, request.Hdr.Sessionid);

                var reply = new BackVideoStopReply()
                {
                    Status = new MediaContract.Status()
                    {
                        Code = 200,
                        Msg = "Stop Successful!"
                    }
                };
                return Task.FromResult(reply);
            }
            catch (Exception ex)
            {
                logger.Error("Exception GRPC BackVideoStop: " + ex.Message);
                var reply = new BackVideoStopReply()
                {
                    Status = new MediaContract.Status()
                    {
                        Code = 400,
                        Msg = ex.Message
                    }
                };
                return Task.FromResult(reply);
            }
        }

        /// <summary>
        /// BackVideoSpeed
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override Task<BackVideoSpeedReply> BackVideoSpeed(BackVideoSpeedRequest request, ServerCallContext context)
        {
            try
            {
                var processResult = _sipServiceDirector.BackVideoPlaySpeedControlReq(string.IsNullOrEmpty(request.Gbid) ? "42010000001180000184" : request.Gbid, request.Hdr.Sessionid, request.Scale);

                var reply = new BackVideoSpeedReply()
                {
                    Status = new MediaContract.Status()
                    {
                        Code = 200,
                        Msg = "Stop Successful!"
                    }
                };
                return Task.FromResult(reply);
            }
            catch (Exception ex)
            {
                logger.Error("Exception GRPC BackVideoSpeed: " + ex.Message);
                var reply = new BackVideoSpeedReply()
                {
                    Status = new MediaContract.Status()
                    {
                        Code = 400,
                        Msg = ex.Message
                    }
                };
                return Task.FromResult(reply);
            }
        }

        /// <summary>
        /// BackVideoPause
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override Task<BackVideoPauseReply> BackVideoPause(BackVideoPauseRequest request, ServerCallContext context)
        {
            try
            {
                var processResult = _sipServiceDirector.BackVideoPauseControlReq(string.IsNullOrEmpty(request.Gbid) ? "42010000001180000184" : request.Gbid, request.Hdr.Sessionid);

                var reply = new BackVideoPauseReply()
                {
                    Status = new MediaContract.Status()
                    {
                        Code = 200,
                        Msg = "Stop Successful!"
                    }
                };
                return Task.FromResult(reply);
            }
            catch (Exception ex)
            {
                logger.Error("Exception GRPC BackVideoStop: " + ex.Message);
                var reply = new BackVideoPauseReply()
                {
                    Status = new MediaContract.Status()
                    {
                        Code = 400,
                        Msg = ex.Message
                    }
                };
                return Task.FromResult(reply);
            }
        }

        /// <summary>
        /// BackVideoContinue
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override Task<BackVideoContinueReply> BackVideoContinue(BackVideoContinueRequest request, ServerCallContext context)
        {
            try
            {
                var processResult = _sipServiceDirector.BackVideoContinuePlayingControlReq(string.IsNullOrEmpty(request.Gbid) ? "42010000001180000184" : request.Gbid, request.Hdr.Sessionid);

                var reply = new BackVideoContinueReply()
                {
                    Status = new MediaContract.Status()
                    {
                        Code = 200,
                        Msg = "Stop Successful!"
                    }
                };
                return Task.FromResult(reply);
            }
            catch (Exception ex)
            {
                logger.Error("Exception GRPC BackVideoStop: " + ex.Message);
                var reply = new BackVideoContinueReply()
                {
                    Status = new MediaContract.Status()
                    {
                        Code = 400,
                        Msg = ex.Message
                    }
                };
                return Task.FromResult(reply);
            }
        }

        /// <summary>
        /// BackVideoPosition
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override Task<BackVideoPositionReply> BackVideoPosition(BackVideoPositionRequest request, ServerCallContext context)
        {
            try
            {
                var processResult = _sipServiceDirector.BackVideoPlayPositionControlReq(string.IsNullOrEmpty(request.Gbid) ? "42010000001180000184" : request.Gbid, request.Hdr.Sessionid, request.Range);

                var reply = new BackVideoPositionReply()
                {
                    Status = new MediaContract.Status()
                    {
                        Code = 200,
                        Msg = "Stop Successful!"
                    }
                };
                return Task.FromResult(reply);
            }
            catch (Exception ex)
            {
                logger.Error("Exception GRPC BackVideoStop: " + ex.Message);
                var reply = new BackVideoPositionReply()
                {
                    Status = new MediaContract.Status()
                    {
                        Code = 400,
                        Msg = ex.Message
                    }
                };
                return Task.FromResult(reply);
            }
        }
    }
}
