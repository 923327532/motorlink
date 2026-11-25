using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MotorLink.Application.Abstractions;

namespace MotorLink.Infrastructure.Persistence;

/// <summary>
/// Reporte "ingresos mensuales por plan y origen de pago" con el modelo DESCONECTADO de ADO.NET:
/// 1) dos SqlCommand parametrizados llenan un DataSet mediante SqlDataAdapter;
/// 2) se define un DataRelation Planes → Suscripciones;
/// 3) las columnas calculadas, filtros (DataView) y totales (DataTable.Compute) se resuelven en memoria, sin volver a la BD.
/// SqlDataAdapter.Fill no tiene versión asíncrona, por lo que se ejecuta en un hilo del pool.
/// </summary>
public sealed class PlanRevenueReportService(AppDbContext db, TimeProvider clock) : IPlanRevenueReport
{
    public Task<PlanRevenueReport> GenerateAsync(CancellationToken ct) => Task.Run(() => Generate(ct), ct);

    private PlanRevenueReport Generate(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var ds = new DataSet("PlanRevenue");

        using var conn = new SqlConnection(db.Database.GetConnectionString());
        using var adapter = new SqlDataAdapter();

        // --- Tabla 1: planes ---
        adapter.SelectCommand = new SqlCommand(
            "SELECT Id, Code, Name, MonthlyPrice FROM dbo.Plans WHERE IsActive = @active OR @active IS NULL ORDER BY SortOrder", conn);
        adapter.SelectCommand.Parameters.Add(new SqlParameter("@active", SqlDbType.Bit) { Value = DBNull.Value });
        adapter.Fill(ds, "Plans");
        ct.ThrowIfCancellationRequested();

        // --- Tabla 2: suscripciones activas agrupadas por plan y origen de pago ---
        adapter.SelectCommand = new SqlCommand("""
            SELECT s.PlanId, s.PaymentMode, COUNT(*) AS Total
            FROM dbo.Subscriptions s
            WHERE s.Status = @status AND (s.EndsAt IS NULL OR s.EndsAt > @now)
            GROUP BY s.PlanId, s.PaymentMode
            """, conn);
        adapter.SelectCommand.Parameters.Add(new SqlParameter("@status", SqlDbType.NVarChar, 20) { Value = "Active" });
        adapter.SelectCommand.Parameters.Add(new SqlParameter("@now", SqlDbType.DateTime2) { Value = now });
        adapter.Fill(ds, "ActiveSubscriptions");

        var plans = ds.Tables["Plans"]!;
        var subs = ds.Tables["ActiveSubscriptions"]!;

        // --- Manipulación desconectada ---
        ds.Relations.Add("Plan_Subscriptions", plans.Columns["Id"]!, subs.Columns["PlanId"]!);
        subs.Columns.Add("MonthlyValue", typeof(decimal), "Total * Parent(Plan_Subscriptions).MonthlyPrice");

        var rows = new List<PlanRevenueRow>();
        foreach (DataRow plan in plans.Rows)
        {
            var children = plan.GetChildRows("Plan_Subscriptions");
            int Count(string mode) => children.Where(r => (string)r["PaymentMode"] == mode).Sum(r => (int)r["Total"]);
            decimal Value(string mode) => children.Where(r => (string)r["PaymentMode"] == mode).Sum(r => (decimal)r["MonthlyValue"]);
            rows.Add(new PlanRevenueRow((string)plan["Code"], (string)plan["Name"], (decimal)plan["MonthlyPrice"],
                children.Sum(r => (int)r["Total"]), Count("Real"), Count("Simulated"), Count("Complimentary"), Count("None"),
                Value("Real"), Value("Simulated"), Value("Complimentary")));
        }

        // Totales con DataTable.Compute / DataView (sobre datos ya desconectados)
        decimal Total(string mode) => subs.Compute("SUM(MonthlyValue)", $"PaymentMode = '{mode}'") is decimal d ? d : 0m;
        var active = new DataView(subs) { RowFilter = "Total > 0" }.Count == 0 ? 0 : Convert.ToInt32(subs.Compute("SUM(Total)", ""));

        return new PlanRevenueReport(now, rows, Total("Real"), Total("Simulated"), Total("Complimentary"), active);
    }
}
