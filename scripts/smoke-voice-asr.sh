#!/usr/bin/env bash
set -euo pipefail

PROJECT_ID="${FIREBASE_PROJECT:-fari-app-b2fd2}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
API_KEY="${FIREBASE_WEB_API_KEY:-}"
BASE_URL="${FUNCTIONS_BASE_URL:-https://us-central1-$PROJECT_ID.cloudfunctions.net}"
CLEANUP_AUTH_USER="${CLEANUP_AUTH_USER:-1}"
CURL_MAX_TIME="${CURL_MAX_TIME:-90}"
CURL_CONNECT_TIMEOUT="${CURL_CONNECT_TIMEOUT:-20}"
CURL_RETRY="${CURL_RETRY:-1}"
CURL_RETRY_DELAY="${CURL_RETRY_DELAY:-2}"
VOICE_ASR_REQUIRE_LIVE="${VOICE_ASR_REQUIRE_LIVE:-0}"
VOICE_ASR_SMOKE_TEXT="${VOICE_ASR_SMOKE_TEXT:-你好，今天测试语音识别。}"
VOICE_ASR_EXPECT_TEXT="${VOICE_ASR_EXPECT_TEXT:-}"
VOICE_ASR_AUDIO_MODE="${VOICE_ASR_AUDIO_MODE:-say}"
VOICE_ASR_SAMPLE_RATE="${VOICE_ASR_SAMPLE_RATE:-16000}"
VOICE_ASR_SOURCE="${VOICE_ASR_SOURCE:-smoke_voice_asr}"

if [[ -n "${MOONLY_PROXY:-}" ]]; then
  export HTTPS_PROXY="$MOONLY_PROXY"
  export HTTP_PROXY="$MOONLY_PROXY"
fi

if [[ -n "${MOONLY_ALL_PROXY:-}" ]]; then
  export ALL_PROXY="$MOONLY_ALL_PROXY"
fi

if ! command -v node >/dev/null 2>&1; then
  echo "node not found." >&2
  exit 127
fi

if ! command -v curl >/dev/null 2>&1; then
  echo "curl not found." >&2
  exit 127
fi

if [[ -z "$API_KEY" ]]; then
  API_KEY="$(
    node - "$ROOT_DIR" <<'NODE'
const fs = require("fs");
const path = require("path");
const root = process.argv[2];
const candidates = [
  "Assets/google-services.json",
  "Assets/StreamingAssets/google-services-desktop.json",
];

for (const relativePath of candidates) {
  const fullPath = path.join(root, relativePath);
  if (!fs.existsSync(fullPath)) continue;
  const data = JSON.parse(fs.readFileSync(fullPath, "utf8"));
  for (const client of data.client || []) {
    for (const key of client.api_key || []) {
      if (key.current_key) {
        process.stdout.write(key.current_key);
        process.exit(0);
      }
    }
  }
}
process.exit(1);
NODE
  )" || {
    echo "Firebase Web API key not found. Set FIREBASE_WEB_API_KEY or keep Assets/google-services.json available." >&2
    exit 2
  }
fi

TMP_DIR="$(mktemp -d)"
ID_TOKEN=""
LOCAL_ID=""

curl_http() {
  local output="$1"
  shift

  local status_file="$TMP_DIR/curl-status-$RANDOM.txt"
  local error_file="$TMP_DIR/curl-error-$RANDOM.txt"
  local exit_code=0
  local http_status

  curl \
    --silent \
    --show-error \
    --location \
    --max-time "$CURL_MAX_TIME" \
    --connect-timeout "$CURL_CONNECT_TIMEOUT" \
    --retry "$CURL_RETRY" \
    --retry-all-errors \
    --retry-delay "$CURL_RETRY_DELAY" \
    "$@" \
    --output "$output" \
    --write-out "%{http_code}" \
    >"$status_file" \
    2>"$error_file" || exit_code=$?

  http_status="$(tr -d '\r\n' <"$status_file" 2>/dev/null || true)"
  [[ -f "$output" ]] || : >"$output"

  if [[ "$exit_code" -ne 0 || ! "$http_status" =~ ^[0-9][0-9][0-9]$ ]]; then
    echo "curl request failed with exit $exit_code; treating as HTTP 000." >&2
    if [[ -s "$error_file" ]]; then
      sed -n '1,8p' "$error_file" >&2
    fi
    http_status="000"
  fi

  rm -f "$status_file" "$error_file"
  printf "%s" "$http_status"
}

