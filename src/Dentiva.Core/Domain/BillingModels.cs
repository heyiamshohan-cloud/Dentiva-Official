namespace Dentiva.Core.Domain;

public sealed class ServiceItem
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public long DefaultPriceMinor { get; set; }
    public decimal TaxRatePercent { get; set; }
    public bool IsActive { get; set; } = true;
    public string Notes { get; set; } = string.Empty;
}

public sealed class Invoice
{
    public long Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public long PatientId { get; set; }
    public long? VisitId { get; set; }
    public DateOnly InvoiceDate { get; set; }
    public DateOnly? DueDate { get; set; }

    public long SubtotalMinor { get; set; }
    public DiscountKind DiscountKind { get; set; }
    public decimal DiscountValue { get; set; }
    public long DiscountMinor { get; set; }
    public decimal TaxPercent { get; set; }
    public long TaxMinor { get; set; }
    public long TotalMinor { get; set; }
    public long PaidMinor { get; set; }
    public long DueMinor { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Unpaid;

    public string Notes { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? VoidedAt { get; set; }
    public string VoidReason { get; set; } = string.Empty;

    public List<InvoiceItem> Items { get; set; } = new();

    public static InvoiceStatus DeriveStatus(long totalMinor, long paidMinor, bool voided) =>
        voided ? InvoiceStatus.Voided
        : paidMinor <= 0 ? InvoiceStatus.Unpaid
        : paidMinor < totalMinor ? InvoiceStatus.PartiallyPaid
        : InvoiceStatus.Paid;
}

public sealed class InvoiceItem
{
    public long Id { get; set; }
    public long InvoiceId { get; set; }
    public long? ServiceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Tooth { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1m;
    public long UnitPriceMinor { get; set; }
    public long DiscountMinor { get; set; }
    public long AmountMinor { get; set; }
}

public sealed class Payment
{
    public long Id { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public long InvoiceId { get; set; }
    public long PatientId { get; set; }
    public DateTime PaidAt { get; set; }
    public long AmountMinor { get; set; }
    public PaymentMethodKind Method { get; set; }
    public string MethodLabel { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? VoidedAt { get; set; }
    public string VoidReason { get; set; } = string.Empty;
}
