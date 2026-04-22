using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Prism.Ioc;
using PF.Core.Attributes;
using PF.Core.Enums;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.Camera.IntelligentCamera;
using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Logging;
using PF.Core.Models;
using PF.Infrastructure.Mechanisms;
using PF.Services.Hardware;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.CostParam;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.Mechanisms
{
    /// <summary>
    /// 【全局视觉检测模组】 (OCR Detection Module)
    ///
    /// <para>物理架构：</para>
    /// 包含 X、Y、Z 三个直角坐标轴组成的三轴龙门模组，以及搭载在 Z 轴末端的智能 OCR 相机。
    ///
    /// <para>软件职责：</para>
    /// 负责在工位1和工位2之间调度相机位置，根据不同工位的产品动态切换相机内部的检测配方 (Program)，
    /// 触发相机拍照解码，并维护检测源图片的本地存根，以便工程师在发生误判时进行图像追溯。
    /// </summary>
    [MechanismUI("检测模组", "WorkStationDetectionModuleDebugView", 5)]
    public class WorkStationDetectionModule : BaseMechanism
    {
        #region Enums (轴关键点位枚举)

        /// <summary>X轴点位枚举</summary>
        public enum XAxisPoint
        {
            /// <summary>待机位</summary>
            待机位 = 0,
        }

        /// <summary>Y轴点位枚举</summary>
        public enum YAxisPoint
        {
            /// <summary>待机位</summary>
            待机位 = 0,
        }

        /// <summary>Z轴点位枚举</summary>
        public enum ZAxisPoint
        {
            /// <summary>待机位</summary>
            待机位 = 0, // Z轴必须退回到最高安全位，防止 XY 移动时相机撞击下方的料盒或干涉物
        }

        #endregion

        #region Fields & Properties (硬件实例与配方缓存)

        // ── 底层硬件实例（延迟加载） ──
        private IAxis _xAxis;
        private IAxis _yAxis;
        private IAxis _zAxis;
        private IIntelligentCamera _camera;

        // ── 业务数据通讯模块 ──
        private WorkStationDataModule _dataModule;
        private IContainerProvider Provider;

        // ── 生产配方缓存 ──
        private OCRRecipeParam _1StationRecipe;
        private OCRRecipeParam _2StationRecipe;

        /// <summary>
        /// 记录当前相机内部正在运行的程序/配方名称。
        /// 用于防呆：只有目标工位配方与当前相机内部配方不一致时，才向相机发送切换指令，节省通信时间。
        /// </summary>
        private string _curOCRRecipeName;

        // ── 公开硬件绑定属性 (供 ViewModel/UI 面板调试使用) ──
        /// <summary>获取X轴实例</summary>
        public IAxis XAxis => _xAxis;
        /// <summary>获取Y轴实例</summary>
        public IAxis YAxis => _yAxis;
        /// <summary>获取Z轴实例</summary>
        public IAxis ZAxis => _zAxis;
        /// <summary>获取相机实例</summary>
        public IIntelligentCamera Camera => _camera;

        #endregion

        #region Constructor & Lifecycle (构造与生命周期)

        /// <summary>
        /// 初始化全局视觉检测模组
        /// </summary>
        public WorkStationDetectionModule(
            IHardwareManagerService hardwareManagerService,
            IParamService paramService,
            IContainerProvider provider,
            ILogService logger)
            : base(E_Mechanisms.OCR识别模组.ToString(), hardwareManagerService, paramService, logger)
        {
            Provider = provider;
        }

        /// <summary>
        /// 模组初始化核心逻辑：延迟解析三轴与相机 → 注册报警聚合防线 → 建立通信并使能
        /// </summary>
        protected override async Task<bool> InternalInitializeAsync(CancellationToken token)
        {
            _curOCRRecipeName = string.Empty;

            // ① 延迟解析硬件实例
            _xAxis = HardwareManagerService?.GetDevice(E_AxisName.视觉X轴.ToString()) as IAxis;
            if (_xAxis == null)
            {
                _logger.Error($"[{MechanismName}] 未找到X轴 '{E_AxisName.视觉X轴}'，请确认硬件配置。");
                return false;
            }

            _yAxis = HardwareManagerService?.GetDevice(E_AxisName.视觉Y轴.ToString()) as IAxis;
            if (_yAxis == null)
            {
                _logger.Error($"[{MechanismName}] 未找到Y轴 '{E_AxisName.视觉Y轴}'，请确认硬件配置。");
                return false;
            }

            _zAxis = HardwareManagerService?.GetDevice(E_AxisName.视觉Z轴.ToString()) as IAxis;
            if (_zAxis == null)
            {
                _logger.Error($"[{MechanismName}] 未找到Z轴 '{E_AxisName.视觉Z轴}'，请确认硬件配置。");
                return false;
            }

            _camera = HardwareManagerService?.GetDevice(E_Camera.OCR相机.ToString()) as IIntelligentCamera;
            if (_camera == null)
            {
                _logger.Error($"[{MechanismName}] 未找到相机 '{E_Camera.OCR相机}'，请确认硬件配置。");
                return false;
            }

            // 获取数据交互中枢模块
            _dataModule = Provider.Resolve<IMechanism>(nameof(WorkStationDataModule)) as WorkStationDataModule;
            if (_dataModule == null)
            {
                _logger.Error($"[{MechanismName}] 未找到 {nameof(WorkStationDataModule)} 模块，请检查软件依赖。");
                return false;
            }

            // ② 注册报警聚合防线
            RegisterHardwareDevice(_xAxis as IHardwareDevice);
            RegisterHardwareDevice(_yAxis as IHardwareDevice);
            RegisterHardwareDevice(_zAxis as IHardwareDevice);
            RegisterHardwareDevice(_camera as IHardwareDevice);

            await ConfirmEunmPoints();

            // ③ 建立底层物理通信连接
            if (!await _xAxis.ConnectAsync(token)) { _logger.Error($"[{MechanismName}] X轴连接失败"); return false; }
            if (!await _yAxis.ConnectAsync(token)) { _logger.Error($"[{MechanismName}] Y轴连接失败"); return false; }
            if (!await _zAxis.ConnectAsync(token)) { _logger.Error($"[{MechanismName}] Z轴连接失败"); return false; }

            // ④ 伺服上电使能
            if (!await _xAxis.EnableAsync(token)) { _logger.Error($"[{MechanismName}] X轴使能失败"); return false; }
            if (!await _yAxis.EnableAsync(token)) { _logger.Error($"[{MechanismName}] Y轴使能失败"); return false; }
            if (!await _zAxis.EnableAsync(token)) { _logger.Error($"[{MechanismName}] Z轴使能失败"); return false; }

            return true;
        }

        /// <summary>
        /// 模组急停/停止钩子：安全阻断三轴运动
        /// </summary>
        protected override async Task InternalStopAsync()
        {
            if (_xAxis != null) await _xAxis.StopAsync();
            if (_yAxis != null) await _yAxis.StopAsync();
            if (_zAxis != null) await _zAxis.StopAsync();
        }

        #endregion

        #region Motion Control (轴联动与定位)

        /// <summary>
        /// 移动到全局待机避让位置。
        /// <para>防碰撞逻辑：必须先提升 Z 轴至安全位，确认到位后再移动 X、Y 轴。</para>
        /// </summary>
        public async Task<MechResult> MoveInitial(CancellationToken token = default)
        {
            try
            {
                CheckReady();

                // 1. Z轴优先抬升撤离
                if (!await MoveToPointAndWaitAsync(_zAxis, nameof(ZAxisPoint.待机位), token: token))
                {
                    return MechResult.Fail(AlarmCodesExtensions.Detection.MoveInitialFailed, "Z轴移动到待机位失败");
                }

                // 2. XY水平轴安全归位
                if (!await MoveMultiAxesToPointsAsync(new[] {
                    (_xAxis, nameof(XAxisPoint.待机位)),
                    (_yAxis, nameof(YAxisPoint.待机位)) }, token: token))
                {
                    return MechResult.Fail(AlarmCodesExtensions.Detection.MoveInitialFailed, "XY轴移动到待机位失败");
                }

                _logger.Info($"[{MechanismName}] 移动到待机位成功");
                return MechResult.Success();
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return MechResult.Fail(AlarmCodesExtensions.Detection.MoveInitialFailed, ex.Message);
            }
        }

        /// <summary>
        /// 仅将相机 Z 轴提升到安全位置（用于工位内的小范围避让）
        /// </summary>
        public async Task<MechResult> MoveZSafePos(CancellationToken token = default)
        {
            try
            {
                CheckReady();
                if (!await MoveToPointAndWaitAsync(_zAxis, nameof(ZAxisPoint.待机位), token: token))
                {
                    return MechResult.Fail(AlarmCodesExtensions.Detection.MoveZSafePosFailed, "Z轴移动到安全位置失败");
                }

                _logger.Info($"[{MechanismName}] Z轴移动到安全位置成功");
                return MechResult.Success();
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return MechResult.Fail(AlarmCodesExtensions.Detection.MoveZSafePosFailed, ex.Message);
            }
        }

        /// <summary>
        /// 驱动相机模组前往【工位1】执行检测。
        /// 动作包含：读取动态配方坐标 -> 三轴联动寻址 -> 动态切换相机匹配程序。
        /// </summary>
        public async Task<MechResult> MoveToStation1(CancellationToken token = default)
        {
            try
            {
                CheckReady();
                _1StationRecipe = _dataModule.Station1ReciepParam;
                if (_1StationRecipe == null)
                {
                    return MechResult.Fail(AlarmCodesExtensions.Detection.MoveToStation1RecipeNull, "工位1未加载配方");
                }

                // 发起三轴联动 (使用轴默认参数)
                if (!await _xAxis.MoveAbsoluteAsync(_1StationRecipe._1PosX, _xAxis.Param.Vel, _xAxis.Param.Acc, _xAxis.Param.Dec, 0.08, token) ||
                    !await _yAxis.MoveAbsoluteAsync(_1StationRecipe._1PosY, _yAxis.Param.Vel, _yAxis.Param.Acc, _yAxis.Param.Dec, 0.1, token) ||
                    !await _zAxis.MoveAbsoluteAsync(_1StationRecipe._1PosZ, _zAxis.Param.Vel, _zAxis.Param.Acc, _zAxis.Param.Dec, 0.1, token))
                {
                    return MechResult.Fail(AlarmCodesExtensions.Detection.MoveToStation1MoveFailed, "移动到工位1触发失败");
                }

                // 在轴运动的同时，后台并行下发指令切换相机的视觉配方，节省 Cycle Time
                if (IsChangedOcrCamera(E_WorkSpace.工位1))
                {
                    if (!await _camera.ChangeProgram(_1StationRecipe.OCRRecipeName))
                    {
                        _curOCRRecipeName = string.Empty;
                        return MechResult.Fail(AlarmCodesExtensions.Detection.MoveToStation1RecipeSwitchFailed, "切换到工位1的OCR配方失败");
                    }
                    _curOCRRecipeName = _1StationRecipe.OCRRecipeName;
                }

                // 等待物理运动最终到位
                int timeout = await ParamService.GetParamAsync<int>(E_Params.AxisMoveTimeout.ToString());
                if (!await WaitAxisMoveDoneAsync(_xAxis, timeout, token) ||
                    !await WaitAxisMoveDoneAsync(_yAxis, timeout, token) ||
                    !await WaitAxisMoveDoneAsync(_zAxis, timeout, token))
                {
                    return MechResult.Fail(AlarmCodesExtensions.Detection.MoveToStation1MoveTimeout, "XYZ轴移动到工位1超时");
                }
                return MechResult.Success();
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return MechResult.Fail(AlarmCodesExtensions.Detection.MoveToStation1MoveFailed, ex.Message);
            }
        }

        /// <summary>
        /// 驱动相机模组前往【工位2】执行检测。
        /// </summary>
        public async Task<MechResult> MoveToStation2(CancellationToken token = default)
        {
            try
            {
                CheckReady();
                _2StationRecipe = _dataModule.Station2ReciepParam;
                if (_2StationRecipe == null)
                {
                    return MechResult.Fail(AlarmCodesExtensions.Detection.MoveToStation2RecipeNull, "工位2未加载配方");
                }

                // 发起三轴联动
                if (!await _xAxis.MoveAbsoluteAsync(_2StationRecipe._2PosX, _xAxis.Param.Vel, _xAxis.Param.Acc, _xAxis.Param.Dec, 0.08, token) ||
                    !await _yAxis.MoveAbsoluteAsync(_2StationRecipe._2PosY, _yAxis.Param.Vel, _yAxis.Param.Acc, _yAxis.Param.Dec, 0.1, token) ||
                    !await _zAxis.MoveAbsoluteAsync(_2StationRecipe._2PosZ, _zAxis.Param.Vel, _zAxis.Param.Acc, _zAxis.Param.Dec, 0.1, token))
                {
                    return MechResult.Fail(AlarmCodesExtensions.Detection.MoveToStation2MoveFailed, "移动到工位2触发失败");
                }

                // 并行切换配方
                if (IsChangedOcrCamera(E_WorkSpace.工位2))
                {
                    if (!await _camera.ChangeProgram(_2StationRecipe.OCRRecipeName))
                    {
                        _curOCRRecipeName = string.Empty;
                        return MechResult.Fail(AlarmCodesExtensions.Detection.MoveToStation2RecipeSwitchFailed, "切换到工位2的OCR配方失败");
                    }
                    _curOCRRecipeName = _2StationRecipe.OCRRecipeName;
                }

                int timeout = await ParamService.GetParamAsync<int>(E_Params.AxisMoveTimeout.ToString());
                if (!await WaitAxisMoveDoneAsync(_xAxis, timeout, token) ||
                    !await WaitAxisMoveDoneAsync(_yAxis, timeout, token) ||
                    !await WaitAxisMoveDoneAsync(_zAxis, timeout, token))
                {
                    return MechResult.Fail(AlarmCodesExtensions.Detection.MoveToStation2MoveTimeout, "XYZ轴移动到工位2超时");
                }
                return MechResult.Success();
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return MechResult.Fail(AlarmCodesExtensions.Detection.MoveToStation2MoveFailed, ex.Message);
            }
        }

        #endregion

        #region Vision & Camera (视觉与相机调度)

        /// <summary>
        /// 触发 OCR 相机拍照解码。
        /// <para>支持两种模式：无感强制刷新抓图 (主要用于视觉标定调试)；自动重试并校验 MES 合法性 (用于正式生产)。</para>
        /// </summary>
        /// <param name="IsCheckResult">是否请求中枢模块 <see cref="WorkStationDataModule"/> 对解码结果进行 MES 合法性校验</param>
        /// <param name="workStation">触发拍照所在的工位标识</param>
        /// <param name="token">取消令牌</param>
        /// <returns>MechResult，Data 包含 (OcrText, ImagePath) 元组</returns>
        public async Task<MechResult<(string OcrText, string ImagePath)>> CameraTigger(bool IsCheckResult, E_WorkSpace workStation = E_WorkSpace.工位1, CancellationToken token = default)
        {
            try
            {
                string originalPathDir = await ParamService.GetParamAsync<string>(E_Params.OCRCameraImageOriginalPath.ToString());

                if (!IsCheckResult)
                {
                    // 调试/强制抓图模式：清空旧图片以确保提取到的是最新一帧
                    DeleteDir(originalPathDir);
                    string ocreec = await _camera.Tigger(token);
                    string path = GetLatestCreatedFile(originalPathDir);
                    return MechResult<(string, string)>.Success((ocreec, path));
                }
                else
                {
                    // 生产模式：提供最多3次的容错重拍机会
                    string rec = string.Empty;
                    for (int i = 0; i < 3; i++)
                    {
                        rec = await _camera.Tigger(token); // 触发拍照与底层算法解析

                        // 提交数据中枢进行逻辑比对
                        var flag = await _dataModule.CheckOcrTextAsync(workStation, rec, token);
                        if (flag.IsSuccess)
                        {
                            break; // 校验成功，跳出重试
                        }
                    }

                    // 获取当前拍摄最新生成的图像物理路径
                    string imagePath = GetLatestCreatedFile(originalPathDir);
                    return MechResult<(string, string)>.Success((rec, imagePath));
                }
            }
            catch (Exception ex)
            {
                return MechResult<(string, string)>.Fail(AlarmCodesExtensions.Detection.CameraTiggerFailed, $"相机拍照触发异常: {ex.Message}");
            }
        }

        #endregion

        #region File & Image Processing (文件与图像留存处理)

        /// <summary>
        /// 将相机吐出的原始图像，根据 MES 批次和晶圆槽号进行结构化归档保存。
        /// 方便未来根据 "批次号+槽位" 快速查找良率缺陷图。
        /// </summary>
        public async Task<string> SaveImage(string Originalpath, E_WorkSpace workSpace, WaferInfo info, CancellationToken token = default)
        {
            try
            {
                // 确定当前的业务上下文批次数据
                var flag = workSpace == E_WorkSpace.工位1 ? _dataModule.Station1MesDetectionData : _dataModule.Station2MesDetectionData;
                string baseSaveDir = await ParamService.GetParamAsync<string>(E_Params.OCRCameraImageSavePath.ToString());

                // 结构化存储路径：基路径 / 内部批次号 / 客户批次号 / 晶圆ID号 / 时间戳.jpg
                string path = $"{baseSaveDir}\\{flag.InternalBatchId}\\{info.CustomerBatch}\\{info.WaferId}\\{DateTime.Now:yyyyMMddHHmmss}.jpg";

                FileInfo fileInfo = new FileInfo(path);
                if (!Directory.Exists(fileInfo.DirectoryName))
                {
                    Directory.CreateDirectory(fileInfo.DirectoryName);
                }

                File.Copy(Originalpath, path);
                return path;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 辅助方法：检索指定目录下创建时间最新的文件
        /// </summary>
        private string GetLatestCreatedFile(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return null;

            var dir = new DirectoryInfo(folderPath);
            var files = dir.GetFiles();

            if (files.Length == 0) return null;

            // 按创建时间降序排序，提取首个文件的完整路径
            return files.OrderByDescending(f => f.CreationTime).First().FullName;
        }

        /// <summary>
        /// 辅助方法：递归强制清空指定文件夹下的所有旧文件和子目录
        /// </summary>
        private void DeleteDir(string dir)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    DirectoryInfo directory = new DirectoryInfo(dir);

                    foreach (FileInfo file in directory.GetFiles())
                    {
                        file.Delete();
                    }

                    foreach (DirectoryInfo subDir in directory.GetDirectories())
                    {
                        subDir.Delete(true);
                    }
                }
            }
            catch (Exception)
            {
                // 忽略因相机系统后台独占或权限限制导致的部分删除失败
            }
        }

        #endregion

        #region Helper Methods (内部辅助验证)

        /// <summary>
        /// 验证当前轴所绑定的枚举示教点是否在底层配置中全部存在，
        /// 避免引发运行时的空引用异常 (<see cref="NullReferenceException"/>)。
        /// </summary>
        public async Task ConfirmEunmPoints()
        {
            if (_xAxis != null) EnsurePointsExist<XAxisPoint>(_xAxis);
            if (_yAxis != null) EnsurePointsExist<YAxisPoint>(_yAxis);
            if (_zAxis != null) EnsurePointsExist<ZAxisPoint>(_zAxis);
            await Task.CompletedTask;
        }

        /// <summary>
        /// 判断是否需要向相机发送底层程序切换指令。
        /// <para>防呆机制：仅当目标工位配方要求加载的视觉工程，与相机内部现存工程不同时，才予以切换，避免浪费通信时效。</para>
        /// </summary>
        private bool IsChangedOcrCamera(E_WorkSpace station)
        {
            var targetRecipeName = station == E_WorkSpace.工位1 ? _1StationRecipe.OCRRecipeName : _2StationRecipe.OCRRecipeName;

            // [逻辑修复] 若目标配方名与当前记录 _curOCRRecipeName 不等，则证明需要执行 ChangeProgram
            return !string.Equals(targetRecipeName, _curOCRRecipeName, StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}
