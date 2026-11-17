#!/usr/bin/env bash
# Envia um comando IniciarExecucao (envelope MassTransit) para a fila local no LocalStack.
# Uso: ./scripts/enviar-iniciar-execucao.sh [osId] [unidade]
set -euo pipefail

os_id="${1:-3f2b8c1e-6a4d-4e7b-9c3a-1d2e3f4a5b6c}"
unidade="${2:-SP-01}"
fila="local-oficina-execucao-comandos"
endpoint="http://localhost:4566"
export AWS_ACCESS_KEY_ID="${AWS_ACCESS_KEY_ID:-test}"
export AWS_SECRET_ACCESS_KEY="${AWS_SECRET_ACCESS_KEY:-test}"
export AWS_DEFAULT_REGION="${AWS_DEFAULT_REGION:-us-east-1}"

dir="$(cd "$(dirname "$0")" && pwd)"
exemplo="$dir/iniciar-execucao.exemplo.json"

# A fila so existe depois que o MassTransit da API inicia.
fila_url=""
for _ in $(seq 1 30); do
  if fila_url=$(aws --endpoint-url "$endpoint" sqs get-queue-url --queue-name "$fila" --query QueueUrl --output text 2>/dev/null); then
    break
  fi
  fila_url=""
  sleep 2
done
if [[ -z "$fila_url" ]]; then
  echo "ERRO: a fila '$fila' nao existe apos 60s. A API esta no ar? (docker compose -f docker/docker-compose.yml up -d --wait)" >&2
  exit 1
fi

message_id=$(cat /proc/sys/kernel/random/uuid 2>/dev/null || openssl rand -hex 16)
agora=$(date -u +%Y-%m-%dT%H:%M:%SZ)
corpo=$(sed -e "s/__OS_ID__/$os_id/" -e "s/__UNIDADE__/$unidade/" -e "s/__AGORA__/$agora/" "$exemplo")
envelope=$(printf '{"messageId":"%s","messageType":["urn:message:oficina:saga:IniciarExecucao"],"message":%s}' "$message_id" "$corpo")

aws --endpoint-url "$endpoint" sqs send-message \
  --queue-url "$fila_url" \
  --message-body "$envelope" \
  --message-attributes '{"Content-Type":{"DataType":"String","StringValue":"application/vnd.masstransit+json"}}'
