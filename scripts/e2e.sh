# Uji alur admin end-to-end terhadap PostgreSQL nyata.
# Jalankan dari folder proyek: bash scripts/e2e.sh
set -euo pipefail

BASE="${BASE:-http://localhost:5260}"
COOKIE="$(mktemp)"
trap 'rm -f "$COOKIE"' EXIT

set -a
. ./.env
set +a

pass() { echo "  OK   $1"; }
fail() { echo "  FAIL $1"; exit 1; }

echo "1. health"
[ "$(curl -s -o /dev/null -w '%{http_code}' "$BASE/health")" = "200" ] && pass health || fail health

echo "2. admin tanpa sesi harus 401"
[ "$(curl -s -o /dev/null -w '%{http_code}' "$BASE/api/admin/cases")" = "401" ] && pass unauthenticated || fail unauthenticated

echo "3. login"
[ "$(curl -s -c "$COOKIE" -o /dev/null -w '%{http_code}' -X POST "$BASE/api/admin/login" \
  -H 'Content-Type: application/json' \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PASSWORD\"}")" = "200" ] && pass login || fail login

echo "4. buat sumber"
SRC=$(curl -s -b "$COOKIE" -X POST "$BASE/api/admin/sources" -H 'Content-Type: application/json' \
  -d '{"title":"Uji sumber","url":"https://www.bnpb.go.id/berita/uji","publisher":"BNPB","checkedAt":"2026-04-06"}')
SID=$(printf '%s' "$SRC" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p')
[ -n "$SID" ] && pass "source $SID" || fail "source: $SRC"

echo "5. buat kasus"
SLUG="kasus-uji-$(date +%s)"
CSE=$(curl -s -b "$COOKIE" -X POST "$BASE/api/admin/cases" -H 'Content-Type: application/json' \
  -d "{\"title\":\"Kasus uji relational\",\"slug\":\"$SLUG\",\"summary\":\"Ringkasan uji.\"}")
CID=$(printf '%s' "$CSE" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p')
[ -n "$CID" ] && pass "case $CID" || fail "case: $CSE"

echo "6. draft tidak boleh tampil di publik"
[ "$(curl -s -o /dev/null -w '%{http_code}' "$BASE/api/cases/$SLUG")" = "404" ] && pass draft-hidden || fail draft-hidden

echo "7. publish kasus tanpa catatan dana harus 422"
[ "$(curl -s -b "$COOKIE" -o /dev/null -w '%{http_code}' -X POST "$BASE/api/admin/cases/$CID/publish")" = "422" ] && pass publish-422 || fail publish-422

echo "8. catatan dana 537,22 miliar (butuh bigint)"
REC=$(curl -s -b "$COOKIE" -X POST "$BASE/api/admin/funding-records" -H 'Content-Type: application/json' \
  -d "{\"caseId\":\"$CID\",\"informationType\":\"realization_reported\",\"amount\":537220000000,\"purpose\":\"Bantuan stimulan\",\"periodLabel\":\"hingga 6 April 2026\",\"sources\":[{\"sourceId\":\"$SID\",\"supportingNote\":\"Tabel\"}]}")
printf '%s' "$REC" | grep -q '537220000000' && pass "record 537220000000" || fail "record: $REC"

echo "9. publish harus 200"
[ "$(curl -s -b "$COOKIE" -o /dev/null -w '%{http_code}' -X POST "$BASE/api/admin/cases/$CID/publish")" = "200" ] && pass published || fail published

echo "10. detail publik memuat angka dan sumber"
DETAIL=$(curl -s "$BASE/api/cases/$SLUG")
printf '%s' "$DETAIL" | grep -q '537220000000' || fail "amount hilang: $DETAIL"
printf '%s' "$DETAIL" | grep -q 'BNPB' || fail "publisher hilang: $DETAIL"
pass "detail lengkap"

echo "11. edit sumber yang dipakai -> kasus kembali draft"
[ "$(curl -s -b "$COOKIE" -o /dev/null -w '%{http_code}' -X PATCH "$BASE/api/admin/sources/$SID" \
  -H 'Content-Type: application/json' \
  -d '{"title":"Uji sumber revisi","url":"https://www.bnpb.go.id/berita/revisi","publisher":"BNPB","checkedAt":"2026-04-07"}')" = "200" ] && pass source-patched || fail source-patched
[ "$(curl -s -o /dev/null -w '%{http_code}' "$BASE/api/cases/$SLUG")" = "404" ] && pass demoted || fail demoted

echo
echo "SEMUA LANGKAH LULUS"
