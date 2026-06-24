#!/usr/bin/env bash
set -u

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
EXPECTED_FIREBASE_UNITY_VERSION="${EXPECTED_FIREBASE_UNITY_VERSION:-13.13.0}"
FAILURES=0
WARNINGS=0

ok() {
  echo "[OK] $*"
}

warn() {
  echo "[WARN] $*"
  WARNINGS=$((WARNINGS + 1))
}

fail() {
  echo "[FAIL] $*"
  FAILURES=$((FAILURES + 1))
}

grep_repo() {
  local pattern="$1"
  shift

  if command -v rg >/dev/null 2>&1; then
    rg -n "$pattern" "$@" 2>/dev/null
  else
    grep -R -n -E "$pattern" "$@" 2>/dev/null
  fi
}

contains_text() {
  local pattern="$1"
  local file="$2"
  grep -q -- "$pattern" "$ROOT_DIR/$file"
}

cd "$ROOT_DIR" || exit 1

echo "Moonly Firebase SDK version check"
echo "Expected Firebase Unity SDK: $EXPECTED_FIREBASE_UNITY_VERSION"
echo

if grep_repo "gvh_version-13\\.12\\.0|13\\.12\\.0|firebase-(app|auth|database|firestore|messaging)-unity/13\\.12\\.0" \
  Assets/Firebase \
  Assets/Plugins \
  Assets/GeneratedLocalRepo/Firebase/m2repository \
  ProjectSettings \
  Packages >/tmp/moonly_firebase_sdk_old_versions.log; then
  fail "old Firebase Unity SDK 13.12.0 artifacts remain; see /tmp/moonly_firebase_sdk_old_versions.log"
else
  ok "no Firebase Unity SDK 13.12.0 artifacts detected in active SDK/plugin paths"
fi

if grep_repo "FirebaseCpp(Storage|RemoteConfig|Crashlytics)|FirebaseCrashlytics" \
  Assets/Firebase/Plugins \
  Assets/Plugins >/tmp/moonly_firebase_sdk_unused_modules.log; then
  warn "unused Firebase Storage / RemoteConfig / Crashlytics plugin artifacts are present; see /tmp/moonly_firebase_sdk_unused_modules.log"
else
  ok "unused Firebase Storage / RemoteConfig / Crashlytics plugin artifacts are absent"
fi

for package in firebase-app-unity firebase-auth-unity firebase-database-unity firebase-firestore-unity firebase-messaging-unity; do
  if contains_text "com.google.firebase:$package:$EXPECTED_FIREBASE_UNITY_VERSION" "ProjectSettings/AndroidResolverDependencies.xml"; then
    ok "AndroidResolverDependencies.xml uses $package@$EXPECTED_FIREBASE_UNITY_VERSION"
  else
    fail "AndroidResolverDependencies.xml missing $package@$EXPECTED_FIREBASE_UNITY_VERSION"
  fi

  if [[ -f Assets/Plugins/Android/mainTemplate.gradle ]]; then
    if contains_text "com.google.firebase:$package:$EXPECTED_FIREBASE_UNITY_VERSION" "Assets/Plugins/Android/mainTemplate.gradle"; then
      ok "mainTemplate.gradle uses $package@$EXPECTED_FIREBASE_UNITY_VERSION"
    else
      fail "mainTemplate.gradle missing $package@$EXPECTED_FIREBASE_UNITY_VERSION"
    fi
  else
    warn "Assets/Plugins/Android/mainTemplate.gradle missing; Android resolver output cannot be checked"
  fi
done

for path in \
  Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/$EXPECTED_FIREBASE_UNITY_VERSION \
  Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/$EXPECTED_FIREBASE_UNITY_VERSION \
  Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-database-unity/$EXPECTED_FIREBASE_UNITY_VERSION \
  Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/$EXPECTED_FIREBASE_UNITY_VERSION \
  Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-messaging-unity/$EXPECTED_FIREBASE_UNITY_VERSION; do
  if [[ -d "$path" ]]; then
    ok "$path exists"
  else
    fail "$path missing"
  fi
done

echo
echo "Summary: $FAILURES failure(s), $WARNINGS warning(s)"
if [[ "$FAILURES" -gt 0 ]]; then
  exit 1
fi

exit 0
