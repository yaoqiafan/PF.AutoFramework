using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Data
{
    /// <summary>
    /// 通用仓库接口
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IGenericRepository<T> where T : class, new()
    {
        /// <summary>
        /// 根据ID获取单个对象
        /// </summary>
        /// <param name="id">唯一标识</param>
        /// <returns>返回单个对象（可为null）</returns>
        Task<T?> GetByIdAsync(int id);

        /// <summary>
        /// 获取所有对象
        /// </summary>
        /// <returns>返回所有对象</returns>
        Task<IEnumerable<T>> GetAllAsync();

        /// <summary>
        /// 根据条件查找
        /// </summary>
        /// <param name="predicate">条件表达式</param>
        /// <returns>返回查询结果集合</returns>
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// 根据条件查询单个对象
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns></returns>
        Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// 添加
        /// </summary>
        /// <param name="entity">要添加的对象</param>
        /// <returns>返回要添加的对象</returns>
        Task<T> AddAsync(T entity);

        /// <summary>
        /// 批量添加
        /// </summary>
        /// <param name="entities">要添加的对象集合</param>
        /// <returns></returns>
        Task AddRangeAsync(IEnumerable<T> entities);

        /// <summary>
        /// 修改
        /// </summary>
        /// <param name="entity">要修改的对象</param>
        /// <returns></returns>
        Task UpdateAsync(T entity);

        /// <summary>
        /// 批量修改
        /// </summary>
        /// <param name="entities">要修改的对象集合</param>
        /// <returns></returns>
        Task UpdateRangeAsync(IEnumerable<T> entities);

        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="entity">要删除的对象</param>
        /// <returns></returns>
        Task RemoveAsync(T entity);

        /// <summary>
        /// 批量删除
        /// </summary>
        /// <param name="entities">要删除的对象集合</param>
        /// <returns></returns>
        Task RemoveRangeAsync(IEnumerable<T> entities);

        /// <summary>
        /// 查询数量
        /// </summary>
        /// <returns>返回查询结果</returns>
        Task<int> CountAsync();

        /// <summary>
        /// 根据条件查询是否存在
        /// </summary>
        /// <param name="predicate">条件表达式</param>
        /// <returns>返回查询结果 true：存在、false：不存在</returns>
        Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// 保存更改
        /// </summary>
        /// <returns></returns>
        Task<int> SaveChangesAsync();
    }
}
