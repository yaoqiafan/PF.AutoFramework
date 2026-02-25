using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PF.Data.Entity.Category
{
    /// <summary>
    /// 硬件配置参数表
    ///
    /// 每条记录对应一个 HardwareConfig 实体，通过 IParamService 泛型机制读写。
    /// 存储键：HardwareConfig.DeviceId（如 "SIM_CARD_0", "SIM_X_AXIS_0"）
    /// 存储值：HardwareConfig 对象的 JSON 序列化结果
    /// </summary>
    [Table("HardwareParams")]
    public class HardwareParam : ParamEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public override string ID { get; set; } = Guid.NewGuid().ToString();
    }
}
