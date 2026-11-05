using PF.Core.Constants;
using PF.Core.Entities.Speech;
using PF.Core.Enums;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Speech;
using System.IO;
using System.Speech.Synthesis;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace PF.CommonTools.Speech
{
    /// <summary>
    /// 基于 System.Speech（SAPI5，离线）的语音播报服务实现。
    /// <para>
    /// 线程模型：播放用的 <see cref="SpeechSynthesizer"/> 在专用后台线程上创建和使用。
    /// 该线程没有 SynchronizationContext，合成器的完成事件因此走线程池回调，
    /// 不会被封送回 WPF UI 线程（UI 线程同步等待播报时不会死锁）。
    /// 音量 / 语速 / 语音只记在字段里，由工作线程在每句开播前写入合成器，跨线程只调用 SpeakAsyncCancel。
    /// </para>
    /// <para>生成 WAV 文件时每次新建独立的合成器，不和播放共用，否则 SetOutputToWaveFile 会抢走扬声器输出。</para>
    /// <para>设置保存在 {ConfigPath}\speech_config.json：修改后防抖 500ms 写盘（先写临时文件再替换），Dispose 时补写未落盘的修改。</para>
    /// </summary>
    public sealed class SpeechService : ISpeechService
    {
        private const string LogTag = "【语音播报】";
        private const string ConfigFileName = "speech_config.json";

        /// <summary>普通优先级排队上限，超出时丢弃最早的一句，防止循环里反复调用导致积压。</summary>
        private const int MaxNormalQueueLength = 50;

        /// <summary>播报记录保留条数。</summary>
        private const int MaxRecords = 200;

        private const int DefaultVolume = 100;
        private const int DefaultRate = 0;
        private const int SaveDebounceMs = 500;

        private static readonly TimeSpan InitTimeout = TimeSpan.FromSeconds(10);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping   // 中文不转义，方便现场直接查看
        };

        private readonly ILogService _logger;

        // —— 播报队列 ——
        private readonly object _queueLock = new();
        private readonly Queue<SpeechJob> _highQueue = new();
        private readonly Queue<SpeechJob> _normalQueue = new();
        private readonly SemaphoreSlim _signal = new(0);
        private readonly CancellationTokenSource _disposeCts = new();
        private readonly Thread _worker;
        private SpeechJob? _current;                     // 受 _queueLock 保护
        private bool _overflowWarned;                    // 受 _queueLock 保护

        // —— 引擎 ——
        private SpeechSynthesizer? _synth;              // 只在工作线程创建和配置；跨线程只调 SpeakAsyncCancel
        private IReadOnlyList<string> _voices = Array.Empty<string>();
        private string? _defaultVoice;                   // 初始化时选出的默认语音（zh-CN 优先）
        private string? _appliedVoice;                   // 仅工作线程访问
        private volatile string? _voice;
        private volatile bool _available;
        private volatile bool _lastPlayFailed;
        private int _disposed;

        // —— 设置 ——
        private volatile bool _muted;
        private int _volume = DefaultVolume;
        private int _rate = DefaultRate;
        private readonly object _phraseLock = new();
        private List<SpeechPhrase> _phrases = new();
        private readonly Timer _saveTimer;
        private int _savePending;
        private readonly object _saveLock = new();
        private DateTime? _lastSavedTime;

        // —— 播报记录 ——
        private readonly object _recordLock = new();
        private readonly LinkedList<SpeechRecord> _records = new();   // 新的在前
        private readonly Dictionary<long, LinkedListNode<SpeechRecord>> _recordIndex = new();
        private long _recordSeq;

        /// <summary>创建服务：加载设置文件，并在专用后台线程上初始化引擎（最多等待 10 秒）。</summary>
        public SpeechService(ILogService logger)
        {
            _logger = logger;
            ConfigFilePath = ResolveConfigFilePath();
            _saveTimer = new Timer(_ => SaveNow(), null, Timeout.Infinite, Timeout.Infinite);

            var ready = new ManualResetEventSlim(false);
            _worker = new Thread(() => WorkerMain(ready))
            {
                IsBackground = true,
                Name = "PF.SpeechService"
            };
            _worker.Start();

            // 初始化超时不当作失败：工作线程稍后完成初始化时 IsAvailable 会自动变为 true
            if (!ready.Wait(InitTimeout))
                _logger.Warn($"{LogTag}引擎初始化超过 {InitTimeout.TotalSeconds:0} 秒仍未完成，暂按不可用处理");
        }

        #region 状态

        /// <inheritdoc/>
        public bool IsAvailable => _available;

        /// <inheritdoc/>
        public string? CurrentText
        {
            get { lock (_queueLock) return _current?.Text; }
        }

        /// <inheritdoc/>
        public int PendingHighCount
        {
            get { lock (_queueLock) return _highQueue.Count(j => !j.Completion.Task.IsCompleted); }
        }

        /// <inheritdoc/>
        public int PendingNormalCount
        {
            get { lock (_queueLock) return _normalQueue.Count(j => !j.Completion.Task.IsCompleted); }
        }

        #endregion

        #region 设置

        /// <inheritdoc/>
        public bool IsMuted
        {
            get => _muted;
            set
            {
                if (_muted == value) return;
                _muted = value;
                if (value) CancelAll();
                _logger.Info($"{LogTag}静音已{(value ? "开启" : "关闭")}");
                ScheduleSave();
            }
        }

        /// <inheritdoc/>
        public int Volume
        {
            get => Volatile.Read(ref _volume);
            set
            {
                var v = Math.Clamp(value, 0, 100);
                if (Interlocked.Exchange(ref _volume, v) != v) ScheduleSave();
            }
        }

        /// <inheritdoc/>
        public int Rate
        {
            get => Volatile.Read(ref _rate);
            set
            {
                var v = Math.Clamp(value, -10, 10);
                if (Interlocked.Exchange(ref _rate, v) != v) ScheduleSave();
            }
        }

        /// <inheritdoc/>
        public string? CurrentVoice => _voice;

        /// <inheritdoc/>
        public IReadOnlyList<string> GetInstalledVoices() => _voices;

        /// <inheritdoc/>
        public bool SelectVoice(string voiceName)
        {
            if (string.IsNullOrWhiteSpace(voiceName) || !_voices.Contains(voiceName))
            {
                _logger.Warn($"{LogTag}切换语音失败，未安装该语音：{voiceName}");
                return false;
            }
            if (_voice == voiceName) return true;

            _voice = voiceName;
            _logger.Info($"{LogTag}已切换语音：{voiceName}");
            ScheduleSave();
            return true;
        }

        /// <inheritdoc/>
        public IReadOnlyList<SpeechPhrase> GetPhrases()
        {
            lock (_phraseLock)
                return _phrases.Select(p => new SpeechPhrase { Text = p.Text, IsHighPriority = p.IsHighPriority }).ToList();
        }

        /// <inheritdoc/>
        public void SetPhrases(IEnumerable<SpeechPhrase> phrases)
        {
            var list = (phrases ?? Enumerable.Empty<SpeechPhrase>())
                .Where(p => p != null && !string.IsNullOrWhiteSpace(p.Text))
                .Select(p => new SpeechPhrase { Text = p.Text.Trim(), IsHighPriority = p.IsHighPriority })
                .ToList();
            lock (_phraseLock) _phrases = list;
            ScheduleSave();
        }

        /// <inheritdoc/>
        public void ResetSettings()
        {
            if (_defaultVoice != null) _voice = _defaultVoice;
            Volatile.Write(ref _volume, DefaultVolume);
            Volatile.Write(ref _rate, DefaultRate);
            _muted = false;
            _logger.Info($"{LogTag}设置已恢复默认：语音 {_voice ?? "-"}，音量 {DefaultVolume}，语速 {DefaultRate}，不静音");
            ScheduleSave();
        }

        /// <inheritdoc/>
        public string? ConfigFilePath { get; }

        /// <inheritdoc/>
        public DateTime? LastSavedTime
        {
            get { lock (_saveLock) return _lastSavedTime; }
        }

        /// <inheritdoc/>
        public event EventHandler? SettingsSaved;

        private string? ResolveConfigFilePath()
        {
            try
            {
                return Path.Combine(ConstGlobalParam.ConfigPath, ConfigFileName);
            }
            catch (InvalidOperationException)
            {
                // 非主程序宿主（工具、测试）没有初始化配置路径：照常播报，只是设置不落盘
                _logger.Warn($"{LogTag}配置路径未初始化，语音设置不会保存");
                return null;
            }
        }

        /// <summary>读取设置文件。只在工作线程初始化时调用，此时还没有任何播报，不需要加锁。</summary>
        private SpeechSettingsFile? LoadSettingsFile()
        {
            if (ConfigFilePath == null || !File.Exists(ConfigFilePath)) return null;
            try
            {
                var settings = JsonSerializer.Deserialize<SpeechSettingsFile>(File.ReadAllText(ConfigFilePath), JsonOptions);
                if (settings == null) return null;

                Volatile.Write(ref _volume, Math.Clamp(settings.Volume, 0, 100));
                Volatile.Write(ref _rate, Math.Clamp(settings.Rate, -10, 10));
                _muted = settings.IsMuted;
                lock (_phraseLock)
                {
                    _phrases = (settings.Phrases ?? new List<SpeechPhrase>())
                        .Where(p => p != null && !string.IsNullOrWhiteSpace(p.Text))
                        .ToList();
                }
                _logger.Info($"{LogTag}已加载设置：音量 {Volume}，语速 {Rate}，{(_muted ? "静音" : "未静音")}，常用语 {_phrases.Count} 条");
                return settings;
            }
            catch (Exception ex)
            {
                _logger.Warn($"{LogTag}读取设置文件失败，使用默认设置：{ConfigFilePath}，{ex.Message}");
                return null;
            }
        }

        private void ScheduleSave()
        {
            if (ConfigFilePath == null || Volatile.Read(ref _disposed) == 1) return;
            Interlocked.Exchange(ref _savePending, 1);
            _saveTimer.Change(SaveDebounceMs, Timeout.Infinite);
        }

        private void SaveNow()
        {
            if (ConfigFilePath == null || Interlocked.Exchange(ref _savePending, 0) == 0) return;

            var settings = new SpeechSettingsFile
            {
                VoiceName = _voice,
                Volume = Volume,
                Rate = Rate,
                IsMuted = _muted,
                Phrases = GetPhrases().ToList()
            };

            lock (_saveLock)
            {
                try
                {
                    var dir = Path.GetDirectoryName(ConfigFilePath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                    // 先写临时文件再整体替换，写到一半断电也不会留下半截 JSON
                    var tmp = ConfigFilePath + ".tmp";
                    File.WriteAllText(tmp, JsonSerializer.Serialize(settings, JsonOptions));
                    File.Move(tmp, ConfigFilePath, overwrite: true);
                    _lastSavedTime = DateTime.Now;
                }
                catch (Exception ex)
                {
                    _logger.Warn($"{LogTag}保存设置失败：{ConfigFilePath}，{ex.Message}");
                    return;
                }
            }

            try { SettingsSaved?.Invoke(this, EventArgs.Empty); }
            catch (Exception ex) { _logger.Debug($"{LogTag}SettingsSaved 处理函数异常（已忽略）：{ex.Message}"); }
        }

        #endregion

        #region 播报

        /// <inheritdoc/>
        public void Speak(string text, SpeechPriority priority = SpeechPriority.Normal, string? source = null)
            => Enqueue(text, priority, source, CancellationToken.None);

        /// <inheritdoc/>
        public Task SpeakAsync(string text, SpeechPriority priority = SpeechPriority.Normal, string? source = null, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            var job = Enqueue(text, priority, source, token);
            return job?.Completion.Task ?? Task.CompletedTask;
        }

        /// <inheritdoc/>
        public void CancelAll()
        {
            List<SpeechJob> pending;
            lock (_queueLock)
            {
                pending = new List<SpeechJob>(_highQueue.Count + _normalQueue.Count);
                pending.AddRange(_highQueue);
                pending.AddRange(_normalQueue);
                _highQueue.Clear();
                _normalQueue.Clear();
                _overflowWarned = false;
            }
            // 信号量计数不回收：工作线程取到空队列会直接跳过
            foreach (var job in pending)
            {
                job.Complete();
                Finish(job, SpeechStatus.Cancelled);
            }

            InterruptCurrent(onlyNormal: false);
        }

        private SpeechJob? Enqueue(string text, SpeechPriority priority, string? source, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(text) || !_available || Volatile.Read(ref _disposed) == 1)
                return null;

            var job = new SpeechJob(Interlocked.Increment(ref _recordSeq), text.Trim(), priority,
                                    string.IsNullOrWhiteSpace(source) ? null : source.Trim());

            if (_muted)
            {
                Finish(job, SpeechStatus.Muted);
                return null;
            }

            SpeechJob? dropped = null;
            bool warnOverflow = false;
            bool interrupt = false;

            lock (_queueLock)
            {
                if (priority == SpeechPriority.High)
                {
                    _highQueue.Enqueue(job);
                    interrupt = _current is { Priority: SpeechPriority.Normal };
                }
                else
                {
                    if (_normalQueue.Count >= MaxNormalQueueLength)
                    {
                        dropped = _normalQueue.Dequeue();
                        warnOverflow = !_overflowWarned;
                        _overflowWarned = true;
                    }
                    _normalQueue.Enqueue(job);
                }
            }

            Publish(job.Snapshot(SpeechStatus.Queued));

            if (dropped != null)
            {
                dropped.Complete();
                Finish(dropped, SpeechStatus.Dropped);
            }
            if (warnOverflow)
                _logger.Warn($"{LogTag}排队超过 {MaxNormalQueueLength} 句，开始丢弃最早的普通播报（请检查是否在循环中重复调用）");

            _signal.Release();

            if (interrupt) InterruptCurrent(onlyNormal: true);

            if (token.CanBeCanceled)
            {
                job.Registration = token.Register(() => CancelJob(job, token));
                if (job.Completion.Task.IsCompleted) job.Registration.Unregister();
            }
            return job;
        }

        private void CancelJob(SpeechJob job, CancellationToken token)
        {
            if (!job.Completion.TrySetCanceled(token)) return;

            bool isCurrent;
            lock (_queueLock) isCurrent = ReferenceEquals(_current, job);

            // 播放中：立即停止，结束记录由工作线程写；排队中：工作线程会跳过它，记录在这里写
            if (isCurrent) Interrupt(job);
            else Finish(job, SpeechStatus.Cancelled);
        }

        private void InterruptCurrent(bool onlyNormal)
        {
            SpeechJob? job;
            lock (_queueLock) job = _current;

            if (job == null || (onlyNormal && job.Priority != SpeechPriority.Normal)) return;
            Interrupt(job);
        }

        private void Interrupt(SpeechJob job)
        {
            // 先置标记再读 Prompt：工作线程是先写 Prompt、SpeakAsync 后再查标记，两边总有一方能取消到
            job.InterruptRequested = true;
            var prompt = job.Prompt;
            if (prompt != null) TryCancelPrompt(prompt);
        }

        private void TryCancelPrompt(Prompt prompt)
        {
            try
            {
                _synth?.SpeakAsyncCancel(prompt);
            }
            catch (Exception ex)
            {
                _logger.Debug($"{LogTag}取消播报时出现异常（已忽略）：{ex.Message}");
            }
        }

        #endregion

        #region 播报记录

        /// <inheritdoc/>
        public event EventHandler<SpeechRecord>? RecordUpdated;

        /// <inheritdoc/>
        public IReadOnlyList<SpeechRecord> GetRecentRecords()
        {
            lock (_recordLock) return _records.ToList();
        }

        /// <inheritdoc/>
        public void ClearRecords()
        {
            lock (_recordLock)
            {
                _records.Clear();
                _recordIndex.Clear();
            }
        }

        /// <summary>写入结束状态。每句只认第一次结束（取消与播放结束可能并发到达）。</summary>
        private void Finish(SpeechJob job, SpeechStatus status, string? error = null)
        {
            if (Interlocked.Exchange(ref job.Finished, 1) == 1) return;
            Publish(job.Snapshot(status, DateTime.Now, error));
        }

        private void Publish(SpeechRecord record)
        {
            lock (_recordLock)
            {
                if (_recordIndex.TryGetValue(record.Id, out var node))
                {
                    node.Value = record;
                }
                else if (!record.IsFinished || record.Status is SpeechStatus.Muted)
                {
                    // 清空记录后才结束的那几句不再补回来；静音丢弃是一次性记录，照常加入
                    _recordIndex[record.Id] = _records.AddFirst(record);
                    while (_records.Count > MaxRecords)
                    {
                        _recordIndex.Remove(_records.Last!.Value.Id);
                        _records.RemoveLast();
                    }
                }
            }

            var handler = RecordUpdated;
            if (handler == null) return;
            try { handler(this, record); }
            catch (Exception ex) { _logger.Debug($"{LogTag}RecordUpdated 处理函数异常（已忽略）：{ex.Message}"); }
        }

        #endregion

        #region 工作线程

        private void WorkerMain(ManualResetEventSlim ready)
        {
            bool initialized;
            try
            {
                initialized = TryInitialize();
            }
            finally
            {
                ready.Set();
            }
            if (!initialized) return;

            try
            {
                RunLoop(_disposeCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Dispose 退出
            }
            catch (Exception ex)
            {
                _available = false;
                _logger.Error($"{LogTag}播报线程异常退出，语音播报已停用：{ex.Message}", exception: ex);
            }
            finally
            {
                var synth = _synth;
                _synth = null;
                if (synth != null)
                {
                    synth.SpeakCompleted -= OnSpeakCompleted;
                    try { synth.Dispose(); } catch { /* 退出阶段忽略 */ }
                }
            }
        }

        private bool TryInitialize()
        {
            var settings = LoadSettingsFile();

            SpeechSynthesizer? synth = null;
            try
            {
                synth = new SpeechSynthesizer();
                var voices = synth.GetInstalledVoices()
                                  .Where(v => v.Enabled)
                                  .Select(v => v.VoiceInfo)
                                  .ToList();
                if (voices.Count == 0)
                {
                    _logger.Warn($"{LogTag}系统未安装可用的语音包，语音播报不可用");
                    synth.Dispose();
                    return false;
                }

                var preferred = voices.FirstOrDefault(v => string.Equals(v.Culture?.Name, "zh-CN", StringComparison.OrdinalIgnoreCase))
                                ?? voices[0];
                _defaultVoice = preferred.Name;

                var voice = preferred.Name;
                if (!string.IsNullOrWhiteSpace(settings?.VoiceName))
                {
                    if (voices.Any(v => v.Name == settings!.VoiceName))
                        voice = settings!.VoiceName!;
                    else
                        _logger.Warn($"{LogTag}设置中的语音「{settings!.VoiceName}」本机未安装，改用「{preferred.Name}」");
                }

                synth.SelectVoice(voice);
                synth.SetOutputToDefaultAudioDevice();
                synth.SpeakCompleted += OnSpeakCompleted;

                _voices = voices.Select(v => v.Name).ToList();
                _voice = voice;
                _appliedVoice = voice;
                _synth = synth;
                _available = true;

                _logger.Info($"{LogTag}引擎就绪，当前语音：{voice}，共 {voices.Count} 个可用语音");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Warn($"{LogTag}引擎初始化失败，语音播报不可用：{ex.Message}");
                try { synth?.Dispose(); } catch { /* 忽略 */ }
                return false;
            }
        }

        private void RunLoop(CancellationToken token)
        {
            while (true)
            {
                _signal.Wait(token);

                SpeechJob? job;
                lock (_queueLock)
                {
                    if (_highQueue.Count > 0) job = _highQueue.Dequeue();
                    else if (_normalQueue.Count > 0) job = _normalQueue.Dequeue();
                    else continue;                          // CancelAll 清过队列，信号计数滞后

                    if (_normalQueue.Count == 0) _overflowWarned = false;
                    if (job.Completion.Task.IsCompleted) continue;   // 排队期间已被取消 / 清除，记录已写过
                    _current = job;
                }

                try
                {
                    Play(job, token);
                }
                finally
                {
                    lock (_queueLock) _current = null;

                    var status = job.Error != null ? SpeechStatus.Failed
                               : job.Completion.Task.IsCanceled || token.IsCancellationRequested ? SpeechStatus.Cancelled
                               : job.InterruptRequested ? SpeechStatus.Interrupted
                               : SpeechStatus.Completed;
                    job.Complete();
                    Finish(job, status, job.Error);
                }
            }
        }

        private void Play(SpeechJob job, CancellationToken token)
        {
            var synth = _synth!;
            ApplySettings(synth);

            var prompt = new Prompt(job.Text);
            job.Prompt = prompt;            // 必须先于 SpeakAsync，见 Interrupt 中的说明
            job.StartTime = DateTime.Now;
            Publish(job.Snapshot(SpeechStatus.Playing));

            synth.SpeakAsync(prompt);
            if (job.InterruptRequested) TryCancelPrompt(prompt);

            try
            {
                job.PlayDone.Task.Wait(token);
            }
            catch (OperationCanceledException)
            {
                TryCancelPrompt(prompt);
                throw;
            }
        }

        private void ApplySettings(SpeechSynthesizer synth)
        {
            synth.Volume = Volume;
            synth.Rate = Rate;

            var voice = _voice;
            if (voice != null && voice != _appliedVoice)
            {
                try
                {
                    synth.SelectVoice(voice);
                    _appliedVoice = voice;
                }
                catch (Exception ex)
                {
                    _logger.Warn($"{LogTag}应用语音「{voice}」失败，继续使用「{_appliedVoice}」：{ex.Message}");
                    _voice = _appliedVoice;
                }
            }
        }

        private void OnSpeakCompleted(object? sender, SpeakCompletedEventArgs e)
        {
            SpeechJob? job;
            lock (_queueLock) job = _current;
            if (job == null || !ReferenceEquals(job.Prompt, e.Prompt)) return;

            if (IsCancelled(e))
            {
                // 被打断 / 取消，不算失败
            }
            else if (e.Error != null)
            {
                job.Error = e.Error.Message;
                // 连续失败只记一次（例如没有声卡时每句都会失败），恢复后再失败会重新记录
                if (!_lastPlayFailed)
                    _logger.Warn($"{LogTag}播报失败（请检查音频设备）：{e.Error.Message}");
                _lastPlayFailed = true;
            }
            else
            {
                _lastPlayFailed = false;
            }
            job.PlayDone.TrySetResult();
        }

        /// <summary>
        /// SpeakAsyncCancel 取消的一句，System.Speech 实测不置 Cancelled，而是把 OperationCanceledException
        /// 放进 Error（"Async operation canceled."），两种都要按"取消"处理，否则会被误报成播放失败。
        /// </summary>
        private static bool IsCancelled(SpeakCompletedEventArgs e)
            => e.Cancelled || e.Error is OperationCanceledException;

        #endregion

        #region 生成 WAV 文件

        /// <inheritdoc/>
        public Task<bool> SaveToWaveFileAsync(string text, string filePath, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("合成文字不能为空", nameof(text));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("输出路径不能为空", nameof(filePath));
            token.ThrowIfCancellationRequested();

            if (!_available)
            {
                _logger.Warn($"{LogTag}引擎不可用，无法生成语音文件：{filePath}");
                return Task.FromResult(false);
            }

            // 线程池线程没有 SynchronizationContext，合成器完成事件同样走线程池，同步等待不会死锁
            return Task.Run(() => SaveCore(text.Trim(), filePath, token), token);
        }

        private bool SaveCore(string text, string filePath, CancellationToken token)
        {
            string fullPath = Path.GetFullPath(filePath);
            bool ok = false;
            bool cancelled = false;

            try
            {
                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                using var synth = new SpeechSynthesizer();
                var voice = _voice;
                if (voice != null) synth.SelectVoice(voice);
                synth.Volume = Volume;
                synth.Rate = Rate;
                synth.SetOutputToWaveFile(fullPath);

                var done = new TaskCompletionSource<SpeakCompletedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
                synth.SpeakCompleted += (_, e) => done.TrySetResult(e);
                var prompt = synth.SpeakAsync(text);

                SpeakCompletedEventArgs result;
                using (token.Register(() =>
                {
                    try { synth.SpeakAsyncCancel(prompt); } catch { /* 已结束 */ }
                }))
                {
                    result = done.Task.GetAwaiter().GetResult();
                }

                synth.SetOutputToNull();    // 关闭文件句柄，之后才能安全删除残缺文件

                if (IsCancelled(result))
                    cancelled = true;
                else if (result.Error != null)
                    _logger.Warn($"{LogTag}生成语音文件失败：{fullPath}，{result.Error.Message}");
                else
                    ok = true;
            }
            catch (Exception ex)
            {
                _logger.Warn($"{LogTag}生成语音文件失败：{fullPath}，{ex.Message}");
            }

            if (!ok) TryDeleteFile(fullPath);
            if (cancelled) throw new OperationCanceledException(token);

            if (ok) _logger.Info($"{LogTag}已生成语音文件：{fullPath}");
            return ok;
        }

        private void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                _logger.Debug($"{LogTag}删除残缺语音文件失败（已忽略）：{path}，{ex.Message}");
            }
        }

        #endregion

        /// <inheritdoc/>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

            _available = false;
            CancelAll();
            _disposeCts.Cancel();
            _worker.Join(TimeSpan.FromSeconds(2));

            // 补写防抖窗口内尚未落盘的修改
            _saveTimer.Dispose();
            SaveNow();
        }

        /// <summary>speech_config.json 的文件格式</summary>
        private sealed class SpeechSettingsFile
        {
            public string? VoiceName { get; set; }
            public int Volume { get; set; } = DefaultVolume;
            public int Rate { get; set; } = DefaultRate;
            public bool IsMuted { get; set; }
            public List<SpeechPhrase>? Phrases { get; set; }
        }

        private sealed class SpeechJob
        {
            public SpeechJob(long id, string text, SpeechPriority priority, string? source)
            {
                Id = id;
                Text = text;
                Priority = priority;
                Source = source;
                RequestTime = DateTime.Now;
            }

            public long Id { get; }
            public string Text { get; }
            public SpeechPriority Priority { get; }
            public string? Source { get; }
            public DateTime RequestTime { get; }
            public DateTime? StartTime { get; set; }

            /// <summary>播放失败原因（SpeakCompleted 回调写入，工作线程在其完成后读取）</summary>
            public volatile string? Error;

            /// <summary>结束记录是否已写（0/1）</summary>
            public int Finished;

            /// <summary>对外：这一句的生命周期结束（播完 / 打断 / 清除 / 取消）</summary>
            public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>对内：合成器报告这一句已结束（SpeakCompleted）</summary>
            public TaskCompletionSource PlayDone { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public CancellationTokenRegistration Registration { get; set; }

            private volatile Prompt? _prompt;
            public Prompt? Prompt { get => _prompt; set => _prompt = value; }

            private volatile bool _interruptRequested;
            public bool InterruptRequested { get => _interruptRequested; set => _interruptRequested = value; }

            public SpeechRecord Snapshot(SpeechStatus status, DateTime? endTime = null, string? error = null) => new()
            {
                Id = Id,
                Text = Text,
                Priority = Priority,
                Source = Source,
                Status = status,
                RequestTime = RequestTime,
                StartTime = StartTime,
                EndTime = endTime,
                Error = error
            };

            public void Complete()
            {
                Completion.TrySetResult();
                // Unregister 不等待正在执行的回调，避免在回调线程上互等
                Registration.Unregister();
            }
        }
    }
}
