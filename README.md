# SAD Bot

A Discord bot built on [Crovus](https://github.com/Nyakaze/Crovus). It currently handles birthdays: members save their birthday with a timezone, and on the day the bot pings them, gives them a **Birthday** role and posts a random birthday GIF. The message is deleted and the role removed after 24 hours. You can also post a live birthday list that updates itself.

## Commands

| Command | Who | What it does |
| --- | --- | --- |
| `/birthday set day month timezone [year]` | Everyone | Save or change your birthday |
| `/birthday remove` | Everyone | Delete your birthday |
| `/birthday show [user]` | Everyone | Show a saved birthday |
| `/birthday list` | Everyone | Show every birthday on the server, sorted by month and day |
| `/birthday next` | Everyone | Show the next 10 birthdays |
| `/birthday-admin set` / `remove` | Manage Server | Change or delete a member's birthday, picked with `user` or `user_id` |
| `/birthday-admin channel` | Manage Server | Set the announcement channel. **Nobody gets celebrated until this is set** |
| `/birthday-admin role` | Manage Server | Use an existing role. If none is set, a "Birthday" role is created |
| `/birthday-admin message` | Manage Server | Set the announcement text with `{user}`, `{name}` and `{age}`. Leave it empty to reset |
| `/birthday-admin board` / `board-remove` | Manage Server | Post or delete the live birthday list |
| `/birthday-admin settings` / `test` | Manage Server | Show the settings or preview an announcement |

## Discord setup

1. Create an application at <https://discord.com/developers/applications> and copy the bot token. No privileged intents are needed.
2. Invite the bot with the `bot` and `applications.commands` scopes and these permissions: **View Channels**, **Send Messages**, **Embed Links**, **Manage Roles**.
3. In the server's role list, drag the bot's role **above** the Birthday role. Otherwise the bot can't assign it.

## Deploying on Hostinger (Docker Manager)

Every push to `main` builds the image `ghcr.io/nyakaze/sadbot:latest` with GitHub Actions. The VPS only needs `deploy/compose.yaml`.

1. After the first workflow run, open the package on GitHub (Profile → Packages → sadbot → Package settings) and set its visibility to **Public**, so the VPS can pull it without logging in.
2. In hPanel, open **VPS → Docker Manager → Compose**, create a project named `sadbot` and paste the contents of `deploy/compose.yaml`.
3. Set the environment variables:
   - `DISCORD_TOKEN` is required.
   - `POSTGRES_PASSWORD` is required. Choose a long random value and never change it after the first start.
   - `GIPHY_API_KEY` is optional but recommended.
4. Deploy. Database migrations run automatically, and the data lives in the `postgres-data` volume.

To update, push to `main`, wait for the workflow, then redeploy the project in Docker Manager. `pull_policy: always` makes it fetch the new image.

## Running from source

```bash
cp .env.example .env
docker compose up -d --build
docker compose logs -f sadbot
```

## Health and logs

- `GET /health/live` shows whether the process is running.
- `GET /health/ready` checks the database and the Discord gateway.
- Both are bound to `127.0.0.1:8080` on the host. Docker uses the ready check for the container health status.
- In the container, logs are written as structured JSON to stdout.

## Development

The default connection string in `appsettings.json` expects the password `sadbot`, so set `POSTGRES_PASSWORD=sadbot` in your local `.env`.

```bash
docker compose up -d postgres
dotnet user-secrets --project src/SadBot set Discord:Token <token>
dotnet user-secrets --project src/SadBot set Discord:DevelopmentGuildId <guild-id>
dotnet run --project src/SadBot
dotnet test
```

Setting `Discord:DevelopmentGuildId` registers the commands on that one server only, where they update instantly.

To add a migration:

```bash
dotnet ef migrations add <Name> --project src/SadBot --output-dir Data/Migrations
```
