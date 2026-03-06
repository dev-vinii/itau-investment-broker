#!/usr/bin/env bash
set -euo pipefail

MOTOR_EXECUTION_URL="${MOTOR_EXECUTION_URL:-http://api:8080/api/motor/executar-compra}"
STATE_DIR="${STATE_DIR:-/state}"
STATE_FILE="${STATE_DIR}/last-execution"

mkdir -p "${STATE_DIR}"

today_day="$(date +%d | sed 's/^0//')"
today_key="$(date +%Y-%m-%d)"

calc_exec_day_for_target() {
  local target_day="$1"
  local year month target_date dow exec_day
  year="$(date +%Y)"
  month="$(date +%m)"

  target_date="${year}-${month}-$(printf "%02d" "${target_day}")"
  dow="$(date -d "${target_date}" +%u)"

  exec_day="${target_day}"
  if [ "${dow}" -eq 6 ]; then
    exec_day=$((target_day + 2))
  elif [ "${dow}" -eq 7 ]; then
    exec_day=$((target_day + 1))
  fi

  echo "${exec_day}"
}

should_run="false"
cycle_label=""

for target in 5 15 25; do
  exec_day="$(calc_exec_day_for_target "${target}")"
  if [ "${today_day}" -eq "${exec_day}" ]; then
    should_run="true"
    cycle_label="$(date +%Y-%m)-${target}"
    break
  fi
done

if [ "${should_run}" != "true" ]; then
  echo "[scheduler] ${today_key}: fora da janela 5/15/25 (com ajuste de dia util)."
  exit 0
fi

if [ -f "${STATE_FILE}" ] && grep -qx "${cycle_label}" "${STATE_FILE}"; then
  echo "[scheduler] ${today_key}: ciclo ${cycle_label} ja executado, ignorando."
  exit 0
fi

echo "[scheduler] ${today_key}: disparando ciclo ${cycle_label} em ${MOTOR_EXECUTION_URL}"
http_code="$(curl -sS -o /tmp/motor-response.json -w "%{http_code}" -X POST "${MOTOR_EXECUTION_URL}")"

if [ "${http_code}" -lt 200 ] || [ "${http_code}" -ge 300 ]; then
  echo "[scheduler] erro HTTP ${http_code} ao chamar motor."
  cat /tmp/motor-response.json || true
  exit 1
fi

echo "${cycle_label}" > "${STATE_FILE}"
echo "[scheduler] sucesso: ciclo ${cycle_label} executado."
