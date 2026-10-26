# Retail Inventory & Billing Desktop Application (POS) 🖥️🧾

A Windows desktop application designed for managing retail store operations, real-time stock inventory, automated low-stock alerts, and customer invoice generation.

## 🚀 Tech Stack
* **Framework & UI:** C#, Windows Forms (WinForms - .NET 8.0)
* **Data Access:** ADO.NET (`SqlConnection`, `SqlCommand`, `SqlDataAdapter`, `SqlDataReader`)
* **Database:** MS SQL Server (`SqlTransaction` for atomic commits & rollbacks)

## ✨ Key Features
* **Direct ADO.NET Database Architecture:** High-performance, low-latency SQL Server connectivity with automatic database & schema initialization (`Products` and `Invoices` tables).
* **Atomic Transactional Billing (`SqlTransaction`):** Ensures stock deduction and invoice generation execute as a single atomic unit—automatically rolling back on insufficient stock or errors.
* **Automated Low-Stock Alerts:** Real-time inventory monitoring that triggers visual alerts whenever product quantities fall below threshold levels (< 10 units).
* **Interactive DataGridView:** Instant view of product catalog, pricing, and live stock quantities.

## ⚙️ How to Run Locally
1. Clone the repository and open in **Visual Studio 2022**.
2. Ensure **SQL Server Express (`localhost\SQLEXPRESS`)** is running.
3. Press `F5` — the application will automatically initialize the `RetailInventoryDb` database and tables on startup!
