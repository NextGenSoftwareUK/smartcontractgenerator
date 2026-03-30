# Fix: Medusa "Pg connection failed" / KnexTimeoutError on Railway

If Medusa deploy logs show **Pg connection failed** and **KnexTimeoutError**, the container cannot reach PostgreSQL. Use the **public** database URL.

## Do this in Railway

### 1. Medusa service → Variables

1. **Remove** the DATABASE_URL that comes from a **reference** (the internal/private URL from the Postgres service). Delete that variable or unlink it.
2. **Add** a **plain** variable (not a reference):
   - **Name:** `DATABASE_URL`
   - **Value:** your **public** PostgreSQL connection string.

   Example (replace with your real password if different):

   ```
   postgresql://postgres:EgjYwBDeLZmrjseuYEmVzhrhcKFGfzoQ@interchange.proxy.rlwy.net:42471/railway
   ```

   Get the exact value from: Railway → **PostgreSQL** service → **Connect** or **Variables** → copy **`DATABASE_PUBLIC_URL`** (or the URL that contains `interchange.proxy.rlwy.net` or similar **public** host).

3. Do **not** add `?sslmode=require` yourself — the app adds it for Railway public URLs.

### 2. Redeploy Medusa

Trigger a **redeploy** of the Medusa service so it picks up the new `DATABASE_URL`.

### 3. Check logs

You should see:

- `wait-for-db: checking database connectivity...`
- `Database is ready.`
- Then Medusa starting without "Pg connection failed".

If you still see timeouts, the public URL may be wrong (wrong password/host/port) or your Railway plan may restrict outbound connections; double-check the URL from the Postgres service.
