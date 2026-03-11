using PF.Core.Entities.SecsGem.Params.ValidateParam;
using PF.SecsGem.DataBase.Entities.Basic;
using System.ComponentModel.DataAnnotations;

namespace PF.SecsGem.DataBase.Entities.Variable
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
        public static CommandIDEntity ToEntity(this CommandID commandID)
        {
            return new CommandIDEntity
            {
                Code = commandID.ID,
                Description = commandID.Description,
                Comment = commandID.Comment,
                LinkVID = commandID.LinkVID,
                RCMD = commandID.RCMD,
                Key = commandID.Key
            };
        }

        public static CommandID ToCommandID(this CommandIDEntity entity)
        {
            var cmd = new CommandID(entity.Code, entity.Description, entity.LinkVID, entity.RCMD, entity.Key);
            cmd.Comment = entity.Comment;
            return cmd;
        }

        // 保持向后兼容的旧方法名
        [Obsolete("Use ToEntity() instead")]
        public static CommandIDEntity GetCommandIDEntityFormCommandID(this CommandID commandID) => commandID.ToEntity();

        [Obsolete("Use ToCommandID() instead")]
        public static CommandID GetCommandIDFormCommandIDEntity(this CommandIDEntity entity) => entity.ToCommandID();
    }
}
