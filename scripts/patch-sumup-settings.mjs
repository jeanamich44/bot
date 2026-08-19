import pg from "pg";

const dbUrl = (process.env.DATABASE_URL || "").trim();
if (!dbUrl) {
  console.error("DATABASE_URL manquante.");
  process.exit(1);
}

const client = new pg.Client({
  connectionString: dbUrl,
  ssl: /localhost|127\.0\.0\.1/i.test(dbUrl) ? false : { rejectUnauthorized: false }
});

function asObject(value) {
  if (value && typeof value === "object" && !Array.isArray(value)) return { ...value };
  if (typeof value === "string") return JSON.parse(value);
  throw new Error("JSON settings invalide");
}

async function lire(key) {
  const { rows } = await client.query(
    'SELECT value FROM settings WHERE lower(key) = lower($1) LIMIT 1',
    [key]
  );
  if (!rows.length) throw new Error("settings." + key + " absent");
  return asObject(rows[0].value);
}

async function ecrire(key, obj) {
  await client.query(
    'UPDATE settings SET value = $1::jsonb WHERE lower(key) = lower($2)',
    [JSON.stringify(obj), key]
  );
}

await client.connect();
try {
  for (const bank of ["sumup", "sumup_bank2"]) {
    const obj = await lire(bank);
    const email = String(obj.pay_to_email || "").trim();
    if (!email) throw new Error(bank + ".pay_to_email vide, impossible de créer name");
    if (!String(obj.name || "").trim()) {
      obj.name = email;
      await ecrire(bank, obj);
      console.log(bank + ".name copié depuis pay_to_email");
    } else {
      console.log(bank + ".name déjà en table");
    }
  }

  const general = await lire("general");
  const expArg = String(process.argv[2] || "").trim();
  const exp = String(general.sumup_expiration_minutes || "").trim();
  if (!exp) {
    if (!expArg || !/^[1-9]\d*$/.test(expArg)) {
      throw new Error("general.sumup_expiration_minutes absent — passe les minutes en argument");
    }
    general.sumup_expiration_minutes = expArg;
    await ecrire("general", general);
    console.log("general.sumup_expiration_minutes créé depuis l'argument");
  } else {
    console.log("general.sumup_expiration_minutes déjà en table");
  }
} finally {
  await client.end();
}
