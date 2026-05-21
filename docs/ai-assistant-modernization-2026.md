# AI Assistant Modernization 2026

هذا المستند يضع التصميم المستهدف لتحديث `AIAssistantService` من خدمة monolithic إلى منصة AI حديثة قابلة للتوسع، مع الحفاظ على:

- Arabic-first responses
- Role-based permissions
- Daily limits
- Direct Response Engine الحالي
- تكامل تدريجي بدون كسر `AIAssistantController`

## 1. Target Folder Structure

```text
TrainingCenterManagement_MVC/
├─ Controllers/
│  └─ AIAssistantController.cs
├─ Services/
│  ├─ AI/
│  │  ├─ Abstractions/
│  │  │  ├─ IAIAssistantService.cs
│  │  │  ├─ IAIConversationOrchestrator.cs
│  │  │  ├─ IConversationMemoryService.cs
│  │  │  ├─ IAIContextRetriever.cs
│  │  │  ├─ IAIStreamingService.cs
│  │  │  ├─ IDirectResponseEngine.cs
│  │  │  ├─ IAgentFactory.cs
│  │  │  ├─ IToolAccessPolicy.cs
│  │  │  └─ IContentSafetyService.cs
│  │  ├─ Contracts/
│  │  │  ├─ AIAssistantRequest.cs
│  │  │  ├─ AIAssistantResponse.cs
│  │  │  ├─ AIStreamChunk.cs
│  │  │  ├─ AIUserContext.cs
│  │  │  ├─ AIRetrievalDocument.cs
│  │  │  └─ AIToolCallContext.cs
│  │  ├─ Orchestration/
│  │  │  ├─ AIAssistantService.cs
│  │  │  ├─ AIConversationOrchestrator.cs
│  │  │  ├─ AgentSupervisor.cs
│  │  │  └─ AgentExecutionPipeline.cs
│  │  ├─ Agents/
│  │  │  ├─ AgentFactory.cs
│  │  │  ├─ AgentRegistry.cs
│  │  │  ├─ Profiles/
│  │  │  │  ├─ SupervisorAgentProfile.cs
│  │  │  │  ├─ TraineeAgentProfile.cs
│  │  │  │  ├─ TrainerAgentProfile.cs
│  │  │  │  ├─ ReceptionistAgentProfile.cs
│  │  │  │  └─ AdminAgentProfile.cs
│  │  ├─ Plugins/
│  │  │  ├─ TraineePlugin.cs
│  │  │  ├─ TrainerPlugin.cs
│  │  │  ├─ ReceptionistPlugin.cs
│  │  │  ├─ AdminPlugin.cs
│  │  │  ├─ CommonKnowledgePlugin.cs
│  │  │  └─ CertificatePlugin.cs
│  │  ├─ Retrieval/
│  │  │  ├─ PgVectorContextRetriever.cs
│  │  │  ├─ EmbeddingGenerationService.cs
│  │  │  ├─ VectorIndexingService.cs
│  │  │  ├─ DocumentChunker.cs
│  │  │  └─ RetrievalQueryBuilder.cs
│  │  ├─ Memory/
│  │  │  ├─ RedisConversationMemoryService.cs
│  │  │  ├─ ConversationSummarizer.cs
│  │  │  └─ ChatHistoryRepository.cs
│  │  ├─ Safety/
│  │  │  ├─ ContentSafetyService.cs
│  │  │  ├─ PromptGuardService.cs
│  │  │  └─ ToolGuardService.cs
│  │  ├─ Observability/
│  │  │  ├─ AITelemetry.cs
│  │  │  ├─ AIActivitySource.cs
│  │  │  └─ AILoggingExtensions.cs
│  │  ├─ Providers/
│  │  │  ├─ ChatClientFactory.cs
│  │  │  ├─ ProviderOptions.cs
│  │  │  └─ ProviderHealthService.cs
│  │  ├─ Permissions/
│  │  │  ├─ ToolAccessPolicy.cs
│  │  │  └─ UsageLimitPolicy.cs
│  │  ├─ Direct/
│  │  │  ├─ DirectResponseEngine.cs
│  │  │  ├─ DirectResponseClassifier.cs
│  │  │  └─ DirectResponseTemplates.cs
│  │  └─ Configuration/
│  │     ├─ AIAssistantOptions.cs
│  │     ├─ AgentOptions.cs
│  │     ├─ RagOptions.cs
│  │     └─ CacheOptions.cs
│  └─ ...
├─ Data/
│  ├─ AI/
│  │  ├─ AIConversationSummary.cs
│  │  ├─ AIEmbeddingDocument.cs
│  │  ├─ AIEmbeddingChunk.cs
│  │  └─ AIToolAuditLog.cs
├─ BackgroundJobs/
│  └─ AI/
│     ├─ RebuildEmbeddingsJob.cs
│     ├─ SyncCourseEmbeddingsJob.cs
│     └─ SyncKnowledgeEmbeddingsJob.cs
└─ docs/
   └─ ai-assistant-modernization-2026.md
```

