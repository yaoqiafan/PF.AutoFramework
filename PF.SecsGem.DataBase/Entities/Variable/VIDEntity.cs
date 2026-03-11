using PF.Core.Entities.SecsGem.Params.ValidateParam;
using PF.Core.Enums;
using PF.SecsGem.DataBase.Entities.Basic;
using System.ComponentModel.DataAnnotations;

namespace PF.SecsGem.DataBase.Entities.Variable
{
    public class VIDEntity : BasicEntity
    {
        [Required(AllowEmptyStrings = false)]
        public override string ID { get; set; } = Guid.NewGuid().ToString();

        public uint Code { get; set; }

        public string Description { get; set; } = string.Empty;

        public string Comment { get; set; } = string.Empty;

        public string Type { get; set; }

        public string Value { get; set; }
    }

    public static class VIDExtend
    {
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

        public static VID ToVID(this VIDEntity entity)
        {
            var dataType = Enum.Parse<Core.Enums.DataType>(entity.Type);
            var vid = new VID(entity.Code, entity.Description, dataType);
            vid.Comment = entity.Comment;
            vid.SetValue(entity.Value);
            return vid;
        }

        // 保持向后兼容的旧方法名
        [Obsolete("Use ToEntity() instead")]
        public static VIDEntity GetVIDEntityFormVID(this VID vID) => vID.ToEntity();

        [Obsolete("Use ToVID() instead")]
        public static VID GetVIDFormVIDEntity(this VIDEntity entity) => entity.ToVID();
    }
}
