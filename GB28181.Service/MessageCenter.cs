using System;
using System.Collections.Generic;
using SIPSorcery.GB28181.Servers;
using SIPSorcery.GB28181.Servers.SIPMessage;
using SIPSorcery.GB28181.SIP;
using SIPSorcery.GB28181.Sys.XML;
using NATS.Client;
using System.Diagnostics;
using SIPSorcery.GB28181.Sys;
using System.Text;
using Logger4Net;
using Newtonsoft.Json;
using Google.Protobuf;
using SIPSorcery.GB28181.SIP.App;
using Manage;
using Grpc.Core;

namespace GB28181Service
{
    public class MessageCenter
    {
        private static ILog logger = AppState.logger;
        private DateTime _keepaliveTime;
        private Queue<HeartBeatEndPoint> _keepAliveQueue = new Queue<HeartBeatEndPoint>();
        private Queue<Catalog> _catalogQueue = new Queue<Catalog>();
        private List<string> _deviceAlarmSubscribed = new List<string>();
        private ISipMessageCore _sipCoreMessageService;
        private ISIPMonitorCore _sIPMonitorCore;
        private ISIPRegistrarCore _registrarCore;
        private Dictionary<string, HeartBeatEndPoint> _HeartBeatStatuses = new Dictionary<string, HeartBeatEndPoint>();
        public Dictionary<string, HeartBeatEndPoint> HeartBeatStatuses => _HeartBeatStatuses;
        private Dictionary<string, DeviceStatus> _DeviceStatuses = new Dictionary<string, DeviceStatus>();
        public Dictionary<string, DeviceStatus> DeviceStatuses => _DeviceStatuses;

        public MessageCenter(ISipMessageCore sipCoreMessageService, ISIPMonitorCore sIPMonitorCore, ISIPRegistrarCore sipRegistrarCore)
        {
            _sipCoreMessageService = sipCoreMessageService;
            _sIPMonitorCore = sIPMonitorCore;
            _registrarCore = sipRegistrarCore;
            _registrarCore.DeviceAlarmSubscribe += OnDeviceAlarmSubscribeReceived;
            _sipCoreMessageService.OnDeviceStatusReceived += _sipCoreMessageService_OnDeviceStatusReceived;
        }

        private void _sipCoreMessageService_OnDeviceStatusReceived(SIPEndPoint arg1, DeviceStatus arg2)
        {
            if (!DeviceStatuses.ContainsKey(arg2.DeviceID))
            {
                DeviceStatuses.Add(arg2.DeviceID, arg2);
            }
        }

        internal void OnKeepaliveReceived(SIPEndPoint remoteEP, KeepAlive keapalive, string devId)
        {
            _keepaliveTime = DateTime.Now;
            var hbPoint = new HeartBeatEndPoint()
            {
                RemoteEP = remoteEP,
                Heart = keapalive,
                KeepaliveTime = _keepaliveTime
            };
            _keepAliveQueue.Enqueue(hbPoint);

            HeartBeatStatuses.Remove(devId);
            HeartBeatStatuses.Add(devId, hbPoint);
        }

        internal void OnServiceChanged(string msg, ServiceStatus state)
        {
            SetSIPService(msg, state);
        }

        /// <summary>
        /// 设置sip服务状态
        /// </summary>
        /// <param name="state">sip状态</param>
        private void SetSIPService(string msg, ServiceStatus state)
        {
            logger.Debug("SIP Service Status: " + msg + "," + state);
        }

        ///// <summary>
        ///// 目录查询回调
        ///// </summary>
        ///// <param name="cata"></param>
        //public void OnCatalogReceived(Catalog cata)
        //{
        //    _catalogQueue.Enqueue(cata);
        //}

        //设备信息查询回调函数
        private void DeviceInfoReceived(SIPEndPoint remoteEP, DeviceInfo device)
        {
        }

        //设备状态查询回调函数
        private void DeviceStatusReceived(SIPEndPoint remoteEP, DeviceStatus device)
        {
        }

        ///// <summary>
        ///// 录像查询回调
        ///// </summary>
        ///// <param name="record"></param>
        //internal void OnRecordInfoReceived(RecordInfo record)
        //{
        //    SetRecord(record);
        //}

        //private void SetRecord(RecordInfo record)
        //{
        //    foreach (var item in record.RecordItems.Items)
        //    {
        //    }
        //}

        //internal void OnNotifyCatalogReceived(NotifyCatalog notify)
        //{
        //    if (notify.DeviceList == null)
        //    {
        //        return;
        //    }
        //    new Action(() =>
        //    {
        //        foreach (var item in notify.DeviceList.Items)
        //        {
        //        }
        //    }).BeginInvoke(null, null);
        //}

