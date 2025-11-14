// Example job entry (scheduled by your orchestrator)
// Update `webstir.module.jobs` in package.json to point to this job with a schedule, e.g.:
// { "name": "nightly", "schedule": "0 0 * * *", "description": "Nightly maintenance" }

export async function run(): Promise<void> {
  // Do some nightly maintenance work here
  console.info('[job:nightly] ran at', new Date().toISOString());
}

// Execute when launched directly: `node build/backend/jobs/nightly/index.js`
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
