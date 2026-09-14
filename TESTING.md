# Validation and compatibility

## Verified locally for v1.2.0

- Compiles on Windows using the .NET Framework x64 compiler without package installation.
- Reverse dragging and clipping coordinates produce the expected PNG dimensions and pixels.
- Selection cancellation returns Cancel; reselect clears selection; the Complete button returns OK.
- The clipboard-only preference persists and can be switched back to automatic paste.
- A separate installation directory containing Chinese characters and spaces launches successfully and can locate the current desktop composer.
- The earlier attachment integration check inserted a synthetic image into the current Codex composer and removed that test attachment. This confirms the local app version's clipboard route, not every future desktop version.
- The welcome screen and selection UI are rendered to PNG and visually reviewed. Documentation screenshots contain only synthetic content.
- Plugin manifest and skill frontmatter pass their validators.

The project includes Windows CI for compilation, selection tests, and packaging. A passing local test does not imply that the remote CI run has already passed; check the repository's Actions page for its current status.

## Added checks in v1.2.0

- Real Windows hotkey registration detects a competing registration, preserves the old binding and saved preference, and releases the old combination after a successful change.
- Invalid shortcut files fall back to the default; custom choices and scissors visibility persist in an isolated test directory.
- Floating button placement stays outside the editor with negative monitor origins and narrow windows.
- The scissors button was located next to the actual current desktop composer. Clicking it opened the capture overlay, hid the scissors, and Escape cancelled without saving an image.
- Shortcut settings and welcome UI are rendered and inspected. Public button illustrations use synthetic input content.

## Visual refresh in v1.2.1

- The upright thin gray scissors and pale background were compiled, rendered, and visually compared with the provided reference.
- No capture, positioning, or shortcut behavior was changed in this patch.

## Run the checks

From the repository root in Windows PowerShell:

```powershell
./plugins/snip-to-codex/scripts/build.ps1
$exe=Join-Path $PWD 'plugins/snip-to-codex/bin/SnipToCodex.exe'
$results=Join-Path $PWD 'test-results'
$p=Start-Process $exe -ArgumentList @('--self-test', ('"'+$results+'"')) -WindowStyle Hidden -PassThru -Wait
if($p.ExitCode -ne 0){throw 'Self-test failed'}
Get-Content (Join-Path $results 'self-test.txt')
./scripts/package.ps1
```

UI previews: `SnipToCodex.exe --render-ui <output-directory>`. This draws the actual forms on synthetic data; it does not capture a user's desktop. `--probe <output-file>` checks whether one supported desktop composer can currently be located.

## Before claiming wider support

- Test the release ZIP on a clean Windows 10/11 x64 account without developer tools.
- Manually check mixed-DPI monitors, negative monitor origins, remote desktop, and clipboard contention.
- Confirm image attachment behavior on each supported Codex / ChatGPT desktop release.
- Windows ARM, macOS, and Linux are not verified or offered as native builds.
- The current UI is Chinese. The onboarding, guide, installer and shortcuts should be checked with screen readers before claiming full accessibility support.
- Binaries are currently unsigned. Hash files verify release integrity against the published value; they do not replace a trusted distribution source or signing certificate.
