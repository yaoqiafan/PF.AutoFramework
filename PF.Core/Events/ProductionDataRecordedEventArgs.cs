using PF.Core.Entities.ProductionData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Events
{
    public class ProductionDataRecordedEventArgs : EventArgs
    {
        public ProductionRecord Record { get; set; } = null!;
    }
}
