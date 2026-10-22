using HotChocolate.AspNetCore.Instrumentation;
using HotChocolate.Execution;
using HotChocolate.Execution.Instrumentation;
using HotChocolate.Language;
using StargateAPI.Application.Logging;

namespace StargateAPI.GraphQL
{
    /// <summary>
    /// Names each GraphQL request for its process log row. Every request posts to the same URL,
    /// so the name comes from the operation: its type and root fields, such as
    /// <c>mutation assignDuty</c> or <c>query person+businessCalendar</c>. The document has passed
    /// validation by then, so these are schema field names (never aliases, the client's operation
    /// name or argument values, which may contain personal names).
    /// </summary>
    public sealed class OperationNameListener : ExecutionDiagnosticEventListener
    {
        public override IDisposable ExecuteOperation(RequestContext context)
        {
            if (context.RequestServices.GetService<OperationContext>() is { } operation
                && context.OperationDocumentInfo.Document is { } document
                && SelectOperation(document, context.Request.OperationName) is { } definition)
            {
                var fields = definition.SelectionSet.Selections.OfType<FieldNode>().Select(field => field.Name.Value);
                operation.OperationName =
                    $"{definition.Operation.ToString().ToLowerInvariant()} {string.Join('+', fields.Distinct())}";
            }
            return EmptyScope;
        }

        private static OperationDefinitionNode? SelectOperation(DocumentNode document, string? operationName)
        {
            var operations = document.Definitions.OfType<OperationDefinitionNode>().ToList();
            return operationName is null
                ? operations.Count == 1 ? operations[0] : null
                : operations.SingleOrDefault(o => o.Name?.Value == operationName);
        }
    }

    /// <summary>
    /// Gives a GraphQL request a name before parsing, so one that never reaches execution (a
    /// syntax or validation error) is still logged as <c>graphql</c> rather than unmatched.
    /// </summary>
    public sealed class GraphQLRequestNameListener : ServerDiagnosticEventListener
    {
        public override IDisposable ExecuteHttpRequest(HttpContext context, HttpRequestKind kind)
        {
            if (context.RequestServices.GetService<OperationContext>() is { } operation)
            {
                operation.OperationName ??= "graphql";
            }
            return EmptyScope;
        }
    }
}
