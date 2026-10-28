using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Gallery.Persistence;

/// <summary>
/// Photo positions are reserved before the bytes arrive, so the gallery follows
/// the owner's upload order rather than the order workers happen to finish in.
/// A single atomic increment is what makes concurrent reservations safe.
/// </summary>
public static class PhotoPositions
{
    public static async Task<int> ReserveAsync(
        GalleryDbContext database,
        string collectionId,
        CancellationToken cancellationToken)
    {
        var connection = database.Database.GetDbConnection();
        var opened = false;

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
            opened = true;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE "Collections"
                SET "NextPhotoPosition" = "NextPhotoPosition" + 1
                WHERE "Id" = @collection
                RETURNING "NextPhotoPosition" - 1
                """;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "collection";
            parameter.Value = collectionId;
            command.Parameters.Add(parameter);

            var transaction = database.Database.CurrentTransaction;
            if (transaction is not null)
            {
                command.Transaction = transaction.GetDbTransaction();
            }

            var reserved = await command.ExecuteScalarAsync(cancellationToken);

            return reserved is null or DBNull
                ? throw new InvalidOperationException($"Collection {collectionId} does not exist.")
                : Convert.ToInt32(reserved);
        }
        finally
        {
            if (opened)
            {
                await connection.CloseAsync();
            }
        }
    }
}
