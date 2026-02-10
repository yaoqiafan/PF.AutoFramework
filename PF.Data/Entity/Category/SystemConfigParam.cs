
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Data.Entity.Category
{
    /// <summary>
    /// 系统配置参数表
    /// </summary>
    [Table("SystemConfigParams")]
    public class SystemConfigParam : ParamEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public override string ID { get; set; } = Guid.NewGuid().ToString();

        
    }
}
