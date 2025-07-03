# Azure OpenAI Realtime Chat Demo - AI Agent Guide

## Project Overview
This is a **real-time voice chat application** with Azure OpenAI integration, featuring:
- React frontend with WebRTC for voice communication
- .NET 9 API backend handling Azure OpenAI Realtime API
- **NL2SQL capabilities** for database-driven responses using Formula One statistics
- **Intent classification** system to route queries appropriately

## Architecture & Data Flow

```
Browser (React) ←→ .NET API ←→ Azure OpenAI Realtime API
                    ↓
              Azure SQL Database (F1 Data)
```

**Critical Data Flow**:
1. Voice captured → WebRTC → Azure OpenAI transcription
2. **Intent classification**: Statistical vs Conversational queries
3. **Statistical queries**: NL2SQL generation → SQL execution → Results formatted for LLM
4. **Conversational queries**: Direct LLM response
5. Streaming audio responses back to user

## Project Structure

- `realtime-api-dotnet/` - .NET 9 API backend (main application)
- `azure-openai-demo/` - React frontend
- `DatabaseImporter/` - Utility for populating SQL database with CSV data

## Essential Configuration Patterns

### Backend Configuration
**Always use `appsettings.Local.json`** (gitignored) for secrets:
```json
{
  "AzureOpenAI": {
    "ResourceName": "your-resource",
    "RealtimeDeploymentName": "gpt-4o-realtime-preview", 
    "ChatDeploymentName": "gpt-4o",
    "ApiKey": "your-key",
    "ApiVersion": "2025-04-01-preview"
  },
  "DatabaseConnection": "Server=...",
  "Nl2SqlConfig": {
    "database": {
      "description": "Domain-specific description for SQL generation",
      "schemas": [{"name": "dbo", "tables": ["F1Records", "..."]}]
    }
  }
}
```

### Frontend Environment
Default API endpoint: `https://localhost:7254/api/AzureOpenAI` (configure via `REACT_APP_API_URL`)

## Key Development Patterns

### Intent Classification System
Located in `AzureOpenAiService.ClassifyIntent()` - determines if query needs database lookup:
- **STATISTICAL**: Triggers NL2SQL pipeline 
- **CONVERSATIONAL**: Direct LLM response

### NL2SQL Pipeline (Statistical Queries)
1. `RewriteQuery()` - Adds conversation context for follow-up questions
2. `GenerateSqlQuery()` - Uses database schema + prompts to generate SQL
3. **Retry mechanism**: 3 attempts with error feedback for SQL generation
4. Results formatted as markdown tables for LLM consumption

### WebRTC Audio Processing
- **24kHz sample rate** required for Azure OpenAI compatibility
- PCM16 conversion in `Controls.js:setupAudioProcessor()`
- Server VAD (Voice Activity Detection) configured in session setup

## Critical Files for Modifications

| Component | File | Purpose |
|-----------|------|---------|
| System Behavior | `Prompts/CorePrompts.cs` | All LLM prompts and AI behavior |
| Intent Logic | `Services/AzureOpenAiService.cs` | Query classification and NL2SQL |
| Frontend Logic | `src/components/Controls.js` | WebRTC, audio processing, intent handling |
| Data Access | `Services/DatabaseService.cs` | SQL execution with error handling |

### Prompt Management (`CorePrompts.cs`)
- `GetSystemPrompt()` - Main AI assistant behavior
- `GetIntentClassificationPrompt()` - Statistical vs conversational classification
- `GetQueryRewritePrompt()` - Context-aware query enhancement
- `GetSqlGenerationPrompt()` - Database schema-driven SQL generation

## Domain Adaptation Guide

To adapt from Formula One to another domain:

1. **Update database schema** in `Nl2SqlConfig`
2. **Modify intent classification** - change "STATISTICAL" to domain-specific term
3. **Update system prompts** in `CorePrompts.cs` to reflect new domain expertise
4. **Adjust frontend status messages** in `Controls.js`

## Development Commands

### Backend (.NET)
```bash
cd realtime-api-dotnet
dotnet restore
dotnet run  # Runs on https://localhost:7254
```

### Frontend (React)
```bash
cd azure-openai-demo
npm install
npm start   # Runs on http://localhost:3000
```

### Database Setup
```bash
cd DatabaseImporter
dotnet run  # Imports CSV files to SQL database
```

## Common Issues & Solutions

- **WebRTC failures**: Check microphone permissions, use Chrome browser
- **Audio quality**: Ensure 24kHz sample rate for Azure OpenAI compatibility  
- **SQL generation errors**: Check `Nl2SqlConfig` schema matches actual database
- **Intent misclassification**: Review conversation history handling in classification prompts

## Package Dependencies

### Backend (.NET 9)
- `Azure.AI.OpenAI` (2.2.0-beta.4) - Azure OpenAI client
- `SQLServerSchemaExtractor` - Database schema extraction for NL2SQL
- `Microsoft.Data.SqlClient` - SQL Server connectivity

### Frontend (React 19)
- Standard React app with WebRTC APIs
- No additional major dependencies

## Security Notes
- Ephemeral keys used for WebRTC sessions
- CORS configured for local development (`AllowReactApp` policy)
- Secrets managed via `appsettings.Local.json` (gitignored)

---
*Last updated: Generated based on codebase analysis*
