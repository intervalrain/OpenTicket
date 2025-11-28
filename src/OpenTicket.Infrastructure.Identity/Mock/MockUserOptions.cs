namespace OpenTicket.Infrastructure.Identity.Mock;

/// <summary>
/// Configuration options for mock user provider.
/// Used for development and testing.
/// </summary>
public class MockUserOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "MockUser";

    /// <summary>
    /// The mock user's ID.
    /// </summary>
    public Guid UserId { get; set; } = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>
    /// The mock user's email address.
    /// </summary>
    public string Email { get; set; } = "test@openticket.local";

    /// <summary>
    /// The mock user's display name.
    /// </summary>
    public string Name { get; set; } = "Test User";

    /// <summary>
    /// The mock user's roles.
    /// </summary>
    public List<string> Roles { get; set; } = ["User"];

    /// <summary>
    /// Whether the mock user has an active subscription.
    /// </summary>
    public bool HasSubscription { get; set; } = false;
}