## 2. Why This Structure

- `Abstractions`: تثبيت العقود حتى لا يعود الـ controller أو الـ UI مرتبطًا بتفاصيل provider أو framework.
- `Orchestration`: كل منطق القرار هنا: direct answer، retrieval، agent routing، safety، streaming.
- `Plugins`: كل tool يصبح plugin حقيقي بواجهات واضحة، بدل `ExecuteToolAsync`.
- `Retrieval`: فصل RAG عن orchestrator لتسهيل تغيير vector store أو ranking.
- `Memory`: فصل Redis + summaries + persistence لتقليل token cost.
- `Safety` و `Permissions`: الحراسة يجب أن تكون طبقة مستقلة وليست شرطًا داخل prompt فقط.
- `Observability`: tracing و metrics و logs يجب أن تُبنى كجزء من architecture وليست إضافة لاحقة.

## 3. New Main Interface

الواجهة الجديدة يجب أن تدعم:

- synchronous request/response
- streaming
- conversation context
- cancellation
- metadata and citations
- migration path من `AskQuestionAsync`

```csharp
using System.Runtime.CompilerServices;

namespace TrainingCenterManagement_MVC.Services.AI.Abstractions;

public interface IAIAssistantService
{
    Task<AIAssistantResponse> AskAsync(
        AIAssistantRequest request,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<AIStreamChunk> StreamAsync(
        AIAssistantRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AIChatMessage>> GetChatHistoryAsync(
        string userId,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<bool> RateResponseAsync(
        Guid messageId,
        string userId,
        int rating,
        string? feedback,
        CancellationToken cancellationToken = default);

    Task<AIStatistics> GetStatisticsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteMessageAsync(
        Guid messageId,
        string userId,
        CancellationToken cancellationToken = default);
}
```

### Request Contract

```csharp
namespace TrainingCenterManagement_MVC.Services.AI.Contracts;

public sealed record AIAssistantRequest(
    string UserId,
    string Message,
    string? ConversationId,
    string? Role,
    string? IpAddress,
    string? UserAgent,
    bool EnableStreaming = false,
    bool ForceDirectEngineOnly = false,
    IReadOnlyDictionary<string, object?>? Metadata = null);
```

### Response Contract

```csharp
namespace TrainingCenterManagement_MVC.Services.AI.Contracts;

public sealed record AIAssistantResponse(
    Guid MessageId,
    string ConversationId,
    string Role,
    string Provider,
    string Model,
    string OutputText,
    bool FromDirectEngine,
    bool UsedRetrieval,
    IReadOnlyList<string> Citations,
    IReadOnlyList<string> UsedPlugins,
    IReadOnlyDictionary<string, object?> Metadata);
```

### Stream Contract

```csharp
namespace TrainingCenterManagement_MVC.Services.AI.Contracts;

public sealed record AIStreamChunk(
    string ConversationId,
    string EventType,
    string? Text,
    bool IsFinal,
    IReadOnlyDictionary<string, object?>? Metadata = null);
```

## 4. Recommended Core Pipeline

الـ pipeline المقترحة:

