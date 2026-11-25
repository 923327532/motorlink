using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MotorLink.Application.Abstractions;

namespace MotorLink.Infrastructure.Persistence;

/// <summary>Reportes con ADO.NET puro: SQL explícito y siempre parametrizado.</summary>
public sealed class ReportQueries(AppDbContext db) : IReportQueries
{
    public async Task<IReadOnlyList<UsersByProfileTypeRow>> GetUsersByProfileTypeAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT p.ProfileType, COUNT(*) AS Total
            FROM dbo.UserProfiles p
            INNER JOIN dbo.Users u ON u.Id = p.UserId
            WHERE u.IsDeleted = @isDeleted
            GROUP BY p.ProfileType
            ORDER BY p.ProfileType
            """;

        var rows = new List<UsersByProfileTypeRow>();
        await using var conn = new SqlConnection(db.Database.GetConnectionString());
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@isDeleted", System.Data.SqlDbType.Bit) { Value = false });

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            rows.Add(new UsersByProfileTypeRow(reader.GetString(0), reader.GetInt32(1)));
        return rows;
    }
}
