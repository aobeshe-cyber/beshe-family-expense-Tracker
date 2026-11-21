using System.Text.Json;

// Expense tracker application — Console UI wired to ExpenseTrackerService

ExpenseTrackerService tracker = new ExpenseTrackerService();

// Seed initial sample expenses only if storage is completely empty
if (tracker.GetAllExpenses().Count == 0)
{
    tracker.TryAddExpense("95 Unleaded Petrol", 350.00m, "Transportation", out _, out _);
    tracker.TryAddExpense("Chicken Wrap & Drink", 89.50m, "Food", out _, out _);
}

Console.WriteLine("--- Initial Sample Expenses ---");
foreach (Expense item in tracker.GetAllExpenses())
{
    Console.WriteLine($"Id: {item.Id}, Title: {item.Title}, Amount: R{item.Amount:F2}, Category: {item.Category}");
}

bool running = true;

while (running)
{
    Console.WriteLine("\n=== EXPENSE TRACKER ===");
    Console.WriteLine("1. Add Expense");
    Console.WriteLine("2. View Expenses & Total");
    Console.WriteLine("3. Exit");
    Console.Write("Choose an option (1-3): ");

    string choice = Console.ReadLine()?.Trim() ?? "";
    Console.WriteLine();

    switch (choice)
    {
        case "1":
            Console.Write("Expense title: ");
            string title = Console.ReadLine()?.Trim() ?? "";

            decimal amount;
            while (true)
            {
                Console.Write("Amount (R): ");
                string amountInput = Console.ReadLine()?.Trim() ?? "";

                // Validate numeric input at the console layer before passing a decimal to the service
                if (decimal.TryParse(amountInput, out amount))
                {
                    break;
                }

                Console.WriteLine("Please enter a valid number for the amount (e.g., 120.00).");
            }

            Console.Write("Category (e.g. Food, Transportation, Electronics): ");
            string category = Console.ReadLine()?.Trim() ?? "";

            // Delegate business rules & storage to ExpenseTrackerService
            if (tracker.TryAddExpense(title, amount, category, out Expense? created, out string error))
            {
                Console.WriteLine($"Added #{created!.Id}: {created.Title} (R{created.Amount:F2})");
            }
            else
            {
                Console.WriteLine($"Could not add expense: {error}");
            }
            break;

        case "2":
            IReadOnlyList<Expense> currentExpenses = tracker.GetAllExpenses();
            if (currentExpenses.Count == 0)
            {
                Console.WriteLine("No expenses to display.");
            }
            else
            {
                Console.WriteLine("--- Logged Expenses ---");
                foreach (Expense item in currentExpenses)
                {
                    Console.WriteLine($"#{item.Id} | {item.Title} ({item.Category}): R{item.Amount:F2}");
                }
                Console.WriteLine("-----------------------");
                Console.WriteLine($"Total Spent: R{tracker.GetTotalSpent():F2}");
            }
            break;

        case "3":
            running = false;
            Console.WriteLine("Exiting Expense Tracker.");
            break;

        default:
            Console.WriteLine("Invalid option. Please choose a valid option (1-3).");
            break;
    }
}


// Data model: defines the shape of a single expense
public class Expense
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Category { get; set; } = string.Empty;
    public string SpentBy { get; set; } = "Me";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Expense() { }

    public Expense(int id, string title, decimal amount, string category, string spentBy = "Me")
    {
        Id = id;
        Title = title;
        Amount = amount;
        Category = category;
        SpentBy = string.IsNullOrWhiteSpace(spentBy) ? "Me" : spentBy.Trim();
    }
}

public class SavingsGoal
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public decimal SavedAmount { get; set; }
    public string Emoji { get; set; } = "🎯";

    public SavingsGoal() { }

    public SavingsGoal(int id, string name, decimal targetAmount, decimal savedAmount = 0m, string emoji = "🎯")
    {
        Id = id;
        Name = name;
        TargetAmount = targetAmount;
        SavedAmount = savedAmount;
        Emoji = emoji;
    }

    public decimal ProgressPercent => TargetAmount > 0
        ? Math.Min(100m, Math.Round((SavedAmount / TargetAmount) * 100m, 1))
        : 0m;
}

