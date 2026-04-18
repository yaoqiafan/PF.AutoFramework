using PF.Core.Constants;
using System.Collections.Concurrent;

namespace PF.Core.Entities.SecsGem.Params.ValidateParam
{
    /// <summary>
    /// 验证配置，管理CEID、ReportID、VID和CommandID的映射关系
    /// </summary>
    public class ValidateConfiguration
    {
        /// <summary>配置文件路径</summary>
        public static readonly string filePath = Path.Combine(ConstGlobalParam.ConfigPath, "SecsGemInformationConfig.xlsx");
        private static readonly object _lock = new object();

        /// <summary>CEID集合</summary>
        public ConcurrentDictionary<string, CEID> CEIDS { get; set; } = new();
        /// <summary>ReportID集合</summary>
        public ConcurrentDictionary<string, ReportID> ReportIDS { get; set; } = new();
        /// <summary>VID集合</summary>
        public ConcurrentDictionary<string, VID> VIDS { get; set; } = new();

        /// <summary>CommandID集合</summary>
        public ConcurrentDictionary<string, CommandID> CommandIDS { get; set; } = new();


        /// <summary>根据描述获取CEID</summary>
        public CEID? GetCEID(string description)
        {
            return CEIDS.TryGetValue(description, out var ceid) ? ceid : null;
        }

        /// <summary>根据描述获取ReportID</summary>
        public ReportID? GetReportID(string description)
        {
            return ReportIDS.TryGetValue(description, out var report) ? report : null;
        }

        /// <summary>根据描述获取VID</summary>
        public VID? GetVID(string description)
        {
            return VIDS.TryGetValue(description, out var vid) ? vid : null;
        }

        /// <summary>根据CEID描述获取关联的VID列表</summary>
        public List<VID>? GetVIDsWithCEID(string CEIDdescription)
        {
            List<VID>? result = new List<VID>();
            CEID? cEID = GetCEID(CEIDdescription);
            if (cEID != null)
            {
                for (int j = 0; j < cEID.LinkReportID.Length; j++)
                {
                    ReportID? reportID = GetReportID(cEID.LinkReportID[j]);
                    if (reportID != null)
                    {
                        for (int i = 0; i < reportID.LinkVID.Length; i++)
                        {
                            VID? vID = GetVID(reportID.LinkVID[i]);
                            if (vID != null)
                            {
                                result.Add(vID);
                            }
                        }
                    }
                }
            }
            return result;
        }


        /// <summary>根据描述获取CommandID</summary>
        public CommandID? GetCommandID(string description)
        {
            return CommandIDS.TryGetValue(description, out var cmd) ? cmd : null;
        }


        /// <summary>根据RCMD名称获取CommandID</summary>
        public CommandID? GetCommandIDByRCMD(string RCMD)
        {
            return CommandIDS.Values.FirstOrDefault(cmd => cmd.RCMD == RCMD);
        }

        /// <summary>根据描述设置VID值</summary>
        public bool SetVIDValue(string description, object value)
        {
            if (VIDS.TryGetValue(description, out var vid))
            {
                return vid.SetValue(value);
            }
            return false;
        }


        // ����ID�Ĳ��ҷ���
        /// <summary>根据ID编号获取CEID</summary>
        public CEID? GetCEID(uint code)
        {
            return CEIDS.Values.FirstOrDefault(ceid => ceid.ID == code);
        }

        /// <summary>根据ID编号获取ReportID</summary>
        public ReportID? GetReportID(uint code)
        {
            return ReportIDS.Values.FirstOrDefault(report => report.ID == code);
        }

