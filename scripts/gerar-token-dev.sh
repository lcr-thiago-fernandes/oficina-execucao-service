#!/usr/bin/env bash
# Gera um JWT HS256 de desenvolvimento com as claims emitidas pela Lambda oficina-auth.
# Uso: JWT_SECRET=... ./scripts/gerar-token-dev.sh [Atendente|Admin]   (le .env se JWT_SECRET nao estiver definido)
set -euo pipefail

perfil="${1:-Atendente}"
if [[ -z "${JWT_SECRET:-}" && -f "$(dirname "$0")/../.env" ]]; then
  JWT_SECRET="$(grep -E '^JWT_SECRET=' "$(dirname "$0")/../.env" | head -1 | cut -d= -f2-)"
fi
: "${JWT_SECRET:?defina JWT_SECRET (ou crie .env a partir de .env.example)}"

b64url() { openssl base64 -A | tr '+/' '-_' | tr -d '='; }

agora=$(date +%s)
exp=$((agora + 3600))
jti=$(cat /proc/sys/kernel/random/uuid 2>/dev/null || openssl rand -hex 16)

header=$(printf '{"alg":"HS256","typ":"JWT"}' | b64url)
payload=$(printf '{"sub":"dev-user","perfil":"%s","nome":"Usuario Dev","jti":"%s","iss":"oficina-auth","aud":"oficina-api","exp":%s}' \
  "$perfil" "$jti" "$exp" | b64url)
assinatura=$(printf '%s.%s' "$header" "$payload" | openssl dgst -sha256 -hmac "$JWT_SECRET" -binary | b64url)

printf '%s.%s.%s\n' "$header" "$payload" "$assinatura"
