# Odyssey for AmigaOS 4.1 Update 3 — CI package

This directory documents the reproducible GitHub Actions package job for the native AmigaOS 4 / PowerPC Odyssey release.

## Get the ZIP

Open the repository's **Actions** tab, select **Package Odyssey 3.1.0 for AmigaOS 4**, then open a successful run and download the `Odyssey-OS4-3.1.0` artifact. The artifact contains `Odyssey-OS4-3.1.0.zip`.

The workflow downloads the upstream `Odyssey_310.lha`, verifies its pinned SHA-256 before extraction, checks the extracted files for a PowerPC executable, and zips the complete application tree. It does not silently pass through a failed checksum or a missing PowerPC binary.

## Install

1. Download the artifact ZIP on a PC and extract `Odyssey-OS4-3.1.0.zip`.
2. Copy the complete extracted Odyssey directory and all its supporting files to the AmigaOS 4.1 Update 3 system. Preserve the folder structure.
3. Launch the included Odyssey executable from the copied directory.

Do not copy just the executable: browsers of this age commonly depend on their accompanying libraries, resources, and configuration files.

## Important build status

This pipeline packages the **upstream native Odyssey 3.1.0 binary release** for AmigaOS 4. It does **not** compile a new WebKit engine from source, and it does not imply that the experimental feature-profile changes in the separate source kit have been applied to the binary. The current upstream browser still has modern-web compatibility limits; this is not a claim of current-Chromium/Firefox feature parity.

Upstream source/release: https://github.com/AmigaLabs/OdysseyWebBrowser/releases/tag/ODYSSEY_3_1_0
