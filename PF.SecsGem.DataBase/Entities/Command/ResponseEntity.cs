using PF.Core.Entities.SecsGem.Command;
using PF.SecsGem.DataBase.Entities.Basic;
using System.ComponentModel.DataAnnotations;

namespace PF.SecsGem.DataBase.Entities.Command
{
    /// <summary>
    /// BasicEntity 实体
    /// </summary>
    public class ResponseEntity : BasicEntity
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
    }

    /// <summary>
    /// ResponseExtend
    /// </summary>
    public static class ResponseExtend
    {
        /// <summary>
        /// GetResponseEntityFormSFCommand 实体
        /// </summary>
        public static ResponseEntity GetResponseEntityFormSFCommand(this SFCommand sFCommand)
        {
            return new ResponseEntity
            {
                ID = sFCommand.ID,
                Stream = sFCommand.Stream,
                Function = sFCommand.Function,
                Name = sFCommand.Name,
                Key = sFCommand.Key,
                JsonMessage = sFCommand.ToJson()
            };
        }

        /// <summary>
        /// GetSFCommandFormResponseEntity 实体
        /// </summary>
        public static SFCommand GetSFCommandFormResponseEntity(this ResponseEntity responseEntity)
        {
            return SFCommand.FromJson(responseEntity.JsonMessage);
        }
    }
}
