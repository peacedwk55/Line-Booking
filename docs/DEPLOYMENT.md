# 🚀 Diamond Massage — คู่มือ Deploy ฉบับสมบูรณ์

## ภาพรวมระบบ

```
LINE Official Account
    ↓ Webhook
Backend (.NET API) ──── PostgreSQL
    ↑
LIFF App (Next.js)
    ↑
Admin Dashboard (HTML)
```

---

## ✅ ขั้นตอนที่ 1: ตั้งค่า LINE

### 1.1 สร้าง LINE Official Account
1. ไปที่ https://manager.line.biz
2. สร้าง Official Account ใหม่ (ชื่อ "Diamond Massage")
3. ไปที่ **Settings → Messaging API → Enable**

### 1.2 ตั้งค่า Messaging API
1. ไปที่ https://developers.line.biz → เลือก Provider → Channel ที่สร้าง
2. จดค่าต่อไปนี้:
   - **Channel ID**
   - **Channel Secret** (Basic settings)
   - **Channel Access Token** (Messaging API → Issue)

### 1.3 สร้าง LIFF App
1. ใน LINE Developers Console → เลือก Channel → **LIFF**
2. กด **Add** → ตั้งค่า:
   - **Name**: Diamond Massage Booking
   - **Size**: Full
   - **Endpoint URL**: `https://your-liff-app.vercel.app` (ใส่ทีหลัง)
   - **Scopes**: profile, openid
3. จด **LIFF ID** (รูปแบบ `1234567890-xxxxxxxx`)

---

## ✅ ขั้นตอนที่ 2: Deploy Backend บน Railway

### 2.1 สร้าง Railway Project
```bash
# ติดตั้ง Railway CLI
npm install -g @railway/cli

# Login
railway login

# สร้างโปรเจค
railway new
# ตั้งชื่อ: diamond-massage-api
```

### 2.2 เพิ่ม PostgreSQL
1. ใน Railway Dashboard → **New** → **Database** → **PostgreSQL**
2. Railway จะสร้าง `DATABASE_URL` ให้อัตโนมัติ

### 2.3 Deploy Backend
```bash
cd backend/

# Link กับ Railway project
railway link

# ตั้ง environment variables
railway variables set ConnectionStrings__Default="$DATABASE_URL"
railway variables set FrontendUrl="https://your-liff-app.vercel.app"
railway variables set ASPNETCORE_ENVIRONMENT="Production"
railway variables set ASPNETCORE_URLS="http://+:8080"

# Deploy
railway up
```

### 2.4 สร้าง Dockerfile (ถ้า Railway ต้องการ)
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY *.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .
EXPOSE 8080
ENTRYPOINT ["dotnet", "DiamondBooking.Api.dll"]
```

### 2.5 รัน Database Migration + Seed
```bash
# ใน Railway terminal หรือ local (ชี้ไปที่ Railway DB)
dotnet ef migrations add InitialCreate
dotnet ef database update

# รัน seed SQL
psql $DATABASE_URL < database/schema.sql
# (เฉพาะส่วน INSERT INTO tenants ... และ services ... และ time_slots)
```

จด Backend URL: `https://diamond-massage-api.railway.app`

---

## ✅ ขั้นตอนที่ 3: Deploy Frontend บน Vercel

### 3.1 เตรียม Environment Variables
สร้างไฟล์ `frontend/.env.local`:
```env
NEXT_PUBLIC_LIFF_ID=1234567890-xxxxxxxx
NEXT_PUBLIC_API_URL=https://diamond-massage-api.railway.app
NEXT_PUBLIC_TENANT_SLUG=diamond-massage
```

### 3.2 Deploy ด้วย Vercel
```bash
cd frontend/

# ติดตั้ง Vercel CLI
npm install -g vercel

# Deploy (ครั้งแรก)
vercel

# ตั้ง environment variables บน Vercel
vercel env add NEXT_PUBLIC_LIFF_ID
vercel env add NEXT_PUBLIC_API_URL
vercel env add NEXT_PUBLIC_TENANT_SLUG

# Deploy production
vercel --prod
```

จด Frontend URL: `https://diamond-massage-liff.vercel.app`

### 3.3 อัพเดต LIFF Endpoint URL
1. ไปที่ LINE Developers → LIFF → Edit
2. อัพเดต **Endpoint URL** → `https://diamond-massage-liff.vercel.app`

---

## ✅ ขั้นตอนที่ 4: ตั้งค่า LINE Webhook

### 4.1 อัพเดต Webhook URL
1. LINE Developers Console → Messaging API
2. **Webhook URL**: `https://diamond-massage-api.railway.app/webhook/diamond-massage`
3. กด **Verify** — ต้องได้ ✅ Success
4. เปิด **Use webhook: ON**

