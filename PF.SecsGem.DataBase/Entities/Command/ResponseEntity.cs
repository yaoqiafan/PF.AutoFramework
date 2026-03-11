using PF.Core.Entities.SecsGem.Command;
using PF.SecsGem.DataBase.Entities.Basic;
using System.ComponentModel.DataAnnotations;

namespace PF.SecsGem.DataBase.Entities.Command
{
    public class ResponseEntity : BasicEntity
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
    }

    public static class ResponseExtend
    {
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

        public static SFCommand GetSFCommandFormResponseEntity(this ResponseEntity responseEntity)
        {
            return SFCommand.FromJson(responseEntity.JsonMessage);
        }
    }
}
