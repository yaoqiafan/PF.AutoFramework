using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Station.Basic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Services.CustomWorkstation
{
    public class DispenseStation : StationBase
    {
        // 可以在构造函数注入它需要的机构模组 (IMechanism)
        public DispenseStation(ILogService logger) : base("点胶工站", logger) { }

        protected override async Task ProcessLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                // 1. 【核心防呆】在动作循环顶部检查是否被主控要求暂停
                _pauseEvent.Wait(token);

                _logger.Info($"[{StationName}] 正在等待产品到位...");
                await Task.Delay(1000, token); // 模拟等待IO，注意传入 token

                // 2. 再次检查暂停（防在等待期间被暂停）
                _pauseEvent.Wait(token);

                _logger.Info($"[{StationName}] 开始执行点胶动作...");
                // await _dispenseMechanism.DispenseAsync(token);
                await Task.Delay(2000, token); // 模拟耗时动作

                _logger.Info($"[{StationName}] 点胶完成。");
            }
        }
    }
}
