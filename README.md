# Azure OpenAI Realtime Chat Demo

This project demonstrates the integration between a React frontend and a .NET API backend to create a real-time chat application using Azure OpenAI's Realtime API. The application utilizes WebRTC for voice communication and showcases streaming responses from Azure OpenAI models.

## Project Structure

The project consists of two main parts:

1. **React Frontend (`azure-openai-demo`)**
   - A modern React application that handles the user interface and WebRTC communication
   - Provides settings configuration, message display, and recording controls
   
2. **.NET API Backend (`realtime-api-dotnet`)**
   - Serves as a proxy to the Azure OpenAI service
   - Manages session creation and WebRTC connection establishment
   - Handles authentication with Azure OpenAI using API keys
   - Provides Retrieval-Augmented Generation (RAG) capabilities with Azure Cognitive Search

## Features

- Real-time voice transcription using Whisper model
- Real-time streaming responses from Azure OpenAI GPT models
- WebRTC audio communication
- Configurable voice selection
- Low-latency communication
- Session management
- Detailed logging for debugging and monitoring
- Retrieval-Augmented Generation (RAG) for contextual responses

## Prerequisites

- Node.js and npm for the React frontend
- .NET 10+ for the backend API
- Azure subscription with access to Azure OpenAI service
- Azure OpenAI resource with GPT-4o models deployed
- Azure Cognitive Search service for RAG functionality

## Setup and Configuration

### Backend Setup (.NET API)

1. Navigate to the `realtime-api-dotnet` directory
2. Update the `appsettings.json` file with your Azure OpenAI and Cognitive Search configuration:
   ```json
   {
     "AzureOpenAI": {
       "ResourceName": "<your-resource-name>",
       "DeploymentName": "gpt-4o-realtime-preview",
       "ApiKey": "<your-api-key>",
       "ApiVersion": "2025-04-01-preview",
       "EmbeddingDeployment": "text-embedding-ada-002",
       "Endpoint": "https://<your-resource-name>.openai.azure.com/"
     },
     "AzureCognitiveSearch": {
       "Endpoint": "https://<your-search-service>.search.windows.net",
       "ApiKey": "<your-search-api-key>",
       "IndexName": "documents"
     }
   }
   ```
3. Run the API:
   ```
   dotnet run
   ```
   The API will be available at http://localhost:5126 by default

### Frontend Setup (React)

1. Navigate to the `azure-openai-demo` directory
2. Install dependencies:
   ```
   npm install
   ```
3. Start the development server:
   ```
   npm start
   ```
   The application will be available at http://localhost:3000

## Usage

1. Open the web application at http://localhost:3000
2. Configure your Azure OpenAI settings if needed
3. Configure RAG settings (enable RAG, specify search query)
4. Click "Start Conversation" to begin
5. Speak into your microphone to interact with the AI assistant
6. View real-time transcription and streaming responses
7. Monitor the logs panel for detailed information about the connection and messages

## Architecture

The application uses the following architecture:

1. React frontend establishes a WebRTC connection to Azure OpenAI through the .NET API
2. The .NET API creates a session with Azure OpenAI and handles authentication
3. When RAG is enabled, relevant documents are retrieved from Azure Cognitive Search
4. Retrieved document content is used as context for the conversation
5. Audio is streamed from the browser to Azure OpenAI's real-time service
6. Transcription and AI responses (augmented with document context if RAG is enabled) are streamed back to the frontend

## RAG Integration

The RAG (Retrieval-Augmented Generation) functionality enhances the AI assistant by:

1. Storing document content in Azure Cognitive Search with vector embeddings
2. Retrieving relevant documents based on user queries using semantic search
3. Providing document content as context to the Azure OpenAI model
4. Generating responses that incorporate knowledge from the retrieved documents

## Component Details

### Frontend Components

- **Settings**: Configures Azure OpenAI service parameters and RAG settings
- **ChatWindow**: Displays conversation messages and streaming responses
- **Controls**: Manages WebRTC connection and recording
- **Logs**: Shows detailed log information for debugging

### Backend Controllers

- **AzureOpenAIController**: Handles session creation, WebRTC connection, and RAG context integration
- **DocumentsController**: Manages document uploading and retrieval for RAG functionality

### Backend Services

- **CognitiveSearchService**: Handles document indexing, embedding generation, and semantic search

## Development

To modify or extend the application:

1. The React components in `azure-openai-demo/src/components` manage different aspects of the UI
2. The `ApiService.js` in `azure-openai-demo/src/services` handles communication with the .NET API
3. The .NET API's `AzureOpenAIController.cs` manages communication with Azure OpenAI

## License

This project is for demonstration and learning purposes.

## Resources

- [Azure OpenAI Documentation](https://learn.microsoft.com/en-us/azure/ai-services/openai/)
- [WebRTC Documentation](https://webrtc.org/)
- [React Documentation](https://reactjs.org/)
- [ASP.NET Core Documentation](https://learn.microsoft.com/en-us/aspnet/core/)
