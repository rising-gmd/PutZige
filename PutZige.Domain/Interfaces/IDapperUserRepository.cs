namespace PutZige.Domain.Interfaces
{
    public interface IDapperUserRepository
    {
        Task<int> VerifyEmailByTokenAsync(string token, CancellationToken ct = default);
    }
}
