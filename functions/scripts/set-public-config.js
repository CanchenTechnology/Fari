"use strict";

const fs = require("node:fs");
const path = require("node:path");
const admin = require("firebase-admin");

const DEFAULT_PROJECT_ID = "fari-app-b2fd2";
const SOCIAL_LINK_KEYS = ["instagram", "facebook", "x", "tiktok", "pinterest"];
const IAP_PRODUCT_KEYS = ["proMonthly", "proYearly"];

function parseArgs(argv) {
  const options = {
    configPath: path.join(__dirname, "..", "public-config.example.json"),
    dryRun: process.env.PUBLIC_CONFIG_DRY_RUN === "1",
    requireRealSocialLinks: process.env.REQUIRE_REAL_SOCIAL_LINKS === "1"
      || process.env.PUBLIC_CONFIG_REQUIRE_REAL_SOCIAL_LINKS === "1",
    projectId: process.env.GCLOUD_PROJECT
      || process.env.GOOGLE_CLOUD_PROJECT
      || DEFAULT_PROJECT_ID,
  };

  for (let i = 0; i < argv.length; i += 1) {
    const arg = argv[i];
    if (arg === "--dry-run") {
      options.dryRun = true;
    } else if (arg === "--require-real-social-links") {
      options.requireRealSocialLinks = true;
    } else if (arg === "--project") {
      options.projectId = argv[i + 1] || "";
      i += 1;
    } else if (arg === "--help" || arg === "-h") {
      options.help = true;
    } else if (arg.startsWith("--")) {
      throw new Error(`Unknown option: ${arg}`);
    } else {
      options.configPath = arg;
    }
  }

  return options;
}

function printUsage() {
  console.log(`Usage:
  node functions/scripts/set-public-config.js [--dry-run] [--require-real-social-links] [--project fari-app-b2fd2] [config.json]

Examples:
  node functions/scripts/set-public-config.js --dry-run
  node functions/scripts/set-public-config.js --dry-run --require-real-social-links functions/public-config.live.json
  node functions/scripts/set-public-config.js functions/public-config.example.json
`);
}

function readJson(filePath) {
  const absolutePath = path.resolve(process.cwd(), filePath);
  return JSON.parse(fs.readFileSync(absolutePath, "utf8"));
}

function isPlainObject(value) {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

function validateHttpsUrl(value, label, errors) {
  if (typeof value !== "string" || value.trim().length === 0) {
    errors.push(`${label} is required`);
    return;
  }

  try {
    const url = new URL(value);
    if (url.protocol !== "https:") {
      errors.push(`${label} must use https`);
    }
  } catch {
    errors.push(`${label} is not a valid URL`);
  }
}

function validateProductId(value, label, errors) {
  if (typeof value !== "string" || value.trim().length === 0) {
    errors.push(`${label}.productId is required`);
    return;
  }

  if (!/^[A-Za-z0-9][A-Za-z0-9._-]{2,119}$/.test(value)) {
    errors.push(`${label}.productId has invalid format: ${value}`);
  }
}

function isPlaceholderSocialLink(key, value) {
  if (typeof value !== "string") return true;
  const normalized = value.trim().replace(/\/+$/, "").toLowerCase();
  const placeholders = {
    instagram: "https://www.instagram.com",
    facebook: "https://www.facebook.com",
    x: "https://x.com",
    tiktok: "https://www.tiktok.com",
    pinterest: "https://www.pinterest.com",
  };

  return normalized === placeholders[key];
}

function validatePublicConfig(config, options = {}) {
  const errors = [];
  if (!isPlainObject(config)) {
    errors.push("config must be a JSON object");
    return errors;
  }

  if (!isPlainObject(config.socialLinks)) {
    errors.push("socialLinks must be an object");
  } else {
    for (const key of SOCIAL_LINK_KEYS) {
      validateHttpsUrl(config.socialLinks[key], `socialLinks.${key}`, errors);
      if (options.requireRealSocialLinks && isPlaceholderSocialLink(key, config.socialLinks[key])) {
        errors.push(`socialLinks.${key} must be a real account/community URL, not the platform homepage placeholder`);
      }
    }
  }

  if (!isPlainObject(config.iapProducts)) {
    errors.push("iapProducts must be an object");
  } else {
    const productIds = [];
    for (const key of IAP_PRODUCT_KEYS) {
      const product = config.iapProducts[key];
      if (!isPlainObject(product)) {
        errors.push(`iapProducts.${key} must be an object`);
        continue;
      }

      validateProductId(product.productId, `iapProducts.${key}`, errors);
      if (product.productId) productIds.push(product.productId);

      if (product.type !== "subscription") {
        errors.push(`iapProducts.${key}.type must be subscription`);
      }

      if (typeof product.store !== "string" || product.store.trim().length === 0) {
        errors.push(`iapProducts.${key}.store is required`);
      }

      if (typeof product.displayName !== "string" || product.displayName.trim().length === 0) {
        errors.push(`iapProducts.${key}.displayName is required`);
      }

      if (product.priceLabel !== undefined && typeof product.priceLabel !== "string") {
        errors.push(`iapProducts.${key}.priceLabel must be a string`);
      }
    }

    if (new Set(productIds).size !== productIds.length) {
      errors.push("iapProducts productId values must be unique");
    }
  }

  return errors;
}

async function main() {
  const options = parseArgs(process.argv.slice(2));
  if (options.help) {
    printUsage();
    return;
  }

  const config = readJson(options.configPath);
  const errors = validatePublicConfig(config, options);
  if (errors.length > 0) {
    throw new Error(`Invalid public config:\n- ${errors.join("\n- ")}`);
  }

  console.log(`Validated public config: ${options.configPath}`);
  if (options.dryRun) {
    console.log("Dry run only; app_config/public was not updated.");
    return;
  }

  if (!admin.apps.length) {
    admin.initializeApp({ projectId: options.projectId });
  }

  await admin.firestore().collection("app_config").doc("public").set({
    ...config,
    updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    updatedBy: "local-script",
  }, { merge: true });

  console.log(`Updated app_config/public in ${options.projectId} from ${options.configPath}`);
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