// Helper container used only for saving/loading all app data to JSON
public class TrackerDataSnapshot
{
    public List<Expense> Expenses { get; set; } = new();
    public List<SavingsGoal> Goals { get; set; } = new();
    public Dictionary<string, decimal> MemberAllowances { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

// Persistent Service Layer: saves automatically to disk so data survives restarts
public class ExpenseTrackerService
{
    private readonly object _sync = new();
    private readonly string _storageFilePath;
    private readonly List<Expense> _expenses = new();
    private readonly List<SavingsGoal> _goals = new();
    private readonly Dictionary<string, decimal> _memberAllowances = new(StringComparer.OrdinalIgnoreCase);

    public ExpenseTrackerService()
    {
        string? configuredFile = Environment.GetEnvironmentVariable("BESHE_DATA_FILE");
        if (!string.IsNullOrWhiteSpace(configuredFile))
        {
            _storageFilePath = configuredFile;
            string? parentDir = Path.GetDirectoryName(_storageFilePath);
            if (!string.IsNullOrWhiteSpace(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }
        }
        else
        {
            string dataFolder = Path.Combine(AppContext.BaseDirectory, "App_Data");
            Directory.CreateDirectory(dataFolder);
            _storageFilePath = Path.Combine(dataFolder, "beshe-family-data.json");
        }

        LoadFromDisk();
    }

    public IReadOnlyList<Expense> GetAllExpenses()
    {
        lock (_sync)
        {
            return _expenses.ToList().AsReadOnly();
        }
    }

    public IReadOnlyList<SavingsGoal> GetAllGoals()
    {
        lock (_sync)
        {
            return _goals.ToList().AsReadOnly();
        }
    }

    public IReadOnlyDictionary<string, decimal> GetMemberAllowances()
    {
        lock (_sync)
        {
            return new Dictionary<string, decimal>(_memberAllowances, StringComparer.OrdinalIgnoreCase);
        }
    }

    public IReadOnlyList<string> GetFamilyMembers()
    {
        lock (_sync)
        {
            return _memberAllowances.Keys.ToList();
        }
    }

    public void SetMemberAllowance(string member, decimal allowance)
    {
        if (string.IsNullOrWhiteSpace(member) || allowance < 0) return;

        lock (_sync)
        {
            _memberAllowances[member.Trim()] = allowance;
            SaveToDisk();
        }
    }

    public bool TryAddExpense(string title, decimal amount, string category, out Expense? createdExpense, out string errorMessage)
    {
        return TryAddExpense(title, amount, category, "Me", out createdExpense, out errorMessage);
    }

    public bool TryAddExpense(string title, decimal amount, string category, string spentBy, out Expense? createdExpense, out string errorMessage)
    {
        createdExpense = null;

        if (string.IsNullOrWhiteSpace(title))
        {
            errorMessage = "Title cannot be empty.";
            return false;
        }

        if (amount <= 0)
        {
            errorMessage = "Amount must be greater than 0.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            errorMessage = "Category cannot be empty.";
            return false;
        }

        lock (_sync)
        {
            string cleanMember = string.IsNullOrWhiteSpace(spentBy) ? "Me" : spentBy.Trim();
            if (!_memberAllowances.ContainsKey(cleanMember))
            {
                _memberAllowances[cleanMember] = 1000m;
            }

            int nextId = _expenses.Count == 0 ? 1 : _expenses.Max(e => e.Id) + 1;
            createdExpense = new Expense(nextId, title.Trim(), amount, category.Trim(), cleanMember);
            _expenses.Add(createdExpense);

            SaveToDisk();
        }

        errorMessage = string.Empty;
        return true;
    }

    public decimal GetTotalSpent()
    {
        lock (_sync)
        {
            decimal total = 0m;
            foreach (Expense item in _expenses)
            {
                total += item.Amount;
            }
            return total;
        }
    }

    public bool DeleteExpense(int id)
    {
        lock (_sync)
        {
            for (int i = 0; i < _expenses.Count; i++)
            {
                if (_expenses[i].Id == id)
                {
                    _expenses.RemoveAt(i);
                    SaveToDisk();
                    return true;
                }
            }
            return false;
        }
    }

    public bool TryAddGoal(string name, decimal targetAmount, string emoji, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            errorMessage = "Goal name cannot be empty.";
            return false;
        }

        if (targetAmount <= 0)
        {
            errorMessage = "Target amount must be greater than R 0.";
            return false;
        }

        lock (_sync)
        {
            int nextId = _goals.Count == 0 ? 1 : _goals.Max(g => g.Id) + 1;
            string cleanEmoji = string.IsNullOrWhiteSpace(emoji) ? "🎯" : emoji.Trim();
            _goals.Add(new SavingsGoal(nextId, name.Trim(), targetAmount, 0m, cleanEmoji));

            SaveToDisk();
        }

        errorMessage = string.Empty;
        return true;
    }

    public bool ContributeToGoal(int id, decimal amount)
    {
        if (amount <= 0) return false;

        lock (_sync)
        {
            SavingsGoal? goal = _goals.FirstOrDefault(g => g.Id == id);
            if (goal is null) return false;

            goal.SavedAmount += amount;
            SaveToDisk();
            return true;
        }
    }

    public bool DeleteGoal(int id)
    {
        lock (_sync)
        {
            SavingsGoal? goal = _goals.FirstOrDefault(g => g.Id == id);
            if (goal is null) return false;

            _goals.Remove(goal);
            SaveToDisk();
            return true;
        }
    }

    private void LoadFromDisk()
    {
        if (File.Exists(_storageFilePath))
        {
            try
            {
                string json = File.ReadAllText(_storageFilePath);
                var snapshot = JsonSerializer.Deserialize<TrackerDataSnapshot>(json);
                if (snapshot is not null)
                {
                    _expenses.Clear();
                    _expenses.AddRange(snapshot.Expenses);

                    _goals.Clear();
                    _goals.AddRange(snapshot.Goals);

                    _memberAllowances.Clear();
                    foreach (var kv in snapshot.MemberAllowances)
                    {
                        _memberAllowances[kv.Key] = kv.Value;
                    }
                    return;
                }
            }
            catch
            {
                // Fall back to starter defaults if file is empty or unreadable
            }
        }

        // First-time defaults
        _memberAllowances["Me"] = 1500m;
        _memberAllowances["Mom"] = 3000m;
        _memberAllowances["Dad"] = 3500m;
        _memberAllowances["Brother"] = 800m;

        _goals.Add(new SavingsGoal(1, "December Holiday", 12000m, 4500m, "🏖️"));
        _goals.Add(new SavingsGoal(2, "Family Braai & Treats", 1500m, 950m, "🔥"));

        SaveToDisk();
    }

    private void SaveToDisk()
    {
        var snapshot = new TrackerDataSnapshot
        {
            Expenses = _expenses.ToList(),
            Goals = _goals.ToList(),
            MemberAllowances = new Dictionary<string, decimal>(_memberAllowances, StringComparer.OrdinalIgnoreCase)
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(snapshot, options);
        File.WriteAllText(_storageFilePath, json);
    }
}