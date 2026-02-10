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
          AND IsDeleted = 0
          AND IsEmailVerified = 0
          AND EmailVerificationTokenExpiry > GETUTCDATE();
        """;

    public const string GET_USER_PROFILE_BY_ID =
        """
        SELECT
            Id,
            Username,
            Email,
            DisplayName,
            Bio,
            ProfilePictureUrl,
            CreatedAt,
            LastLoginAt AS LastSeenAt
        FROM Users
        WHERE Id = @Id
          AND IsDeleted = 0;
        """;
    public const string SEARCH_USERS =
        """
        SELECT TOP(@Limit)
            Id,
            Username,
            DisplayName,
            Email,
            NULL AS JobTitle,
            Bio,
            ProfilePictureUrl
        FROM Users
        WHERE (Username LIKE @Query OR DisplayName LIKE @Query)
          AND Id != @CurrentUserId
          AND IsDeleted = 0
        ORDER BY Username;
        """;
}
