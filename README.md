<img width="512" height="512" alt="forma_companion_app_512 - Copy" src="https://github.com/user-attachments/assets/d4890612-4a3d-4c39-bcd9-5b74fa2e499d" />

# Forma Companion

Forma Companion is a portable Windows utility for rebuilding your software setup after a format or clean install.

It helps you:

- scan installed software on a machine
- build a curated restore list
- save that list as a profile
- reload the profile later
- reinstall supported apps through WinGet
- keep track of installed, failed, and manual-only items

---

<img width="1459" height="924" alt="UI - Copy" src="https://github.com/user-attachments/assets/11706b72-5305-4c2e-b664-c260b1dfb278" />


---

## Main idea

The app is designed for a simple workflow:

1. Run it on your current system
2. Scan your installed software
3. Move the apps you care about into **Programs to install**
4. Save the profile to a storage folder, such as a USB drive
5. After formatting Windows, run the app again
6. Select the same storage folder
7. Load the profile
8. Reinstall the saved apps

---

## Portable usage

The app is intended to run in a portable way.

You only need:

- the executable
- access to a **storage folder** on a USB drive or another disk

The storage folder is used automatically for:

- `profile.json`
- session logs inside `logs\`

Example:

```text
E:\FormatCompanionData\
E:\FormatCompanionData\profile.json
E:\FormatCompanionData\logs\
