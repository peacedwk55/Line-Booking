# CLAUDE.md — Diamond Massage Booking System

> ไฟล์นี้ให้บริบทแก่ AI assistant (Claude) เพื่อให้เข้าใจโปรเจคนี้อย่างรวดเร็ว

---

## 🎯 ภาพรวมโปรเจค

ระบบจองนัดหมายและสมาชิก สำหรับธุรกิจ **Diamond Massage** ผ่าน LINE
ออกแบบเป็น **SaaS MVP** รองรับหลายธุรกิจ (multi-tenant) ด้วย TenantId

**ลูกค้าตัวอย่าง:** Diamond Massage (`slug = "diamond-massage"`)

---

## 🧱 Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | .NET 8 Web API (C#) |
| Frontend | Next.js 14 (App Router, TypeScript) |
| Database | PostgreSQL |
| LINE | Messaging API + LIFF 2.x |
| Background Jobs | Quartz.NET |
| Hosting | Railway (backend + DB), Vercel (frontend) |
| Backend Testing | xUnit + Moq + FluentAssertions + EF InMemory |
| Frontend Testing | Jest + Testing Library + jsdom |

---

## 📁 โครงสร้างไฟล์

```
diamond-massage/
├── DiamondBooking.sln              ← Solution file (backend + tests)
│
├── backend/
│   ├── Controllers/
│   │   ├── BookingController.cs    ← POST/GET/DELETE /api/{slug}/bookings
│   │   ├── ServiceController.cs    ← GET /api/{slug}/services
│   │   ├── WebhookController.cs    ← POST /webhook/{slug}
│   │   └── AdminController.cs      ← /api/{slug}/admin/*
│   ├── Data/
│   │   └── AppDbContext.cs         ← EF Core DbContext
│   ├── Jobs/
│   │   └── ReminderJob.cs          ← Quartz: รัน 18:00 Bangkok ทุกวัน
│   ├── Models/
│   │   └── Entities.cs             ← Tenant, User, Service, TimeSlot, Booking, NotificationLog
│   ├── Services/
│   │   ├── LineService.cs          ← LINE API: verify, send text/flex, reminder
│   │   └── BookingService.cs       ← สร้างจอง, double-booking check, cancel
│   ├── Program.cs
│   ├── appsettings.json
│   ├── DiamondBooking.Api.csproj
│   └── .env.example
│
├── tests/                          ← .NET test project
│   ├── DiamondBooking.Tests.csproj
│   ├── Helpers/
│   │   └── TestDbFactory.cs        ← In-memory DB + seed helpers
│   ├── BookingServiceTests.cs      ← 14 unit tests (core business logic)
│   ├── LineServiceTests.cs         ← 7 unit tests (signature + webhook)
│   ├── ReminderJobTests.cs         ← 8 unit tests (Quartz job)
│   ├── ApiIntegrationTests.cs      ← 9 HTTP integration tests
│   └── AdminControllerTests.cs     ← 9 admin endpoint tests
│
├── frontend/
│   ├── app/
│   │   ├── page.tsx                ← LIFF booking flow (5 steps)
│   │   ├── my-bookings/page.tsx    ← ประวัติการจอง + ยกเลิก
│   │   └── layout.tsx
│   ├── hooks/
│   │   └── useLiff.ts              ← LINE login hook
│   ├── lib/
│   │   └── api.ts                  ← Axios API client + TypeScript types
│   ├── admin/
│   │   └── index.html              ← Admin dashboard (vanilla HTML)
│   ├── __tests__/
│   │   ├── api.test.ts             ← 8 unit tests (API client)
│   │   ├── useLiff.test.ts         ← 5 unit tests (LIFF hook)
│   │   ├── BookingPage.test.tsx    ← 9 component tests (booking flow)
│   │   └── MyBookingsPage.test.tsx ← 8 component tests (booking history)
│   ├── jest.config.ts
│   ├── jest.setup.ts
│   ├── package.json
│   └── .env.example
│
├── database/
│   └── schema.sql                  ← DDL + seed data
│
├── docs/
│   └── DEPLOYMENT.md               ← คู่มือ deploy Railway + Vercel
│
└── scripts/
    ├── dev.sh                      ← รัน local dev
    └── test.sh                     ← รันเทสทั้งหมด (+ flags)
```

---

## 🧪 Testing

### ภาพรวม test suite

| ชั้น | Framework | ไฟล์ | จำนวน |
|------|-----------|------|-------|
| Backend unit | xUnit + Moq + EF InMemory | `BookingServiceTests.cs` | 14 |
| Backend unit | xUnit + Moq | `LineServiceTests.cs` | 7 |
| Backend unit | xUnit + Moq | `ReminderJobTests.cs` | 8 |
| Backend integration | WebApplicationFactory | `ApiIntegrationTests.cs` | 9 |
| Backend integration | WebApplicationFactory | `AdminControllerTests.cs` | 9 |
| Frontend unit | Jest | `api.test.ts` | 8 |
| Frontend unit | Jest | `useLiff.test.ts` | 5 |
| Frontend component | Testing Library | `BookingPage.test.tsx` | 9 |
| Frontend component | Testing Library | `MyBookingsPage.test.tsx` | 8 |
| **รวม** | | | **77 tests** |

