#!/usr/bin/env bash
#
# Gate local pre-PR: build sense warnings + tests + cobertura de línies combinada >= 80%.
# Equival al job build-and-test de CI (.github/workflows/ci.yml) pero en local.
#
# Us:
#   ./build/gate.sh
#
# Variables opcionals:
#   MIN_COVERAGE=80   llindar de cobertura de línies combinada (en %)
#   REPORTGENERATOR=  ruta al binari reportgenerator (per defecte el busca)
#
set -euo pipefail

ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

MIN_COVERAGE="${MIN_COVERAGE:-80}"

# --- localitza reportgenerator ------------------------------------------------
REPORTGENERATOR="${REPORTGENERATOR:-}"
if [[ -z "$REPORTGENERATOR" ]]; then
    for candidate in \
        "$HOME/.dotnet/tools/reportgenerator" \
        "$(command -v reportgenerator 2>/dev/null || true)"; do
        if [[ -n "$candidate" && -x "$candidate" ]]; then
            REPORTGENERATOR="$candidate"
            break
        fi
    done
fi
if [[ -z "$REPORTGENERATOR" ]]; then
    echo "error: reportgenerator no trobat. Instal·la'l amb:" >&2
    echo "  dotnet tool install -g dotnet-reportgenerator-globaltool --version 5.2.4" >&2
    echo "o passa la ruta amb REPORTGENERATOR=..." >&2
    exit 2
fi

RESULTS_DIR="$(mktemp -d "${TMPDIR:-/tmp}/gate-results.XXXXXX")"
COVERAGE_DIR="$RESULTS_DIR/coverage"
REPORT_DIR="$RESULTS_DIR/report"
TEST_LOG="$RESULTS_DIR/tests.log"
mkdir -p "$COVERAGE_DIR" "$REPORT_DIR"

echo "==> Build (Release, -warnaserror --no-incremental)"
dotnet build MyAccountingApp.sln -c Release -warnaserror --no-incremental

echo "==> Tests + cobertura"
dotnet test MyAccountingApp.sln -c Release --no-build \
    --settings build/coverlet.runsettings \
    --collect:"XPlat Code Coverage" \
    --results-directory "$COVERAGE_DIR" 2>&1 | tee "$TEST_LOG"

# --- fusiona les cobertures ---------------------------------------------------
REPORTS="$(find "$COVERAGE_DIR" -type f -name 'coverage.cobertura.xml' | tr '\n' ';')"
if [[ -z "$REPORTS" ]]; then
    echo "error: no s'ha generat cap coverage.cobertura.xml" >&2
    exit 1
fi

"$REPORTGENERATOR" \
    -reports:"$REPORTS" \
    -targetdir:"$REPORT_DIR" \
    -reporttypes:"Cobertura;TextSummary"

LINE_COVERAGE="$(grep -oP '<coverage .*line-rate="\K[0-9.]+' "$REPORT_DIR/Cobertura.xml" | head -n 1 | awk '{print $1*100}')"

echo ""
echo "==> Resum"
grep -h "Passed!" "$TEST_LOG" || true
PASSED_TOTAL="$(grep -oP 'Passed:\s+\K[0-9]+' "$TEST_LOG" | awk '{s+=$1} END {print s+0}')"
echo "Total de tests passats: $PASSED_TOTAL"
echo "Cobertura de línies combinada: $LINE_COVERAGE% (minim $MIN_COVERAGE%)"
echo "Resultats: $RESULTS_DIR"

if awk "BEGIN { exit !($LINE_COVERAGE >= $MIN_COVERAGE) }"; then
    echo "GATE OK"
else
    echo "GATE FAIL: cobertura per sota del llindar" >&2
    exit 1
fi