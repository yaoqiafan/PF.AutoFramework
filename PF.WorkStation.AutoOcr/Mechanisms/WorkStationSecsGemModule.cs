using NPOI.POIFS.Crypt.Dsig.Facets;
using NPOI.SS.Formula.Functions;
using Org.BouncyCastle.Pkcs;
using PF.Core.Entities.SecsGem.Message;
using PF.Core.Entities.SecsGem.Params.ValidateParam;
using PF.Core.Enums;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Recipe;
using PF.Core.Interfaces.SecsGem;
using PF.Core.Interfaces.SecsGem.Params;
using PF.Infrastructure.Mechanisms;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.CostParam;
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


        private readonly IRecipeService<OCRRecipeParam> _recipeService;


        private WorkStationDataModule _workStationDataModule;

        private readonly Infrastructure.Logging.CategoryLogger _secsGemlog;

        private readonly IContainerProvider _containerProvider;
        public WorkStationSecsGemModule(IHardwareManagerService hardwareManagerService, IParamService paramService, ISecsGemManger secsGemManger, PF.Core.Interfaces.SecsGem.Params.IParams secsGemParam, IRecipeService<OCRRecipeParam> recipeService, IContainerProvider containerProvider, ILogService logger) : base(E_Mechanisms.SECSGEM通讯模组.ToString(), hardwareManagerService, paramService, logger)
        {
            _secsGemManger = secsGemManger;
            _secsGemlog = Infrastructure.Logging.CategoryLoggerFactory.SecsGem(logger);
            _recipeService = recipeService;
            _containerProvider = containerProvider;

        }

        protected override async Task<bool> InternalInitializeAsync(CancellationToken token=default)
        {
            if (_secsGemManger == null)
            {
                _secsGemlog.Error("SecsGem实例未创建，检查软件配置逻辑");
                return false;
            }
            if (!await _secsGemManger.InitializeAsync())
            {
                _secsGemlog.Error("设备连接SecsGem服务端失败");
                return false;
            }
            _workStationDataModule = _containerProvider.Resolve<IMechanism>(nameof(WorkStationDataModule)) as WorkStationDataModule;
            _secsGemlog.Info("SecsGem连接成功");
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
            if (message.Function % 2==0)
            {
                return;
            }

            switch (str)
            {
                case "S1F1":
                    //处理S1F1消息
                    HandleS1F1Message(message).ConfigureAwait(false);
                    break;
                case "S1F3":
                    //处理S1F3消息
                    HandleS1F3Message(message).ConfigureAwait(false);
                    break;
                case "S1F13":
                    HandleS1F13Message(message).ConfigureAwait(false);
                    //处理S1F13消息
                    break;

                case "S1F15":
                    HandleS1F15Message(message).ConfigureAwait(false);
                    //处理S1F15消息
                    break;

                case "S1F17":
                    HandleS1F17Message(message).ConfigureAwait(false);
                    //处理S1F17消息
                    break;


                case "S2F41":
                    //处理S2F41消息
                    HandleS2F41Message(message).ConfigureAwait(false);
                    break;
                case "S5F1":
                    //处理S5F1消息
                    _secsGemlog.Warn($"无法处理的SecsGem消息: {message}，S5F1为设备上抛主机指令，不能主机发送设备");
                    break;

                case "S6F11":
                    //处理S6F11消息
                    _secsGemlog.Warn($"无法处理的SecsGem消息: {message}，S6F11为设备上抛主机指令，不能主机发送设备");
                    break;

                case "S7F1":
                    //处理S7F1消息
                    HandleS7F1Message(message).ConfigureAwait(false);
                    break;


                case "S7F3":
                    //处理S7F3消息
                    HandleS7F3Message(message).ConfigureAwait(false);
                    break;

                case "S7F5":
                    //处理S7F5消息
                    HandleS7F5Message(message).ConfigureAwait(false);
                    break;

                case "S7F17":
                    //处理S7F17消息
                    HandleS7F17Message(message).ConfigureAwait(false);
                    break;

                case "S7F19":
                    //处理S7F19消息
                    HandleS7F19Message(message).ConfigureAwait(false);
                    break;

                case "S10F3":
                    //处理S10F3消息
                    HandleS10F3Message(message).ConfigureAwait(false);
                    break;

                default:
                    _secsGemlog.Warn($"未处理的SecsGem消息: {message}");
                    break;

            }


        }

        #region S1F1--> S1F2
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




        #endregion S1F1--> S1F2



        #region S1F3--->S1F4

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


                List<(DataType type, object val)> statusValues = new List<(DataType type, object val)>();
                foreach (var item in _secsGemManger?.ParamsManager?.GetParam<ValidateConfiguration>(ParamType.Validate)?.VIDS)
                {
                    statusValues.Add((item.Value.DataType, item.Value.Value));
                }
                var s1f4 = CreateS1F4Response(message, statusValues);

                s1f4.IsIncoming = false;
                _secsGemlog.Info($"发送SecsGem消息: {s1f4}");
                await _secsGemManger.SendMessageAsync(s1f4);
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
                List<(DataType type, object val)> statusValues = new List<(DataType type, object val)>();
                for (int i = 0; i < vids?.Count; i++)
                {
                    var VID = _secsGemManger?.ParamsManager?.GetParam<ValidateConfiguration>(ParamType.Validate).GetVID((uint)vids[i]);
                    if (VID != null)
                    {
                        statusValues.Add((VID.DataType, VID.Value));
                    }
                    else
                    {
                        statusValues.Add((DataType.LIST, null));
                    }
                }
                var s1f4 = CreateS1F4Response(message, statusValues);

                s1f4.IsIncoming = false;
                _secsGemlog.Info($"发送SecsGem消息: {s1f4}");
                await _secsGemManger.SendMessageAsync(s1f4);
            }

        }
        /// <summary>
        /// 回复 S1F4
        /// </summary>
        /// <param name="s1f3Request">收到的 S1F3 原始消息</param>
        /// <param name="statusValues">对应的变量值列表（可以是不同类型的 object）</param>
        private SecsGemMessage CreateS1F4Response(SecsGemMessage s1f3Request, List<(DataType type, object val)> statusValues)
        {
            List<SecsGemNodeMessage> subNodes = new List<SecsGemNodeMessage>();

            foreach (var item in statusValues)
            {
                if (item.type == DataType.ASCII)
                {
                    // 根据实际数据类型构建节点（如 ASCII 字符串、U4 数字等）
                    subNodes.Add(new SecsGemNodeMessage() { DataType = item.type, Length = 0 });
                }
                else
                {
                    // 根据实际数据类型构建节点（如 ASCII 字符串、U4 数字等）
                    subNodes.Add(new SecsGemNodeMessage(item.type, item.val));
                }
            }

            return new SecsGemMessage
            {
                Stream = 1,
                Function = 4,
                WBit = false, // 回复消息不需要 WBit
                SystemBytes = s1f3Request.SystemBytes, // 必须匹配请求的系统字节
                RootNode = new SecsGemNodeMessage(DataType.LIST, subNodes)
            };
        }
        #endregion S1F3--->S1F4





        #region S1F13--S1F14
        /// <summary>
        /// 正式建立通信连接的消息
        /// </summary>
        /// <param name="message"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task HandleS1F13Message(SecsGemMessage message, CancellationToken token = default)
        {
            SecsGemMessage response = CreateS1F14Response(message, commack: 0, mdln: "T-E243-0010", softrev: "V1.0.1");
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



        #endregion S1F13--S1F14


        #region S1F15--->S1F16 (请求离线)
        /// <summary>
        /// 用于控制设备的 控制状态 (Control State)--设备离线。
        /// </summary>
        /// <param name="message"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task HandleS1F15Message(SecsGemMessage message, CancellationToken token = default)
        {
            SecsGemMessage response = CreateS1F16Response(message, oflack: 0x00);
            response.IsIncoming = false;
            _secsGemlog.Info($"发送SecsGem消息: {response}");
            await _secsGemManger.SendMessageAsync(response);
        }

        /// <summary>
        /// 创建S1F16回复消息
        /// </summary>
        /// <param name="s1f15Request"></param>
        /// <param name="oflack"></param>
        /// <returns></returns>
        private SecsGemMessage CreateS1F16Response(SecsGemMessage s1f15Request, byte oflack = 0x00)
        {
            return new SecsGemMessage
            {
                Stream = 1,
                Function = 16,
                WBit = false,
                SystemBytes = s1f15Request.SystemBytes, // 必须匹配请求
                                                        // 触发您的 case DataType.Binary 分支
                RootNode = new SecsGemNodeMessage(DataType.Binary, new byte[] { oflack })
            };
        }



        #endregion  S1F15--->S1F16



        #region S1F17-->S1F18(设备在线)

        /// <summary>
        /// 用于控制设备的 控制状态 (Control State)--设备上线。
        /// </summary>
        /// <param name="message"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task HandleS1F17Message(SecsGemMessage message, CancellationToken token = default)
        {
            SecsGemMessage response = CreateS1F18Response(message, onlack: 0x00);
            response.IsIncoming = false;
            _secsGemlog.Info($"发送SecsGem消息: {response}");
            await _secsGemManger.SendMessageAsync(response);
        }

        /// <summary>
        /// 创建S1F18回复消息 0x00: ON-LINE Accepted (接受上线)  0x01: Not Allowed(拒绝上线，通常因为设备有本地锁定或处于维护模式)  0x02: Equipment Already ON-LINE(设备已经在线)
        /// </summary>
        /// <param name="s1f17Request"></param>
        /// <param name="onlack">0x00: ON-LINE Accepted (接受上线)。
        ///0x01: Not Allowed(拒绝上线，通常因为设备有本地锁定或处于维护模式)。
        ///0x02: Equipment Already ON-LINE(设备已经在线)</param>
        /// <returns></returns>
        private SecsGemMessage CreateS1F18Response(SecsGemMessage s1f17Request, byte onlack = 0x00)
        {
            // 触发您的 case DataType.Binary 分支
            var rootNode = new SecsGemNodeMessage(DataType.Binary, new byte[] { onlack });

            return new SecsGemMessage
            {
                Stream = 1,
                Function = 18,
                WBit = false,
                SystemBytes = s1f17Request.SystemBytes, // 必须匹配请求
                RootNode = rootNode
            };
        }

        #endregion S1F17-->S1F18(设备在线)



        #region S2F41---->S2F42（待完善消息处理）

        /// <summary>
        /// 设备发送指定及参数
        /// </summary>
        /// <param name="message"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task HandleS2F41Message(SecsGemMessage message, CancellationToken token = default)
        {
            SecsGemMessage response = CreateS2F42Response(message, hcack: 0x00);
            response.IsIncoming = false;
            _secsGemlog.Info($"发送SecsGem消息: {response}");
            await _secsGemManger.SendMessageAsync(response);
        }

        /// <summary>
        /// 创建S2F42回复命令
        /// </summary>
        /// <param name="s2f41Request"></param>
        /// <param name="hcack"> /* HCACK: 0=接受, 1=指令不存在, 2=现在无法执行, 3=参数非法, 4=稍后完成 */</param>
        /// <returns></returns>
        private SecsGemMessage CreateS2F42Response(SecsGemMessage s2f41Request, byte hcack)
        {
            var rootNodes = new List<SecsGemNodeMessage>
            {
                new SecsGemNodeMessage(DataType.Binary, new byte[] { hcack }),
                new SecsGemNodeMessage(DataType.LIST, new List<SecsGemNodeMessage>()) // 通常为空参数列表
            };
            return new SecsGemMessage
            {
                Stream = 2,
                Function = 42,
                WBit = false,
                SystemBytes = s2f41Request.SystemBytes,
                RootNode = new SecsGemNodeMessage(DataType.LIST, rootNodes)
            };
        }

        #endregion S2F41---->S2F42


      #region  S7F1-->S7F2 （正式下发配方数据之前的一个“预问询”环节。它的逻辑是：主机问设备“我有个配方要传给你，你现在有地方存吗？”，设备回答“可以传”或“现在不行）


        /// <summary>
        /// 设备下发配方询问
        /// </summary>
        /// <param name="message"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task HandleS7F1Message(SecsGemMessage message, CancellationToken token = default)
        {
            SecsGemMessage response = CreateS7F2Response(message, ppgnt: 0x00);
            response.IsIncoming = false;
            _secsGemlog.Info($"发送SecsGem消息: {response}");
            await _secsGemManger.SendMessageAsync(response);
        }


        /// <summary>
        /// 创建S7F2回复消息体
        /// </summary>
        /// <param name="s7f1Request"></param>
        /// <param name="ppgnt">PPGNT: 0=OK, 1=忙, 2=内存不足, 3=已存在, 4=非法 PPID</param>
        /// <returns></returns>
        private SecsGemMessage CreateS7F2Response(SecsGemMessage s7f1Request, byte ppgnt = 0X00)
        {
            // 触发您的 case DataType.Binary
            var rootNode = new SecsGemNodeMessage(DataType.Binary, new byte[] { ppgnt });
            return new SecsGemMessage
            {
                Stream = 7,
                Function = 2,
                WBit = false,
                SystemBytes = s7f1Request.SystemBytes, // 必须匹配请求
                RootNode = rootNode
            };
        }


        #endregion S7F1-->S7F2
        #region S7F3-->S7F4  (通过 S7F3 正式将配方的内容（二进制或格式化数据）发送给设备)

        /// <summary>
        /// 设备下发配方
        /// </summary>
        /// <param name="message"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task HandleS7F3Message(SecsGemMessage message, CancellationToken token = default)
        {
            SecsGemMessage response = CreateS7F4Response(message, ackc7: 0x00);
            response.IsIncoming = false;
            _secsGemlog.Info($"发送SecsGem消息: {response}");
            await _secsGemManger.SendMessageAsync(response);
        }

        /// <summary>
        /// 创建 S7F4 回复 (Equipment 确认接收)
        /// </summary>
        /// <param name="s7f3Request"></param>
        /// <param name="ackc7">ACKC7: 0=接受, 1=不接受, 2=内存不足, 3=非法 PPID, 4=非法格式</param>
        /// <returns></returns>
        private SecsGemMessage CreateS7F4Response(SecsGemMessage s7f3Request, byte ackc7)
        {
            // 触发您的 case DataType.Binary 分支
            var rootNode = new SecsGemNodeMessage(DataType.Binary, new byte[] { ackc7 });

            return new SecsGemMessage
            {
                Stream = 7,
                Function = 4,
                WBit = false,
                SystemBytes = s7f3Request.SystemBytes, // 必须匹配请求
                RootNode = rootNode
            };
        }

        #endregion S7F3-->S7F4  (通过 S7F3 正式将配方的内容（二进制或格式化数据）发送给设备)



        #region S7F5-->S7F6  (主机请求配方上传)

        /// <summary>
        /// 上传配方请求处理，主机通过 S7F5 请求设备上传指定 PPID 的配方数据，设备回复 S7F6 消息，如果配方存在则包含配方内容，如果配方不存在则回复一个空的列表或特定错误码。
        /// </summary>
        /// <param name="message"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task HandleS7F5Message(SecsGemMessage message, CancellationToken token = default)
        {
            // 1. 获取请求的配方名 (假设 RootNode 是 ASCII)
            string requestedPpid = message.RootNode.TypedValue.ToString();
            var recipe = await _recipeService.RecipeParam(requestedPpid);
            if (recipe == null)
            {
                var response = CreateS7F6EmptyResponse(message);
                response.IsIncoming = false;
                _secsGemlog.Info($"发送SecsGem消息: {response}");
                await _secsGemManger.SendMessageAsync(response);
            }
            else
            {
                string recipestr = System.Text.Json.JsonSerializer.Serialize(recipe);
                byte[] recipedata = Encoding.UTF8.GetBytes(recipestr);
                var response = CreateS7F6Response(message, requestedPpid, recipedata);
                response.IsIncoming = false;
                _secsGemlog.Info($"发送SecsGem消息: {response}");
                await _secsGemManger.SendMessageAsync(response);
            }
        }




        private SecsGemMessage CreateS7F6Response(SecsGemMessage s7f5Request, string ppid, byte[] ppBody)
        {
            var rootNodes = new List<SecsGemNodeMessage>
            {
                new SecsGemNodeMessage(DataType.ASCII, ppid),
                new SecsGemNodeMessage(DataType.Binary, ppBody) // 触发你的 case DataType.Binary
            };
            return new SecsGemMessage
            {
                Stream = 7,
                Function = 6,
                WBit = false,
                SystemBytes = s7f5Request.SystemBytes, // 必须匹配请求
                RootNode = new SecsGemNodeMessage(DataType.LIST, rootNodes)
            };

        }

        // 配方不存在时的回复
        private SecsGemMessage CreateS7F6EmptyResponse(SecsGemMessage s7f5Request)
        {
            return new SecsGemMessage
            {
                Stream = 7,
                Function = 6,
                WBit = false,
                SystemBytes = s7f5Request.SystemBytes,
                RootNode = new SecsGemNodeMessage(DataType.LIST, 0) // 触发你的 case DataType.LIST 中的 int 长度逻辑
            };
        }

        #endregion S7F5-->S7F6  (主机请求配方上传)




        #region S7F17-->S7F18 (设备请求删除配方)

        /// <summary>
        /// 当接收到 S7F17 (Process Program Delete Request) 时调用(删除指定配方)
        /// </summary>
        /// <param name="s7f17Request"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task HandleS7F17Message(SecsGemMessage s7f17Request, CancellationToken token = default)
        {
            // 1. 解析数据：S7F17 的 RootNode 是一个包含多个 ASCII 节点的 LIST
            var ppidNodes = s7f17Request.RootNode.SubNode;

            // 如果列表为空，某些标准定义为“删除所有配方”，此处需根据你的协议文档处理
            if (ppidNodes == null || ppidNodes.Count == 0)
            {
                // 示例：拒绝删除所有请求
                ReplyS7F18(s7f17Request, 0x01);
                return;
            }

            byte ackc7 = 0; // 0=成功, 1=拒绝, 3=至少一个没找到

            foreach (var node in ppidNodes)
            {
                string ppid = node.TypedValue?.ToString();
                if (string.IsNullOrEmpty(ppid)) continue;

                // 2. 安全检查：正在运行的配方禁止删除
                //if (IsRecipeCurrentlyRunning(ppid))
                //{
                //    ackc7 = 1; // 只要有一个正在跑，就整体拒绝（或按文档部分删除）
                //    break;
                //}

                // 3. 执行物理删除
                var recipe = await _recipeService.RecipeParam(ppid);
                if (recipe == null)
                {
                    ackc7 = 3; // 至少一个配方没找到

                }
                else
                {
                    bool deleted = await _recipeService.RecipeDeleteAsync(ppid);
                    if (!deleted)
                    {
                        ackc7 = 1; // 删除失败，拒绝请求
                        break;
                    }

                }

                // 4. 回复主机
                ReplyS7F18(s7f17Request, ackc7);

                // 5. 如果删除成功，触发 GEM 事件上报 (S6F11)
                if (ackc7 == 0)
                {

                }
            }

        }


        /// <summary>
        /// 构造并发送 S7F18 回复
        /// </summary>
        private SecsGemMessage ReplyS7F18(SecsGemMessage request, byte ackc7)
        {
            // 触发你的 case DataType.Binary 分支
            var rootNode = new SecsGemNodeMessage(DataType.Binary, new byte[] { ackc7 });

            return new SecsGemMessage
            {
                Stream = 7,
                Function = 18,
                WBit = false,
                SystemBytes = request.SystemBytes, // 必须原样返回
                RootNode = rootNode
            };
        }

        #endregion S7F17-->S7F18 (设备请求删除配方)


        #region S7F19-->S7F20 (主机请求当前配方)


        /// <summary>
        /// S7F19-->S7F20 (主机请求当前配方)
        /// </summary>
        /// <param name="message"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task HandleS7F19Message(SecsGemMessage message, CancellationToken token = default)
        {
            string currentPpid = $"{_workStationDataModule.Station1ReciepParam.RecipeName ?? ""}&{_workStationDataModule.Station2ReciepParam.RecipeName ?? ""}"; // 从数据模块获取当前配方名
            var s7f20Response = CreateS7F20Response(message, currentPpid);
            s7f20Response.IsIncoming = false;
            _secsGemlog.Info($"发送SecsGem消息: {s7f20Response}");
            await _secsGemManger.SendMessageAsync(s7f20Response);
        }


        /// <summary>
        /// 构造 S7F20 响应消息
        /// </summary>
        private SecsGemMessage CreateS7F20Response(SecsGemMessage s7f19Request, string currentPpid)
        {
            // 触发您的 case DataType.ASCII 分支
            // 如果没有配方，传入 string.Empty 即可
            var rootNode = new SecsGemNodeMessage(DataType.ASCII, currentPpid ?? string.Empty);

            return new SecsGemMessage
            {
                Stream = 7,
                Function = 20,
                WBit = false,
                SystemBytes = s7f19Request.SystemBytes, // 必须匹配请求
                RootNode = rootNode
            };
        }


        #endregion S7F19-->S7F20 (主机请求当前配方)



        #region S10F3-->S10F4 主机下发通知到设备


        /// <summary>
        /// S10F3-->S10F4 主机下发通知到设备
        /// </summary>
        /// <param name="message"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task HandleS10F3Message(SecsGemMessage message, CancellationToken token = default)
        {
            // 1. 解析数据
            try
            {
                // 1. 解析数据
                var subNodes = message.RootNode.SubNode;
                byte tid = ((byte[])subNodes[0].Data)[0]; // 终端 ID
                string text = subNodes[1].TypedValue.ToString(); // 文本内容

                // 2. 在 UI 线程弹出对话框 (示例代码)


                // 3. 回复主机：0x00 表示已成功接收并尝试显示
                var s10f4Response = ReplyS10F4(message, 0x00);
                s10f4Response.IsIncoming = false;
                _secsGemlog.Info($"发送SecsGem消息: {s10f4Response}");
                await _secsGemManger.SendMessageAsync(s10f4Response);
            }
            catch (Exception ex)
            {
                _secsGemlog.Error($"处理 S10F3 失败: {ex.Message}");
                var s10f4Response = ReplyS10F4(message, 0x01);
                s10f4Response.IsIncoming = false;
                _secsGemlog.Info($"发送SecsGem消息: {s10f4Response}");
                await _secsGemManger.SendMessageAsync(s10f4Response);
            }
        }


        /// <summary>
        /// 生成S10F4回复信息
        /// </summary>
        /// <param name="request"></param>
        /// <param name="ackc10"></param>
        /// <returns></returns>
        private SecsGemMessage ReplyS10F4(SecsGemMessage request, byte ackc10)
        {
            // 触发您的 case DataType.Binary 分支
            var rootNode = new SecsGemNodeMessage(DataType.Binary, new byte[] { ackc10 });

            var response = new SecsGemMessage
            {
                Stream = 10,
                Function = 4,
                WBit = false,
                SystemBytes = request.SystemBytes, // 必须匹配请求
                RootNode = rootNode
            };

            return response;
        }
        #endregion S10F3-->S10F4 主机下发通知到设备


        #endregion SecsGem消息处理


    }
}
