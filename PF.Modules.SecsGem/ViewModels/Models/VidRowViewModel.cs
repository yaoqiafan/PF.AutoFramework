using Prism.Mvvm;

namespace PF.Modules.SecsGem.ViewModels
{
    public class VidRowViewModel : BindableBase
    {
        public uint Code { get; set; }
        public string Description { get; set; }
        public string DataType { get; set; }
        public string Value { get; set; }
        public string Comment { get; set; }
    }
}
