// Example function entry (invoked by your runtime)
// This is a simple placeholder; wire it into your job/queue system as needed.

export async function run(): Promise<void> {
  // Do some background work here
  // e.g., send an email, process a small batch, etc.
  console.info('[function:hello] ran at', new Date().toISOString());
}

// Execute when launched directly: `node build/backend/functions/hello/index.js`
const isMain = (() => {
  try {
    const argv1 = process.argv?.[1];
    if (!argv1) return false;
    const here = new URL(import.meta.url);
    const run = new URL(`file://${argv1}`);
    return here.pathname === run.pathname;
  } catch {
    return false;
  }
})();

if (isMain) {
  run().catch((err) => {
    console.error(err);
    process.exitCode = 1;
  });
}

