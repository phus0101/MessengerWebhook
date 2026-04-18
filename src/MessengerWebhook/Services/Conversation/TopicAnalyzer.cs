using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.Services.Conversation.Models;

namespace MessengerWebhook.Services.Conversation;

/// <summary>
/// Analyzes conversation topics and their relevance
/// </summary>
public class TopicAnalyzer
{
    private static readonly Dictionary<string, HashSet<string>> TopicKeywords = new()
    {
        ["product"] = new() { "sản phẩm", "mỹ phẩm", "kem", "serum", "toner", "lotion", "mask", "son" },
        ["price"] = new() { "giá", "bao nhiêu", "tiền", "đắt", "rẻ", "giá cả", "chi phí" },
        ["shipping"] = new() { "giao", "ship", "vận chuyển", "nhận hàng", "giao hàng", "chuyển phát" },
        ["quality"] = new() { "chất lượng", "tốt", "xịn", "fake", "thật", "hàng thật", "chính hãng" },
        ["usage"] = new() { "dùng", "sử dụng", "cách dùng", "thoa", "bôi", "apply" },
        ["ingredients"] = new() { "thành phần", "có gì", "chứa", "ingredient", "công thức" }
    };

    /// <summary>
    /// Extract dominant topics from conversation history
    /// </summary>
    public List<ConversationTopic> ExtractTopics(List<ConversationMessage> history)
    {
        var topics = new List<ConversationTopic>();

        if (history.Count == 0)
            return topics;

        // Count mentions for each topic
        var topicMentions = new Dictionary<string, List<string>>();

        foreach (var message in history)
        {
            if (message.Role != "user")
                continue;

            var content = message.Content.ToLower();

            foreach (var (topicName, keywords) in TopicKeywords)
            {
                var matchedKeywords = keywords.Where(kw => content.Contains(kw)).ToList();

                if (matchedKeywords.Any())
                {
                    if (!topicMentions.ContainsKey(topicName))
                    {
                        topicMentions[topicName] = new List<string>();
                    }
                    topicMentions[topicName].AddRange(matchedKeywords);
                }
            }
        }

        // Calculate relevance and create topic objects
        var totalMentions = topicMentions.Values.Sum(list => list.Count);

        foreach (var (topicName, keywords) in topicMentions)
        {
            var mentionCount = keywords.Count;
            var relevance = totalMentions > 0 ? (double)mentionCount / totalMentions : 0;

            topics.Add(new ConversationTopic
            {
                Name = topicName,
                MentionCount = mentionCount,
                Relevance = relevance,
                Keywords = keywords.Distinct().ToList()
            });
        }

        // Sort by relevance descending
        return topics.OrderByDescending(t => t.Relevance).ToList();
    }

    /// <summary>
    /// Calculate relevance score for a specific topic
    /// </summary>
    public double CalculateRelevance(string topicName, List<ConversationMessage> history)
    {
        if (!TopicKeywords.ContainsKey(topicName))
            return 0;

        var keywords = TopicKeywords[topicName];
        var mentionCount = 0;
        var totalWords = 0;

        foreach (var message in history)
        {
            if (message.Role != "user")
                continue;

            var content = message.Content.ToLower();
            var words = content.Split(new[] { ' ', ',', '.', '?', '!' }, StringSplitOptions.RemoveEmptyEntries);
            totalWords += words.Length;

            mentionCount += keywords.Count(kw => content.Contains(kw));
        }

        return totalWords > 0 ? (double)mentionCount / totalWords : 0;
    }

    /// <summary>
    /// Track topic shifts across conversation
    /// </summary>
    public List<(int TurnIndex, string FromTopic, string ToTopic)> TrackTopicShifts(List<ConversationMessage> history)
    {
        var shifts = new List<(int, string, string)>();
        var userMessages = history
            .Select((msg, idx) => new { Message = msg, Index = idx })
            .Where(x => x.Message.Role == "user")
            .ToList();

        if (userMessages.Count < 2)
            return shifts;

        string? previousTopic = null;

        for (int i = 0; i < userMessages.Count; i++)
        {
            var content = userMessages[i].Message.Content.ToLower();
            var currentTopic = GetDominantTopic(content);

            if (currentTopic != null && previousTopic != null && currentTopic != previousTopic)
            {
                shifts.Add((userMessages[i].Index, previousTopic, currentTopic));
            }

            if (currentTopic != null)
            {
                previousTopic = currentTopic;
            }
        }

        return shifts;
    }

    /// <summary>
    /// Get the dominant topic in a message
    /// </summary>
    private string? GetDominantTopic(string content)
    {
        var topicScores = new Dictionary<string, int>();

        foreach (var (topicName, keywords) in TopicKeywords)
        {
            var score = keywords.Count(kw => content.Contains(kw));
            if (score > 0)
            {
                topicScores[topicName] = score;
            }
        }

        return topicScores.Any()
            ? topicScores.OrderByDescending(kvp => kvp.Value).First().Key
            : null;
    }
}
