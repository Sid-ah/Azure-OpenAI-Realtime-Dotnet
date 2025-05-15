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

## Features

- Real-time voice transcription using Whisper model
- Real-time streaming responses from Azure OpenAI GPT models
- WebRTC audio communication
- Configurable voice selection
- Low-latency communication
- Session management
- Detailed logging for debugging and monitoring

## Prerequisites

- Node.js and npm for the React frontend
- .NET 10+ for the backend API
- Azure subscription with access to Azure OpenAI service
- Azure OpenAI resource with GPT-4o models deployed

## Setup and Configuration

### Backend Setup (.NET API)

1. Navigate to the `realtime-api-dotnet` directory
2. Update the `appsettings.json` file with your Azure OpenAI configuration:
   ```json
   "AzureOpenAI": {
     "ResourceName": "<your-resource-name>",
     "DeploymentName": "gpt-4o-realtime-preview",
     "ApiKey": "<your-api-key>",
     "ApiVersion": "2025-04-01-preview"
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
3. Click "Start Conversation" to begin
4. Speak into your microphone to interact with the AI assistant
5. View real-time transcription and streaming responses
6. Monitor the logs panel for detailed information about the connection and messages

## Architecture

The application uses the following architecture:

1. React frontend establishes a WebRTC connection to Azure OpenAI through the .NET API
2. The .NET API creates a session with Azure OpenAI and handles authentication
3. Audio is streamed from the browser to Azure OpenAI's real-time service
4. Transcription and AI responses are streamed back to the frontend

## Component Details

### Frontend Components

- **Settings**: Configures Azure OpenAI service parameters
- **ChatWindow**: Displays conversation messages and streaming responses
- **Controls**: Manages WebRTC connection and recording
- **Logs**: Shows detailed log information for debugging

### Backend Controllers

- **AzureOpenAIController**: Handles session creation and WebRTC connection

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
