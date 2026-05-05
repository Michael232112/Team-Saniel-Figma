namespace Gymers.Models;

public record ClassSession(
    string Id,
    string Title,
    string Location,
    DateTime Start,
    DateTime End);
