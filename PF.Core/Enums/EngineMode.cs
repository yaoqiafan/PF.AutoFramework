namespace PF.Core.Enums;

/// <summary>视觉引擎工作模式。</summary>
public enum EngineMode
{
    /// <summary>生产模式：连接真实硬件，执行正式视觉流程。</summary>
    Production = 0,
    /// <summary>调试模式：连接 HDevelop 调试服务器，可断点调试 .hdev 过程。</summary>
    Debug      = 1,
    /// <summary>离线模式：使用本地图片文件离线运行，不依赖相机硬件。</summary>
    Offline    = 2,
}
