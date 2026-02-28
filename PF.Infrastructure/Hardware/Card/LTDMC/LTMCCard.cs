using Microsoft.EntityFrameworkCore.Metadata;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Hardware.Card.LTDMC
{
    public class LTMCCard : BaseMotionCard
    {


        public LTMCCard(int cardIndex, ILogService logger)
            : base(
                deviceId: $"SIM_CARD_{cardIndex}",
                deviceName: $"雷赛运动控制卡[{cardIndex}]",
                isSimulated: true,
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
            throw new NotImplementedException();
        }

        public override Task<bool> EnableAxisAsync(int axisIndex)
        {
            throw new NotImplementedException();
        }

        public override double GetAxisCurrentPosition(int axisIndex)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> HomeAxisAsync(int axisIndex, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        public override bool IsAxisEnabled(int axisIndex)
        {
            throw new NotImplementedException();
        }

        public override bool IsAxisMoving(int axisIndex)
        {
            throw new NotImplementedException();
        }

        public override bool IsAxisNegativeLimit(int axisIndex)
        {
            throw new NotImplementedException();
        }

        public override bool IsAxisPositiveLimit(int axisIndex)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> JogAsync(int axisIndex, double velocity, bool isPositive)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> MoveAbsoluteAsync(int axisIndex, double targetPosition, double velocity, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> MoveRelativeAsync(int axisIndex, double distance, double velocity, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        public override bool ReadInputPort(int portIndex)
        {
            throw new NotImplementedException();
        }

        public override bool ReadOutputPort(int portIndex)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> StopAxisAsync(int axisIndex)
        {
            throw new NotImplementedException();
        }

        public override void WriteOutputPort(int portIndex, bool value)
        {
            throw new NotImplementedException();
        }

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
            throw new NotImplementedException();
        }


        /********雷赛总线卡不加载配置文件********/
        protected override Task<bool> InternalLoadConfigAsync(string configFilePath)
        {
            return Task.FromResult(true);
        }

        protected override Task InternalResetAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}
