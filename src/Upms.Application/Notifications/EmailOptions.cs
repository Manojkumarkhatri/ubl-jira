namespace Upms.Application.Notifications;

/// <summary>How emails are addressed (Phase 4 FR-050, research R10): section <c>Email</c>, with the links' base taken from
/// <c>App:PublicBaseUrl</c>. Whether email is on at all is the transport's business (<c>Email:Smtp:Host</c>).</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>The sender's address, such as <c>upms@bank.example</c>.</summary>
    public string FromAddress { get; set; } = "";

    public string FromName { get; set; } = "U-PMS";

    /// <summary>The address people open U-PMS at, such as <c>https://upms.bank.example</c>.</summary>
    public string PublicBaseUrl { get; set; } = "";
}