1. Validate request
2. Resolve user + role + permissions + daily limit
3. Run safety pre-check
4. Try `DirectResponseEngine`
5. If unresolved, fetch summarized history from Redis/persistence
6. Run intent classification and agent routing
7. Retrieve top-k chunks from PGVector
8. Create agent with allowed plugins only
9. Stream answer
10. Persist full transcript + summary refresh + telemetry

### Orchestrator Skeleton

```csharp
namespace TrainingCenterManagement_MVC.Services.AI.Orchestration;

public sealed class AIConversationOrchestrator : IAIConversationOrchestrator
{
    private readonly IDirectResponseEngine _directEngine;
    private readonly IAIContextRetriever _retriever;
    private readonly IConversationMemoryService _memory;
    private readonly IAgentFactory _agentFactory;
    private readonly IContentSafetyService _safety;
    private readonly IToolAccessPolicy _toolAccessPolicy;
    private readonly ILogger<AIConversationOrchestrator> _logger;

    public async Task<AIAssistantResponse> ExecuteAsync(
        AIAssistantRequest request,
        CancellationToken cancellationToken)
    {
        await _safety.ValidateInputAsync(request, cancellationToken);

        var direct = await _directEngine.TryAnswerAsync(request, cancellationToken);
        if (direct is not null)
        {
            return direct;
        }

        var memory = await _memory.GetContextAsync(request.UserId, request.ConversationId, cancellationToken);
        var retrieval = await _retriever.RetrieveAsync(request, memory, cancellationToken);
        var toolPolicy = await _toolAccessPolicy.BuildAsync(request.UserId, request.Role, cancellationToken);
        var agent = await _agentFactory.CreateAsync(request, memory, retrieval, toolPolicy, cancellationToken);

        var response = await agent.InvokeAsync(request.Message, cancellationToken);
        return response;
    }
}
```

## 5. `IChatClient` Strategy

الطبقة المستهدفة هي `Microsoft.Extensions.AI` بحيث يصبح الكود محايدًا للمزوّد:

```csharp
services.AddChatClient(chatClient =>
{
    // OpenAI / Azure OpenAI / Anthropic adapter / local provider
});
```

التوصية العملية للمشروع:

- استخدم `IChatClient` كـ primary abstraction للمحادثة والـ streaming.
- استخدم Semantic Kernel في طبقة agent/plugins لأن ecosystem الخاص به أنضج عادة في الإنتاج من بناء manual agent loop.
- إذا أصبح Microsoft Agent Framework مستقرًا في بيئتكم، اجعل `IAgentFactory` قادراً على التبديل بين:
  - `SemanticKernelAgentFactory`
  - `MicrosoftAgentFrameworkFactory`

بالتالي الـ controller و`IAIAssistantService` لا يتأثران.

## 6. Example Plugin: `TraineePlugin`

كل أداة يجب أن:

- تكون صغيرة وواضحة
- ترجع بيانات محددة وليس prompt-ready paragraphs فقط
- تصف الصلاحيات المطلوبة
- تستخدم `CancellationToken`
- تسجل عمليات الوصول الحساسة

### Example Design

