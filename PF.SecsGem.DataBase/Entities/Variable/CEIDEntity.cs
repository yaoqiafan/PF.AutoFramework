using PF.Core.Entities.SecsGem.Params.ValidateParam;
using PF.SecsGem.DataBase.Entities.Basic;
using System.ComponentModel.DataAnnotations;

namespace PF.SecsGem.DataBase.Entities.Variable
{
    public class CEIDEntity : BasicEntity
    {
        [Required(AllowEmptyStrings = false)]
        public override string ID { get; set; } = Guid.NewGuid().ToString();

        public uint Code { get; set; }

        public string Description { get; set; } = string.Empty;

        public string Comment { get; set; } = string.Empty;

        public uint[] LinkReportCode { get; set; } = Array.Empty<uint>();

        public string Key { get; set; } = string.Empty;
    }

    public static class CEIDExtend
    {
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

        public static CEID ToCEID(this CEIDEntity entity)
        {
            var ceid = new CEID(entity.Code, entity.Description, entity.LinkReportCode, entity.Key);
            ceid.Comment = entity.Comment;
            return ceid;
        }

        // 保持向后兼容的旧方法名
        [Obsolete("Use ToEntity() instead")]
        public static CEIDEntity GetCEIDEntityFormCEID(this CEID ceid) => ceid.ToEntity();

        [Obsolete("Use ToCEID() instead")]
        public static CEID GetCEIDFormCEIDEntity(this CEIDEntity entity) => entity.ToCEID();
    }
}
