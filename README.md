# Afterword

a cloud-based media manager tracking books, games, movies & anime, featuring an automated ELT pipeline (PostgreSQL + dbt) for analytical modeling. 

this project marks the evolution of a local c# desktop application (which originally exported static json logs) into a fully decoupled, api-first web architecture backed by postgresql. it is designed from the ground up to serve as a foundation for an automated data engineering pipeline (elt) and habit analytics platform.


## Roadmap

### V1 — online catalog

first release · functional MVP

- [x] **connect C# to neon:** persist data in PostgreSQL instead of local JSON files.
- [x] **turn C# into a web API:** routes to read and manage books, games, movies, and anime.
- [x] **build the web interface:** public catalog and `/admin` panel with login, title search through external APIs, forms, editing, deletion, and API key settings.
- [x] **migrate the old catalogs:** records imported from the JSON files are stored in Neon; review the counts for each category before launch.
- [x] **complete the catalog scope:** individual detail pages and consumption statuses (planned, in progress, completed, and abandoned).
- [x] **deploy and validate:** host the API and website and validate the complete workflow.

**launch criteria:** ; the API, admin panel, and public catalog are deployed. you can create, edit, and delete media through the browser. catalog changes made through the admin panel trigger a new Vercel deployment so the public static catalog can be refreshed. the data remains available even when the free API service is suspended.

**status:** complete.

### V2 — history and watchlist

turn the catalog into a source of historical data.

- a separate waitlist for each media category.
- manual queue ordering.
- dates for adding, starting, and completing an item.
- history of status changes.
- ratings and progress tracking, depending on the category.
- initial statistics: waiting time, items completed per year, and genre distribution.

**launch criteria:** the system can answer not only what you are currently consuming, but also how your habits change over time.

**status:** on-going.

### V3 — data engineering pipeline

deepen the development.

- ingest metadata from external APIs.
- store the raw data received.
- transform data with SQL and dbt.
- build analytical fact and dimension tables.
- add data quality tests.
- run the pipeline automatically.
- document the architecture and technical decisions.

**launch criteria:** there is a reproducible ingestion and transformation workflow, not just manually run SQL queries.

**status:** not yet started.

### V4 — analytics and refinement

optional improvements.

- annual dashboards and comparisons across categories.
- analysis of genre distribution and how it changes over time.
- median waitlist time and the share of items started after different periods.
- pipeline monitoring and logs.
- query optimization, integration tests, and security improvements.
- add orchestration tools if there is a clear need for them.

**completion criteria:** the application and pipeline are documented, tested, and ready to present.

**status:** not yet started.

## How the project works

| part | technology | responsibility |
| --- | --- | --- |
| Website and admin panel | HTML, CSS, and JavaScript | public catalog and `/admin` area |
| API | C# and ASP.NET Core 10 | admin login, external API integrations, and read/write operations |
| database | neon PostgreSQL and Npgsql | books, games, movies, anime, and API key settings |
| published website | vercel | serves the pages and the static catalog snapshot |
| published API | render | handles admin operations and accesses Neon |

the website and admin panel are in english; reviews are displayed in the language in which they were written.

the database uses separate tables for each category. it does not use a single `media` table with `jsonb` columns. the API creates the basic schema when it starts. versioned schema migrations are a future improvement.

### static catalog snapshot

for deployment, a script requests the catalogs from the API and generates static JSON files for all four categories. it also copies book covers stored in PostgreSQL into static files for the deployment. the cover images themselves are stored as image data (bytes in `BYTEA`), not as local file paths.

after an item is created, edited, deleted, or its cover is refreshed through the admin panel, the API triggers a Vercel Deploy Hook. Vercel builds an updated deployment, while the previous deployment remains available. public browsing does not need to call the API. the admin panel and the build process still use it.

games, movies, and anime use image URLs provided by external APIs. the catalog stores those URLs with the metadata instead of copying the images into the database.

## Run locally

### requirements

- .NET 10 SDK.
- access to a PostgreSQL database (preferably on neon).

### configuration

1. copy `configuration.example.json` to `configuration.json`. the local file is ignored by Git. set `Admin.Username` and `Admin.Password`.
2. set `NEON_CONNECTION_STRING` as a system environment variable. do not put the connection string in `Database.cs` or commit it to GitHub.
3. to save API keys through the admin panel, set `APP_SETTINGS_ENCRYPTION_KEY` to a Base64-encoded 32-byte key. it can also be set as `AppSettingsEncryptionKey` in your local `configuration.json`. if API keys are already encrypted in the database, use the same key that was used to save them.

