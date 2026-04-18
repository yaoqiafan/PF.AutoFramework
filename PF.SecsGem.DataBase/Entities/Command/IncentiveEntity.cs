using PF.Core.Entities.SecsGem.Command;
using PF.SecsGem.DataBase.Entities.Basic;
using System.ComponentModel.DataAnnotations;

namespace PF.SecsGem.DataBase.Entities.Command
{
    /// <summary>
    /// BasicEntity 实体
    /// </summary>
    public class IncentiveEntity : BasicEntity
    {
        /// <summary>
        /// 初始化实例
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public override string ID { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Stream
        /// </summary>
        [Required]
        public uint Stream { get; set; }

        /// <summary>
        /// Function
        /// </summary>
        [Required]
        public uint Function { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        [Required]
        public string Name { get; set; }

        /// <summary>
        /// Key
        /// </summary>
        [Required]
        public string Key { get; set; }

        /// <summary>
        /// JsonMessage
        /// </summary>
        [Required]
        public string JsonMessage { get; set; }

        /// <summary>
        /// Response标识
        /// </summary>
        public string ResponseID { get; set; }
    }

    /// <summary>
    /// IncentiveExtend
    /// </summary>
    public static class IncentiveExtend
    {
        /// <summary>
        /// GetIncentiveEntityFormSFCommand 实体
        /// </summary>
        public static IncentiveEntity GetIncentiveEntityFormSFCommand(this SFCommand sFCommand)
        {
            return new IncentiveEntity
            {
                ID = sFCommand.ID,
                Stream = sFCommand.Stream,
                Function = sFCommand.Function,
                Name = sFCommand.Name,
                Key = sFCommand.Key,
                JsonMessage = sFCommand.ToJson(),
                ResponseID = sFCommand.ResponseID
            };
        }

        /// <summary>
        /// GetSFCommandFormIncentiveEntity 实体
        /// </summary>
        public static SFCommand GetSFCommandFormIncentiveEntity(this IncentiveEntity incentiveEntity)
        {
            return SFCommand.FromJson(incentiveEntity.JsonMessage);
        }
    }
}
