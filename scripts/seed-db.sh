#!/bin/bash

# Seed script for DataFlowMapper development database
# Usage: ./scripts/seed-db.sh

CONTAINER="licenta-dataflowmapper-db-1"
DB="dataflow"
USER="postgres"

echo "Seeding database '$DB' in container '$CONTAINER'..."

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
-- CUSTOMERS (20 rows)
-- ============================================================

INSERT INTO customers (first_name, last_name, email, country) VALUES
  ('Alice',   'Popescu',   'alice@example.com',   'Romania'),
  ('Bob',     'Ionescu',   'bob@example.com',     'Romania'),
  ('Carol',   'Smith',     'carol@example.com',   'Germany'),
  ('Dan',     'Müller',    'dan@example.com',     'Germany'),
  ('Eva',     'Popa',      'eva@example.com',     'Romania'),
  ('Frank',   'Brown',     'frank@example.com',   'UK'),
  ('Grace',   'Lee',       'grace@example.com',   'USA'),
  ('Hank',    'Tanaka',    'hank@example.com',    'Japan'),
  ('Irina',   'Dumitrescu','irina@example.com',   'Romania'),
  ('John',    'Doe',       'john@example.com',    'USA'),
  ('Karen',   'White',     'karen@example.com',   'UK'),
  ('Luca',    'Rossi',     'luca@example.com',    'Italy'),
  ('Maria',   'Garcia',    'maria@example.com',   'Spain'),
  ('Niko',    'Papadopoulos','niko@example.com',  'Greece'),
  ('Olivia',  'Martin',    'olivia@example.com',  'France'),
  ('Peter',   'Novak',     'peter@example.com',   'Slovakia'),
  ('Quinn',   'Zhang',     'quinn@example.com',   'China'),
  ('Radu',    'Stanescu',  'radu@example.com',    'Romania'),
  ('Sofia',   'Ivanova',   'sofia@example.com',   'Bulgaria'),
  ('Tom',     'Wilson',    'tom@example.com',     'USA');

-- ============================================================
-- PRODUCTS (10 rows)
-- ============================================================

INSERT INTO products (name, category, price, stock) VALUES
  ('Laptop Pro 15',        'Electronics', 1299.99,  42),
  ('Wireless Mouse',       'Electronics',   29.99, 200),
  ('Standing Desk',        'Furniture',    499.00,  15),
  ('Mechanical Keyboard',  'Electronics',   89.99,  80),
  ('Monitor 27inch',       'Electronics',  349.99,  30),
  ('Office Chair',         'Furniture',    249.00,  20),
  ('USB-C Hub',            'Electronics',   49.99, 150),
  ('Webcam HD',            'Electronics',   79.99,  60),
  ('Desk Lamp',            'Furniture',     34.99,  90),
  ('Noise Cancelling Headphones', 'Electronics', 199.99, 45);

-- ============================================================
-- ORDERS (30 rows)
-- ============================================================

INSERT INTO orders (customer_id, product, quantity, unit_price, status) VALUES
  (1,  'Laptop Pro 15',               1, 1299.99, 'completed'),
  (1,  'Wireless Mouse',              2,   29.99, 'completed'),
  (2,  'Standing Desk',               1,  499.00, 'pending'),
  (3,  'Mechanical Keyboard',         1,   89.99, 'completed'),
  (4,  'Monitor 27inch',              2,  349.99, 'shipped'),
  (5,  'Wireless Mouse',              1,   29.99, 'pending'),
  (6,  'Office Chair',                2,  249.00, 'completed'),
  (7,  'Laptop Pro 15',               1, 1299.99, 'shipped'),
  (8,  'Mechanical Keyboard',         3,   89.99, 'completed'),
  (9,  'Monitor 27inch',              1,  349.99, 'cancelled'),
  (10, 'USB-C Hub',                   2,   49.99, 'completed'),
  (11, 'Webcam HD',                   1,   79.99, 'pending'),
  (12, 'Desk Lamp',                   3,   34.99, 'completed'),
  (13, 'Noise Cancelling Headphones', 1,  199.99, 'shipped'),
  (14, 'Wireless Mouse',              4,   29.99, 'completed'),
  (15, 'Standing Desk',               2,  499.00, 'cancelled'),
  (16, 'Laptop Pro 15',               1, 1299.99, 'completed'),
  (17, 'USB-C Hub',                   1,   49.99, 'pending'),
  (18, 'Office Chair',                1,  249.00, 'completed'),
  (19, 'Webcam HD',                   2,   79.99, 'shipped'),
  (20, 'Mechanical Keyboard',         1,   89.99, 'completed'),
  (1,  'Monitor 27inch',              1,  349.99, 'completed'),
  (2,  'Noise Cancelling Headphones', 2,  199.99, 'pending'),
  (3,  'USB-C Hub',                   3,   49.99, 'completed'),
  (4,  'Desk Lamp',                   1,   34.99, 'completed'),
  (5,  'Laptop Pro 15',               1, 1299.99, 'cancelled'),
  (6,  'Webcam HD',                   1,   79.99, 'completed'),
  (7,  'Wireless Mouse',              5,   29.99, 'shipped'),
  (8,  'Standing Desk',               1,  499.00, 'completed'),
  (9,  'Office Chair',                2,  249.00, 'pending');

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
