using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using PutZige.Domain.DTOs;
using PutZige.Domain.Interfaces;
using PutZige.Infrastructure.Data.Dapper;
using PutZige.Infrastructure.Data.Dapper.SqlQueries;

namespace PutZige.Infrastructure.Repositories.Dapper
{
    public sealed class DapperUserRepository : IDapperUserRepository
    {
        private readonly DapperContext _context;
        private readonly ILogger<DapperUserRepository> _logger;

        public DapperUserRepository(DapperContext context, ILogger<DapperUserRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PutZige.Domain.DTOs.UserProfileProjection?> GetProfileByIdAsync(Guid id, CancellationToken ct = default)
        {
            if (id == Guid.Empty) return null;

            var conn = _context.GetOpenConnection();

            try
            {
                var result = await conn.QuerySingleOrDefaultAsync<UserProfileProjection>(new CommandDefinition(UserQueries.GET_USER_PROFILE_BY_ID, new { Id = id }, cancellationToken: ct)).ConfigureAwait(false);
                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Dapper GetProfileByIdAsync canceled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dapper GetProfileByIdAsync failed");
                throw;
            }
        }

        public async Task<int> VerifyEmailByTokenAsync(string token, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(token)) return 0;

            var conn = _context.GetOpenConnection();

            try
            {
                var result = await conn.ExecuteAsync(new CommandDefinition(UserQueries.VERIFY_EMAIL_BY_TOKEN, new { Token = token }, cancellationToken: ct)).ConfigureAwait(false);
                _logger.LogInformation("Dapper VerifyEmailByToken executed, rows affected: {Rows}", result);
                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Dapper VerifyEmailByToken canceled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dapper VerifyEmailByToken failed");
                throw;
            }
        }
    }
}
