using PF.Core.Entities.Speech;
using PF.Core.Enums;

namespace PF.Core.Interfaces.Speech
{
    /// <summary>
    /// 语音播报服务：文字转语音，经系统默认扬声器播报，或输出为 WAV 文件。
    /// <para>
    /// 播报全局串行排队，<see cref="Speak"/> 入队后立即返回，不阻塞调用方（工站流程里可直接调用）。
    /// 引擎不可用（系统没有可用语音包、初始化失败）时，所有播报调用都是空操作，不抛异常，不影响主流程。
    /// </para>
    /// <para>
    /// 语音 / 音量 / 语速 / 静音 / 常用语保存在 <c>{ConfigPath}\speech_config.json</c>，修改后自动保存（防抖 500ms），
    /// 启动时自动加载。
    /// </para>
    /// </summary>
    public interface ISpeechService : IDisposable
    {
        #region 状态

        /// <summary>引擎是否可用（已找到语音包并初始化成功）。为 false 时播报为空操作、生成文件返回 false。</summary>
        bool IsAvailable { get; }

        /// <summary>正在播报的文字；空闲时为 null。</summary>
        string? CurrentText { get; }

        /// <summary>排队中的高优先级播报数（不含正在播的一句）。</summary>
        int PendingHighCount { get; }

        /// <summary>排队中的普通优先级播报数（不含正在播的一句）。</summary>
        int PendingNormalCount { get; }

        #endregion

        #region 设置（修改后自动保存）

        /// <summary>
        /// 静音。置 true 时清空排队并打断当前播报，此后的播报请求直接丢弃（<see cref="SpeakAsync"/> 立即完成）；
        /// 不影响 <see cref="SaveToWaveFileAsync"/>。
        /// </summary>
        bool IsMuted { get; set; }

        /// <summary>音量 0~100，超出范围自动截断。从下一句开始生效。</summary>
        int Volume { get; set; }

        /// <summary>语速 -10（最慢）~ 10（最快），0 为正常，超出范围自动截断。从下一句开始生效。</summary>
        int Rate { get; set; }

        /// <summary>当前使用的语音名称；引擎不可用时为 null。默认优先选 zh-CN 语音。</summary>
        string? CurrentVoice { get; }

        /// <summary>系统已安装且启用的语音名称列表（引擎不可用时为空）。</summary>
        IReadOnlyList<string> GetInstalledVoices();

        /// <summary>切换语音，名称取自 <see cref="GetInstalledVoices"/>。名称不存在返回 false。从下一句开始生效。</summary>
        bool SelectVoice(string voiceName);

        /// <summary>常用语列表（副本）。</summary>
        IReadOnlyList<SpeechPhrase> GetPhrases();

        /// <summary>整体替换常用语列表并保存。空文字的条目会被忽略。</summary>
        void SetPhrases(IEnumerable<SpeechPhrase> phrases);

        /// <summary>语音 / 音量 / 语速 / 静音恢复默认（zh-CN 优先、100、0、不静音）并保存；常用语不变。</summary>
        void ResetSettings();

        /// <summary>设置文件完整路径；配置路径未初始化（非主程序宿主）时为 null，此时设置不持久化。</summary>
        string? ConfigFilePath { get; }

        /// <summary>最近一次成功保存设置的时间；本次运行尚未保存过为 null。</summary>
        DateTime? LastSavedTime { get; }

        /// <summary>设置保存成功后触发（在后台线程上）。</summary>
        event EventHandler? SettingsSaved;

        #endregion

        #region 播报

        /// <summary>入队播报，立即返回。空文本或引擎不可用时直接忽略；静音时丢弃（记录为 <see cref="SpeechStatus.Muted"/>）。</summary>
        /// <param name="text">播报文字</param>
        /// <param name="priority">优先级</param>
        /// <param name="source">来源（如工站名），只用于播报记录与排查，可为 null</param>
        void Speak(string text, SpeechPriority priority = SpeechPriority.Normal, string? source = null);

        /// <summary>
        /// 入队播报，并在这一句结束时完成。
        /// 正常播完、被高优先级打断、被 <see cref="CancelAll"/> / 静音清掉、播放出错，都按"正常完成"结束（出错只记日志）；
        /// 只有 <paramref name="token"/> 取消时才抛 <see cref="OperationCanceledException"/>（排队中则移出队列，播放中则立即停止）。
        /// </summary>
        /// <param name="text">播报文字</param>
        /// <param name="priority">优先级</param>
        /// <param name="source">来源（如工站名），只用于播报记录与排查，可为 null</param>
        /// <param name="token">取消令牌</param>
        Task SpeakAsync(string text, SpeechPriority priority = SpeechPriority.Normal, string? source = null, CancellationToken token = default);

        /// <summary>清空所有排队播报并打断当前播报。</summary>
        void CancelAll();

        #endregion

        #region 播报记录

        /// <summary>最近的播报记录（最多 200 条，新的在前），只保存在内存中。</summary>
        IReadOnlyList<SpeechRecord> GetRecentRecords();

        /// <summary>清空播报记录（排队中 / 播放中的那几句之后仍会继续推送快照）。</summary>
        void ClearRecords();

        /// <summary>
        /// 播报记录有新快照时触发：入队、开始播放、结束各一次，同一句的 <see cref="SpeechRecord.Id"/> 相同。
        /// 在后台线程上触发，界面订阅需自行切回 UI 线程；处理函数抛出的异常会被吞掉。
        /// </summary>
        event EventHandler<SpeechRecord>? RecordUpdated;

        #endregion

        #region 生成文件

        /// <summary>
        /// 文字转 WAV 文件，不经过扬声器、不占用播报队列，沿用当前的语音 / 语速 / 音量。
        /// 目录不存在时自动创建，文件已存在时覆盖；失败（含引擎不可用）返回 false 并记日志，不留残缺文件。
        /// <paramref name="token"/> 取消时抛 <see cref="OperationCanceledException"/>。
        /// </summary>
        /// <param name="text">要合成的文字</param>
        /// <param name="filePath">输出文件完整路径（建议 .wav 扩展名）</param>
        /// <param name="token">取消令牌</param>
        Task<bool> SaveToWaveFileAsync(string text, string filePath, CancellationToken token = default);

        #endregion
    }
}
