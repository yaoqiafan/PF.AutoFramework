using log4net.Core;
using Microsoft.EntityFrameworkCore.Metadata;
using PF.Core.Constants;
using PF.Core.Interfaces.Device.Hardware.Card;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace PF.Infrastructure.Hardware.Card.LTDMC
{
    /// <summary>
    /// 雷赛 (Leadshine) EtherCAT 总线运动控制卡底层驱动实现类。
    /// 封装了雷赛 LTDMC 系列（如 E5032/E3032）控制卡的初始化、轴控制、IO 读写及高级锁存功能。
    /// </summary>
    public class LTMDCMotionCard : BaseMotionCard
    {
        /// <summary>
        /// 实例化雷赛运动控制卡对象
        /// </summary>
        /// <param name="cardIndex">控制卡在系统中的硬件索引号 (Card ID)</param>
        /// <param name="deviceId">系统分配的全局唯一设备 ID</param>
        /// <param name="deviceName">设备的可读名称</param>
        /// <param name="isSimulated">是否以软件模拟模式运行（不真正调用底层 DLL）</param>
        /// <param name="logger">日志记录器</param>
        public LTMDCMotionCard(int cardIndex, string deviceId, string deviceName, bool isSimulated, ILogService logger)
            : base(
                deviceId: deviceId,
                deviceName: deviceName,
                isSimulated: isSimulated,
                logger: logger)
        {
            CardIndex = cardIndex;
        }

        /// <summary>获取控制卡硬件索引号</summary>
        public override int CardIndex { get; }

        private int _axiscount = 0;
        /// <summary>获取控制卡实际挂载和支持的轴总数</summary>
        public override int AxisCount => _axiscount;

        private int _inputcount = 0;
        /// <summary>获取控制卡支持的数字量输入 (DI) 端口总数</summary>
        public override int InputCount => _inputcount;

        private int _outputcount = 0;
        /// <summary>获取控制卡支持的数字量输出 (DO) 端口总数</summary>
        public override int OutputCount => _outputcount;

        /// <summary>
        /// 异步关闭指定轴的伺服使能 (Servo Off)
        /// </summary>
        public override Task<bool> DisableAxisAsync(int axisIndex)
        {
            try
            {
                if (IsSimulated) { return Task.FromResult(true); }

                short ret = CardAPI.LTDMC.nmc_set_axis_disable((ushort)CardIndex, (ushort)axisIndex);
                if (ret != 0)
                {
                    throw new Exception($"轴[{axisIndex}]去使能失败，函数名：nmc_set_axis_disable，返回值：{ret}");
                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 异步开启指定轴的伺服使能 (Servo On)
        /// </summary>
        public override Task<bool> EnableAxisAsync(int axisIndex)
        {
            try
            {
                if (IsSimulated) { return Task.FromResult(true); }

                short ret = CardAPI.LTDMC.nmc_set_axis_enable((ushort)CardIndex, (ushort)axisIndex);
                if (ret != 0)
                {
                    throw new Exception($"轴[{axisIndex}]使能失败，函数名：nmc_set_axis_enable，返回值：{ret}");
                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 获取指定轴的当前实际位置（转换为工程单位，如 mm 或度）
        /// </summary>
        public override double? GetAxisCurrentPosition(int axisIndex)
        {
            if (IsSimulated) { return 0; }
            try
            {
                double pos = 0;
                double? equiv = this.GetCurEquiv(axisIndex);
                if (equiv == null)
                {
                    return null;
                }
                // 获取以单位 (Unit) 为基准的位置
                short ret = CardAPI.LTDMC.dmc_get_position_unit((ushort)CardIndex, (ushort)axisIndex, ref pos);
                if (ret != 0)
                {
                    throw new Exception($"获取轴当前位置错误，函数名：dmc_get_position_unit，返回值：{ret}");
                }
                // 乘以脉冲当量转换为实际工程物理量
                pos *= equiv.Value;
                return pos;
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return null;
            }
        }

        /// <summary>
        /// 异步触发单轴回原点 (Homing)
        /// </summary>
        public override Task<bool> HomeAxisAsync(int axisIndex, int HomeModel, int HomeVel, int HomeAcc, int HomeDec, int HomeOffest, CancellationToken token = default)
        {
            try
            {
                // 模拟模式下等待 3 秒模拟回零耗时
                if (IsSimulated) { Task.Delay(3000).Wait(); return Task.FromResult(true); }

                double? equiv = this.GetCurEquiv(axisIndex);
                if (equiv == null)
                {
                    throw new Exception($"获取轴[{axisIndex}]脉冲当量失败");
                }

                // 将传入的工程单位参数转换为控制卡底层所需的脉冲单位
                var hovel = HomeVel / equiv;
                var hoacc = HomeAcc / equiv;
                var hodec = HomeDec / equiv;
                var hooffest = HomeOffest / equiv;

                // 根据速度和加速度计算加速时间 (Tacc) 与减速时间 (Tdec)
                // 公式：T = V / A。这里为了平滑处理，使用了最高速度的 90% 来计算加减速时间
                var Tacc = ((double)hovel - (double)hovel / 10.0) / (double)hoacc;
                var Tdec = ((double)hovel - (double)hovel / 10.0) / (double)hodec;

                // 1. 设置回原点参数曲线
                short ret = CardAPI.LTDMC.nmc_set_home_profile((ushort)CardIndex, (ushort)axisIndex, (ushort)HomeModel, hovel.Value / 10, hovel.Value, Tacc, Tdec, hooffest.Value);
                if (ret != 0)
                {
                    throw new Exception($"设置轴回零参数失败，函数名：nmc_set_home_profile，返回值：{ret}");
                }

                // 2. 清除轴先前的停止原因状态
                ret = CardAPI.LTDMC.dmc_clear_stop_reason((ushort)CardIndex, (ushort)axisIndex);
                if (ret != 0)
                {
                    throw new Exception($"单轴回原点失败，清除到位原因失败函数名：dmc_clear_stop_reason，返回值：{ret}");
                }

                // 3. 启动回原点运动
                ret = CardAPI.LTDMC.nmc_home_move((ushort)CardIndex, (ushort)axisIndex);
                if (ret != 0)
                {
                    throw new Exception($"单轴回原点失败，函数名：nmc_home_move，返回值：{ret}");
                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 获取底层配置的轴脉冲当量 (Equiv)
        /// 即每个脉冲对应的物理位移量
        /// </summary>
        /// <param name="axisIndex">轴索引</param>
        private double? GetCurEquiv(int axisIndex)
        {
            double equiv = 0;
            short ret = CardAPI.LTDMC.dmc_get_equiv((ushort)CardIndex, (ushort)axisIndex, ref equiv);
            if (ret != 0)
            {
                HardwareLogger.Debug($"获取轴[{axisIndex}]脉冲当量错误，函数名：dmc_get_equiv，返回值：{ret}");
                return null;
            }
            return equiv;
        }

        /// <summary>
        /// 获取轴当前的硬件 IO 信号及运动状态机状态
        /// </summary>
        public override MotionIOStatus GetMotionIOStatus(int axisIndex)
        {
            MotionIOStatus iostatus = new MotionIOStatus();
            try
            {
                if (IsSimulated) { return new MotionIOStatus(); }

                // 1. 读取轴的基础 IO 状态寄存器并执行位掩码 (Bitmask) 解析
                uint psts = CardAPI.LTDMC.dmc_axis_io_status((ushort)CardIndex, (ushort)axisIndex);
                iostatus.ALM = (psts & 0x01) == 0x01; // Bit 0: 伺服报警
                iostatus.PEL = (psts & 0x02) == 0x02; // Bit 1: 正硬限位
                iostatus.MEL = (psts & 0x04) == 0x04; // Bit 2: 负硬限位
                iostatus.Emg = (psts & 0x08) == 0x08; // Bit 3: 急停信号
                iostatus.ORG = (psts & 0x10) == 0x10; // Bit 4: 原点信号

                // 2. 读取 EtherCAT 轴的 CIA402 状态机
                ushort Axis_StateMachine = 0;
                short ret = CardAPI.LTDMC.nmc_get_axis_state_machine((ushort)CardIndex, (ushort)axisIndex, ref Axis_StateMachine);
                if (ret == 0)
                {
                    // 状态 4 代表 Operation Enabled (即伺服已使能/上电)
                    iostatus.SVO = Axis_StateMachine == 4;
                }
                else
                {
                    iostatus.SVO = false;
                }

                // 3. 读取轴当前的运行模式与运动到位标志
                ushort run_mode = 0;
                ret = CardAPI.LTDMC.dmc_get_axis_run_mode((ushort)CardIndex, (ushort)axisIndex, ref run_mode);
                if (ret == 0)
                {
                    // 检查常规运动是否完成
                    ret = CardAPI.LTDMC.dmc_check_done((ushort)CardIndex, (ushort)axisIndex);
                    iostatus.MoveDone = run_mode == 0 && ret == 1 ? true : false;

                    // 运行模式: 1(点位运动), 2(连续运动)。且无报警停止状态 (psts == 0)
                    iostatus.Moving = (run_mode == 1 || run_mode == 2) && psts == 0 ? true : false;

                    // 检查回零运动结果
                    ushort homeResult = 0;
                    ret = CardAPI.LTDMC.dmc_get_home_result((ushort)CardIndex, (ushort)axisIndex, ref homeResult);
                    if (ret == 0)
                    {
                        // run_mode == 0 (未在运动) 且 homeResult == 1 (回零成功)
                        iostatus.HomeDone = run_mode == 0 && homeResult == 1 ? true : false;
                        // run_mode == 3 (回零模式中) 且 homeResult == 0 (未完成)
                        iostatus.Homing = run_mode == 3 && homeResult == 0 ? true : false;
                    }
                    else
                    {
                        iostatus.HomeDone = false;
                        iostatus.Homing = false;
                    }
                }
                else
                {
                    iostatus.MoveDone = false;
                    iostatus.Moving = false;
                    iostatus.HomeDone = false;
                    iostatus.Homing = false;
                }
                return iostatus;
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return iostatus;
            }
        }

        /// <summary>
        /// 异步启动轴的连续 Jog (点动) 运动
        /// </summary>
        public override Task<bool> JogAsync(int axisIndex, double velocity, double Acc, double Dec, bool isPositive)
        {
            try
            {
                if (IsSimulated) { return Task.FromResult(true); }

                // 计算加减速时间
                double tAcc = (velocity - velocity / 10.0) / Acc;
                double tDec = (velocity - velocity / 10.0) / Dec;
                ushort jogDir = isPositive ? (ushort)1 : (ushort)0;

                // 设置速度曲线
                short ret = CardAPI.LTDMC.dmc_set_profile_unit((ushort)CardIndex, (ushort)axisIndex, velocity / 10.0, velocity, tAcc, tDec, velocity / 10.0);
                if (ret != 0)
                {
                    throw new Exception($"指定轴JOG运动失败，设置单轴运动速度曲线失败 函数名： dmc_set_profile_unit  返回值：{ret}");
                }

                // 启动连续运动 (VMove)
                ret = CardAPI.LTDMC.dmc_vmove((ushort)CardIndex, (ushort)axisIndex, jogDir);
                if (ret != 0)
                {
                    throw new Exception($"指定轴JOG运动失败    函数名：dmc_vmove  返回值：{ret}");
                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 异步发起单轴绝对位置运动 (Absolute Move)
        /// </summary>
        public override Task<bool> MoveAbsoluteAsync(int axisIndex, double targetPosition, double velocity, double Acc, double Dec, double STime, CancellationToken token = default)
        {
            try
            {
                if (IsSimulated) { return Task.FromResult(true); }

                double? equiv = this.GetCurEquiv(axisIndex);
                if (!equiv.HasValue)
                {
                    throw new Exception($"获取轴[{axisIndex}] 脉冲当量失败");
                }

                double tAcc = (velocity - velocity / 10.0) / (double)Acc;
                double tDec = (velocity - velocity / 10.0) / (double)Dec;
                targetPosition /= equiv.Value; // 转换为底层脉冲数

                ushort pos_mode = 1; // 运动模式：1 = 绝对坐标模式

                // 1. 设置单轴梯形运动曲线
                short ret = CardAPI.LTDMC.dmc_set_profile_unit((ushort)CardIndex, (ushort)axisIndex, velocity / 10.0, velocity, tAcc, tDec, velocity / 10.0);
                if (ret != 0)
                {
                    throw new Exception($"指定轴绝对位置运动失败，设置单轴运动速度曲线失败 函数名： dmc_set_profile_unit  返回值：{ret}");
                }

                // 2. 设置 S 段平滑速度时间
                ret = CardAPI.LTDMC.dmc_set_s_profile((ushort)CardIndex, (ushort)axisIndex, 0, STime);
                if (ret != 0)
                {
                    throw new Exception($"指定轴绝对位置运动失败，设置单轴速度曲线S段参数值失败 函数名：dmc_set_s_profile  返回值：{ret}");
                }

                // 3. 执行点位运动 (PMove)
                ret = CardAPI.LTDMC.dmc_pmove_unit((ushort)CardIndex, (ushort)axisIndex, targetPosition, pos_mode);
                if (ret != 0)
                {
                    throw new Exception($"指定轴绝对位置运动失败 函数名：dmc_pmove_unit  返回值：{ret}");
                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 异步发起单轴相对位置运动 (Relative Move)
        /// </summary>
        public override Task<bool> MoveRelativeAsync(int axisIndex, double distance, double velocity, double Acc, double Dec, double STime, CancellationToken token = default)
        {
            try
            {
                if (IsSimulated) { return Task.FromResult(true); }

                double? equiv = this.GetCurEquiv(axisIndex);
                if (!equiv.HasValue)
                {
                    throw new Exception($"获取轴[{axisIndex}] 脉冲当量失败");
                }

                double tAcc = (velocity - velocity / 10.0) / (double)Acc;
                double tDec = (velocity - velocity / 10.0) / (double)Dec;
                distance /= equiv.Value; // 转换为底层脉冲数

                ushort pos_mode = 0; // 运动模式：0 = 相对坐标模式

                // 1. 设置单轴运动曲线
                short ret = CardAPI.LTDMC.dmc_set_profile_unit((ushort)CardIndex, (ushort)axisIndex, velocity / 10.0, velocity, tAcc, tDec, velocity / 10.0);
                if (ret != 0)
                {
                    throw new Exception($"轴相对位置运动失败，设置单轴运动速度曲线失败 函数名： dmc_set_profile_unit  返回值：{ret}");
                }

                // 2. 设置 S 段平滑速度时间
                ret = CardAPI.LTDMC.dmc_set_s_profile((ushort)CardIndex, (ushort)axisIndex, 0, STime);
                if (ret != 0)
                {
                    throw new Exception($"轴相对位置运动失败，设置单轴速度曲线S段参数值失败 函数名：dmc_set_s_profile  返回值：{ret}");
                }

                // 3. 执行点位运动 (PMove)
                ret = CardAPI.LTDMC.dmc_pmove_unit((ushort)CardIndex, (ushort)axisIndex, distance, pos_mode);
                if (ret != 0)
                {
                    throw new Exception($"轴相对位置运动失败 函数名：dmc_pmove_unit  返回值：{ret}");
                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 读取指定的数字量输入端口 (DI) 状态
        /// </summary>
        public override bool? ReadInputPort(int portIndex)
        {
            try
            {
                if (IsSimulated) { return true; }

                ushort state = 0;
                short ret = CardAPI.LTDMC.dmc_read_inbit_ex((ushort)CardIndex, (ushort)portIndex, ref state);
                if (ret != 0)
                {
                    throw new Exception($"获取输入状态失败 函数名：dmc_read_inbit_ex 返回值:{ret}");
                }
                // 返回 true 表示有信号 (这里假设 0 为导通/低电平有效，具体取决于光耦接线)
                return state == 0;
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return null;
            }
        }

        /// <summary>
        /// 读取指定的数字量输出端口 (DO) 当前反馈状态
        /// </summary>
        public override bool? ReadOutputPort(int portIndex)
        {
            try
            {
                if (IsSimulated) { return true; }

                ushort state = 0;
                short ret = CardAPI.LTDMC.dmc_read_outbit_ex((ushort)CardIndex, (ushort)portIndex, ref state);
                if (ret != 0)
                {
                    throw new Exception($"获取输出状态失败 函数名：dmc_read_outbit_ex 返回值:{ret}");
                }
                return state == 0;
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return null;
            }
        }

        /// <summary>
        /// 异步停止指定轴的运动
        /// </summary>
        /// <param name="axisIndex">轴索引</param>
        /// <param name="IsEmgStop">是否为紧急停止（无减速过程直接抱死）</param>
        public override Task<bool> StopAxisAsync(int axisIndex, bool IsEmgStop = false)
        {
            try
            {
                if (IsSimulated) { return Task.FromResult(true); }

                ushort stop_mode = IsEmgStop ? (ushort)1 : (ushort)0; // 制动方式：0：减速停止，1：紧急停止
                short ret = CardAPI.LTDMC.dmc_stop((ushort)CardIndex, (ushort)axisIndex, stop_mode);
                if (ret != 0)
                {
                    throw new Exception($"指定轴停止运动失败 函数名： dmc_stop 返回值：{ret}");
                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 写入指定的数字量输出端口 (DO) 状态
        /// </summary>
        public override bool WriteOutputPort(int portIndex, bool value)
        {
            if (IsSimulated) { return true; }
            try
            {
                // 控制输出电平：假设 0 代表开启光耦，1 代表关闭 (具体取决于硬件板卡设计)
                ushort uvalue = value ? (ushort)0 : (ushort)1;
                short ret = CardAPI.LTDMC.dmc_write_outbit((ushort)CardIndex, (ushort)portIndex, uvalue);
                if (ret != 0)
                {
                    throw new Exception($"设置输出状态失败 函数名：dmc_write_outbit 返回值:{ret}");
                }
                return true;
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return false;
            }
        }


        #region 控制卡连接和初始化

        /// <summary>
        /// 内部执行控制卡的物理连接与板卡初始化
        /// </summary>
        protected override async Task<bool> InternalConnectAsync(CancellationToken token)
        {
            try
            {
                /******* 控制卡库初始化 *******/
                short ret = CardAPI.LTDMC.dmc_board_init();
                if (ret <= 0 || ret > 8)
                {
                    if (ret == 0)
                    {
                        throw new Exception($"初始化运动控制卡失败,没有找到控制卡/控制卡异常 函数：dmc_board_init 返回值:{ret} ");
                    }
                    else if (ret < 0)
                    {
                        throw new Exception($"初始化运动控制卡失败,有2张或2张以上的控制卡的硬件拨码开关设置卡号相同 函数：dmc_board_init 返回值:{ret} ");
                    }
                    else
                    {
                        throw new Exception($"初始化运动控制卡失败 函数：dmc_board_init 返回值:{ret} ");
                    }
                }

                #region 获取卡信息
                ushort cardnum = 0;
                ushort[] cardids = new ushort[8];
                uint[] cardtypes = new uint[8];

                /***** 获取控制卡硬件ID号列表 ****/
                ret = CardAPI.LTDMC.dmc_get_CardInfList(ref cardnum, cardtypes, cardids);
                if (ret != 0)
                {
                    throw new Exception($"初始化运动控制卡失败,获取控制卡硬件ID号失败,dmc_get_CardInfList返回值：{ret}");
                }

                bool EtherCardFlag = false;
                int m_cardID = 0;
                for (ushort i = 0; i < cardnum; i++)
                {
                    int _cardtype = (int)cardtypes[i];
                    _cardtype = _cardtype & 0xfffff; // 掩码提取真实的型号编号

                    // 查找第一张 E5032 或 E3032 (EtherCAT 总线卡)
                    if (_cardtype == 0x15032 || _cardtype == 0x13032)
                    {
                        m_cardID = cardids[i];
                        EtherCardFlag = true;
                        break;
                    }
                }

                if (!EtherCardFlag)
                {
                    throw new Exception("初始化运动控制卡失败,不存在 EtherCAT 总线卡 (未扫描到 E5032 或 E3032)");
                }
                if (CardIndex != m_cardID)
                {
                    throw new Exception("初始化运动控制卡失败,配置文件指定的卡号和实际硬件拨码卡号不符");
                }
                #endregion 获取卡信息

                #region 总线热复位
                if (!await ResetECat(token))
                {
                    throw new Exception("初始化运动控制卡失败, EtherCAT 总线热复位失败");
                }
                #endregion 总线热复位

                #region 获取轴数目
                uint axiscount = 0;
                ret = CardAPI.LTDMC.nmc_get_total_axes((ushort)CardIndex, ref axiscount);
                if (ret != 0)
                {
                    throw new Exception($"读取EtherCAT总线轴和虚拟轴轴数失败,方法运行异常：nmc_get_total_axes：{ret}");
                }
                _axiscount = (int)axiscount;
                #endregion 获取轴数目

                #region 获取IO数目
                ushort incount = 0;
                ushort outcount = 0;
                ret = CardAPI.LTDMC.nmc_get_total_ionum((ushort)CardIndex, ref incount, ref outcount);
                if (ret != 0)
                {
                    throw new Exception($"读取EtherCAT总线IO数失败,方法运行异常：nmc_get_total_ionum：{ret}");
                }
                _inputcount = (int)incount;
                _outputcount = (int)outcount;
                #endregion  获取IO数目

                #region 设置轴偏移
                // 初始化阶段清零所有轴的内部坐标偏移
                for (int i = 0; i < this.AxisCount; i++)
                {
                    ret = CardAPI.LTDMC.nmc_set_offset_pos((ushort)CardIndex, (ushort)i, 0);
                    if (ret != 0)
                    {
                        throw new Exception($"设置轴{i}当前位置失败,方法运行异常：nmc_set_offset_pos：{ret}");
                    }
                }
                #endregion 设置轴偏移

                return true;
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return false;
            }
        }

        /// <summary>
        /// 执行雷赛 EtherCAT 总线卡热复位。
        /// 用于在发生总线错误时尝试自动恢复通信状态。
        /// </summary>
        private async Task<bool> ResetECat(CancellationToken token = default)
        {
            try
            {
                ushort errcode = 0;
                // 检查总线状态节点 (Node=2) 的错误代码
                short ret = CardAPI.LTDMC.nmc_get_errcode((ushort)CardIndex, 2, ref errcode);
                if (ret != 0)
                {
                    throw new Exception($"雷赛总线卡错误读取失败,nmc_get_errcode返回值：{ret}");
                }

                // 若存在错误，执行软件复位流程
                if (errcode != 0)
                {
                    DateTime dt = DateTime.Now;
                    ret = CardAPI.LTDMC.dmc_soft_reset((ushort)CardIndex);
                    if (ret != 0)
                    {
                        throw new Exception($"雷赛总线卡软复位指令发送失败,dmc_soft_reset返回值：{ret}");
                    }

                    // 轮询等待复位完成，最长等待 10 秒
                    while (true)
                    {
                        await Task.Delay(10, token);
                        if ((DateTime.Now - dt).TotalSeconds > 10)
                        {
                            throw new Exception($"雷赛总线卡热复位失败,等待超时(>10s)");
                        }

                        ret = CardAPI.LTDMC.nmc_get_errcode((ushort)CardIndex, 2, ref errcode);
                        if (ret == 0)
                        {
                            if (errcode == 0)
                            {
                                return true; // 错误已清除，复位成功
                            }
                        }
                        else
                        {
                            throw new Exception($"轮询雷赛总线卡状态失败,nmc_get_errcode返回值：{ret}");
                        }
                    }
                }
                else
                {
                    return true; // 本身无错误，无需复位
                }
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return false;
            }
        }

        /// <summary>
        /// 断开连接，关闭控制卡并释放驱动层资源
        /// </summary>
        protected override Task InternalDisconnectAsync()
        {
            try
            {
                short ret = CardAPI.LTDMC.dmc_board_close();
                if (ret != 0)
                {
                    throw new Exception($"关闭运动控制卡失败,dmc_board_close返回值：{ret}");
                }
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// 雷赛总线卡内部参数通常固化在轴驱动器中，无需单独加载控制卡配置文件。
        /// 故此处仅作兼容实现。
        /// </summary>
        protected override Task<bool> InternalLoadConfigAsync(string configFilePath)
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// 硬件级别系统复位（桩方法）
        /// </summary>
        protected override Task InternalResetAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 内部健康检查定时任务。
        /// 周期性检查 EtherCAT 节点的底层错误码，发现通信异常时向上抛出硬件报警。
        /// </summary>
        protected override Task InternalCheckHealthAsync(CancellationToken token)
        {
            ushort errcode = 0;
            short ret = CardAPI.LTDMC.nmc_get_errcode((ushort)CardIndex, 2, ref errcode);
            if ((ret != 0 || errcode != 0) && !HasAlarm)
                RaiseAlarm(AlarmCodes.Hardware.MotionCardBusError,
                    $"运动控制卡总线错误，nmc_get_errcode 返回 ret={ret}, errcode={errcode}");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 异步清除指定轴的伺服/驱动器底层异常代码
        /// </summary>
        public override Task<bool> ClearAxisError(int axisIndex)
        {
            try
            {
                short ret = CardAPI.LTDMC.nmc_clear_axis_errcode((ushort)CardIndex, (ushort)axisIndex);
                if (ret != 0)
                {
                    throw new Exception($"清除轴异常失败,nmc_clear_axis_errcode返回值：{ret}");
                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return Task.FromResult(false);
            }
        }

        #endregion 控制卡连接和初始化

        #region 高级功能

        #region 位置锁存

        /// <summary>
        /// 配置硬件高速位置锁存模式 (High-Speed Position Latch)。
        /// 主要应用于飞拍、高速探头定位等需要微秒级响应的场景。
        /// </summary>
        /// <param name="LatchNo">锁存器通道编号 (通常 0-3)</param>
        /// <param name="AxisNo">需要被锁存位置的关联轴索引</param>
        /// <param name="InPutPort">触发锁存动作的硬件输入口 (DI)</param>
        /// <param name="LtcMode">锁存模式配置</param>
        /// <param name="LtcLogic">锁存触发逻辑 (上升沿或下降沿)</param>
        /// <param name="Filter">滤波时间参数</param>
        /// <param name="LatchSource">锁存数据源（指令位置或编码器反馈位置）</param>
        /// <param name="token">取消令牌</param>
        public override Task<bool> SetLatchMode(int LatchNo, int AxisNo, int InPutPort, int LtcMode = 1, int LtcLogic = 0, double Filter = 0, double LatchSource = 0, CancellationToken token = default)
        {
            try
            {
                if (IsSimulated) { return Task.FromResult(true); }
                if (LatchNo < 0 || LatchNo > 3)
                {
                    throw new Exception($"设置锁存位置参数错误：锁存器ID错误");
                }
                if (InPutPort < 0 || InPutPort > 7)
                {
                    throw new Exception($"设置锁存位置参数错误：输入参数ID错误");
                }

                // 1. 设置软件锁存模式和硬件关联触发口
                short ret = CardAPI.LTDMC.dmc_softltc_set_mode((ushort)(this.CardIndex), (ushort)LatchNo, (ushort)1, (ushort)LtcMode, (ushort)InPutPort, (ushort)LtcLogic, Filter);
                if (ret != 0)
                {
                    throw new Exception($"配置锁存器失败，函数 dmc_softltc_set_mode 返回值 {ret}");
                }

                // 2. 将锁存器关联到目标物理轴
                ret = CardAPI.LTDMC.dmc_softltc_set_source((ushort)(this.CardIndex), (ushort)LatchNo, (ushort)AxisNo, (ushort)1);
                if (ret != 0)
                {
                    throw new Exception($"配置锁存源失败，函数 dmc_softltc_set_source 返回值 {ret}");
                }

                // 3. 复位清理锁存器以准备捕获
                ret = CardAPI.LTDMC.dmc_softltc_reset((ushort)(this.CardIndex), (ushort)LatchNo);
                if (ret != 0)
                {
                    throw new Exception($"复位锁存器失败，函数 dmc_softltc_reset 返回值 {ret}");
                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 异步获取指定锁存器当前已捕获的有效位置点数
        /// </summary>
        public override Task<int> GetLatchNumber(int LatchNo, int AxisNo, CancellationToken token = default)
        {
            try
            {
                if (IsSimulated) { return Task.FromResult(0); }

                int latchNumber = 0;
                short ret = CardAPI.LTDMC.dmc_softltc_get_number((ushort)CardIndex, (ushort)LatchNo, (ushort)AxisNo, ref latchNumber);
                if (ret != 0)
                {
                    throw new Exception($"读取锁存个数失败, dmc_softltc_get_number：{ret}");
                }
                return Task.FromResult(latchNumber);
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return Task.FromResult(-1);
            }
        }

        /// <summary>
        /// 异步获取最近一次被捕获到的锁存点实际物理坐标
        /// </summary>
        public override Task<double?> GetLatchPos(int LatchNo, int AxisNo, CancellationToken token = default)
        {
            try
            {
                if (IsSimulated) { return Task.FromResult((double?)0); }

                double pos = 0;
                short ret = CardAPI.LTDMC.dmc_softltc_get_value_unit((ushort)CardIndex, (ushort)LatchNo, (ushort)AxisNo, ref pos);
                if (ret != 0)
                {
                    throw new Exception($"读取锁存位置失败, dmc_softltc_get_value_unit：{ret}");
                }
                return Task.FromResult((double?)pos);
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return null;
            }
        }

        #endregion 位置锁存

        #endregion 高级功能
    }
}