# Azure OpenAI Realtime Chat Demo with .NET Backend

This project demonstrates the integration between a React frontend and a .NET API backend to create a real-time voice chat application using Azure OpenAI's Realtime API. The application utilizes WebRTC for bidirectional audio communication and showcases streaming responses from Azure OpenAI's GPT-4o models.

The backend supports answering questions using data stored in a SQL database through Natural Language to SQL (NL2SQL) capabilities. The current implementation uses NBA sports data, but the system can be adapted to any domain by updating the database content and the prompt templates.

> **Note:** This project uses Azure OpenAI's Realtime API (preview), which provides low-latency, streaming interactions with Azure OpenAI models.

## Project Structure

The project consists of two main parts:

1. **React Frontend (`azure-openai-demo`)**
   - A modern React application that handles the user interface and WebRTC communication
   - Provides settings configuration, message display, and recording controls
   
2. **.NET API Backend (`realtime-api-dotnet`)**
   - Serves as a proxy to the Azure OpenAI service
   - Manages session creation and WebRTC connection establishment
   - Handles authentication with Azure OpenAI using API keys
   - Integrates with a SQL database and uses the LLM to peform NL2SQL to answer questions based on the data stored

## Features

- **Real-time Audio Processing**:
  - Voice transcription using Whisper model integration
  - Streaming bidirectional audio communication via WebRTC
  - Multiple voice options (Verse, Alloy, Nova, Shimmer)
  - Low-latency response generation

- **Advanced AI Capabilities**:
  - Real-time streaming responses from Azure OpenAI GPT-4o models
  - Intent classification to determine query type (statistical vs. conversational)
  - Context-aware query rewriting to include chat history information
  - Natural Language to SQL (NL2SQL) for database querying

- **User Experience**:
  - Clean, responsive React-based interface
  - Real-time message transcription display
  - Detailed connection and communication logging
  - Configurable service settings

- **Backend Functionality**:
  - Secure session management with ephemeral keys
  - Database integration for answering data-specific questions
  - Structured prompt management for consistent AI interactions
  - Error handling and retry mechanisms for robust operation

## Prerequisites

- **Development Environment**:
  - Node.js v18+ and npm for the React frontend
  - .NET 9.0+ for the backend API
  - Visual Studio 2022/VS Code or compatible IDE

- **Azure Resources**:
  - Azure subscription with access to Azure OpenAI service
  - Azure OpenAI resource with GPT-4o models deployed
  - Access to Azure OpenAI Realtime API preview features
  - Azure SQL Server instance for database storage

- **Local Requirements**:
  - Microphone access for voice input
  - Modern browser with WebRTC support (Chrome recommended)
  - Network connectivity to Azure services

## Setup and Configuration

### Step 1: Database Setup (Optional)

The `DatabaseImporter` project is a utility to populate an Azure SQL Database with CSV data. This is required only if you need to set up a new database with your data.

1. Navigate to the `DatabaseImporter` directory
2. Update the `appsettings.json` file with your configuration:
   ```json
   {
     "ConnectionStrings": {
       "SqlServer": "Server=your-server.database.windows.net;Database=your-database;User Id=your-username;Password=your-password;Encrypt=True;TrustServerCertificate=False;"
     },
     "DataFileName": "your-data-file.csv"
   }
   ```
3. Place your CSV data file in the `DatabaseImporter` directory
4. Run the importer to populate your database:
   ```bash
   cd DatabaseImporter
   dotnet run
   ```

> **Note:** The importer creates a table called `NBAStats` with an auto-generated schema based on your CSV headers. For production use, consider creating proper table schemas with appropriate data types.

### Step 2: Backend API Setup (.NET)

1. Navigate to the `realtime-api-dotnet` directory
2. Create a local configuration file `appsettings.Local.json` (this file is gitignored):
   ```json
   {
     "AzureOpenAI": {
       "ResourceName": "your-azure-openai-resource-name",
       "RealtimeDeploymentName": "gpt-4o-realtime-preview",
       "ChatDeploymentName": "gpt-4o",
       "ApiKey": "your-azure-openai-api-key",
       "ApiVersion": "2025-04-01-preview"
     },
     "DatabaseConnection": "Server=your-server.database.windows.net;Database=your-database;User Id=your-username;Password=your-password;Encrypt=True;TrustServerCertificate=False;",
     "Nl2SqlConfig": {
       "database": {
         "description": "Stats for NBA players from the 2023-24 season",
         "schemas": [
           {
             "name": "dbo",
             "tables": [
               "NBAStats"
             ]
           }
         ]
       }
     }
   }
   ```

