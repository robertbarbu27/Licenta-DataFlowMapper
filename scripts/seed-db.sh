#!/bin/bash

# Seed script for DataFlowMapper development database
# Usage: ./scripts/seed-db.sh

CONTAINER="licenta-dataflowmapper-db-1"
DB="dataflow"
USER="postgres"

echo "Seeding database '$DB' in container '$CONTAINER'..."
echo "This will generate 1 million orders + 100k customers. Please wait..."

docker exec -i $CONTAINER psql -U $USER -d $DB << 'SQL'

-- ============================================================
-- DROP & RECREATE TABLES
-- ============================================================

DROP TABLE IF EXISTS orders CASCADE;
DROP TABLE IF EXISTS customers CASCADE;
DROP TABLE IF EXISTS products CASCADE;
DROP TABLE IF EXISTS orders_completed CASCADE;
DROP TABLE IF EXISTS customers_clean CASCADE;

CREATE TABLE customers (
  id          SERIAL PRIMARY KEY,
  first_name  VARCHAR(50),
  last_name   VARCHAR(50),
  email       VARCHAR(100),
  country     VARCHAR(50),
  created_at  TIMESTAMP DEFAULT NOW()
);

CREATE TABLE products (
  id        SERIAL PRIMARY KEY,
  name      VARCHAR(100),
  category  VARCHAR(50),
  price     NUMERIC(10,2),
  stock     INT
);

CREATE TABLE orders (
  id          SERIAL PRIMARY KEY,
  customer_id INT,
  product     VARCHAR(100),
  quantity    INT,
  unit_price  NUMERIC(10,2),
  status      VARCHAR(20),
  ordered_at  TIMESTAMP DEFAULT NOW()
);

-- ============================================================
-- PRODUCTS (10 rows)
-- ============================================================

INSERT INTO products (name, category, price, stock) VALUES
  ('Laptop Pro 15',               'Electronics', 1299.99,  42),
  ('Wireless Mouse',              'Electronics',   29.99, 200),
  ('Standing Desk',               'Furniture',    499.00,  15),
  ('Mechanical Keyboard',         'Electronics',   89.99,  80),
  ('Monitor 27inch',              'Electronics',  349.99,  30),
  ('Office Chair',                'Furniture',    249.00,  20),
  ('USB-C Hub',                   'Electronics',   49.99, 150),
  ('Webcam HD',                   'Electronics',   79.99,  60),
  ('Desk Lamp',                   'Furniture',     34.99,  90),
  ('Noise Cancelling Headphones', 'Electronics',  199.99,  45);

-- ============================================================
-- CUSTOMERS (100,000 rows via generate_series)
-- ============================================================

INSERT INTO customers (first_name, last_name, email, country)
SELECT
  (ARRAY['Alice','Bob','Carol','Dan','Eva','Frank','Grace','Hank','Irina','John',
         'Karen','Luca','Maria','Niko','Olivia','Peter','Quinn','Radu','Sofia','Tom'])
    [(i % 20) + 1],
  (ARRAY['Popescu','Ionescu','Smith','Muller','Popa','Brown','Lee','Tanaka',
         'Dumitrescu','Doe','White','Rossi','Garcia','Papadopoulos','Martin',
         'Novak','Zhang','Stanescu','Ivanova','Wilson'])
    [(i % 20) + 1],
  'user' || i || '@example.com',
  (ARRAY['Romania','Germany','USA','UK','France','Italy','Spain','Greece',
         'Japan','China','Slovakia','Bulgaria'])
    [(i % 12) + 1]
FROM generate_series(1, 100000) AS s(i);

-- ============================================================
-- ORDERS (1,000,000 rows via generate_series)
-- ============================================================

INSERT INTO orders (customer_id, product, quantity, unit_price, status, ordered_at)
SELECT
  (i % 100000) + 1,
  (ARRAY['Laptop Pro 15','Wireless Mouse','Standing Desk','Mechanical Keyboard',
         'Monitor 27inch','Office Chair','USB-C Hub','Webcam HD','Desk Lamp',
         'Noise Cancelling Headphones'])
    [(i % 10) + 1],
  (i % 5) + 1,
  (ARRAY[1299.99, 29.99, 499.00, 89.99, 349.99, 249.00, 49.99, 79.99, 34.99, 199.99])
    [(i % 10) + 1],
  (ARRAY['completed','pending','shipped','cancelled'])
    [(i % 4) + 1],
  NOW() - ((random() * 365 * 2)::int || ' days')::interval
FROM generate_series(1, 1000000) AS s(i);

-- ============================================================
-- TARGET TABLES (empty, ready to receive pipeline output)
-- ============================================================

CREATE TABLE orders_completed (
  id          INT,
  customer_id INT,
  product     TEXT,
  quantity    INT,
  unit_price  NUMERIC(10,2),
  status      TEXT,
  ordered_at  TIMESTAMP
);

CREATE TABLE customers_clean (
  id         INT,
  full_name  TEXT,
  email      TEXT,
  country    TEXT,
  created_at TIMESTAMP
);

SQL

echo ""
echo "Done. Tables seeded:"
docker exec $CONTAINER psql -U $USER -d $DB -c "\dt"
echo ""
echo "Row counts:"
docker exec $CONTAINER psql -U $USER -d $DB -c "
  SELECT 'customers' as table, COUNT(*) as rows FROM customers
  UNION ALL
  SELECT 'products',           COUNT(*) FROM products
  UNION ALL
  SELECT 'orders',             COUNT(*) FROM orders;
"
