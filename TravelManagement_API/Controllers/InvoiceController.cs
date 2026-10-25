using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TravelManagement.API.Reports;
using TravelManagement.DataAccessLayer.Repository.Interface;

namespace TravelManagement.API.Controllers
{
    /// <summary>Customer invoices: one booking, its trip, or all bookings of the customer.</summary>
    [Authorize(Roles = "Admin")]
    [Route("api/Booking")]
    [EnableRateLimiting("api")]
    public class InvoiceController : ApiControllerBase
    {
        private readonly IInvoiceRepository _invoices;

        public InvoiceController(IInvoiceRepository invoices) => _invoices = invoices;

        /// <summary>The invoice contents, for the on-screen preview.</summary>
        [HttpGet("{id:int}/invoice-data")]
        public async Task<IActionResult> Data(int id, [FromQuery] string? scope, [FromQuery] bool includeTerms = false) =>
            ApiOk(await _invoices.GetAsync(id, scope, includeTerms));

        /// <summary>The invoice as a PDF (download or share to WhatsApp).</summary>
        [HttpGet("{id:int}/invoice")]
        public async Task<IActionResult> Pdf(int id, [FromQuery] string? scope, [FromQuery] bool includeTerms = false)
        {
            var data = await _invoices.GetAsync(id, scope, includeTerms);
            var pdf = CustomerInvoicePdf.Generate(data);
            var name = new string(data.CustomerName.Where(char.IsLetterOrDigit).ToArray());
            return ApiFile(pdf, "application/pdf", $"Invoice_{data.InvoiceNo}_{(name.Length > 0 ? name : "Customer")}.pdf");
        }
    }
}
