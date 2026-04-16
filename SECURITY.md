# Security Policy

## Supported Versions

Forma Companion is currently an early public alpha.

Security fixes, if needed, will be applied to the latest code on the `main` branch and the most recent public release.

| Version | Supported |
| ------- | --------- |
| Latest `main` | Yes |
| Latest release | Yes |
| Older releases | No |

---

## Reporting a Vulnerability

If you discover a security issue, please do **not** open a public GitHub issue with full exploit details.

Instead, report it privately by contacting the maintainer through GitHub.

Please include:

- a clear description of the issue
- steps to reproduce
- affected version or commit
- screenshots or logs if relevant
- impact assessment, if known

I will try to acknowledge valid reports in a reasonable timeframe and work toward a fix.

---

## Security Scope

Forma Companion is a local Windows desktop utility intended to help users rebuild software setups after a clean install or format.

Its main functions include:

- scanning installed software
- building a restore profile
- loading a saved profile
- installing supported applications through WinGet

Because of that design, the main trust boundary is the **storage folder** and the `profile.json` file loaded from it.

### Important trust model

Only use storage folders and profile files that you trust.

If a malicious or tampered `profile.json` is loaded, the app may attempt to install attacker-chosen WinGet packages. This is currently the most important security consideration in the project.

---

## Current Security Notes

At this stage, the project has the following characteristics:

- local desktop utility, not a network service
- no intended remote administration features
- installation is performed through WinGet
- logs are stored locally in the selected storage folder
- the app is intended for Windows only

Current limitations include:

- profiles are not signed
- profiles are not hashed or integrity-checked
- loading a profile currently assumes the storage source is trusted
- release binaries may not yet be code-signed

---

## Recommended Safe Usage

Users should:

- only load profiles from trusted USB drives or folders
- review the install queue before starting installation
- manually verify unexpected or unfamiliar WinGet package IDs
- avoid using shared or untrusted storage media for profile transfer

---

## Planned Improvements

The following improvements are intended to strengthen the security posture over time:

- install confirmation step showing the exact install queue
- clearer warnings when loading profiles from external storage
- release checksums
- optional binary signing
- automated dependency and code scanning
- improved validation of loaded profile data

---

## Out of Scope

The following are currently out of scope:

- defending against a fully compromised local machine
- preventing execution of software already trusted by the operating system or WinGet ecosystem
- replacing full system backup, disk imaging, or enterprise software management tools

---

## Disclosure Philosophy

Please report issues responsibly and allow time for review and remediation before public disclosure.

Thank you for helping improve the safety of the project.