cleanup_smoke() {
  local status=$?
  if [[ "$CLEANUP_AUTH_USER" == "1" && -n "${ID_TOKEN:-}" ]]; then
    local delete_body="$TMP_DIR/delete-auth.json"
    local delete_http
    delete_http="$(
      curl_http "$delete_body" \
        --request POST \
        --header "Content-Type: application/json" \
        --data "{\"idToken\":\"$ID_TOKEN\"}" \
        "https://identitytoolkit.googleapis.com/v1/accounts:delete?key=$API_KEY"
    )" || delete_http="000"

    if [[ "$delete_http" == "200" ]]; then
      echo "smoke auth user cleanup: ok"
    else
      echo "smoke auth user cleanup: HTTP $delete_http" >&2
    fi
  fi

  rm -rf "$TMP_DIR"
  exit "$status"
}
trap cleanup_smoke EXIT

generate_tone_pcm() {
  node - "$1" "$VOICE_ASR_SAMPLE_RATE" <<'NODE'
const fs = require("fs");
const output = process.argv[2];
const sampleRate = Number(process.argv[3] || 16000);
const seconds = 1.2;
const samples = Math.floor(sampleRate * seconds);
const buffer = Buffer.alloc(samples * 2);
for (let i = 0; i < samples; i += 1) {
  const envelope = Math.min(1, i / (sampleRate * 0.08), (samples - i) / (sampleRate * 0.12));
  const sample = Math.round(Math.sin((2 * Math.PI * 440 * i) / sampleRate) * 8000 * Math.max(0, envelope));
  buffer.writeInt16LE(sample, i * 2);
}
fs.writeFileSync(output, buffer);
NODE
}

generate_say_pcm() {
  local output="$1"
  local aiff="$TMP_DIR/say.aiff"
  local voice="${VOICE_ASR_SAY_VOICE:-}"

  if ! command -v say >/dev/null 2>&1; then
    return 1
  fi

  if [[ -z "$voice" ]]; then
    voice="$(say -v '?' | sed -n 's/^\(.*[^[:space:]]\)[[:space:]][[:space:]]*zh_CN[[:space:]].*/\1/p' | head -n 1)"
  fi

  if [[ -n "$voice" ]]; then
    say -v "$voice" -o "$aiff" -- "$VOICE_ASR_SMOKE_TEXT"
  else
    say -o "$aiff" -- "$VOICE_ASR_SMOKE_TEXT"
  fi

  if command -v ffmpeg >/dev/null 2>&1; then
    ffmpeg -v error -y -i "$aiff" -ac 1 -ar "$VOICE_ASR_SAMPLE_RATE" -f s16le "$output"
    return 0
  fi

  if command -v afconvert >/dev/null 2>&1; then
    local wav="$TMP_DIR/say.wav"
    afconvert "$aiff" "$wav" -f WAVE -d LEI16@"$VOICE_ASR_SAMPLE_RATE" -c 1
    node - "$wav" "$output" <<'NODE'
const fs = require("fs");
const wavPath = process.argv[2];
const outPath = process.argv[3];
const wav = fs.readFileSync(wavPath);
let offset = 12;
while (offset + 8 <= wav.length) {
  const id = wav.toString("ascii", offset, offset + 4);
  const size = wav.readUInt32LE(offset + 4);
  if (id === "data") {
    fs.writeFileSync(outPath, wav.subarray(offset + 8, offset + 8 + size));
    process.exit(0);
  }
  offset += 8 + size + (size % 2);
}
throw new Error("WAV data chunk not found");
NODE
    return 0
  fi

  return 1
}

prepare_audio_payload() {
  local pcm="$TMP_DIR/audio.pcm"
  local mode="$VOICE_ASR_AUDIO_MODE"

  if [[ -n "${VOICE_ASR_PCM_FILE:-}" ]]; then
    cp "$VOICE_ASR_PCM_FILE" "$pcm"
  elif [[ -n "${VOICE_ASR_WAV_FILE:-}" ]]; then
    if ! command -v ffmpeg >/dev/null 2>&1; then
      echo "VOICE_ASR_WAV_FILE requires ffmpeg to convert to PCM16." >&2
      exit 2
    fi
    ffmpeg -v error -y -i "$VOICE_ASR_WAV_FILE" -ac 1 -ar "$VOICE_ASR_SAMPLE_RATE" -f s16le "$pcm"
  elif [[ "$mode" == "tone" ]]; then
    generate_tone_pcm "$pcm"
  else
    if ! generate_say_pcm "$pcm"; then
      echo "Could not generate speech audio with say; falling back to tone smoke audio." >&2
      generate_tone_pcm "$pcm"
    fi
  fi

  node - "$pcm" "$VOICE_ASR_SAMPLE_RATE" "$VOICE_ASR_SOURCE" >"$TMP_DIR/asr-request.json" <<'NODE'
const fs = require("fs");
const pcmPath = process.argv[2];
const sampleRate = Number(process.argv[3] || 16000);
const source = process.argv[4] || "smoke_voice_asr";
const pcm = fs.readFileSync(pcmPath);
process.stdout.write(JSON.stringify({
  audioBase64: pcm.toString("base64"),
  sampleRate,
  format: "pcm_s16le",
  source,
  requestId: `smoke-${Date.now()}`,
}));
NODE
}

