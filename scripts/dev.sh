#!/bin/bash
# scripts/dev.sh  —  รัน local development

set -e

echo "🚀 Diamond Massage — Local Dev Setup"
echo "======================================"

# Check prerequisites
command -v dotnet >/dev/null 2>&1 || { echo "❌ ต้องการ .NET 10 SDK: https://dotnet.microsoft.com/download"; exit 1; }
command -v node   >/dev/null 2>&1 || { echo "❌ ต้องการ Node.js 20+: https://nodejs.org"; exit 1; }
command -v docker >/dev/null 2>&1 || { echo "❌ ต้องการ Docker Desktop: https://www.docker.com/products/docker-desktop"; exit 1; }

# Start PostgreSQL via Docker
echo ""
echo "🐘 เริ่ม PostgreSQL (Docker)..."
docker compose up -d postgres
echo "  รอ PostgreSQL พร้อม..."
until docker compose exec -T postgres pg_isready -U postgres >/dev/null 2>&1; do sleep 1; done
echo "✅ PostgreSQL ready"

# Backend
echo ""
echo "🔧 เริ่ม Backend..."
cd backend/
dotnet restore -q
echo "  Backend: http://localhost:5000"
echo "  Swagger: http://localhost:5000/swagger"
ASPNETCORE_ENVIRONMENT=Development dotnet run &
BACKEND_PID=$!
cd ..

# Wait for backend
sleep 3

# Frontend
echo ""
echo "🎨 เริ่ม Frontend..."
cd frontend/
npm install -q
echo "  LIFF App: http://localhost:3000"
npm run dev &
FRONTEND_PID=$!
cd ..

echo ""
echo "======================================"
echo "✅ ระบบพร้อมใช้งาน!"
echo ""
echo "  Backend API : http://localhost:5000"
echo "  Swagger UI  : http://localhost:5000/swagger"
echo "  LIFF App    : http://localhost:3000"
echo "  Admin Panel : open frontend/admin/index.html"
echo ""
echo "กด Ctrl+C เพื่อหยุด"
echo "======================================"

# Trap Ctrl+C
trap "kill $BACKEND_PID $FRONTEND_PID 2>/dev/null; docker compose stop postgres; echo 'Stopped.'" INT
wait
