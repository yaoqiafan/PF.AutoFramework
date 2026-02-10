
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
    /// 用户登录参数表
    /// </summary>
    [Table("UserLoginParams")]
    public class UserLoginParam : ParamEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public override string ID { get; set; } = Guid.NewGuid().ToString();

    }
}
