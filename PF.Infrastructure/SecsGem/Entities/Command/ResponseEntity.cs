using PF.Core.Entities.SecsGem.Command;
using PF.Infrastructure.SecsGem.Entities.Basic;
using System.ComponentModel.DataAnnotations;

namespace PF.Infrastructure.SecsGem.Entities.Command
{
   
    public class ResponseEntity:BasicEntity
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
            ResponseEntity responseEntity = new ResponseEntity();

            responseEntity.ID = sFCommand.ID;
            responseEntity.Stream = sFCommand.Stream;
            responseEntity.Function = sFCommand.Function;
            responseEntity.Name = sFCommand.Name;
            responseEntity.Key = sFCommand.Key;
            responseEntity.JsonMessage = sFCommand.ToJson();
          
            return responseEntity;
        }


        public static SFCommand GetSFCommandFormResponseEntity(this ResponseEntity responseEntity)
        {
            SFCommand sFCommand = SFCommand.FromJson(responseEntity.JsonMessage);
            return sFCommand;
        }

    }
}
