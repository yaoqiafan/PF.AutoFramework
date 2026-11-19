using PF.Core.Enums.Hardware;
using PF.Core.Interfaces.Device.Hardware.IO.Analog;
using PF.Core.Models.Device.Hardware.IO.Analog;

namespace PF.Infrastructure.Hardware.Card.LTDMC
{
    /// <summary>
    /// 雷赛控制卡的模拟量通道原语（<see cref="IAnalogCard"/>），并作为模拟量宿主（<see cref="IAnalogCarrier"/>）
    /// 让模拟量设备可以直接挂在本卡下单独使用。
    ///
    /// <para><b>⚠ 待硬件确认</b>：以下映射按 CardAPI\LTMC.cs 中的函数声明及其注释实现，尚未上机验证——</para>
    /// <list type="bullet">
    ///   <item>NodeId = null → <c>dmc_get_ad_input / dmc_set_da_output / dmc_get_da_output</c>
    ///     （注释为"控制卡接线盒 AD/DA，适用于所有脉冲卡"），首次写出前调用一次 <c>dmc_set_da_enable</c>；</item>
    ///   <item>NodeId ≠ null → <c>nmc_get_ad_input / nmc_set_da_output / nmc_get_da_output / nmc_set_ad_mode / nmc_set_da_mode</c>
    ///     （注释为"CAN ADDA，适用于所有脉冲卡"）。</item>
    /// </list>
    /// <para>E5032/E3032 总线卡上的 EtherCAT 模拟量从站是否走这组函数、还是需要通过 PDO
    /// （<c>nmc_read_txpdo_extra / nmc_write_rxpdo_extra</c>）访问，以及返回值单位（V/mA 还是码值）、
    /// <c>mode</c> / <c>buffer_nums</c> 的含义，须以雷赛手册为准。若需调整，只改本文件即可，上层不受影响。</para>
    /// </summary>
    public partial class LTMDCMotionCard : IAnalogCard, IAnalogCarrier
    {
        // 板卡本体 DA 输出使能只需下发一次；板卡重连后重新下发
        private volatile bool _onboardDaEnabled;

        /// <summary>本卡自身即模拟量通道原语的持有者</summary>
        public IAnalogCard? AnalogCard => this;

        /// <inheritdoc/>
        public double? ReadAnalogInputRaw(AnalogAddress address)
        {
            if (IsSimulated) return 0;
            try
            {
                double value = 0;
                short ret = address.NodeId is int node
                    ? CardAPI.LTDMC.nmc_get_ad_input((ushort)CardIndex, (ushort)node, (ushort)address.Channel, ref value)
                    : CardAPI.LTDMC.dmc_get_ad_input((ushort)CardIndex, (ushort)address.Channel, ref value);
                if (ret != 0)
                    throw new Exception($"读取模拟量输入失败 {address} 函数名：{(address.NodeId.HasValue ? "nmc_get_ad_input" : "dmc_get_ad_input")} 返回值:{ret}");
                return value;
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return null;
            }
        }

        /// <inheritdoc/>
        public bool WriteAnalogOutputRaw(AnalogAddress address, double raw)
        {
            if (IsSimulated) return true;
            try
            {
                short ret;
                if (address.NodeId is int node)
                {
                    ret = CardAPI.LTDMC.nmc_set_da_output((ushort)CardIndex, (ushort)node, (ushort)address.Channel, raw);
                }
                else
                {
                    if (!_onboardDaEnabled)
                    {
                        short en = CardAPI.LTDMC.dmc_set_da_enable((ushort)CardIndex, 1);
                        if (en != 0) throw new Exception($"使能板卡 DA 输出失败 函数名：dmc_set_da_enable 返回值:{en}");
                        _onboardDaEnabled = true;
                    }
                    ret = CardAPI.LTDMC.dmc_set_da_output((ushort)CardIndex, (ushort)address.Channel, raw);
                }
                if (ret != 0)
                    throw new Exception($"写出模拟量失败 {address} 函数名：{(address.NodeId.HasValue ? "nmc_set_da_output" : "dmc_set_da_output")} 返回值:{ret}");
                return true;
            }
            catch (Exception ex)
            {
                HardwareLogger.Error(ex.Message, ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public double? ReadAnalogOutputRaw(AnalogAddress address)
        {
            if (IsSimulated) return 0;
            try
            {
                double value = 0;
                short ret = address.NodeId is int node
                    ? CardAPI.LTDMC.nmc_get_da_output((ushort)CardIndex, (ushort)node, (ushort)address.Channel, ref value)
                    : CardAPI.LTDMC.dmc_get_da_output((ushort)CardIndex, (ushort)address.Channel, ref value);
                if (ret != 0)
                    throw new Exception($"回读模拟量输出失败 {address} 函数名：{(address.NodeId.HasValue ? "nmc_get_da_output" : "dmc_get_da_output")} 返回值:{ret}");
                return value;
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return null;
            }
        }

        /// <summary>
        /// 下发通道模式：节点通道调用 nmc_set_ad_mode / nmc_set_da_mode（buffer_nums 固定为 0，待手册确认）；
        /// 板卡本体通道无模式可配，直接返回 true。
        /// </summary>
        public bool ConfigureAnalogChannel(AnalogAddress address, AnalogDirection direction, int mode)
        {
            if (IsSimulated || address.NodeId is not int node) return true;
            try
            {
                short ret = direction == AnalogDirection.Input
                    ? CardAPI.LTDMC.nmc_set_ad_mode((ushort)CardIndex, (ushort)node, (ushort)address.Channel, (ushort)mode, 0)
                    : CardAPI.LTDMC.nmc_set_da_mode((ushort)CardIndex, (ushort)node, (ushort)address.Channel, (ushort)mode, 0);
                if (ret != 0)
                    throw new Exception($"设置模拟量模式失败 {address} 模式:{mode} 函数名：{(direction == AnalogDirection.Input ? "nmc_set_ad_mode" : "nmc_set_da_mode")} 返回值:{ret}");
                return true;
            }
            catch (Exception ex)
            {
                HardwareLogger.Error(ex.Message, ex);
                return false;
            }
        }
    }
}
