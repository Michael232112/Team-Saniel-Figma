namespace Gymers.Models;

public record Equipment(
    string Id,
    string Name,
    string Category,
    string Status,
    string Location,
    int    OrderRank);
