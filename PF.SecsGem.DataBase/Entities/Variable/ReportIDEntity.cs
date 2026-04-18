using PF.Core.Entities.SecsGem.Params.ValidateParam;
using PF.SecsGem.DataBase.Entities.Basic;
using System.ComponentModel.DataAnnotations;

namespace PF.SecsGem.DataBase.Entities.Variable
{
    /// <summary>
    /// BasicEntity 实体
    /// </summary>
    public class ReportIDEntity : BasicEntity
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
    }

    /// <summary>
    /// ReportIDExtend
    /// </summary>
    public static class ReportIDExtend
    {
        /// <summary>
        /// ToEntity 实体
        /// </summary>
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

        /// <summary>
        /// 初始化实例
        /// </summary>
        public static ReportID ToReportID(this ReportIDEntity entity)
        {
            var report = new ReportID(entity.Code, entity.Description, entity.LinkVID);
            report.Comment = entity.Comment;
            return report;
        }

        // 保持向后兼容的旧方法名
        /// <summary>
        /// GetReportIDEntityFormReportID 实体
        /// </summary>
        [Obsolete("Use ToEntity() instead")]
        public static ReportIDEntity GetReportIDEntityFormReportID(this ReportID reportID) => reportID.ToEntity();

        /// <summary>
        /// GetReportIDFormReportIDEntity 实体
        /// </summary>
        [Obsolete("Use ToReportID() instead")]
        public static ReportID GetReportIDFormReportIDEntity(this ReportIDEntity entity) => entity.ToReportID();
    }
}
