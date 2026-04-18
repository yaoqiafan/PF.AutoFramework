using PF.Core.Entities.SecsGem.Params.ValidateParam;
using PF.Core.Enums;
using PF.SecsGem.DataBase.Entities.Basic;
using System.ComponentModel.DataAnnotations;

namespace PF.SecsGem.DataBase.Entities.Variable
{
    /// <summary>
    /// BasicEntity 实体
    /// </summary>
    public class VIDEntity : BasicEntity
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
        /// 类型
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 值
        /// </summary>
        public string Value { get; set; }
    }

    /// <summary>
    /// VIDExtend
    /// </summary>
    public static class VIDExtend
    {
        /// <summary>
        /// ToEntity 实体
        /// </summary>
        public static VIDEntity ToEntity(this VID vID)
        {
            return new VIDEntity
            {
                Code = vID.ID,
                Description = vID.Description,
                Comment = vID.Comment,
                Type = vID.DataType.ToString(),
                Value = vID.Value?.ToString()
            };
        }

        /// <summary>
        /// 初始化实例
        /// </summary>
        public static VID ToVID(this VIDEntity entity)
        {
            var dataType = Enum.Parse<Core.Enums.DataType>(entity.Type);
            var vid = new VID(entity.Code, entity.Description, dataType);
            vid.Comment = entity.Comment;
            vid.SetValue(entity.Value);
            return vid;
        }

        // 保持向后兼容的旧方法名
        /// <summary>
        /// GetVIDEntityFormVID 实体
        /// </summary>
        [Obsolete("Use ToEntity() instead")]
        public static VIDEntity GetVIDEntityFormVID(this VID vID) => vID.ToEntity();

        /// <summary>
        /// GetVIDFormVIDEntity 实体
        /// </summary>
        [Obsolete("Use ToVID() instead")]
        public static VID GetVIDFormVIDEntity(this VIDEntity entity) => entity.ToVID();
    }
}
