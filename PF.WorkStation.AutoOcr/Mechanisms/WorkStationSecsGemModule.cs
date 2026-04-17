using NPOI.POIFS.Crypt.Dsig.Facets;
using NPOI.SS.Formula.Functions;
using Org.BouncyCastle.Pkcs;
using Prism.Ioc;
using PF.Core.Attributes;
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
using System.Threading;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.Mechanisms
{
    /// <summary>
    /// 【SECS/GEM 模块】 (SECS/GEM Communication Module)
    /// 
    /// <para>架构定位：</para>
    /// 负责半导体机台与上位机 (Host/EAP) 之间的标准通讯。继承自 <see cref="BaseMechanism"/>，实现 SEMI E5/E37/E30 协议。
    /// 
    /// <para>软件职责：</para>
    /// 监听处理各类 <see cref="SecsGemMessage"/> 消息，维护设备的在线状态、报警上报、配方上下发 (RMS) 
    /// 以及动态事件 (CEID) 与报表 (RPTID) 的绑定与主动上报。
    /// </summary>
    /// <remarks>
    /// 💡 【已实现协议流】
    /// - Stream 1: 设备状态 (<see cref="HandleS1F1Message"/>, <see cref="HandleS1F3Message"/>, <see cref="HandleS1F13Message"/>, <see cref="HandleS1F15Message"/>, <see cref="HandleS1F17Message"/>)
    /// - Stream 2: 设备控制与配置 (<see cref="HandleS2F33Message"/>, <see cref="HandleS2F35Message"/>, <see cref="HandleS2F41Message"/>)
    /// - Stream 6: 数据采集 (<see cref="CreateS6F11"/> 事件触发上报)
    /// - Stream 7: 过程程序管理/配方管理 (<see cref="HandleS7F1Message"/>, <see cref="HandleS7F3Message"/>, <see cref="HandleS7F5Message"/>, <see cref="HandleS7F17Message"/>, <see cref="HandleS7F19Message"/>)
    /// - Stream 10: 终端服务/消息显示 (<see cref="HandleS10F3Message"/>)
    /// </remarks>
    [MechanismUI("SECSGEM模块", "WorkStationSecsGemModuleDebugView", 1)]
    public class WorkStationSecsGemModule : BaseMechanism
    {
        #region Fields & Properties (依赖服务与核心状态)

        private readonly ISecsGemManger _secsGemManger;
        private readonly IRecipeService<OCRRecipeParam> _recipeService;
        private readonly IContainerProvider _containerProvider;
        private readonly Infrastructure.Logging.CategoryLogger _secsGemlog;
        private WorkStationDataModule _workStationDataModule;

        private bool _isOnOffine = false;

        /// <summary>
        /// SECS/GEM 当前是否处于逻辑在线 (ON-LINE) 状态。
        /// 许多数据上报与配方指令必须在在线状态下才能执行。
        /// </summary>
        public bool IsOnOffine => _isOnOffine;

        #endregion

        #region Dynamic Data Snapshots (动态数据快照与映射)

        /// <summary>
        /// 报表定义映射表 (S2F33 定义)。
        /// Key: RPTID (报表ID) -> Value: 该报表包含的 VID (变量ID) 集合的 <see cref="List{T}"/>
        /// </summary>
        private Dictionary<uint, List<uint>> _reportDefinitions = new Dictionary<uint, List<uint>>();

        /// <summary>
        /// 事件关联映射表 (S2F35 定义)。
        /// Key: CEID (事件ID) -> Value: 该事件触发时需要绑定的 RPTID (报表ID) 集合的 <see cref="List{T}"/>
        /// </summary>
        private Dictionary<uint, List<uint>> _eventLinks = new Dictionary<uint, List<uint>>();

        #endregion

        #region Constructor & Lifecycle (构造与生命周期)

        public WorkStationSecsGemModule(
            IHardwareManagerService hardwareManagerService,
            IParamService paramService,
            ISecsGemManger secsGemManger,
            IRecipeService<OCRRecipeParam> recipeService,
            IContainerProvider containerProvider,
            ILogService logger)
            : base(E_Mechanisms.SECSGEM通讯模组.ToString(), hardwareManagerService, paramService, logger)
        {
            _secsGemManger = secsGemManger;
            _secsGemlog = Infrastructure.Logging.CategoryLoggerFactory.SecsGem(logger);
            _recipeService = recipeService;
            _containerProvider = containerProvider;
        }

        protected override async Task<bool> InternalInitializeAsync(CancellationToken token = default)
        {
            if (_secsGemManger == null)
            {
                _secsGemlog.Error("SecsGem 实例未创建，请检查软件配置逻辑。");
                return false;
            }

            if (!await _secsGemManger.InitializeAsync())
            {
                _secsGemlog.Error("SecsGem 物理层初始化连接失败。");
                return false;
            }

            _workStationDataModule = _containerProvider.Resolve<IMechanism>(nameof(WorkStationDataModule)) as WorkStationDataModule;

            _secsGemlog.Info("SecsGem 连接成功。");

            // 绑定全局消息监听路由
            _secsGemManger.MessageReceived += SecsGemManger_MessageReceived;

            return true;
        }

        protected override async Task InternalStopAsync()
        {
            if (_secsGemManger != null)
            {
                _secsGemManger.MessageReceived -= SecsGemManger_MessageReceived;
                await _secsGemManger.DisconnectAsync();
            }
        }

        #endregion

        #region Message Router (全局消息路由派发)

        /// <summary>
        /// 全局 SECS/GEM 消息接收与派发中枢，订阅自 <see cref="ISecsGemManger.MessageReceived"/>。
        /// </summary>
        private void SecsGemManger_MessageReceived(object? sender, SecsMessageReceivedEventArgs e)
        {
            var message = e.Message;
            message.IsIncoming = true;
            _secsGemlog.Info($"收到 SecsGem 消息: {message}");

            // 过滤设备发出的 Primary 消息回复 (偶数 Function)
            if (message.Function % 2 == 0) return;

            string str = $"S{message.Stream}F{message.Function}";

            // 基于 Stream 和 Function 进行业务分发
            switch (str)
            {
                case "S1F1":
                    HandleS1F1Message(message).ConfigureAwait(false);
                    break;
                case "S1F3":
                    HandleS1F3Message(message).ConfigureAwait(false);
                    break;
                case "S1F13":
                    HandleS1F13Message(message).ConfigureAwait(false);
                    break;
                case "S1F15":
                    HandleS1F15Message(message).ConfigureAwait(false);
                    break;
                case "S1F17":
                    HandleS1F17Message(message).ConfigureAwait(false);
                    break;

                case "S2F33":
                    HandleS2F33Message(message).ConfigureAwait(false);
                    break;
                case "S2F35":
                    HandleS2F35Message(message).ConfigureAwait(false);
                    break;
                case "S2F41":
                    HandleS2F41Message(message).ConfigureAwait(false);
                    break;

                case "S5F1":
                    _secsGemlog.Warn($"非法方向: {message}，S5F1 为设备上抛主机的报警指令，主机不应发送给设备。");
                    break;
                case "S6F11":
                    _secsGemlog.Warn($"非法方向: {message}，S6F11 为设备上抛主机的事件指令，主机不应发送给设备。");
                    break;

                case "S7F1":
                    HandleS7F1Message(message).ConfigureAwait(false);
                    break;
                case "S7F3":
                    HandleS7F3Message(message).ConfigureAwait(false);
                    break;
                case "S7F5":
                    HandleS7F5Message(message).ConfigureAwait(false);
                    break;
                case "S7F17":
                    HandleS7F17Message(message).ConfigureAwait(false);
                    break;
                case "S7F19":
                    HandleS7F19Message(message).ConfigureAwait(false);
                    break;

                case "S10F3":
                    HandleS10F3Message(message).ConfigureAwait(false);
                    break;

                default:
                    _secsGemlog.Warn($"未处理的 SecsGem 消息 (未实现该指令逻辑): {message}");
                    break;
            }
        }

        #endregion

        #region Stream 1: Equipment Status (设备状态)

        /// <summary>
        /// 处理 S1F1 (Are You There Request) 消息，并回复 S1F2 (On Line Data)。
        /// <para>心跳包：用于检查通信链路是否处于激活状态以及设备是否在线，通过 <see cref="SecsGemNodeMessage"/> 回复设备 MDLN 与 SOFTREV。</para>
        /// </summary>
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

            _secsGemlog.Info($"发送 SecsGem 消息: {send}");
            await _secsGemManger.SendMessageAsync(send);
        }

        /// <summary>
        /// 处理 S1F3 (Selected Equipment Status Request) 消息，并回复 S1F4。
        /// <para>状态变量请求：主机请求设备特定的变量值 (VID)。若请求列表为空，则设备应上报所有状态变量。</para>
        /// </summary>
        private async Task HandleS1F3Message(SecsGemMessage message, CancellationToken token = default)
        {
            if (message.RootNode == null || message.RootNode.SubNode == null)
            {
                _secsGemlog.Warn($"S1F3 消息 RootNode 结构异常，无法处理消息: {message}");
                return;
            }

            List<(DataType type, object val)> statusValues = new List<(DataType type, object val)>();

            if (message.RootNode.SubNode.Count == 0)
            {
                // 回复所有配置中已知的 VID
                foreach (var item in _secsGemManger?.ParamsManager?.GetParam<ValidateConfiguration>(ParamType.Validate)?.VIDS)
                {
                    statusValues.Add((item.Value.DataType, item.Value.Value));
                }
            }
            else
            {
                // 回复主机指定查询的特定 VID
                List<int> vids = new List<int>();
                for (int i = 0; i < message.RootNode.SubNode.Count; i++)
                {
                    vids.Add(Convert.ToInt32(message.RootNode.SubNode[i].TypedValue));
                }

                for (int i = 0; i < vids.Count; i++)
                {
                    var VID = _secsGemManger?.ParamsManager?.GetParam<ValidateConfiguration>(ParamType.Validate).GetVID((uint)vids[i]);
                    if (VID != null)
                    {
                        statusValues.Add((VID.DataType, VID.Value));
                    }
                    else
                    {
                        // 若主机查询了设备不支持的 VID，返回空列表以示缺省
                        statusValues.Add((DataType.LIST, null));
                    }
                }
            }

            var s1f4 = CreateS1F4Response(message, statusValues);
            s1f4.IsIncoming = false;

            _secsGemlog.Info($"发送 SecsGem 消息: {s1f4}");
            await _secsGemManger.SendMessageAsync(s1f4);
        }

        private SecsGemMessage CreateS1F4Response(SecsGemMessage s1f3Request, List<(DataType type, object val)> statusValues)
        {
            List<SecsGemNodeMessage> subNodes = new List<SecsGemNodeMessage>();

            foreach (var item in statusValues)
            {
                if (item.type == DataType.LIST)
                {
                    subNodes.Add(new SecsGemNodeMessage() { DataType = item.type, Length = 0 });
                }
                else
                {
                    subNodes.Add(new SecsGemNodeMessage(item.type, item.val));
                }
            }

            return new SecsGemMessage
            {
                Stream = 1,
                Function = 4,
                WBit = false,
                SystemBytes = s1f3Request.SystemBytes,
                RootNode = new SecsGemNodeMessage(DataType.LIST, subNodes)
            };
        }

        /// <summary>
        /// 处理 S1F13 (Establish Communications Request) 消息，并回复 S1F14。
        /// <para>正式建立通信连接的握手协议 (Commack)。</para>
        /// </summary>
        private async Task HandleS1F13Message(SecsGemMessage message, CancellationToken token = default)
        {
            SecsGemMessage response = CreateS1F14Response(message, commack: 0, mdln: "T-E243-0010", softrev: "V1.0.1");
            response.IsIncoming = false;
            _secsGemlog.Info($"发送 SecsGem 消息: {response}");
            await _secsGemManger.SendMessageAsync(response);
        }

        private SecsGemMessage CreateS1F14Response(SecsGemMessage s1f13Request, byte commack, string mdln, string softrev)
        {
            var infoSubList = new List<SecsGemNodeMessage>
            {
              new SecsGemNodeMessage(DataType.ASCII, mdln),
              new SecsGemNodeMessage(DataType.ASCII, softrev)
            };

            var rootSubNodes = new List<SecsGemNodeMessage>
            {
                new SecsGemNodeMessage(DataType.Binary, new byte[] { commack }),
                new SecsGemNodeMessage(DataType.LIST, infoSubList)
            };

            return new SecsGemMessage
            {
                Stream = 1,
                Function = 14,
                WBit = false,
                SystemBytes = s1f13Request.SystemBytes,
                RootNode = new SecsGemNodeMessage(DataType.LIST, rootSubNodes)
            };
        }

        /// <summary>
        /// 处理 S1F15 (Request OFF-LINE) 消息，并回复 S1F16。
        /// <para>主机请求将设备切换至离线状态 (OFF-LINE)。</para>
        /// </summary>
        private async Task HandleS1F15Message(SecsGemMessage message, CancellationToken token = default)
        {
            _isOnOffine = false; // 切换状态标识
            SecsGemMessage response = CreateS1F16Response(message, oflack: 0x00);
            response.IsIncoming = false;
            _secsGemlog.Info($"发送 SecsGem 消息: {response}");
            await _secsGemManger.SendMessageAsync(response);
        }

        private SecsGemMessage CreateS1F16Response(SecsGemMessage s1f15Request, byte oflack = 0x00)
        {
            return new SecsGemMessage
            {
                Stream = 1,
                Function = 16,
                WBit = false,
                SystemBytes = s1f15Request.SystemBytes,
                RootNode = new SecsGemNodeMessage(DataType.Binary, new byte[] { oflack })
            };
        }

        /// <summary>
        /// 处理 S1F17 (Request ON-LINE) 消息，并回复 S1F18。
        /// <para>主机请求将设备切换至在线状态 (ON-LINE)。</para>
        /// </summary>
        private async Task HandleS1F17Message(SecsGemMessage message, CancellationToken token = default)
        {
            _isOnOffine = true; // 切换状态标识
            SecsGemMessage response = CreateS1F18Response(message, onlack: 0x00);
            response.IsIncoming = false;
            _secsGemlog.Info($"发送 SecsGem 消息: {response}");
            await _secsGemManger.SendMessageAsync(response);
        }

        private SecsGemMessage CreateS1F18Response(SecsGemMessage s1f17Request, byte onlack = 0x00)
        {
            return new SecsGemMessage
            {
                Stream = 1,
                Function = 18,
                WBit = false,
                SystemBytes = s1f17Request.SystemBytes,
                RootNode = new SecsGemNodeMessage(DataType.Binary, new byte[] { onlack })
            };
        }

        #endregion 

        #region Stream 2: Equipment Control (设备控制与数据定义)

        /// <summary>
        /// 处理 S2F33 (Define Report) 消息，并回复 S2F34。
        /// <para>数据快照定制：主机通知设备将哪些 VID (变量) 组合成一个特定的 RPTID (报表)。</para>
        /// </summary>
        private async Task HandleS2F33Message(SecsGemMessage request)
        {
            try
            {
                var dataId = Convert.ToUInt32(request.RootNode.SubNode[0].TypedValue);
                var reportList = request.RootNode.SubNode[1].SubNode;

                // 若列表为空，表示要求设备解除/清空所有的报表定义
                if (reportList.Count == 0)
                {
                    _reportDefinitions.Clear();
                }
                else
                {
                    foreach (var reportNode in reportList)
                    {
                        uint rptid = Convert.ToUInt32(reportNode.SubNode[0].TypedValue);
                        var vids = reportNode.SubNode[1].SubNode.Select(v => Convert.ToUInt32(v.TypedValue)).ToList();

                        // 更新或新增该报表定义结构
                        _reportDefinitions[rptid] = vids;
                    }
                }
                var response = CreateS2F34Response(request, drack: 0x00);
                response.IsIncoming = false;
                _secsGemlog.Info($"发送 SecsGem 消息: {response}");
                await _secsGemManger.SendMessageAsync(response);
            }
            catch (Exception ex)
            {
                _secsGemlog.Error($"处理 S2F33 失败: {ex.Message}");
                var response = CreateS2F34Response(request, drack: 0x01); // 0x01: 拒绝
                response.IsIncoming = false;
                _secsGemlog.Info($"发送 SecsGem 消息: {response}");
                await _secsGemManger.SendMessageAsync(response);
            }
        }

        private SecsGemMessage CreateS2F34Response(SecsGemMessage s2f33Request, byte drack)
        {
            return new SecsGemMessage
            {
                Stream = 2,
                Function = 34,
                WBit = false,
                SystemBytes = s2f33Request.SystemBytes,
                RootNode = new SecsGemNodeMessage(DataType.Binary, new byte[] { drack })
            };
        }

        /// <summary>
        /// 处理 S2F35 (Link Event Report) 消息，并回复 S2F36。
        /// <para>事件链接订阅：主机通知设备，当特定的 CEID (事件) 发生时，必须一并上抛哪些定制的 RPTID (报表)。</para>
        /// </summary>
        private async Task HandleS2F35Message(SecsGemMessage request)
        {
            try
            {
                var dataId = Convert.ToUInt32(request.RootNode.SubNode[0].TypedValue);
                var eventList = request.RootNode.SubNode[1].SubNode;

                // 如果 eventList 长度为 0，按标准通常是解除所有事件链接关系
                if (eventList.Count == 0)
                {
                    _eventLinks.Clear();
                }
                else
                {
                    foreach (var eventNode in eventList)
                    {
                        uint ceid = Convert.ToUInt32(eventNode.SubNode[0].TypedValue);
                        var rptids = eventNode.SubNode[1].SubNode.Select(r => Convert.ToUInt32(r.TypedValue)).ToList();

                        // 建立事件触发联动链接
                        _eventLinks[ceid] = rptids;
                    }
                }

                var response = CreateS2F36Response(request, drack: 0x00);
                response.IsIncoming = false;
                _secsGemlog.Info($"发送 SecsGem 消息: {response}");
                await _secsGemManger.SendMessageAsync(response);
            }
            catch (Exception ex)
            {
                _secsGemlog.Error($"处理 S2F35 失败: {ex.Message}");
                var response = CreateS2F36Response(request, drack: 0x01);
                response.IsIncoming = false;
                _secsGemlog.Info($"发送 SecsGem 消息: {response}");
                await _secsGemManger.SendMessageAsync(response);
            }
        }

        private SecsGemMessage CreateS2F36Response(SecsGemMessage s2f35Request, byte drack)
        {
            return new SecsGemMessage
            {
                Stream = 2,
                Function = 36,
                WBit = false,
                SystemBytes = s2f35Request.SystemBytes,
                RootNode = new SecsGemNodeMessage(DataType.Binary, new byte[] { drack })
            };
        }

        /// <summary>
        /// 处理 S2F41 (Host Command Send) 消息，并回复 S2F42。
        /// <para>主机向设备下发指令 (如 START, STOP, PAUSE)。需根据实际业务逻辑进行内部重写。</para>
        /// </summary>
        private async Task HandleS2F41Message(SecsGemMessage message, CancellationToken token = default)
        {
            // HCACK: 0=接受, 1=指令不存在, 2=现在无法执行, 3=参数非法
            SecsGemMessage response = CreateS2F42Response(message, hcack: 0x00);
            response.IsIncoming = false;
            _secsGemlog.Info($"发送 SecsGem 消息: {response}");
            await _secsGemManger.SendMessageAsync(response);
        }

        private SecsGemMessage CreateS2F42Response(SecsGemMessage s2f41Request, byte hcack)
        {
            var rootNodes = new List<SecsGemNodeMessage>
            {
                new SecsGemNodeMessage(DataType.Binary, new byte[] { hcack }),
                new SecsGemNodeMessage(DataType.LIST, new List<SecsGemNodeMessage>()) // 空的返回参数列表
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

        #endregion

        #region Stream 7: Process Program Management (配方与工艺管理)

        /// <summary>
        /// 处理 S7F1 (Process Program Load Inquire) 消息，并回复 S7F2。
        /// <para>配方上传前的“预问询”。主机询问：“我有配方要下发，设备有空间存吗？”</para>
        /// </summary>
        private async Task HandleS7F1Message(SecsGemMessage message, CancellationToken token = default)
        {
            // PPGNT: 0x00 = OK 允许下发
            SecsGemMessage response = CreateS7F2Response(message, ppgnt: 0x00);
            response.IsIncoming = false;
            _secsGemlog.Info($"发送 SecsGem 消息: {response}");
            await _secsGemManger.SendMessageAsync(response);
        }

        private SecsGemMessage CreateS7F2Response(SecsGemMessage s7f1Request, byte ppgnt = 0X00)
        {
            return new SecsGemMessage
            {
                Stream = 7,
                Function = 2,
                WBit = false,
                SystemBytes = s7f1Request.SystemBytes,
                RootNode = new SecsGemNodeMessage(DataType.Binary, new byte[] { ppgnt })
            };
        }

        /// <summary>
        /// 处理 S7F3 (Process Program Send) 消息，并回复 S7F4。
        /// <para>主机正式下发配方数据 (二进制流或 Json 格式)，设备利用 <see cref="IRecipeService{T}"/> 负责接收、解析并落盘持久化。</para>
        /// </summary>
        private async Task HandleS7F3Message(SecsGemMessage message, CancellationToken token = default)
        {
            if (message.RootNode == null || message.RootNode.SubNode.Count != 2)
            {
                // 格式不合法，拒绝接收 (ACKC7: 0x04 = 非法格式)
                SecsGemMessage response = CreateS7F4Response(message, ackc7: 0x04);
                response.IsIncoming = false;
                _secsGemlog.Info($"发送 SecsGem 消息: {response}");
                await _secsGemManger.SendMessageAsync(response);
                return;
            }

            try
            {
                string PPID = message.RootNode.SubNode[0].TypedValue.ToString();
                var data = message.RootNode.SubNode[1].Data;

                // 序列化反解并写入本地
                string recipestr = Encoding.UTF8.GetString(data);
                var recipe = System.Text.Json.JsonSerializer.Deserialize<OCRRecipeParam>(recipestr);

                if (recipe != null && await _recipeService.RecipeParamWriteAsync(recipe))
                {
                    // 保存成功
                    SecsGemMessage response = CreateS7F4Response(message, ackc7: 0x00);
                    response.IsIncoming = false;
                    _secsGemlog.Info($"发送 SecsGem 消息: {response}");
                    await _secsGemManger.SendMessageAsync(response);
                }
                else
                {
                    throw new Exception($"配方保存本地实体化失败");
                }
            }
            catch (Exception ex)
            {
                _secsGemlog.Error($"SecsGem S7F3 下载配方失败: {ex.Message}");
                // 异常拒绝
                SecsGemMessage response = CreateS7F4Response(message, ackc7: 0x04);
                response.IsIncoming = false;
                _secsGemlog.Info($"发送 SecsGem 消息: {response}");
                await _secsGemManger.SendMessageAsync(response);
            }
        }

        private SecsGemMessage CreateS7F4Response(SecsGemMessage s7f3Request, byte ackc7)
        {
            return new SecsGemMessage
            {
                Stream = 7,
                Function = 4,
                WBit = false,
                SystemBytes = s7f3Request.SystemBytes,
                RootNode = new SecsGemNodeMessage(DataType.Binary, new byte[] { ackc7 })
            };
        }

        /// <summary>
        /// 处理 S7F5 (Process Program Request) 消息，并回复 S7F6。
        /// <para>主机通过 <see cref="IRecipeService{T}"/> 请求索要设备中特定 PPID 的配方数据。设备将其转为二进制流上传。</para>
        /// </summary>
        private async Task HandleS7F5Message(SecsGemMessage message, CancellationToken token = default)
        {
            string requestedPpid = message.RootNode.TypedValue?.ToString();

            if (string.IsNullOrEmpty(requestedPpid))
            {
                var response = CreateS7F6EmptyResponse(message);
                response.IsIncoming = false;
                _secsGemlog.Info($"发送 SecsGem 消息: {response}");
                await _secsGemManger.SendMessageAsync(response);
                return;
            }

            var recipe = await _recipeService.RecipeParam(requestedPpid);
            if (recipe == null)
            {
                // 配方不存在，返回空报文
                var response = CreateS7F6EmptyResponse(message);
                response.IsIncoming = false;
                _secsGemlog.Info($"发送 SecsGem 消息: {response}");
                await _secsGemManger.SendMessageAsync(response);
            }
            else
            {
                // 配方存在，实体化并回传
                string recipestr = System.Text.Json.JsonSerializer.Serialize(recipe);
                byte[] recipedata = Encoding.UTF8.GetBytes(recipestr);
                var response = CreateS7F6Response(message, requestedPpid, recipedata);
                response.IsIncoming = false;

                _secsGemlog.Info($"发送 SecsGem 消息: {response}");
                await _secsGemManger.SendMessageAsync(response);
            }
        }

        private SecsGemMessage CreateS7F6Response(SecsGemMessage s7f5Request, string ppid, byte[] ppBody)
        {
            var rootNodes = new List<SecsGemNodeMessage>
            {
                new SecsGemNodeMessage(DataType.ASCII, ppid),
                new SecsGemNodeMessage(DataType.Binary, ppBody)
            };

            return new SecsGemMessage
            {
                Stream = 7,
                Function = 6,
                WBit = false,
                SystemBytes = s7f5Request.SystemBytes,
                RootNode = new SecsGemNodeMessage(DataType.LIST, rootNodes)
            };
        }

        private SecsGemMessage CreateS7F6EmptyResponse(SecsGemMessage s7f5Request)
        {
            return new SecsGemMessage
            {
                Stream = 7,
                Function = 6,
                WBit = false,
                SystemBytes = s7f5Request.SystemBytes,
                RootNode = new SecsGemNodeMessage(DataType.LIST, 0) // 空列表代表无此配方
            };
        }

        /// <summary>
        /// 处理 S7F17 (Delete Process Program Send) 消息，并回复 S7F18。
        /// <para>主机通过 <see cref="IRecipeService{T}"/> 请求设备删除指定的列表里的配方。</para>
        /// </summary>
        private async Task HandleS7F17Message(SecsGemMessage s7f17Request, CancellationToken token = default)
        {
            var ppidNodes = s7f17Request.RootNode.SubNode;

            if (ppidNodes == null || ppidNodes.Count == 0)
            {
                // 根据协议：列表为空代表企图删除所有配方，设备通常予以拒绝
                var rejectRes = ReplyS7F18(s7f17Request, 0x01);
                rejectRes.IsIncoming = false;
                await _secsGemManger.SendMessageAsync(rejectRes);
                return;
            }

            byte ackc7 = 0; // 0=成功, 1=拒绝, 3=至少一个没找到

            foreach (var node in ppidNodes)
            {
                string ppid = node.TypedValue?.ToString();
                if (string.IsNullOrEmpty(ppid)) continue;

                var recipe = await _recipeService.RecipeParam(ppid);
                if (recipe == null)
                {
                    ackc7 = 3;
                }
                else
                {
                    bool deleted = await _recipeService.RecipeDeleteAsync(ppid);
                    if (!deleted)
                    {
                        ackc7 = 1; // 物理删除失败
                        break;
                    }
                }
            }

            var response = ReplyS7F18(s7f17Request, ackc7);
            response.IsIncoming = false;
            _secsGemlog.Info($"发送 SecsGem 消息: {response}");
            await _secsGemManger.SendMessageAsync(response);

            // TODO: 如果删除成功 (ackc7 == 0)，则可能需要触发 GEM CEID 事件上报 (S6F11)
        }

        private SecsGemMessage ReplyS7F18(SecsGemMessage request, byte ackc7)
        {
            return new SecsGemMessage
            {
                Stream = 7,
                Function = 18,
                WBit = false,
                SystemBytes = request.SystemBytes,
                RootNode = new SecsGemNodeMessage(DataType.Binary, new byte[] { ackc7 })
            };
        }

        /// <summary>
        /// 处理 S7F19 (Current EPPD Request) 消息，并回复 S7F20。
        /// <para>主机询问机台当前各个工位正在执行哪套配方 (PPID)，通过 <see cref="WorkStationDataModule"/> 进行检索。</para>
        /// </summary>
        private async Task HandleS7F19Message(SecsGemMessage message, CancellationToken token = default)
        {
            // 通过数据中枢拼装出实际活跃的配方字串
            string currentPpid = $"{_workStationDataModule.Station1ReciepParam.RecipeName ?? ""}&{_workStationDataModule.Station2ReciepParam.RecipeName ?? ""}";

            var s7f20Response = CreateS7F20Response(message, currentPpid);
            s7f20Response.IsIncoming = false;

            _secsGemlog.Info($"发送 SecsGem 消息: {s7f20Response}");
            await _secsGemManger.SendMessageAsync(s7f20Response);
        }

        private SecsGemMessage CreateS7F20Response(SecsGemMessage s7f19Request, string currentPpid)
        {
            return new SecsGemMessage
            {
                Stream = 7,
                Function = 20,
                WBit = false,
                SystemBytes = s7f19Request.SystemBytes,
                RootNode = new SecsGemNodeMessage(DataType.ASCII, currentPpid ?? string.Empty)
            };
        }

        #endregion

        #region Stream 10: Terminal Services (终端显示与通知)

        /// <summary>
        /// 处理 S10F3 (Terminal Display, Single) 消息，并回复 S10F4。
        /// <para>主机下发文本消息，要求设备在操作员控制面板 (HMI) 上弹出气泡或对话框通知。</para>
        /// </summary>
        private async Task HandleS10F3Message(SecsGemMessage message, CancellationToken token = default)
        {
            try
            {
                var subNodes = message.RootNode.SubNode;
                byte tid = ((byte[])subNodes[0].Data)[0]; // 终端 ID
                string text = subNodes[1].TypedValue.ToString(); // 主机发来的文本内容

                // TODO: 在 UI 线程抛出对话框事件 (可以通过 EventAggregator 派发至 MainWindow)

                // 回复已成功接收
                var s10f4Response = ReplyS10F4(message, 0x00);
                s10f4Response.IsIncoming = false;
                _secsGemlog.Info($"发送 SecsGem 消息: {s10f4Response}");
                await _secsGemManger.SendMessageAsync(s10f4Response);
            }
            catch (Exception ex)
            {
                _secsGemlog.Error($"处理 S10F3 失败: {ex.Message}");
                var s10f4Response = ReplyS10F4(message, 0x01); // 0x01: Terminal not available
                s10f4Response.IsIncoming = false;
                _secsGemlog.Info($"发送 SecsGem 消息: {s10f4Response}");
                await _secsGemManger.SendMessageAsync(s10f4Response);
            }
        }

        private SecsGemMessage ReplyS10F4(SecsGemMessage request, byte ackc10)
        {
            return new SecsGemMessage
            {
                Stream = 10,
                Function = 4,
                WBit = false,
                SystemBytes = request.SystemBytes,
                RootNode = new SecsGemNodeMessage(DataType.Binary, new byte[] { ackc10 })
            };
        }

        #endregion

        #region Stream 6: Data Collection (事件与报表主动上报)

        /// <summary>
        /// 核心方法：供其他工站和机制类主动调用。
        /// 当机台发生特定动作时 (如：开始生产、配方修改、报警停机)，传入对应的事件 ID (CEID)，
        /// 本方法将自动检索绑定的报表，打包数据，并通过 <see cref="CreateS6F11"/> 生成 S6F11 上抛至 Host。
        /// </summary>
        /// <param name="ceid">设备发生变化的事件 ID</param>
        public async Task TriggerDynamicEvent(uint ceid)
        {
            // 检查 S2F35 中是否为该事件订阅过报表链接，若未订阅，标准做法是可以静默忽略
            if (!_eventLinks.ContainsKey(ceid))
            {
                return;
            }

            var s6f11 = CreateS6F11(ceid);
            s6f11.IsIncoming = false;

            _secsGemlog.Info($"发送 SecsGem 消息 (事件上抛): {s6f11}");
            await _secsGemManger.SendMessageAsync(s6f11);
        }

        /// <summary>
        /// 根据 CEID 动态拼装 S6F11 报文
        /// </summary>
        private SecsGemMessage CreateS6F11(uint ceid, uint dataId = 0)
        {
            if (!_eventLinks.TryGetValue(ceid, out List<uint> linkedRptIds))
            {
                linkedRptIds = new List<uint>();
            }

            var reportListNode = new List<SecsGemNodeMessage>();

            // 遍历并组装事件关联的所有报表
            foreach (var rptid in linkedRptIds)
            {
                if (_reportDefinitions.TryGetValue(rptid, out List<uint> vids))
                {
                    var vValueNodes = new List<SecsGemNodeMessage>();

                    // 获取报表内要求的每一个变量实时值
                    foreach (var vid in vids)
                    {
                        var VIDObj = _secsGemManger?.ParamsManager?.GetParam<ValidateConfiguration>(ParamType.Validate).GetVID(vid);

                        // 取出实体对象的实际类型与值，不可将整个类对象混作数值传入
                        if (VIDObj != null)
                        {
                            vValueNodes.Add(new SecsGemNodeMessage(VIDObj.DataType, VIDObj.Value));
                        }
                        else
                        {
                            // 防呆占位
                            vValueNodes.Add(new SecsGemNodeMessage(DataType.LIST, 0));
                        }
                    }

                    // 构建单条报表块: [RPTID, [V1, V2, ...]]
                    var rptItem = new List<SecsGemNodeMessage>
                    {
                        new SecsGemNodeMessage(DataType.U4, rptid),
                        new SecsGemNodeMessage(DataType.LIST, vValueNodes)
                    };

                    reportListNode.Add(new SecsGemNodeMessage(DataType.LIST, rptItem));
                }
            }

            // 构建 S6F11 根结构: [DATAID, CEID, [Reports]]
            var s6f11Root = new List<SecsGemNodeMessage>
            {
                new SecsGemNodeMessage(DataType.U4, dataId),
                new SecsGemNodeMessage(DataType.U4, ceid),
                new SecsGemNodeMessage(DataType.LIST, reportListNode)
            };

            return new SecsGemMessage
            {
                Stream = 6,
                Function = 11,
                WBit = true, // 要求 Host 必须回复 S6F12 确认收到
                RootNode = new SecsGemNodeMessage(DataType.LIST, s6f11Root),
                SystemBytes = GenerateSystemBytes()
            };
        }

        #endregion

        #region Helper Methods (内部通讯辅助)

        private uint _systemByteCounter = 0;

        /// <summary>
        /// 生成唯一的系统字节 (System Bytes)
        /// 用于设备端主动发起 Primary 消息（如 S6F11, S1F1）时，借助 <see cref="BitConverter"/> 和 <see cref="Interlocked"/> 填充 SECS 报文头部标识。
        /// </summary>
        /// <returns>4 字节的大端序唯一标识符的 <see cref="List{T}"/></returns>
        public List<byte> GenerateSystemBytes()
        {
            // 原子递增，确保多线程并发下系统字节仍唯一
            uint nextId = (uint)Interlocked.Increment(ref _systemByteCounter);

            // 转为 SECS 标准的大端序 (Big-Endian)
            byte[] bytes = BitConverter.GetBytes(nextId);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return bytes.ToList();
        }

        #endregion
    }
}