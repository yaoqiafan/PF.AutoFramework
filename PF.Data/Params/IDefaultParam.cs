using PF.Data.Entity.Category;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Data.Params
{
    public interface IDefaultParam
    {
        Dictionary<string, CommonParam> GetCommonDefaults();

        Dictionary<string, UserLoginParam> GetUsersDefaults();

        Dictionary<string, SystemConfigParam> GetSystemDefaults();
    }
}
