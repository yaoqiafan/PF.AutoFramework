using Prism.Mvvm;

namespace PF.Modules.SecsGem.ViewModels
{
    public class CeidRowViewModel : BindableBase
    {
        public uint Code { get; set; }
        public string Description { get; set; }
        public string LinkReportIDs { get; set; }
        public string Comment { get; set; }
    }
}