```csharp
using Microsoft.SemanticKernel;
using Microsoft.EntityFrameworkCore;
using TrainingCenterManagement_MVC.Data;

namespace TrainingCenterManagement_MVC.Services.AI.Plugins;

public sealed class TraineePlugin
{
    private readonly ApplicationDbContext _db;
    private readonly IAIPermissionService _permissions;

    public TraineePlugin(ApplicationDbContext db, IAIPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    [KernelFunction("get_my_courses")]
    [Description("Returns the authenticated trainee's enrolled courses with status, schedule, trainer name, and progress. Use when the user asks about my courses, registrations, course schedule, or progress.")]
    public async Task<IReadOnlyList<TraineeCourseDto>> GetMyCoursesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _db.CourseTrainees
            .AsNoTracking()
            .Where(x => x.Trainee.UserId == userId)
            .Select(x => new TraineeCourseDto(
                x.CourseId,
                x.Course.CourseName,
                x.Course.StartDate,
                x.Course.EndDate,
                x.Course.Status,
                x.Course.Trainers
                    .Select(t => t.Trainer.FullName)
                    .FirstOrDefault(),
                x.ProgressPercentage))
            .ToListAsync(cancellationToken);
    }

    [KernelFunction("get_my_exam_results")]
    [Description("Returns the authenticated trainee's exam attempts and scores. Use for exam result, exam score, pass/fail, and attempt history questions.")]
    public async Task<IReadOnlyList<TraineeExamResultDto>> GetMyExamResultsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _db.ExamAttempts
            .AsNoTracking()
            .Where(x => x.Trainee.UserId == userId)
            .OrderByDescending(x => x.StartedAt)
            .Select(x => new TraineeExamResultDto(
                x.ExamId,
                x.Exam.Title,
                x.Score,
                x.Status.ToString(),
                x.StartedAt,
                x.SubmittedAt))
            .ToListAsync(cancellationToken);
    }

    [KernelFunction("get_my_certificates")]
    [Description("Returns certificates belonging to the authenticated trainee. Use when the user asks about certificate availability, certificate verification, or issued certificates.")]
    public async Task<IReadOnlyList<TraineeCertificateDto>> GetMyCertificatesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Certificates
            .AsNoTracking()
            .Where(x => x.Trainee.UserId == userId)
            .OrderByDescending(x => x.IssueDate)
            .Select(x => new TraineeCertificateDto(
                x.Id,
                x.CourseName,
                x.CertificateNumber,
                x.IssueDate))
            .ToListAsync(cancellationToken);
    }
}

public sealed record TraineeCourseDto(
    int CourseId,
    string CourseName,
    DateTime? StartDate,
    DateTime? EndDate,
    string? Status,
    string? TrainerName,
    double? ProgressPercentage);

public sealed record TraineeExamResultDto(
    int ExamId,
    string ExamTitle,
    double? Score,
    string Status,
    DateTime StartedAt,
    DateTime? SubmittedAt);

public sealed record TraineeCertificateDto(
    int CertificateId,
    string CourseName,
    string CertificateNumber,
    DateTime IssueDate);
```

### Plugin Design Rules

- اسم function يجب أن يصف intent الفعلي وليس مصدر البيانات.
- الوصف يجب أن يخبر الوكيل متى يستخدم الأداة.
- return DTO يجب أن يكون structured وصغيرًا.
- لا ترجع HTML ولا جمل طويلة من داخل الـ plugin.
- كل plugin role-scoped، ولا يُسجَّل إلا إذا سمحت policy بذلك.

## 7. Agent Initialization

### Recommended Path

- `IChatClient`: abstraction للموديل والمزود
- `Semantic Kernel`: plugins + function calling + agent memory orchestration
- `IAgentFactory`: نقطة فصل حتى يمكن التبديل لاحقًا إلى MAF

### Agent Factory Shape

```csharp
namespace TrainingCenterManagement_MVC.Services.AI.Agents;

public interface IAgentFactory
{
    Task<IAgentRuntime> CreateAsync(
        AIAssistantRequest request,
        ConversationMemoryContext memory,
        RetrievalContext retrieval,
        ToolAccessPolicySnapshot toolPolicy,
        CancellationToken cancellationToken = default);
}
```

### Semantic Kernel Example

