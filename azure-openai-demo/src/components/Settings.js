import React, { useEffect, useState } from 'react';

function Settings({ settings, setSettings, addLog }) {
  const [voice, setVoice] = useState(settings.voice);
  const [region, setRegion] = useState(settings.region);
  const [deploymentName, setDeploymentName] = useState(settings.deploymentName);
  const [apiVersion, setApiVersion] = useState(settings.apiVersion);

  const saveSettings = () => {
    const newSettings = {
      ...settings,
      voice,
      region,
      deploymentName,
      apiVersion
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
      <button id="saveSettings" onClick={saveSettings}>Save Settings</button>
    </div>
  );
}

export default Settings;