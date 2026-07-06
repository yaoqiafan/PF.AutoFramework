using PF.Core.Enums;
using PF.Core.Interfaces.Data;
using System;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.SecsGem.DataBase
{
    /// <summary>
    /// SECS/GEM 数据库的工作单元作用域。
    /// <para>由 <see cref="ISecsGemDataBase.BeginScope"/> 开启，持有一个短生命周期的
    /// <c>DbContext</c>，作用域内的多次 <see cref="GetRepository{T}"/> 调用共享同一
    /// ChangeTracker，<see cref="SaveChangesAsync"/> 一次性提交整个作用域的变更。</para>
    /// <para>用完必须 Dispose（推荐 <c>using var scope = db.BeginScope();</c>），
    /// Dispose 时关闭并释放所持有的 DbContext。</para>
    /// </summary>
    public interface ISecsGemDbScope : IDisposable
    {
        /// <summary>
        /// 在当前作用域内获取指定实体类型的泛型仓储。
        /// 多次调用返回绑定到同一 DbContext 的仓储实例，确保跨仓储的工作单元语义。
        /// </summary>
        /// <typeparam name="T">实体类型，必须是引用类型且实现 <see cref="IEntity"/> 接口，并具有无参构造函数。</typeparam>
        /// <param name="dbSet">用于指定目标数据表的 <see cref="SecsDbSet"/> 枚举值。</param>
        /// <returns>对应实体类型的泛型仓储接口。</returns>
        IGenericRepository<T> GetRepository<T>(SecsDbSet dbSet) where T : class, IEntity, new();

        /// <summary>
        /// 异步保存当前作用域上下文中的所有挂起更改到数据库（工作单元提交）。
        /// </summary>
        /// <returns>成功写入底层数据库的状态实体数量。</returns>
        Task<int> SaveChangesAsync();
    }
}
