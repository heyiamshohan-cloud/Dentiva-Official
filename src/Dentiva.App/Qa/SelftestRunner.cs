using System.Text.Json;
using Dentiva.App.Services;
using Dentiva.Core.Domain;
using Dentiva.Core.Security;
using Dentiva.Core.Settings;
using Dentiva.Infrastructure;
using Dentiva.Infrastructure.Files;
using Dentiva.Infrastructure.Repositories;

namespace Dentiva.App.Qa;

/// <summary>
/// End-to-end verification of the real service stack against an isolated
/// database: setup, patients, appointments/serials, clinical records,
/// billing math, finance, staff, search, backup/restore, export and audit.
/// Executed by CI with --selftest; never touches a production data folder.
/// </summary>
public static class SelftestRunner
{
    private static readonly List<(string Name, bool Ok, string Detail)> Checks = new();

    public static JsonElement RunAll()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        Check("database-migrate", "Schema migrates and passes integrity check", VerifyDatabase);
        Check("settings", "Settings persist and round-trip", VerifySettings);
        Check("security", "Admin user creation and authentication", VerifyUsers);
        Check("patients", "Patient lifecycle, code generation and search", VerifyPatients);
        Check("appointments", "Appointments with serial allocation and conflicts", VerifyAppointments);
        Check("clinical", "Visit, procedures, tooth chart, plan, prescription, referral", VerifyClinical);
        Check("billing", "Invoice math, payments, balances, void flows", VerifyBilling);
        Check("finance", "Expenses, other income, summary correctness", VerifyFinance);
        Check("staff", "Staff records and salary payment", VerifyStaff);
        Check("search", "Global FTS search finds patients and invoices", VerifySearch);
        Check("backup", "Backup creation, manifest and restore round-trip", VerifyBackup);
        Check("export", "CSV/XLSX/JSON exports produce content", VerifyExports);
        Check("notifications", "Notification lifecycle", VerifyNotifications);
        Check("audit", "Audit trail records actions", VerifyAudit);

        stopwatch.Stop();

