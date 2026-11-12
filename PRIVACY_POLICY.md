# Privacy Policy

**Last Updated: October 7, 2026**

## Overview

KaiROS AI ("the App") is a local AI assistant that runs entirely on your device. We are committed to protecting your privacy.

## Data Collection

### We Do NOT Collect

- ❌ Personal information
- ❌ Chat conversations
- ❌ Usage analytics
- ❌ Device identifiers
- ❌ Location data
- ❌ Any telemetry, other than the automatic crash reporting described below

### Data Stored Locally on Your Device

- ✅ Downloaded AI models (stored in app directory)
- ✅ Chat history (stored locally in SQLite database)
- ✅ App settings and preferences

## How the App Works

1. **100% Local Processing** - All AI inference runs on your device
2. **No Cloud Connection** - No chat data is sent to external servers (see Crash Reporting below for the one exception)
3. **No Account Required** - No sign-up or login needed
4. **Offline Capable** - Works without internet (after model download)

## Crash Reporting

The Windows app uses [Sentry](https://sentry.io) to automatically report unhandled crashes and errors so we can fix bugs faster.

- **What's sent**: exception type/message, stack trace, app version, OS version, and device type
- **What's never sent**: chat conversations, prompts, model output, file contents, or any other personal data
- Crash reports are not linked to your identity and contain no account or device identifiers
- A local copy of the same crash details is always kept at `%LOCALAPPDATA%\KaiROS.AI\crash.log` on your device

## Model Downloads

When you download AI models:

- Models are downloaded from Hugging Face (huggingface.co)
- Only the model file is downloaded
- No personal data is transmitted
- Downloads can be paused and resumed

## Mobile App Permissions (Android)

To provide full functionality, the Android app requires the following permissions:

- **Microphone (`RECORD_AUDIO`)**: Used *only* when you tap the microphone button to speak to the AI. Audio is processed locally and never uploaded.
- **Network (`INTERNET`)**: Used *only* to download AI models from Hugging Face.
- **Storage**: Used to save downloaded models and chat history on your device.

## Data Storage Locations

| Platform | Data | Location |
|:---|:---|:---|
| **Windows** | All Data | `%LOCALAPPDATA%\KaiROS.AI\` |
| **Android** | Models & DB | App Internal Storage (Sandbox) |

## Your Control

You have full control over your data:

- **Delete chat history** - Use "Clear" button in Chat
- **Delete models** - Use "Delete" button in Models tab
- **Uninstall** - Removes all app data (optional)

## Third-Party Services

The App does not integrate with any third-party analytics, advertising, or tracking services. The only third-party service used is Sentry, solely for crash reporting as described above.

## Children's Privacy

The App does not knowingly collect information from children under 13.

## Changes to This Policy

We may update this Privacy Policy. Changes will be posted in the app repository.

## Contact

For privacy questions, please open an issue on our GitHub repository:
<https://github.com/avikeid2007/KaiROS-AI>

---

**Summary: KaiROS AI is a privacy-first application. All your data stays on your device. We collect nothing.**