3. Install required packages and run the API:
   ```bash
   cd realtime-api-dotnet
   dotnet restore
   dotnet run
   ```
   The API will be available at `http://localhost:5126` by default

> **Important:** Ensure your Azure OpenAI resource has both a GPT-4o model deployed for chat completion and a GPT-4o Realtime model deployed for the streaming audio functionality.

### Step 3: Frontend Setup (React)

1. Navigate to the `azure-openai-demo` directory
2. Install dependencies:
   ```bash
   cd azure-openai-demo
   npm install
   ```
3. Start the development server:
   ```bash
   npm start
   ```
   The application will be available at `http://localhost:3000`

> **Note:** The frontend is configured to connect to the backend API at `https://localhost:7254` by default. If your backend runs on a different port, update the `REACT_APP_API_URL` environment variable.

### Database Schema Optimization (Optional)

For optimal NL2SQL performance, add column descriptions to your database tables. This helps the AI understand the data structure better:

```sql
-- Example: Adding descriptions to improve NL2SQL accuracy
EXEC sp_addextendedproperty 
  @name = N'MS_Description', 
  @value = N'Player name in the format: FirstName LastName', 
  @level0type = N'SCHEMA', @level0name = 'dbo',
  @level1type = N'TABLE',  @level1name = 'NBAStats',
  @level2type = N'COLUMN', @level2name = 'PlayerName';

EXEC sp_addextendedproperty 
  @name = N'MS_Description', 
  @value = N'Points scored per game during the 2023-24 season', 
  @level0type = N'SCHEMA', @level0name = 'dbo',
  @level1type = N'TABLE',  @level1name = 'NBAStats',
  @level2type = N'COLUMN', @level2name = 'PointsPerGame';
```

## Usage

1. **Start the Application**:
   - Ensure the .NET API is running (`dotnet run` in `realtime-api-dotnet` directory)
   - Launch the React frontend (`npm start` in `azure-openai-demo` directory)
   - Open your browser to `http://localhost:3000`

2. **Configure Settings** (if needed):
   - Set your deployment name (default: `gpt-4o-realtime-preview`)
   - Choose your preferred voice (Verse, Alloy, Nova, or Shimmer)
   - Select your Azure region (East US 2 or Sweden Central)

3. **Start Conversing**:
   - Click "Start Conversation" to establish the WebRTC connection
   - Grant microphone permissions when prompted
   - Speak naturally to interact with the AI assistant
   - The system will automatically classify your question as either:
     - **Statistical**: Queries the database for data-driven answers
     - **Conversational**: Uses the AI's general knowledge

4. **Monitor the Interaction**:
   - View real-time transcription in the chat window
   - Watch streaming AI responses as they're generated
   - Check the logs panel for detailed connection information

## Technical Architecture

The application follows a three-tier architecture:

```
Browser (React) ←→ .NET API ←→ Azure OpenAI Realtime API
                    ↓
              Azure SQL Database
```

**Data Flow:**
1. React frontend captures audio and establishes WebRTC connection via .NET API
2. .NET API creates authenticated session with Azure OpenAI using ephemeral keys
3. Audio streams bidirectionally between browser and Azure OpenAI service
4. For data-related questions, the system:
   - Classifies the intent using Azure OpenAI
   - Rewrites queries with chat context if needed
   - Generates SQL queries using NL2SQL capabilities
   - Executes database queries and incorporates results into AI responses
5. Responses stream back to the user in real-time

## Component Details

### Frontend Components (React)

- **App.js**: Main application component managing global state and component orchestration
- **Settings.js**: Configuration interface for Azure OpenAI service parameters and voice selection
- **ChatWindow.js**: Message display component showing conversation history and real-time transcription
- **Controls.js**: WebRTC connection management, audio processing, and communication orchestration
- **Logs.js**: Debugging and monitoring interface displaying detailed connection logs
- **ApiService.js**: HTTP client service for communicating with the .NET backend

### Backend Components (.NET)

- **AzureOpenAIController.cs**: Main API controller handling:
  - Session creation and management
  - WebRTC connection establishment
  - Intent classification for user queries
  - NL2SQL query processing and execution

