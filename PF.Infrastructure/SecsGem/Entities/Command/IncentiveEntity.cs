using PF.Core.Entities.SecsGem.Command;
using PF.Infrastructure.SecsGem.Entities.Basic;
using System.ComponentModel.DataAnnotations;

namespace PF.Infrastructure.SecsGem.Entities.Command
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
        public string  JsonMessage { get; set; }

        public string ResponseID { get; set; }

    }



    public static class IncentiveExtend
    {
        public static IncentiveEntity GetIncentiveEntityFormSFCommand(this SFCommand sFCommand)
        {
            IncentiveEntity incentiveEntity = new IncentiveEntity();

            incentiveEntity.ID = sFCommand.ID;
            incentiveEntity.Stream = sFCommand.Stream;
            incentiveEntity.Function = sFCommand.Function;
            incentiveEntity.Name = sFCommand.Name;
            incentiveEntity.Key = sFCommand.Key;
            incentiveEntity.JsonMessage = sFCommand.ToJson();
            incentiveEntity.ResponseID = sFCommand.ResponseID;
            return incentiveEntity;
        }


        public static SFCommand GetSFCommandFormIncentiveEntity(this IncentiveEntity incentiveEntity)
        {
            SFCommand sFCommand = SFCommand.FromJson(incentiveEntity.JsonMessage);
            return sFCommand;
        }

    }
}
