using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Hardware.Motor.Basic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Hardware.Motor
{
    public class EtherCatAxis : BaseAxisDevice
    {
        public EtherCatAxis(string deviceId, int axisIndex, AxisParam  axisParam, string deviceName, bool isSimulated, ILogService logger, string dataDirectory)
            : base(
                deviceId: deviceId,
                deviceName: deviceName,
                isSimulated: isSimulated,
                logger: logger,
                dataDirectory: dataDirectory)
        {
            AxisIndex = axisIndex;
            Category = Core.Enums.HardwareCategory.Axis;
            Param = axisParam ;
        }

        public override int AxisIndex { get; }

        public override AxisParam Param { get; set; } = new AxisParam();

        protected override Task<bool> InternalConnectAsync(CancellationToken token)
        {
            return Task.FromResult(true);
        }

        protected override Task InternalDisconnectAsync()
        {
            return Task.FromResult(true);
        }


        protected override Task InternalResetAsync(CancellationToken token)
        {
            EnsureCardAttached();
            if (IsSimulated)
            {
                return Task .CompletedTask ;
            }
            return ParentCard!.ClearAxisError (AxisIndex);
        }



        // ── 私有工具 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 检查父板卡是否已挂载，未挂载则记录错误日志并抛出 InvalidOperationException。
        /// </summary>
        private void EnsureCardAttached([CallerMemberName] string caller = "")
        {
            if (ParentCard is null)
            {
                var msg = $"[{DeviceName}] '{caller}'：设备尚未挂载到板卡，请先调用 AttachToCard()。";
                _logger?.Error(msg);
                throw new InvalidOperationException(msg);
            }
        }


    }
}