```csharp
using Microsoft.SemanticKernel;

namespace TrainingCenterManagement_MVC.Services.AI.Agents;

public sealed class SemanticKernelAgentFactory : IAgentFactory
{
    private readonly IServiceProvider _services;
    private readonly IChatClient _chatClient;

    public SemanticKernelAgentFactory(IServiceProvider services, IChatClient chatClient)
    {
        _services = services;
        _chatClient = chatClient;
    }

    public Task<IAgentRuntime> CreateAsync(
        AIAssistantRequest request,
        ConversationMemoryContext memory,
        RetrievalContext retrieval,
        ToolAccessPolicySnapshot toolPolicy,
        CancellationToken cancellationToken = default)
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(_chatClient);

        var kernel = builder.Build();

        if (toolPolicy.AllowTraineeTools)
            kernel.Plugins.AddFromObject(_services.GetRequiredService<TraineePlugin>(), "Trainee");

        if (toolPolicy.AllowTrainerTools)
            kernel.Plugins.AddFromObject(_services.GetRequiredService<TrainerPlugin>(), "Trainer");

        if (toolPolicy.AllowReceptionistTools)
            kernel.Plugins.AddFromObject(_services.GetRequiredService<ReceptionistPlugin>(), "Receptionist");

        if (toolPolicy.AllowAdminTools)
            kernel.Plugins.AddFromObject(_services.GetRequiredService<AdminPlugin>(), "Admin");

        kernel.Plugins.AddFromObject(_services.GetRequiredService<CommonKnowledgePlugin>(), "CommonKnowledge");

        return Task.FromResult<IAgentRuntime>(
            new SemanticKernelAgentRuntime(kernel, request, memory, retrieval));
    }
}
```

### Supervisor Model

التركيب المقترح:

- `SupervisorAgent`
  - يقرر: direct answer / retrieval / plugin / specialist
- `TraineeSpecialistAgent`
  - أسئلة التسجيل، التقدم، الامتحانات، الشهادات
- `TrainerSpecialistAgent`
  - الحضور، المحاضرات، المهام التعليمية
- `ReceptionistSpecialistAgent`
  - المدفوعات، التسجيل، الحجوزات
- `AdminSpecialistAgent`
  - التقارير، الصلاحيات، إعدادات النظام

قاعدة مهمة:

- لا تستخدم multi-agent لكل سؤال بسيط.
- ابدأ بـ supervisor + single specialist handoff.
- فعل multi-agent الحقيقي فقط للحالات المركبة أو workflows متعددة النطاقات.

## 8. Direct Engine Integration

الـ Direct Response Engine الحالي ممتاز كطبقة low-cost/high-speed ويجب الحفاظ عليه، لكن يجب عزله.

### Recommended Shape

```csharp
namespace TrainingCenterManagement_MVC.Services.AI.Direct;

public interface IDirectResponseEngine
{
    Task<AIAssistantResponse?> TryAnswerAsync(
        AIAssistantRequest request,
        CancellationToken cancellationToken = default);
}
```

### Integration Rule

- direct engine قبل أي LLM call
- إذا أعاد `null` ننتقل إلى retrieval + agent
- إذا أعاد إجابة:
  - تُحفظ في التاريخ
  - تُحسب ضمن limits
  - تُرسل بدون token cost

### Decision Logic

استخدم classifier صغيرًا بدل `IsDataQuestion()` الحالي:

- `DirectFaq`
- `DirectPersonalData`
- `NeedsRag`
- `NeedsToolCalling`
- `NeedsHumanEscalation`

وبذلك تصبح المعالجة:

```csharp
var direct = await _directEngine.TryAnswerAsync(request, cancellationToken);
if (direct is not null)
{
    return direct with
    {
        Metadata = new Dictionary<string, object?>
        {
            ["pipeline"] = "direct",
            ["costTier"] = "low"
        }
    };
}

// else continue with RAG/agent pipeline
```

## 9. RAG and Context Management

التحسين الأساسي هنا هو التوقف عن ضخ كامل البيانات داخل prompt.

### What To Embed

- courses
- lectures
- exams
- certificates
- AI knowledge entries
- policies and FAQs
- user-personalized snapshots

### Important Separation

- domain embeddings:
  - courses, exams, certificates, KB
- personalized runtime facts:
  - current trainee registrations
  - current trainer assignments
  - balance, payment state

المعلومات الشخصية الديناميكية لا يجب الاعتماد فيها على vector retrieval فقط. الأفضل:

- retrieval للمعرفة العامة وشبه الثابتة
- plugins/data access للحقائق الدقيقة والحساسة

### Retrieval Contract

