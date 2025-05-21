// App.js
import React, { useState } from 'react';
import Settings from './components/Settings';
import ChatWindow from './components/ChatWindow';
import Controls from './components/Controls';
import Logs from './components/Logs';
import './App.css';

function App() {
  const [logs, setLogs] = useState([]);
  const [status, setStatus] = useState('Idle');
  const [isConnected, setIsConnected] = useState(false);
  const [settings, setSettings] = useState({
    voice: 'verse',
    region: 'eastus2',
    deploymentName: 'gpt-4o-mini-realtime-preview',
    apiVersion: '2025-04-01-preview',
    ragEnabled: false,
    ragQuery: '',
    ragTopK: 3
  });
  const [messages, setMessages] = useState([]);
  const [currentTranscript, setCurrentTranscript] = useState('');

  const addLog = (msg) => {
    const ts = new Date().toLocaleTimeString();
    setLogs(prevLogs => [...prevLogs, `[${ts}] ${msg}`]);
  };

  const updateStatus = (msg) => {
    setStatus(msg);
    addLog(`Status → ${msg}`);
  };

  const addMessage = (sender, text = '') => {
    // Only add messages with actual content
    if (text && text.trim()) {
      addLog(`Adding ${sender} message to history`);
      setMessages(prevMessages => [...prevMessages, { sender, text }]);
    }
  };

  const updateAssistantMessage = (delta) => {
  // Append incoming delta to current transcript
  setCurrentTranscript(prev => prev + delta);
  };

  return (
    <div className="container">
      <h1>Azure OpenAI Realtime API Demo</h1>
      
      <Settings 
        settings={settings} 
        setSettings={setSettings} 
        addLog={addLog} 
      />

      <ChatWindow 
        messages={messages} 
        currentTranscript={currentTranscript} 
      />
      
      <Controls 
        isConnected={isConnected}
        setIsConnected={setIsConnected}
        updateStatus={updateStatus}
        addLog={addLog}
        settings={settings}
        addMessage={addMessage}
        updateAssistantMessage={updateAssistantMessage}
        setCurrentTranscript={setCurrentTranscript}
        currentTranscript={currentTranscript}
        status={status}
      />

      <Logs logs={logs} />
    </div>
  );
}

export default App;