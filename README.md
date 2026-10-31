# remind-me

A minimal Telegram reminder bot. Send it a message with a date and time at the end, and it will ping you back when the moment arrives.

Built as a personal project: small, understandable, easy to modify. No database, no web framework, no dependency injection — just JSON files and a console app.

---

## Features

- **Text reminders** — any text followed by `HH MM DD MM YYYY`.
- **Bare alarms** — omit the text and get a reminder with only a timestamp.
- **Approval-based access** — new users are held in `pending.json` until the admin approves them.
- **Automatic cleanup** — a reminder is removed from storage after it is delivered.
- **Past-date protection** — reminders set in the past are rejected with a clear message.
- **Commands** — `/start`, `/help`, `/privacy`, `/allow <userId>`.
- **UTC storage, local display** — times are stored in UTC, shown in the user's local zone (configurable offset).

---

## Architecture

The solution has two active projects (plus tests):

| Project | Purpose |
|---|---|
| `RemindMe.Core` | Domain logic: parsing, storage, services. No Telegram dependency. |
| `RemindMe.Bot` | Telegram bot: receives messages, handles commands and approval callbacks. **Also hosts the reminder delivery loop** via `CronRunner` (runs every minute inside the bot process). |

Three JSON files live on a mounted volume (Railway /data):

- `notes.json` — active reminders.
- `users.json` — approved user IDs.
- `pending.json` — users awaiting approval.

Project tree:

```
remind-me/
  RemindMe.slnx
  src/
    RemindMe.Core/
    RemindMe.Bot/
  tests/
    RemindMe.Tests/
  README.md
```

---

## Input format

```
<text> HH MM DD MM YYYY
```

- The date and time go at the end of the message.
- Separators between digit groups can be any non-digit characters (space, dot, dash, colon, multiple in a row).
- Year must be 4 digits.
- The note text must not end with a digit (otherwise the parser cannot tell where the text ends and the time begins).

### Examples

| Input | Result |
|---|---|
| `buy bread 17 00 21 06 2026` | Reminder "buy bread" on 2026-06-21 at 17:00 |
| `call mom 09:30 26 10 2026` | Reminder "call mom" on 2026-10-26 at 09:30 |
| `17 00 21 06 2026` | Bare alarm on 2026-06-21 at 17:00 (no text) |
| `standup 09 00 01 11 2026` | Reminder "standup" on 2026-11-01 at 09:00 |

---

## Commands

| Command | Who | What it does |
|---|---|---|
| `/start` | approved | Greeting + input format |
| `/help` | approved | Detailed format and examples |
| `/privacy` | approved | Privacy notice |
| `/allow <userId>` | admin only | Manually approve a user |

New users who write to the bot are placed in `pending.json` and the admin receives an inline keyboard with Approve / Reject buttons.

---

## Configuration

All settings are read from `appsettings.json` (and appsettings.Development.json locally) or from environment variables (Railway).

### `RemindMe.Bot/appsettings.json`

```json
{
  "Telegram": {
    "BotToken": "",
    "BootstrapAdminId": 0
  },
  "Storage": {
    "NotesFilePath": "/data/notes.json",
    "UsersFilePath": "/data/users.json",
    "PendingFilePath": "/data/pending.json"
  },
  "LocalTime": {
    "OffsetHours": 3
  }
}
```

| Key | Description |
|---|---|
| `Telegram:BotToken` | Bot token from [@BotFather](https://t.me/BotFather). |
| `Telegram:BootstrapAdminId` | Telegram user ID of the admin. Added to `users.json` on first run. |
| `Storage:NotesFilePath` | Path to `notes.json`. |
| `Storage:UsersFilePath` | Path to `users.json`. |
| `Storage:PendingFilePath` | Path to `pending.json`. |
| `LocalTime:OffsetHours` | Local time zone offset in hours. `3` for Minsk (Belarus). |

`appsettings.Development.json` is **not** committed to git — use it for local secrets and paths.

---

## Running locally

### Prerequisites

- [.NET 11 SDK](https://dotnet.microsoft.com/) (RC is fine)
- A Telegram bot token from [@BotFather](https://t.me/BotFather)
- Your Telegram user ID from [@userinfobot](https://t.me/userinfobot)

### Setup

1. Clone the repository.
2. Create `src/RemindMe.Bot/appsettings.Development.json`:

```json
      {
        "Telegram": {
          "BotToken": "YOUR_BOT_TOKEN",
          "BootstrapAdminId": YOUR_USER_ID
        },
        "Storage": {
          "NotesFilePath": "D:\\data\\notes.json",
          "UsersFilePath": "D:\\data\\users.json",
          "PendingFilePath": "D:\\data\\pending.json"
        },
        "LocalTime": {
          "OffsetHours": 3
        }
      }
```

### Run the bot

```bash
dotnet run --project src/RemindMe.Bot
```

### Run tests

```bash
dotnet test
```

---

## Deployment (Railway)

Railway runs the bot as a single long-lived service. The reminder delivery
loop (`CronRunner`) runs **inside the bot process** on a 1-minute timer,
because Railway volumes cannot be shared between services.

**Why one service instead of two:** Railway attaches a volume to exactly one
service. If the bot and the cron job were separate services, they would each
get their own isolated disk and could not share `notes.json`. Running both in
one process solves this without changing the storage layer.

The bot writes to `/data` (a mounted volume), which must be created in the
Railway dashboard and attached to the `bot` service.

Reminders are delivered with a tolerance of **up to ~1 minute**, limited by
the timer interval.

---

## Development notes

- **All user-facing strings** live in `RemindMe.Core/Messages.cs`.
- **All times are stored in UTC.** Conversion to and from local time happens at the edges (bot input, cron output) via `LocalTime`.
- **Storage is JSON on disk.** For a few hundred notes with a handful of users this is sufficient. If the bot ever grows beyond that, replace `NoteStore` with a SQLite-backed implementation — the rest of the code does not need to change.
- **No DI, no logging framework, no web server.** Console output with `[INFO]` / `[WARN]` prefixes is enough; Railway collects it from stdout.
- **Cron runs inside the bot.** `CronRunner.RunOnceAsync` is called every minute
  by a `PeriodicTimer` in `Program.cs`. It has no `Environment.Exit`, and all
  exceptions are caught so a failed delivery does not kill the bot.

---

## License

See [LICENSE](LICENSE).