namespace TravelManagement.Core.Common
{
    public class Calculations
    {
        public static double CalculateAge(DateOnly registrationDate, DateTime currentDate)
        {
            var regDate = registrationDate.ToDateTime(TimeOnly.MinValue);

            int totalMonths =
                (currentDate.Year - regDate.Year) * 12 +
                (currentDate.Month - regDate.Month);

            if (currentDate.Day < regDate.Day)
                totalMonths--;

            double ageInYears = totalMonths / 12.0;
            return Math.Round(ageInYears, 1);
        }
    }
}