### วิธีรันเทส

```bash
# รันทั้งหมด
bash scripts/test.sh

# เฉพาะ backend
bash scripts/test.sh --backend-only

# เฉพาะ frontend
bash scripts/test.sh --frontend-only

# พร้อม coverage
bash scripts/test.sh --coverage

# Frontend watch mode (ระหว่าง dev)
bash scripts/test.sh --frontend-only --watch

# รันตรงๆ ไม่ผ่าน script
dotnet test tests/DiamondBooking.Tests.csproj
cd frontend && npm test -- --ci
```

### สิ่งสำคัญ: ไม่ต้องการ DB หรือ LINE credentials จริง

**Backend** ใช้ EF Core InMemory — แต่ละ test สร้าง database ชื่อ unique ด้วย `Guid.NewGuid()` เพื่อ isolation สมบูรณ์

**Frontend** mock `@line/liff` และ `axios` ผ่าน `jest.mock()` — ไม่ต้องการ LINE credentials

### TestDbFactory pattern (backend)

```csharp
var db      = TestDbFactory.Create("optional-db-name");
var tenant  = TestDbFactory.SeedTenant(db);
var user    = TestDbFactory.SeedUser(db, tenant.Id);
var service = TestDbFactory.SeedService(db, tenant.Id);
var slot    = TestDbFactory.SeedTimeSlot(db, tenant.Id, dayOfWeek: 1, maxBookings: 2);
var booking = TestDbFactory.SeedBooking(db, tenant.Id, user.Id, service.Id,
    date: "2025-06-02", status: BookingStatus.Confirmed);
```

### Mock LineService pattern (backend)

```csharp
var lineMock = new Mock<LineService>(null!, null!, null!);
lineMock.Setup(l => l.SendBookingConfirmationAsync(It.IsAny<Booking>()))
        .Returns(Task.CompletedTask);
var svc = new BookingService(db, lineMock.Object, NullLogger<BookingService>.Instance);
```

### Mock pattern (frontend)

```typescript
jest.mock('@/hooks/useLiff', () => ({ useLiff: jest.fn() }))
jest.mock('@/lib/api', () => ({ getServices: jest.fn(), getSlots: jest.fn(), createBooking: jest.fn() }))

// ใน test:
;(useLiff as jest.Mock).mockReturnValue({ profile: mockProfile, ready: true, error: null })
;(getServices as jest.Mock).mockResolvedValue(mockServices)
```

### Coverage ที่ test ครอบคลุม

- ✅ จองสำเร็จ (new user, existing user, no duplicate user record)
- ✅ Double-booking: slot full, user conflict
- ✅ Cancelled booking ไม่ block slot ใหม่
- ✅ Service ของ tenant อื่นใช้ไม่ได้ (tenant isolation)
- ✅ Cancel by user, cancel by admin
- ✅ Available slots calculation (empty, partial, full)
- ✅ LINE signature: valid, wrong secret, tampered body, empty
- ✅ Webhook: follow → create user, message → upsert user, unknown type → no throw
- ✅ No duplicate user on re-follow
- ✅ Reminder: tomorrow bookings sent, today = skip, already-sent = skip, cancelled = skip
- ✅ Reminder resilience: LINE error per-booking caught, next booking continues
- ✅ Multi-booking: all 3 reminders sent
- ✅ HTTP 200/400/401/404 on all major endpoints
- ✅ LIFF hook: init, login redirect, error state, correct liffId passed
- ✅ Booking UI: 5-step flow, back button, error alert, slot disable
- ✅ MyBookings: upcoming vs history split, cancel dialog, cancel error

---

## 🗄️ Database Schema

```
tenants          — หนึ่งแถว = หนึ่งธุรกิจ (SaaS tenant)
  id, slug, name, line_channel_id, line_channel_secret,
  line_access_token, liff_id, timezone, is_active

users            — ลูกค้าที่ identify ด้วย LINE userId
  id, tenant_id, line_user_id, display_name, phone, picture_url

services         — รายการบริการ เช่น "นวดแผนไทย 60 นาที"
  id, tenant_id, name, description, duration_minutes, price, sort_order

time_slots       — ช่วงเวลาที่เปิดรับจอง (รายวัน หรือวันเฉพาะ)
  id, tenant_id, day_of_week (0-6 / NULL), specific_date, start_time,
  end_time, max_bookings

bookings
  id, tenant_id, user_id, service_id, booking_date, start_time,
  end_time, status (pending/confirmed/cancelled/completed),
  note, admin_note, reminder_sent

notification_logs — log การส่ง LINE message
  id, tenant_id, booking_id, user_id, type, status, sent_at
```

