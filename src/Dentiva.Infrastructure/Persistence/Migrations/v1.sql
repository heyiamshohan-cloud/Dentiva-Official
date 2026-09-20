-- Dentiva initial schema (v1)
-- SQLite, WAL mode, enforced foreign keys, FTS5 global search.
-- All monetary values are integer minor units; dates are ISO-8601 TEXT.

CREATE TABLE IF NOT EXISTS schema_version (
  version INTEGER PRIMARY KEY,
  name TEXT NOT NULL,
  applied_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS app_settings (
  key TEXT PRIMARY KEY,
  value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS counters (
  name TEXT NOT NULL,
  scope TEXT NOT NULL DEFAULT '',
  value INTEGER NOT NULL DEFAULT 0,
  updated_at TEXT NOT NULL,
  PRIMARY KEY (name, scope)
);

CREATE TABLE IF NOT EXISTS roles (
  name TEXT PRIMARY KEY,
  permissions INTEGER NOT NULL,
  is_system INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS users (
  id INTEGER PRIMARY KEY,
  username TEXT NOT NULL COLLATE NOCASE UNIQUE,
  password_hash TEXT NOT NULL,
  display_name TEXT NOT NULL,
  role_name TEXT NOT NULL REFERENCES roles(name),
  is_active INTEGER NOT NULL DEFAULT 1,
  created_at TEXT NOT NULL,
  last_login_at TEXT
);

CREATE TABLE IF NOT EXISTS audit_log (
  id INTEGER PRIMARY KEY,
  ts TEXT NOT NULL,
  user_id INTEGER,
  username TEXT NOT NULL,
  action TEXT NOT NULL,
  entity TEXT NOT NULL DEFAULT '',
  entity_id TEXT NOT NULL DEFAULT '',
  details TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS ix_audit_ts ON audit_log(ts DESC);
CREATE INDEX IF NOT EXISTS ix_audit_action ON audit_log(action);

CREATE TABLE IF NOT EXISTS patients (
  id INTEGER PRIMARY KEY,
  code TEXT NOT NULL COLLATE NOCASE UNIQUE,
  full_name TEXT NOT NULL,
  preferred_name TEXT NOT NULL DEFAULT '',
  dob TEXT,
  gender INTEGER NOT NULL DEFAULT 0,
  phone TEXT NOT NULL DEFAULT '',
  alt_phone TEXT NOT NULL DEFAULT '',
  email TEXT NOT NULL DEFAULT '',
  address TEXT NOT NULL DEFAULT '',
  city TEXT NOT NULL DEFAULT '',
  occupation TEXT NOT NULL DEFAULT '',
  emergency_name TEXT NOT NULL DEFAULT '',
  emergency_relation TEXT NOT NULL DEFAULT '',
  emergency_phone TEXT NOT NULL DEFAULT '',
  chief_complaint TEXT NOT NULL DEFAULT '',
  medical_history TEXT NOT NULL DEFAULT '',
  dental_history TEXT NOT NULL DEFAULT '',
  allergies TEXT NOT NULL DEFAULT '',
  current_medications TEXT NOT NULL DEFAULT '',
  risk_factors TEXT NOT NULL DEFAULT '',
  notes TEXT NOT NULL DEFAULT '',
  status INTEGER NOT NULL DEFAULT 0,
  registration_date TEXT NOT NULL,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  deleted_at TEXT
);
CREATE INDEX IF NOT EXISTS ix_patients_name ON patients(full_name COLLATE NOCASE);
CREATE INDEX IF NOT EXISTS ix_patients_phone ON patients(phone);
CREATE INDEX IF NOT EXISTS ix_patients_reg ON patients(registration_date DESC);

CREATE TABLE IF NOT EXISTS patient_tags (
  id INTEGER PRIMARY KEY,
  name TEXT NOT NULL COLLATE NOCASE UNIQUE,
  color TEXT NOT NULL DEFAULT '#0E7490',
  is_system INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS patient_tag_map (
  patient_id INTEGER NOT NULL REFERENCES patients(id) ON DELETE CASCADE,
  tag_id INTEGER NOT NULL REFERENCES patient_tags(id) ON DELETE CASCADE,
  PRIMARY KEY (patient_id, tag_id)
);

CREATE TABLE IF NOT EXISTS staff (
  id INTEGER PRIMARY KEY,
  code TEXT NOT NULL COLLATE NOCASE UNIQUE,
  name TEXT NOT NULL,
  role TEXT NOT NULL DEFAULT '',
  phone TEXT NOT NULL DEFAULT '',
  email TEXT NOT NULL DEFAULT '',
  address TEXT NOT NULL DEFAULT '',
  joining_date TEXT NOT NULL,
  salary_minor INTEGER NOT NULL DEFAULT 0,
  salary_frequency INTEGER NOT NULL DEFAULT 0,
  responsibilities TEXT NOT NULL DEFAULT '',
  status INTEGER NOT NULL DEFAULT 0,
  emergency_name TEXT NOT NULL DEFAULT '',
  emergency_phone TEXT NOT NULL DEFAULT '',
  notes TEXT NOT NULL DEFAULT '',
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS salary_payments (
  id INTEGER PRIMARY KEY,
  staff_id INTEGER NOT NULL REFERENCES staff(id),
  period TEXT NOT NULL,
  paid_date TEXT NOT NULL,
  amount_minor INTEGER NOT NULL,
  method INTEGER NOT NULL DEFAULT 0,
  method_label TEXT NOT NULL DEFAULT '',
  notes TEXT NOT NULL DEFAULT '',
  created_by TEXT NOT NULL DEFAULT '',
  created_at TEXT NOT NULL,
  UNIQUE (staff_id, period)
);
CREATE INDEX IF NOT EXISTS ix_salary_period ON salary_payments(period);

CREATE TABLE IF NOT EXISTS services (
  id INTEGER PRIMARY KEY,
  name TEXT NOT NULL,
  category TEXT NOT NULL DEFAULT 'General',
  default_price_minor INTEGER NOT NULL DEFAULT 0,
  tax_rate TEXT NOT NULL DEFAULT '0',
  is_active INTEGER NOT NULL DEFAULT 1,
  notes TEXT NOT NULL DEFAULT '',
  UNIQUE (name, category)
);
CREATE INDEX IF NOT EXISTS ix_services_active ON services(is_active, name);

CREATE TABLE IF NOT EXISTS visits (
  id INTEGER PRIMARY KEY,
  patient_id INTEGER NOT NULL REFERENCES patients(id),
  visit_date TEXT NOT NULL,
  reason TEXT NOT NULL DEFAULT '',
  chief_complaint TEXT NOT NULL DEFAULT '',
  diagnosis TEXT NOT NULL DEFAULT '',
  examination TEXT NOT NULL DEFAULT '',
  treatment_notes TEXT NOT NULL DEFAULT '',
  doctor_name TEXT NOT NULL DEFAULT '',
  follow_up_date TEXT,
  status INTEGER NOT NULL DEFAULT 0,
  appointment_id INTEGER,
  created_by TEXT NOT NULL DEFAULT '',
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  deleted_at TEXT
);
CREATE INDEX IF NOT EXISTS ix_visits_patient ON visits(patient_id, visit_date DESC);
CREATE INDEX IF NOT EXISTS ix_visits_date ON visits(visit_date);
CREATE INDEX IF NOT EXISTS ix_visits_followup ON visits(follow_up_date) WHERE deleted_at IS NULL;

CREATE TABLE IF NOT EXISTS visit_procedures (
  id INTEGER PRIMARY KEY,
  visit_id INTEGER NOT NULL REFERENCES visits(id) ON DELETE CASCADE,
  service_id INTEGER,
  name TEXT NOT NULL,
  tooth TEXT NOT NULL DEFAULT '',
  cost_minor INTEGER NOT NULL DEFAULT 0,
  notes TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS ix_visit_procedures_visit ON visit_procedures(visit_id);

CREATE TABLE IF NOT EXISTS tooth_records (
  id INTEGER PRIMARY KEY,
  patient_id INTEGER NOT NULL REFERENCES patients(id) ON DELETE CASCADE,
  tooth TEXT NOT NULL,
  dentition INTEGER NOT NULL DEFAULT 0,
  condition INTEGER NOT NULL DEFAULT 0,
  surfaces TEXT NOT NULL DEFAULT '',
  notes TEXT NOT NULL DEFAULT '',
  updated_at TEXT NOT NULL,
  UNIQUE (patient_id, tooth)
);
CREATE INDEX IF NOT EXISTS ix_tooth_records_patient ON tooth_records(patient_id);

CREATE TABLE IF NOT EXISTS treatment_plans (
  id INTEGER PRIMARY KEY,
  patient_id INTEGER NOT NULL REFERENCES patients(id) ON DELETE CASCADE,
  title TEXT NOT NULL,
  status INTEGER NOT NULL DEFAULT 0,
  notes TEXT NOT NULL DEFAULT '',
  created_date TEXT NOT NULL,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_plans_patient ON treatment_plans(patient_id, created_date DESC);

CREATE TABLE IF NOT EXISTS treatment_plan_items (
  id INTEGER PRIMARY KEY,
  plan_id INTEGER NOT NULL REFERENCES treatment_plans(id) ON DELETE CASCADE,
  service_id INTEGER,
  treatment_name TEXT NOT NULL,
  tooth TEXT NOT NULL DEFAULT '',
  estimated_cost_minor INTEGER NOT NULL DEFAULT 0,
  status INTEGER NOT NULL DEFAULT 0,
  priority INTEGER NOT NULL DEFAULT 0,
  planned_date TEXT,
  completed_date TEXT,
  completed_visit_id INTEGER,
  notes TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS ix_plan_items_plan ON treatment_plan_items(plan_id);

CREATE TABLE IF NOT EXISTS medications (
  id INTEGER PRIMARY KEY,
  patient_id INTEGER NOT NULL REFERENCES patients(id) ON DELETE CASCADE,
  visit_id INTEGER REFERENCES visits(id) ON DELETE SET NULL,
  name TEXT NOT NULL,
  strength TEXT NOT NULL DEFAULT '',
  dosage TEXT NOT NULL DEFAULT '',
  frequency TEXT NOT NULL DEFAULT '',
  duration TEXT NOT NULL DEFAULT '',
  route TEXT NOT NULL DEFAULT '',
  instructions TEXT NOT NULL DEFAULT '',
  notes TEXT NOT NULL DEFAULT '',
  prescribed_date TEXT NOT NULL,
  created_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_medications_patient ON medications(patient_id, prescribed_date DESC);

CREATE TABLE IF NOT EXISTS referrals (
  id INTEGER PRIMARY KEY,
  patient_id INTEGER NOT NULL REFERENCES patients(id) ON DELETE CASCADE,
  visit_id INTEGER REFERENCES visits(id) ON DELETE SET NULL,
  referral_date TEXT NOT NULL,
  provider TEXT NOT NULL DEFAULT '',
  specialty TEXT NOT NULL DEFAULT '',
  organization TEXT NOT NULL DEFAULT '',
  reason TEXT NOT NULL DEFAULT '',
  notes TEXT NOT NULL DEFAULT '',
  report_received INTEGER NOT NULL DEFAULT 0,
  report_summary TEXT NOT NULL DEFAULT '',
  follow_up_date TEXT,
  created_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_referrals_patient ON referrals(patient_id, referral_date DESC);

CREATE TABLE IF NOT EXISTS invoices (
  id INTEGER PRIMARY KEY,
  invoice_no TEXT NOT NULL COLLATE NOCASE UNIQUE,
  patient_id INTEGER NOT NULL REFERENCES patients(id),
  visit_id INTEGER REFERENCES visits(id) ON DELETE SET NULL,
  date TEXT NOT NULL,
  due_date TEXT,
  subtotal_minor INTEGER NOT NULL DEFAULT 0,
  discount_kind INTEGER NOT NULL DEFAULT 0,
  discount_value TEXT NOT NULL DEFAULT '0',
  discount_minor INTEGER NOT NULL DEFAULT 0,
  tax_percent TEXT NOT NULL DEFAULT '0',
  tax_minor INTEGER NOT NULL DEFAULT 0,
  total_minor INTEGER NOT NULL DEFAULT 0,
  paid_minor INTEGER NOT NULL DEFAULT 0,
  due_minor INTEGER NOT NULL DEFAULT 0,
  status INTEGER NOT NULL DEFAULT 0,
  notes TEXT NOT NULL DEFAULT '',
  created_by TEXT NOT NULL DEFAULT '',
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  voided_at TEXT,
  void_reason TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS ix_invoices_patient ON invoices(patient_id, date DESC);
CREATE INDEX IF NOT EXISTS ix_invoices_date ON invoices(date);
CREATE INDEX IF NOT EXISTS ix_invoices_status ON invoices(status) WHERE voided_at IS NULL;

CREATE TABLE IF NOT EXISTS invoice_items (
  id INTEGER PRIMARY KEY,
  invoice_id INTEGER NOT NULL REFERENCES invoices(id) ON DELETE CASCADE,
  service_id INTEGER,
  description TEXT NOT NULL,
  tooth TEXT NOT NULL DEFAULT '',
  quantity TEXT NOT NULL DEFAULT '1',
  unit_price_minor INTEGER NOT NULL DEFAULT 0,
  discount_minor INTEGER NOT NULL DEFAULT 0,
  amount_minor INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_invoice_items_invoice ON invoice_items(invoice_id);

CREATE TABLE IF NOT EXISTS payments (
  id INTEGER PRIMARY KEY,
  receipt_no TEXT NOT NULL COLLATE NOCASE UNIQUE,
  invoice_id INTEGER NOT NULL REFERENCES invoices(id),
  patient_id INTEGER NOT NULL REFERENCES patients(id),
  paid_at TEXT NOT NULL,
  amount_minor INTEGER NOT NULL,
  method INTEGER NOT NULL DEFAULT 0,
  method_label TEXT NOT NULL DEFAULT '',
  reference TEXT NOT NULL DEFAULT '',
  notes TEXT NOT NULL DEFAULT '',
  created_by TEXT NOT NULL DEFAULT '',
  created_at TEXT NOT NULL,
  voided_at TEXT,
  void_reason TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS ix_payments_invoice ON payments(invoice_id);
CREATE INDEX IF NOT EXISTS ix_payments_patient ON payments(patient_id, paid_at DESC);
CREATE INDEX IF NOT EXISTS ix_payments_date ON payments(paid_at);

CREATE TABLE IF NOT EXISTS expense_categories (
  id INTEGER PRIMARY KEY,
  name TEXT NOT NULL COLLATE NOCASE UNIQUE,
  is_system INTEGER NOT NULL DEFAULT 0,
  notes TEXT NOT NULL DEFAULT ''
);

CREATE TABLE IF NOT EXISTS expenses (
  id INTEGER PRIMARY KEY,
  date TEXT NOT NULL,
  category_id INTEGER NOT NULL REFERENCES expense_categories(id),
  description TEXT NOT NULL DEFAULT '',
  amount_minor INTEGER NOT NULL,
  method INTEGER NOT NULL DEFAULT 0,
  method_label TEXT NOT NULL DEFAULT '',
  vendor TEXT NOT NULL DEFAULT '',
  staff_id INTEGER REFERENCES staff(id) ON DELETE SET NULL,
  reference TEXT NOT NULL DEFAULT '',
  notes TEXT NOT NULL DEFAULT '',
  created_by TEXT NOT NULL DEFAULT '',
  created_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_expenses_date ON expenses(date);
CREATE INDEX IF NOT EXISTS ix_expenses_category ON expenses(category_id, date);

CREATE TABLE IF NOT EXISTS other_income (
  id INTEGER PRIMARY KEY,
  date TEXT NOT NULL,
  source TEXT NOT NULL DEFAULT '',
  description TEXT NOT NULL DEFAULT '',
  amount_minor INTEGER NOT NULL,
  method INTEGER NOT NULL DEFAULT 0,
  method_label TEXT NOT NULL DEFAULT '',
  notes TEXT NOT NULL DEFAULT '',
  created_by TEXT NOT NULL DEFAULT '',
  created_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_other_income_date ON other_income(date);

CREATE TABLE IF NOT EXISTS attachments (
  id INTEGER PRIMARY KEY,
  patient_id INTEGER REFERENCES patients(id) ON DELETE CASCADE,
  visit_id INTEGER REFERENCES visits(id) ON DELETE SET NULL,
  referral_id INTEGER REFERENCES referrals(id) ON DELETE SET NULL,
  staff_id INTEGER REFERENCES staff(id) ON DELETE SET NULL,
  invoice_id INTEGER REFERENCES invoices(id) ON DELETE SET NULL,
  category INTEGER NOT NULL DEFAULT 8,
  file_name TEXT NOT NULL,
  stored_name TEXT NOT NULL UNIQUE,
  content_type TEXT NOT NULL DEFAULT '',
  ext TEXT NOT NULL DEFAULT '',
  size_bytes INTEGER NOT NULL DEFAULT 0,
  pixel_w INTEGER,
  pixel_h INTEGER,
  description TEXT NOT NULL DEFAULT '',
  added_by TEXT NOT NULL DEFAULT '',
  created_at TEXT NOT NULL,
  deleted_at TEXT,
  sha256 TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS ix_attachments_patient ON attachments(patient_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_attachments_visit ON attachments(visit_id);

CREATE TABLE IF NOT EXISTS appointments (
  id INTEGER PRIMARY KEY,
  patient_id INTEGER NOT NULL REFERENCES patients(id),
  date TEXT NOT NULL,
  start_time TEXT NOT NULL,
  duration INTEGER NOT NULL DEFAULT 30,
  serial INTEGER NOT NULL,
  type TEXT NOT NULL DEFAULT '',
  doctor_name TEXT NOT NULL DEFAULT '',
  status INTEGER NOT NULL DEFAULT 0,
  priority INTEGER NOT NULL DEFAULT 0,
  is_walk_in INTEGER NOT NULL DEFAULT 0,
  notes TEXT NOT NULL DEFAULT '',
  rescheduled_from INTEGER,
  visit_id INTEGER,
  created_by TEXT NOT NULL DEFAULT '',
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  UNIQUE (date, doctor_name, serial)
);
CREATE INDEX IF NOT EXISTS ix_appointments_date ON appointments(date, status);
CREATE INDEX IF NOT EXISTS ix_appointments_patient ON appointments(patient_id, date DESC);

CREATE TABLE IF NOT EXISTS notifications (
  id INTEGER PRIMARY KEY,
  kind INTEGER NOT NULL,
  priority INTEGER NOT NULL DEFAULT 0,
  title TEXT NOT NULL,
  message TEXT NOT NULL DEFAULT '',
  entity TEXT NOT NULL DEFAULT '',
  entity_id TEXT NOT NULL DEFAULT '',
  created_at TEXT NOT NULL,
  read_at TEXT,
  dismissed_at TEXT,
  snoozed_until TEXT
);
CREATE INDEX IF NOT EXISTS ix_notifications_created ON notifications(created_at DESC);

-- ===================== Global search (FTS5) =====================

CREATE VIRTUAL TABLE IF NOT EXISTS search_fts USING fts5(
  title,
  body,
  kind UNINDEXED,
  ref_id UNINDEXED,
  patient_id UNINDEXED,
  tokenize='unicode61'
);

-- Patients
CREATE TRIGGER IF NOT EXISTS trg_patients_fts_ai AFTER INSERT ON patients BEGIN
  INSERT INTO search_fts(kind, ref_id, patient_id, title, body)
  VALUES ('patient', new.id, new.id,
    new.code || ' ' || new.full_name || ' ' || new.preferred_name || ' ' || new.phone || ' ' || new.alt_phone || ' ' || new.email,
    new.chief_complaint || ' ' || new.notes || ' ' || new.city || ' ' || new.occupation);
END;
CREATE TRIGGER IF NOT EXISTS trg_patients_fts_au AFTER UPDATE ON patients BEGIN
  DELETE FROM search_fts WHERE kind='patient' AND ref_id=new.id;
  INSERT INTO search_fts(kind, ref_id, patient_id, title, body)
  VALUES ('patient', new.id, new.id,
    new.code || ' ' || new.full_name || ' ' || new.preferred_name || ' ' || new.phone || ' ' || new.alt_phone || ' ' || new.email,
    new.chief_complaint || ' ' || new.notes || ' ' || new.city || ' ' || new.occupation);
END;
CREATE TRIGGER IF NOT EXISTS trg_patients_fts_ad AFTER DELETE ON patients BEGIN
  DELETE FROM search_fts WHERE kind='patient' AND ref_id=old.id;
END;

-- Visits
CREATE TRIGGER IF NOT EXISTS trg_visits_fts_ai AFTER INSERT ON visits BEGIN
  INSERT INTO search_fts(kind, ref_id, patient_id, title, body)
  VALUES ('visit', new.id, new.patient_id,
    new.reason || ' ' || new.diagnosis,
    new.chief_complaint || ' ' || new.examination || ' ' || new.treatment_notes || ' ' || new.doctor_name);
END;
CREATE TRIGGER IF NOT EXISTS trg_visits_fts_au AFTER UPDATE ON visits BEGIN
  DELETE FROM search_fts WHERE kind='visit' AND ref_id=new.id;
  INSERT INTO search_fts(kind, ref_id, patient_id, title, body)
  VALUES ('visit', new.id, new.patient_id,
    new.reason || ' ' || new.diagnosis,
    new.chief_complaint || ' ' || new.examination || ' ' || new.treatment_notes || ' ' || new.doctor_name);
END;
CREATE TRIGGER IF NOT EXISTS trg_visits_fts_ad AFTER DELETE ON visits BEGIN
  DELETE FROM search_fts WHERE kind='visit' AND ref_id=old.id;
END;

-- Appointments
CREATE TRIGGER IF NOT EXISTS trg_appointments_fts_ai AFTER INSERT ON appointments BEGIN
  INSERT INTO search_fts(kind, ref_id, patient_id, title, body)
  VALUES ('appointment', new.id, new.patient_id,
    new.type || ' ' || new.doctor_name, new.notes);
END;
CREATE TRIGGER IF NOT EXISTS trg_appointments_fts_au AFTER UPDATE ON appointments BEGIN
  DELETE FROM search_fts WHERE kind='appointment' AND ref_id=new.id;
  INSERT INTO search_fts(kind, ref_id, patient_id, title, body)
  VALUES ('appointment', new.id, new.patient_id,
    new.type || ' ' || new.doctor_name, new.notes);
END;
CREATE TRIGGER IF NOT EXISTS trg_appointments_fts_ad AFTER DELETE ON appointments BEGIN
  DELETE FROM search_fts WHERE kind='appointment' AND ref_id=old.id;
END;

-- Invoices (rebuilt from items)
CREATE TRIGGER IF NOT EXISTS trg_invoices_fts_ai AFTER INSERT ON invoices BEGIN
  INSERT INTO search_fts(kind, ref_id, patient_id, title, body)
  VALUES ('invoice', new.id, new.patient_id, new.invoice_no, new.notes);
END;
CREATE TRIGGER IF NOT EXISTS trg_invoices_fts_au AFTER UPDATE ON invoices BEGIN
  DELETE FROM search_fts WHERE kind='invoice' AND ref_id=new.id;
  INSERT INTO search_fts(kind, ref_id, patient_id, title, body)
  VALUES ('invoice', new.id, new.patient_id, new.invoice_no, new.notes);
END;
CREATE TRIGGER IF NOT EXISTS trg_invoices_fts_ad AFTER DELETE ON invoices BEGIN
  DELETE FROM search_fts WHERE kind='invoice' AND ref_id=old.id;
END;
CREATE TRIGGER IF NOT EXISTS trg_invoice_items_fts_ai AFTER INSERT ON invoice_items BEGIN
  DELETE FROM search_fts WHERE kind='invoice' AND ref_id=new.invoice_id;
  INSERT INTO search_fts(kind, ref_id, patient_id, title, body)
  SELECT 'invoice', i.id, i.patient_id, i.invoice_no,
    (SELECT group_concat(description || ' ' || tooth, ' | ') FROM invoice_items WHERE invoice_id = i.id)
  FROM invoices i WHERE i.id = new.invoice_id;
END;
CREATE TRIGGER IF NOT EXISTS trg_invoice_items_fts_ad AFTER DELETE ON invoice_items BEGIN
  DELETE FROM search_fts WHERE kind='invoice' AND ref_id=old.invoice_id;
  INSERT INTO search_fts(kind, ref_id, patient_id, title, body)
  SELECT 'invoice', i.id, i.patient_id, i.invoice_no,
    (SELECT group_concat(description || ' ' || tooth, ' | ') FROM invoice_items WHERE invoice_id = i.id)
  FROM invoices i WHERE i.id = old.invoice_id;
END;

-- Payments
CREATE TRIGGER IF NOT EXISTS trg_payments_fts_ai AFTER INSERT ON payments BEGIN
  INSERT INTO search_fts(kind, ref_id, patient_id, title, body)
  VALUES ('payment', new.id, new.patient_id, new.receipt_no, new.reference || ' ' || new.method_label || ' ' || new.notes);
END;
CREATE TRIGGER IF NOT EXISTS trg_payments_fts_au AFTER UPDATE ON payments BEGIN
  DELETE FROM search_fts WHERE kind='payment' AND ref_id=new.id;
  INSERT INTO search_fts(kind, ref_id, patient_id, title, body)
  VALUES ('payment', new.id, new.patient_id, new.receipt_no, new.reference || ' ' || new.method_label || ' ' || new.notes);
END;
CREATE TRIGGER IF NOT EXISTS trg_payments_fts_ad AFTER DELETE ON payments BEGIN
  DELETE FROM search_fts WHERE kind='payment' AND ref_id=old.id;
END;

-- Medications
CREATE TRIGGER IF NOT EXISTS trg_medications_fts_ai AFTER INSERT ON medications BEGIN
  INSERT INTO search_fts(kind, ref_id, patient_id, title, body)
  VALUES ('medication', new.id, new.patient_id, new.name,
    new.strength || ' ' || new.dosage || ' ' || new.frequency || ' ' || new.instructions);
END;
CREATE TRIGGER IF NOT EXISTS trg_medications_fts_ad AFTER DELETE ON medications BEGIN
  DELETE FROM search_fts WHERE kind='medication' AND ref_id=old.id;
END;

-- Referrals
CREATE TRIGGER IF NOT EXISTS trg_referrals_fts_ai AFTER INSERT ON referrals BEGIN
  INSERT INTO search_fts(kind, ref_id, patient_id, title, body)
  VALUES ('referral', new.id, new.patient_id,
    new.provider || ' ' || new.specialty || ' ' || new.organization,
    new.reason || ' ' || new.notes || ' ' || new.report_summary);
END;
CREATE TRIGGER IF NOT EXISTS trg_referrals_fts_au AFTER UPDATE ON referrals BEGIN
  DELETE FROM search_fts WHERE kind='referral' AND ref_id=new.id;
  INSERT INTO search_fts(kind, ref_id, patient_id, title, body)
  VALUES ('referral', new.id, new.patient_id,
    new.provider || ' ' || new.specialty || ' ' || new.organization,
    new.reason || ' ' || new.notes || ' ' || new.report_summary);
END;
CREATE TRIGGER IF NOT EXISTS trg_referrals_fts_ad AFTER DELETE ON referrals BEGIN
  DELETE FROM search_fts WHERE kind='referral' AND ref_id=old.id;
END;

-- Attachments (metadata only)
CREATE TRIGGER IF NOT EXISTS trg_attachments_fts_ai AFTER INSERT ON attachments BEGIN
  INSERT INTO search_fts(kind, ref_id, patient_id, title, body)
  VALUES ('attachment', new.id, COALESCE(new.patient_id, 0), new.file_name, new.description);
END;
CREATE TRIGGER IF NOT EXISTS trg_attachments_fts_ad AFTER DELETE ON attachments BEGIN
  DELETE FROM search_fts WHERE kind='attachment' AND ref_id=old.id;
END;

-- Staff
CREATE TRIGGER IF NOT EXISTS trg_staff_fts_ai AFTER INSERT ON staff BEGIN
  INSERT INTO search_fts(kind, ref_id, patient_id, title, body)
  VALUES ('staff', new.id, 0, new.code || ' ' || new.name, new.role || ' ' || new.phone || ' ' || new.email);
END;
CREATE TRIGGER IF NOT EXISTS trg_staff_fts_au AFTER UPDATE ON staff BEGIN
  DELETE FROM search_fts WHERE kind='staff' AND ref_id=new.id;
  INSERT INTO search_fts(kind, ref_id, patient_id, title, body)
  VALUES ('staff', new.id, 0, new.code || ' ' || new.name, new.role || ' ' || new.phone || ' ' || new.email);
END;
CREATE TRIGGER IF NOT EXISTS trg_staff_fts_ad AFTER DELETE ON staff BEGIN
  DELETE FROM search_fts WHERE kind='staff' AND ref_id=old.id;
END;

-- ===================== Seed data (system lookups only) =====================
-- Role permissions are seeded from code (PermissionPresets) so the mask
-- cannot drift from the application. Expense categories, patient tags and a
-- starter service catalog below are system reference data, fully editable.

INSERT OR IGNORE INTO expense_categories(name, is_system) VALUES
  ('Clinic Rent', 1),
  ('Electricity', 1),
  ('Internet', 1),
  ('Staff Salary', 1),
  ('Equipment', 1),
  ('Supplies', 1),
  ('Maintenance', 1),
  ('Laboratory', 1),
  ('Transportation', 1),
  ('Marketing', 1),
  ('Software & Services', 1),
  ('Other', 1);

INSERT OR IGNORE INTO patient_tags(name, color, is_system) VALUES
  ('Follow-up Required', '#0E7490', 1),
  ('High Priority', '#B23A2E', 1),
  ('Treatment Pending', '#A9660B', 1),
  ('Payment Due', '#2B6394', 1),
  ('Special Attention', '#177E52', 1);

INSERT OR IGNORE INTO services(name, category, default_price_minor, tax_rate, is_active) VALUES
  ('Consultation', 'Consultation', 50000, '0', 1),
  ('Scaling & Polishing', 'Preventive', 200000, '0', 1),
  ('Composite Filling', 'Restorative', 250000, '0', 1),
  ('Extraction (Simple)', 'Oral Surgery', 150000, '0', 1),
  ('Root Canal Treatment', 'Endodontics', 800000, '0', 1),
  ('Porcelain Crown', 'Prosthodontics', 1200000, '0', 1),
  ('Full Denture (Single Jaw)', 'Prosthodontics', 3000000, '0', 1),
  ('Dental Implant', 'Implantology', 5000000, '0', 1),
  ('X-ray (Periapical)', 'Diagnostics', 15000, '0', 1),
  ('Teeth Whitening', 'Cosmetic', 1000000, '0', 1);
