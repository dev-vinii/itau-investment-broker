#!/usr/bin/env bash
set -euo pipefail

MOTOR_EXECUTION_URL="${MOTOR_EXECUTION_URL:-http://api:8080/api/motor/executar-compra}"
STATE_DIR="${STATE_DIR:-/state}"
STATE_FILE="${STATE_DIR}/last-execution"
EXEC_HISTORY_FILE="${STATE_DIR}/script-executions.log"
IN_PROGRESS_FILE="${STATE_DIR}/in-progress-cycle"
LOCK_DIR="${STATE_DIR}/run-motor.lock"
MAX_ATTEMPTS="${MAX_ATTEMPTS:-3}"
RETRY_BASE_DELAY_SECONDS="${RETRY_BASE_DELAY_SECONDS:-2}"
REQUEST_TIMEOUT_SECONDS="${REQUEST_TIMEOUT_SECONDS:-20}"
RESPONSE_FILE="${RESPONSE_FILE:-/tmp/motor-response.json}"
MAX_DAILY_EXECUTIONS="${MAX_DAILY_EXECUTIONS:-1}"
MAX_MONTHLY_EXECUTIONS="${MAX_MONTHLY_EXECUTIONS:-3}"

mkdir -p "${STATE_DIR}"

today_day="$(date +%d | sed 's/^0//')"
today_key="$(date +%Y-%m-%d)"
month_key="$(date +%Y-%m)"

log() {
  local level="$1"
  local message="$2"
  printf '[scheduler] ts=%s level=%s msg="%s"\n' "$(date -Iseconds)" "${level}" "${message}"
}

write_atomic() {
  local target_file="$1"
  local content="$2"
  local tmp_file
  tmp_file="$(mktemp "${STATE_DIR}/tmp.XXXXXX")"
  printf '%s\n' "${content}" > "${tmp_file}"
  mv "${tmp_file}" "${target_file}"
}

cleanup_lock() {
  rmdir "${LOCK_DIR}" 2>/dev/null || true
}

if ! mkdir "${LOCK_DIR}" 2>/dev/null; then
  log "WARN" "execucao concorrente detectada; finalizando para evitar duplicidade."
  exit 0
fi
trap cleanup_lock EXIT

touch "${EXEC_HISTORY_FILE}"

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
  log "INFO" "${today_key}: fora da janela 5/15/25 (com ajuste de dia util)."
  exit 0
fi

daily_count="$(grep -c "^${today_key}$" "${EXEC_HISTORY_FILE}" || true)"
if [ "${daily_count}" -ge "${MAX_DAILY_EXECUTIONS}" ]; then
  log "WARN" "${today_key}: limite diario atingido (${daily_count}/${MAX_DAILY_EXECUTIONS})."
  exit 0
fi

monthly_count="$(grep -c "^${month_key}-" "${EXEC_HISTORY_FILE}" || true)"
if [ "${monthly_count}" -ge "${MAX_MONTHLY_EXECUTIONS}" ]; then
  log "WARN" "${today_key}: limite mensal atingido (${monthly_count}/${MAX_MONTHLY_EXECUTIONS})."
  exit 0
fi

if [ -f "${IN_PROGRESS_FILE}" ] && grep -qx "${cycle_label}" "${IN_PROGRESS_FILE}"; then
  log "WARN" "${today_key}: ciclo ${cycle_label} marcado como in-progress. Bloqueando nova execucao automatica para evitar duplicidade. Acione execucao manual apos validacao."
  exit 0
fi

if [ -f "${STATE_FILE}" ] && grep -qx "${cycle_label}" "${STATE_FILE}"; then
  log "INFO" "${today_key}: ciclo ${cycle_label} ja executado, ignorando."
  exit 0
fi

log "INFO" "${today_key}: disparando ciclo ${cycle_label} em ${MOTOR_EXECUTION_URL}"
write_atomic "${IN_PROGRESS_FILE}" "${cycle_label}"

attempt=1
success="false"

while [ "${attempt}" -le "${MAX_ATTEMPTS}" ]; do
  start_epoch="$(date +%s)"
  http_code="$(curl -sS --max-time "${REQUEST_TIMEOUT_SECONDS}" -o "${RESPONSE_FILE}" -w "%{http_code}" -X POST "${MOTOR_EXECUTION_URL}" || echo "000")"
  end_epoch="$(date +%s)"
  elapsed_seconds=$((end_epoch - start_epoch))

  if [ "${http_code}" -ge 200 ] && [ "${http_code}" -lt 300 ]; then
    log "INFO" "tentativa=${attempt}/${MAX_ATTEMPTS} http=${http_code} duracao=${elapsed_seconds}s ciclo=${cycle_label} resultado=sucesso"
    success="true"
    break
  fi

  log "WARN" "tentativa=${attempt}/${MAX_ATTEMPTS} http=${http_code} duracao=${elapsed_seconds}s ciclo=${cycle_label} resultado=falha"
  if [ -s "${RESPONSE_FILE}" ]; then
    log "WARN" "resposta_erro=$(tr '\n' ' ' < "${RESPONSE_FILE}" | cut -c1-300)"
  fi

  if [ "${attempt}" -lt "${MAX_ATTEMPTS}" ]; then
    sleep_seconds=$((RETRY_BASE_DELAY_SECONDS * attempt))
    log "INFO" "aguardando ${sleep_seconds}s antes da proxima tentativa"
    sleep "${sleep_seconds}"
  fi

  attempt=$((attempt + 1))
done

if [ "${success}" != "true" ]; then
  log "ERROR" "falha apos ${MAX_ATTEMPTS} tentativa(s): ciclo=${cycle_label}. O marcador in-progress foi mantido para evitar duplicidade automatica."
  exit 1
fi

write_atomic "${STATE_FILE}" "${cycle_label}"
printf '%s\n' "${today_key}" >> "${EXEC_HISTORY_FILE}"
rm -f "${IN_PROGRESS_FILE}"
log "INFO" "sucesso: ciclo ${cycle_label} executado."