**Multi-tenant rule:** ทุก query ต้องมี `WHERE tenant_id = ?` — ห้ามลืม

---

## 🔌 API Endpoints

### LIFF (Public)
```
GET    /api/{slug}/services
GET    /api/{slug}/bookings/slots?date=YYYY-MM-DD
POST   /api/{slug}/bookings
GET    /api/{slug}/bookings?lineUserId=Uxxxx
DELETE /api/{slug}/bookings/{id}?lineUserId=Uxxxx
```

### Admin
```
GET    /api/{slug}/admin/dashboard
GET    /api/{slug}/admin/bookings?date=&status=
PATCH  /api/{slug}/admin/bookings/{id}     body: { status, adminNote }
DELETE /api/{slug}/admin/bookings/{id}?reason=
GET    /api/{slug}/admin/users
```

### LINE Webhook
```
POST /webhook/{slug}
  Header: X-Line-Signature (HMAC-SHA256)
  Events: follow → welcome msg, message "จองนัด" → LIFF link
```

### Utility
```
GET /health
GET /swagger  (Development only)
```

---

## ⚙️ Environment Variables

### Backend
```env
ConnectionStrings__Default=Host=...;Database=...;Username=...;Password=...
FrontendUrl=https://your-liff-app.vercel.app
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
```

### Frontend (.env.local)
```env
NEXT_PUBLIC_LIFF_ID=1234567890-xxxxxxxx
NEXT_PUBLIC_API_URL=https://your-backend.railway.app
NEXT_PUBLIC_TENANT_SLUG=diamond-massage
```

### ค่าที่เก็บใน DB (ตาราง tenants)
- `line_channel_id`, `line_channel_secret`, `line_access_token`, `liff_id`

---

## 🔑 Business Logic สำคัญ

### Double-booking Prevention (BookingService.cs)
1. นับ bookings ที่ overlap time range (ไม่รวม Cancelled)
2. เทียบกับ `max_bookings` ของ slot
3. ตรวจ user conflict ช่วงเวลาเดียวกัน
4. ถ้าเต็ม → return Thai error message

### Reminder Job (ReminderJob.cs)
- Quartz cron: `0 0 11 * * ?` (11:00 UTC = 18:00 Bangkok)
- Query: `booking_date = tomorrow AND status = Confirmed AND reminder_sent = false`
- ส่ง LINE text → set `reminder_sent = true`
- Error per-booking = catch & log → ไม่หยุด booking อื่น

### Cancellation Rules
- **ผู้ใช้:** ยกเลิกได้ก่อนนัด > 2 ชั่วโมง (BookingController.cs)
- **Admin:** ยกเลิกได้ทุกเวลา พร้อมเหตุผล (AdminController.cs)
- ทั้งคู่: ส่ง LINE notification แจ้งลูกค้า

### Webhook Signature (LineService.cs)
```
HMAC-SHA256(rawBody, channelSecret) → Base64 == X-Line-Signature
```

---

## 🚀 วิธีรัน Local

```bash
# DB
psql -U postgres -c "CREATE DATABASE diamond_booking;"
psql -U postgres -d diamond_booking -f database/schema.sql

# Backend → http://localhost:5000/swagger
cd backend && dotnet run

# Frontend → http://localhost:3000
cd frontend && cp .env.example .env.local && npm install && npm run dev

# Admin dashboard
open frontend/admin/index.html   # แก้ API URL ในไฟล์ก่อน

# เทสทั้งหมด (ไม่ต้องการ DB / LINE จริง)
bash scripts/test.sh
```

---

## 🏗️ SaaS Extensibility

```sql
-- เพิ่มลูกค้า SaaS ใหม่ — ไม่แก้ code
INSERT INTO tenants (slug, name, line_channel_id, line_channel_secret, line_access_token, liff_id)
VALUES ('happy-spa', 'Happy Spa', 'CHANNEL_ID', 'SECRET', 'TOKEN', 'LIFF_ID');
```

ทุก endpoint ใช้ `{tenantSlug}` ใน URL path → resolve tenantId → filter ทุก query

### Roadmap (Post-MVP)
- [ ] Subscription billing (Stripe / Omise)
- [ ] Multiple branches per tenant
- [ ] Staff / therapist assignment
- [ ] LINE Pay integration
- [ ] Analytics dashboard

---

## ⚠️ Known Limitations (MVP)

- Admin API ไม่มี authentication — ต้องเพิ่ม middleware ก่อน production
- Admin dashboard (`admin/index.html`) ควร password protect
- Timezone ใช้ UTC+7 fixed — ควรใช้ `tenant.timezone` แทน
- LINE Access Token ไม่มี auto-refresh — ควรใช้ short-lived stateless token
