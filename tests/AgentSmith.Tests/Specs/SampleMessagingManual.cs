namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-17-293ba: a migration manual of the shape that cut a live run wrong — shared setup every
/// service follows, then a publisher variant and a consumer variant that must not reach each other's
/// service — and the cut that gives each of two specs the shared part plus its own variant.
/// Segments: 1 title, 2-3 shared, 4-5 publisher, 6-7 consumer, 8 sign-off.
/// </summary>
public static class SampleMessagingManual
{
    public const string PublisherTemplate = "o.PublishMessage<OrderPlaced>().ToTopic(\"orders\");";
    public const string ConsumerTemplate =
        "o.ListenToQueue(\"orders-billing\").UseQuorum().WithDeadLetterQueue(\"orders-billing-dlq\");";
    public const string SharedTemplate = "services.AddSampleMessaging(o => o.UseKebabCaseNames());";

    public const string Ticket = $$"""
        # Sample.Messaging migration manual

        Shared setup: every service references Sample.Messaging 3.0, names its endpoints with the kebab-case formatter and uses no Sample.Messaging.Legacy API.

        ```csharp
        {{SharedTemplate}}
        ```

        Publisher variant: a service that only publishes declares no listener queue and no dead-letter queue.

        ```csharp
        {{PublisherTemplate}}
        ```

        Consumer variant: every listener gets a quorum queue and its own dead-letter queue.

        ```csharp
        {{ConsumerTemplate}}
        ```

        Thanks, the platform team
        """;

    /// <summary>The cut in the shape the skill now asks for: shared stated once, each spec its own variant.</summary>
    public const string SharedCut = """
        {"specs": [
           {"slug": "publisher-messaging", "goal": "The server repository publishes through Sample.Messaging 3.0",
            "steps": [{"id": "publish", "action": "Move the publisher onto Sample.Messaging 3.0"}],
            "done": ["The server repository references Sample.Messaging 3.0."], "carries": [4, 5]},
           {"slug": "consumer-messaging", "goal": "The worker repository consumes through Sample.Messaging 3.0",
            "steps": [{"id": "consume", "action": "Move the listeners onto Sample.Messaging 3.0"}],
            "done": ["The worker repository references Sample.Messaging 3.0."], "carries": [6, 7]}],
         "shared": [1, 2, 3],
         "discarded": [{"segment": 8, "reason": "a sign-off"}],
         "ignored_instructions": [],
         "handback": {"case": "none", "reason": ""}}
        """;
}
