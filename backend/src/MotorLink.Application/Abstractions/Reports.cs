namespace MotorLink.Application.Abstractions;

public sealed record UsersByProfileTypeRow(string ProfileType, int TotalUsers);

/// <summary>Consultas de reporte de solo lectura (implementadas con ADO.NET en Infrastructure).</summary>
public interface IReportQueries
{
    Task<IReadOnlyList<UsersByProfileTypeRow>> GetUsersByProfileTypeAsync(CancellationToken ct);
}

public sealed record PlanRevenueRow(
    string PlanCode, string PlanName, decimal MonthlyPrice, int ActiveTotal, int Real, int Simulated, int Complimentary, int Unpaid,
    decimal MonthlyValueReal, decimal MonthlyValueSimulated, decimal MonthlyValueComplimentary);

public sealed record PlanRevenueReport(DateTime GeneratedAt, IReadOnlyList<PlanRevenueRow> Plans, decimal TotalReal, decimal TotalSimulated, decimal TotalComplimentary, int TotalActiveSubscriptions);

/// <summary>Reporte administrativo con ADO.NET desconectado (SqlDataAdapter + DataSet + DataRelation).</summary>
public interface IPlanRevenueReport
{
    Task<PlanRevenueReport> GenerateAsync(CancellationToken ct);
}