```csharp
public interface IAIContextRetriever
{
    Task<RetrievalContext> RetrieveAsync(
        AIAssistantRequest request,
        ConversationMemoryContext memory,
        CancellationToken cancellationToken = default);
}

public sealed record RetrievalContext(
    bool UsedRetrieval,
    IReadOnlyList<AIRetrievalDocument> Documents,
    string SearchQuery,
    string? AppliedFilter);
```

### PGVector Guidance

- استخدم PostgreSQL + pgvector إذا كان لديكم استعداد تشغيلي جيد
- وإن كان المشروع مرتبطًا بقوة بـ SQL Server اليوم:
  - ابدأ abstraction `IAIEmbeddingStore`
  - ثم نفذ:
    - `PgVectorEmbeddingStore`
    - `SqlServerFallbackEmbeddingStore` لاحقًا إذا لزم

### Retrieval Defaults

- `TopK = 8`
- hybrid retrieval:
  - vector similarity
  - keyword fallback
- chunk size:
  - 500 to 900 tokens
- chunk overlap:
  - 80 to 120 tokens
- per-role filters:
  - trainee لا يرى admin chunks
  - trainer لا يرى finance-only chunks

## 10. Memory, Caching, and Cost Control

### Conversation Memory

- recent turns: Redis
- long-term history: SQL
- old conversations: summarized every N turns

### Summarization Policy

- after 12 to 20 message turns
- summarize into Arabic
- preserve:
  - user goals
  - unresolved tasks
  - constraints
  - important entities
  - user preferences

### Caching Layers

- `Config Cache`: AI settings and provider routing
- `Retrieval Cache`: normalized query + role + user scope
- `Prompt Fragment Cache`: static Arabic instructions, policy blocks
- `Tool Result Cache`: public/non-sensitive results only

### Do Not Cache

- raw personal financial data
- role escalation decisions
- unsafe prompts/results

## 11. Safety and Guardrails

### Required Guardrails

- prompt injection detection
- role boundary enforcement
- tool whitelist per role
- output moderation
- PII-aware logging

### Tool Guard Example

```csharp
public interface IToolAccessPolicy
{
    Task<ToolAccessPolicySnapshot> BuildAsync(
        string userId,
        string? role,
        CancellationToken cancellationToken = default);
}

public sealed record ToolAccessPolicySnapshot(
    bool AllowTraineeTools,
    bool AllowTrainerTools,
    bool AllowReceptionistTools,
    bool AllowAdminTools,
    IReadOnlySet<string> AllowedFunctions);
```

قاعدة تنفيذية مهمة:

- حتى لو حاول الـ model استدعاء function غير مسموحة، الرفض يجب أن يكون من التطبيق لا من الـ prompt فقط.

## 12. Observability

### Add OpenTelemetry For

- request span
- retrieval span
- tool execution span
- provider call span
- summarization span
- cache hit/miss metrics

### Recommended Metrics

- `ai_requests_total`
- `ai_direct_answers_total`
- `ai_rag_answers_total`
- `ai_tool_calls_total`
- `ai_tool_call_failures_total`
- `ai_provider_latency_ms`
- `ai_token_input_total`
- `ai_token_output_total`
- `ai_cache_hit_ratio`

### Log Structure

- `UserId`
- `ConversationId`
- `Role`
- `Agent`
- `Provider`
- `Model`
- `SafetyDecision`
- `ToolNames`
- `LatencyMs`
- `CostEstimate`

## 13. Resilience and Error Handling

### Retry Policy

استخدم `Polly` للـ:

- transient HTTP failures
- rate limits
- provider timeouts

ولا تستخدم retries في:

- permission denied
- validation failures
- content safety blocks

### Fallback Strategy

1. direct engine
2. primary provider
3. secondary provider
4. safe Arabic fallback response

### User-Facing Errors

يجب أن تبقى عربية وواضحة ومقسمة مثل:

- `AI_UNAVAILABLE`
- `AI_RATE_LIMITED`
- `AI_PERMISSION_DENIED`
- `AI_UNSAFE_INPUT`
- `AI_CONTEXT_NOT_FOUND`

## 14. Dependency Injection Direction

### Transitional Registration

