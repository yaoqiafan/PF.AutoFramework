using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PF.Data.Entity.Category
{
    /// <summary>
    /// 通讯实例配置参数表
    ///
    /// 每条记录对应一个 CommunicationConfig 实体，通过 IParamService 泛型机制读写。
    /// 存储键：CommunicationConfig.InstanceId
    /// 存储值：CommunicationConfig 对象的 JSON 序列化结果
    /// </summary>
    [Table("CommunicationParams")]
    public class CommunicationParam : ParamEntity
    {
        /// <summary>
        /// 初始化实例
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public override string ID { get; set; } = Guid.NewGuid().ToString();
    }
}
