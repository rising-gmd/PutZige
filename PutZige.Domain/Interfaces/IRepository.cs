using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using PutZige.Domain.Models;

namespace PutZige.Domain.Interfaces
{
    /// <summary>
    /// Generic repository interface for entities inheriting from BaseEntity.
    /// </summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    public interface IRepository<TEntity> where TEntity : Entities.BaseEntity
    {
        /// <summary>
        /// Gets an entity by its unique identifier.
        /// </summary>
        Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);

        /// <summary>
        /// Gets an entity by its unique identifier, including related entities.
        /// </summary>
        Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default, params Expression<Func<TEntity, object>>[] includes);

        /// <summary>
        /// Gets all entities.
        /// </summary>
        Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken ct = default);

        /// <summary>
        /// Gets the first entity matching the predicate or null.
        /// </summary>
        Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);

        /// <summary>
        /// Finds all entities matching the predicate.
        /// </summary>
        Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);

        /// <summary>
        /// Gets the count of all entities.
        /// </summary>
        Task<int> CountAsync(CancellationToken ct = default);

        /// <summary>
        /// Gets the count of entities matching the predicate.
        /// </summary>
        Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);

        /// <summary>
        /// Checks if any entity matches the predicate.
        /// </summary>
        Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);

        /// <summary>
        /// Adds a new entity.
        /// </summary>
        Task AddAsync(TEntity entity, CancellationToken ct = default);

        /// <summary>
        /// Updates an existing entity.
        /// </summary>
        void Update(TEntity entity);

        /// <summary>
        /// Soft deletes an entity.
        /// </summary>
        void Delete(TEntity entity);

        /// <summary>
        /// Permanently deletes an entity.
        /// </summary>
        void HardDelete(TEntity entity);

        /// <summary>
        /// Get paginated results with optional filtering and ordering.
        /// </summary>
        Task<PagedResult<TEntity>> GetPagedAsync(
            int pageNumber,
            int pageSize,
            Expression<Func<TEntity, bool>>? filter = null,
            Expression<Func<TEntity, object>>? orderBy = null,
            bool ascending = true,
            CancellationToken ct = default);

        /// <summary>
        /// Project entities to DTOs with pagination (performance optimized).
        /// </summary>
        Task<PagedResult<TResult>> GetPagedProjectionAsync<TResult>(
            Expression<Func<TEntity, TResult>> selector,
            int pageNumber,
            int pageSize,
            Expression<Func<TEntity, bool>>? filter = null,
            Expression<Func<TEntity, object>>? orderBy = null,
            bool ascending = true,
            CancellationToken ct = default);
    }
}
