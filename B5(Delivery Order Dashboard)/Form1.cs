#nullable disable
using System;
using System.Drawing;
using System.Globalization;
using System.Collections.Generic;
using System.Windows.Forms;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace B5_Delivery_Order_Dashboard_
{
    public partial class Form1 : Form
    {
        // ===== KHAI BÁO CONTROL CHÍNH =====
        private SplitContainer splitMain;
        private TabControl tabInfo;
        private DataGridView dgvItems;
        private StatusStrip statusStrip;
        private ErrorProvider errorProvider;
        private System.Windows.Forms.Timer timer;

        // ===== BÊN TRÁI – KHÁCH HÀNG =====
        private TextBox txtName, txtPhone, txtAddress;

        // ===== BÊN TRÁI – VẬN CHUYỂN =====
        private RadioButton rbStandard, rbFast, rbExpress;

        // ===== STATUS STRIP =====
        private ToolStripStatusLabel lblTime;
        private ToolStripStatusLabel lblQty;
        private ToolStripStatusLabel lblWeight;
        private ToolStripStatusLabel lblMoney;

        // ===== ĐỊNH NGHĨA CỘT DATAGRIDVIEW =====
        private const string COL_NAME = "colName";
        private const string COL_QTY = "colQty";
        private const string COL_WEIGHT = "colWeight";
        private const string COL_PRICE = "colPrice";
        private const string COL_TOTAL = "colTotal";

        // Error visual helpers
        private ToolTip toolTip1;
        private readonly HashSet<string> invalidCells = new HashSet<string>();

        public Form1()
        {
            InitializeComponent();
            this.Load += Form1_Load;
        }

        // =========================================================
        //  BƯỚC 1: FORM LOAD
        // =========================================================
        private void Form1_Load(object sender, EventArgs e)
        {
            SetupSplitContainer();
            SetupLeftPanel();
            SetupRightPanel();
            SetupStatusStrip();
            SetupErrorProvider();
            SetupTimer();
            SetupKeyboardShortcuts();

            UpdateTotals();
        }

        // =========================================================
        //  BƯỚC 2: SPLITCONTAINER CHIA 2 CỘT
        // =========================================================
        private void SetupSplitContainer()
        {
            splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 6,
                SplitterDistance = 380    // Cột trái rộng 380px
            };
            this.Controls.Add(splitMain);
        }

        // =========================================================
        //  BƯỚC 3: CỘT TRÁI – THÔNG TIN KH + LOẠI VẬN CHUYỂN + TAB
        // =========================================================
        private void SetupLeftPanel()
        {
            // ----- GroupBox: Thông tin khách hàng -----
            GroupBox grpCustomer = new GroupBox
            {
                Text = "Thông tin khách hàng",
                Dock = DockStyle.Top,
                Height = 200,
                Padding = new Padding(10)
            };

            grpCustomer.Controls.Add(MakeLabel("Họ tên KH:", 15, 30));
            txtName = MakeTextBox(120, 27);
            grpCustomer.Controls.Add(txtName);

            grpCustomer.Controls.Add(MakeLabel("Số điện thoại:", 15, 65));
            txtPhone = MakeTextBox(120, 62);
            grpCustomer.Controls.Add(txtPhone);

            grpCustomer.Controls.Add(MakeLabel("Địa chỉ giao:", 15, 100));
            txtAddress = MakeTextBox(120, 97);
            grpCustomer.Controls.Add(txtAddress);

            splitMain.Panel1.Controls.Add(grpCustomer);

            // ----- GroupBox: Loại vận chuyển -----
            GroupBox grpShip = new GroupBox
            {
                Text = "Loại vận chuyển",
                Dock = DockStyle.Top,
                Height = 130,
                Padding = new Padding(10)
            };

            rbStandard = new RadioButton { Text = "Tiêu chuẩn (2-3 ngày)", Location = new Point(15, 25), AutoSize = true, Checked = true };
            rbFast = new RadioButton { Text = "Nhanh (24 giờ)", Location = new Point(15, 55), AutoSize = true };
            rbExpress = new RadioButton { Text = "Hỏa tốc (4 giờ)", Location = new Point(15, 85), AutoSize = true };
            grpShip.Controls.Add(rbStandard);
            grpShip.Controls.Add(rbFast);
            grpShip.Controls.Add(rbExpress);

            splitMain.Panel1.Controls.Add(grpShip);

            // ----- TabControl: Ghi chú / Hướng dẫn -----
            tabInfo = new TabControl { Dock = DockStyle.Fill };

            TabPage tabNote = new TabPage("Ghi chú");
            TextBox txtNote = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical,
                Text = "Ghi chú đơn hàng...\r\n(Ví dụ: giao giờ hành chính, gọi trước khi giao)"
            };
            tabNote.Controls.Add(txtNote);

            TabPage tabGuide = new TabPage("Hướng dẫn");
            Label lblGuide = new Label
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                Text = "PHÍM TẮT:\r\n" +
                       "  • F2   : Thêm dòng hàng mới\r\n" +
                       "  • Delete: Xóa dòng đang chọn\r\n\r\n" +
                       "LƯU Ý:\r\n" +
                       "  • Số lượng và Trọng lượng phải > 0\r\n" +
                       "  • Thành tiền = Số lượng × Đơn giá (tự tính)"
            };
            tabGuide.Controls.Add(lblGuide);

            tabInfo.TabPages.Add(tabNote);
            tabInfo.TabPages.Add(tabGuide);

            splitMain.Panel1.Controls.Add(tabInfo);

            // Đảm bảo thứ tự dock: tabInfo lấp đầy phần còn lại
            tabInfo.BringToFront();
        }

        // =========================================================
        //  BƯỚC 4: CỘT PHẢI – DATAGRIDVIEW CHI TIẾT ĐƠN HÀNG
        // =========================================================
        private void SetupRightPanel()
        {
            // Label tiêu đề
            Label lblTitle = new Label
            {
                Text = "CHI TIẾT ĐƠN HÀNG",
                Dock = DockStyle.Top,
                Height = 35,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = Color.DarkBlue,
                Padding = new Padding(8, 0, 0, 0)
            };
            splitMain.Panel2.Controls.Add(lblTitle);

            // ToolStrip with buttons (adds above the grid)
            ToolStrip toolStrip = new ToolStrip
            {
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden,
                BackColor = Color.FromArgb(245, 245, 245),
                ImageScalingSize = new Size(20, 20),
                Padding = new Padding(4)
            };

            ToolStripButton btnAdd = new ToolStripButton
            {
                Text = "➕ Thêm dòng (F2)",
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.DarkGreen,
                AutoSize = true
            };
            btnAdd.Click += (s, e) => AddNewRow();

            ToolStripButton btnDel = new ToolStripButton
            {
                Text = "🗑️ Xóa dòng (Delete)",
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.DarkRed,
                AutoSize = true
            };
            btnDel.Click += (s, e) => DeleteCurrentRow();

            ToolStripButton btnClearAll = new ToolStripButton
            {
                Text = "🧹 Xóa tất cả",
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.DarkOrange,
                AutoSize = true
            };
            btnClearAll.Click += (s, e) =>
            {
                if (MessageBox.Show("Xóa tất cả dòng hàng?", "Xác nhận",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    dgvItems.Rows.Clear();
                    UpdateTotals();
                }
            };

            toolStrip.Items.Add(btnAdd);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(btnDel);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(btnClearAll);

            // Label hướng dẫn dưới cùng
            Label lblHint = new Label
            {
                Text = "💡 Phím tắt: F2 = Thêm dòng | Delete = Xóa dòng | Hoặc dùng các nút trên thanh công cụ",
                Dock = DockStyle.Bottom,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.DimGray,
                Padding = new Padding(8, 0, 0, 0)
            };

            // DataGridView
            dgvItems = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                Font = new Font("Segoe UI", 10),
                BackgroundColor = Color.White,
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2
            };

            // Cột Tên hàng
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = COL_NAME,
                HeaderText = "Tên hàng",
                FillWeight = 35
            });

            // Cột Số lượng
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = COL_QTY,
                HeaderText = "Số lượng",
                FillWeight = 15,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            // Cột Trọng lượng
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = COL_WEIGHT,
                HeaderText = "Trọng lượng (kg)",
                FillWeight = 20,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            // Cột Đơn giá
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = COL_PRICE,
                HeaderText = "Đơn giá (VNĐ)",
                FillWeight = 20,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            // Cột Thành tiền (tự tính – khóa)
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = COL_TOTAL,
                HeaderText = "Thành tiền (VNĐ)",
                FillWeight = 20,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    BackColor = Color.FromArgb(240, 240, 240),
                    ForeColor = Color.DarkRed
                }
            });

            // Thêm vài dòng mẫu
            dgvItems.Rows.Add("Thùng carton", 5, 2.5, 25000, 0);
            dgvItems.Rows.Add("Balo da", 2, 1.2, 450000, 0);

            // Gắn sự kiện
            dgvItems.CellValueChanged += DgvItems_CellValueChanged;
            dgvItems.CellValidating += DgvItems_CellValidating;
            dgvItems.CellBeginEdit += DgvItems_CellBeginEdit;
            dgvItems.CellEndEdit += DgvItems_CellEndEdit;
            dgvItems.CellPainting += DgvItems_CellPainting;
            dgvItems.RowsRemoved += (s, ev) => UpdateTotals();
            dgvItems.PreviewKeyDown += DgvItems_PreviewKeyDown;
            dgvItems.DataError += (s, ev) => { ev.ThrowException = false; };

            // Cho CellValueChanged bắn ngay khi rời ô (live update)
            dgvItems.CurrentCellDirtyStateChanged += (s, ev) =>
            {
                if (dgvItems.IsCurrentCellDirty)
                    dgvItems.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            // Add controls in order: title (already added), toolStrip, grid, hint
            splitMain.Panel2.Controls.Add(toolStrip);
            splitMain.Panel2.Controls.Add(dgvItems);
            splitMain.Panel2.Controls.Add(lblHint);

            toolStrip.BringToFront();
            dgvItems.BringToFront();
        }

        // =========================================================
        //  BƯỚC 5: STATUSSTRIP + TIMER
        // =========================================================
        private void SetupStatusStrip()
        {
            statusStrip = new StatusStrip();
            statusStrip.BackColor = Color.FromArgb(230, 240, 250);

            lblTime = new ToolStripStatusLabel("🕒 --:--:--");
            lblQty = new ToolStripStatusLabel("📦 SL: 0");
            lblWeight = new ToolStripStatusLabel("⚖️ KL: 0.00 kg");
            lblMoney = new ToolStripStatusLabel("💰 Tổng: 0 VNĐ");

            lblMoney.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            lblMoney.ForeColor = Color.DarkRed;
            lblTime.Font = new Font("Segoe UI", 9, FontStyle.Bold);

            statusStrip.Items.Add(lblTime);
            statusStrip.Items.Add(new ToolStripStatusLabel("  |  "));
            statusStrip.Items.Add(lblQty);
            statusStrip.Items.Add(new ToolStripStatusLabel("  |  "));
            statusStrip.Items.Add(lblWeight);
            statusStrip.Items.Add(new ToolStripStatusLabel("  |  "));
            statusStrip.Items.Add(lblMoney);

            // Cho tiền đẩy về bên phải
            lblMoney.Spring = true;
            lblMoney.TextAlign = ContentAlignment.MiddleRight;

            this.Controls.Add(statusStrip);
        }

        private void SetupTimer()
        {
            timer = new System.Windows.Forms.Timer { Interval = 1000 };
            timer.Tick += (s, e) =>
            {
                lblTime.Text = "🕒 " + DateTime.Now.ToString("HH:mm:ss  dd/MM/yyyy");
            };
            timer.Start();
            // Bắn ngay 1 lần
            lblTime.Text = "🕒 " + DateTime.Now.ToString("HH:mm:ss  dd/MM/yyyy");
        }

        // =========================================================
        //  BƯỚC 6: ERRORPROVIDER
        // =========================================================
        private void SetupErrorProvider()
        {
            errorProvider = new ErrorProvider
            {
                BlinkStyle = ErrorBlinkStyle.AlwaysBlink,
                BlinkRate = 400
            };
            errorProvider.ContainerControl = this;
            // For the whole DataGridView (fallback)
            errorProvider.SetIconAlignment(dgvItems, ErrorIconAlignment.MiddleLeft);
            errorProvider.SetIconPadding(dgvItems, 8);

            toolTip1 = new ToolTip
            {
                InitialDelay = 200,
                ReshowDelay = 100,
                AutoPopDelay = 5000,
                ShowAlways = true
            };
        }

        // =========================================================
        //  BƯỚC 7: PHÍM TẮT F2 / DELETE TRÊN DATAGRIDVIEW
        // =========================================================
        private void SetupKeyboardShortcuts()
        {
            // Keyboard handling is done via PreviewKeyDown on the grid and
            // a form-level KeyDown handler (KeyPreview=true) for reliable capture.
            this.KeyPreview = true;
            this.KeyDown += Form_KeyDown;
        }
        private void DgvItems_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.F2)
                AddNewRow();
            else if (e.KeyCode == Keys.Delete)
                DeleteCurrentRow();
        }

        private void AddNewRow()
        {
            dgvItems.Rows.Add("Hàng mới", 1, 1.0, 0, 0);
            int last = dgvItems.Rows.Count - 1;
            if (last >= 0)
            {
                dgvItems.CurrentCell = dgvItems.Rows[last].Cells[COL_NAME];
                dgvItems.BeginEdit(true);
            }
            UpdateTotals();
        }

        private void DeleteCurrentRow()
        {
            if (dgvItems.CurrentRow == null) return;
            if (dgvItems.CurrentRow.IsNewRow) return;
            dgvItems.Rows.Remove(dgvItems.CurrentRow);
            UpdateTotals();
        }

        private void Form_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F2)
            {
                AddNewRow();
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Delete && dgvItems.Focused && dgvItems.CurrentRow != null)
            {
                if (!dgvItems.IsCurrentCellInEditMode)
                {
                    DeleteCurrentRow();
                    e.Handled = true;
                    return;
                }
            }
        }

        // =========================================================
        //  BƯỚC 8: VALIDATION (ErrorProvider)
        // =========================================================
        private void DgvItems_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (this.IsDisposed || this.Disposing) return;
            if (dgvItems == null || dgvItems.IsDisposed) return;
            if (e.RowIndex < 0) return;

            string colName = dgvItems.Columns[e.ColumnIndex].Name;
            if (colName != COL_QTY && colName != COL_WEIGHT) return;

            string raw = e.FormattedValue == null ? "" : e.FormattedValue.ToString().Trim();
            string errMsg = null;

            if (string.IsNullOrEmpty(raw))
                errMsg = "Không được để trống!";
            else
            {
                double num;
                bool ok = double.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out num)
                       || double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out num);
                if (!ok) errMsg = "Phải là số hợp lệ!";
                else if (num <= 0) errMsg = "Giá trị phải LỚN HƠN 0!";
            }

            if (errMsg != null)
            {
                MarkCellInvalid(e.RowIndex, e.ColumnIndex, errMsg);
                e.Cancel = true;
            }
            else
            {
                ClearCellInvalid(e.RowIndex, e.ColumnIndex);
            }
        }

        private string CellKey(int rowIndex, int colIndex)
        {
            return rowIndex + ":" + colIndex;
        }

        private void MarkCellInvalid(int rowIndex, int colIndex, string message)
        {
            string key = CellKey(rowIndex, colIndex);
            invalidCells.Add(key);

            if (rowIndex < 0 || colIndex < 0) return;
            DataGridViewCell cell = dgvItems.Rows[rowIndex].Cells[colIndex];
            cell.Style.BackColor = Color.FromArgb(255, 224, 224);  // light pink
            cell.Style.ForeColor = Color.DarkRed;
            cell.Style.SelectionBackColor = Color.FromArgb(255, 180, 180);
            cell.Style.SelectionForeColor = Color.DarkRed;

            // Tooltip on cell
            cell.ToolTipText = "⚠ " + message;

            // Show icon next to the cell's editing TextBox
            Control ctrl = dgvItems.EditingControl as Control;
            if (ctrl != null)
            {
                errorProvider.SetIconAlignment(ctrl, ErrorIconAlignment.MiddleLeft);
                errorProvider.SetIconPadding(ctrl, 6);
                errorProvider.SetError(ctrl, message);
                toolTip1?.SetToolTip(ctrl, "⚠ " + message);
            }
            else
            {
                errorProvider.SetError(dgvItems, message);
            }
            dgvItems.InvalidateCell(cell);
        }

        private void ClearCellInvalid(int rowIndex, int colIndex)
        {
            string key = CellKey(rowIndex, colIndex);
            invalidCells.Remove(key);

            if (rowIndex < 0 || colIndex < 0) return;
            DataGridViewCell cell = dgvItems.Rows[rowIndex].Cells[colIndex];
            cell.Style.BackColor = Color.Empty;
            cell.Style.ForeColor = Color.Empty;
            cell.Style.SelectionBackColor = Color.Empty;
            cell.Style.SelectionForeColor = Color.Empty;
            cell.ToolTipText = "";

            Control ctrl = dgvItems.EditingControl as Control;
            if (ctrl != null)
            {
                errorProvider.SetError(ctrl, "");
                toolTip1?.SetToolTip(ctrl, "");
            }
            errorProvider.SetError(dgvItems, "");
            dgvItems.InvalidateCell(cell);
        }

        private void DgvItems_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            // Clear previous errors when entering edit mode
            if (errorProvider != null)
            {
                errorProvider.SetError(dgvItems, "");
                Control ctrl = dgvItems.EditingControl as Control;
                if (ctrl != null)
                {
                    errorProvider.SetError(ctrl, "");
                    toolTip1?.SetToolTip(ctrl, "");
                }
            }
        }

        private void DgvItems_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            string colName = dgvItems.Columns[e.ColumnIndex].Name;
            if (colName != COL_QTY && colName != COL_WEIGHT) return;

            object val = dgvItems.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
            double num = 0;
            bool ok = val != null && (double.TryParse(val.ToString(), NumberStyles.Any,
                CultureInfo.CurrentCulture, out num) ||
                double.TryParse(val.ToString(), NumberStyles.Any,
                CultureInfo.InvariantCulture, out num));

            if (ok && num > 0)
            {
                ClearCellInvalid(e.RowIndex, e.ColumnIndex);
            }
        }

        // =========================================================
        //  BƯỚC 9: KHI Ô CELL THAY ĐỔI -> TÍNH LẠI
        // =========================================================
        private void DgvItems_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            // Nếu là cột Số lượng / Đơn giá → tính lại Thành tiền của dòng đó
            string col = dgvItems.Columns[e.ColumnIndex].Name;
            if (col == COL_QTY || col == COL_PRICE || col == COL_WEIGHT)
            {
                RecalcRowTotal(e.RowIndex);
            }

            UpdateTotals();
        }

        private void DgvItems_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (!invalidCells.Contains(CellKey(e.RowIndex, e.ColumnIndex))) return;

            e.PaintBackground(e.ClipBounds, true);
            e.PaintContent(e.ClipBounds);

            // Draw a bright red border around the invalid cell
            using (Pen p = new Pen(Color.Red, 2))
            {
                Rectangle r = new Rectangle(e.CellBounds.X + 1, e.CellBounds.Y + 1,
                                            e.CellBounds.Width - 3, e.CellBounds.Height - 3);
                e.Graphics.DrawRectangle(p, r);
            }
            e.Handled = true;
        }

        private void RecalcRowTotal(int rowIndex)
        {
            DataGridViewRow row = dgvItems.Rows[rowIndex];
            if (row.IsNewRow) return;

            double qty = ParseDouble(row.Cells[COL_QTY].Value);
            double price = ParseDouble(row.Cells[COL_PRICE].Value);

            double total = qty * price;
            row.Cells[COL_TOTAL].Value = total;
        }

        // =========================================================
        //  BƯỚC 10: CẬP NHẬT STATUSSTRIP (TỔNG SL / KL / TIỀN)
        // =========================================================
        private void UpdateTotals()
        {
            double totalQty = 0;
            double totalWeight = 0;
            double totalMoney = 0;

            foreach (DataGridViewRow row in dgvItems.Rows)
            {
                if (row.IsNewRow) continue;

                double qty = ParseDouble(row.Cells[COL_QTY].Value);
                double weight = ParseDouble(row.Cells[COL_WEIGHT].Value);
                double money = ParseDouble(row.Cells[COL_TOTAL].Value);

                totalQty += qty;
                totalWeight += weight;
                totalMoney += money;
            }

            lblQty.Text = "📦 SL: " + totalQty.ToString("N0");
            lblWeight.Text = "⚖️ KL: " + totalWeight.ToString("N2") + " kg";
            lblMoney.Text = "💰 Tổng: " + totalMoney.ToString("N0") + " VNĐ";
        }

        // =========================================================
        //  HELPERS
        // =========================================================
        private double ParseDouble(object val)
        {
            if (val == null) return 0;
            double d;
            if (double.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.CurrentCulture, out d))
                return d;
            if (double.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out d))
                return d;
            return 0;
        }

        private Label MakeLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
        }

        private TextBox MakeTextBox(int x, int y)
        {
            return new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(230, 27),
                Font = new Font("Segoe UI", 10)
            };
        }
    }
}