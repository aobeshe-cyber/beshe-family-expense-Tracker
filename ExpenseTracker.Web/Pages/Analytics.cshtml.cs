using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseTracker.Web.Pages;

public class AnalyticsModel : PageModel
{
    private readonly ExpenseTrackerService _tracker;
    private static readonly CultureInfo SaCulture = CultureInfo.GetCultureInfo("en-ZA");

    public AnalyticsModel(ExpenseTrackerService tracker)
    {
        _tracker = tracker;
    }

    public decimal TotalSpent { get; private set; }
    public decimal AverageDailySpend { get; private set; }
    public string TopCategoryName { get; private set; } = "None yet";
    public decimal TopCategoryAmount { get; private set; }
    public int TotalTransactions { get; private set; }

    public List<KeyValuePair<string, decimal>> CategoryTotals { get; private set; } = new();
    public string CategoryLabelsJson { get; private set; } = "[]";
    public string CategoryValuesJson { get; private set; } = "[]";
    public string DailyLabelsJson { get; private set; } = "[]";
    public string DailyValuesJson { get; private set; } = "[]";

    public string FormatRand(decimal amount) => $"R {amount.ToString("N2", SaCulture)}";

    public void OnGet()
    {
        var expenses = _tracker.GetAllExpenses();
        TotalTransactions = expenses.Count;
        TotalSpent = _tracker.GetTotalSpent();

        if (expenses.Count > 0)
        {
            int activeDays = expenses.Select(e => e.CreatedAt.Date).Distinct().Count();
            AverageDailySpend = activeDays > 0 ? TotalSpent / activeDays : TotalSpent;

            CategoryTotals = expenses
                .GroupBy(e => e.Category)
                .Select(g => new KeyValuePair<string, decimal>(g.Key, g.Sum(x => x.Amount)))
                .OrderByDescending(kv => kv.Value)
                .ToList();

            TopCategoryName = CategoryTotals[0].Key;
            TopCategoryAmount = CategoryTotals[0].Value;

            CategoryLabelsJson = JsonSerializer.Serialize(CategoryTotals.Select(c => c.Key));
            CategoryValuesJson = JsonSerializer.Serialize(CategoryTotals.Select(c => c.Value));

            var dailyGroups = expenses
                .GroupBy(e => e.CreatedAt.Date)
                .OrderBy(g => g.Key)
                .TakeLast(10)
                .ToList();

            DailyLabelsJson = JsonSerializer.Serialize(dailyGroups.Select(g => g.Key.ToString("dd MMM")));
            DailyValuesJson = JsonSerializer.Serialize(dailyGroups.Select(g => g.Sum(x => x.Amount)));
        }
    }
}