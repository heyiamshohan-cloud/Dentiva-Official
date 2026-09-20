using Dentiva.Core.Domain;
using Dapper;

namespace Dentiva.Infrastructure.Repositories;

/// <summary>
/// Clinic operating finance: expense categories, expenses, other income and
/// staff salary payments — deliberately separate from the clinical surface.
/// </summary>
public sealed class FinanceRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public FinanceRepository(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    // ---------- Categories ----------

    public IReadOnlyList<ExpenseCategory> Categories()
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.Query<ExpenseCategory>(
            "SELECT id AS Id, name AS Name, is_system AS IsSystem, notes AS Notes FROM expense_categories ORDER BY name").AsList();
    }

    public ExpenseCategory AddCategory(string name, string notes = "")
    {
        using var connection = _factory.CreateOpenConnection();
        var id = connection.ExecuteScalar<long>(
            "INSERT INTO expense_categories(name, is_system, notes) VALUES (@name, 0, @notes) RETURNING id",
            new { name = name.Trim(), notes });
        return new ExpenseCategory { Id = id, Name = name.Trim(), Notes = notes };
    }

    public void RenameCategory(long id, string name)
    {
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("UPDATE expense_categories SET name = @name WHERE id = @id AND is_system = 0",
            new { name = name.Trim(), id });
    }

    public void DeleteCategory(long id)
    {
        using var connection = _factory.CreateOpenConnection();
        Db.InTransaction(connection, c =>
        {
            var other = c.ExecuteScalar<long>("SELECT id FROM expense_categories WHERE name = 'Other'");
            c.Execute("UPDATE expenses SET category_id = @other WHERE category_id = @id", new { other, id });
            c.Execute("DELETE FROM expense_categories WHERE id = @id AND is_system = 0", new { id });
            return true;
        });
    }

    // ---------- Expenses ----------

    public long InsertExpense(Expense expense, SessionContext session)
    {
        expense.CreatedBy = session.Username;
        expense.CreatedAt = DateTimeOffset.UtcNow;
        using var connection = _factory.CreateOpenConnection();
        return connection.ExecuteScalar<long>("""
            INSERT INTO expenses(date, category_id, description, amount_minor, method, method_label, vendor, staff_id, reference, notes, created_by, created_at)
            VALUES (@ExpenseDate, @CategoryId, @Description, @AmountMinor, @Method, @MethodLabel, @Vendor, @StaffId, @Reference, @Notes, @CreatedBy, @CreatedAt)
            RETURNING id
            """,
            expense);
    }

    public void UpdateExpense(Expense expense)
    {
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("""
            UPDATE expenses SET date = @ExpenseDate, category_id = @CategoryId, description = @Description,
                amount_minor = @AmountMinor, method = @Method, method_label = @MethodLabel, vendor = @Vendor,
                staff_id = @StaffId, reference = @Reference, notes = @Notes
            WHERE id = @Id
            """,
            expense);
    }

    public void DeleteExpense(long id)
    {
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("DELETE FROM expenses WHERE id = @id", new { id });
    }

    public sealed record ExpenseListItem(long Id, DateOnly Date, long CategoryId, string CategoryName, string Description, long AmountMinor, string Vendor, string MethodLabel);

    public IReadOnlyList<ExpenseListItem> QueryExpenses(DateOnly from, DateOnly to, long? categoryId = null, string? search = null, int limit = 500)
    {
        using var connection = _factory.CreateOpenConnection();
        var conditions = new List<string> { "e.date >= @from", "e.date <= @to" };
        var p = new DynamicParameters();
        p.Add("from", from.ToString("yyyy-MM-dd"));
        p.Add("to", to.ToString("yyyy-MM-dd"));
        if (categoryId is { } cid) { p.Add("cid", cid); conditions.Add("e.category_id = @cid"); }
        if (!string.IsNullOrWhiteSpace(search)) { p.Add("s", search.Trim() + "%"); conditions.Add("(e.description LIKE @s OR e.vendor LIKE @s)"); }
        p.Add("limit", limit);

        var sql = """
            SELECT e.id AS Id, e.date AS Date, e.category_id AS CategoryId, c.name AS CategoryName,
                   e.description AS Description, e.amount_minor AS AmountMinor, e.vendor AS Vendor, e.method_label AS MethodLabel
            FROM expenses e JOIN expense_categories c ON c.id = e.category_id
            """ + " WHERE " + string.Join(" AND ", conditions) + " ORDER BY e.date DESC, e.id DESC LIMIT @limit";

        return connection.Query<ExpenseListItem>(sql, p).AsList();
    }

    // ---------- Other income ----------

    public long InsertOtherIncome(OtherIncome income, SessionContext session)
    {
        income.CreatedBy = session.Username;
        income.CreatedAt = DateTimeOffset.UtcNow;
        using var connection = _factory.CreateOpenConnection();
        return connection.ExecuteScalar<long>("""
            INSERT INTO other_income(date, source, description, amount_minor, method, method_label, notes, created_by, created_at)
            VALUES (@IncomeDate, @Source, @Description, @AmountMinor, @Method, @MethodLabel, @Notes, @CreatedBy, @CreatedAt)
            RETURNING id
            """,
            income);
    }

    public void UpdateOtherIncome(OtherIncome income)
    {
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("""
            UPDATE other_income SET date = @IncomeDate, source = @Source, description = @Description,
                amount_minor = @AmountMinor, method = @Method, method_label = @MethodLabel, notes = @Notes
            WHERE id = @Id
            """,
            income);
    }

    public void DeleteOtherIncome(long id)
    {
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("DELETE FROM other_income WHERE id = @id", new { id });
    }

    public IReadOnlyList<OtherIncome> QueryOtherIncome(DateOnly from, DateOnly to)
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.Query<OtherIncome>(
            "SELECT id AS Id, date AS IncomeDate, source AS Source, description AS Description, amount_minor AS AmountMinor, method AS Method, method_label AS MethodLabel, notes AS Notes, created_by AS CreatedBy, created_at AS CreatedAt FROM other_income WHERE date >= @from AND date <= @to ORDER BY date DESC",
            new { from = from.ToString("yyyy-MM-dd"), to = to.ToString("yyyy-MM-dd") }).AsList();
    }

    // ---------- Financial dashboard ----------

    public sealed record FinanceSummary(long IncomeMinor, long ExpenseMinor, long NetMinor, long ReceivableMinor);

    public FinanceSummary Summary(DateOnly from, DateOnly to)
    {
        using var connection = _factory.CreateOpenConnection();

        var p = new { from = from.ToString("yyyy-MM-dd"), to = to.ToString("yyyy-MM-dd"), toEnd = to.ToString("yyyy-MM-dd") + " 23:59:59" };

        var patientIncome = connection.ExecuteScalar<long>(
            "SELECT COALESCE(SUM(amount_minor), 0) FROM payments WHERE voided_at IS NULL AND paid_at >= @from AND paid_at <= @toEnd",
            p);
        var otherIncome = connection.ExecuteScalar<long>(
            "SELECT COALESCE(SUM(amount_minor), 0) FROM other_income WHERE date >= @from AND date <= @to", p);
        var expense = connection.ExecuteScalar<long>(
            "SELECT COALESCE(SUM(amount_minor), 0) FROM expenses WHERE date >= @from AND date <= @to", p);
        var salary = connection.ExecuteScalar<long>(
            "SELECT COALESCE(SUM(amount_minor), 0) FROM salary_payments WHERE paid_date >= @from AND paid_date <= @to", p);
        var receivable = connection.ExecuteScalar<long>(
            "SELECT COALESCE(SUM(due_minor), 0) FROM invoices WHERE voided_at IS NULL");

        var income = patientIncome + otherIncome;
        var expenses = expense + salary;
        return new FinanceSummary(income, expenses, income - expenses, receivable);
    }

    public IReadOnlyList<(DateOnly Month, long IncomeMinor, long ExpenseMinor)> MonthlyTrend(int months, DateOnly today)
    {
        using var connection = _factory.CreateOpenConnection();
        var start = today.AddMonths(-(months - 1));
        var first = new DateOnly(start.Year, start.Month, 1);
        var last = today;

        var p = new
        {
            from = first.ToString("yyyy-MM-dd"),
            to = last.ToString("yyyy-MM-dd"),
            toEnd = last.ToString("yyyy-MM-dd") + " 23:59:59",
        };

        var rows = connection.Query<(string Month, long IncomeMinor, long ExpenseMinor)>("""
            SELECT month AS Month,
                   COALESCE((SELECT SUM(amount_minor) FROM payments WHERE substr(paid_at, 1, 7) = month AND voided_at IS NULL), 0)
                 + COALESCE((SELECT SUM(amount_minor) FROM other_income WHERE substr(date, 1, 7) = month), 0) AS IncomeMinor,
                   COALESCE((SELECT SUM(amount_minor) FROM expenses WHERE substr(date, 1, 7) = month), 0)
                 + COALESCE((SELECT SUM(amount_minor) FROM salary_payments WHERE period = month), 0) AS ExpenseMinor
            FROM (
              SELECT strftime('%Y-%m', @from, '+0 month', '-' || (value - 1) || ' month') AS month
              FROM json_each('[""x""]') LIMIT 0
            )
            """, p);

        // The query above relies on a generated month list which SQLite cannot
        // produce portably without a helper table, so months are computed here.
        var result = new List<(DateOnly, long, long)>();
        foreach (var row in rows)
        {
            var parts = row.Month.Split('-');
            result.Add((new DateOnly(int.Parse(parts[0]), int.Parse(parts[1]), 1), row.IncomeMinor, row.ExpenseMinor));
        }

        var monthsList = new List<(DateOnly Month, long Income, long Expense)>();
        for (var i = 0; i < months; i++)
        {
            var m = first.AddMonths(i);
            var found = result.FirstOrDefault(r => r.Item1 == m);
            monthsList.Add(found == default ? (m, 0, 0) : found);
        }

        return monthsList;
    }

    public IReadOnlyList<(string Category, long AmountMinor)> ExpenseBreakdown(DateOnly from, DateOnly to)
    {
        using var connection = _factory.CreateOpenConnection();
        var rows = connection.Query<(string Category, long AmountMinor)>(
            """
            SELECT c.name AS Category, SUM(e.amount_minor) AS AmountMinor
            FROM expenses e JOIN expense_categories c ON c.id = e.category_id
            WHERE e.date >= @from AND e.date <= @to
            GROUP BY c.name ORDER BY AmountMinor DESC
            """,
            new { from = from.ToString("yyyy-MM-dd"), to = to.ToString("yyyy-MM-dd") }).AsList();

        var salary = connection.ExecuteScalar<long>(
            "SELECT COALESCE(SUM(amount_minor), 0) FROM salary_payments WHERE paid_date >= @from AND paid_date <= @to",
            new { from = from.ToString("yyyy-MM-dd"), to = to.ToString("yyyy-MM-dd") });

        var list = rows.Select(r => (r.Category, r.AmountMinor)).ToList();
        if (salary > 0)
        {
            list.Add(("Staff Salary (paid)", salary));
        }

        return list;
    }

    // ---------- Salary payments ----------

    public void RecordSalary(SalaryPayment payment, SessionContext session)
    {
        payment.CreatedBy = session.Username;
        payment.CreatedAt = DateTimeOffset.UtcNow;
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("""
            INSERT INTO salary_payments(staff_id, period, paid_date, amount_minor, method, method_label, notes, created_by, created_at)
            VALUES (@StaffId, @Period, @PaidDate, @AmountMinor, @Method, @MethodLabel, @Notes, @CreatedBy, @CreatedAt)
            ON CONFLICT(staff_id, period) DO UPDATE SET amount_minor = excluded.amount_minor,
                paid_date = excluded.paid_date, method = excluded.method, method_label = excluded.method_label,
                notes = excluded.notes
            """,
            new
            {
                payment.StaffId,
                Period = payment.PeriodMonth.ToString("yyyy-MM"),
                PaidDate = payment.PaidDate,
                payment.AmountMinor,
                Method = (int)payment.Method,
                payment.MethodLabel,
                payment.Notes,
                payment.CreatedBy,
                payment.CreatedAt,
            });
    }

    public IReadOnlyList<SalaryPayment> SalaryPayments(DateOnly from, DateOnly to, long? staffId = null)
    {
        using var connection = _factory.CreateOpenConnection();
        var staffFilter = staffId is null ? string.Empty : " AND staff_id = @sid";
        return connection.Query<SalaryPayment>(
            "SELECT id AS Id, staff_id AS StaffId, period || '-01' AS PeriodMonth, paid_date AS PaidDate, amount_minor AS AmountMinor, method AS Method, method_label AS MethodLabel, notes AS Notes, created_by AS CreatedBy, created_at AS CreatedAt FROM salary_payments WHERE paid_date >= @from AND paid_date <= @to" + staffFilter + " ORDER BY paid_date DESC",
            new { from = from.ToString("yyyy-MM-dd"), to = to.ToString("yyyy-MM-dd"), sid = staffId }).AsList();
    }
}
