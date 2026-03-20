using Microsoft.Extensions.DependencyInjection;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.UI.Infrastructure.PrismBase;
using PF.WorkStation.AutoOcr.Mechanisms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.UI.ViewModels.Mechanisms
{
    public class Workstation1FeedingModelDebugViewModel: RegionViewModelBase
    {
        private readonly WorkStation1FeedingModule? _feedingModule;

        public Workstation1FeedingModelDebugViewModel(IContainerProvider containerProvider)
        {
            _feedingModule = containerProvider.Resolve<IMechanism>(nameof(WorkStation1FeedingModule)) as WorkStation1FeedingModule;
        }

    }
}
