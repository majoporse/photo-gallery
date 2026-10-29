using Gallery.Api.Admin;

namespace Gallery.Api.Tests;

public sealed class PasswordHasherCliTests
{
    [Fact]
    public void Hash_password_prints_a_hash_that_verifies()
    {
        var output = new StringWriter();

        var exitCode = PasswordHasherCli.TryRun(["hash-password", "s3cret"], output, TextWriter.Null);

        Assert.Equal(0, exitCode);
        Assert.True(PasswordHasher.Verify("s3cret", output.ToString().Trim()));
    }

    [Fact]
    public void Verify_password_accepts_a_matching_password()
    {
        var hash = PasswordHasher.Hash("s3cret", iterations: 10_000);
        var output = new StringWriter();

        var exitCode = PasswordHasherCli.TryRun(["verify-password", "s3cret", hash], output, TextWriter.Null);

        Assert.Equal(0, exitCode);
        Assert.Contains("matches", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verify_password_rejects_a_wrong_password()
    {
        var hash = PasswordHasher.Hash("s3cret", iterations: 10_000);
        var output = new StringWriter();

        var exitCode = PasswordHasherCli.TryRun(["verify-password", "wrong", hash], output, TextWriter.Null);

        Assert.Equal(1, exitCode);
        Assert.Contains("does not match", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_malformed_hash_is_a_mismatch_rather_than_a_crash()
    {
        var exitCode = PasswordHasherCli.TryRun(
            ["verify-password", "s3cret", "not-a-hash"],
            TextWriter.Null,
            TextWriter.Null);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void A_command_with_the_wrong_arity_prints_usage()
    {
        var error = new StringWriter();

        var exitCode = PasswordHasherCli.TryRun(["verify-password", "s3cret"], TextWriter.Null, error);

        Assert.Equal(2, exitCode);
        Assert.Contains("verify-password", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Unrelated_arguments_are_left_for_the_web_host()
    {
        Assert.Null(PasswordHasherCli.TryRun(
            ["--urls", "http://localhost:8080"],
            TextWriter.Null,
            TextWriter.Null));
    }

    [Fact]
    public void No_arguments_are_left_for_the_web_host()
    {
        Assert.Null(PasswordHasherCli.TryRun([], TextWriter.Null, TextWriter.Null));
    }
}
