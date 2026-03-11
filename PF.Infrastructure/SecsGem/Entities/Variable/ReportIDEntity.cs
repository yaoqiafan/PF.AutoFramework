using PF.Core.Entities.SecsGem.Params.ValidateParam;
using PF.Infrastructure.SecsGem.Entities.Basic;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.SecsGem.Entities.Variable
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
        public static ReportIDEntity GetReportIDEntityFormReportID(this ReportID reportID)
        {
            ReportIDEntity reportIDEntity = new ReportIDEntity();
            reportIDEntity.Code = reportID.ID;
            reportIDEntity.Description = reportID.Description;
            reportIDEntity.Comment = reportID.Comment;
            reportIDEntity.LinkVID = reportID.LinkVID;

            return reportIDEntity;
        }

        public static ReportID GetReportIDFormReportIDEntity(this ReportIDEntity reportIDEntity)
        {
            ReportID reportID = new ReportID(reportIDEntity.Code, reportIDEntity.Description, reportIDEntity.LinkVID);
            reportID.Comment = reportIDEntity.Comment;
            return reportID;
        }

    }
}