AUTH_BODY="$TMP_DIR/auth.json"
AUTH_HTTP="$(
  curl_http "$AUTH_BODY" \
    --request POST \
    --header "Content-Type: application/json" \
    --data '{"returnSecureToken":true}' \
    "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=$API_KEY"
)"

if [[ "$AUTH_HTTP" != "200" ]]; then
  if grep -q "ADMIN_ONLY_OPERATION" "$AUTH_BODY"; then
    TEST_EMAIL="moonly-voice-asr-smoke-$(date +%s)-$RANDOM@example.invalid"
    TEST_PASSWORD="MoonlyVoiceSmoke${RANDOM}!"
    echo "anonymous auth is disabled; creating temporary email/password smoke user"
    AUTH_HTTP="$(
      curl_http "$AUTH_BODY" \
        --request POST \
        --header "Content-Type: application/json" \
        --data "{\"email\":\"$TEST_EMAIL\",\"password\":\"$TEST_PASSWORD\",\"returnSecureToken\":true}" \
        "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=$API_KEY"
    )"
  fi

  if [[ "$AUTH_HTTP" != "200" ]]; then
    echo "Firebase smoke sign-in failed with HTTP $AUTH_HTTP:" >&2
    sed -n '1,20p' "$AUTH_BODY" >&2
    exit 3
  fi
fi

ID_TOKEN="$(node -e 'const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8")); process.stdout.write(j.idToken || "");' "$AUTH_BODY")"
LOCAL_ID="$(node -e 'const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8")); process.stdout.write(j.localId || "");' "$AUTH_BODY")"

if [[ -z "$ID_TOKEN" ]]; then
  echo "Firebase smoke sign-in did not return idToken." >&2
  exit 3
fi

echo "smoke uid: $LOCAL_ID"
prepare_audio_payload

ASR_BODY="$TMP_DIR/asr-response.json"
ASR_HTTP="$(
  curl_http "$ASR_BODY" \
    --request POST \
    --header "Content-Type: application/json" \
    --header "Authorization: Bearer $ID_TOKEN" \
    --data-binary "@$TMP_DIR/asr-request.json" \
    "$BASE_URL/voiceAsrTranscribe"
)"

node - "$ASR_HTTP" "$ASR_BODY" "$VOICE_ASR_REQUIRE_LIVE" "$VOICE_ASR_EXPECT_TEXT" <<'NODE'
const fs = require("fs");
const httpStatus = process.argv[2];
const bodyPath = process.argv[3];
const requireLive = process.argv[4] === "1";
const expected = String(process.argv[5] || "").trim();
const raw = fs.readFileSync(bodyPath, "utf8");
let data;
try {
  data = JSON.parse(raw);
} catch (error) {
  console.error(`voiceAsrTranscribe returned invalid JSON with HTTP ${httpStatus}: ${raw.slice(0, 500)}`);
  process.exit(10);
}

function fail(message) {
  console.error(`voiceAsrTranscribe smoke failed: ${message}`);
  console.error(JSON.stringify(data, null, 2).slice(0, 1200));
  process.exit(10);
}

if (httpStatus === "200") {
  const text = String(data.text || "").trim();
  if (requireLive && !text) {
    fail(`expected a non-empty transcript, got ${data.reason || "empty text"}`);
  }
  if (expected) {
    const compactText = text.replace(/\s/g, "");
    const compactExpected = expected.replace(/\s/g, "");
    if (!compactText.includes(compactExpected)) {
      fail(`expected transcript to include "${expected}", got "${text}"`);
    }
  }
  console.log(`voiceAsrTranscribe: HTTP 200 ok`);
  console.log(`text: ${text || "<empty>"}`);
  if (data.audioBytes) console.log(`audioBytes: ${data.audioBytes}`);
  if (data.rms !== undefined) console.log(`rms: ${data.rms}`);
  process.exit(0);
}

if (!requireLive && httpStatus === "500" && data.code === "voice-asr-missing-secret") {
  console.log("voiceAsrTranscribe: backend reachable; ASR secrets are missing");
  console.log(JSON.stringify(data, null, 2));
  process.exit(0);
}

if (httpStatus === "429") {
  fail("unexpected quota exhaustion for a temporary smoke user");
}

fail(`unexpected HTTP ${httpStatus}`);
NODE