to generate a new key in PowerShell:

```powershell
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

run the API:

```powershell
dotnet run --project MediaManager.csproj --launch-profile Afterword.Api
```

the local profile runs at `http://localhost:5000`. the website and API are served together: open `/` for the catalog and `/admin` for the admin panel. in development, the pages call the API directly.

## Deployment: GitHub, Render, Vercel, and Neon

1. **gitHub:** push the project code. `.gitignore` excludes local configuration, secrets, generated files, the temporary folder, and the old Avalonia files. `configuration.example.json` contains only empty fields and can be committed.
2. **render:** create the service from `render.yaml`. Set `NEON_CONNECTION_STRING`, `Admin__Username`, `Admin__Password`, and `APP_SETTINGS_ENCRYPTION_KEY` as service secrets. The encryption key is separate from the Google Books, TMDB, and RAWG API keys; use the same encryption key if those API keys were already saved in Neon.
3. **vercel:** import the same repository with the framework preset set to **Other** and set `AFTERWORD_API_ORIGIN` to the public HTTPS URL of the Render API, without a trailing slash. The root `vercel.json` configures the static output (`wwwroot`) and proxies `/api/*` to the Render service. The build runs `npm run build` and generates the first static snapshot from the database. If the Render service URL changes, update the rewrite destination in `vercel.json` too.
4. **move the external API keys:** the keys in your local `configuration.json` are not sent to GitHub or copied to Render. After the website is deployed, open `/admin`, sign in, and select the gear-shaped **Settings** tab. Copy `ExternalApis.GoogleBooksApiKey`, `ExternalApis.TmdbApiKey`, and `ExternalApis.RawgApiKey` from your private local `configuration.json` into their matching fields, then save. The API encrypts the keys and stores them in Neon. Before saving, make sure `APP_SETTINGS_ENCRYPTION_KEY` is set on Render. The fields stay blank after saving; the status beside each field shows whether a key is configured. Do not put these API keys in `vercel.json`, GitHub, or Vercel environment variables.
5. **automatic updates:** create a Deploy Hook for the production branch in the Vercel project's Git settings. Then add its URL manually as `VERCEL_DEPLOY_HOOK` in the Render service's environment variables. This URL is secret and must not be committed to GitHub.
6. **validation and backups:** open the deployed catalogs, test admin login and CRUD operations, confirm that a write triggers a new deployment, and configure a database backup plan.

## Security and local files

- `configuration.json`, connection strings, passwords, API keys, `APP_SETTINGS_ENCRYPTION_KEY`, and `VERCEL_DEPLOY_HOOK` must not be committed to the repository.
- The API keys in local `configuration.json` are not automatically available to the hosted API. Copy them through `/admin` → **Settings** after deployment; the panel encrypts them before storing them in Neon.
- `APP_SETTINGS_ENCRYPTION_KEY` is the separate encryption key used by the server to protect API keys in Neon. Keep it in the Render environment, not in Vercel or GitHub.
- `wwwroot/catalog-data/` is generated during deployment and is also excluded from Git.

## API routes

| Method | Route | Action |
| --- | --- | --- |
| GET | `/api/health` | Checks whether the API is online |
| GET | `/api/books` and `/api/books/{id}` | Lists or retrieves books |
| GET | `/api/books/{id}/cover` | Returns a cover stored in the database |
| POST / PUT / DELETE | `/api/books[/{id}]` | Creates, edits, or deletes a book; login required |
| POST | `/api/books/{id}/cover/refresh` | Fetches and stores a new cover; login required |
| GET | `/api/games[/{id}]` | Lists or retrieves games |
| POST / PUT / DELETE | `/api/games[/{id}]` | Creates, edits, or deletes a game; login required |
| GET | `/api/movies[/{id}]` | Lists or retrieves movies |
| POST / PUT / DELETE | `/api/movies[/{id}]` | Creates, edits, or deletes a movie; login required |
| GET | `/api/animes[/{id}]` | Lists or retrieves anime |
| POST / PUT / DELETE | `/api/animes[/{id}]` | Creates, edits, or deletes an anime; login required |
| POST | `/api/admin/login` | Starts an admin session |
| GET | `/api/admin/session` | Checks the current session |
| POST | `/api/admin/logout` | Ends the session |
| GET / PUT | `/api/admin/settings` | Reads and saves API key settings |

