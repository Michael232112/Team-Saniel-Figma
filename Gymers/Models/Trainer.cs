namespace Gymers.Models;

public record Trainer(
    string  Id,
    string  Name,
    string  Title,
    decimal Rating,
    int     SessionsCompleted);
