namespace Gymers.Models;

public record Member(
    string Id,
    string Name,
    MembershipTier Tier,
    string Status,
    DateOnly Expires);
