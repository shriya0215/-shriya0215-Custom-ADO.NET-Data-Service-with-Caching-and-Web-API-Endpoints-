# Custom ADO.NET Data Service with Caching and Web API

An ASP.NET Core (.NET 8) Web API over SQL Server that uses ADO.NET directly (no ORM), with
transactions and a configurable in-memory cache.

## Run it

1. Create the database: `sqlcmd -S "(localdb)\MSSQLLocalDB" -i Database/schema.sql`
   (or run the script in SSMS / Azure Data Studio).
2. Adjust `ConnectionStrings:Default` in `appsettings.json` if you are not using LocalDB.
3. `dotnet run`, then use `requests.http` (VS Code REST Client / Visual Studio) or curl.

## Endpoints

| Method | Route | Purpose | Data access |
|---|---|---|---|
| GET | `/api/products?category=` | Catalog + per-category summary | **Disconnected** (`SqlDataAdapter` -> `DataSet`), cached |
| GET | `/api/products/{id}` | Single product | Connected (`SqlDataReader`), cached |
| POST | `/api/products` | Create | Connected, `OUTPUT INSERTED.Id` |
| PUT | `/api/products/{id}` | Update | Connected |
| DELETE | `/api/products/{id}` | Delete (409 if referenced by orders) | Connected |
| POST | `/api/orders` | Place order | **Transaction** across 3 tables |
| GET | `/api/orders/{id}` | Read order and lines | Connected, two result sets |

Cached GETs return an `X-Cache: HIT|MISS` header.

## UI

`wwwroot/index.html` is a single-file front end served by the same app, so it needs no build step and no CORS setup.
After `dotnet run`, open the root URL (e.g. `http://localhost:5000/`).

- Browse and filter the catalog (the filter calls `GET /api/products?category=`).
- Add, edit and delete products.
- Build an order slip and place it. Ordering more than is in stock shows the rejection and leaves stock untouched.
- The strip in the top-right shows the latest request with its status, `X-Cache` result (HIT or MISS) and time. Click it for the last 10 requests.
- Prices use the `CURRENCY` constant at the top of the script.

## Requirement map

- **Repository with SqlConnection/SqlCommand + parameters:** `Data/ProductRepository.cs`. Every value is a typed `SqlParameter`; no string concatenation anywhere.
- **Disconnected retrieval:** `ProductRepository.LoadCatalog` fills one `DataSet` from two SELECTs (mapped to `Products` and `CategorySummary` tables) and shapes it into JSON-friendly records.
- **Transactions:** `Data/OrderRepository.PlaceOrderAsync` inserts the order, inserts lines, and decrements stock in one `SqlTransaction`. If any line fails (`InsufficientStockException`) everything is rolled back. Stock is checked and decremented in a single `UPDATE ... WHERE Stock >= @Quantity`, so two concurrent orders can never oversell. Lines are processed in product-id order to avoid deadlocks.
- **REST endpoints returning JSON:** `Controllers/`. Input is validated with data annotations (automatic 400s); domain failures map to 404/409 problem details.
- **Caching with configurable expiration:** `Services/ProductCache.cs` (wrapper over `IMemoryCache`), settings in `appsettings.json` under `Cache` (`ProductSeconds`, `CatalogSeconds`, `MaxEntries`), validated at startup.

## Caching design

- **Cache-aside reads:** hit returns immediately; a miss loads from SQL and stores with an absolute TTL.
- **Invalidation on write:** product update/delete evicts that product; any write (including a successful order) expires all cached catalogs through a shared change token. Rolled-back orders invalidate nothing because nothing changed.
- **Stampede protection:** concurrent misses for the same key share one database call (striped semaphores).
- **Bounded memory:** `SizeLimit` caps entries, and null results (unknown ids) are never cached, so random ids/categories cannot exhaust memory.
- **Trade-off:** catalogs include stock, so frequent orders flush them often. If that matters, drop stock from the cached list or lengthen `CatalogSeconds` and accept slightly stale stock.
- **Known limitation:** the cache is per process. Running several instances needs a distributed cache (e.g. Redis); the `ProductCache` wrapper is the single place to change.

## Project layout

```
Controllers/  HTTP layer (thin)
Services/     Caching + cache invalidation (ProductService, OrderService, ProductCache)
Data/         ADO.NET repositories and connection factory
Models/       Records, request DTOs, domain exceptions
Database/     schema.sql with seed data
wwwroot/      Single-page UI (index.html)
```
