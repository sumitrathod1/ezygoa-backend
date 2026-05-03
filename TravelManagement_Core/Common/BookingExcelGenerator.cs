using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using TravelManagement.Core.Models;

namespace TravelManagement.Core.Common
{
    public class BookingExcelGenerator
    {
        public static byte[] Generate(List<Booking> bookings, string agentName, DateOnly? fromDate, DateOnly? toDate)
        {
            using var ms = new MemoryStream();
            using (SpreadsheetDocument document = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook))
            {
                WorkbookPart workbookPart = document.AddWorkbookPart();
                workbookPart.Workbook = new Workbook();

                WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                SheetData sheetData = new SheetData();
                worksheetPart.Worksheet = new Worksheet(sheetData);

                Sheets sheets = document.WorkbookPart!.Workbook.AppendChild(new Sheets());
                Sheet sheet = new Sheet()
                {
                    Id = document.WorkbookPart.GetIdOfPart(worksheetPart),
                    SheetId = 1,
                    Name = "Booking Report"
                };
                sheets.Append(sheet);

                string[] headers = { "No", "Customer", "From", "To", "Type", "Date", "Vehicle", "Amount" };
                Row headerRow = new Row() { RowIndex = 1 };
                foreach (var h in headers)
                    headerRow.Append(CreateCell(h, CellValues.String));
                sheetData.Append(headerRow);

                int rowIndex = 2;
                foreach (var b in bookings)
                {
                    Row row = new Row() { RowIndex = (uint)rowIndex };
                    row.Append(CreateCell((rowIndex - 1).ToString(), CellValues.Number));
                    row.Append(CreateCell(b.Customer?.CustomerName ?? "N/A", CellValues.String));
                    row.Append(CreateCell(b.From ?? "", CellValues.String));
                    row.Append(CreateCell(b.To ?? "", CellValues.String));
                    row.Append(CreateCell(b.BookingType.ToString(), CellValues.String));
                    row.Append(CreateCell(b.travelDate.ToString("dd-MM-yyyy"), CellValues.String));
                    row.Append(CreateCell(b.Vehicle?.VehicleName ?? "N/A", CellValues.String));
                    row.Append(CreateCell(b.Amount.ToString(), CellValues.Number));
                    sheetData.Append(row);
                    rowIndex++;
                }

                Row totalRow = new Row() { RowIndex = (uint)rowIndex };
                for (int i = 0; i < 6; i++) totalRow.Append(CreateCell("", CellValues.String));
                totalRow.Append(CreateCell("TOTAL", CellValues.String));
                totalRow.Append(new Cell() { CellFormula = new CellFormula($"SUM(H2:H{rowIndex - 1})") });
                sheetData.Append(totalRow);

                workbookPart.Workbook.Save();
            }
            return ms.ToArray();
        }

        private static Cell CreateCell(string text, CellValues dataType)
        {
            return new Cell()
            {
                CellValue = new CellValue(text),
                DataType = new EnumValue<CellValues>(dataType)
            };
        }
    }
}