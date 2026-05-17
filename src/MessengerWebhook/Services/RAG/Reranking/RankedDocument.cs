namespace MessengerWebhook.Services.RAG.Reranking;

public record RankedDocument(string Id, string Text, double RelevanceScore);
