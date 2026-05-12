namespace Gymers.Services;

public sealed class Session
{
    public static Session Current { get; } = new();

    public bool   IsAdmin  { get; private set; } = true;
    public string Username { get; private set; } = "";
    public string RoleLabel => IsAdmin ? "Admin" : "Staff";

    public void SignIn(string username, bool isAdmin)
    {
        Username = username;
        IsAdmin  = isAdmin;
    }
}
