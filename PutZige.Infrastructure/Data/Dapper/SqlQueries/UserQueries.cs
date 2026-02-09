namespace PutZige.Infrastructure.Data.Dapper.SqlQueries;

public static class UserQueries
{
    public const string VERIFY_EMAIL_BY_TOKEN =
        """
        UPDATE Users
        SET IsEmailVerified = 1,
            EmailVerificationToken = NULL,
            EmailVerificationTokenExpiry = NULL,
            UpdatedAt = GETUTCDATE()
        WHERE EmailVerificationToken = @Token
          AND IsEmailVerified = 0
          AND EmailVerificationTokenExpiry > GETUTCDATE();
        """;
}
