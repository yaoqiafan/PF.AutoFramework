
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
    /// 通用参数表
    /// </summary>
    [Table("CommonParams")]
    public class CommonParam : ParamEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public override string ID { get; set; } = Guid.NewGuid().ToString();
    }
}
