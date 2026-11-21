using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseTracker.Web.Pages;

public class IndexModel : PageModel
{
    private readonly ExpenseTrackerService _tracker;
    private static readonly CultureInfo SaCulture = CultureInfo.GetCultureInfo("en-ZA");

    public IndexModel(ExpenseTrackerService tracker)
    {
        _tracker = tracker;
    }

    [BindProperty]
    public string Title { get; set; } = string.Empty;

    [BindProperty]
    public decimal Amount { get; set; }

    [BindProperty]
    public string Category { get; set; } = string.Empty;

    [BindProperty]
    public string SpentBy { get; set; } = "Me";

    [BindProperty(SupportsGet = true)]
    public string? SelectedCategory { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedMonth { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool ShowAll { get; set; }

    public DateTime ActiveMonthDate { get; private set; }
    public string ActiveMonthLabel { get; private set; } = string.Empty;
    public string PreviousMonth { get; private set; } = string.Empty;
    public string NextMonth { get; private set; } = string.Empty;

    public IReadOnlyList<string> FamilyMembers { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<Expense> DisplayedExpenses { get; private set; } = Array.Empty<Expense>();
    public int TotalMatchingCount { get; private set; }
    public List<string> Categories { get; private set; } = new();

    public decimal TotalSpent { get; private set; }
    public string? ErrorMessage { get; private set; }

    public string FormatRand(decimal amount) => $"R {amount.ToString("N2", SaCulture)}";

    public void OnGet()
    {
        LoadState();
    }

    public IActionResult OnPost()
    {
        if (_tracker.TryAddExpense(Title, Amount, Category, SpentBy, out Expense? created, out string error))
        {
            if (created is not null && DateTime.TryParseExact(SelectedMonth, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime targetMonth))
            {
                int safeDay = Math.Min(DateTime.Now.Day, DateTime.DaysInMonth(targetMonth.Year, targetMonth.Month));
                created.CreatedAt = new DateTime(targetMonth.Year, targetMonth.Month, safeDay, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second);
            }

            return RedirectToPage(new { SelectedCategory, SelectedMonth, ShowAll });
        }

        ErrorMessage = error;
        LoadState();
        return Page();
    }

    public IActionResult OnPostDelete(int id)
    {
        _tracker.DeleteExpense(id);
        return RedirectToPage(new { SelectedCategory, SelectedMonth, ShowAll });
    }

    public IActionResult OnGetExportCsv()
    {
        ShowAll = true;
        LoadState();

        var csv = new StringBuilder();
        csv.AppendLine("Id,SpentBy,Title,Category,Date,Amount (ZAR)");

        foreach (var item in DisplayedExpenses)
        {
            string safeWho = $"\"{item.SpentBy.Replace("\"", "\"\"")}\"";
            string safeTitle = $"\"{item.Title.Replace("\"", "\"\"")}\"";
            string safeCategory = $"\"{item.Category.Replace("\"", "\"\"")}\"";
            csv.AppendLine($"{item.Id},{safeWho},{safeTitle},{safeCategory},{item.CreatedAt:yyyy-MM-dd},{item.Amount.ToString("F2", CultureInfo.InvariantCulture)}");
        }

        string fileName = $"Beshe-Expenses-{SelectedMonth}.csv";
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
    }

    private void LoadState()
    {
        FamilyMembers = _tracker.GetFamilyMembers();

        if (!DateTime.TryParseExact(SelectedMonth, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedMonth))
        {
            parsedMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            SelectedMonth = parsedMonth.ToString("yyyy-MM");
        }

        ActiveMonthDate = parsedMonth;
        ActiveMonthLabel = parsedMonth.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        PreviousMonth = parsedMonth.AddMonths(-1).ToString("yyyy-MM");
        NextMonth = parsedMonth.AddMonths(1).ToString("yyyy-MM");

        var monthExpenses = _tracker.GetAllExpenses()
            .Where(e => e.CreatedAt.Year == parsedMonth.Year && e.CreatedAt.Month == parsedMonth.Month)
            .ToList();

        TotalSpent = monthExpenses.Sum(e => e.Amount);

        Categories = monthExpenses
            .Select(e => e.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();

        List<Expense> filtered;
        if (!string.IsNullOrWhiteSpace(SelectedCategory) &&
            !SelectedCategory.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = monthExpenses
                .Where(e => e.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.Id)
                .ToList();
        }
        else
        {
            SelectedCategory = "All";
            filtered = monthExpenses
                .OrderByDescending(e => e.Id)
                .ToList();
        }

        TotalMatchingCount = filtered.Count;
        DisplayedExpenses = ShowAll ? filtered : filtered.Take(5).ToList();
    }
}