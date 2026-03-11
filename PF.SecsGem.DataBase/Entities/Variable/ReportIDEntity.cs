using PF.Core.Entities.SecsGem.Params.ValidateParam;
using PF.SecsGem.DataBase.Entities.Basic;
using System.ComponentModel.DataAnnotations;

namespace PF.SecsGem.DataBase.Entities.Variable
{
    public class ReportIDEntity : BasicEntity
    {
        [Required(AllowEmptyStrings = false)]
        public override string ID { get; set; } = Guid.NewGuid().ToString();

        public uint Code { get; set; }

        public string Description { get; set; } = string.Empty;

        public string Comment { get; set; } = string.Empty;

        public uint[] LinkVID { get; set; } = Array.Empty<uint>();
    }

    public static class ReportIDExtend
    {
        public static ReportIDEntity ToEntity(this ReportID reportID)
        {
            return new ReportIDEntity
            {
                Code = reportID.ID,
                Description = reportID.Description,
                Comment = reportID.Comment,
                LinkVID = reportID.LinkVID
            };
        }

        public static ReportID ToReportID(this ReportIDEntity entity)
        {
            var report = new ReportID(entity.Code, entity.Description, entity.LinkVID);
            report.Comment = entity.Comment;
            return report;
        }

        // 保持向后兼容的旧方法名
        [Obsolete("Use ToEntity() instead")]
        public static ReportIDEntity GetReportIDEntityFormReportID(this ReportID reportID) => reportID.ToEntity();

        [Obsolete("Use ToReportID() instead")]
        public static ReportID GetReportIDFormReportIDEntity(this ReportIDEntity entity) => entity.ToReportID();
    }
}
