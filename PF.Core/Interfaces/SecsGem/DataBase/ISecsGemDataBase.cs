using System.Threading.Tasks;

namespace PF.Core.Interfaces.SecsGem.DataBase
{
    /// <summary>
    /// SECS/GEM 数据库管理接口。
    /// <para>提供对 SECS/GEM 相关数据表的工作单元作用域访问与数据库初始化。</para>
    /// <para>并发模型：每次 <see cref="BeginScope"/> 返回一个独立的短生命周期 DbContext 作用域，
    /// 多线程调用各自隔离，天然避免 DbContext 的线程不安全问题。</para>
    /// </summary>
    public interface ISecsGemDataBase
    {
        /// <summary>
        /// 开启一个工作单元作用域。作用域内多次 <see cref="ISecsGemDbScope.GetRepository{T}"/>
        /// 返回绑定到同一 DbContext 的仓储，<see cref="ISecsGemDbScope.SaveChangesAsync"/>
        /// 一次性提交整个作用域的变更。
        /// <para>调用方负责 Dispose（推荐 <c>using var scope = db.BeginScope();</c>）。</para>
        /// </summary>
        /// <returns>一个新的工作单元作用域。</returns>
        ISecsGemDbScope BeginScope();

        /// <summary>
        /// 异步初始化 SECS/GEM 数据库。
        /// 通常用于在系统启动时检查数据库连接、执行必要的迁移或验证基础表结构。
        /// </summary>
        /// <returns>返回一个表示异步操作的任务。如果初始化成功，结果为 <c>true</c>；否则为 <c>false</c>。</returns>
        Task<bool> InitializationDataBase();
    }
}
