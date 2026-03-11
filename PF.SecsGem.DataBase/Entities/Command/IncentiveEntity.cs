using PF.Core.Entities.SecsGem.Command;
using PF.SecsGem.DataBase.Entities.Basic;
using System.ComponentModel.DataAnnotations;

namespace PF.SecsGem.DataBase.Entities.Command
{
    public class IncentiveEntity : BasicEntity
    {
        [Required(AllowEmptyStrings = false)]
        public override string ID { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public uint Stream { get; set; }

        [Required]
        public uint Function { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string Key { get; set; }

        [Required]
        public string JsonMessage { get; set; }

        public string ResponseID { get; set; }
    }

    public static class IncentiveExtend
    {
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

        public static SFCommand GetSFCommandFormIncentiveEntity(this IncentiveEntity incentiveEntity)
        {
            return SFCommand.FromJson(incentiveEntity.JsonMessage);
        }
    }
}
