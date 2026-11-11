using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace TutoringManager.Api.Infrastructure;

public static class DbErrors
{
    public static bool IsExclusionViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation };

    public static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
