using Prism.Mvvm;

namespace PF.Modules.SecsGem.ViewModels
{
    public class ReportIdRowViewModel : BindableBase
    {
        public uint Code { get; set; }
        public string Description { get; set; }
        public string LinkVIDs { get; set; }
        public string Comment { get; set; }
    }
}
