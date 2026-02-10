#nullable enable
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
    public sealed class DapperMessageRepository : IDapperMessageRepository
    {
        private readonly DapperContext _context;
        private readonly ILogger<DapperMessageRepository> _logger;

        public DapperMessageRepository(DapperContext context, ILogger<DapperMessageRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<System.Collections.Generic.IEnumerable<ConversationProjection>> GetConversationsForUserAsync(Guid userId, int limit, CancellationToken ct = default)
        {
            if (userId == Guid.Empty) return Array.Empty<ConversationProjection>();

            var conn = _context.GetOpenConnection();

            try
            {
                var param = new { UserId = userId, Limit = limit };
                var result = await conn.QueryAsync<ConversationProjection>(new CommandDefinition(MessageQueries.GET_CONVERSATIONS_FOR_USER, param, cancellationToken: ct)).ConfigureAwait(false);
                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Dapper GetConversationsForUserAsync canceled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dapper GetConversationsForUserAsync failed");
                throw;
            }
        }
    }
}
