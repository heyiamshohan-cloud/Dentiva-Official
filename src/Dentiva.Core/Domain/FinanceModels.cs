namespace Dentiva.Core.Domain;

public sealed class ExpenseCategory
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class Expense
{
    public long Id { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public long CategoryId { get; set; }
    public string Description { get; set; } = string.Empty;
    public long AmountMinor { get; set; }
    public PaymentMethodKind Method { get; set; }
    public string MethodLabel { get; set; } = string.Empty;
    public string Vendor { get; set; } = string.Empty;
    public long? StaffId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class OtherIncome
{
    public long Id { get; set; }
    public DateOnly IncomeDate { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long AmountMinor { get; set; }
    public PaymentMethodKind Method { get; set; }
    public string MethodLabel { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class StaffMember
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public DateOnly JoiningDate { get; set; }
    public long SalaryMinor { get; set; }
    public SalaryFrequency SalaryFrequency { get; set; } = SalaryFrequency.Monthly;
    public string Responsibilities { get; set; } = string.Empty;
    public StaffStatus Status { get; set; } = StaffStatus.Active;
    public string EmergencyContactName { get; set; } = string.Empty;
    public string EmergencyContactPhone { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class SalaryPayment
{
    public long Id { get; set; }
    public long StaffId { get; set; }
    public DateOnly PeriodMonth { get; set; }
    public DateOnly PaidDate { get; set; }
    public long AmountMinor { get; set; }
    public PaymentMethodKind Method { get; set; }
    public string MethodLabel { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
