using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Entities.Logging
{
    public class ChatInfoModel
    {
        public DateTime Time { get; set; }
        public object Message { get; set; }

        public string SenderId { get; set; }

        public ChatRoleType Role { get; set; }

        public object Enclosure { get; set; }
    }
}