- **AzureOpenAiService.cs**: Core service managing:
  - Azure OpenAI client initialization
  - Query classification and rewriting
  - SQL query generation using database schema
  - Chat completion orchestration

- **DatabaseService.cs**: Data access layer providing:
  - SQL query execution with proper error handling
  - Connection management and timeout configuration
  - Result formatting for AI consumption

- **CorePrompts.cs**: Centralized prompt management containing:
  - System prompts for AI behavior configuration
  - Intent classification prompts
  - Query rewriting templates
  - SQL generation instructions

## Development and Customization

### Adapting to Different Data Domains

To customize this application for non-sports data:

1. **Update Database Configuration**:
   ```json
   "Nl2SqlConfig": {
     "database": {
       "description": "Your domain-specific data description",
       "schemas": [
         {
           "name": "your_schema",
           "tables": ["your_table_names"]
         }
       ]
     }
   }
   ```

2. **Modify Intent Classification**:
   - Update `CorePrompts.GetIntentClassificationPrompt()` to reflect your data type
   - Change "STATISTICAL" classification to match your domain (e.g., "FINANCIAL", "MEDICAL", etc.)
   - Adjust the classification logic in `AzureOpenAiService.ClassifyIntent()`

3. **Update System Prompts**:
   - Modify `CorePrompts.GetSystemmPrompt()` to describe your AI assistant's role
   - Update the SQL generation prompts to match your database schema
   - Adjust query rewriting logic for your specific use case

4. **Frontend Adjustments**:
   - Update the intent detection logic in `Controls.js`
   - Modify status messages and UI text to match your domain
   - Adjust the data formatting functions for your result types

### Key Files to Modify

| Component | File Path | Purpose |
|-----------|-----------|---------|
| System Behavior | `Prompts/CorePrompts.cs` | AI assistant behavior and SQL generation |
| Intent Logic | `Services/AzureOpenAiService.cs` | Query classification and processing |
| Frontend Logic | `src/components/Controls.js` | User interaction and data handling |
| Configuration | `appsettings.json` | Service endpoints and database settings |

## Troubleshooting

### Common Issues

1. **WebRTC Connection Fails**:
   - Ensure microphone permissions are granted
   - Check that the backend API is running on the correct port
   - Verify Azure OpenAI service has the Realtime API enabled
   - Try using Chrome browser (recommended for WebRTC compatibility)

2. **Authentication Errors**:
   - Verify your Azure OpenAI API key is correct and active
   - Check that your resource name matches the Azure OpenAI resource
   - Ensure the API version is compatible (`2025-04-01-preview`)

3. **Database Connection Issues**:
   - Verify SQL connection string format and credentials
   - Check that your database server allows connections from your IP
   - Ensure the target database and tables exist

4. **NL2SQL Not Working**:
   - Verify database schema is correctly configured in `Nl2SqlConfig`
   - Add column descriptions using `sp_addextendedproperty` for better results
   - Check that the `SqlDbSchemaExtractor` package is properly installed

### Performance Optimization

- **Audio Quality**: Use 24kHz sample rate for optimal Azure OpenAI compatibility
- **Network**: Ensure stable internet connection for real-time streaming
- **Database**: Add proper indexes on frequently queried columns
- **Caching**: Consider implementing response caching for common queries

## License

This project is for demonstration and learning purposes. Please review Azure OpenAI service terms and pricing before using in production.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes with appropriate tests
4. Submit a pull request with a clear description

## Resources and Documentation

- **Azure OpenAI**:
  - [Azure OpenAI Documentation](https://learn.microsoft.com/en-us/azure/ai-services/openai/)
  - [Realtime API Reference](https://learn.microsoft.com/en-us/azure/ai-services/openai/realtime-audio-quickstart)
  - [GPT-4o Model Information](https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/models#gpt-4o-and-gpt-4-turbo)

- **Web Technologies**:
  - [WebRTC Documentation](https://webrtc.org/)
  - [React Documentation](https://reactjs.org/)
  - [ASP.NET Core Documentation](https://learn.microsoft.com/en-us/aspnet/core/)

- **Database Integration**:
  - [SQL Server Documentation](https://learn.microsoft.com/en-us/sql/sql-server/)
  - [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/)

---

**Last Updated**: June 2025  
**Azure OpenAI API Version**: 2025-04-01-preview  
**Supported Models**: GPT-4o, GPT-4o Realtime Preview
