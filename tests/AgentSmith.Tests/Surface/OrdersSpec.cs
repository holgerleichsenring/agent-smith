using AgentSmith.Contracts.Providers;

namespace AgentSmith.Tests.Surface;

/// <summary>
/// 2026-08-30-c6ec: a served description in the shape the provider produces one — the
/// operations already parsed, each body and response still a raw fragment, and the
/// document itself carrying the components those fragments reference. The reference is
/// the point: an operation whose body is a named component must still state the
/// properties it accepts.
/// </summary>
internal static class OrdersSpec
{
    private const string Document =
        """
        {"openapi":"3.0.0","info":{"title":"Orders","version":"1.0"},
         "components":{"schemas":{
           "NewOrder":{"type":"object","properties":{
             "customerId":{"type":"string"},"discountPercent":{"type":"number"}}},
           "Order":{"type":"object","properties":{
             "id":{"type":"string"},"internalNote":{"type":"string"}}}}}}
        """;

    private const string OrderBody =
        """{"content":{"application/json":{"schema":{"$ref":"#/components/schemas/NewOrder"}}}}""";

    private const string OrderResponses =
        """
        {"201":{"content":{"application/json":{"schema":{"$ref":"#/components/schemas/Order"}}}},
         "409":{"content":{"application/json":{"schema":{"type":"object",
           "properties":{"conflictReason":{"type":"string"}}}}}}}
        """;

    public static SwaggerSpec Spec() => new(
        "Orders", "1.0",
        [
            new ApiEndpoint("POST", "/orders", "createOrder", [], true, OrderBody, OrderResponses),
            new ApiEndpoint(
                "DELETE", "/orders/{id}", "deleteOrder",
                [new ApiParameter("id", "path", "string", true)], true, null, null),
        ],
        [],
        Document);
}
