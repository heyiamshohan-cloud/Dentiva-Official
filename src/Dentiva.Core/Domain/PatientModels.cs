namespace Dentiva.Core.Domain;

public sealed class Patient
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PreferredName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string AlternatePhone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Occupation { get; set; } = string.Empty;
    public string EmergencyContactName { get; set; } = string.Empty;
    public string EmergencyContactRelation { get; set; } = string.Empty;
    public string EmergencyContactPhone { get; set; } = string.Empty;

    // Clinical summary
    public string ChiefComplaint { get; set; } = string.Empty;
    public string MedicalHistory { get; set; } = string.Empty;
    public string DentalHistory { get; set; } = string.Empty;
    public string Allergies { get; set; } = string.Empty;
    public string CurrentMedications { get; set; } = string.Empty;
    public string RiskFactors { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public PatientStatus Status { get; set; } = PatientStatus.Active;
    public DateOnly RegistrationDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // Optional computed values for lists (not persisted directly)
    public DateOnly? LastVisitDate { get; set; }
    public DateOnly? NextAppointmentDate { get; set; }
    public long OutstandingMinor { get; set; }

    public int? AgeYears(DateOnly today)
    {
        if (DateOfBirth is null)
        {
            return null;
        }

        var dob = DateOfBirth.Value;
        var age = today.Year - dob.Year;
        if (today < dob.AddYears(age))
        {
            age--;
        }

        return Math.Max(age, 0);
    }

    public string DisplayName => string.IsNullOrWhiteSpace(PreferredName) ? FullName : PreferredName;

    public string AgeLabel
    {
        get
        {
            var age = AgeYears(DateOnly.FromDateTime(DateTime.Today));
            return age is null ? "\u2014" : age + "y";
        }
    }
}

public sealed class PatientTag
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#0E7490";
    public bool IsSystem { get; set; }
}

public sealed class PatientTagLink
{
    public long PatientId { get; set; }
    public long TagId { get; set; }
}
