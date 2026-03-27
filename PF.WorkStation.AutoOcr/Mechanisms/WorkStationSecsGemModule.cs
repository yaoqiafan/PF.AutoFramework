using NPOI.POIFS.Crypt.Dsig.Facets;
using PF.Core.Entities.SecsGem.Message;
using PF.Core.Entities.SecsGem.Params.ValidateParam;
using PF.Core.Enums;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.SecsGem;
using PF.Core.Interfaces.SecsGem.Params;
using PF.Infrastructure.Mechanisms;
using PF.Workstation.AutoOcr.CostParam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.Mechanisms
{
    public class WorkStationSecsGemModule : BaseMechanism
    {

        private readonly ISecsGemManger _secsGemManger;



        private readonly Infrastructure.Logging.CategoryLogger _secsGemlog;
        public WorkStationSecsGemModule(IHardwareManagerService hardwareManagerService, IParamService paramService, ISecsGemManger secsGemManger, ILogService logger) : base(E_Mechanisms.SECSGEM通讯模组.ToString(), hardwareManagerService, paramService, logger)
        {
            _secsGemManger = secsGemManger;
            _secsGemlog = Infrastructure.Logging.CategoryLoggerFactory.SecsGem(logger);
        }

        protected override async Task<bool> InternalInitializeAsync(CancellationToken token)
        {
            if (_secsGemManger == null)
            {
                _secsGemlog.Error("SecsGem实例未创建，检查软件配置逻辑");
                return false;
            }
            if (await _secsGemManger.ConnectAsync())
            {
                _secsGemlog.Error("设备连接SecsGem服务端失败");
                return false;
            }
            _secsGemlog.Error("SecsGem连接成功");
            _secsGemManger.MessageReceived += SecsGemManger_MessageReceived;
            return true;
        }



        protected override async Task InternalStopAsync()
        {
            await _secsGemManger.DisconnectAsync();
        }



        #region SecsGem消息处理

        private void SecsGemManger_MessageReceived(object? sender, SecsMessageReceivedEventArgs e)
        {
            var message = e.Message;
            message.IsIncoming = true;
            _secsGemlog.Info($"收到SecsGem消息: {message}");
            string str = $"S{message.Stream}F{message.Function}";
            switch (str)
            {
                case "S1F1":
                    //处理S1F1消息
                    break;
                case "S1F3":
                    //处理S1F3消息
                    break;
                case "S1F13":
                    //处理S1F13消息
                    break;

                case "S1F15":
                    //处理S1F15消息
                    break;

                case "S1F17":
                    //处理S1F17消息
                    break;


                case "S2F41":
                    //处理S2F41消息
                    break;
                case "S5F1":
                    //处理S5F1消息
                    break;

                case "S6F11":
                    //处理S6F11消息
                    break;

                case "S7F1":
                    //处理S7F1消息
                    break;


                case "S7F3":
                    //处理S7F3消息
                    break;

                case "S7F5":
                    //处理S7F5消息
                    break;

                case "S7F17":
                    //处理S7F17消息
                    break;

                case "S7F19":
                    //处理S7F19消息
                    break;

                case "S10F3":
                    //处理S10F3消息
                    break;

                default:
                    _secsGemlog.Warn($"未处理的SecsGem消息: {message}");
                    break;

            }


        }


        /// <summary>
        /// S1F1消息处理示例，收到S1F1消息后回复S1F2消息，包含设备型号和软件版本信息
        /// 用于检查通信链路是否处于激活状态以及设备是否在线
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task HandleS1F1Message(SecsGemMessage message, CancellationToken token = default)
        {
            SecsGemMessage send = new SecsGemMessage()
            {
                Stream = 1,
                Function = 2,
                WBit = false,
                SystemBytes = message.SystemBytes,
                RootNode = new SecsGemNodeMessage()
                {
                    DataType = DataType.LIST,
                    Length = 2
                }
            };

            SecsGemNodeMessage node1 = new SecsGemNodeMessage(DataType.ASCII, "T-E243-0010");
            SecsGemNodeMessage node2 = new SecsGemNodeMessage(DataType.ASCII, "V1.0.1");
            send.RootNode.SubNode.Add(node1);
            send.RootNode.SubNode.Add(node2);
            send.IsIncoming = false;
            _secsGemlog.Info($"发送SecsGem消息: {send}");
            await _secsGemManger.SendMessageAsync(send);
        }



        /// <summary>
        /// 于获取设备特定变量值的标准消息
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task HandleS1F3Message(SecsGemMessage message, CancellationToken token = default)
        {
            /*******************/
            if (message.RootNode == null)
            {
                _secsGemlog.Warn($"S1F3消息RootNode为空，无法处理消息: {message}");
                return;
            }
            if (message.RootNode.SubNode == null)
            {
                _secsGemlog.Warn($"S1F3消息RootNode为空，无法处理消息: {message}");
                return;
            }
            if (message.RootNode.SubNode.Count == 0)
            {
                /*******回复所有有用信息***********/

                SecsGemMessage send = new SecsGemMessage()
                {
                    Stream = 1,
                    Function = 4,
                    WBit = false,
                    SystemBytes = message.SystemBytes,
                    RootNode = new SecsGemNodeMessage()
                    {
                        DataType = DataType.LIST,
                        Length = _secsGemManger?.ParamsManager?.GetParam<ValidateConfiguration>(ParamType.Validate)?.VIDS.Count ?? 0
                    }
                };
                foreach (var item in _secsGemManger?.ParamsManager?.GetParam<ValidateConfiguration>(ParamType.Validate)?.VIDS)
                {
                    send.RootNode.SubNode.Add(new SecsGemNodeMessage(item.Value.DataType, item.Value.Value));
                }
                send.IsIncoming = false;
                _secsGemlog.Info($"发送SecsGem消息: {send}");
                await _secsGemManger.SendMessageAsync(send);
            }

            else
            {
                /******回复指定消息*******/
                List<int> vids = new List<int>();
                for (int i = 0; i < message.RootNode.SubNode?.Count; i++)
                {
                    vids.Add(Convert.ToInt32(message.RootNode.SubNode[i].TypedValue));
                }

                SecsGemMessage send = new SecsGemMessage()
                {
                    Stream = 1,
                    Function = 4,
                    WBit = false,
                    SystemBytes = message.SystemBytes,
                    RootNode = new SecsGemNodeMessage()
                    {
                        DataType = DataType.LIST,
                        Length = _secsGemManger?.ParamsManager?.GetParam<ValidateConfiguration>(ParamType.Validate)?.VIDS.Count ?? 0
                    }
                };
                for (int i = 0; i < vids?.Count; i++)
                {
                    var VID = _secsGemManger?.ParamsManager?.GetParam<ValidateConfiguration>(ParamType.Validate).GetVID((uint)vids[i]);
                    if (VID != null)
                    {
                        send.RootNode.SubNode.Add(new SecsGemNodeMessage(VID.DataType, VID.Value));
                    }
                    else
                    {
                        send.RootNode.SubNode.Add(new SecsGemNodeMessage()
                        {
                            DataType = DataType.ASCII,
                            Length = 0
                        });
                    }
                }
                send.IsIncoming = false;
                _secsGemlog.Info($"发送SecsGem消息: {send}");
                await _secsGemManger.SendMessageAsync(send);
            }

        }



        private async Task HandleS1F13Message(SecsGemMessage message, CancellationToken token = default)
        {
            SecsGemMessage response = CreateS1F14Response (message, commack: 0, mdln: "T-E243-0010", softrev: "V1.0.1");
            response.IsIncoming = false;
            _secsGemlog.Info($"发送SecsGem消息: {response}");
            await _secsGemManger.SendMessageAsync(response);
        }

        /// <summary>
        /// 回复 S1F14
        /// </summary>
        /// <param name="s1f13Request">收到的 S1F13 原始消息</param>
        /// <param name="commack">0=接受, 1=拒绝, 2=已建立</param>
        private SecsGemMessage CreateS1F14Response(SecsGemMessage s1f13Request, byte commack, string mdln, string softrev)
        {
            // 1. 构建内部的 MDLN/SOFTREV 列表
            var infoSubList = new List<SecsGemNodeMessage>
            {
        new SecsGemNodeMessage(DataType.ASCII, mdln),
        new SecsGemNodeMessage(DataType.ASCII, softrev)
            };

            // 2. 构建根节点外层列表：[COMMACK, [MDLN, SOFTREV]]
            var rootSubNodes = new List<SecsGemNodeMessage> 
            {
        // 注意：根据你的构造函数，Binary 类型传入 byte[]
        new SecsGemNodeMessage(DataType.Binary, new byte[] { commack }),
        new SecsGemNodeMessage(DataType.LIST, infoSubList) 
            };
            return new SecsGemMessage
            {
                Stream = 1,
                Function = 14,
                WBit = false, // 响应消息不带 WBit
                SystemBytes = s1f13Request.SystemBytes, // 必须完全匹配
                RootNode = new SecsGemNodeMessage(DataType.LIST, rootSubNodes)
            };
        }

        #endregion SecsGem消息处理


    }
}
