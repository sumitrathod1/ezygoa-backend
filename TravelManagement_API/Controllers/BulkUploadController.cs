using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;

namespace TravelManagement.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    public class BulkUploadController : ApiControllerBase
    {
        private readonly AppDbContext _db;

        public BulkUploadController(AppDbContext db)
        {
            _db = db;
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        // ── GET api/bulkupload/template/{type} ────────────────────────────────
        [HttpGet("template/{type}")]
        public async Task<IActionResult> DownloadTemplate(string type)
        {
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Data");

            var vehicles = await _db.Vehicles
                .Where(v => v.VehicleName != null)
                .Select(v => v.VehicleName!)
                .ToListAsync();

            var drivers = await _db.Users
                .Where(u => u.Role == Role.Employee && !u.IsDeleted)
                .Select(u => u.EmployeeName)
                .ToListAsync();

            var agents = await _db.TravelAgents
                .Where(a => a.IsActive)
                .Select(a => a.Name)
                .ToListAsync();

            switch (type.ToLower())
            {
                case "booking":
                    BuildBookingTemplate(ws, vehicles, drivers, agents);
                    break;
                case "expense":
                    BuildExpenseTemplate(ws, vehicles);
                    break;
                case "document":
                    BuildDocumentTemplate(ws, vehicles, drivers);
                    break;
                case "maintenance":
                    BuildMaintenanceTemplate(ws, vehicles);
                    break;
                default:
                    return ApiBadRequest("Unknown type. Use: booking, expense, document, maintenance");
            }

            var bytes = package.GetAsByteArray();
            return ApiFile(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"{type}_template.xlsx");
        }

        // ── POST api/bulkupload/upload/{type} ─────────────────────────────────
        [HttpPost("upload/{type}")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> Upload(string type, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return ApiBadRequest("No file uploaded.");

            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                return ApiBadRequest("Only .xlsx files are supported.");

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var stream = file.OpenReadStream();
            using var package = new ExcelPackage(stream);
            var ws = package.Workbook.Worksheets.FirstOrDefault();
            if (ws == null)
                return ApiBadRequest("Excel file has no worksheets.");

            var result = new UploadResult();

            switch (type.ToLower())
            {
                case "booking":
                    await ProcessBookings(ws, result);
                    break;
                case "expense":
                    await ProcessExpenses(ws, result);
                    break;
                case "document":
                    await ProcessDocuments(ws, result);
                    break;
                case "maintenance":
                    await ProcessMaintenance(ws, result);
                    break;
                default:
                    return ApiBadRequest("Unknown type.");
            }

            return ApiOk(result);
        }

        // ════════════════════════════════════════════════════════════════════
        // TEMPLATE BUILDERS
        // ════════════════════════════════════════════════════════════════════

        private static void StyleHeader(ExcelWorksheet ws, int cols)
        {
            using var range = ws.Cells[1, 1, 1, cols];
            range.Style.Font.Bold = true;
            range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(13, 110, 110));
            range.Style.Font.Color.SetColor(System.Drawing.Color.White);
        }

        private static void AddDropdown(ExcelWorksheet ws, int col, int fromRow, int toRow, IList<string> list)
        {
            if (list.Count == 0) return;
            var addr = ws.Cells[fromRow, col, toRow, col].Address;
            var v = ws.DataValidations.AddListValidation(addr);
            v.ShowErrorMessage = true;
            v.ErrorTitle = "Invalid value";
            v.Error = "Please choose from the dropdown list.";
            foreach (var item in list) v.Formula.Values.Add(item);
        }

        private static void AddEnumDropdown(ExcelWorksheet ws, int col, int fromRow, int toRow, string[] values)
            => AddDropdown(ws, col, fromRow, toRow, values);

        private void BuildBookingTemplate(ExcelWorksheet ws, List<string> vehicles, List<string> drivers, List<string> agents)
        {
            string[] headers = {
                "Customer Name *", "Customer Phone *", "Travel Date * (YYYY-MM-DD)",
                "Travel Time * (HH:MM)", "Booking Type *", "From Location *",
                "To Location *", "Vehicle Name *", "Driver Name *",
                "Agent Name", "Pax Count *", "Total Amount *", "Advance Amount", "Notes"
            };
            for (int i = 0; i < headers.Length; i++)
                ws.Cells[1, i + 1].Value = headers[i];
            StyleHeader(ws, headers.Length);

            ws.Cells[2, 1].Value = "John Doe";
            ws.Cells[2, 2].Value = "9876543210";
            ws.Cells[2, 3].Value = DateTime.Today.ToString("yyyy-MM-dd");
            ws.Cells[2, 4].Value = "10:00";
            ws.Cells[2, 5].Value = "AirportPickup";
            ws.Cells[2, 6].Value = "Goa Airport";
            ws.Cells[2, 7].Value = "Panaji";
            ws.Cells[2, 8].Value = vehicles.FirstOrDefault() ?? "Swift Dezire";
            ws.Cells[2, 9].Value = drivers.FirstOrDefault() ?? "Driver Name";
            ws.Cells[2, 10].Value = "";
            ws.Cells[2, 11].Value = 2;
            ws.Cells[2, 12].Value = 800;
            ws.Cells[2, 13].Value = 300;
            ws.Cells[2, 14].Value = "";

            AddEnumDropdown(ws, 5, 3, 1000, new[] { "AirportPickup", "AirportDrop", "SightSeeing", "Shuttle", "FullDay", "RailwayStation", "Notspecified" });
            if (vehicles.Count > 0) AddDropdown(ws, 8, 3, 1000, vehicles);
            if (drivers.Count > 0) AddDropdown(ws, 9, 3, 1000, drivers);
            if (agents.Count > 0) AddDropdown(ws, 10, 3, 1000, agents);

            ws.Cells.AutoFitColumns();
        }

        private void BuildExpenseTemplate(ExcelWorksheet ws, List<string> vehicles)
        {
            string[] headers = { "Vehicle Name *", "Expense Date * (YYYY-MM-DD)", "Expense Type *", "Amount *", "Notes" };
            for (int i = 0; i < headers.Length; i++)
                ws.Cells[1, i + 1].Value = headers[i];
            StyleHeader(ws, headers.Length);

            ws.Cells[2, 1].Value = vehicles.FirstOrDefault() ?? "Vehicle Name";
            ws.Cells[2, 2].Value = DateTime.Today.ToString("yyyy-MM-dd");
            ws.Cells[2, 3].Value = "Fuel";
            ws.Cells[2, 4].Value = 500;

            if (vehicles.Count > 0) AddDropdown(ws, 1, 3, 1000, vehicles);
            AddEnumDropdown(ws, 3, 3, 1000, new[] { "Repair", "Fuel", "Towing", "DocumentRenew" });

            ws.Cells.AutoFitColumns();
        }

        private void BuildDocumentTemplate(ExcelWorksheet ws, List<string> vehicles, List<string> drivers)
        {
            string[] headers = {
                "Category * (Vehicle/Driver/Company)", "Vehicle Name (if Vehicle)", "Driver Name (if Driver)",
                "Document Type *", "Document Number *", "Issue Date (YYYY-MM-DD)",
                "Expiry Date (YYYY-MM-DD)", "Notes"
            };
            for (int i = 0; i < headers.Length; i++)
                ws.Cells[1, i + 1].Value = headers[i];
            StyleHeader(ws, headers.Length);

            ws.Cells[2, 1].Value = "Vehicle";
            ws.Cells[2, 2].Value = vehicles.FirstOrDefault() ?? "Vehicle Name";
            ws.Cells[2, 4].Value = "Insurance";
            ws.Cells[2, 5].Value = "DOC123456";
            ws.Cells[2, 6].Value = DateTime.Today.ToString("yyyy-MM-dd");
            ws.Cells[2, 7].Value = DateTime.Today.AddYears(1).ToString("yyyy-MM-dd");

            AddEnumDropdown(ws, 1, 3, 1000, new[] { "Vehicle", "Driver", "Company" });
            if (vehicles.Count > 0) AddDropdown(ws, 2, 3, 1000, vehicles);
            if (drivers.Count > 0) AddDropdown(ws, 3, 3, 1000, drivers);
            AddEnumDropdown(ws, 4, 3, 1000, new[] { "RC", "Insurance", "PUC", "Permit", "Licence", "Fitness", "Other" });

            ws.Cells.AutoFitColumns();
        }

        private void BuildMaintenanceTemplate(ExcelWorksheet ws, List<string> vehicles)
        {
            string[] headers = { "Vehicle Name *", "Maintenance Type *", "Due Date * (YYYY-MM-DD)", "Notes" };
            for (int i = 0; i < headers.Length; i++)
                ws.Cells[1, i + 1].Value = headers[i];
            StyleHeader(ws, headers.Length);

            ws.Cells[2, 1].Value = vehicles.FirstOrDefault() ?? "Vehicle Name";
            ws.Cells[2, 2].Value = "Service";
            ws.Cells[2, 3].Value = DateTime.Today.AddMonths(3).ToString("yyyy-MM-dd");

            if (vehicles.Count > 0) AddDropdown(ws, 1, 3, 1000, vehicles);
            AddEnumDropdown(ws, 2, 3, 1000, new[] { "oilChange", "TireChange", "Service" });

            ws.Cells.AutoFitColumns();
        }

        // ════════════════════════════════════════════════════════════════════
        // UPLOAD PROCESSORS
        // ════════════════════════════════════════════════════════════════════

        private async Task ProcessBookings(ExcelWorksheet ws, UploadResult result)
        {
            var vehicleMap = await _db.Vehicles.ToDictionaryAsync(v => v.VehicleName ?? "", v => v.VehicleId);
            var driverMap = await _db.Users
                .Where(u => u.Role == Role.Employee && !u.IsDeleted)
                .ToDictionaryAsync(u => u.EmployeeName, u => u.userId);
            var agentMap = await _db.TravelAgents
                .ToDictionaryAsync(a => a.Name, a => a.AgentId);

            for (int row = 3; row <= ws.Dimension?.End.Row; row++)
            {
                result.Total++;
                try
                {
                    var custName = ws.Cells[row, 1].Text.Trim();
                    var custPhone = ws.Cells[row, 2].Text.Trim();
                    var dateStr = ws.Cells[row, 3].Text.Trim();
                    var timeStr = ws.Cells[row, 4].Text.Trim();
                    var typeStr = ws.Cells[row, 5].Text.Trim();
                    var from = ws.Cells[row, 6].Text.Trim();
                    var to = ws.Cells[row, 7].Text.Trim();
                    var vehicleName = ws.Cells[row, 8].Text.Trim();
                    var driverName = ws.Cells[row, 9].Text.Trim();
                    var agentName = ws.Cells[row, 10].Text.Trim();
                    var paxStr = ws.Cells[row, 11].Text.Trim();
                    var amountStr = ws.Cells[row, 12].Text.Trim();

                    if (string.IsNullOrWhiteSpace(custName) && string.IsNullOrWhiteSpace(custPhone))
                        break;

                    var errors = new List<string>();
                    if (string.IsNullOrWhiteSpace(custName)) errors.Add("Customer Name required");
                    if (string.IsNullOrWhiteSpace(custPhone)) errors.Add("Customer Phone required");
                    if (!DateOnly.TryParse(dateStr, out var travelDate)) errors.Add($"Invalid Travel Date: '{dateStr}'");
                    if (!TimeOnly.TryParse(timeStr, out var travelTime)) errors.Add($"Invalid Travel Time: '{timeStr}'");
                    if (!Enum.TryParse<BookingType>(typeStr, true, out var bookingType)) errors.Add($"Invalid Booking Type: '{typeStr}'");
                    if (!decimal.TryParse(amountStr, out var amount)) errors.Add($"Invalid Amount: '{amountStr}'");
                    if (!int.TryParse(paxStr, out var pax)) pax = 1;
                    if (!vehicleMap.TryGetValue(vehicleName, out var vehicleId)) errors.Add($"Vehicle not found: '{vehicleName}'");
                    if (!driverMap.TryGetValue(driverName, out var driverId)) errors.Add($"Driver not found: '{driverName}'");

                    if (errors.Count > 0)
                    {
                        result.Failed++;
                        result.Errors.Add(new RowError { Row = row, Messages = errors });
                        continue;
                    }

                    var customer = await _db.Customers.FirstOrDefaultAsync(c => c.CustomerNumber == custPhone);
                    if (customer == null)
                    {
                        customer = new Customers { CustomerName = custName, CustomerNumber = custPhone, TravelDate = travelDate };
                        _db.Customers.Add(customer);
                        await _db.SaveChangesAsync();
                    }

                    int? agentId = null;
                    if (!string.IsNullOrWhiteSpace(agentName) && agentMap.TryGetValue(agentName, out var aid))
                        agentId = aid;

                    _db.Bookings.Add(new Booking
                    {
                        travelDate = travelDate,
                        Traveltime = travelTime,
                        BookingType = bookingType,
                        From = from,
                        To = to,
                        VehicleId = vehicleId,
                        Userid = driverId,
                        Amount = amount,
                        Pax = pax,
                        CustomerID = customer.CustomersId,
                        TravelAgentId = agentId,
                        Status = Status.Pending,
                        Payment = Payment.Admin,
                        Assigned = true,
                    });
                    await _db.SaveChangesAsync();
                    result.Successful++;
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Errors.Add(new RowError { Row = row, Messages = new List<string> { ex.Message } });
                }
            }
        }

        private async Task ProcessExpenses(ExcelWorksheet ws, UploadResult result)
        {
            var vehicleMap = await _db.Vehicles.ToDictionaryAsync(v => v.VehicleName ?? "", v => v.VehicleId);

            for (int row = 3; row <= ws.Dimension?.End.Row; row++)
            {
                result.Total++;
                try
                {
                    var vehicleName = ws.Cells[row, 1].Text.Trim();
                    var dateStr = ws.Cells[row, 2].Text.Trim();
                    var categoryStr = ws.Cells[row, 3].Text.Trim();
                    var amountStr = ws.Cells[row, 4].Text.Trim();

                    if (string.IsNullOrWhiteSpace(vehicleName) && string.IsNullOrWhiteSpace(amountStr))
                        break;

                    var errors = new List<string>();
                    if (!vehicleMap.TryGetValue(vehicleName, out var vehicleId)) errors.Add($"Vehicle not found: '{vehicleName}'");
                    if (!DateTime.TryParse(dateStr, out var expDate)) errors.Add($"Invalid Date: '{dateStr}'");
                    if (!Enum.TryParse<Category>(categoryStr, true, out var category)) errors.Add($"Invalid Type: '{categoryStr}'");
                    if (!decimal.TryParse(amountStr, out var amount)) errors.Add($"Invalid Amount: '{amountStr}'");

                    if (errors.Count > 0)
                    {
                        result.Failed++;
                        result.Errors.Add(new RowError { Row = row, Messages = errors });
                        continue;
                    }

                    _db.vehicleExpences.Add(new VehicleExpence
                    {
                        VehicleID = vehicleId,
                        ExpenseDate = expDate,
                        CategoryType = category,
                        Amount = amount,
                    });
                    await _db.SaveChangesAsync();
                    result.Successful++;
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Errors.Add(new RowError { Row = row, Messages = new List<string> { ex.Message } });
                }
            }
        }

        private async Task ProcessDocuments(ExcelWorksheet ws, UploadResult result)
        {
            var vehicleMap = await _db.Vehicles.ToDictionaryAsync(v => v.VehicleName ?? "", v => (int?)v.VehicleId);

            for (int row = 3; row <= ws.Dimension?.End.Row; row++)
            {
                result.Total++;
                try
                {
                    var category = ws.Cells[row, 1].Text.Trim();
                    var vehicleName = ws.Cells[row, 2].Text.Trim();
                    var driverName = ws.Cells[row, 3].Text.Trim();
                    var docType = ws.Cells[row, 4].Text.Trim();
                    var docNumber = ws.Cells[row, 5].Text.Trim();
                    var issueDateStr = ws.Cells[row, 6].Text.Trim();
                    var expiryDateStr = ws.Cells[row, 7].Text.Trim();
                    var notes = ws.Cells[row, 8].Text.Trim();

                    if (string.IsNullOrWhiteSpace(category) && string.IsNullOrWhiteSpace(docType))
                        break;

                    var errors = new List<string>();
                    if (string.IsNullOrWhiteSpace(category)) errors.Add("Category required");
                    if (string.IsNullOrWhiteSpace(docType)) errors.Add("Document Type required");
                    if (string.IsNullOrWhiteSpace(docNumber)) errors.Add("Document Number required");

                    int? vehicleId = null;
                    if (category == "Vehicle")
                    {
                        if (!vehicleMap.TryGetValue(vehicleName, out vehicleId))
                            errors.Add($"Vehicle not found: '{vehicleName}'");
                    }

                    DateTime? issueDate = null;
                    if (!string.IsNullOrWhiteSpace(issueDateStr) && DateTime.TryParse(issueDateStr, out var id))
                        issueDate = id;

                    bool hasExpiry = !string.IsNullOrWhiteSpace(expiryDateStr);
                    DateTime? expiryDate = null;
                    if (hasExpiry && DateTime.TryParse(expiryDateStr, out var ed))
                        expiryDate = ed;
                    else if (hasExpiry)
                        errors.Add($"Invalid Expiry Date: '{expiryDateStr}'");

                    if (errors.Count > 0)
                    {
                        result.Failed++;
                        result.Errors.Add(new RowError { Row = row, Messages = errors });
                        continue;
                    }

                    _db.Documents.Add(new Documents
                    {
                        Category = category,
                        VehicleID = vehicleId,
                        DriverName = category == "Driver" ? driverName : null,
                        DocumentType = docType,
                        DocumentNumber = docNumber,
                        IssueDate = issueDate,
                        ExpiryDate = expiryDate,
                        HasExpiry = hasExpiry,
                        Description = notes,
                        Title = docType,
                    });
                    await _db.SaveChangesAsync();
                    result.Successful++;
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Errors.Add(new RowError { Row = row, Messages = new List<string> { ex.Message } });
                }
            }
        }

        private async Task ProcessMaintenance(ExcelWorksheet ws, UploadResult result)
        {
            var vehicleMap = await _db.Vehicles.ToDictionaryAsync(v => v.VehicleName ?? "", v => v.VehicleId);

            for (int row = 3; row <= ws.Dimension?.End.Row; row++)
            {
                result.Total++;
                try
                {
                    var vehicleName = ws.Cells[row, 1].Text.Trim();
                    var typeStr = ws.Cells[row, 2].Text.Trim();
                    var dueDateStr = ws.Cells[row, 3].Text.Trim();
                    var notes = ws.Cells[row, 4].Text.Trim();

                    if (string.IsNullOrWhiteSpace(vehicleName) && string.IsNullOrWhiteSpace(dueDateStr))
                        break;

                    var errors = new List<string>();
                    if (!vehicleMap.TryGetValue(vehicleName, out var vehicleId)) errors.Add($"Vehicle not found: '{vehicleName}'");
                    if (!Enum.TryParse<MaintenanceType>(typeStr, true, out var mType)) errors.Add($"Invalid Maintenance Type: '{typeStr}'");
                    if (!DateTime.TryParse(dueDateStr, out var dueDate)) errors.Add($"Invalid Due Date: '{dueDateStr}'");

                    if (errors.Count > 0)
                    {
                        result.Failed++;
                        result.Errors.Add(new RowError { Row = row, Messages = errors });
                        continue;
                    }

                    _db.vehicleMaintenanceShedules.Add(new VehicleMaintenanceShedule
                    {
                        VehicleID = vehicleId,
                        maintenanceType = mType,
                        Nextduedate = dueDate,
                        ServieDate = DateTime.Today,
                        Description = notes,
                        cost = 0,
                    });
                    await _db.SaveChangesAsync();
                    result.Successful++;
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Errors.Add(new RowError { Row = row, Messages = new List<string> { ex.Message } });
                }
            }
        }

        private class UploadResult
        {
            public int Total { get; set; }
            public int Successful { get; set; }
            public int Failed { get; set; }
            public List<RowError> Errors { get; set; } = new();
        }

        private class RowError
        {
            public int Row { get; set; }
            public List<string> Messages { get; set; } = new();
        }
    }
}
