using Dentiva.Core.Domain;
using Dentiva.Core.Numbering;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Dentiva.Infrastructure.Repositories;

public sealed record InvoiceQuery
{
    public long? PatientId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public InvoiceStatus? Status { get; init; }
    public bool? OnlyOutstanding { get; init; }
    public string SearchText { get; init; } = string.Empty;
    public int Limit { get; init; } = 200;
}

public sealed record InvoiceListItem(
    long Id, string InvoiceNumber, long PatientId, string PatientName, string PatientCode,
    DateOnly InvoiceDate, long TotalMinor, long PaidMinor, long DueMinor, InvoiceStatus Status, long VisitId);

/// <summary>
/// Services catalog, invoices and payments. Payment recording updates the
/// invoice balance inside the same transaction, so stored totals can never
/// disagree with the payment history.
/// </summary>
public sealed class BillingRepository
{
    private readonly ISqliteConnectionFactory _factory;
    private readonly CounterService _counters;

    public BillingRepository(ISqliteConnectionFactory factory, CounterService counters)
    {
        _factory = factory;
        _counters = counters;
    }

    // ---------- Service catalog ----------

    public IReadOnlyList<ServiceItem> Services(bool includeInactive = true)
    {
        using var connection = _factory.CreateOpenConnection();
        var filter = includeInactive ? string.Empty : "WHERE is_active = 1";
        return connection.Query<ServiceItem>(
            $"SELECT id AS Id, name AS Name, category AS Category, default_price_minor AS DefaultPriceMinor, tax_rate AS TaxRatePercent, is_active AS IsActive, notes AS Notes FROM services {filter} ORDER BY category, name").AsList();
    }

    public ServiceItem UpsertService(ServiceItem service)
    {
        using var connection = _factory.CreateOpenConnection();
        if (service.Id == 0)
        {
            service.Id = connection.ExecuteScalar<long>("""
                INSERT INTO services(name, category, default_price_minor, tax_rate, is_active, notes)
                VALUES (@Name, @Category, @DefaultPriceMinor, @TaxRatePercent, @IsActive, @Notes)
                RETURNING id
                """,
                service);
        }
        else
        {
            connection.Execute("""
                UPDATE services SET name = @Name, category = @Category, default_price_minor = @DefaultPriceMinor,
                    tax_rate = @TaxRatePercent, is_active = @IsActive, notes = @Notes
                WHERE id = @Id
                """,
                service);
        }

        return service;
    }

    public void DeactivateService(long serviceId)
    {
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("UPDATE services SET is_active = 0 WHERE id = @id", new { id = serviceId });
    }

    // ---------- Invoices ----------

    private const string InvoiceListSelect = """
        SELECT i.id AS Id, i.invoice_no AS InvoiceNumber, i.patient_id AS PatientId,
               pa.full_name AS PatientName, pa.code AS PatientCode, i.date AS InvoiceDate,
               i.total_minor AS TotalMinor, i.paid_minor AS PaidMinor, i.due_minor AS DueMinor,
               i.status AS Status, i.visit_id AS VisitId
        FROM invoices i JOIN patients pa ON pa.id = i.patient_id
        """;

    public Invoice CreateInvoice(Invoice invoice, AppSettings settings, SessionContext session)
    {
        invoice.CreatedBy = session.Username;
        invoice.CreatedAt = DateTimeOffset.UtcNow;
        invoice.UpdatedAt = invoice.CreatedAt;

        using var connection = _factory.CreateOpenConnection();

        return Db.InTransaction(connection, c =>
        {
            var date = invoice.InvoiceDate;
            var scope = CounterService.ScopeForTemplate(settings.Billing.InvoiceNumberTemplate, date);
            var seq = _counters.Next(c, null, "invoice", scope);
            invoice.InvoiceNumber = NumberFormatter.Format(settings.Billing.InvoiceNumberTemplate, seq, date);

            invoice.Id = c.ExecuteScalar<long>("""
                INSERT INTO invoices(invoice_no, patient_id, visit_id, date, due_date, subtotal_minor,
                    discount_kind, discount_value, discount_minor, tax_percent, tax_minor, total_minor,
                    paid_minor, due_minor, status, notes, created_by, created_at, updated_at)
                VALUES (@InvoiceNumber, @PatientId, @VisitId, @InvoiceDate, @DueDate, @SubtotalMinor,
                    @DiscountKind, @DiscountValue, @DiscountMinor, @TaxPercent, @TaxMinor, @TotalMinor,
                    @PaidMinor, @DueMinor, @Status, @Notes, @CreatedBy, @CreatedAt, @UpdatedAt)
                RETURNING id
                """,
                invoice);

            foreach (var item in invoice.Items)
            {
                item.InvoiceId = invoice.Id;
                c.Execute("""
                    INSERT INTO invoice_items(invoice_id, service_id, description, tooth, quantity, unit_price_minor, discount_minor, amount_minor)
                    VALUES (@InvoiceId, @ServiceId, @Description, @Tooth, @Quantity, @UnitPriceMinor, @DiscountMinor, @AmountMinor)
                    """,
                    item);
            }

            return invoice;
        });
    }

