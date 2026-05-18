using Prism.Events;
using System.Threading;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr
{
    /// <summary>OCR 不匹配时操作员的处置动作。</summary>
    public enum OcrMismatchAction
    {
        /// <summary>重新拍照识别。</summary>
        Retry,
        /// <summary>操作员手工录入 OCR 文本。</summary>
        ManualInput
    }

    /// <summary>OCR 不匹配弹窗的操作结果。</summary>
    public class OcrMismatchResult
    {
        /// <summary>操作员选择的处置动作。</summary>
        public OcrMismatchAction Action        { get; set; }
        /// <summary>手动录入模式下操作员填写的 OCR 文本。</summary>
        public string            ManualOcrText { get; set; } = string.Empty;
    }

    /// <summary>OCR 不匹配事件的载荷，包含触发上下文与异步完成源。</summary>
    public class OcrMismatchPayload
    {
        /// <summary>触发不匹配事件的工位名称。</summary>
        public string            WorkSpaceName    { get; set; } = string.Empty;
        /// <summary>本次 OCR 识别结果文本。</summary>
        public string            OcrText          { get; set; } = string.Empty;
        /// <summary>当前批次的内部批号。</summary>
        public string            InternalBatchId  { get; set; } = string.Empty;
        /// <summary>工站主循环的取消令牌，用于在工站停止时中止等待。</summary>
        public CancellationToken StationToken     { get; set; }
        /// <summary>UI 层回填操作结果的异步完成源。</summary>
        public TaskCompletionSource<OcrMismatchResult> Tcs { get; set; }
            = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>OCR 识别结果与预期不符时发布的 Prism 事件，触发 UI 弹窗让操作员处置。</summary>
    public class OcrMismatchRequestedEvent : PubSubEvent<OcrMismatchPayload> { }
}
