using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace RetailInventoryBillingApp
{
    public partial class Form1 : Form
    {
        // SQL Server Connection Strings (Same SQLEXPRESS server as Project 1 & 2)
        private readonly string _masterConn = "Server=localhost\\SQLEXPRESS;Database=master;Trusted_Connection=True;TrustServerCertificate=True;";
        private readonly string _dbConn = "Server=localhost\\SQLEXPRESS;Database=RetailInventoryDb;Trusted_Connection=True;TrustServerCertificate=True;";

        private DataGridView dgvInventory = new();
        private TextBox txtName = new(), txtCategory = new(), txtPrice = new(), txtStock = new();
        private TextBox txtCustomer = new(), txtBillProductId = new(), txtBillQty = new();
        private Label lblAlert = new();

        public Form1()
        {
            InitializeComponent();
            SetupCustomUI();
            InitializeDatabaseWithAdoNet();
            LoadInventory();
        }
        private void Form1_Load(object sender, EventArgs e)
        {

        }

        // 1. ADO.NET Auto Database & Table Creation (No manual SQL script needed!)
        private void InitializeDatabaseWithAdoNet()
        {
            try
            {
                using (var conn = new SqlConnection(_masterConn))
                {
                    conn.Open();
                    var cmd = new SqlCommand("IF DB_ID('RetailInventoryDb') IS NULL CREATE DATABASE RetailInventoryDb;", conn);
                    cmd.ExecuteNonQuery();
                }

                using (var conn = new SqlConnection(_dbConn))
                {
                    conn.Open();
                    string createTablesQuery = @"
                        IF OBJECT_ID('Products', 'U') IS NULL
                        CREATE TABLE Products (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            Name NVARCHAR(100) NOT NULL,
                            Category NVARCHAR(50),
                            Price DECIMAL(18,2) NOT NULL,
                            StockQuantity INT NOT NULL
                        );

                        IF OBJECT_ID('Invoices', 'U') IS NULL
                        CREATE TABLE Invoices (
                            InvoiceId INT IDENTITY(1,1) PRIMARY KEY,
                            CustomerName NVARCHAR(100),
                            ProductId INT,
                            Quantity INT,
                            TotalAmount DECIMAL(18,2),
                            InvoiceDate DATETIME DEFAULT GETDATE()
                        );";
                    var cmd = new SqlCommand(createTablesQuery, conn);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("DB Init Error: " + ex.Message);
            }
        }

        // 2. ADO.NET Fetch Inventory & Automated Stock Alerts
        private void LoadInventory()
        {
            try
            {
                using var conn = new SqlConnection(_dbConn);
                using var adapter = new SqlDataAdapter("SELECT * FROM Products ORDER BY Id DESC", conn);
                var dt = new DataTable();
                adapter.Fill(dt);
                dgvInventory.DataSource = dt;

                int lowStockCount = 0;
                foreach (DataRow row in dt.Rows)
                {
                    if (Convert.ToInt32(row["StockQuantity"]) < 10)
                        lowStockCount++;
                }

                lblAlert.Text = lowStockCount > 0
                    ? $"⚠️ LOW STOCK ALERT: {lowStockCount} item(s) have stock below 10 units!"
                    : "✅ All stock levels are healthy.";
                lblAlert.ForeColor = lowStockCount > 0 ? Color.Red : Color.Green;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading inventory: " + ex.Message);
            }
        }

        // 3. ADO.NET Add Product to Inventory
        private void BtnAddProduct_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text) || !decimal.TryParse(txtPrice.Text, out decimal price) || !int.TryParse(txtStock.Text, out int stock))
            {
                MessageBox.Show("Please enter valid Product Name, Price, and Stock Quantity.");
                return;
            }

            using var conn = new SqlConnection(_dbConn);
            conn.Open();
            string query = "INSERT INTO Products (Name, Category, Price, StockQuantity) VALUES (@name, @cat, @price, @stock)";
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@name", txtName.Text);
            cmd.Parameters.AddWithValue("@cat", txtCategory.Text);
            cmd.Parameters.AddWithValue("@price", price);
            cmd.Parameters.AddWithValue("@stock", stock);
            cmd.ExecuteNonQuery();

            txtName.Clear(); txtCategory.Clear(); txtPrice.Clear(); txtStock.Clear();
            LoadInventory();
            MessageBox.Show("Product added to inventory!");
        }

        // 4. ADO.NET Transactional Commit (Generate Invoice + Deduct Stock Atomically)
        private void BtnGenerateInvoice_Click(object? sender, EventArgs e)
        {
            if (!int.TryParse(txtBillProductId.Text, out int prodId) || !int.TryParse(txtBillQty.Text, out int qty) || qty <= 0)
            {
                MessageBox.Show("Enter valid Product ID and Quantity.");
                return;
            }

            using var conn = new SqlConnection(_dbConn);
            conn.Open();
            using SqlTransaction transaction = conn.BeginTransaction();

            try
            {
                var checkCmd = new SqlCommand("SELECT Name, Price, StockQuantity FROM Products WHERE Id = @id", conn, transaction);
                checkCmd.Parameters.AddWithValue("@id", prodId);

                string prodName = "";
                decimal price = 0;
                int currentStock = 0;

                using (var reader = checkCmd.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new Exception("Product ID not found!");

                    prodName = reader.GetString(0);
                    price = reader.GetDecimal(1);
                    currentStock = reader.GetInt32(2);
                }

                if (currentStock < qty)
                    throw new Exception($"Insufficient Stock! Available: {currentStock}");

                decimal totalAmount = price * qty;

                // Deduct Stock inside Transaction
                var updateCmd = new SqlCommand("UPDATE Products SET StockQuantity = StockQuantity - @qty WHERE Id = @id", conn, transaction);
                updateCmd.Parameters.AddWithValue("@qty", qty);
                updateCmd.Parameters.AddWithValue("@id", prodId);
                updateCmd.ExecuteNonQuery();

                // Generate Invoice Record inside Transaction
                var invoiceCmd = new SqlCommand(
                    "INSERT INTO Invoices (CustomerName, ProductId, Quantity, TotalAmount) VALUES (@cust, @pid, @qty, @total)",
                    conn, transaction);
                string customerName = string.IsNullOrWhiteSpace(txtCustomer.Text) ? "Walk-in Customer" : txtCustomer.Text;
                invoiceCmd.Parameters.AddWithValue("@cust", customerName);
                invoiceCmd.Parameters.AddWithValue("@pid", prodId);
                invoiceCmd.Parameters.AddWithValue("@qty", qty);
                invoiceCmd.Parameters.AddWithValue("@total", totalAmount);
                invoiceCmd.ExecuteNonQuery();

                // Commit Transaction
                transaction.Commit();

                LoadInventory();
                MessageBox.Show(
                    $"🧾 INVOICE GENERATED!\n\nCustomer: {customerName}\nProduct: {prodName}\nQuantity: {qty}\nTotal Amount: ₹{totalAmount:F2}",
                    "Billing Receipt", MessageBoxButtons.OK, MessageBoxIcon.Information);

                txtCustomer.Clear(); txtBillProductId.Clear(); txtBillQty.Clear();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                MessageBox.Show("Transaction Rolled Back: " + ex.Message, "Billing Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 5. Build Desktop Controls
        private void SetupCustomUI()
        {
            this.Text = "Retail Inventory & Billing POS System (C# WinForms + ADO.NET)";
            this.Size = new Size(950, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 10);

            GroupBox grpAdd = new() { Text = "📦 Add New Stock (Inventory)", Location = new Point(20, 20), Size = new Size(430, 220) };
            grpAdd.Controls.Add(new Label { Text = "Product Name:", Location = new Point(15, 35), AutoSize = true });
            txtName.Location = new Point(140, 32); txtName.Width = 260; grpAdd.Controls.Add(txtName);

            grpAdd.Controls.Add(new Label { Text = "Category:", Location = new Point(15, 75), AutoSize = true });
            txtCategory.Location = new Point(140, 72); txtCategory.Width = 260; grpAdd.Controls.Add(txtCategory);

            grpAdd.Controls.Add(new Label { Text = "Price (₹):", Location = new Point(15, 115), AutoSize = true });
            txtPrice.Location = new Point(140, 112); txtPrice.Width = 110; grpAdd.Controls.Add(txtPrice);

            grpAdd.Controls.Add(new Label { Text = "Stock Qty:", Location = new Point(260, 115), AutoSize = true });
            txtStock.Location = new Point(340, 112); txtStock.Width = 60; grpAdd.Controls.Add(txtStock);

            Button btnAdd = new() { Text = "+ Add to Inventory", Location = new Point(140, 160), Size = new Size(260, 38), BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnAdd.Click += BtnAddProduct_Click;
            grpAdd.Controls.Add(btnAdd);
            this.Controls.Add(grpAdd);

            GroupBox grpBill = new() { Text = "🧾 Customer Billing & Invoice (SQL Transaction)", Location = new Point(470, 20), Size = new Size(440, 220) };
            grpBill.Controls.Add(new Label { Text = "Customer Name:", Location = new Point(15, 35), AutoSize = true });
            txtCustomer.Location = new Point(150, 32); txtCustomer.Width = 260; grpBill.Controls.Add(txtCustomer);

            grpBill.Controls.Add(new Label { Text = "Product ID:", Location = new Point(15, 75), AutoSize = true });
            txtBillProductId.Location = new Point(150, 72); txtBillProductId.Width = 100; grpBill.Controls.Add(txtBillProductId);

            grpBill.Controls.Add(new Label { Text = "Buy Quantity:", Location = new Point(15, 115), AutoSize = true });
            txtBillQty.Location = new Point(150, 112); txtBillQty.Width = 100; grpBill.Controls.Add(txtBillQty);

            Button btnBill = new() { Text = "⚡ Generate Invoice & Deduct Stock", Location = new Point(150, 160), Size = new Size(260, 38), BackColor = Color.FromArgb(40, 167, 69), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnBill.Click += BtnGenerateInvoice_Click;
            grpBill.Controls.Add(btnBill);
            this.Controls.Add(grpBill);

            lblAlert.Location = new Point(20, 255);
            lblAlert.AutoSize = true;
            lblAlert.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            this.Controls.Add(lblAlert);

            dgvInventory.Location = new Point(20, 285);
            dgvInventory.Size = new Size(890, 270);
            dgvInventory.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvInventory.ReadOnly = true;
            dgvInventory.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.Controls.Add(dgvInventory);
        }
    }
}