    public Invoice? GetInvoice(long id)
    {
        using var connection = _factory.CreateOpenConnection();
        var invoice = connection.QuerySingleOrDefault<Invoice>(
            "SELECT id AS Id, invoice_no AS InvoiceNumber, patient_id AS PatientId, visit_id AS VisitId, date AS InvoiceDate, due_date AS DueDate, subtotal_minor AS SubtotalMinor, discount_kind AS DiscountKind, discount_value AS DiscountValue, discount_minor AS DiscountMinor, tax_percent AS TaxPercent, tax_minor AS TaxMinor, total_minor AS TotalMinor, paid_minor AS PaidMinor, due_minor AS DueMinor, status AS Status, notes AS Notes, created_by AS CreatedBy, created_at AS CreatedAt, updated_at AS UpdatedAt, voided_at AS VoidedAt, void_reason AS VoidReason FROM invoices WHERE id = @id",
            new { id });

        if (invoice is not null)
        {
            invoice.Items = connection.Query<InvoiceItem>(
                "SELECT id AS Id, invoice_id AS InvoiceId, service_id AS ServiceId, description AS Description, tooth AS Tooth, quantity AS Quantity, unit_price_minor AS UnitPriceMinor, discount_minor AS DiscountMinor, amount_minor AS AmountMinor FROM invoice_items WHERE invoice_id = @id ORDER BY id",
                new { id }).AsList();
        }

        return invoice;
    }

    public Invoice? GetInvoiceByNumber(string invoiceNumber)
    {
        using var connection = _factory.CreateOpenConnection();
        var id = connection.ExecuteScalar<long?>("SELECT id FROM invoices WHERE invoice_no = @n", new { n = invoiceNumber });
        return id is null ? null : GetInvoice(id.Value);
    }

    public IReadOnlyList<InvoiceListItem> QueryInvoices(InvoiceQuery query)
    {
        using var connection = _factory.CreateOpenConnection();

        var conditions = new List<string> { "1 = 1" };
        var p = new DynamicParameters();

        if (query.PatientId is { } pid) { p.Add("pid", pid); conditions.Add("i.patient_id = @pid"); }
        if (query.From is { } from) { p.Add("from", from.ToString("yyyy-MM-dd")); conditions.Add("i.date >= @from"); }
        if (query.To is { } to) { p.Add("to", to.ToString("yyyy-MM-dd")); conditions.Add("i.date <= @to"); }
        if (query.Status is { } status) { p.Add("status", (int)status); conditions.Add("i.status = @status"); }
        if (query.OnlyOutstanding == true) { conditions.Add("i.due_minor > 0 AND i.voided_at IS NULL"); }
        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            p.Add("term", query.SearchText.Trim() + "%");
            conditions.Add("(i.invoice_no LIKE @term OR pa.full_name LIKE @term OR pa.code LIKE @term)");
        }

        p.Add("limit", query.Limit);

