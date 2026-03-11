using NPOI.SS.Formula.Functions;
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
        public static CEIDEntity GetCEIDEntityFormCEID(this CEID cEID )
        {
            CEIDEntity cEIDEntity=new CEIDEntity();

            cEIDEntity.Code = cEID.ID;
            cEIDEntity.Description=cEID.Description;
            cEIDEntity.Comment=cEID.Comment;
            cEIDEntity.LinkReportCode = cEID.LinkReportID;
            cEIDEntity.Key = cEID.Key;
            return cEIDEntity;
        }


        public static CEID GetCEIDFormCEIDEntity(this CEIDEntity cEIDEntity)
        {
            CEID cEID = new CEID(cEIDEntity.Code, cEIDEntity.Description, cEIDEntity.LinkReportCode, cEIDEntity.Key);
            cEID.Comment = cEIDEntity.Comment;
            return cEID;
        }

    }
}