        /// <summary>根据ID编号获取VID</summary>
        public VID? GetVID(uint code)
        {
            return VIDS.Values.FirstOrDefault(vid => vid.ID == code);
        }
        /// <summary>根据CEID编号获取关联的VID列表</summary>
        public List<VID>? GetVIDsWithCEID(uint CEIDcode)
        {
            List<VID>? result = new List<VID>();
            CEID? cEID = GetCEID(CEIDcode);
            if (cEID != null)
            {
                for (int j = 0; j < cEID.LinkReportID.Length; j++)
                {
                    ReportID? reportID = GetReportID(cEID.LinkReportID[j]);
                    if (reportID != null)
                    {
                        for (int i = 0; i < reportID.LinkVID.Length; i++)
                        {
                            VID? vID = GetVID(reportID.LinkVID[i]);
                            if (vID != null)
                            {
                                result.Add(vID);
                            }
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>根据ID编号获取Command</summary>
        public CommandID? GetCommand(uint code)
        {
            return CommandIDS.Values.FirstOrDefault(cmd => cmd.ID == code);
        }

        /// <summary>根据ID编号设置VID值</summary>
        public bool SetVIDValue(uint code, object value)
        {
            var vid = GetVID(code);

            if (vid != null)
            {
                return vid.SetValue(value);
            }
            return false;
        }


        /// <summary>根据ID编号集合获取CEID列表</summary>
        public IEnumerable<CEID> GetCEIDs(IEnumerable<uint> codes)
        {
            return CEIDS.Values.Where(ceid => codes.Contains(ceid.ID));
        }

        /// <summary>根据ID编号集合获取ReportID列表</summary>
        public IEnumerable<ReportID> GetReportIDs(IEnumerable<uint> codes)
        {
            return ReportIDS.Values.Where(report => codes.Contains(report.ID));
        }

        /// <summary>根据ID编号集合获取VID列表</summary>
        public IEnumerable<VID> GetVIDs(IEnumerable<uint> codes)
        {
            return VIDS.Values.Where(vid => codes.Contains(vid.ID));
        }


        /// <summary>根据ID编号集合获取Command列表</summary>
        public IEnumerable<CommandID> GetCommands(IEnumerable<uint> codes)
        {
            return CommandIDS.Values.Where(cmd => codes.Contains(cmd.ID));
        }

        /// <summary>添加CEID</summary>
        public void AddCEID(string description, CEID ceid)
        {
            CEIDS[description] = ceid;
        }

        /// <summary>添加ReportID</summary>
        public void AddReportID(string description, ReportID report)
        {
            ReportIDS[description] = report;
        }

        /// <summary>添加VID</summary>
        public void AddVID(string description, VID vid)
        {
            VIDS[description] = vid;
        }


        /// <summary>添加CommandID</summary>
        public void AddCommandID(string description, CommandID cmd)
        {
            CommandIDS[description] = cmd;
        }

        // ��ȡ������ReportID
        /// <summary>获取CEID关联的ReportID列表</summary>
        public IEnumerable<ReportID> GetLinkedReports(CEID ceid)
        {
            return ceid.LinkReportID
                .Select(reportId => ReportIDS.Values.FirstOrDefault(r => r.ID == reportId))
                .Where(report => report != null)!;
        }

        // ��ȡ������VID
        /// <summary>获取ReportID关联的VID列表</summary>
        public IEnumerable<VID> GetLinkedVIDs(ReportID report)
        {
            return report.LinkVID
                .Select(vidId => VIDS.Values.FirstOrDefault(v => v.ID == vidId))
                .Where(vid => vid != null)!;
        }

        /// <summary>获取CommandID关联的VID列表</summary>
        public IEnumerable<VID> GetLinkedVIDs(CommandID command)
        {
            return command.LinkVID.Select(vidId => VIDS.Values.FirstOrDefault(v => v.ID == vidId))
                .Where(vid => vid != null)!;
        }

        // ��������VIDֵ
        /// <summary>批量更新VID值（按描述）</summary>
        public void UpdateMultipleVIDs(Dictionary<string, object> updates)
        {
            foreach (var (key, value) in updates)
            {
                if (VIDS.TryGetValue(key, out var vid))
                {
                    vid.SetValue(value);
                }
            }
        }

        /// <summary>批量更新VID值（按ID编号）</summary>
        public void UpdateMultipleVIDs(Dictionary<uint, object> updates)
        {
            foreach (var (key, value) in updates)
            {
                if (key==2013)
                {

                }
                var vid = GetVID(key);
                vid?.SetValue(value);
            }
        }

        // ��֤����
        /// <summary>验证所有关联关系的完整性</summary>
        public bool Validate()
        {
            bool allReportsExist = CEIDS.Values.All(ceid =>
                ceid.LinkReportID.All(reportId =>
                    ReportIDS.Values.Any(r => r.ID == reportId)));

            bool allVIDsExist = ReportIDS.Values.All(report =>
                report.LinkVID.All(vidId =>
                    VIDS.Values.Any(v => v.ID == vidId)));

            bool allCommandVIDsExist = CommandIDS.Values.All(cmd =>
                cmd.LinkVID.All(vidId =>
                    VIDS.Values.Any(v => v.ID == vidId)));

            return allReportsExist && allVIDsExist && allCommandVIDsExist;
        }

        /// <summary>保存配置</summary>
        public void Save()
        {
            throw new NotImplementedException();
        }

       
    }
}