        /// <summary>
        /// 报警订阅
        /// </summary>
        /// <param name="sIPTransaction"></param>
        /// <param name="sIPAccount"></param>
        internal void OnDeviceAlarmSubscribeReceived(SIPTransaction sIPTransaction)
        {
            try
            {
                string keyDeviceAlarmSubscribe = sIPTransaction.RemoteEndPoint.ToString() + " - " + sIPTransaction.TransactionRequestFrom.URI.User;
                //if (!_deviceAlarmSubscribed.Contains(keyDeviceAlarmSubscribe))
                //{
                    //_sIPMonitorCore.DeviceControlResetAlarm(sIPTransaction.RemoteEndPoint, sIPTransaction.TransactionRequestFrom.URI.User);
                    //logger.Debug("Device Alarm Reset: " + keyDeviceAlarmSubscribe);
                    _sIPMonitorCore.DeviceAlarmSubscribe(sIPTransaction.RemoteEndPoint, sIPTransaction.TransactionRequestFrom.URI.User);
                    logger.Debug("Device Alarm Subscribe: " + keyDeviceAlarmSubscribe);
                    //_deviceAlarmSubscribed.Add(keyDeviceAlarmSubscribe);
                //}
            }
            catch (Exception ex)
            {
                logger.Error("OnDeviceAlarmSubscribeReceived: " + ex.Message);
            }
        }
        /// <summary>
        /// 设备报警
        /// </summary>
        /// <param name="alarm"></param>
        internal void OnAlarmReceived(Alarm alarm)
        {
            try
            {
                Event.Alarm alm = new Event.Alarm();
                alm.AlarmType = alm.AlarmType = Event.Alarm.Types.AlarmType.CrossingLine ;
                //switch (alarm.AlarmMethod)
                //{
                //    case "1":
                //        break;
                //    case "2":
                //        alm.AlarmType = Event.Alarm.Types.AlarmType.AlarmOutput;
                //        break;
                //}
                alm.Detail = alarm.AlarmDescription ?? string.Empty;
                //alm.DeviceID = alarm.DeviceID;//dms deviceid
                //alm.DeviceName = alarm.DeviceID;//dms name
                string GBServerChannelAddress = EnvironmentVariables.DeviceManagementServiceAddress ?? "devicemanagementservice:8080";
                logger.Debug("Device Management Service Address: " + GBServerChannelAddress);
                Channel channel = new Channel(GBServerChannelAddress, ChannelCredentials.Insecure);
                var client = new Manage.Manage.ManageClient(channel);
                QueryGBDeviceByGBIDsResponse _rep = new QueryGBDeviceByGBIDsResponse();
                QueryGBDeviceByGBIDsRequest req = new QueryGBDeviceByGBIDsRequest();
                logger.Debug("OnAlarmReceived Alarm: " + JsonConvert.SerializeObject(alarm));
                req.GbIds.Add(alarm.DeviceID);
                _rep = client.QueryGBDeviceByGBIDs(req);
                if (_rep.Devices != null && _rep.Devices.Count > 0)
                {
                    alm.DeviceID = _rep.Devices[0].GBID;
                    alm.DeviceName = _rep.Devices[0].Name;
                }
                else
                {
                    logger.Debug("QueryGBDeviceByGBIDsResponse .Devices.Count: " + _rep.Devices.Count);
                }
                logger.Debug("QueryGBDeviceByGBIDsRequest-Alarm .Devices: " + _rep.Devices[0].ToString());
                UInt64 timeStamp = (UInt64)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalSeconds;
                alm.EndTime = timeStamp;
                alm.StartTime = timeStamp;

                Message message = new Message();
                Dictionary<string, string> dic = new Dictionary<string, string>();
                dic.Add("Content-Type", "application/octet-stream");
                message.Header = dic;
                message.Body = alm.ToByteArray();

                byte[] payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));
                string subject = Event.AlarmTopic.OriginalAlarmTopic.ToString();//"OriginalAlarmTopic"
                #region
                Options opts = ConnectionFactory.GetDefaultOptions();
                opts.Url = EnvironmentVariables.GBNatsChannelAddress ?? Defaults.Url;
                logger.Debug("GB Nats Channel Address: " + opts.Url);
                using (IConnection c = new ConnectionFactory().CreateConnection(opts))
                {
                    c.Publish(subject, payload);
                    c.Flush();
                    logger.Debug("Device alarm created connection and published.");
                }
                #endregion

