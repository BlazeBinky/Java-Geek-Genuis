# FitTrack for Windows 10 Mobile

Offline-first fitness tracking app focused on Lumia / Windows 10 Mobile ARM devices.

## Current features
- Daily step count from the Windows pedometer API when a compatible sensor is available, plus manual step entry.
- Step goal, estimated distance from stride length, and estimated activity calories.
- Run, walk and cycle logs by distance and duration.
- Manual food, meal and calorie tracking.
- Water-glass counter, saved weight and configurable daily goals.
- Local history and CSV export.
- Records stored locally on the phone; no account or cloud API required.

## Build and install
GitHub Actions workflow: [Build FitTrack for Lumia ARM](../actions/workflows/fitness-tracker.yml).

The workflow builds an ARM AppX package and creates a development signing certificate. Download the `FitTrack-Lumia-ARM.appx` and `FitTrackDev.cer` release assets from the [FitTrack Lumia release](../releases/tag/fittrack-lumia) after the workflow succeeds.

Install the certificate on the phone before sideloading the AppX. This is a development-signed package, not a Microsoft Store release.

## Notes
Pedometer availability and background sensor behavior vary by device and firmware. Distance and calories are estimates, not medical measurements. Food calories are entered manually; there is no barcode scanning or automatic nutrition database in this version.
