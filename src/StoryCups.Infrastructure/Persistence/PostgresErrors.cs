using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace StoryCups.Infrastructure.Persistence;

internal static class PostgresErrors
{
    /// <summary>
    /// Нарушено уникално ограничение (SQLSTATE 23505), по желание точно определено по име.
    /// </summary>
    public static bool IsUniqueViolation(this DbUpdateException exception, string? constraintName = null) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
        && (constraintName is null || postgres.ConstraintName == constraintName);
}
