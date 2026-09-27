using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Upms.Infrastructure.Persistence;
using Upms.Seed;

// Fills an empty U-PMS database with realistic volumes for the performance suite (tasks.md T105).
//   dotnet run --project tools/Upms.Seed -- --connection "<connection string>" [--migrate]
//       [--users 2000] [--projects 1000] [--tasks 500000] [--seed 20260927]
// The connection string may also come from the ConnectionStrings__Default environment variable.
var options = SeedOptions.Baseline;
var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
var migrate = false;
try
{
    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--users":
                options = options with { Users = Number(args, ++i) };
                break;
            case "--projects":
                options = options with { Projects = Number(args, ++i) };
                break;
            case "--tasks":
                options = options with { WorkItems = Number(args, ++i) };
                break;
            case "--seed":
                options = options with { RandomSeed = Number(args, ++i) };
                break;
            case "--connection":
                connection = i + 1 < args.Length ? args[++i] : throw new ArgumentException("--connection needs a value.");
                break;
            case "--migrate":
                migrate = true;
                break;
            case "--help" or "-h":
                PrintUsage();
                return 0;
            default:
                throw new ArgumentException($"Unknown option {args[i]}.");
        }
    }

    if (string.IsNullOrWhiteSpace(connection))
    {
        throw new ArgumentException("Give a connection string with --connection or ConnectionStrings__Default.");
    }
}
catch (ArgumentException e)
{
    Console.Error.WriteLine(e.Message);
    PrintUsage();
    return 2;
}

if (migrate)
{
    await using var db = AppDbContext.Create(connection);
    await db.Database.MigrateAsync();
    Console.WriteLine("Database migrated.");
}

Console.WriteLine($"Seeding {options.Users:N0} users, {options.Projects:N0} projects and about {options.WorkItems:N0} work items…");
await new Seeder(connection, Console.WriteLine).RunAsync(options, CancellationToken.None);
return 0;

static int Number(string[] args, int index) =>
    index < args.Length && int.TryParse(args[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
        ? value
        : throw new ArgumentException($"{args[index - 1]} needs a positive whole number.");

static void PrintUsage() => Console.WriteLine(
    "Usage: Upms.Seed --connection <connection string> [--migrate] [--users 2000] [--projects 1000] [--tasks 500000] [--seed 20260927]");
