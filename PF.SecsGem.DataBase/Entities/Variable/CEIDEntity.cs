using PF.Core.Entities.SecsGem.Params.ValidateParam;
using PF.SecsGem.DataBase.Entities.Basic;
using System.ComponentModel.DataAnnotations;

namespace PF.SecsGem.DataBase.Entities.Variable
{
    /// <summary>
    /// BasicEntity 实体
    /// </summary>
    public class CEIDEntity : BasicEntity
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
        public uint[] LinkReportCode { get; set; } = Array.Empty<uint>();

        /// <summary>
        /// Key
        /// </summary>
        public string Key { get; set; } = string.Empty;
    }

    /// <summary>
    /// CEIDExtend
    /// </summary>
    public static class CEIDExtend
    {
        /// <summary>
        /// ToEntity 实体
        /// </summary>
        public static CEIDEntity ToEntity(this CEID ceid)
        {
            return new CEIDEntity
            {
                Code = ceid.ID,
                Description = ceid.Description,
                Comment = ceid.Comment,
                LinkReportCode = ceid.LinkReportID,
                Key = ceid.Key
            };
        }

        /// <summary>
        /// 初始化实例
        /// </summary>
        public static CEID ToCEID(this CEIDEntity entity)
        {
            var ceid = new CEID(entity.Code, entity.Description, entity.LinkReportCode, entity.Key);
            ceid.Comment = entity.Comment;
            return ceid;
        }

        // 保持向后兼容的旧方法名
        /// <summary>
        /// GetCEIDEntityFormCEID 实体
        /// </summary>
        [Obsolete("Use ToEntity() instead")]
        public static CEIDEntity GetCEIDEntityFormCEID(this CEID ceid) => ceid.ToEntity();

        /// <summary>
        /// GetCEIDFormCEIDEntity 实体
        /// </summary>
        [Obsolete("Use ToCEID() instead")]
        public static CEID GetCEIDFormCEIDEntity(this CEIDEntity entity) => entity.ToCEID();
    }
}
