-- ================================================================
-- Diamond Massage (Tenant: diamond-massage)
-- LINE Booking + Membership SaaS MVP
-- Database Schema v1.0
-- ================================================================

-- ----------------------------------------------------------------
-- TENANTS  (SaaS-ready: one row per business)
-- ----------------------------------------------------------------
CREATE TABLE tenants (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    slug          VARCHAR(100) UNIQUE NOT NULL,          -- e.g. "diamond-massage"
    name          VARCHAR(255) NOT NULL,
    line_channel_id     VARCHAR(255) NOT NULL,
    line_channel_secret VARCHAR(255) NOT NULL,
    line_access_token   TEXT NOT NULL,
    liff_id       VARCHAR(255),
    timezone      VARCHAR(50) DEFAULT 'Asia/Bangkok',
    is_active     BOOLEAN DEFAULT TRUE,
    created_at    TIMESTAMPTZ DEFAULT NOW()
);

-- ----------------------------------------------------------------
-- USERS  (LINE members per tenant)
-- ----------------------------------------------------------------
CREATE TABLE users (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id     UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    line_user_id  VARCHAR(255) NOT NULL,
    display_name  VARCHAR(255),
    phone         VARCHAR(20),
    picture_url   TEXT,
    is_blocked    BOOLEAN DEFAULT FALSE,
    created_at    TIMESTAMPTZ DEFAULT NOW(),
    updated_at    TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (tenant_id, line_user_id)
);

-- ----------------------------------------------------------------
-- SERVICES  (e.g. Thai Massage 60min, Oil Massage 90min)
-- ----------------------------------------------------------------
CREATE TABLE services (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id     UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    name          VARCHAR(255) NOT NULL,
    description   TEXT,
    duration_minutes INT NOT NULL DEFAULT 60,
    price         DECIMAL(10,2),
    is_active     BOOLEAN DEFAULT TRUE,
    sort_order    INT DEFAULT 0,
    created_at    TIMESTAMPTZ DEFAULT NOW()
);

-- ----------------------------------------------------------------
-- TIME SLOTS  (available slots per day-of-week or specific date)
-- ----------------------------------------------------------------
CREATE TABLE time_slots (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id     UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    -- day_of_week: 0=Sunday ... 6=Saturday, NULL = specific date only
    day_of_week   SMALLINT CHECK (day_of_week BETWEEN 0 AND 6),
    specific_date DATE,
    start_time    TIME NOT NULL,
    end_time      TIME NOT NULL,
    max_bookings  INT DEFAULT 1,
    is_active     BOOLEAN DEFAULT TRUE,
    CONSTRAINT chk_slot_type CHECK (day_of_week IS NOT NULL OR specific_date IS NOT NULL)
);

-- ----------------------------------------------------------------
-- BOOKINGS
-- ----------------------------------------------------------------
CREATE TYPE booking_status AS ENUM ('pending', 'confirmed', 'cancelled', 'completed');

CREATE TABLE bookings (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id     UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    user_id       UUID NOT NULL REFERENCES users(id),
    service_id    UUID REFERENCES services(id),
    booking_date  DATE NOT NULL,
    start_time    TIME NOT NULL,
    end_time      TIME NOT NULL,
    status        booking_status DEFAULT 'confirmed',
    note          TEXT,
    admin_note    TEXT,
    reminder_sent BOOLEAN DEFAULT FALSE,
    created_at    TIMESTAMPTZ DEFAULT NOW(),
    updated_at    TIMESTAMPTZ DEFAULT NOW()
);

-- ----------------------------------------------------------------
-- NOTIFICATIONS LOG
-- ----------------------------------------------------------------
CREATE TABLE notification_logs (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id     UUID NOT NULL REFERENCES tenants(id),
    booking_id    UUID REFERENCES bookings(id),
    user_id       UUID REFERENCES users(id),
    type          VARCHAR(50) NOT NULL,   -- 'booking_confirmed', 'reminder', 'cancelled'
    status        VARCHAR(20) DEFAULT 'sent',
    sent_at       TIMESTAMPTZ DEFAULT NOW()
);

-- ----------------------------------------------------------------
-- INDEXES
-- ----------------------------------------------------------------
CREATE INDEX idx_users_tenant_line     ON users(tenant_id, line_user_id);
CREATE INDEX idx_bookings_tenant_date  ON bookings(tenant_id, booking_date);
CREATE INDEX idx_bookings_user         ON bookings(user_id);
CREATE INDEX idx_bookings_status       ON bookings(tenant_id, status);
CREATE INDEX idx_slots_tenant_dow      ON time_slots(tenant_id, day_of_week);

-- ----------------------------------------------------------------
-- SEED: Diamond Massage tenant + sample data
-- ----------------------------------------------------------------
INSERT INTO tenants (id, slug, name, line_channel_id, line_channel_secret, line_access_token, timezone)
VALUES (
    '00000000-0000-0000-0000-000000000001',
    'diamond-massage',
    'Diamond Massage',
    'YOUR_LINE_CHANNEL_ID',
    'YOUR_LINE_CHANNEL_SECRET',
    'YOUR_LINE_ACCESS_TOKEN',
    'Asia/Bangkok'
);

INSERT INTO services (tenant_id, name, description, duration_minutes, price, sort_order)
VALUES
    ('00000000-0000-0000-0000-000000000001', 'นวดแผนไทย 60 นาที', 'ผ่อนคลายกล้ามเนื้อแบบดั้งเดิม', 60, 350.00, 1),
    ('00000000-0000-0000-0000-000000000001', 'นวดแผนไทย 90 นาที', 'ผ่อนคลายแบบเต็มรูปแบบ', 90, 500.00, 2),
    ('00000000-0000-0000-0000-000000000001', 'นวดน้ำมัน 60 นาที', 'อโรมาเธอราพีช่วยผ่อนคลาย', 60, 450.00, 3),
    ('00000000-0000-0000-0000-000000000001', 'นวดฝ่าเท้า 45 นาที', 'กระตุ้นจุดสะท้อนสุขภาพ', 45, 280.00, 4);

-- Weekly slots (Mon-Sat, 10:00-20:00 every 2 hours)
INSERT INTO time_slots (tenant_id, day_of_week, start_time, end_time, max_bookings)
SELECT 
    '00000000-0000-0000-0000-000000000001',
    d.dow,
    t.start_time::TIME,
    t.end_time::TIME,
    2
FROM 
    (SELECT unnest(ARRAY[1,2,3,4,5,6]) AS dow) d,
    (VALUES 
        ('10:00','11:30'), ('11:30','13:00'), ('13:00','14:30'),
        ('14:30','16:00'), ('16:00','17:30'), ('17:30','19:00'),
        ('19:00','20:30')
    ) AS t(start_time, end_time);
