/**
 * ZKTeco poller — for devices that DON'T support HTTP push.
 *
 * Uses node-zklib to connect to the device over TCP (port 4370, the ZKTeco
 * proprietary protocol) and pull the attendance log every 5 minutes.
 *
 * Enable by setting ZKTECO_BRIDGE_ENABLED=true in .env.
 * Required env vars: ZKTECO_DEVICE_IP, ZKTECO_DEVICE_PORT (default 4370)
 *
 * This job runs in the background when the server starts.
 */

require('dotenv').config();
const cron = require('node-cron');
const ZKLib = require('node-zklib');
const { processCheckin } = require('../lib/checkin');

const DEVICE_IP   = process.env.ZKTECO_DEVICE_IP;
const DEVICE_PORT = parseInt(process.env.ZKTECO_DEVICE_PORT ?? '4370', 10);
const CHAPTER     = process.env.DEFAULT_CHAPTER ?? 'PDC';

// Track the last attendance record index we processed to avoid re-processing
let lastProcessedIndex = 0;

async function pollDevice() {
  if (!DEVICE_IP) {
    console.warn('[zkteco] ZKTECO_DEVICE_IP not set — skipping poll');
    return;
  }

  const zk = new ZKLib(DEVICE_IP, DEVICE_PORT, 10000, 4000);

  try {
    await zk.createSocket();
    const { data: attendances } = await zk.getAttendances();

    if (!attendances || !attendances.length) {
      await zk.disconnect();
      return;
    }

    // Only process records we haven't seen before
    const newRecords = attendances.slice(lastProcessedIndex);
    console.log(`[zkteco] Fetched ${attendances.length} records, ${newRecords.length} new`);

    for (const record of newRecords) {
      const fingerprintId = String(record.deviceUserId);
      try {
        const result = await processCheckin({ fingerprintId, chapter: CHAPTER, source: 'fingerprint' });
        if (result.skipped) {
          console.log(`[zkteco] Skipped ${fingerprintId}: ${result.reason}`);
        } else {
          console.log(`[zkteco] Processed ${fingerprintId}: karma awarded=${result.karmaAwarded}`);
        }
      } catch (err) {
        console.error(`[zkteco] Failed to process ${fingerprintId}:`, err.message);
      }
    }

    lastProcessedIndex = attendances.length;
    await zk.disconnect();
  } catch (err) {
    console.error('[zkteco] Poll error:', err.message);
    try { await zk.disconnect(); } catch {}
  }
}

// Poll every 5 minutes
cron.schedule('*/5 * * * *', pollDevice);
console.log('[zkteco] Attendance poller started — polling every 5 min');

// Run immediately on startup
pollDevice();
