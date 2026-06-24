#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

cd "$ROOT_DIR"

node <<'NODE'
const fs = require("fs");

const EXPECTED = {
  proMonthly: {
    productId: process.env.IAP_MONTHLY_PRODUCT_ID || "fari.pro.monthly",
    type: "subscription",
    store: "app_store_google_play",
  },
  proYearly: {
    productId: process.env.IAP_YEARLY_PRODUCT_ID || "fari.pro.yearly",
    type: "subscription",
    store: "app_store_google_play",
  },
};

let failures = 0;

function ok(message) {
  console.log(`[OK] ${message}`);
}

function fail(message) {
  failures += 1;
  console.log(`[FAIL] ${message}`);
}

function read(path) {
  return fs.readFileSync(path, "utf8");
}

function assertEqual(label, actual, expected) {
  if (actual === expected) ok(`${label}: ${actual}`);
  else fail(`${label}: expected ${expected}, got ${actual || "(empty)"}`);
}

function extractCsDefault(text, factoryName, fieldName) {
  const factoryIndex = text.indexOf(`static IapProductConfig ${factoryName}`);
  if (factoryIndex < 0) return "";
  const nextFactoryIndex = text.indexOf("static IapProductConfig", factoryIndex + 1);
  const block = text.slice(factoryIndex, nextFactoryIndex > factoryIndex ? nextFactoryIndex : text.length);
  const match = block.match(new RegExp(`${fieldName}\\s*=\\s*"([^"]*)"`));
  return match ? match[1] : "";
}

function extractJsDefault(text, key, fieldName) {
  const keyIndex = text.indexOf(`${key}: Object.freeze({`);
  if (keyIndex < 0) return "";
  const nextKeyIndex = text.indexOf("}),", keyIndex + 1);
  const block = text.slice(keyIndex, nextKeyIndex > keyIndex ? nextKeyIndex : text.length);
  const match = block.match(new RegExp(`${fieldName}:\\s*"([^"]*)"`));
  return match ? match[1] : "";
}

console.log("Moonly IAP product config check");
console.log(`Expected monthly: ${EXPECTED.proMonthly.productId}`);
console.log(`Expected yearly: ${EXPECTED.proYearly.productId}`);
console.log();

const publicConfig = JSON.parse(read("functions/public-config.example.json"));
for (const [key, expected] of Object.entries(EXPECTED)) {
  const product = publicConfig.iapProducts && publicConfig.iapProducts[key];
  if (!product) {
    fail(`functions/public-config.example.json missing iapProducts.${key}`);
    continue;
  }
  assertEqual(`public-config ${key}.productId`, product.productId, expected.productId);
  assertEqual(`public-config ${key}.type`, product.type, expected.type);
  assertEqual(`public-config ${key}.store`, product.store, expected.store);
}

const functionsIndex = read("functions/index.js");
assertEqual("functions DEFAULT proMonthly.productId", extractJsDefault(functionsIndex, "proMonthly", "productId"), EXPECTED.proMonthly.productId);
assertEqual("functions DEFAULT proYearly.productId", extractJsDefault(functionsIndex, "proYearly", "productId"), EXPECTED.proYearly.productId);
assertEqual("functions DEFAULT proMonthly.type", extractJsDefault(functionsIndex, "proMonthly", "type"), EXPECTED.proMonthly.type);
assertEqual("functions DEFAULT proYearly.type", extractJsDefault(functionsIndex, "proYearly", "type"), EXPECTED.proYearly.type);

const firestoreManager = read("Assets/Scripts/Platform/FireBase/FirestoreManager.cs");
assertEqual("client MonthlyDefault.productId", extractCsDefault(firestoreManager, "MonthlyDefault", "productId"), EXPECTED.proMonthly.productId);
assertEqual("client YearlyDefault.productId", extractCsDefault(firestoreManager, "YearlyDefault", "productId"), EXPECTED.proYearly.productId);
assertEqual("client MonthlyDefault.type", extractCsDefault(firestoreManager, "MonthlyDefault", "type"), EXPECTED.proMonthly.type);
assertEqual("client YearlyDefault.type", extractCsDefault(firestoreManager, "YearlyDefault", "type"), EXPECTED.proYearly.type);

const smokeIap = read("scripts/smoke-submit-iap-receipt.sh");
if (smokeIap.includes(`IAP_PRODUCT_ID="\${IAP_PRODUCT_ID:-${EXPECTED.proMonthly.productId}}"`)) {
  ok("submitIapReceipt smoke default uses monthly product id");
} else {
  fail("submitIapReceipt smoke default product id does not match monthly product id");
}

const deployIap = read("scripts/deploy-iap-functions.sh");
if (deployIap.includes(`IAP_PRODUCT_ID=${EXPECTED.proMonthly.productId}`)) {
  ok("deploy-iap-functions real receipt example uses monthly product id");
} else {
  fail("deploy-iap-functions real receipt example product id does not match monthly product id");
}

const publicConfigSetter = read("functions/scripts/set-public-config.js");
if (publicConfigSetter.includes('collection("app_config").doc("public").set')) {
  ok("public config setter writes app_config/public");
} else {
  fail("public config setter does not write app_config/public");
}

const unlockPro = read("Assets/Scripts/UI/UnlockProUI.cs");
for (const needle of ["LoadPublicAppConfig", "ConfigureProducts", "OnRuntimeMonthlyPurchaseBtnButtonClick", "OnRuntimeYearlyPurchaseBtnButtonClick"]) {
  if (unlockPro.includes(needle)) ok(`UnlockProUI contains ${needle}`);
  else fail(`UnlockProUI missing ${needle}`);
}

const iapBridge = read("Assets/Scripts/Platform/IAP/UnityIapPurchaseBridge.cs");
for (const needle of ["ProductType.Subscription", "IapProductConfig.MonthlyDefault", "IapProductConfig.YearlyDefault", "SubmitPurchaseReceipt"]) {
  if (iapBridge.includes(needle)) ok(`Unity IAP bridge contains ${needle}`);
  else fail(`Unity IAP bridge missing ${needle}`);
}

const iapManager = read("Assets/Scripts/Platform/IAP/IapPurchaseManager.cs");
if (iapManager.includes("UnityEngine.Purchasing.StandardPurchasingModule, UnityEngine.Purchasing.Stores")) {
  ok("IapPurchaseManager detects Unity IAP 4.x Stores assembly");
} else {
  fail("IapPurchaseManager missing Unity IAP 4.x Stores assembly detection");
}

console.log();
console.log(`Summary: ${failures} failure(s)`);
process.exit(failures > 0 ? 1 : 0);
NODE
