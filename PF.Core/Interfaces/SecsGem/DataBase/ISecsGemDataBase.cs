using PF.Core.Enums;
using PF.Core.Interfaces.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.SecsGem.DataBase
{
    /// <summary>
    /// SECS/GEM 数据库管理接口。
    /// 提供对 SECS/GEM 相关数据表的仓储访问、数据库初始化以及统一的事务提交功能。
    /// </summary>
    public interface ISecsGemDataBase
    {
        /// <summary>
        /// 根据指定的枚举获取对应的泛型数据仓储。
        /// </summary>
        /// <typeparam name="T">实体类型，必须是引用类型且实现 <see cref="IEntity"/> 接口，并具有无参构造函数。</typeparam>
        /// <param name="dbSet">用于指定目标数据表的 <see cref="SecsDbSet"/> 枚举值。</param>
        /// <returns>返回对应实体类型的泛型仓储接口 <see cref="IGenericRepository{T}"/>。</returns>
        IGenericRepository<T> GetRepository<T>(SecsDbSet dbSet) where T : class, IEntity, new();

        /// <summary>
        /// 异步初始化 SECS/GEM 数据库。
        /// 通常用于在系统启动时检查数据库连接、执行必要的迁移或验证基础表结构。
        /// </summary>
        /// <returns>返回一个表示异步操作的任务。如果初始化成功，结果为 <c>true</c>；否则为 <c>false</c>。</returns>
        Task<bool> InitializationDataBase();

        /// <summary>
        /// 异步保存上下文中的所有挂起更改到数据库（实现工作单元模式的统一提交）。
        /// </summary>
        /// <returns>返回一个表示异步保存操作的任务。任务结果包含成功写入底层数据库的状态实体数量。</returns>
        Task<int> SaveChangesAsync();
    }
}