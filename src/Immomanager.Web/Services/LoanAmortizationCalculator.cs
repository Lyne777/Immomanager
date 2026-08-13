using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

public record LoanYearEntry(int Year, decimal Interest, decimal Principal, decimal EndBalance);

/// <summary>Simuliert den monatlichen Annuitätenverlauf eines Darlehens (Zins/Tilgungs-Split verschiebt
/// sich mit sinkender Restschuld, die Monatsrate bleibt konstant) inkl. jährlicher Sondertilgung.</summary>
public static class LoanAmortizationCalculator
{
    public static List<LoanYearEntry> BuildSchedule(LoanCalculation loan, int years)
    {
        var schedule = new List<LoanYearEntry>();
        var balance = loan.LoanAmount;
        var monthlyRate = loan.InterestRatePercent / 100 / 12;
        var monthlyPayment = loan.LoanAmount * (loan.InterestRatePercent + loan.InitialRepaymentRatePercent) / 100 / 12;

        for (var year = 1; year <= years; year++)
        {
            decimal yearInterest = 0, yearPrincipal = 0;

            for (var month = 1; month <= 12 && balance > 0; month++)
            {
                var interest = balance * monthlyRate;
                var principal = Math.Min(monthlyPayment - interest, balance);
                if (principal < 0)
                {
                    principal = 0;
                }

                balance -= principal;
                yearInterest += interest;
                yearPrincipal += principal;
            }

            if (balance > 0 && loan.AnnualSpecialRepayment > 0)
            {
                var special = Math.Min(loan.AnnualSpecialRepayment, balance);
                balance -= special;
                yearPrincipal += special;
            }

            schedule.Add(new LoanYearEntry(year, yearInterest, yearPrincipal, balance));

            if (balance <= 0)
            {
                for (var remainingYear = year + 1; remainingYear <= years; remainingYear++)
                {
                    schedule.Add(new LoanYearEntry(remainingYear, 0, 0, 0));
                }

                break;
            }
        }

        return schedule;
    }
}
