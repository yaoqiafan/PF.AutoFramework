using PF.Core.Entities.SecsGem.Params.ValidateParam;
using PF.SecsGem.DataBase.Entities.Basic;
using System.ComponentModel.DataAnnotations;

namespace PF.SecsGem.DataBase.Entities.Variable
{
    /// <summary>
    /// BasicEntity 实体
    /// </summary>
    public class CommandIDEntity : BasicEntity
    {
        /// <summary>
        /// 初始化实例
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public override string ID { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Code
        /// </summary>
        public uint Code { get; set; }

        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Comment
        /// </summary>
        public string Comment { get; set; } = string.Empty;

        /// <summary>
        /// 初始化实例
        /// </summary>
        public uint[] LinkVID { get; set; } = Array.Empty<uint>();

        /// <summary>
        /// RCMD
        /// </summary>
        public string RCMD { get; set; } = string.Empty;

        /// <summary>
        /// Key
        /// </summary>
        public string Key { get; set; } = string.Empty;
    }

    /// <summary>
    /// CommandIDExtend
    /// </summary>
    public static class CommandIDExtend
    {
        /// <summary>
        /// ToEntity 实体
        /// </summary>
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

        /// <summary>
        /// 初始化实例
        /// </summary>
        public static CommandID ToCommandID(this CommandIDEntity entity)
        {
            var cmd = new CommandID(entity.Code, entity.Description, entity.LinkVID, entity.RCMD, entity.Key);
            cmd.Comment = entity.Comment;
            return cmd;
        }

        // 保持向后兼容的旧方法名
        /// <summary>
        /// GetCommandIDEntityFormCommandID 实体
        /// </summary>
        [Obsolete("Use ToEntity() instead")]
        public static CommandIDEntity GetCommandIDEntityFormCommandID(this CommandID commandID) => commandID.ToEntity();

        /// <summary>
        /// GetCommandIDFormCommandIDEntity 实体
        /// </summary>
        [Obsolete("Use ToCommandID() instead")]
        public static CommandID GetCommandIDFormCommandIDEntity(this CommandIDEntity entity) => entity.ToCommandID();
    }
}
