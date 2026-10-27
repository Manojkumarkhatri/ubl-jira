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

    /// <summary>What people write in comments: progress, questions and findings, in the words of software, PMO and data
    /// teams, so that word searches have both common and rare words to find (Phase 4 research R26).</summary>
    private static readonly string[] CommentSentences =
    [
        "Agreed in the weekly review.",
        "Moved the deadline after talking to the sponsor.",
        "The reconciliation still shows a small difference in the settlement totals.",
        "Refund requests over the limit need a second approval.",
        "Reproduced on staging; production is not affected.",
        "Rolled back the migration until the index rebuild finishes.",
        "The regression came from the new caching layer.",
        "Latency went down after the query change.",
        "The pipeline failed at the validation step again.",
        "The model accuracy dropped on last month's dataset.",
        "Retrained with the corrected labels; the results look better.",
        "The dashboard needs the churn figures by region.",
        "Waiting for the vendor's invoice before closing this.",
        "The budget line for licences is in the steering committee pack.",
        "Added this to the risk register as a medium risk.",
        "Stakeholders asked for a demo before the milestone.",
        "Compliance signed off on the retention period.",
        "The chargeback flow is covered by the new test cases.",
        "Pairing with the data engineers tomorrow on the schema.",
        "Merged the fix; it will go out with the next release.",
        "Security asked for the access review evidence.",
        "The customer letter template is approved by legal.",
        "Branch staff reported the printer issue again.",
        "Please check the FX rates feed before the cut-off.",
        "Load test results are attached to the release notes.",
        "Escalated to the change advisory board.",
        "Needs a second pair of eyes from compliance.",
        "Blocked until the vendor replies.",
        "Start with the most used screens.",
        "See https://example.com/wiki/project-guidelines for the house style.",
    ];

    private static readonly string[] FilterNames =
    [
        "My open work", "Unassigned to do", "High priority in progress", "Open bugs", "Reconciliation items", "Refund work",
        "Release blockers", "Data pipeline issues", "Overdue reviews", "Settlement follow-ups",
    ];

    private static readonly string[] FilterQueries =
    [
        "assignee=me&category=todo,inprogress", "category=todo&assignee=none", "category=inprogress&priority=high,highest",
        "kind=bug&category=todo,inprogress", "q=reconciliation", "q=refund&category=todo,inprogress", "q=regression&priority=highest",
        "q=pipeline", "q=review&category=todo", "q=settlement",
    ];

    private static readonly string[] LabelTopics =
    [
        "release-blocker", "tech-debt", "data-quality", "security", "performance", "ux", "accessibility", "compliance", "audit",
        "customer-impact", "quick-win", "regression", "hotfix", "api", "mobile", "web", "reporting", "migration", "infra", "ml-model",
    ];

    private static readonly string[] LabelSuffixes = ["", "-2025", "-2026", "-q1", "-q2", "-q3", "-q4", "-urgent", "-review", "-followup"];

    /// <summary>The 200 labels the seed uses, the plain topics first (Phase 4 research R26).</summary>
    public static IReadOnlyList<string> LabelNames() =>
        LabelSuffixes.SelectMany(suffix => LabelTopics.Select(topic => topic + suffix)).ToList();

    public static string DisplayName(Random random) => $"{Pick(random, FirstNames)} {Pick(random, LastNames)}";

    public static string ProjectName(int index) => $"{Themes[index % Themes.Length]} {index + 1:0000}";

    public static string Title(Random random) => $"{Pick(random, Verbs)} {Pick(random, Things)} for {Pick(random, Areas)}";

    public static string? Description(Random random) =>
        random.Next(100) < 30
            ? string.Join('\n', Enumerable.Range(0, random.Next(1, 4)).Select(_ => Pick(random, Sentences)))
            : null;

    /// <summary>One to three sentences; now and then a ticket or account number, which few other comments share.</summary>
    public static string Comment(Random random)
    {
        var sentences = Enumerable.Range(0, random.Next(1, 4)).Select(_ => Pick(random, CommentSentences)).Distinct().ToList();
        if (random.Next(100) < 20)
        {
            sentences.Add(random.Next(2) == 0
                ? $"Ticket INC{random.Next(100_000, 999_999)} has the details."
                : $"Seen with account {random.Next(10_000_000, 99_999_999)}.");
        }

        return string.Join(' ', sentences);
    }

    /// <summary>A saved filter's name and query string (contracts/ui-routes.md); <paramref name="index"/> picks a pair.</summary>
    public static (string Name, string Query) Filter(int index) =>
        (FilterNames[index % FilterNames.Length], FilterQueries[index % FilterQueries.Length]);

    private static string Pick(Random random, string[] values) => values[random.Next(values.Length)];
}
