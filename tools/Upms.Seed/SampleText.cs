namespace Upms.Seed;

/// <summary>Word lists for believable names, titles and descriptions.</summary>
internal static class SampleText
{
    private static readonly string[] FirstNames =
    [
        "Amina", "Bilal", "Carla", "Daniyal", "Esha", "Farhan", "Gul", "Hassan", "Iqra", "Jamal", "Kiran", "Laila",
        "Mehwish", "Nadia", "Omar", "Priya", "Qasim", "Rafael", "Sana", "Tariq", "Usman", "Venus", "Waleed", "Yasmin", "Zara",
    ];

    private static readonly string[] LastNames =
    [
        "Ahmed", "Baig", "Costa", "Diaz", "Farooq", "Hussain", "Iqbal", "Jamil", "Khan", "Malik", "Mirza", "Noor",
        "Qureshi", "Raza", "Shah", "Siddiqui", "Tariq", "Usmani", "Zaidi",
    ];

    private static readonly string[] Verbs =
    [
        "Review", "Update", "Draft", "Test", "Design", "Migrate", "Document", "Automate", "Reconcile", "Approve", "Fix",
        "Plan", "Clean up", "Publish", "Validate", "Prepare", "Audit", "Simplify",
    ];

    private static readonly string[] Things =
    [
        "the onboarding checklist", "the payment limits", "the login screen", "the monthly report", "the card printer quotes",
        "the branch survey", "the cash forecast", "the release notes", "the data retention policy", "the customer letter",
        "the settlement file", "the API contract", "the dashboard layout", "the backup job", "the vendor contract",
        "the training plan", "the complaint workflow", "the FX rates feed", "the access review", "the test cases",
    ];

    private static readonly string[] Areas =
    [
        "retail banking", "the mobile app", "treasury", "HR", "the call centre", "compliance", "card services", "IT operations",
        "the website", "branch network", "finance", "risk",
    ];

    private static readonly string[] Themes =
    [
        "Website Revamp", "Mobile Banking", "Branch Refresh", "Treasury Operations", "Card Services", "Audit Findings",
        "HR Onboarding", "Data Platform", "Customer Care", "Compliance Programme", "Payments Hub", "Risk Reporting",
    ];

    private static readonly string[] Sentences =
    [
        "Agreed in the weekly review.",
        "Keep the current behaviour for existing customers.",
        "Check the figures with finance before sign-off.",
        "The draft is in the shared folder.",
        "Needs a second pair of eyes from compliance.",
        "Blocked until the vendor replies.",
        "Start with the most used screens.",
        "See https://example.com/wiki/project-guidelines for the house style.",
    ];

    public static string DisplayName(Random random) => $"{Pick(random, FirstNames)} {Pick(random, LastNames)}";

    public static string ProjectName(int index) => $"{Themes[index % Themes.Length]} {index + 1:0000}";

    public static string Title(Random random) => $"{Pick(random, Verbs)} {Pick(random, Things)} for {Pick(random, Areas)}";

    public static string? Description(Random random) =>
        random.Next(100) < 30
            ? string.Join('\n', Enumerable.Range(0, random.Next(1, 4)).Select(_ => Pick(random, Sentences)))
            : null;

    public static string Comment(Random random) => Pick(random, Sentences);

    private static string Pick(Random random, string[] values) => values[random.Next(values.Length)];
}