                new Action(() =>
                {
                    logger.Debug("OnAlarmReceived AlarmResponse: " + alm.ToString());

                    _sipCoreMessageService.NodeMonitorService[alarm.DeviceID].AlarmResponse(alarm);
                }).Invoke();
            }
            catch (Exception ex)
            {
                logger.Error("OnAlarmReceived Exception: " + ex.Message);
            }
        }

        /// <summary>
        /// 设备状态上报
        /// </summary>
        internal void DeviceStatusReport()
        {
            logger.Debug("DeviceStatusReport started.");
            TimeSpan pre = new TimeSpan(DateTime.Now.Ticks);
            while (true)
            {
                TimeSpan suf = new TimeSpan(DateTime.Now.Ticks);
                //report status every 30 seconds 
                if (suf.Subtract(pre).Duration().Seconds > 30)
                {
                    try
                    {
                        foreach (HeartBeatEndPoint obj in HeartBeatStatuses.Values)
                        {
                            Event.Status stat = new Event.Status();
                            stat.Status_ = false;
                            stat.OccurredTime = (UInt64)DateTime.Now.Ticks;
                            #region waiting DeviceStatuses add in for 500 Milliseconds
                            _sipCoreMessageService.DeviceStateQuery(obj.Heart.DeviceID);
                            TimeSpan t1 = new TimeSpan(DateTime.Now.Ticks);
                            while (true)
                            {
                                TimeSpan t2 = new TimeSpan(DateTime.Now.Ticks);
                                if (DeviceStatuses.ContainsKey(obj.Heart.DeviceID))
                                {
                                    //on line
                                    stat.Status_ = DeviceStatuses[obj.Heart.DeviceID].Status.Equals("ON") ? true : false;
                                    logger.Debug("Device status of [" + obj.Heart.DeviceID + "]: " + DeviceStatuses[obj.Heart.DeviceID].Status);
                                    DeviceStatuses.Remove(obj.Heart.DeviceID);
                                    break;
                                }
                                else if (t2.Subtract(t1).Duration().Milliseconds > 500)
                                {
                                    //off line
                                    logger.Debug("Device status of [" + obj.Heart.DeviceID + "]: OFF");
                                    break;
                                }
                            }
                            #endregion
                            string GBServerChannelAddress = EnvironmentVariables.DeviceManagementServiceAddress ?? "devicemanagementservice:8080";
                            //logger.Debug("Device Management Service Address: " + GBServerChannelAddress);
                            Channel channel = new Channel(GBServerChannelAddress, ChannelCredentials.Insecure);
                            var client = new Manage.Manage.ManageClient(channel);
                            QueryGBDeviceByGBIDsResponse _rep = new QueryGBDeviceByGBIDsResponse();
                            QueryGBDeviceByGBIDsRequest req = new QueryGBDeviceByGBIDsRequest();
                            //logger.Debug("OnStatusReceived Status: " + JsonConvert.SerializeObject(stat));
                            req.GbIds.Add(obj.Heart.DeviceID);
                            _rep = client.QueryGBDeviceByGBIDs(req);
                            if (_rep.Devices != null && _rep.Devices.Count > 0)
                            {
                                stat.DeviceID = _rep.Devices[0].GBID;
                                stat.DeviceName = _rep.Devices[0].Name;
                            }
                            //else
                            //{
                            //    logger.Debug("QueryGBDeviceByGBIDsResponse .Devices.Count: " + _rep.Devices.Count);
                            //}
                            logger.Debug("QueryGBDeviceByGBIDsRequest-Status .Devices: " + _rep.Devices[0].ToString());

                            Message message = new Message();
                            Dictionary<string, string> dic = new Dictionary<string, string>();
                            dic.Add("Content-Type", "application/octet-stream");
                            message.Header = dic;
                            message.Body = stat.ToByteArray();

                            byte[] payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));
                            string subject = Event.StatusTopic.OriginalStatusTopic.ToString();//"OriginalStatusTopic"
                            #region
                            Options opts = ConnectionFactory.GetDefaultOptions();
                            opts.Url = EnvironmentVariables.GBNatsChannelAddress ?? Defaults.Url;
                            //logger.Debug("GB Nats Channel Address: " + opts.Url);
                            using (IConnection c = new ConnectionFactory().CreateConnection(opts))
                            {
                                c.Publish(subject, payload);
                                c.Flush();
                                logger.Debug("Device status created connection and published.");
                            }
                            #endregion
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Warn("Device counts from HeartBeatStatuses queue: " + HeartBeatStatuses.Count);
                        logger.Error("DeviceStatusReport Exception: " + ex.Message);
                    }
                    //recover
                    pre = new TimeSpan(DateTime.Now.Ticks);
                }
            }
        }

        //internal void OnDeviceStatusReceived(SIPEndPoint remoteEP, DeviceStatus device)
        //{
        //    var msg = "DeviceID:" + device.DeviceID +
        //         "\r\nResult:" + device.Result +
        //         "\r\nOnline:" + device.Online +
        //         "\r\nState:" + device.Status;
        //    new Action(() =>
        //    {
        //    }).Invoke();
        //}

        internal void OnDeviceInfoReceived(SIPEndPoint arg1, DeviceInfo arg2)
        {
            throw new NotImplementedException();
        }

        internal void OnMediaStatusReceived(SIPEndPoint arg1, MediaStatus arg2)
        {
            throw new NotImplementedException();
        }

        internal void OnPresetQueryReceived(SIPEndPoint arg1, PresetInfo arg2)
        {
            throw new NotImplementedException();
        }

        internal void OnDeviceConfigDownloadReceived(SIPEndPoint arg1, DeviceConfigDownload arg2)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 心跳
    /// </summary>
    public class HeartBeatEndPoint
    {
        /// <summary>
        /// 远程终结点
        /// </summary>
        public SIPEndPoint RemoteEP { get; set; }

        /// <summary>
        /// 心跳周期
        /// </summary>
        public KeepAlive Heart { get; set; }

        public DateTime KeepaliveTime { get; set; }
    }

    public class Message
    {
        public Dictionary<string, string> Header { get; set; }
        public byte[] Body { get; set; }
    }
}