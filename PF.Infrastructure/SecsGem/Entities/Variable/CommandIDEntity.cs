using PF.Core.Entities.SecsGem.Params.ValidateParam;
using PF.Infrastructure.SecsGem.Entities.Basic;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.SecsGem.Entities.Variable
{
    public class CommandIDEntity : BasicEntity
    {
        [Required(AllowEmptyStrings = false)]
        public override string ID { get; set; } = Guid.NewGuid().ToString();

        public uint Code { get; set; }

        public string Description { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;

        public uint[] LinkVID { get; set; } = Array.Empty<uint>();

        public string RCMD { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;

    }

    public static class CommandIDExtend
    {
        public static CommandIDEntity GetCommandIDEntityFormCommandID(this CommandID commandID)
        {
            CommandIDEntity commandIDEntity = new CommandIDEntity();
            commandIDEntity.Code = commandID.ID;
            commandIDEntity.Description = commandID.Description;
            commandIDEntity.Comment = commandID.Comment;
            commandIDEntity.LinkVID = commandID.LinkVID;
            commandIDEntity.RCMD = commandID.RCMD;
            commandIDEntity.Key = commandID.Key;
            return commandIDEntity;
        }



        public static CommandID GetCommandIDFormCommandIDEntity(this CommandIDEntity commandIDEntity)
        {
            CommandID commandID = new CommandID(commandIDEntity.Code, commandIDEntity.Description, commandIDEntity.LinkVID, commandIDEntity.RCMD ,commandIDEntity.Key);
            commandID.Comment = commandIDEntity.Comment;
            return commandID;
        }

    }
}
