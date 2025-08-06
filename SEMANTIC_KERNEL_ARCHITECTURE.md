# Semantic Kernel Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                              Frontend (React)                                   │
│                         WebRTC Audio + Chat Interface                           │
└─────────────────────────┬───────────────────────────────────────────────────────┘
                          │ HTTP API Calls
                          ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                        .NET API Controller                                      │
│  ┌─────────────────────────────────────────────────────────────────────────┐    │
│  │              AzureOpenAIController.cs                                   │    │
│  │  • /classify-intent → Intent Classification                             │    │
│  │  • /query → NL2SQL Pipeline                                             │    │
│  │  • /sessions → Realtime Audio Setup                                     │    │
│  └─────────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────┬───────────────────────────────────────────────────────┘
                          │ Service Injection
                          ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                       AzureOpenAiService                                        │
│  ┌─────────────────────────────────────────────────────────────────────────┐    │
│  │                    Semantic Kernel Orchestration                        │    │
│  │                                                                         │    │
│  │  ClassifyIntent() ──────► IntentClassificationPlugin                    │    │
│  │  RewriteQuery() ────────► QueryRewritePlugin                            │    │
│  │  GenerateSqlQuery() ────► SqlGenerationPlugin                           │    │
│  └─────────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────┬───────────────────────────────────────────────────────┘
                          │ Kernel.InvokeAsync()
                          ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                       Semantic Kernel Core                                      │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────┐ │
│  │ Intent          │  │ Query Rewrite   │  │ SQL Generation  │  │ Database    │ │
│  │ Classification  │  │ Plugin          │  │ Plugin          │  │ Plugin      │ │
│  │ Plugin          │  │                 │  │                 │  │             │ │
│  │                 │  │ • Context       │  │ • Schema-aware  │  │ • Execute   │ │
│  │ • STATISTICAL   │  │   enhancement   │  │ • Error retry   │  │   SQL       │ │
│  │ • CONVERSATIONAL│  │ • Follow-up     │  │ • JSON schema   │  │ • Format    │ │
│  │                 │  │   resolution    │  │   integration   │  │   results   │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘  └─────────────┘ │
└─────────────────────────┬───────────────────────────────────────────────────────┘
                          │ Plugin Function Calls
                          ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                      Azure OpenAI Service                                       │
│  ┌─────────────────────────────────────────────────────────────────────────┐    │
│  │                  GPT-4o Chat Completions                                │    │
│  │  • Intent classification prompts                                        │    │
│  │  • Query enhancement prompts                                            │    │
│  │  • NL2SQL generation prompts                                            │    │
│  │  • Context-aware responses                                              │    │
│  └─────────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                        Azure SQL Database                                       │
│  ┌─────────────────────────────────────────────────────────────────────────┐    │
│  │                  Formula One Statistics                                 │    │
│  │  • F1Records (drivers, points, wins)                                    │    │
│  │  • Constructor Championships                                            │    │
│  │  • Circuit Winners                                                      │    │
│  │  • Race Results (2014-2023)                                             │    │
│  └─────────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────────────┘

Data Flow:
1. User voice/text → Frontend
2. Frontend → API Controller
3. Controller → AzureOpenAiService
4. Service → Semantic Kernel Plugins
5. Plugins → Azure OpenAI
6. SQL Plugin → Database
7. Results → JSON → Frontend
```

## Plugin Function Call Flow

### Statistical Query Processing

```
User Query: "Who won the most races in 2023?"

┌─────────────────────────────────────────────────────────────────────┐
│ 1. Intent Classification                                            │
│    Input: conversationHistory + userQuery                           │
│    Plugin: IntentClassificationPlugin.ClassifyIntentAsync()         │
│    Output: "STATISTICAL"                                            │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 2. Query Rewriting                                                  │
│    Input: conversationHistory + userQuery                           │
│    Plugin: QueryRewritePlugin.RewriteQueryAsync()                   │
│    Output: "Who won the most races in the 2023 Formula One season?" │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 3. SQL Generation                                                   │
│    Input: rewrittenQuery + jsonSchema                               │
│    Plugin: SqlGenerationPlugin.GenerateSqlQueryAsync()              │
│    Output: "SELECT TOP 1 DriverName, COUNT(*) as Wins FROM..."      │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 4. Database Execution                                               │
│    Input: sqlQuery                                                  │
│    Plugin: DatabasePlugin.ExecuteQueryAsync()                       │
│    Output: JSON formatted results                                   │
└─────────────────────────────────────────────────────────────────────┘
```

### Follow-up Query Processing

```
Previous: "Who won the most races in 2023?" → "Max Verstappen"
Follow-up: "How many points did he score?"

┌─────────────────────────────────────────────────────────────────────┐
│ 1. Intent Classification                                            │
│    Input: "User: Who won most races 2023? Assistant: Max Verstappen"│
│           + "How many points did he score?"                         │
│    Plugin: IntentClassificationPlugin.ClassifyIntentAsync()         │
│    Output: "STATISTICAL" (due to conversation context)              │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 2. Query Rewriting (Context Enhancement)                            │
│    Input: Full conversation history                                 │
│    Plugin: QueryRewritePlugin.RewriteQueryAsync()                   │
│    Output: "How many points did Max Verstappen score in 2023?"      │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 3. SQL Generation & Execution                                       │
│    Enhanced query enables proper SQL generation                     │
│    Output: Accurate points total for Max Verstappen in 2023         │
└─────────────────────────────────────────────────────────────────────┘
```