        var json = new Dictionary<string, object>
        {
            ["ok"] = Checks.All(c => c.Ok),
            ["version"] => App.Version,
            ["dataDir"] => App.DataDirectory,
            ["durationMs"] => stopwatch.ElapsedMilliseconds,
            ["checks"] => Checks.Select(c => new Dictionary<string, object>
            {
                ["name"] = c.Name,
                ["ok"] = c.Ok,
                ["detail"] = c.Detail,
            }).ToList(),
        };
        return JsonSerializer.SerializeToElement(json);
    }

    private static void Check(string name, string description, Action action)
    {
        try
        {
            action();
            Checks.Add((name, true, description));
        }
        catch (Exception ex)
        {
            Checks.Add((name, false, $"{description} — FAILED: {ex.Message}"));
        }
    }

    private static ISqliteConnectionFactory Factory => AppServices.Get<ISqliteConnectionFactory>();
    private static SessionContext Session { get; set; } = SessionContext.System;

    private static void VerifyDatabase()
    {
        var healthy = Database.IntegrityCheck(Factory);
        if (!string.Equals(healthy, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"integrity_check returned {healthy}");
        }
    }

    private static void VerifySettings()
    {
        var store = AppServices.Get<SettingsStore>();
        store.Save(s =>
        {
            s.Clinic.ClinicName = "Selftest Dental Care";
            s.Clinic.CurrencySymbol = "৳";
            s.Billing.InvoiceNumberTemplate = "INV-{YYYY}-{SEQ:4}";
        });

        var settings = AppServices.Get<SettingsRepository>().Load();
        if (settings.Clinic.ClinicName != "Selftest Dental Care")
        {
            throw new InvalidOperationException("clinic name did not round-trip");
        }
    }

    private static void VerifyUsers()
    {
        var users = AppServices.Get<UserRepository>();
        var admin = users.Create("qa.admin", "Str0ng!Passw0rd", "QA Administrator", PermissionPresets.Administrator);
        if (admin is null)
        {
            throw new InvalidOperationException("admin creation returned null");
        }

        var session = users.Authenticate("qa.admin", "Str0ng!Passw0rd")
            ?? throw new InvalidOperationException("authentication failed with correct password");
        if (users.Authenticate("qa.admin", "wrong") is not null)
        {
            throw new InvalidOperationException("authentication succeeded with wrong password");
        }

        Session = session;
    }

    private static void VerifyPatients()
    {
        var repo = AppServices.Get<PatientRepository>();
        var settings = AppServices.Get<SettingsStore>().Settings;
        var today = DateOnly.FromDateTime(DateTime.Today);

        var code = repo.GenerateCode(settings, today);
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("patient code generation failed");
        }

        var patient = new Patient
        {
            FullName = "Selftest Patient One",
            Phone = "01710000001",
            Gender = Gender.Male,
            RegistrationDate = today,
            Status = PatientStatus.Active,
        };
        repo.Insert(patient, code, Session);

        var secondCode = repo.GenerateCode(settings, today);
        if (string.Equals(secondCode, code, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("patient codes are not unique");
        }

        patient.Email = "updated@example.com";
        repo.Update(patient, Session);

        var reloaded = repo.GetById(patient.Id)
            ?? throw new InvalidOperationException("patient not found after update");
        if (reloaded.Email != "updated@example.com")
        {
            throw new InvalidOperationException("patient update did not persist");
        }

        if (repo.CodeExists(code))
        {
            throw new InvalidOperationException("code existence check reported false positive");
        }

        var page = repo.Search(new PatientQuery { SearchText = "Selftest", Today = today });
        if (page.TotalCount < 1)
        {
            throw new InvalidOperationException("patient search found no results");
        }
    }

    private static void VerifyAppointments()
    {
        var patients = AppServices.Get<PatientRepository>();
        var appointments = AppServices.Get<AppointmentRepository>();
        var settings = AppServices.Get<SettingsStore>().Settings;
        var today = DateOnly.FromDateTime(DateTime.Today);

        var patient = patients.Search(new PatientQuery { SearchText = "Selftest", Today = today }).Items[0];

        var appt = appointments.Insert(new Appointment
        {
            PatientId = patient.Id,
            AppointmentDate = today,
            StartTime = new TimeOnly(10, 0),
            AppointmentType = "Consultation",
            DoctorName = "Dr. QA",
        }, perDoctor: true, Session);

        if (appt.SerialNumber != 1)
        {
            throw new InvalidOperationException($"expected serial 1, got {appt.SerialNumber}");
        }

        var second = appointments.Insert(new Appointment
        {
            PatientId = patient.Id,
            AppointmentDate = today,
            StartTime = new TimeOnly(10, 30),
            AppointmentType = "Follow-up",
            DoctorName = "Dr. QA",
        }, perDoctor: true, Session);

        if (second.SerialNumber != 2)
        {
            throw new InvalidOperationException("serial did not increment");
        }

        // Manual serial collision must be rejected.
        try
        {
            appointments.Insert(new Appointment
            {
                PatientId = patient.Id,
                AppointmentDate = today,
                StartTime = new TimeOnly(11, 0),
                SerialNumber = 1,
                DoctorName = "Dr. QA",
            }, perDoctor: true, Session);
            throw new InvalidOperationException("duplicate serial was accepted");
        }
        catch (SerialConflictException)
        {
            // expected
        }

        var queue = appointments.DayQueue(today);
        if (queue.Count != 2)
        {
            throw new InvalidOperationException("day queue count mismatch");
        }

        appointments.UpdateStatus(second.Id, AppointmentStatus.Completed);
        var summary = appointments.DaySummary(today);
        if (summary.Completed != 1)
        {
            throw new InvalidOperationException("status change did not reflect in summary");
        }

        var moved = appointments.Reschedule(appt.Id, today.AddDays(1), new TimeOnly(12, 0), true, Session);
        if (moved.SerialNumber != 1 || moved.AppointmentDate != today.AddDays(1))
        {
            throw new InvalidOperationException("reschedule did not allocate correctly");
        }
    }

    private static void VerifyClinical()
    {
        var patients = AppServices.Get<PatientRepository>();
        var visits = AppServices.Get<VisitRepository>();
        var today = DateOnly.FromDateTime(DateTime.Today);

        var patient = patients.Search(new PatientQuery { SearchText = "Selftest", Today = today }).Items[0];

        var visit = visits.Insert(new Visit
        {
            PatientId = patient.Id,
            VisitDate = DateTime.Now,
            Reason = "Toothache",
            Diagnosis = "Irreversible pulpitis 36",
            ExaminationFindings = "Deep caries 36",
            DoctorName = "Dr. QA",
        }, Session);

        visits.ReplaceProcedures(visit.Id, new[]
        {
            new VisitProcedure { Name = "Root Canal Treatment", Tooth = "36", CostMinor = 800_00 },
        });

        visits.UpsertToothRecord(new ToothRecord
        {
            PatientId = patient.Id,
            ToothNumber = "36",
            Condition = ToothCondition.Decayed,
            Surfaces = "O",
            Notes = "Deep caries",
        });

        var plan = visits.InsertPlan(new TreatmentPlan
        {
            PatientId = patient.Id,
            Title = "Quadrant restoration plan",
            CreatedDate = today,
            Items =
            {
                new TreatmentPlanItem { TreatmentName = "Root Canal Treatment", Tooth = "36", EstimatedCostMinor = 800_00 },
                new TreatmentPlanItem { TreatmentName = "Porcelain Crown", Tooth = "36", EstimatedCostMinor = 1200_00 },
            },
        }, Session);

        if (plan.Items.Count != 2)
        {
            throw new InvalidOperationException("plan items were not saved");
        }

        visits.InsertMedication(new MedicationOrder
        {
            PatientId = patient.Id,
            VisitId = visit.Id,
            Name = "Amoxicillin",
            Strength = "500 mg",
            Dosage = "1 capsule",
            Frequency = "3 times daily",
            Duration = "5 days",
            PrescribedDate = today,
        });

        var referral = visits.InsertReferral(new Referral
        {
            PatientId = patient.Id,
            ReferralDate = today,
            Provider = "Dr. Specialist",
            Specialty = "Oral Surgery",
            Organization = "City Dental Hospital",
            Reason = "Surgical extraction 38",
        });

        if (visits.GetReferrals(patient.Id).Count != 1 || referral.Id == 0)
        {
            throw new InvalidOperationException("referral was not persisted");
        }

        var timeline = visits.GetTimeline(patient.Id);
        if (timeline.Count < 4)
        {
            throw new InvalidOperationException("timeline is missing entries");
        }
    }

    private static void VerifyBilling()
    {
        var patients = AppServices.Get<PatientRepository>();
        var billing = AppServices.Get<BillingRepository>();
        var settings = AppServices.Get<SettingsStore>().Settings;
        var today = DateOnly.FromDateTime(DateTime.Today);

        var patient = patients.Search(new PatientQuery { SearchText = "Selftest", Today = today }).Items[0];

        var lines = new[]
        {
            new InvoiceCalculator.LineInput { UnitPriceMinor = 800_00, Quantity = 1 },
            new InvoiceCalculator.LineInput { UnitPriceMinor = 1200_00, Quantity = 1, LineDiscountMinor = 100_00 },
        };
        var expected = InvoiceCalculator.Calculate(lines, 0, 10m, 0m, 0);
        // subtotal 2000 - line discount 100 = 1900; 10% discount = 190; total 1710

        var invoice = new Invoice
        {
            PatientId = patient.Id,
            InvoiceDate = today,
            SubtotalMinor = expected.SubtotalMinor,
            DiscountKind = DiscountKind.Percentage,
            DiscountValue = 10m,
            DiscountMinor = expected.InvoiceDiscountMinor,
            TaxPercent = 0m,
            TaxMinor = 0,
            TotalMinor = expected.GrandTotalMinor,
            PaidMinor = 0,
            DueMinor = expected.GrandTotalMinor,
            Status = InvoiceStatus.Unpaid,
            Items =
            {
                new InvoiceItem { Description = "Root Canal Treatment", Tooth = "36", Quantity = 1, UnitPriceMinor = 800_00, AmountMinor = 800_00 },
                new InvoiceItem { Description = "Porcelain Crown", Tooth = "36", Quantity = 1, UnitPriceMinor = 1200_00, DiscountMinor = 100_00, AmountMinor = 1100_00 },
            },
        };

        billing.CreateInvoice(invoice, settings, Session);

        if (invoice.InvoiceNumber is null || !invoice.InvoiceNumber.StartsWith("INV-"))
        {
            throw new InvalidOperationException("invoice number was not generated");
        }

        if (invoice.TotalMinor != 1710_00)
        {
            throw new InvalidOperationException($"invoice total {invoice.TotalMinor} != 171000");
        }

        billing.RecordPayment(new Payment
        {
            InvoiceId = invoice.Id,
            PatientId = patient.Id,
            PaidAt = DateTime.Now,
            AmountMinor = 700_00,
            Method = PaymentMethodKind.Cash,
            MethodLabel = "Cash",
        }, settings, Session);

        var afterFirst = billing.GetInvoice(invoice.Id)!;
        if (afterFirst.PaidMinor != 700_00 || afterFirst.DueMinor != 1010_00 || afterFirst.Status != InvoiceStatus.PartiallyPaid)
        {
            throw new InvalidOperationException($"balance after partial payment wrong: paid={afterFirst.PaidMinor} due={afterFirst.DueMinor}");
        }

        billing.RecordPayment(new Payment
        {
            InvoiceId = invoice.Id,
            PatientId = patient.Id,
            PaidAt = DateTime.Now,
            AmountMinor = 1010_00,
            Method = PaymentMethodKind.MobileFinancialService,
            MethodLabel = "Mobile Banking",
            Reference = "TXN-001",
        }, settings, Session);

        var afterSecond = billing.GetInvoice(invoice.Id)!;
        if (afterSecond.Status != InvoiceStatus.Paid || afterSecond.DueMinor != 0)
        {
            throw new InvalidOperationException("invoice did not reach paid state");
        }

        // Voiding an invoice with payments must be blocked.
        try
        {
            billing.VoidInvoice(invoice.Id, "test", Session);
            throw new InvalidOperationException("void invoice unexpectedly succeeded with active payments");
        }
        catch (InvalidOperationException)
        {
            // expected
        }

        var payments = billing.GetPayments(invoice.Id);
        billing.VoidPayment(payments[0].Id, "QA reversal", Session);
        var afterVoid = billing.GetInvoice(invoice.Id)!;
        if (afterVoid.PaidMinor != 1010_00 || afterVoid.Status != InvoiceStatus.PartiallyPaid)
        {
            throw new InvalidOperationException("payment void did not restore balance");
        }

        var summary = billing.Summary(today, today);
        if (summary.InvoiceCount < 1)
        {
            throw new InvalidOperationException("billing summary is empty");
        }
    }

    private static void VerifyFinance()
    {
        var finance = AppServices.Get<FinanceRepository>();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var categories = finance.Categories();
        var rent = categories.First(c => c.Name == "Clinic Rent");

        finance.InsertExpense(new Expense
        {
            ExpenseDate = today,
            CategoryId = rent.Id,
            Description = "September rent",
            AmountMinor = 2500_00,
            Method = PaymentMethodKind.Cash,
            MethodLabel = "Cash",
        }, Session);

        finance.InsertOtherIncome(new OtherIncome
        {
            IncomeDate = today,
            Source = "Pharmacy partnership",
            Description = "Referral commission",
            AmountMinor = 100_00,
        }, Session);

        var summary = finance.Summary(today, today);
        if (summary.ExpenseMinor < 2500_00 || summary.IncomeMinor < 100_00)
        {
            throw new InvalidOperationException($"finance summary wrong: {summary}");
        }

        if (summary.NetMinor != summary.IncomeMinor - summary.ExpenseMinor)
        {
            throw new InvalidOperationException("net calculation is inconsistent");
        }

        var breakdown = finance.ExpenseBreakdown(today, today);
        if (!breakdown.Any(b => b.Category == "Clinic Rent"))
        {
            throw new InvalidOperationException("expense breakdown missing category");
        }
    }

    private static void VerifyStaff()
    {
        var staff = AppServices.Get<StaffRepository>();
        var finance = AppServices.Get<FinanceRepository>();
        var today = DateOnly.FromDateTime(DateTime.Today);

        var member = staff.Insert(new StaffMember
        {
            Code = staff.NextStaffCode(today),
            Name = "QA Assistant",
            Role = "Dental Assistant",
            JoiningDate = today,
            SalaryMinor = 2000_00,
            SalaryFrequency = SalaryFrequency.Monthly,
        }, Session);

        var reloaded = staff.GetById(member.Id) ?? throw new InvalidOperationException("staff not persisted");
        if (reloaded.Role != "Dental Assistant")
        {
            throw new InvalidOperationException("staff update mismatch");
        }

        finance.RecordSalary(new SalaryPayment
        {
            StaffId = member.Id,
            PeriodMonth = new DateOnly(today.Year, today.Month, 1),
            PaidDate = today,
            AmountMinor = 2000_00,
            Method = PaymentMethodKind.Cash,
            MethodLabel = "Cash",
        }, Session);

        var paid = finance.SalaryPayments(today, today, member.Id);
        if (paid.Count != 1 || paid[0].AmountMinor != 2000_00)
        {
            throw new InvalidOperationException("salary payment not recorded");
        }
    }

    private static void VerifySearch()
    {
        var search = AppServices.Get<SearchRepository>();
        var hits = search.Search("Selftest");
        if (hits.Hits.Count == 0)
        {
            throw new InvalidOperationException("FTS search found nothing for seeded patient");
        }

        var invoiceHits = search.Search("INV-");
        if (invoiceHits.Hits.Count == 0)
        {
            throw new InvalidOperationException("FTS search found no invoices");
        }
    }

    private static void VerifyBackup()
    {
        var backup = AppServices.Get<BackupService>();
        var settings = AppServices.Get<SettingsStore>().Settings;
        var today = DateOnly.FromDateTime(DateTime.Today);

        var result = backup.Create(Path.Combine(App.DataDirectory, "backups"), settings.Backup.IncludeAttachments, App.Version);
        if (!File.Exists(result.Path) || result.SizeBytes <= 0)
        {
            throw new InvalidOperationException("backup file missing or empty");
        }

        var manifest = backup.Inspect(result.Path);
        if (manifest.SchemaVersion != Database.CurrentVersion)
        {
            throw new InvalidOperationException("manifest schema mismatch");
        }

        // Restore into a scratch directory and verify integrity there.
        var scratch = Path.Combine(App.DataDirectory, "restore-scratch");
        Directory.CreateDirectory(scratch);
        backup.Restore(result.Path, App.Version, dataDirectory: scratch, attachmentsDirectory: Path.Combine(scratch, "attachments"));

        var restored = Database.IntegrityCheck(new SqliteConnectionFactory(Path.Combine(scratch, "dentiva.db")));
        if (!string.Equals(restored, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"restored database failed integrity: {restored}");
        }
    }

    private static void VerifyExports()
    {
        var dir = Path.Combine(App.DataDirectory, "exports");
        Directory.CreateDirectory(dir);

        var headers = new List<string> { "Code", "Name", "Amount" };
        var rows = new List<IReadOnlyList<object?>> { new List<object?> { "P-0001", "Selftest Patient One", 1710.00m } };

        var csv = Path.Combine(dir, "patients.csv");
        ExportWriter.WriteCsvFile(csv, headers, rows);
        if (new FileInfo(csv).Length < 20)
        {
            throw new InvalidOperationException("csv export too small");
        }

        var xlsx = Path.Combine(dir, "patients.xlsx");
        ExportWriter.WriteXlsxFile(xlsx, "Patients", headers, rows);
        using (var archive = System.IO.Compression.ZipFile.OpenRead(xlsx))
        {
            if (!archive.Entries.Any(e => e.FullName == "xl/worksheets/sheet1.xml"))
            {
                throw new InvalidOperationException("xlsx is missing worksheet part");
            }
        }

        var json = Path.Combine(dir, "patients.json");
        ExportWriter.WriteJsonFile(json, rows);
        if (new FileInfo(json).Length < 20)
        {
            throw new InvalidOperationException("json export too small");
        }
    }

    private static void VerifyNotifications()
    {
        var notifications = AppServices.Get<NotificationRepository>();
        var id = notifications.Insert(new NotificationItem
        {
            Kind = NotificationKind.FollowUpDue,
            Priority = NotificationPriority.Important,
            Title = "Follow-up due",
            Message = "Selftest follow-up",
            Entity = "patient",
            EntityId = "1",
        });

        if (notifications.UnreadCount() < 1)
        {
            throw new InvalidOperationException("unread count did not increase");
        }

        notifications.MarkRead(id);
        notifications.Dismiss(id);
    }

    private static void VerifyAudit()
    {
        var audit = AppServices.Get<AuditRepository>();
        audit.RecordStandalone(Session, "patient.created", "patient", "1");
        if (audit.Count() < 1)
        {
            throw new InvalidOperationException("audit trail is empty");
        }
    }
}
