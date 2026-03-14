# DataFlowMapper — Connection Info

## Aplicatie

| Serviciu   | URL                              |
|------------|----------------------------------|
| Frontend   | http://localhost:3000            |
| Backend API| http://localhost:5001/api        |
| Swagger    | http://localhost:5001/swagger    |
| SignalR Hub| http://localhost:5001/hubs/execution |

## PostgreSQL (Docker)

| Parametru        | Valoare   |
|------------------|-----------|
| Host (din Docker)| `db`      |
| Host (din afara) | `localhost` |
| Port             | `5432`    |
| Database         | `dataflow`|
| Username         | `postgres`|
| Password         | `dev123`  |

### Connection string (folosit in aplicatie — backend in Docker)
```
Host=db;Port=5432;Database=dataflow;Username=postgres;Password=dev123
```

### Connection string (din afara Docker — DBeaver, psql local etc.)
```
Host=localhost;Port=5432;Database=dataflow;Username=postgres;Password=dev123
```

### psql CLI
```bash
psql -h localhost -p 5432 -U postgres -d dataflow
# parola: dev123
```

### DBeaver / TablePlus
- Host: `localhost`
- Port: `5432`
- Database: `dataflow`
- User: `postgres`
- Password: `dev123`

## Tabele mock disponibile

| Tabel       | Coloane principale                                          | Randuri |
|-------------|-------------------------------------------------------------|---------|
| `customers` | id, first_name, last_name, email, country, created_at       | 8       |
| `orders`    | id, customer_id, product, quantity, unit_price, status      | 10      |
| `products`  | id, name, category, price, stock                            | 6       |
