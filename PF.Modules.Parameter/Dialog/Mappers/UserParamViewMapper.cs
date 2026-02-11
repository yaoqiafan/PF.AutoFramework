using PF.Core.Entities.Identity;
using PF.Modules.Parameter.ViewModels.Models;
using PF.UI.Infrastructure.Mappers;

namespace PF.Modules.Parameter.Dialog.Mappers
{
    /// <summary>
    /// UserParamView 数据映射器
    /// </summary>
    public class UserParamViewMapper : ViewDataMapperBase
    {
        protected override bool HasSpecificMapping(object viewInstance, object data)
        {
            if (viewInstance is UserParamView userView && data is UserInfo userData)
            {
                // 直接映射
                userView.UserName = userData.UserName;
                userView.UserId = userData.UserId;
                userView.Root = userData.Root;
                userView.Password = userData.Password;
                return true;
            }

            return false;
        }

        protected override object ExtractSpecificData(object viewInstance)
        {
            if (viewInstance is UserParamView userView)
            {
                return new UserInfo
                {
                    UserName = userView.UserName,
                    UserId = userView.UserId,
                    Root = userView.Root,
                    Password = userView.Password
                };
            }

            return null;
        }
    }
}
