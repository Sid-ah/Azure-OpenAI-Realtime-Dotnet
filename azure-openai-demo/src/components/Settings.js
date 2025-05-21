import React, { useEffect, useState } from 'react';

function Settings({ settings, setSettings, addLog }) {
  const [voice, setVoice] = useState(settings.voice);
  const [region, setRegion] = useState(settings.region);
  const [deploymentName, setDeploymentName] = useState(settings.deploymentName);
  const [apiVersion, setApiVersion] = useState(settings.apiVersion);
  const [ragEnabled, setRagEnabled] = useState(settings.ragEnabled || false);
  const [ragQuery, setRagQuery] = useState(settings.ragQuery || '');
  const [ragTopK, setRagTopK] = useState(settings.ragTopK || 3);

  const saveSettings = () => {
    const newSettings = {
      ...settings,
      voice,
      region,
      deploymentName,
      apiVersion,
      ragEnabled,
      ragQuery,
      ragTopK
    };
    setSettings(newSettings);
    localStorage.setItem('azureOpenAISettings', JSON.stringify(newSettings));
    addLog('✅ Settings saved');
  };

  useEffect(() => {
    const savedSettings = JSON.parse(localStorage.getItem('azureOpenAISettings') || '{}');
    if (savedSettings.voice) setVoice(savedSettings.voice);
    if (savedSettings.region) setRegion(savedSettings.region);
    if (savedSettings.deploymentName) setDeploymentName(savedSettings.deploymentName);
    if (savedSettings.apiVersion) setApiVersion(savedSettings.apiVersion);
    if (savedSettings.ragEnabled !== undefined) setRagEnabled(savedSettings.ragEnabled);
    if (savedSettings.ragQuery) setRagQuery(savedSettings.ragQuery);
    if (savedSettings.ragTopK) setRagTopK(savedSettings.ragTopK);
    
    if (Object.keys(savedSettings).length > 0) {
      setSettings(prevSettings => ({
        ...prevSettings,
        ...savedSettings
      }));
    }
  }, []);

  return (
    <div className="settings">
      <h2>Settings</h2>
      <div className="form-group">
        <label htmlFor="deploymentName">Deployment Name:</label>
        <input 
          type="text" 
          id="deploymentName" 
          placeholder="e.g., gpt-4o-mini-realtime-preview" 
          value={deploymentName}
          onChange={(e) => setDeploymentName(e.target.value)}
        />
      </div>
      <div className="form-group">
        <label htmlFor="apiVersion">API Version:</label>
        <input 
          type="text" 
          id="apiVersion" 
          value={apiVersion}
          onChange={(e) => setApiVersion(e.target.value)}
        />
      </div>
      <div className="form-group">
        <label htmlFor="voice">Voice:</label>
        <select 
          id="voice" 
          value={voice} 
          onChange={(e) => setVoice(e.target.value)}
        >
          <option value="verse">Verse</option>
          <option value="alloy">Alloy</option>
          <option value="nova">Nova</option>
          <option value="shimmer">Shimmer</option>
        </select>
      </div>
      <div className="form-group">
        <label htmlFor="region">Region:</label>
        <select 
          id="region" 
          value={region} 
          onChange={(e) => setRegion(e.target.value)}
        >
          <option value="eastus2">East US 2</option>
          <option value="swedencentral">Sweden Central</option>
        </select>
      </div>
      
      <h3>RAG Settings</h3>
      <div className="form-group">
        <label htmlFor="ragEnabled">Enable RAG:</label>
        <input 
          type="checkbox" 
          id="ragEnabled" 
          checked={ragEnabled}
          onChange={(e) => setRagEnabled(e.target.checked)}
        />
      </div>
      <div className="form-group">
        <label htmlFor="ragQuery">Context Query:</label>
        <input 
          type="text" 
          id="ragQuery" 
          placeholder="Enter search query for documents" 
          value={ragQuery}
          onChange={(e) => setRagQuery(e.target.value)}
          disabled={!ragEnabled}
        />
      </div>
      <div className="form-group">
        <label htmlFor="ragTopK">Number of Documents:</label>
        <select 
          id="ragTopK" 
          value={ragTopK} 
          onChange={(e) => setRagTopK(parseInt(e.target.value))}
          disabled={!ragEnabled}
        >
          <option value="1">1</option>
          <option value="2">2</option>
          <option value="3">3</option>
          <option value="5">5</option>
          <option value="10">10</option>
        </select>
      </div>
      
      <button id="saveSettings" onClick={saveSettings}>Save Settings</button>
    </div>
  );
}

export default Settings;