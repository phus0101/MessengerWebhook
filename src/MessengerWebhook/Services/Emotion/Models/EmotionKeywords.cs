namespace MessengerWebhook.Services.Emotion.Models;

/// <summary>
/// Vietnamese emotion keywords lexicon for rule-based detection
/// </summary>
public static class EmotionKeywords
{
    /// <summary>
    /// Positive emotion keywords
    /// </summary>
    public static readonly HashSet<string> Positive = new(StringComparer.OrdinalIgnoreCase)
    {
        // Basic positive
        "vui", "tốt", "hay", "ok", "oke", "được", "ổn", "ngon",
        "hay lắm", "tốt quá",

        // Strong positive
        "tuyệt", "tuyệt vời", "xuất sắc", "hoàn hảo", "tốt lắm",
        "hay quá", "đỉnh",

        // Satisfaction
        "hài lòng", "thích", "ưng", "vừa ý", "đúng ý",

        // Appreciation
        "cảm ơn", "thanks", "thank you", "cám ơn", "cảm ơn nhiều",

        // Agreement
        "đồng ý", "chấp nhận", "đúng rồi", "đúng vậy"
    };

    /// <summary>
    /// Negative emotion keywords
    /// </summary>
    public static readonly HashSet<string> Negative = new(StringComparer.OrdinalIgnoreCase)
    {
        // Basic negative
        "không tốt", "dở", "tệ", "kém", "xấu", "dở quá",

        // Dissatisfaction
        "không thích", "không ưng", "không vừa ý", "không hài lòng",

        // Disappointment
        "thất vọng", "buồn", "tiếc", "đáng tiếc"
    };

    /// <summary>
    /// Frustrated emotion keywords
    /// </summary>
    public static readonly HashSet<string> Frustrated = new(StringComparer.OrdinalIgnoreCase)
    {
        // Annoyance
        "bực", "bực mình", "tức", "tức giận", "khó chịu",

        // Impatience
        "chán", "mệt", "mệt mỏi", "chậm quá", "lâu quá", "chán quá",

        // Anger
        "giận", "tức giận", "phẫn nộ", "bực bội",

        // Frustration expressions
        "ối", "trời ơi", "chết mất", "ôi giời"
    };

    /// <summary>
    /// Excited emotion keywords
    /// </summary>
    public static readonly HashSet<string> Excited = new(StringComparer.OrdinalIgnoreCase)
    {
        // Enthusiasm
        "wow", "wao", "quá đỉnh", "tuyệt vời quá", "xuất sắc quá", "xuất sắc",

        // Eagerness
        "háo hức", "phấn khích", "hào hứng", "mong chờ",

        // Excitement expressions
        "yay", "yeah", "nice", "cool", "awesome"
    };

    /// <summary>
    /// Negation words that flip emotion polarity
    /// </summary>
    public static readonly HashSet<string> Negations = new(StringComparer.OrdinalIgnoreCase)
    {
        "không", "chẳng", "chả", "chưa", "không phải"
    };

    /// <summary>
    /// Positive emojis
    /// </summary>
    public static readonly HashSet<string> PositiveEmojis = new()
    {
        "😊", "😀", "😃", "😄", "😁", "🙂", "😌", "😍", "🥰", "😘",
        "👍", "👌", "✅", "💯", "🎉", "❤️", "💖", "💕"
    };

    /// <summary>
    /// Negative emojis
    /// </summary>
    public static readonly HashSet<string> NegativeEmojis = new()
    {
        "😢", "😭", "😞", "😔", "😟", "😕", "☹️", "🙁", "😩", "😫",
        "👎", "❌", "💔"
    };

    /// <summary>
    /// Frustrated emojis
    /// </summary>
    public static readonly HashSet<string> FrustratedEmojis = new()
    {
        "😠", "😡", "🤬", "😤", "😣", "😖", "😫", "🙄"
    };

    /// <summary>
    /// Excited emojis
    /// </summary>
    public static readonly HashSet<string> ExcitedEmojis = new()
    {
        "🤩", "😍", "🥳", "🎉", "🎊", "✨", "🔥", "💥", "🚀"
    };
}