        return connection.Query<InvoiceListItem>(
            $"{InvoiceListSelect} WHERE {string.Join(" AND ", conditions)} ORDER BY i.date DESC, i.id DESC LIMIT @limit",
            p).AsList();
    }

    /// <summary>Void (soft-void) an invoice. Never hard-deletes financial history.</summary>
    public void VoidInvoice(long invoiceId, string reason, SessionContext session)
    {
        using var connection = _factory.CreateOpenConnection();
        Db.InTransaction(connection, c =>
        {
            var count = c.ExecuteScalar<long>("SELECT COUNT(*) FROM payments WHERE invoice_id = @id AND voided_at IS NULL", new { id = invoiceId });
            if (count > 0)
            {
                throw new InvalidOperationException("Void the recorded payments before voiding this invoice.");
            }

            c.Execute("""
                UPDATE invoices SET status = @voided, voided_at = @now, void_reason = @reason, due_minor = 0, updated_at = @now
                WHERE id = @id
                """,
                new { voided = (int)InvoiceStatus.Voided, now = DateTimeOffset.UtcNow.ToString("o"), reason, id = invoiceId });
            return true;
        });
    }

    // ---------- Payments ----------

    public Payment RecordPayment(Payment payment, AppSettings settings, SessionContext session)
    {
        payment.CreatedBy = session.Username;
        payment.CreatedAt = DateTimeOffset.UtcNow;

        using var connection = _factory.CreateOpenConnection();

        return Db.InTransaction(connection, c =>
        {
            var invoice = c.QuerySingleOrDefault<(long Total, long Paid)>(
                "SELECT total_minor, paid_minor FROM invoices WHERE id = @id", new { id = payment.InvoiceId });
            if (invoice == default)
            {
                throw new InvalidOperationException("The invoice for this payment was not found.");
            }

            var date = DateOnly.FromDateTime(payment.PaidAt);
            var scope = CounterService.ScopeForTemplate(settings.Billing.ReceiptNumberTemplate, date);
            var seq = _counters.Next(c, null, "receipt", scope);
            payment.ReceiptNumber = NumberFormatter.Format(settings.Billing.ReceiptNumberTemplate, seq, date);

            payment.Id = c.ExecuteScalar<long>("""
                INSERT INTO payments(receipt_no, invoice_id, patient_id, paid_at, amount_minor, method, method_label, reference, notes, created_by, created_at)
                VALUES (@ReceiptNumber, @InvoiceId, @PatientId, @PaidAt, @AmountMinor, @Method, @MethodLabel, @Reference, @Notes, @CreatedBy, @CreatedAt)
                RETURNING id
                """,
                payment);

            var newPaid = invoice.Paid + payment.AmountMinor;
            var newDue = invoice.Total - newPaid;
            var status = Invoice.DeriveStatus(invoice.Total, newPaid, voided: false);
            c.Execute("""
                UPDATE invoices SET paid_minor = @paid, due_minor = @due, status = @status, updated_at = @now
                WHERE id = @id
                """,
                new { paid = newPaid, due = newDue, status = (int)status, now = DateTimeOffset.UtcNow.ToString("o"), id = payment.InvoiceId });

            return payment;
        });
    }

    public IReadOnlyList<Payment> GetPayments(long invoiceId)
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.Query<Payment>(
            "SELECT id AS Id, receipt_no AS ReceiptNumber, invoice_id AS InvoiceId, patient_id AS PatientId, paid_at AS PaidAt, amount_minor AS AmountMinor, method AS Method, method_label AS MethodLabel, reference AS Reference, notes AS Notes, created_by AS CreatedBy, created_at AS CreatedAt, voided_at AS VoidedAt, void_reason AS VoidReason FROM payments WHERE invoice_id = @id AND voided_at IS NULL ORDER BY paid_at, id",
            new { id = invoiceId }).AsList();
    }

    public IReadOnlyList<Payment> QueryPayments(DateOnly? from, DateOnly? to, long? patientId, int limit = 300)
    {
        using var connection = _factory.CreateOpenConnection();
        var conditions = new List<string> { "voided_at IS NULL" };
        var p = new DynamicParameters();
        if (from is { } f) { p.Add("from", f.ToString("yyyy-MM-dd") + " 00:00:00"); conditions.Add("paid_at >= @from"); }
        if (to is { } t) { p.Add("to", t.ToString("yyyy-MM-dd") + " 23:59:59"); conditions.Add("paid_at <= @to"); }
        if (patientId is { } pid) { p.Add("pid", pid); conditions.Add("patient_id = @pid"); }
        p.Add("limit", limit);

        return connection.Query<Payment>(
            "SELECT id AS Id, receipt_no AS ReceiptNumber, invoice_id AS InvoiceId, patient_id AS PatientId, paid_at AS PaidAt, amount_minor AS AmountMinor, method AS Method, method_label AS MethodLabel, reference AS Reference, notes AS Notes, created_by AS CreatedBy, created_at AS CreatedAt FROM payments WHERE " + string.Join(" AND ", conditions) + " ORDER BY paid_at DESC LIMIT @limit",
            p).AsList();
    }

    /// <summary>Voids a payment and re-derives the invoice balance in one transaction.</summary>
    public void VoidPayment(long paymentId, string reason, SessionContext session)
    {
        using var connection = _factory.CreateOpenConnection();
        Db.InTransaction(connection, c =>
        {
            var payment = c.QuerySingleOrDefault<(long InvoiceId, long Amount)>(
                "SELECT invoice_id, amount_minor FROM payments WHERE id = @id AND voided_at IS NULL", new { id = paymentId });
            if (payment == default)
            {
                throw new InvalidOperationException("This payment was not found or is already voided.");
            }

            c.Execute("UPDATE payments SET voided_at = @now, void_reason = @reason WHERE id = @id",
                new { now = DateTimeOffset.UtcNow.ToString("o"), reason, id = paymentId });

            var inv = c.QuerySingleOrDefault<(long Total, long Paid)>(
                "SELECT total_minor, paid_minor FROM invoices WHERE id = @id", new { id = payment.InvoiceId });
            var newPaid = inv.Paid - payment.Amount;
            var status = Invoice.DeriveStatus(inv.Total, newPaid, voided: false);
            c.Execute("""
                UPDATE invoices SET paid_minor = @paid, due_minor = @due, status = @status, updated_at = @now
                WHERE id = @id
                """,
                new { paid = newPaid, due = inv.Total - newPaid, status = (int)status, now = DateTimeOffset.UtcNow.ToString("o"), id = payment.InvoiceId });
            return true;
        });
    }

    // ---------- Aggregates ----------

    public sealed record BillingSummary(long InvoiceCount, long BilledMinor, long PaidMinor, long DueMinor);

    public BillingSummary Summary(DateOnly from, DateOnly to)
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.QuerySingle<BillingSummary>(
            """
            SELECT COUNT(*) AS InvoiceCount,
                   COALESCE(SUM(total_minor), 0) AS BilledMinor,
                   COALESCE(SUM(paid_minor), 0) AS PaidMinor,
                   COALESCE(SUM(due_minor), 0) AS DueMinor
            FROM invoices
            WHERE voided_at IS NULL AND date >= @from AND date <= @to
            """,
            new { from = from.ToString("yyyy-MM-dd"), to = to.ToString("yyyy-MM-dd") });
    }

    public IReadOnlyList<(string Service, long Count, long AmountMinor)> TopServices(DateOnly from, DateOnly to, int limit = 10)
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.Query<(string Service, long Count, long AmountMinor)>(
            """
            SELECT it.description AS Service, COUNT(*) AS Count, SUM(it.amount_minor) AS AmountMinor
            FROM invoice_items it
            JOIN invoices i ON i.id = it.invoice_id
            WHERE i.voided_at IS NULL AND i.date >= @from AND i.date <= @to
            GROUP BY it.description
            ORDER BY AmountMinor DESC
            LIMIT @limit
            """,
            new { from = from.ToString("yyyy-MM-dd"), to = to.ToString("yyyy-MM-dd"), limit }).AsList();
    }

    public IReadOnlyList<(string Method, long Count, long AmountMinor)> PaymentMethodBreakdown(DateOnly from, DateOnly to)
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.Query<(string, long, long)>(
            """
            SELECT COALESCE(NULLIF(method_label, ''), 'Cash') AS Method, COUNT(*) AS Count, SUM(amount_minor) AS AmountMinor
            FROM payments
            WHERE voided_at IS NULL AND paid_at >= @from AND paid_at <= @to
            GROUP BY COALESCE(NULLIF(method_label, ''), 'Cash')
            ORDER BY AmountMinor DESC
            """,
            new
            {
                from = from.ToString("yyyy-MM-dd") + " 00:00:00",
                to = to.ToString("yyyy-MM-dd") + " 23:59:59",
            }).AsList();
    }
}