### 4.2 อัพเดต Tenant ในฐานข้อมูล
```sql
UPDATE tenants
SET
  line_channel_id     = 'YOUR_CHANNEL_ID',
  line_channel_secret = 'YOUR_CHANNEL_SECRET',
  line_access_token   = 'YOUR_ACCESS_TOKEN',
  liff_id             = 'YOUR_LIFF_ID'
WHERE slug = 'diamond-massage';
```

---

## ✅ ขั้นตอนที่ 5: ตั้งค่า Rich Menu

### สร้าง Rich Menu ผ่าน LINE Messaging API
```bash
curl -X POST https://api.line.me/v2/bot/richmenu \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "size": {"width": 2500, "height": 843},
    "selected": true,
    "name": "Diamond Massage Menu",
    "chatBarText": "เมนู",
    "areas": [
      {
        "bounds": {"x": 0, "y": 0, "width": 1250, "height": 843},
        "action": {
          "type": "uri",
          "uri": "https://liff.line.me/YOUR_LIFF_ID"
        }
      },
      {
        "bounds": {"x": 1250, "y": 0, "width": 1250, "height": 843},
        "action": {
          "type": "uri",
          "uri": "https://liff.line.me/YOUR_LIFF_ID/my-bookings"
        }
      }
    ]
  }'
```

---

## ✅ ขั้นตอนที่ 6: ทดสอบระบบ

### 6.1 ทดสอบ Backend
```bash
# Health check
curl https://diamond-massage-api.railway.app/health

# Services
curl https://diamond-massage-api.railway.app/api/diamond-massage/services

# Available slots
curl "https://diamond-massage-api.railway.app/api/diamond-massage/bookings/slots?date=2025-02-10"
```

### 6.2 ทดสอบ LIFF
1. เพิ่ม LINE OA เป็นเพื่อน
2. กด Rich Menu "จองนัด"
3. ทำตามขั้นตอนจนจองสำเร็จ
4. ตรวจสอบว่าได้รับ Flex Message ยืนยัน

### 6.3 Admin Dashboard
1. เปิด `frontend/admin/index.html`
2. แก้ไข `API` ใน JavaScript ให้ตรงกับ Railway URL
3. เปิดผ่าน browser (หรือ host บน Vercel/Netlify)

---

## 📁 โครงสร้างโปรเจค

```
diamond-massage/
├── backend/
│   ├── Controllers/
│   │   ├── BookingController.cs    ← LIFF Booking API
│   │   ├── ServiceController.cs   ← Services list
│   │   ├── WebhookController.cs   ← LINE webhook
│   │   └── AdminController.cs     ← Admin panel API
│   ├── Data/
│   │   └── AppDbContext.cs
│   ├── Jobs/
│   │   └── ReminderJob.cs         ← Quartz reminder
│   ├── Models/
│   │   └── Entities.cs
│   ├── Services/
│   │   ├── LineService.cs         ← LINE Messaging API
│   │   └── BookingService.cs      ← Booking logic
│   ├── Program.cs
│   └── appsettings.json
│
├── frontend/
│   ├── app/
│   │   ├── page.tsx               ← Booking flow (LIFF)
│   │   ├── my-bookings/page.tsx   ← Booking history
│   │   └── layout.tsx
│   ├── hooks/
│   │   └── useLiff.ts             ← LINE login hook
│   ├── lib/
│   │   └── api.ts                 ← API client
│   └── admin/
│       └── index.html             ← Admin dashboard
│
└── database/
    └── schema.sql                 ← DB schema + seed
```

---

## 🔑 API Endpoints สรุป

| Method | Path | Description |
|--------|------|-------------|
| GET    | `/api/{slug}/services` | รายการบริการ |
| GET    | `/api/{slug}/bookings/slots?date=` | ช่วงเวลาว่าง |
| POST   | `/api/{slug}/bookings` | สร้างการจอง |
| GET    | `/api/{slug}/bookings?lineUserId=` | การจองของผู้ใช้ |
| DELETE | `/api/{slug}/bookings/{id}` | ยกเลิก (ผู้ใช้) |
| GET    | `/api/{slug}/admin/bookings` | รายการจอง (admin) |
| PATCH  | `/api/{slug}/admin/bookings/{id}` | อัพเดตสถานะ |
| DELETE | `/api/{slug}/admin/bookings/{id}` | ยกเลิก (admin) |
| GET    | `/api/{slug}/admin/users` | รายชื่อลูกค้า |
| GET    | `/api/{slug}/admin/dashboard` | สถิติ |
| POST   | `/webhook/{slug}` | LINE webhook |
| GET    | `/health` | Health check |

---

## 💡 เพิ่มลูกค้า SaaS รายใหม่

เพียงแค่ INSERT 1 แถวในตาราง `tenants`:
```sql
INSERT INTO tenants (slug, name, line_channel_id, line_channel_secret, line_access_token, liff_id)
VALUES ('happy-spa', 'Happy Spa', 'CHANNEL_ID', 'SECRET', 'TOKEN', 'LIFF_ID');
```
ระบบรองรับหลายธุรกิจโดยไม่ต้องแก้ code!
