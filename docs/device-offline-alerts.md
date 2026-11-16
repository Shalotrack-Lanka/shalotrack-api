# Device offline alerts

`DeviceOfflineAlertWorker` (hosted service) creates an `AlertType.DeviceOffline` alert and sends a push to
the owner and the accepted viewers when a tracker on an active vehicle has sent nothing for
`DeviceOffline:ThresholdMinutes` (default 15). The Android Alerts screen already renders this type.

## How it decides

* Every 60 s one query joins `DeviceStatuses` (LastSeen) with the active `DeviceAssignments` and
  `Vehicles` (active, not demo) and keeps devices with `LastSeen` older than the threshold.
* **Once per outage, no new table.** A device is alerted only if no `DeviceOffline` alert exists with
  `TriggeredAt >= LastSeen`. When the device reports again, `LastSeen` moves past that alert and the next
  outage alerts again. Uses `IX_Alerts_DeviceId_AlertType_TriggeredAt`.
* The alert row is saved **before** the push, so a failed push is never retried into a duplicate.
* Devices silent longer than `MaxAgeHours` (24) are ignored, so switching this on never reports old outages.
* At most `MaxAlertsPerSweep` (100) alerts per sweep.
* **Mass-outage guard:** if at least `MassOutageMin` (20) devices are candidates *and* more than half of
  all recently active devices are, the sweep is skipped and an error is logged. That pattern is a
  gateway/database outage, not broken trackers. Below 20 candidates it does not apply, so a small fleet
  never trips it.

## Configuration (`appsettings.json`, section `DeviceOffline`)

| Key | Default | Meaning |
|---|---|---|
| `DryRun` | `true` | log what would be sent; save and send nothing |
| `ThresholdMinutes` | 15 (min 5) | silence that counts as offline |
| `IntervalSeconds` | 60 (min 30) | sweep period |
| `MaxAgeHours` | 24 | silent longer than this is never alerted |
| `MaxAlertsPerSweep` | 100 | burst limit |
| `MassOutageMin` | 20 | guard, see above |

Set via environment on the instance, e.g. `DeviceOffline__DryRun=false`.

## Rollout

1. Deploy with `DryRun=true` (default). After the 60 s start-up delay the log shows
   `DRY RUN -- would alert customer ...` (first 10 per sweep). Check the list is plausible
   (devices that really are off, none that are online).
2. Set `DeviceOffline__DryRun=false`, restart.
3. Test: switch off one tracker, wait the threshold + up to 60 s, expect one alert and one push; power it
   on and off again later for a second one.

## Known limits

* **Sleeping trackers.** A battery tracker (e.g. LK208) that reports less often than the threshold will be
  reported offline between reports. Raise `ThresholdMinutes`, or keep them off this feature until a
  per-device interval exists.
* GT06 refreshes `LastSeen` from status/heartbeat packets, not from location packets; a unit whose
  heartbeat interval exceeds the threshold would be flagged.
* Assumes one API instance (like the other workers).
* The Android "device offline" toggle in Settings is local-only and is not consulted by the push.
* No restore ("back online") alert yet.
