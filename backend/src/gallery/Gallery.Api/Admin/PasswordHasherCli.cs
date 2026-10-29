namespace Gallery.Api.Admin;

/// <summary>
/// Developer-facing password commands. They run before the web host is built,
/// so they need no configuration, database, or listening socket.
/// </summary>
public static class PasswordHasherCli
{
    private const string Usage =
        """
        usage:
          hash-password <password>
          verify-password <password> <encoded-hash>
        """;

    /// <summary>
    /// Returns the process exit code when <paramref name="args"/> names a
    /// command, or <c>null</c> when the arguments are meant for the web host.
    /// </summary>
    public static int? TryRun(string[] args, TextWriter output, TextWriter error)
    {
        if (args is not [var command, ..])
        {
            return null;
        }

        switch (command)
        {
            case "hash-password" when args.Length == 2:
                output.WriteLine(PasswordHasher.Hash(args[1]));
                return 0;

            case "verify-password" when args.Length == 3:
                if (PasswordHasher.Verify(args[1], args[2]))
                {
                    output.WriteLine("Password matches the hash.");
                    return 0;
                }

                output.WriteLine("Password does not match the hash.");
                return 1;

            case "hash-password" or "verify-password":
                error.WriteLine(Usage);
                return 2;

            default:
                return null;
        }
    }
}
