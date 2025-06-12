# Azure OpenAI Realtime Chat Demo

This project demonstrates the integration between a React frontend and a .NET API backend to create a real-time chat application using Azure OpenAI's Realtime API. The application utilizes WebRTC for voice communication and showcases streaming responses from Azure OpenAI models.

The backend supports answering questions using data stored in a SQL database. The current implementation uses sports data, but the system can be adapted to any domain by updating the database content and the prompt templates.

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

- Real-time voice transcription using Whisper model
- Real-time streaming responses from Azure OpenAI GPT models
- WebRTC audio communication
- Configurable voice selection
- Low-latency communication
- Session management
- Detailed logging for debugging and monitoring
- Intent recognition from the user's question to determine if the database shoould be queried or if the question should be answered directly by the LLM

## Prerequisites

- Node.js and npm for the React frontend
- .NET 10+ for the backend API
- Azure subscription with access to Azure OpenAI service
- Azure OpenAI resource with GPT-4o models deployed
- Azure SQL Server with sports data or your own dataset

## Setup and Configuration

### Backend Setup (SQL Database)
Note: This project is only needed if you need to populate an Azure SQL Database. It will work with CSV files and import this data into your Azure SQL database.
1. Navigate to the `DatabaseImporter` directory
2. Update the `appsettings.json` file with your SQL database connection string and the name of the CSV data file:
   ```json
   "ConnectionStrings": {
     "SqlServer": "<your-sql-connection-string>"
   },
   "DataFileName": "<filename.csv>"
   ```
3. Place your data file in the `DatabaseImporter` directory.
4. Run the importer:
   ```
   dotnet run
   ```
   This will read the CSV file and populate the SQL database with the data.

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
3. Update the `appsettings.json` file with your SQL database configuration:
   ```json
   "DatabaseConnection": "<your-sql-connection-string>",
   "Nl2SqlConfig": {
     "database": {
       "description": "<your discription>",
       "schema": {
         "name": "<schema-name>",
         "tables": [
           "<your-table-name>"
         ]
       }
     }
   }
   ````
4. Ensure your database is seeded with data.
5. Run the API:
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
3. The backend queries the SQL database to retrieve relevant data for user questions and incorporates it into the AI prompt
4. Audio is streamed from the browser to Azure OpenAI's real-time service
5. Transcription and AI responses are streamed back to the frontend

## Component Details

### Frontend Components

- **Settings**: Configures Azure OpenAI service parameters
- **ChatWindow**: Displays conversation messages and streaming responses
- **Controls**: Manages WebRTC connection and recording
- **Logs**: Shows detailed log information for debugging

### Backend Controllers

- **AzureOpenAIController**: Handles session creation, WebRTC connection, intent classification of user questions, and NL2SQL queries

## Development

To modify or extend the application:

1. The React components in `azure-openai-demo/src/components` manage different aspects of the UI
2. The Controls.js component makes a call to the classify-intent backend API to determine if the question is statistical or not. If your data is not statistical, you should update the intent from "Statistical" to an intent matching your data.
3. The `ApiService.js` in `azure-openai-demo/src/services` handles communication with the .NET API
4. The .NET API's `AzureOpenAIController.cs` manages communication with Azure OpenAI
5. If your data is not statistical data, you should update prompt templates in Prompts\CorePrompts.cs to match the type of your data. Additionally, the ClassifyIntent function in Services\AzureOpenAIService.cs determines if the question is "statistical". If you are using another type of data, you should update this to match your data.

## License

This project is for demonstration and learning purposes.

## Resources

- [Azure OpenAI Documentation](https://learn.microsoft.com/en-us/azure/ai-services/openai/)
- [WebRTC Documentation](https://webrtc.org/)
- [React Documentation](https://reactjs.org/)
- [ASP.NET Core Documentation](https://learn.microsoft.com/en-us/aspnet/core/)
