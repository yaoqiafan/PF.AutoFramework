using Prism.Events;
using System.Threading;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr
{
    public enum OcrMismatchAction { Retry, ManualInput }

    public class OcrMismatchResult
    {
        public OcrMismatchAction Action        { get; set; }
        public string            ManualOcrText { get; set; } = string.Empty;
    }

    public class OcrMismatchPayload
    {
        public string            WorkSpaceName    { get; set; } = string.Empty;
        public string            OcrText          { get; set; } = string.Empty;
        public string            InternalBatchId  { get; set; } = string.Empty;
        public CancellationToken StationToken     { get; set; }
        public TaskCompletionSource<OcrMismatchResult> Tcs { get; set; }
            = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public class OcrMismatchRequestedEvent : PubSubEvent<OcrMismatchPayload> { }
}
