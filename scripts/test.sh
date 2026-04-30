#!/bin/bash
# scripts/test.sh  —  รันเทสทั้งหมด (backend + frontend)
 
set -e
 
PASS=0; FAIL=0
 
run() {
  local label="$1"; shift
  echo ""
  echo "▶ $label"
  echo "─────────────────────────────────────────"
  if "$@"; then
    echo "✅ $label passed"
    PASS=$((PASS+1))
  else
    echo "❌ $label failed"
    FAIL=$((FAIL+1))
  fi
}
 
# ── Parse flags ───────────────────────────────────────────────────
BACKEND=true; FRONTEND=true; COVERAGE=false; WATCH=false
 
for arg in "$@"; do
  case $arg in
    --backend-only)  FRONTEND=false ;;
    --frontend-only) BACKEND=false  ;;
    --coverage)      COVERAGE=true  ;;
    --watch)         WATCH=true     ;;
  esac
done
 
echo "╔══════════════════════════════════════════╗"
echo "║   💎 Diamond Massage — Test Runner       ║"
echo "╚══════════════════════════════════════════╝"
 
# ── Backend (.NET xUnit) ──────────────────────────────────────────
if $BACKEND; then
  if $COVERAGE; then
    run "Backend tests (with coverage)" \
      dotnet test tests/DiamondBooking.Tests.csproj \
        --collect:"XPlat Code Coverage" \
        --results-directory ./coverage/backend \
        --logger "console;verbosity=normal"
  else
    run "Backend tests" \
      dotnet test tests/DiamondBooking.Tests.csproj \
        --logger "console;verbosity=normal"
  fi
fi
 
# ── Frontend (Jest) ───────────────────────────────────────────────
if $FRONTEND; then
  echo ""
  echo "▶ Installing frontend dependencies..."
  (cd frontend && npm install --silent)
 
  cd frontend/
 
  if $WATCH; then
    echo ""; echo "▶ Frontend tests (watch mode)"
    npm run test:watch
  elif $COVERAGE; then
    run "Frontend tests (with coverage)" npm run test:coverage -- --ci
  else
    run "Frontend tests" npm test -- --ci
  fi
  cd ..
fi
 
# ── Summary ───────────────────────────────────────────────────────
echo ""
echo "══════════════════════════════════════════"
echo "  Results: ✅ $PASS passed  ❌ $FAIL failed"
echo "══════════════════════════════════════════"
 
[ $FAIL -eq 0 ] && exit 0 || exit 1