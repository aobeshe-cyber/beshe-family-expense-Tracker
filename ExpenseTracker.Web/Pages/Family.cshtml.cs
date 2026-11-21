using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseTracker.Web.Pages;

public class FamilyModel : PageModel
{
    private readonly ExpenseTrackerService _tracker;
    private static readonly CultureInfo SaCulture = CultureInfo.GetCultureInfo("en-ZA");

    public FamilyModel(ExpenseTrackerService tracker)
    {
        _tracker = tracker;
    }

    [BindProperty]
    public string MemberName { get; set; } = string.Empty;

    [BindProperty]
    public decimal AllowanceAmount { get; set; }

    public record MemberSummary(string Name, decimal Allowance, decimal TotalSpent, int ItemCount, decimal Remaining, decimal UsagePercent);

    public List<MemberSummary> Members { get; private set; } = new();
    public decimal FamilyTotalSpent { get; private set; }

    public string FormatRand(decimal amount) => $"R {amount.ToString("N2", SaCulture)}";

    public void OnGet()
    {
        LoadMembers();
    }

    public IActionResult OnPostUpdateAllowance()
    {
        if (!string.IsNullOrWhiteSpace(MemberName) && AllowanceAmount >= 0)
        {
            _tracker.SetMemberAllowance(MemberName, AllowanceAmount);
        }

        return RedirectToPage();
    }

    private void LoadMembers()
    {
        var expenses = _tracker.GetAllExpenses();
        var allowances = _tracker.GetMemberAllowances();
        FamilyTotalSpent = _tracker.GetTotalSpent();

        Members = allowances
            .Select(kv =>
            {
                var personExpenses = expenses
                    .Where(e => e.SpentBy.Equals(kv.Key, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                decimal spent = personExpenses.Sum(e => e.Amount);
                decimal remaining = kv.Value - spent;
                decimal pct = kv.Value > 0
                    ? Math.Min(100m, Math.Round((spent / kv.Value) * 100m, 1))
                    : 0m;

                return new MemberSummary(kv.Key, kv.Value, spent, personExpenses.Count, remaining, pct);
            })
            .OrderByDescending(m => m.TotalSpent)
            .ToList();
    }
}