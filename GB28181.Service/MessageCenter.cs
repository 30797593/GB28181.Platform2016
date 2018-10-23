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
            _registrarCore.RPCDmsRegisterReceived += _sipRegistrarCore_RPCDmsRegisterReceived;
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
                //logger.Debug("OnAlarmReceived started.");
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
            //logger.Debug("DeviceStatusReport started.");
            TimeSpan pre = new TimeSpan(DateTime.Now.Ticks);
            while (true)
            {
                //report status every 30 seconds 
                System.Threading.Thread.Sleep(30000);
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
                            System.Threading.Thread.Sleep(1);
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
                        //logger.Debug("QueryGBDeviceByGBIDs: " + obj.Heart.DeviceID);
                        _rep = client.QueryGBDeviceByGBIDs(req);
                        if (_rep.Devices != null && _rep.Devices.Count > 0)
                        {
                            stat.DeviceID = _rep.Devices[0].Guid;
                            stat.DeviceName = _rep.Devices[0].Name;
                        }
                        else
                        {
                            logger.Warn("QueryGBDeviceByGBIDsResponse .Devices.Count: " + _rep.Devices.Count);
                            continue;
                        }
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
                    logger.Error("DeviceStatusReport Exception: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// 设备注册事件
        /// </summary>
        /// <param name="sipTransaction"></param>
        /// <param name="sIPAccount"></param>
        private void _sipRegistrarCore_RPCDmsRegisterReceived(SIPTransaction sipTransaction, SIPSorcery.GB28181.SIP.App.SIPAccount sIPAccount)
        {
            try
            {
                Device _device = new Device();
                SIPRequest sipRequest = sipTransaction.TransactionRequest;
                _device.Guid = Guid.NewGuid().ToString();
                _device.IP = sipTransaction.TransactionRequest.RemoteSIPEndPoint.Address.ToString();//IPC
                _device.Name = "gb" + _device.IP;
                _device.LoginUser.Add(new LoginUser() { LoginName = sIPAccount.SIPUsername ?? "admin", LoginPwd = sIPAccount.SIPPassword ?? "123456" });
                _device.Port = Convert.ToUInt32(sipTransaction.TransactionRequest.RemoteSIPEndPoint.Port);//5060
                _device.GBID = sipTransaction.TransactionRequestFrom.URI.User;//42010000001180000184
                _device.PtzType = 0;
                _device.ProtocolType = 0;
                _device.ShapeType = ShapeType.Dome;
                //var options = new List<ChannelOption> { new ChannelOption(ChannelOptions.MaxMessageLength, int.MaxValue) };
                Channel channel = new Channel(EnvironmentVariables.DeviceManagementServiceAddress ?? "devicemanagementservice:8080", ChannelCredentials.Insecure);
                logger.Debug("Device Management Service Address: " + (EnvironmentVariables.DeviceManagementServiceAddress ?? "devicemanagementservice:8080"));
                var client = new Manage.Manage.ManageClient(channel);
                if (!_sipCoreMessageService.NodeMonitorService.ContainsKey(_device.GBID))
                {
                    AddDeviceRequest _AddDeviceRequest = new AddDeviceRequest();
                    _AddDeviceRequest.Device.Add(_device);
                    _AddDeviceRequest.LoginRoleId = "XXXX";
                    var reply = client.AddDevice(_AddDeviceRequest);
                    if (reply.Status == OP_RESULT_STATUS.OpSuccess)
                    {
                        logger.Debug("Device[" + sipTransaction.TransactionRequest.RemoteSIPEndPoint + "] have added registering DMS service.");
                        DeviceEditEvent(_device.GBID, "add");
                    }
                    else
                    {
                        logger.Error("_sipRegistrarCore_RPCDmsRegisterReceived.AddDevice: " + reply.Status.ToString());
                    }
                }
                else
                {
                    UpdateDeviceRequest _UpdateDeviceRequest = new UpdateDeviceRequest();
                    _UpdateDeviceRequest.DeviceItem.Add(_device);
                    _UpdateDeviceRequest.LoginRoleId = "XXXX";
                    var reply = client.UpdateDevice(_UpdateDeviceRequest);
                    if (reply.Status == OP_RESULT_STATUS.OpSuccess)
                    {
                        logger.Debug("Device[" + sipTransaction.TransactionRequest.RemoteSIPEndPoint + "] have updated registering DMS service.");
                        DeviceEditEvent(_device.GBID, "update");
                    }
                    else
                    {
                        logger.Error("_sipRegistrarCore_RPCDmsRegisterReceived.UpdateDevice: " + reply.Status.ToString());
                    }

                }
            }
            catch (Exception ex)
            {
                logger.Error("Device[" + sipTransaction.TransactionRequest.RemoteSIPEndPoint + "] register DMS failed: " + ex.Message);
            }
        }

        /// <summary>
        /// 设备编辑事件
        /// </summary>
        internal void DeviceEditEvent(string DeviceID, string edittype)
        {
            try
            {
                Event.Event evt = new Event.Event();
                evt.Detail = "DeviceEditEvent: " + edittype + " " + DeviceID;
                evt.OccurredTime = (UInt64)DateTime.Now.Ticks;

                string GBServerChannelAddress = EnvironmentVariables.DeviceManagementServiceAddress ?? "devicemanagementservice:8080";
                //logger.Debug("Device Management Service Address: " + GBServerChannelAddress);
                Channel channel = new Channel(GBServerChannelAddress, ChannelCredentials.Insecure);
                var client = new Manage.Manage.ManageClient(channel);
                QueryGBDeviceByGBIDsResponse _rep = new QueryGBDeviceByGBIDsResponse();
                QueryGBDeviceByGBIDsRequest req = new QueryGBDeviceByGBIDsRequest();
                //logger.Debug("OnStatusReceived Status: " + JsonConvert.SerializeObject(stat));
                req.GbIds.Add(DeviceID);
                //logger.Debug("QueryGBDeviceByGBIDs: " + obj.Heart.DeviceID);
                _rep = client.QueryGBDeviceByGBIDs(req);
                if (_rep.Devices != null && _rep.Devices.Count > 0)
                {
                    evt.DeviceID = _rep.Devices[0].Guid;
                    evt.DeviceName = _rep.Devices[0].Name;
                }
                logger.Debug("QueryGBDeviceByGBIDsRequest-EditEvent .Devices: " + _rep.Devices[0].ToString());

                Message message = new Message();
                Dictionary<string, string> dic = new Dictionary<string, string>();
                dic.Add("Content-Type", "application/octet-stream");
                message.Header = dic;
                message.Body = evt.ToByteArray();

                byte[] payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));
                string subject = Event.EventTopic.OriginalEventTopic.ToString();//"OriginalEventTopic"
                #region
                Options opts = ConnectionFactory.GetDefaultOptions();
                opts.Url = EnvironmentVariables.GBNatsChannelAddress ?? Defaults.Url;
                //logger.Debug("GB Nats Channel Address: " + opts.Url);
                using (IConnection c = new ConnectionFactory().CreateConnection(opts))
                {
                    c.Publish(subject, payload);
                    c.Flush();
                    logger.Debug("Device event created connection and published.");
                }
                #endregion
            }
            catch (Exception ex)
            {
                logger.Error("DeviceEditEvent Exception: " + ex.Message);
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
        internal void OnResponseCodeReceived(SIPResponseStatusCodesEnum status, string msg, SIPEndPoint remoteEP)
        {
            logger.Debug("OnResponseCodeReceived: " + msg);
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