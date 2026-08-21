# twttr

## Development

You need to have .NET 10 SDK, Node 24, and PostgreSQL 18 installed. Docker is required only for running tests. To set up the app, you need to manually create an empty database `twttr` and set your database connection string by running the following from the `twttr/` root directory:

```bash
dotnet user-secrets set "ConnectionStrings:twttr" "Host=localhost;Database=twttr;Username=<USERNAME>;Password=<PASSWORD>"
```

Add `;Port=<PORT>` if Postgres is on some port other than 5432. The database schema is created automatically when the app runs in development mode.

You need two terminals to run the app. In one terminal, run `dotnet watch --launch-profile http` from `twttr/`. This will serve the API on port 5007 (no UI). In another terminal, go to `twttr/frontend/`, run `npm install` and then `npm run dev`. Open `http://localhost:5173` in your browser.

## Tests

Tests use Testcontainers and requires Docker to start a temporary PostgreSQL database; your development database is untouched. Run `dotnet test twttr.Tests/`.
