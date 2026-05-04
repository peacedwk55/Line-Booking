# CLAUDE.md — Diamond Massage Booking System

> บริบทสำหรับ AI assistant — อ่านก่อนเริ่มงานทุกครั้ง

---

## ภาพรวม

ระบบจองนัดหมายผ่าน LINE สำหรับธุรกิจ **Diamond Massage** ออกแบบเป็น SaaS MVP (multi-tenant)

**Tenant ตัวอย่าง:** slug = `diamond-massage`, ID = `00000000-0000-0000-0000-000000000001`

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | .NET 10 Web API (C#) |
| Frontend | Next.js 14 (App Router, TypeScript) |
| Database | PostgreSQL (Railway managed) |
| LINE | Messaging API + LIFF 2.x (LINE Login channel) |
| Background Jobs | Quartz.NET |
| Hosting | Railway (backend + DB), Vercel (frontend) |
| Testing | xUnit + Moq + EF InMemory / Jest + Testing Library |

---

## Production URLs (2026-04-30)

### Dashboards (จัดการระบบ)
| ที่ | URL |
|-----|-----|
| Railway (Backend + DB) | `https://railway.app/dashboard` |
| Vercel (Frontend) | `https://vercel.com/dashboard` |

### Services (live)
| Service | URL |
|---------|-----|
| Backend | `https://line-booking-production-a46f.up.railway.app` |
| Frontend / LIFF | `https://line-booking-ochre.vercel.app` |
| Admin | `https://line-booking-ochre.vercel.app/admin/index.html` |
| Webhook | `https://line-booking-production-a46f.up.railway.app/webhook/diamond-massage` |
| Health Check | `https://line-booking-production-a46f.up.railway.app/health` |

- Railway: auto-deploy จาก GitHub `main`, Root Directory = `/backend`, port 8080
- Vercel: auto-deploy จาก GitHub `main`, Root Directory = `/frontend`
- LINE credentials เก็บใน DB ตาราง `tenants` (ไม่ใช่ env var)

---

## โครงสร้างไฟล์

```
diamond-massage/
├── backend/
│   ├── Controllers/
│   │   ├── BookingController.cs    ← LIFF booking API
│   │   ├── ServiceController.cs    ← GET services
│   │   ├── WebhookController.cs    ← LINE webhook
│   │   └── AdminController.cs      ← Admin API (ไม่มี auth)
│   ├── Data/AppDbContext.cs
│   ├── Jobs/ReminderJob.cs         ← Quartz 18:00 Bangkok ทุกวัน
│   ├── Models/Entities.cs          ← Tenant, User, Service, TimeSlot, Booking
│   ├── Services/
│   │   ├── LineService.cs          ← verify signature, send LINE msg
│   │   └── BookingService.cs       ← สร้างจอง, double-booking check, cancel
│   └── Program.cs                  ← CORS, Quartz, auto-seed, migrate
│
├── tests/
│   ├── BookingServiceTests.cs      ← 14 tests
│   ├── LineServiceTests.cs         ← 7 tests
│   ├── ReminderJobTests.cs         ← 8 tests
│   ├── ApiIntegrationTests.cs      ← 9 tests
│   └── AdminControllerTests.cs     ← 9 tests
│
├── frontend/
│   ├── app/
│   │   ├── page.tsx                ← LIFF booking flow (5 steps)
│   │   └── my-bookings/page.tsx    ← ประวัติ + ยกเลิก
│   ├── hooks/useLiff.ts            ← LINE login + mock mode
│   ├── lib/api.ts                  ← Axios client + types
│   ├── public/admin/index.html     ← Admin dashboard (vanilla HTML, serve ผ่าน Vercel)
│   └── __tests__/                  ← 30 frontend tests
│
└── database/schema.sql
```

---

## Local Dev

```powershell
# Backend (PowerShell — อย่ารันใน WSL, .NET 10 ไม่มีใน WSL)
cd backend && dotnet run
# → http://localhost:5000/swagger

# Frontend พร้อม LIFF mock
cd frontend && npm run dev
# .env.local ต้องมี:
# NEXT_PUBLIC_LIFF_MOCK=true
# NEXT_PUBLIC_API_URL=http://localhost:5000
# NEXT_PUBLIC_TENANT_SLUG=diamond-massage

# Tests (PowerShell)
dotnet test tests/DiamondBooking.Tests.csproj
cd frontend && npm test -- --ci
```

> ⚠️ dotnet เชื่อมกับ WSL PostgreSQL (wslrelay:5432) ไม่ใช่ Docker
> ⚠️ LIFF จริงทดสอบได้บน LINE mobile เท่านั้น

---

## Database Schema

```
tenants       — id, slug, name, line_channel_id, line_channel_secret,
                line_access_token, liff_id, timezone, is_active
users         — id, tenant_id, line_user_id, display_name, phone, picture_url
services      — id, tenant_id, name, description, duration_minutes, price, sort_order
time_slots    — id, tenant_id, day_of_week, specific_date, start_time, end_time, max_bookings
bookings      — id, tenant_id, user_id, service_id, booking_date, start_time, end_time,
                status(pending/confirmed/cancelled/completed), note, admin_note, reminder_sent
notification_logs — id, tenant_id, booking_id, user_id, type, status, sent_at
```

**Multi-tenant rule:** ทุก query ต้องมี `WHERE tenant_id = ?`

**Auto-seed:** Program.cs seed tenant + 4 services + timeslots (จันทร์–เสาร์ 10:00–20:30, 7 slots, max 2/slot) ตอน startup ถ้ายังไม่มีข้อมูล

---

## API Endpoints

### LIFF (Public)
```
GET    /api/{slug}/services
GET    /api/{slug}/bookings/slots?date=YYYY-MM-DD
POST   /api/{slug}/bookings                          body: { lineUserId, displayName, pictureUrl, serviceId, date, startTime, endTime, note }
GET    /api/{slug}/bookings?lineUserId=Uxxxx
DELETE /api/{slug}/bookings/{id}?lineUserId=Uxxxx
```

### Admin (ไม่มี auth)
```
GET         /api/{slug}/admin/dashboard
GET         /api/{slug}/admin/bookings?date=&status=
POST|PATCH  /api/{slug}/admin/bookings/{id}          body: { status, adminNote }
DELETE      /api/{slug}/admin/bookings/{id}?reason=
GET         /api/{slug}/admin/users
```

### LINE Webhook
```
POST /webhook/{slug}   Header: X-Line-Signature
  follow  → welcome message
  message "จองนัด" → LIFF link
```

### Utility
```
GET /health
GET /swagger  (Development only)
```

---

## Business Logic

### Double-booking (BookingService.cs)
1. นับ bookings overlap time range (ไม่รวม Cancelled) → เทียบ `max_bookings`
2. ตรวจ user conflict ช่วงเวลาเดียวกัน
3. เต็ม → return Thai error

### Reminder Job (ReminderJob.cs)
- Cron: `0 0 11 * * ?` (11:00 UTC = 18:00 Bangkok)
- Query: `booking_date = tomorrow AND status = Confirmed AND reminder_sent = false`
- ส่ง LINE → set `reminder_sent = true` | error ต่อ booking = catch & log ไม่หยุด

### Cancellation
- **User:** ยกเลิกก่อนนัด > 2 ชั่วโมง
- **Admin:** ยกเลิกได้ทุกเวลา + เหตุผล
- ทั้งคู่ส่ง LINE notification

### CORS
- `WithMethods("GET","POST","PUT","PATCH","DELETE","OPTIONS")` + `SetPreflightMaxAge(0)`
- Admin dashboard ใช้ POST สำหรับ update booking (รองรับ browser ที่มี extension block PATCH)

---

## Testing (77 tests รวม)

ไม่ต้องการ DB หรือ LINE credentials จริง — backend ใช้ EF InMemory, frontend mock axios + liff

```csharp
// Backend pattern
var db      = TestDbFactory.Create();
var tenant  = TestDbFactory.SeedTenant(db);
var user    = TestDbFactory.SeedUser(db, tenant.Id);
var service = TestDbFactory.SeedService(db, tenant.Id);
var slot    = TestDbFactory.SeedTimeSlot(db, tenant.Id, dayOfWeek: 1, maxBookings: 2);
```

```typescript
// Frontend pattern
jest.mock('@/hooks/useLiff', () => ({ useLiff: jest.fn() }))
jest.mock('@/lib/api', () => ({ getServices: jest.fn(), ... }))
;(useLiff as jest.Mock).mockReturnValue({ profile: mockProfile, ready: true, error: null })
```

---

## Environment Variables

### Backend (Railway Variables)
```
ConnectionStrings__Default=Host=...;Database=...;Username=...;Password=...
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
```

### Frontend (Vercel / .env.local)
```
NEXT_PUBLIC_LIFF_ID=...
NEXT_PUBLIC_API_URL=https://line-booking-production-a46f.up.railway.app
NEXT_PUBLIC_TENANT_SLUG=diamond-massage
NEXT_PUBLIC_LIFF_MOCK=true   ← local dev เท่านั้น
```

---

## Known Limitations

- Admin API ไม่มี authentication — ต้องเพิ่ม middleware ก่อนใช้งานจริง
- Timezone UTC+7 fixed — ควรใช้ `tenant.timezone` แทน
- LINE Access Token ไม่มี auto-refresh
- Admin dashboard ไม่มี password protect

---

## SaaS — เพิ่ม Tenant ใหม่

```sql
INSERT INTO tenants (slug, name, line_channel_id, line_channel_secret, line_access_token, liff_id)
VALUES ('happy-spa', 'Happy Spa', 'CHANNEL_ID', 'SECRET', 'TOKEN', 'LIFF_ID');
```

ไม่ต้องแก้ code — ทุก endpoint resolve tenant จาก `{slug}` ใน URL

### Roadmap
- [ ] Admin authentication
- [ ] Subscription billing (Stripe / Omise)
- [ ] Staff / therapist assignment
- [ ] Multiple branches per tenant
- [ ] LINE Pay integration

แก้ตรงใน DB (ผลทันที)
เข้า Railway → PostgreSQL → Query แล้วรัน SQL เช่น เปลี่ยน max เป็น 3:
UPDATE time_slots SET max_bookings = 3;