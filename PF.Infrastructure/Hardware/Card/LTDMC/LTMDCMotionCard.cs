using log4net.Core;
using Microsoft.EntityFrameworkCore.Metadata;
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
    public class LTMDCMotionCard : BaseMotionCard
    {



        public LTMDCMotionCard(int cardIndex, string deviceId, string deviceName, bool isSimulated, ILogService logger)
            : base(
                deviceId: deviceId,
                deviceName: deviceName,
                isSimulated: isSimulated,
                logger: logger)
        {
            CardIndex = cardIndex;
        }

        public override int CardIndex { get; }

        private int _axiscount = 0;

        public override int AxisCount => _axiscount;


        private int _inputcount = 0;
        public override int InputCount => _inputcount;

        private int _outputcount = 0;
        public override int OutputCount => _outputcount;

        public override Task<bool> DisableAxisAsync(int axisIndex)
        {
            try
            {
                if (IsSimulated) { return Task.FromResult(true); }

                short ret = CardAPI.LTDMC.nmc_set_axis_disable((ushort)CardIndex, (ushort)axisIndex);
                if (ret != 0)
                {
                    throw new Exception($"轴[{axisIndex}]使能失败，函数名：nmc_set_axis_disable,返回值：{ret}");
                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return Task.FromResult(false);
            }

        }

        public override Task<bool> EnableAxisAsync(int axisIndex)
        {
            try
            {
                if (IsSimulated) { return Task.FromResult(true); }

                short ret = CardAPI.LTDMC.nmc_set_axis_enable((ushort)CardIndex, (ushort)axisIndex);
                if (ret != 0)
                {
                    throw new Exception($"轴[{axisIndex}]使能失败，函数名：nmc_set_axis_disable,返回值：{ret}");
                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return Task.FromResult(false);
            }
        }

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
                short ret = CardAPI.LTDMC.dmc_get_position_unit((ushort)CardIndex, (ushort)axisIndex, ref pos);
                if (ret != 0)
                {
                    throw new Exception($"获取轴当前位置错误  函数名：dmc_get_position_unit ,返回值：{ret}");
                }
                pos *= equiv.Value;
                return pos;
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return null;
            }
        }

        public override Task<bool> HomeAxisAsync(int axisIndex, int HomeModel, int HomeVel, int HomeAcc, int HomeDec, int HomeOffest, CancellationToken token = default)
        {
            try
            {
                if (IsSimulated) { Task.Delay(3000); return Task.FromResult(true); }

                double? equiv = this.GetCurEquiv(axisIndex);
                if (equiv == null)
                {
                    throw new Exception($"获取轴{axisIndex}脉冲当量失败");
                }
                var hovel = HomeVel / equiv;
                var hoacc = HomeAcc / equiv;
                var hodec = HomeDec / equiv;
                var hooffest = HomeOffest / equiv;
                var Tacc = ((double)hovel - (double)hovel / 10.0) / (double)hoacc;
                var Tdec = ((double)hovel - (double)hovel / 10.0) / (double)hodec;
                short ret = CardAPI.LTDMC.nmc_set_home_profile((ushort)CardIndex, (ushort)axisIndex, (ushort)HomeModel, hovel.Value / 10, hovel.Value, Tacc, Tdec, hooffest.Value);
                if (ret != 0)
                {
                    throw new Exception($"设置轴回零参数失败  函数名：nmc_set_home_profile,返回值：{ret}");
                }
                ret = CardAPI.LTDMC.dmc_clear_stop_reason((ushort)CardIndex, (ushort)axisIndex);
                if (ret != 0)
                {
                    throw new Exception($"单轴回原点失败，清除到位原因失败函数名：dmc_clear_stop_reason,返回值：{ret}");
                }
                ret = CardAPI.LTDMC.nmc_home_move((ushort)CardIndex, (ushort)axisIndex);
                if (ret != 0)
                {
                    throw new Exception($"单轴回原点失败，函数名：nmc_home_move,返回值：{ret}");
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
        /// 获取轴的脉冲当量
        /// </summary>
        /// <param name="axisIndex">轴索引</param>
        /// <returns></returns>
        private double? GetCurEquiv(int axisIndex)
        {


            double equiv = 0;
            short ret = CardAPI.LTDMC.dmc_get_equiv((ushort)CardIndex, (ushort)axisIndex, ref equiv);
            if (ret != 0)
            {
                HardwareLogger.Debug($"获取轴[{axisIndex}]脉冲当量错误错误，函数名：dmc_get_equiv,返回值：{ret}");
                return null;
            }
            return equiv;
        }

        public override MotionIOStatus GetMotionIOStatus(int axisIndex)
        {
            MotionIOStatus iostatus = new MotionIOStatus();
            try
            {
                if (IsSimulated) { Task.Delay(3000); return new MotionIOStatus(); }


                uint psts = CardAPI.LTDMC.dmc_axis_io_status((ushort)CardIndex, (ushort)axisIndex);
                iostatus.ALM = (psts & 0x01) == 0x01;
                iostatus.PEL = (psts & 0x02) == 0x02;
                iostatus.MEL = (psts & 0x04) == 0x04;
                iostatus.Emg = (psts & 0x08) == 0x08;
                iostatus.ORG = (psts & 0x10) == 0x10;
                ushort Axis_StateMachine = 0;
                short ret = CardAPI.LTDMC.nmc_get_axis_state_machine((ushort)CardIndex, (ushort)axisIndex, ref Axis_StateMachine);
                if (ret == 0)
                {
                    iostatus.SVO = Axis_StateMachine == 4;
                }
                else
                {
                    iostatus.SVO = false;
                }
                ushort run_mode = 0;
                ret = CardAPI.LTDMC.dmc_get_axis_run_mode((ushort)CardIndex, (ushort)axisIndex, ref run_mode);
                if (ret == 0)
                {
                    ret = CardAPI.LTDMC.dmc_check_done((ushort)CardIndex, (ushort)axisIndex);
                    iostatus.MoveDone = run_mode == 0 && ret == 1 ? true : false;
                    iostatus.Moving = (run_mode == 1 || run_mode == 2) && psts == 0 ? true : false;
                    ushort homeResult = 0;
                    ret = CardAPI.LTDMC.dmc_get_home_result((ushort)CardIndex, (ushort)axisIndex, ref homeResult);
                    if (ret == 0)
                    {
                        iostatus.HomeDone = run_mode == 0 && homeResult == 1 ? true : false;
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

        public override Task<bool> JogAsync(int axisIndex, double velocity, double Acc, double Dec, bool isPositive)
        {
            try
            {
                if (IsSimulated) { Task.Delay(3000); return Task.FromResult(true); }

                double tAcc = (velocity - velocity / 10.0) / Acc;
                double tDec = (velocity - velocity / 10.0) / Dec;
                ushort jogDir = isPositive ? (ushort)1 : (ushort)0;
                short ret = CardAPI.LTDMC.dmc_set_profile_unit((ushort)CardIndex, (ushort)axisIndex, velocity / 10.0, velocity, tAcc, tDec, velocity / 10.0);
                if (ret != 0)
                {
                    throw new Exception($"指定轴JOG运动失败，设置单轴运动速度曲线失败  函数名： dmc_set_profile_unit  返回值：{ret}");
                }
                ret = CardAPI.LTDMC.dmc_vmove((ushort)CardIndex, (ushort)axisIndex, jogDir);
                if (ret != 0)
                {
                    throw new Exception($"指定轴JOG运动失败    函数名：dmc_pmove_unit  返回值：{ret}");
                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return Task.FromResult(false);
            }

        }

        public override Task<bool> MoveAbsoluteAsync(int axisIndex, double targetPosition, double velocity, double Acc, double Dec, double STime, CancellationToken token = default)
        {
            try
            {
                if (IsSimulated) { Task.Delay(3000); return Task.FromResult(true); }

                double? equiv = this.GetCurEquiv(axisIndex);
                if (!equiv.HasValue)
                {
                    throw new Exception($"获取轴[{axisIndex}] 脉冲当量失败");
                }
                double tAcc = (velocity - velocity / 10.0) / (double)Acc;
                double tDec = (velocity - velocity / 10.0) / (double)Dec;
                targetPosition /= equiv.Value;
                ushort pos_mode = 1;//运动模式，0：相对坐标模式，1：绝对坐标模式
                //设置单轴运动曲线
                short ret = CardAPI.LTDMC.dmc_set_profile_unit((ushort)CardIndex, (ushort)axisIndex, velocity / 10.0, velocity, tAcc, tDec, velocity / 10.0);
                if (ret != 0)
                {
                    throw new Exception($"指定轴绝对位置运动失败，设置单轴运动速度曲线失败 函数名： dmc_set_profile_unit  返回值：{ret}");
                }
                //设置S段速度
                ret = CardAPI.LTDMC.dmc_set_s_profile((ushort)CardIndex, (ushort)axisIndex, 0, STime);
                if (ret != 0)
                {
                    throw new Exception($"指定轴绝对位置运动失败，设置单轴速度曲线S段参数值失败 函数名：dmc_set_s_profile  返回值：{ret}");
                }
                //执行点位运动
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

        public override Task<bool> MoveRelativeAsync(int axisIndex, double distance, double velocity, double Acc, double Dec, double STime, CancellationToken token = default)
        {
            try
            {
                if (IsSimulated) { Task.Delay(3000); return Task.FromResult(true); }

                double? equiv = this.GetCurEquiv(axisIndex);
                if (!equiv.HasValue)
                {
                    throw new Exception($"获取轴[{axisIndex}] 脉冲当量失败");
                }
                double tAcc = (velocity - velocity / 10.0) / (double)Acc;
                double tDec = (velocity - velocity / 10.0) / (double)Dec;
                distance /= equiv.Value;
                ushort pos_mode = 0;//运动模式，0：相对坐标模式，1：绝对坐标模式
                //设置单轴运动曲线
                short ret = CardAPI.LTDMC.dmc_set_profile_unit((ushort)CardIndex, (ushort)axisIndex, velocity / 10.0, velocity, tAcc, tDec, velocity / 10.0);
                if (ret != 0)
                {
                    throw new Exception($"轴相对位置运动失败，设置单轴运动速度曲线失败 函数名： dmc_set_profile_unit  返回值：{ret}");
                }
                //设置S段速度
                ret = CardAPI.LTDMC.dmc_set_s_profile((ushort)CardIndex, (ushort)axisIndex, 0, STime);
                if (ret != 0)
                {
                    throw new Exception($"轴相对位置运动失败，设置单轴速度曲线S段参数值失败 函数名：dmc_set_s_profile  返回值：{ret}");
                }
                //执行点位运动
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
                return state == 0;
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return null;
            }
        }

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

        public override Task<bool> StopAxisAsync(int axisIndex, bool IsEmgStop = false)
        {
            try
            {
                if (IsSimulated) { return Task.FromResult(true); }

                ushort stop_mode = IsEmgStop ? (ushort)1 : (ushort)0;//制动方式，0：减速停止，1：紧急停止
                short ret = CardAPI.LTDMC.dmc_stop((ushort)CardIndex, (ushort)axisIndex, stop_mode);
                if (ret != 0)
                {
                    throw new Exception($"指定轴停止JOG运动失败 函数名： LTDMCMotion.dmc_stop 返回值：{ret}");
                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return Task.FromResult(false);
            }

        }

        public override bool WriteOutputPort(int portIndex, bool value)
        {
            if (IsSimulated) { Task.FromResult(true); }
            try
            {
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

        protected override async Task<bool> InternalConnectAsync(CancellationToken token)
        {
            try
            {
                /*******控制卡初始化*******/
                short ret = CardAPI.LTDMC.dmc_board_init();
                if (ret <= 0 || ret > 8)
                {
                    if (ret == 0)
                    {
                        throw new Exception($"初始化运动控制卡失败,没有找到控制卡/控制卡异常 函数：dmc_board_init返回值:{ret} ");
                    }
                    else if (ret < 0)
                    {
                        throw new Exception($"初始化运动控制卡失败,有2张或2张以上的控制卡的硬件设置卡号相同 函数：dmc_board_init返回值:{ret} ");
                    }
                    else
                    {
                        throw new Exception($"初始化运动控制卡失败 函数：dmc_board_init返回值:{ret} ");
                    }
                }
                #region 获取卡信息
                ushort cardnum = 0;
                ushort[] cardids = new ushort[8];
                uint[] cardtypes = new uint[8];
                /*****获取控制卡硬件ID号****/
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
                    _cardtype = _cardtype & 0xfffff;
                    if (_cardtype == 0x15032 || _cardtype == 0x13032)  //查找第一张E5032或E3032卡
                    {
                        m_cardID = cardids[i];
                        EtherCardFlag = true;
                        break;
                    }
                }
                if (!EtherCardFlag)
                {
                    throw new Exception("初始化运动控制卡失败,不存在EtherCAT总线卡");
                }
                if (CardIndex != m_cardID)
                {
                    throw new Exception("初始化运动控制卡失败,文件配置的卡号和实际硬件卡号不符");
                }
                #endregion 获取卡信息
                #region 总线热复位
                if (!await ResetECat(token))
                {
                    throw new Exception("初始化运动控制卡失败,总线热复位失败");
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
        /// 雷赛总线卡热复位
        /// </summary>
        /// <returns></returns>
        private async Task<bool> ResetECat(CancellationToken token = default)
        {
            try
            {
                ushort errcode = 0;
                short ret = CardAPI.LTDMC.nmc_get_errcode((ushort)CardIndex, 2, ref errcode);
                if (ret != 0)
                {
                    throw new Exception($"雷赛总线卡热复位失败,nmc_get_errcode返回值：{ret}");
                }
                if (errcode != 0)
                {
                    DateTime dt = DateTime.Now;
                    ret = CardAPI.LTDMC.dmc_soft_reset((ushort)CardIndex);
                    if (ret != 0)
                    {
                        throw new Exception($"雷赛总线卡热复位失败雷赛总线卡热复位失败,dmc_soft_reset返回值：{ret}");
                    }
                    while (true)
                    {
                        await Task.Delay(10, token);
                        if ((DateTime.Now - dt).TotalSeconds > 10)
                        {
                            throw new Exception($"雷赛总线卡热复位失败,等待超时");
                        }
                        ret = CardAPI.LTDMC.nmc_get_errcode((ushort)CardIndex, 2, ref errcode);
                        if (ret == 0)
                        {
                            if (errcode == 0)
                            {
                                return true;
                            }
                        }
                        else
                        {
                            throw new Exception($"雷赛总线卡热复位失败,nmc_get_errcode返回值：{ret}");
                        }
                    }

                }
                else
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }


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


        /********雷赛总线卡不加载配置文件********/
        protected override Task<bool> InternalLoadConfigAsync(string configFilePath)
        {
            return Task.FromResult(true);
        }

        protected override Task InternalResetAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }



        public  override Task<bool > ClearAxisError(int axisIndex)
        {
            try
            {
                short ret = CardAPI.LTDMC.nmc_clear_axis_errcode((ushort)CardIndex, (ushort)axisIndex);
                if (ret != 0)
                {
                    throw new Exception($"清除轴异常失败,nmc_clear_axis_errcode返回值：{ret}");
                }
                return Task .FromResult (true );
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return Task.FromResult(false );
            }
        }

        #endregion 控制卡连接和初始化


        #region 高级功能

        #region 位置锁存

        public override Task<bool> SetLatchMode(int LatchNo, int AxisNo, int InPutPort, int LtcMode = 0, int LtcLogic = 0, double Filter = 0, double LatchSource = 0, CancellationToken token = default)
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
                short ret = CardAPI.LTDMC.dmc_softltc_set_mode((ushort)(this.CardIndex), (ushort)LatchNo, (ushort)1, (ushort)LtcMode, (ushort)InPutPort, (ushort)LtcLogic, Filter);
                if (ret != 0)
                {
                    throw new Exception($"配置锁存器 失败，函数 dmc_softltc_set_mode 返回值 {ret}");
                }
                ret = CardAPI.LTDMC.dmc_ltc_set_source((ushort)(this.CardIndex), (ushort)LatchNo, (ushort)AxisNo, (ushort)1);
                if (ret != 0)
                {
                    throw new Exception($"配置锁存源  失败，函数 dmc_ltc_set_source 返回值 {ret}");
                }
                ret = CardAPI.LTDMC.dmc_ltc_reset((ushort)(this.CardIndex), (ushort)LatchNo);
                if (ret != 0)
                {
                    throw new Exception($"复位锁存器  失败，函数 dmc_ltc_reset 返回值 {ret}");
                }
                return Task.FromResult(true);

            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return Task.FromResult(false);
            }


        }

        public override Task<int> GetLatchNumber(int LatchNo, int AxisNo, CancellationToken token = default)
        {
            try
            {
                if (IsSimulated) { return Task.FromResult(0); }

                // 调用雷赛 SDK 获取位置锁存编号
                int latchNumber = 0;
                short ret = CardAPI.LTDMC.dmc_softltc_get_number((ushort)CardIndex, (ushort)LatchNo, (ushort)AxisNo, ref latchNumber);
                if (ret != 0)
                {
                    throw new Exception($"读取锁存个数 ,dmc_softltc_get_number：{ret}");
                }
                return Task.FromResult(latchNumber);
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return Task.FromResult(-1);
            }

        }



        public override Task<double?> GetLatchPos(int LatchNo, int AxisNo, CancellationToken token = default)
        {
            try
            {
                if (IsSimulated) { return Task.FromResult((double?)0); }

                // 调用雷赛 SDK 获取位置锁存编号
                double pos = 0;
                short ret = CardAPI.LTDMC.dmc_softltc_get_value_unit((ushort)CardIndex, (ushort)LatchNo, (ushort)AxisNo, ref pos);
                if (ret != 0)
                {
                    throw new Exception($"读取锁存位置 ,dmc_softltc_get_value_unit：{ret}");
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
