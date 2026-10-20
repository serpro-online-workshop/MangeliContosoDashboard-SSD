using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Tests.TestSupport;

public sealed class TestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private TestDatabase(SqliteConnection connection, ApplicationDbContext context)
    {
        _connection = connection;
        Context = context;
    }

    public ApplicationDbContext Context { get; }

    public static async Task<TestDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedAsync(context);
        return new TestDatabase(connection, context);
    }

    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new ApplicationDbContext(options);
    }

    public FailingSaveApplicationDbContext CreateFailingContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new FailingSaveApplicationDbContext(options);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static async Task SeedAsync(ApplicationDbContext context)
    {
        context.Users.AddRange(
            new User { UserId = 10, Email = "admin@test.local", DisplayName = "Admin", Department = "IT", JobTitle = "Administrator", Role = UserRole.Administrator },
            new User { UserId = 11, Email = "lead@test.local", DisplayName = "Lead", Department = "Engineering", JobTitle = "Team Lead", Role = UserRole.TeamLead },
            new User { UserId = 12, Email = "manager@test.local", DisplayName = "Manager", Department = "Engineering", JobTitle = "Project Manager", Role = UserRole.ProjectManager },
            new User { UserId = 13, Email = "Employee@test.local", DisplayName = "Employee", Department = "Engineering", JobTitle = "Employee", Role = UserRole.Employee },
            new User { UserId = 14, Email = "outsider@test.local", DisplayName = "Outsider", Department = "Finance", JobTitle = "Employee", Role = UserRole.Employee });
        context.Projects.Add(new Project
        {
            ProjectId = 10,
            Name = "Test Project",
            ProjectManagerId = 12,
            StartDate = DateTime.UtcNow,
            Status = ProjectStatus.Active,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        });
        context.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = 10, UserId = 11 },
            new ProjectMember { ProjectId = 10, UserId = 13 });
        await context.SaveChangesAsync();
    }
}