```csharp
builder.Services.AddScoped<TrainingCenterManagement_MVC.Services.AI.Abstractions.IAIAssistantService,
                           TrainingCenterManagement_MVC.Services.AI.Orchestration.AIAssistantService>();

builder.Services.AddScoped<IDirectResponseEngine, DirectResponseEngine>();
builder.Services.AddScoped<IAIConversationOrchestrator, AIConversationOrchestrator>();
builder.Services.AddScoped<IAIContextRetriever, PgVectorContextRetriever>();
builder.Services.AddScoped<IConversationMemoryService, RedisConversationMemoryService>();
builder.Services.AddScoped<IAgentFactory, SemanticKernelAgentFactory>();
builder.Services.AddScoped<IContentSafetyService, ContentSafetyService>();
builder.Services.AddScoped<IToolAccessPolicy, ToolAccessPolicy>();

builder.Services.AddScoped<TraineePlugin>();
builder.Services.AddScoped<TrainerPlugin>();
builder.Services.AddScoped<ReceptionistPlugin>();
builder.Services.AddScoped<AdminPlugin>();
builder.Services.AddScoped<CommonKnowledgePlugin>();
```

### Migration-Friendly Adapter

في المرحلة الانتقالية، يمكن إبقاء الـ controller كما هو تقريبًا عبر adapter:

```csharp
public sealed class LegacyAIAssistantServiceAdapter : Services.IAIAssistantService
{
    private readonly AI.Abstractions.IAIAssistantService _inner;

    public async Task<AIChatMessage> AskQuestionAsync(
        string userId,
        string question,
        string? ipAddress,
        string? userAgent)
    {
        var response = await _inner.AskAsync(new AIAssistantRequest(
            userId,
            question,
            ConversationId: null,
            Role: null,
            IpAddress: ipAddress,
            UserAgent: userAgent));

        return new AIChatMessage
        {
            MessageId = response.MessageId,
            UserId = userId,
            UserMessage = question,
            AIResponse = response.OutputText,
            DataAccessLog = response.Provider,
            IsAnswered = true,
            AnsweredAt = DateTime.UtcNow
        };
    }
}
```

## 15. Practical Migration Plan

### Phase 1

- extract `DirectResponseEngine`
- create new contracts
- add orchestrator
- keep existing DB/history tables
- keep current controller

### Phase 2

- move manual tools to plugins
- add `IChatClient`
- add streaming endpoint
- add structured telemetry

### Phase 3

- add Redis memory
- add summarization
- add vector indexing + PGVector retrieval

### Phase 4

- add supervisor + specialist agents
- add provider failover
- add safety policies and tool audit logs

## 16. Final Recommendations For This Project

أفضل قرار هندسي هنا ليس "إعادة كتابة كل شيء" بل "فصل الطبقات الصحيحة أولًا".

### Order I Recommend

1. Extract `DirectResponseEngine` from current `AIAssistantService`
2. Introduce new `IAIAssistantService` contracts and orchestrator
3. Replace manual tool execution with plugins
4. Add streaming API
5. Add Redis summaries
6. Add PGVector RAG
7. Add supervisor/specialist multi-agent routing

### Cost and Performance Notes

- direct engine يجب أن يخدم أكبر نسبة ممكنة من FAQ والأسئلة المتكررة
- لا تستدعِ RAG إلا عند الحاجة
- لا تستدعِ multi-agent إلا للحالات المركبة
- personal facts يجب أن تأتي من plugins/queries وليس من embeddings فقط
- summaries في Redis تقلل التكلفة أكثر من تمرير full history كل مرة

### Arabic-First Notes

- خزّن summaries بالعربية
- اجعل tool descriptions بالإنجليزية الوظيفية أو bilingual إذا كان model يتعامل معها أفضل
- اجعل system prompt الأساسي عربيًا مع مصطلحات domain ثابتة
- أضف normalization عربي للاستعلامات قبل retrieval

---

هذه الوثيقة تمثل الـ target architecture المقترحة للمشروع الحالي، مع مسار ترحيل تدريجي يحافظ على الخدمة الموجودة بدل كسرها دفعة واحدة.
