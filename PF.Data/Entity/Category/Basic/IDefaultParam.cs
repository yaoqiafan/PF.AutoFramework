using PF.Data.Entity.Category;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Data.Entity.Category.Basic
{
    /// <summary>
    /// IDefaultParam
    /// </summary>
    public interface IDefaultParam
    {
        /// <summary>
        /// 获取UsersDefaults
        /// </summary>
        Dictionary<string, UserLoginParam> GetUsersDefaults();

        /// <summary>
        /// 获取SystemDefaults
        /// </summary>
        Dictionary<string, SystemConfigParam> GetSystemDefaults();

        /// <summary>
        /// 获取HardwareDefaults
        /// </summary>
        Dictionary<string, HardwareParam> GetHardwareDefaults();
    }
}
