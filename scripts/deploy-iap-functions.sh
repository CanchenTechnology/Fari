#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

cd "$ROOT_DIR"

if [[ -n "${MOONLY_PROXY:-}" ]]; then
  export HTTPS_PROXY="$MOONLY_PROXY"
  export HTTP_PROXY="$MOONLY_PROXY"
fi

if [[ -n "${MOONLY_ALL_PROXY:-}" ]]; then
  export ALL_PROXY="$MOONLY_ALL_PROXY"
fi

echo "Checking required Firebase Functions secrets before IAP deploy..."
"$ROOT_DIR/scripts/check-firebase-secrets.sh"

echo
echo "Deploying IAP receipt verification functions..."
MOONLY_BIND_ALL_SECRETS=1 \
FIREBASE_ONLY=functions:readinessStatus,functions:submitIapReceipt \
"$ROOT_DIR/scripts/deploy-firebase.sh"

echo
echo "IAP Functions deploy finished."
echo "Next real receipt smoke example:"
echo "  IAP_SMOKE_MODE=real IAP_PRODUCT_ID=fari.pro.monthly IAP_STORE=AppleAppStore IAP_RECEIPT='<receipt>' ./scripts/smoke-submit-iap-receipt.sh"
