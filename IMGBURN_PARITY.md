# LuxBurn ImgBurn parity notes

These notes summarize recurring engineering lessons from the ImgBurn update log and translate them into LuxBurn implementation targets.

## Burn/write reliability

- Preserve the original drive/IMAPI error and HRESULT in the log before trying fallbacks.
- Block write-once media when the current writer cannot prove capacity/write support before the laser is armed.
- Implement native MMC/DAO CD-R image burning; the current IMAPI compatibility path can accept CD-R during preflight and still fail with `0xC0AA0402` after marking the disc.
- Log recorder vendor, product, firmware revision, media type, media status, blank state, and free/total sectors before writing.
- Wait for media readiness long enough for slow drives and media changers instead of failing after a short fixed delay.
- Block unsupported drive/media combinations before starting a write.
- Check image sectors against reported free sectors and show a clear oversize/overburn decision.
- Add configurable write transfer size and buffering options.
- Add test mode/simulation when the drive and media support it.
- Add write-speed discovery and selection.

## Drive quirks

- Detect and log USB connection speed and controller/driver family information where the OS exposes it.
- Detect LG/Pioneer/Lite-On specific firmware quirks and avoid treating every drive as generic.
- Avoid losing errors when using immediate vs non-immediate operations.
- Add small post-command pauses where specific drive families need time to settle.

## Media intelligence

- Show manufacturer/media ID information when available.
- Treat blank, appendable, finalized, damaged, write-protected, and unsupported media differently in user messages.
- Detect and explain underburn/overburn cases instead of failing late.
- Warn when an image/content type is mismatched to the inserted media.

## Build/read/verify quality

- Keep volume-label handling explicit and validate older filesystem restrictions.
- Add bootable-disc options such as platform ID and boot information table checks.
- Improve verify/read diagnostics by logging the first block error before retrying one sector at a time.
- Export logs/checksums consistently and keep enough detail to reproduce drive failures.

## UI behavior

- Keep dialogs from being triggered by stale MRU drive paths.
- Make log entries useful as primary troubleshooting artifacts.
- Ensure all controls remain visible at higher DPI and small window sizes.
