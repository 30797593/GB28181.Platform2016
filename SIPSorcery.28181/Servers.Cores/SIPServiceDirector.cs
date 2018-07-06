﻿using SIPSorcery.GB28181.Net;
using SIPSorcery.GB28181.Servers.SIPMessage;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SIPSorcery.GB28181.Servers
{
    public class SIPServiceDirector : ISIPServiceDirector
    {

        private ISipMessageCore _sipCoreMessageService;
        public SIPServiceDirector(ISipMessageCore sipCoreMessageService)
        {
            _sipCoreMessageService = sipCoreMessageService;
        }
        public ISIPMonitorCore GetTargetMonitorService(string gbid)
        {
            if (_sipCoreMessageService == null)
            {
                throw new NullReferenceException("instance not exist!");
            }

            if (_sipCoreMessageService.NodeMonitorService.ContainsKey(gbid))
            {
                return _sipCoreMessageService.NodeMonitorService[gbid];
            }

            return null;

        }

        //make real Request
        async public Task<Tuple<string, int, ProtocolType>> MakeVideoRequest(string gbid, int[] mediaPort, string receiveIP)
        {
            var target = GetTargetMonitorService(gbid);

            if (target == null)
            {
                return null;
            }

            var taskResult = await Task.Factory.StartNew(() =>
           {

               var cSeq = target.RealVideoReq(mediaPort, receiveIP, true);

               var result = target.WaitRequestResult();

               return result;
           });

            var ipaddress = _sipCoreMessageService.GetReceiveIP(taskResult.Item2.Body);

            var port = _sipCoreMessageService.GetReceivePort(taskResult.Item2.Body, SDPMediaTypesEnum.video);

            return Tuple.Create(ipaddress, port, ProtocolType.Udp);
        }


        //stop real Request
        async public Task<Tuple<string, int, ProtocolType>> Stop(string gbid)
        {
            var target = GetTargetMonitorService(gbid);

            if (target == null)
            {
                return null;
            }

            var taskResult = await Task.Factory.StartNew(() =>
            {
                //stop
                target.ByeVideoReq();

                var result = target.WaitRequestResult();

                return result;
            });

            var ipaddress = _sipCoreMessageService.GetReceiveIP(taskResult.Item2.Body);

            var port = _sipCoreMessageService.GetReceivePort(taskResult.Item2.Body, SDPMediaTypesEnum.video);

            return Tuple.Create(ipaddress, port, ProtocolType.Udp);
        }
    }
}
