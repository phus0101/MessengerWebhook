using Pinecone;

namespace MessengerWebhook.Services.VectorSearch;

/// <summary>
/// Builds Pinecone metadata filter objects using the SDK's <see cref="Metadata"/> type.
/// Mirrors the Pinecone filter grammar: $eq, $in, $exists, $and, $or.
/// All methods return a top-level Metadata filter ready to assign to QueryRequest.Filter.
/// </summary>
public static class PineconeFilterBuilder
{
    /// <summary>Exact-match filter: { field: { $eq: value } }</summary>
    public static Metadata Eq(string field, object value) =>
        new() { [field] = new Metadata { ["$eq"] = ConvertValue(value) } };

    /// <summary>Membership filter: { field: { $in: [v1, v2, ...] } }</summary>
    public static Metadata In(string field, IEnumerable<string> values) =>
        new() { [field] = new Metadata { ["$in"] = values.ToArray() } };

    /// <summary>
    /// Existence filter: { field: { $exists: true|false } }
    /// When <paramref name="exists"/> is true the field must be present; false means absent.
    /// </summary>
    public static Metadata Exists(string field, bool exists = true) =>
        new() { [field] = new Metadata { ["$exists"] = exists } };

    /// <summary>
    /// Logical AND of all supplied clauses.
    /// { $and: [ clause1, clause2, ... ] }
    /// Returns the single clause unchanged when only one is supplied (avoids wrapping noise).
    /// </summary>
    public static Metadata And(params Metadata[] clauses)
    {
        if (clauses.Length == 1) return clauses[0];
        // MetadataValue has no implicit conversion from Metadata[] directly,
        // but does have one from MetadataValue[] — so we project each Metadata
        // via its own implicit conversion first.
        MetadataValue[] wrapped = Array.ConvertAll(clauses, c => (MetadataValue)c);
        return new Metadata { ["$and"] = wrapped };
    }

    /// <summary>
    /// Logical OR of all supplied clauses.
    /// { $or: [ clause1, clause2, ... ] }
    /// Returns the single clause unchanged when only one is supplied.
    /// </summary>
    public static Metadata Or(params Metadata[] clauses)
    {
        if (clauses.Length == 1) return clauses[0];
        MetadataValue[] wrapped = Array.ConvertAll(clauses, c => (MetadataValue)c);
        return new Metadata { ["$or"] = wrapped };
    }

    /// <summary>
    /// Converts a CLR value to the appropriate Pinecone MetadataValue.
    /// Pinecone supports: string, double (all numerics coerced), bool.
    /// Everything else falls back to ToString().
    /// </summary>
    private static MetadataValue ConvertValue(object value) => value switch
    {
        string s   => s,
        bool b     => b,
        int i      => (double)i,
        long l     => (double)l,
        float f    => (double)f,
        double d   => d,
        decimal dc => (double)dc,
        _          => value.ToString() ?? string.Empty
    };
}
