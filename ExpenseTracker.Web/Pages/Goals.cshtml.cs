using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseTracker.Web.Pages;

public class GoalsModel : PageModel
{
    private readonly ExpenseTrackerService _tracker;
    private static readonly CultureInfo SaCulture = CultureInfo.GetCultureInfo("en-ZA");

    public GoalsModel(ExpenseTrackerService tracker)
    {
        _tracker = tracker;
    }

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public decimal TargetAmount { get; set; }

    [BindProperty]
    public string Emoji { get; set; } = "🎯";

    [BindProperty]
    public decimal ContributionAmount { get; set; }

    public IReadOnlyList<SavingsGoal> Goals { get; private set; } = Array.Empty<SavingsGoal>();
    public decimal TotalSaved { get; private set; }
    public decimal TotalTargets { get; private set; }
    public string? ErrorMessage { get; private set; }

    public string FormatRand(decimal amount) => $"R {amount.ToString("N2", SaCulture)}";

    public void OnGet()
    {
        LoadGoals();
    }

    public IActionResult OnPostCreate()
    {
        if (_tracker.TryAddGoal(Name, TargetAmount, Emoji, out string error))
        {
            return RedirectToPage();
        }

        ErrorMessage = error;
        LoadGoals();
        return Page();
    }

    public IActionResult OnPostContribute(int id)
    {
        if (ContributionAmount <= 0)
        {
            ErrorMessage = "Enter a valid Rand amount to add to this goal.";
            LoadGoals();
            return Page();
        }

        _tracker.ContributeToGoal(id, ContributionAmount);
        return RedirectToPage();
    }

    public IActionResult OnPostDelete(int id)
    {
        _tracker.DeleteGoal(id);
        return RedirectToPage();
    }

    private void LoadGoals()
    {
        Goals = _tracker.GetAllGoals();
        TotalSaved = Goals.Sum(g => g.SavedAmount);
        TotalTargets = Goals.Sum(g => g.TargetAmount);
    }
}