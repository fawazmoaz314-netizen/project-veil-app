using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Veil.Infrastructure.Data;

public static class DbExceptionExtensions
{
    public static bool IsUniqueViolation(this DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
