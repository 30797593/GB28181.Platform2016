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
        public override Task<StartHistoryReply> StartHistory(StartHistoryRequest request, ServerCallContext context)
        {
            try
            {
                _eventSource?.FileHistoryPlayRequestEvent(request, context);
                var reqeustProcessResult = _sipServiceDirector.BackVideoReq(Convert.ToDateTime(request.BeginTime), Convert.ToDateTime(request.EndTime), request.Gbid, new int[] { request.Port }, request.Ipaddr);

                reqeustProcessResult?.Wait(System.TimeSpan.FromSeconds(1));

                //get the response .
                var resReply = new StartHistoryReply()
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
                logger.Error("Exception GRPC StartHistory: " + ex.Message);
                var resReply = new StartHistoryReply()
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
            if (request.IsDownload)
            {
                _eventSource?.FilePlaybackRequestEvent(request, context);
            }

            return base.StartPlayback(request, context);
        }

        public override Task<VideoDownloadReply> VideoDownload(VideoDownloadRequest request, ServerCallContext context)
        {
            try
            {
                _eventSource?.VideoDownloadRequestEvent(request, context);
                var reqeustProcessResult = _sipServiceDirector.VideoDownloadReq(Convert.ToDateTime(request.BeginTime), Convert.ToDateTime(request.EndTime), request.Gbid, new int[] { request.Port }, request.Ipaddr);

                reqeustProcessResult?.Wait(System.TimeSpan.FromSeconds(1));

                //get the response .
                var resReply = new VideoDownloadReply()
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
                logger.Error("Exception GRPC VideoDownloadReply: " + ex.Message);
                var resReply = new VideoDownloadReply()
                {
                    Status = new MediaContract.Status()
                    {
                        Msg = ex.Message
                    }
                };
                return Task.FromResult(resReply);
            }
        }

        public override Task<StopReply> Stop(StopRequest request, ServerCallContext context)
        {
            try
            {
                var stopProcessResult = _sipServiceDirector.Stop(string.IsNullOrEmpty(request.Gbid) ? "42010000001180000184" : request.Gbid, request.Hdr.Sessionid);
                
                var stopReply = new StopReply()
                {
                    Status = new MediaContract.Status()
                    {
                        Code = 200,
                        Msg = "Stop Successful!"
                    }
                };
                return Task.FromResult(stopReply);
            }
            catch (Exception ex)
            {
                logger.Error("Exception GRPC StopVideo: " + ex.Message);
                var stopReply = new StopReply()
                {
                    Status = new MediaContract.Status()
                    {
                        Msg = ex.Message
                    }
                };
                return Task.FromResult(stopReply);
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
    }
}
